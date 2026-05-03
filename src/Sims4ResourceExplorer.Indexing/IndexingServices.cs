using Microsoft.Data.Sqlite;
using LlamaLogic.Packages;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;
using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;

namespace Sims4ResourceExplorer.Indexing;

public sealed class FileSystemCacheService : ICacheService
{
    public FileSystemCacheService()
    {
        AppRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sims4ResourceExplorer");
        CacheRoot = Path.Combine(AppRoot, "Cache");
        ExportRoot = Path.Combine(AppRoot, "Exports");
        DatabasePath = Path.Combine(CacheRoot, "index.sqlite");
    }

    public string AppRoot { get; }
    public string CacheRoot { get; }
    public string ExportRoot { get; }
    public string DatabasePath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(AppRoot);
        Directory.CreateDirectory(CacheRoot);
        Directory.CreateDirectory(ExportRoot);
    }
}

public sealed class SqliteIndexStore : IIndexStore
{
    private const string SeedFactContentVersionMetadataKey = "seed_fact_content_version";
    // Bump this whenever seed-fact extraction changes shape OR when an earlier deployment
    // wiped fact tables without re-triggering a rescan. The version migration also clears
    // package fingerprints so NeedsRescanAsync returns true for every package on the next
    // index run, which forces seed facts to be re-extracted instead of staying empty until
    // a package's mtime happens to change.
    private const string SeedFactContentVersion = "2026-05-01.seed-facts-v2";
    private const string SecondaryIndexesSql =
        """
        CREATE UNIQUE INDEX IF NOT EXISTS ux_resources_id ON resources(id);
        CREATE UNIQUE INDEX IF NOT EXISTS ux_assets_id ON assets(id);
        CREATE INDEX IF NOT EXISTS ix_resources_search ON resources(type_name, full_tgi, package_path, name);
        CREATE INDEX IF NOT EXISTS ix_resources_source ON resources(data_source_id, package_path);
        CREATE INDEX IF NOT EXISTS ix_resources_package_instance ON resources(package_path, instance_hex, type_name, full_tgi);
        CREATE INDEX IF NOT EXISTS ix_resources_instance_lookup ON resources(instance_hex, type_name, package_path, full_tgi);
        CREATE INDEX IF NOT EXISTS ix_assets_search ON assets(asset_kind, is_canonical, display_name, package_path);
        CREATE INDEX IF NOT EXISTS ix_assets_source ON assets(data_source_id, package_path);
        CREATE INDEX IF NOT EXISTS ix_assets_canonical_root ON assets(data_source_id, asset_kind, root_tgi, is_canonical);
        CREATE INDEX IF NOT EXISTS ix_assets_canonical_logical_root ON assets(data_source_id, asset_kind, logical_root_tgi, is_canonical);
        CREATE INDEX IF NOT EXISTS ix_asset_variants_asset ON asset_variants(asset_id, variant_kind, variant_index);
        CREATE INDEX IF NOT EXISTS ix_asset_variants_source ON asset_variants(data_source_id, package_path);
        CREATE INDEX IF NOT EXISTS ix_sim_template_facts_archetype ON sim_template_facts(archetype_key, authoritative_body_driving_outfit_count, authoritative_body_driving_outfit_part_count);
        CREATE INDEX IF NOT EXISTS ix_sim_template_facts_package ON sim_template_facts(data_source_id, package_path);
        CREATE INDEX IF NOT EXISTS ix_sim_template_body_parts_resource ON sim_template_body_parts(resource_id, is_body_driving, body_type, part_instance_hex);
        CREATE INDEX IF NOT EXISTS ix_sim_template_body_parts_package ON sim_template_body_parts(data_source_id, package_path);
        CREATE INDEX IF NOT EXISTS ix_cas_part_facts_asset ON cas_part_facts(asset_id, body_type, slot_category);
        CREATE INDEX IF NOT EXISTS ix_cas_part_facts_lookup ON cas_part_facts(body_type, species_label, age_label, gender_label, has_naked_link, default_body_type, default_body_type_female, default_body_type_male);
        CREATE INDEX IF NOT EXISTS ix_cas_part_facts_package ON cas_part_facts(data_source_id, package_path);
        """;
    private const string ShadowDatabasePattern = "index.building.*.sqlite";
    private const int CatalogShardCount = 4;
    private readonly ICacheService cacheService;
    private enum SqliteConnectionProfile
    {
        LiveServing,
        ShadowBuild
    }

    private enum SqliteWriteSessionMode
    {
        LiveReplace,
        ShadowRebuild
    }

    public SqliteIndexStore(ICacheService cacheService)
    {
        this.cacheService = cacheService;
    }

    private string GetShardPath(int shardIndex) =>
        shardIndex == 0
            ? cacheService.DatabasePath
            : Path.Combine(cacheService.CacheRoot, $"index.shard{shardIndex:00}.sqlite");

    private IReadOnlyList<string> GetServingDatabasePaths()
    {
        var paths = Enumerable.Range(0, CatalogShardCount)
            .Select(GetShardPath)
            .Where(File.Exists)
            .ToArray();
        return paths.Length == 0 ? [cacheService.DatabasePath] : paths;
    }

    private string GetServingDatabasePathForPackage(Guid dataSourceId, string packagePath)
    {
        var servingPaths = GetServingDatabasePaths();
        if (servingPaths.Count <= 1)
        {
            return servingPaths[0];
        }

        return GetShardPath(GetShardIndex(packagePath));
    }

    private string ResolvePackageScopedDatabasePath(string packagePath)
    {
        var servingPaths = GetServingDatabasePaths();
        return servingPaths.Count <= 1
            ? servingPaths[0]
            : GetShardPath(GetShardIndex(packagePath));
    }

    private static int GetShardIndex(string packagePath)
    {
        var input = Encoding.UTF8.GetBytes(packagePath);
        var hash = System.Security.Cryptography.MD5.HashData(input);
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % CatalogShardCount);
    }

    private string[] CreateShadowShardPaths()
    {
        var token = Guid.NewGuid().ToString("N");
        return Enumerable.Range(0, CatalogShardCount)
            .Select(index => Path.Combine(cacheService.CacheRoot, $"index.building.{token}.shard{index:00}.sqlite"))
            .ToArray();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cacheService.EnsureCreated();
        foreach (var databasePath in GetServingDatabasePaths())
        {
            await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
            await EnsureSchemaAsync(connection, includeSearchStructures: true, cancellationToken);
        }
    }

    public async Task UpsertDataSourcesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken)
    {
        var materializedSources = sources.ToArray();
        foreach (var databasePath in GetServingDatabasePaths())
        {
            await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
            await UpsertDataSourcesAsync(connection, materializedSources, cancellationToken);
        }
    }

    private static async Task UpsertDataSourcesAsync(SqliteConnection connection, IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken)
    {
        var materializedSources = sources.ToArray();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var source in materializedSources)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO data_sources(id, display_name, root_path, kind, is_enabled)
                VALUES ($id, $displayName, $rootPath, $kind, $isEnabled)
                ON CONFLICT(id) DO UPDATE SET
                    display_name = excluded.display_name,
                    root_path = excluded.root_path,
                    kind = excluded.kind,
                    is_enabled = excluded.is_enabled;
                """;
            command.Parameters.AddWithValue("$id", source.Id.ToString("D"));
            command.Parameters.AddWithValue("$displayName", source.DisplayName);
            command.Parameters.AddWithValue("$rootPath", source.RootPath);
            command.Parameters.AddWithValue("$kind", source.Kind.ToString());
            command.Parameters.AddWithValue("$isEnabled", source.IsEnabled ? 1 : 0);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            if (materializedSources.Length == 0)
            {
                deleteCommand.CommandText = "DELETE FROM data_sources;";
            }
            else
            {
                var parameterNames = materializedSources
                    .Select(static (_, index) => $"$keep{index}")
                    .ToArray();
                deleteCommand.CommandText = $"DELETE FROM data_sources WHERE id NOT IN ({string.Join(", ", parameterNames)});";
                for (var index = 0; index < materializedSources.Length; index++)
                {
                    deleteCommand.Parameters.AddWithValue(parameterNames[index], materializedSources[index].Id.ToString("D"));
                }
            }

            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, PackageFingerprint>> LoadPackageFingerprintsAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken)
    {
        var sourceIdSet = dataSourceIds.Distinct().ToArray();
        if (sourceIdSet.Length == 0)
        {
            return new Dictionary<string, PackageFingerprint>(StringComparer.OrdinalIgnoreCase);
        }

        var fingerprints = new Dictionary<string, PackageFingerprint>(StringComparer.OrdinalIgnoreCase);
        var shardResults = await Task.WhenAll(GetServingDatabasePaths().Select(path => LoadPackageFingerprintsFromDatabaseAsync(path, sourceIdSet, cancellationToken)));
        foreach (var shard in shardResults)
        {
            foreach (var pair in shard)
            {
                fingerprints[pair.Key] = pair.Value;
            }
        }

        return fingerprints;
    }

    public async Task<bool> NeedsRescanAsync(Guid dataSourceId, string packagePath, long fileSize, DateTimeOffset lastWriteTimeUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(GetServingDatabasePathForPackage(dataSourceId, packagePath), SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT file_size, last_write_utc
            FROM packages
            WHERE data_source_id = $dataSourceId AND package_path = $packagePath;
            """;
        command.Parameters.AddWithValue("$dataSourceId", dataSourceId.ToString("D"));
        command.Parameters.AddWithValue("$packagePath", packagePath);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return true;
        }

        var existingSize = reader.GetInt64(0);
        var existingLastWrite = DateTimeOffset.Parse(reader.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind);

        return existingSize != fileSize || existingLastWrite != lastWriteTimeUtc;
    }

    public async Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
    {
        await using var session = await OpenWriteSessionAsync(cancellationToken);
        await session.ReplacePackageAsync(packageScan, assets, cancellationToken);
        await session.FinalizeAsync(progress: null, cancellationToken);
    }

    public async Task<IIndexWriteSession> OpenWriteSessionAsync(CancellationToken cancellationToken)
    {
        var servingPaths = GetServingDatabasePaths();
        if (servingPaths.Count <= 1)
        {
            var connection = await OpenConnectionAsync(servingPaths[0], SqliteConnectionProfile.LiveServing, cancellationToken);
            var session = new SqliteIndexWriteSession(connection, SqliteWriteSessionMode.LiveReplace);
            await session.PrepareForBulkWriteAsync(cancellationToken);
            return session;
        }

        var shardSessions = new SqliteIndexWriteSession[servingPaths.Count];
        try
        {
            for (var index = 0; index < servingPaths.Count; index++)
            {
                var connection = await OpenConnectionAsync(servingPaths[index], SqliteConnectionProfile.LiveServing, cancellationToken);
                var session = new SqliteIndexWriteSession(connection, SqliteWriteSessionMode.LiveReplace);
                await session.PrepareForBulkWriteAsync(cancellationToken);
                shardSessions[index] = session;
            }

            return new ShardedIndexWriteSession(shardSessions, null, null);
        }
        catch
        {
            foreach (var session in shardSessions.Where(static session => session is not null))
            {
                await session.DisposeAsync();
            }

            throw;
        }
    }

    public async Task<IIndexWriteSession> OpenRebuildSessionAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken)
    {
        cacheService.EnsureCreated();
        CleanupShadowDatabases();
        var materializedSources = sources.ToArray();
        var shadowDatabasePaths = CreateShadowShardPaths();
        var shardSessions = new SqliteIndexWriteSession[shadowDatabasePaths.Length];
        try
        {
            for (var index = 0; index < shadowDatabasePaths.Length; index++)
            {
                var connection = await OpenConnectionAsync(shadowDatabasePaths[index], SqliteConnectionProfile.ShadowBuild, cancellationToken);
                await EnsureSchemaAsync(connection, includeSearchStructures: false, cancellationToken);
                await UpsertDataSourcesAsync(connection, materializedSources, cancellationToken);
                var session = new SqliteIndexWriteSession(connection, SqliteWriteSessionMode.ShadowRebuild);
                await session.PrepareForBulkWriteAsync(cancellationToken);
                shardSessions[index] = session;
            }

            return new ShardedIndexWriteSession(shardSessions, shadowDatabasePaths, ActivateShadowDatabaseSetAsync);
        }
        catch
        {
            foreach (var session in shardSessions.Where(static session => session is not null))
            {
                await session.DisposeAsync();
            }

            foreach (var shadowDatabasePath in shadowDatabasePaths)
            {
                TryDeleteDatabaseArtifacts(shadowDatabasePath);
            }

            throw;
        }
    }

    public async Task<WindowedQueryResult<ResourceMetadata>> QueryResourcesAsync(RawResourceBrowserQuery query, CancellationToken cancellationToken)
    {
        var servingPaths = GetServingDatabasePaths();
        if (servingPaths.Count <= 1)
        {
            return await QueryResourcesFromDatabaseAsync(servingPaths[0], query, cancellationToken);
        }

        var shardQuery = query with { Offset = 0, WindowSize = query.Offset + query.WindowSize };
        var shardResults = await Task.WhenAll(servingPaths.Select(path => QueryResourcesFromDatabaseAsync(path, shardQuery, cancellationToken)));
        var items = OrderByResource(
                shardResults.SelectMany(static result => result.Items),
                query.Sort)
            .Skip(query.Offset)
            .Take(query.WindowSize)
            .ToArray();
        return new WindowedQueryResult<ResourceMetadata>(items, shardResults.Sum(static result => result.TotalCount), query.Offset, query.WindowSize);
    }

    public async Task<WindowedQueryResult<AssetSummary>> QueryAssetsAsync(AssetBrowserQuery query, CancellationToken cancellationToken)
    {
        var servingPaths = GetServingDatabasePaths();
        if (servingPaths.Count <= 1)
        {
            return await QueryAssetsFromDatabaseAsync(servingPaths[0], query, cancellationToken);
        }

        var shardQuery = query with { Offset = 0, WindowSize = query.Offset + query.WindowSize };
        var shardResults = await Task.WhenAll(servingPaths.Select(path => QueryAssetsFromDatabaseAsync(path, shardQuery, cancellationToken)));
        var items = OrderByAsset(
                shardResults.SelectMany(static result => result.Items),
                query.Sort)
            .Skip(query.Offset)
            .Take(query.WindowSize)
            .ToArray();
        return new WindowedQueryResult<AssetSummary>(items, shardResults.Sum(static result => result.TotalCount), query.Offset, query.WindowSize);
    }

    public async Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken)
    {
        var databasePath = GetServingDatabasePaths().FirstOrDefault() ?? cacheService.DatabasePath;
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, display_name, root_path, kind, is_enabled FROM data_sources ORDER BY kind, display_name;";

        var results = new List<DataSourceDefinition>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new DataSourceDefinition(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                Enum.Parse<SourceKind>(reader.GetString(3)),
                reader.GetInt32(4) == 1));
        }

        return results;
    }

    public async Task<AssetFacetOptions> GetAssetFacetOptionsAsync(AssetKind assetKind, CancellationToken cancellationToken)
    {
        var kind = assetKind.ToString();
        var servingPaths = GetServingDatabasePaths();
        var facetTasks = servingPaths.Select(path => ReadAssetFacetOptionsFromDatabaseAsync(path, kind, cancellationToken)).ToArray();
        var shardFacets = await Task.WhenAll(facetTasks);
        return new AssetFacetOptions(
            shardFacets.SelectMany(static facet => facet.Categories).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            shardFacets.SelectMany(static facet => facet.RootTypeNames).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            shardFacets.SelectMany(static facet => facet.IdentityTypes).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            shardFacets.SelectMany(static facet => facet.PrimaryGeometryTypes).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            shardFacets.SelectMany(static facet => facet.ThumbnailTypeNames).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            shardFacets.SelectMany(static facet => facet.CatalogSignal0020Values ?? []).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            shardFacets.SelectMany(static facet => facet.CatalogSignal002CValues ?? []).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            shardFacets.SelectMany(static facet => facet.CatalogSignal0030Values ?? []).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            shardFacets.SelectMany(static facet => facet.CatalogSignal0034Values ?? []).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<IReadOnlyList<IndexedPackageRecord>> GetIndexedPackagesAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken)
    {
        var sourceIds = dataSourceIds.ToArray();
        var results = await Task.WhenAll(GetServingDatabasePaths().Select(path => GetIndexedPackagesFromDatabaseAsync(path, sourceIds, cancellationToken)));
        return results
            .SelectMany(static items => items)
            .OrderBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken)
    {
        var databasePath = ResolvePackageScopedDatabasePath(packagePath);
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint
            FROM resources
            WHERE package_path = $packagePath
            ORDER BY type_name, full_tgi;
            """;
        command.Parameters.AddWithValue("$packagePath", packagePath);

        return await ReadResourcesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceMetadata>> GetResourcesByInstanceAsync(string packagePath, ulong fullInstance, CancellationToken cancellationToken)
    {
        var databasePath = ResolvePackageScopedDatabasePath(packagePath);
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint
            FROM resources
            WHERE package_path = $packagePath AND instance_hex = $instanceHex
            ORDER BY type_name, full_tgi;
            """;
        command.Parameters.AddWithValue("$packagePath", packagePath);
        command.Parameters.AddWithValue("$instanceHex", fullInstance.ToString("X16"));

        return await ReadResourcesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceMetadata>> GetResourcesByFullInstanceAsync(ulong fullInstance, CancellationToken cancellationToken)
    {
        var shardResults = await Task.WhenAll(
            GetServingDatabasePaths().Select(path => GetResourcesByFullInstanceFromDatabaseAsync(path, fullInstance, cancellationToken)));
        return OrderByResource(
                shardResults.SelectMany(static items => items),
                RawResourceSort.Tgi)
            .ToArray();
    }

    public async Task<IReadOnlyList<ResourceMetadata>> GetCasPartResourcesByInstancesAsync(IEnumerable<ulong> fullInstances, CancellationToken cancellationToken)
    {
        var distinctInstances = fullInstances
            .Distinct()
            .ToArray();
        if (distinctInstances.Length == 0)
        {
            return [];
        }

        var shardResults = await Task.WhenAll(
            GetServingDatabasePaths().Select(path => GetCasPartResourcesByInstancesFromDatabaseAsync(path, distinctInstances, cancellationToken)));
        return OrderByResource(
                shardResults.SelectMany(static items => items),
                RawResourceSort.Tgi)
            .ToArray();
    }

    public async Task<IReadOnlyList<AssetSummary>> GetPackageAssetsAsync(string packagePath, CancellationToken cancellationToken)
    {
        var databasePath = ResolvePackageScopedDatabasePath(packagePath);
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, asset_kind, display_name, category, description, package_path, root_tgi, logical_root_tgi, thumbnail_tgi,
                   variant_count, linked_resource_count, has_scene_root, has_exact_geometry_candidate, has_material_references, has_texture_references, diagnostics,
                   package_name, root_type_name, thumbnail_type_name, primary_geometry_type, identity_type, category_normalized,
                   has_identity_metadata, has_rig_reference, has_geometry_reference, has_material_resource_candidate, has_texture_resource_candidate, is_package_local_graph, has_diagnostics,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034
            FROM assets
            WHERE package_path = $packagePath
            ORDER BY display_name, root_tgi;
            """;
        command.Parameters.AddWithValue("$packagePath", packagePath);
        return await ReadAssetsAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<ResourceMetadata>> GetResourcesByFullInstanceFromDatabaseAsync(
        string databasePath,
        ulong fullInstance,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint
            FROM resources
            WHERE instance_hex = $instanceHex
            ORDER BY type_name, package_path, full_tgi;
            """;
        command.Parameters.AddWithValue("$instanceHex", fullInstance.ToString("X16"));
        return await ReadResourcesAsync(command, cancellationToken);
    }

    public async Task<AssetSummary?> GetPackageAssetByIdAsync(string packagePath, Guid assetId, CancellationToken cancellationToken)
    {
        var databasePath = ResolvePackageScopedDatabasePath(packagePath);
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, asset_kind, display_name, category, description, package_path, root_tgi, logical_root_tgi, thumbnail_tgi,
                   variant_count, linked_resource_count, has_scene_root, has_exact_geometry_candidate, has_material_references, has_texture_references, diagnostics,
                   package_name, root_type_name, thumbnail_type_name, primary_geometry_type, identity_type, category_normalized,
                   has_identity_metadata, has_rig_reference, has_geometry_reference, has_material_resource_candidate, has_texture_resource_candidate, is_package_local_graph, has_diagnostics,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034
            FROM assets
            WHERE package_path = $packagePath AND id = $assetId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$packagePath", packagePath);
        command.Parameters.AddWithValue("$assetId", assetId.ToString());
        return (await ReadAssetsAsync(command, cancellationToken)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<AssetVariantSummary>> GetAssetVariantsAsync(Guid assetId, CancellationToken cancellationToken)
    {
        var shardResults = await Task.WhenAll(GetServingDatabasePaths().Select(path => GetAssetVariantsFromDatabaseAsync(path, assetId, cancellationToken)));
        return shardResults
            .SelectMany(static items => items)
            .OrderBy(static item => item.VariantKind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.VariantIndex)
            .ToArray();
    }

    public async Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken)
    {
        var results = await QueryResourcesAsync(
            new RawResourceBrowserQuery(
                new SourceScope(),
                fullTgi,
                RawResourceDomain.All,
                string.Empty,
                packagePath,
                string.Empty,
                string.Empty,
                false,
                false,
                false,
                ResourceLinkFilter.Any,
                RawResourceSort.Tgi,
                0,
                1),
            cancellationToken);
        return results.Items.FirstOrDefault(resource => resource.Key.FullTgi.Equals(fullTgi, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<ResourceMetadata>> GetResourcesByTgiAsync(string fullTgi, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(GetServingDatabasePaths().Select(path => GetResourcesByTgiFromDatabaseAsync(path, fullTgi, cancellationToken)));
        return OrderByResource(
                results.SelectMany(static items => items),
                RawResourceSort.Tgi)
            .ToArray();
    }

    public async Task<IReadOnlyList<SimTemplateFactSummary>> GetSimTemplateFactsByArchetypeAsync(string archetypeKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(archetypeKey))
        {
            return [];
        }

        var shardResults = await Task.WhenAll(
            GetServingDatabasePaths().Select(path => GetSimTemplateFactsByArchetypeFromDatabaseAsync(path, archetypeKey, cancellationToken)));
        return shardResults
            .SelectMany(static items => items)
            .OrderBy(static item => item.ArchetypeKey, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(static item => item.AuthoritativeBodyDrivingOutfitCount)
            .ThenByDescending(static item => item.AuthoritativeBodyDrivingOutfitPartCount)
            .ThenByDescending(static item => item.OutfitPartCount)
            .ThenBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static item => item.RootTgi, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<AssetSummary>> GetIndexedDefaultBodyRecipeAssetsAsync(SimInfoSummary metadata, string slotCategory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slotCategory))
        {
            return [];
        }

        var shardResults = await Task.WhenAll(
            GetServingDatabasePaths().Select(path => GetIndexedDefaultBodyRecipeAssetsFromDatabaseAsync(path, metadata, slotCategory, cancellationToken)));
        return shardResults
            .SelectMany(static items => items)
            .GroupBy(static asset => asset.Id)
            .Select(static group => group.First())
            .OrderBy(static asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static asset => asset.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<BodyRecipeAvailabilitySnapshot> ProbeBodyRecipeAvailabilityAsync(SimInfoSummary metadata, CancellationToken cancellationToken)
    {
        var shardResults = await Task.WhenAll(
            GetServingDatabasePaths().Select(path => ProbeBodyRecipeAvailabilityFromDatabaseAsync(path, metadata, cancellationToken)));
        long Total = 0, B5 = 0, B5Slot = 0, B5Species = 0, B5Age = 0, B5Gender = 0, B5AllLabels = 0;
        long Def = 0, DefF = 0, DefM = 0, NakedAny = 0, B5Def = 0, B5Naked = 0;
        foreach (var snapshot in shardResults)
        {
            Total += snapshot.TotalFacts;
            B5 += snapshot.BodyType5Total;
            B5Slot += snapshot.BodyType5SlotMatch;
            B5Species += snapshot.BodyType5SpeciesMatch;
            B5Age += snapshot.BodyType5AgeMatch;
            B5Gender += snapshot.BodyType5GenderMatch;
            B5AllLabels += snapshot.BodyType5AllLabelsMatch;
            Def += snapshot.DefaultBodyTypeAny;
            DefF += snapshot.DefaultBodyTypeFemaleAny;
            DefM += snapshot.DefaultBodyTypeMaleAny;
            NakedAny += snapshot.HasNakedLinkAny;
            B5Def += snapshot.BodyType5DefaultAny;
            B5Naked += snapshot.BodyType5NakedLinkAny;
        }
        return new BodyRecipeAvailabilitySnapshot(Total, B5, B5Slot, B5Species, B5Age, B5Gender, B5AllLabels, Def, DefF, DefM, NakedAny, B5Def, B5Naked);
    }

    private async Task<BodyRecipeAvailabilitySnapshot> ProbeBodyRecipeAvailabilityFromDatabaseAsync(string databasePath, SimInfoSummary metadata, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
              COUNT(*),
              SUM(CASE WHEN body_type = 5 THEN 1 ELSE 0 END),
              SUM(CASE WHEN body_type = 5 AND lower(coalesce(slot_category, '')) IN ('body', 'full body') THEN 1 ELSE 0 END),
              SUM(CASE WHEN body_type = 5 AND (species_label IS NULL OR species_label = '' OR lower(species_label) = lower($species)) THEN 1 ELSE 0 END),
              SUM(CASE WHEN body_type = 5 AND (age_label IS NULL OR age_label = '' OR lower(age_label) = 'unknown' OR lower(age_label) LIKE '%' || lower($age) || '%') THEN 1 ELSE 0 END),
              SUM(CASE WHEN body_type = 5 AND (gender_label IS NULL OR gender_label = '' OR lower(gender_label) = 'unknown' OR lower(gender_label) = 'unisex' OR lower(gender_label) LIKE '%' || lower($gender) || '%') THEN 1 ELSE 0 END),
              SUM(CASE WHEN body_type = 5
                        AND (species_label IS NULL OR species_label = '' OR lower(species_label) = lower($species))
                        AND (age_label IS NULL OR age_label = '' OR lower(age_label) = 'unknown' OR lower(age_label) LIKE '%' || lower($age) || '%')
                        AND (gender_label IS NULL OR gender_label = '' OR lower(gender_label) = 'unknown' OR lower(gender_label) = 'unisex' OR lower(gender_label) LIKE '%' || lower($gender) || '%')
                       THEN 1 ELSE 0 END),
              SUM(CASE WHEN default_body_type = 1 THEN 1 ELSE 0 END),
              SUM(CASE WHEN default_body_type_female = 1 THEN 1 ELSE 0 END),
              SUM(CASE WHEN default_body_type_male = 1 THEN 1 ELSE 0 END),
              SUM(CASE WHEN has_naked_link = 1 THEN 1 ELSE 0 END),
              SUM(CASE WHEN body_type = 5 AND default_body_type = 1 THEN 1 ELSE 0 END),
              SUM(CASE WHEN body_type = 5 AND has_naked_link = 1 THEN 1 ELSE 0 END)
            FROM cas_part_facts;
            """;
        command.Parameters.AddWithValue("$species", metadata.SpeciesLabel ?? string.Empty);
        command.Parameters.AddWithValue("$age", metadata.AgeLabel ?? string.Empty);
        command.Parameters.AddWithValue("$gender", metadata.GenderLabel ?? string.Empty);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new BodyRecipeAvailabilitySnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
        long ReadLong(int ordinal) => reader.IsDBNull(ordinal) ? 0L : Convert.ToInt64(reader.GetValue(ordinal));
        return new BodyRecipeAvailabilitySnapshot(
            ReadLong(0), ReadLong(1), ReadLong(2), ReadLong(3), ReadLong(4), ReadLong(5),
            ReadLong(6), ReadLong(7), ReadLong(8), ReadLong(9), ReadLong(10), ReadLong(11), ReadLong(12));
    }

    public async Task<IReadOnlyList<SimTemplateBodyPartFact>> GetSimTemplateBodyPartFactsAsync(Guid resourceId, CancellationToken cancellationToken)
    {
        var shardResults = await Task.WhenAll(
            GetServingDatabasePaths().Select(path => GetSimTemplateBodyPartFactsFromDatabaseAsync(path, resourceId, cancellationToken)));
        return shardResults
            .SelectMany(static items => items)
            .OrderBy(static item => item.OutfitIndex)
            .ThenBy(static item => item.PartIndex)
            .ToArray();
    }

    public async Task UpdatePackageAssetsAsync(Guid dataSourceId, string packagePath, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(GetServingDatabasePathForPackage(dataSourceId, packagePath), SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var scanToken = Guid.NewGuid().ToString("N");
        await using var insertCommand = SqliteIndexWriteSession.CreateInsertAssetCommand(connection);
        await using var syncFtsCommand = SqliteIndexWriteSession.CreateSyncAssetsFtsForPackageCommand(connection);
        await using var deletePackageCommand = SqliteIndexWriteSession.CreateDeletePackageAssetsCommand(connection);
        await using var deleteAssetVariantsCommand = SqliteIndexWriteSession.CreateDeletePackageAssetVariantsCommand(connection);
        await using var deleteFtsCommand = SqliteIndexWriteSession.CreateDeleteAssetsFtsCommand(connection);
        insertCommand.Transaction = transaction;
        syncFtsCommand.Transaction = transaction;
        deletePackageCommand.Transaction = transaction;
        deleteAssetVariantsCommand.Transaction = transaction;
        deleteFtsCommand.Transaction = transaction;
        insertCommand.Prepare();
        syncFtsCommand.Prepare();
        deletePackageCommand.Prepare();
        deleteAssetVariantsCommand.Prepare();
        deleteFtsCommand.Prepare();
        SqliteIndexWriteSession.Bind(deletePackageCommand, transaction, dataSourceId, packagePath);
        await deletePackageCommand.ExecuteNonQueryAsync(cancellationToken);
        SqliteIndexWriteSession.Bind(deleteAssetVariantsCommand, transaction, dataSourceId, packagePath);
        await deleteAssetVariantsCommand.ExecuteNonQueryAsync(cancellationToken);
        foreach (var asset in assets)
        {
            SqliteIndexWriteSession.Bind(insertCommand, asset, scanToken);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        SqliteIndexWriteSession.Bind(deleteFtsCommand, transaction, dataSourceId, packagePath);
        await deleteFtsCommand.ExecuteNonQueryAsync(cancellationToken);
        SqliteIndexWriteSession.Bind(syncFtsCommand, transaction, dataSourceId, packagePath);
        await syncFtsCommand.ExecuteNonQueryAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, PackageFingerprint>> LoadPackageFingerprintsFromDatabaseAsync(string databasePath, IReadOnlyList<Guid> dataSourceIds, CancellationToken cancellationToken)
    {
        var sourceIds = dataSourceIds.Select(id => id.ToString("D")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        var parameterNames = new List<string>(sourceIds.Length);
        for (var index = 0; index < sourceIds.Length; index++)
        {
            var parameterName = $"$sourceId{index}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, sourceIds[index]);
        }

        command.CommandText =
            $"""
            SELECT data_source_id, package_path, file_size, last_write_utc
            FROM packages
            WHERE data_source_id IN ({string.Join(", ", parameterNames)});
            """;

        var fingerprints = new Dictionary<string, PackageFingerprint>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var dataSourceId = Guid.Parse(reader.GetString(0));
            var packagePath = reader.GetString(1);
            fingerprints[BuildFingerprintKey(dataSourceId, packagePath)] = new PackageFingerprint(
                dataSourceId,
                packagePath,
                reader.GetInt64(2),
                DateTimeOffset.Parse(reader.GetString(3), null, System.Globalization.DateTimeStyles.RoundtripKind));
        }

        return fingerprints;
    }

    private async Task<WindowedQueryResult<ResourceMetadata>> QueryResourcesFromDatabaseAsync(string databasePath, RawResourceBrowserQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        var (whereClause, bindParameters) = BuildRawResourceWhereClause(query);

        var totalCount = await CountAsync(connection, $"SELECT COUNT(*) FROM resources{whereClause};", bindParameters, cancellationToken);
        await using var command = connection.CreateCommand();
        bindParameters(command);
        command.CommandText =
            $"""
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint
            FROM resources
            {whereClause}
            ORDER BY {BuildRawResourceSort(query.Sort)}
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$limit", query.WindowSize);
        command.Parameters.AddWithValue("$offset", query.Offset);
        var items = await ReadResourcesAsync(command, cancellationToken);
        return new WindowedQueryResult<ResourceMetadata>(items, totalCount, query.Offset, query.WindowSize);
    }

    private async Task<WindowedQueryResult<AssetSummary>> QueryAssetsFromDatabaseAsync(string databasePath, AssetBrowserQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        var (whereClause, bindParameters) = BuildAssetWhereClause(query);

        var totalCount = await CountAsync(connection, $"SELECT COUNT(*) FROM assets{whereClause};", bindParameters, cancellationToken);
        await using var command = connection.CreateCommand();
        bindParameters(command);
        command.CommandText =
            $"""
            SELECT id, data_source_id, source_kind, asset_kind, display_name, category, description, package_path, root_tgi, logical_root_tgi, thumbnail_tgi, variant_count, linked_resource_count,
                   has_scene_root, has_exact_geometry_candidate, has_material_references, has_texture_references, diagnostics,
                   package_name, root_type_name, thumbnail_type_name, primary_geometry_type, identity_type, category_normalized,
                   has_identity_metadata, has_rig_reference, has_geometry_reference, has_material_resource_candidate, has_texture_resource_candidate, is_package_local_graph, has_diagnostics,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034
            FROM assets
            {whereClause}
            ORDER BY {BuildAssetSort(query.Sort)}
            LIMIT $limit OFFSET $offset;
            """;
        command.Parameters.AddWithValue("$limit", query.WindowSize);
        command.Parameters.AddWithValue("$offset", query.Offset);
        var items = await ReadAssetsAsync(command, cancellationToken);
        return new WindowedQueryResult<AssetSummary>(items, totalCount, query.Offset, query.WindowSize);
    }

    private async Task<AssetFacetOptions> ReadAssetFacetOptionsFromDatabaseAsync(string databasePath, string assetKind, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        return new AssetFacetOptions(
            await ReadDistinctAssetValuesAsync(connection, assetKind, "category", cancellationToken),
            await ReadDistinctAssetValuesAsync(connection, assetKind, "root_type_name", cancellationToken),
            await ReadDistinctAssetValuesAsync(connection, assetKind, "identity_type", cancellationToken),
            await ReadDistinctAssetValuesAsync(connection, assetKind, "primary_geometry_type", cancellationToken),
            await ReadDistinctAssetValuesAsync(connection, assetKind, "thumbnail_type_name", cancellationToken),
            await ReadDistinctAssetHexValuesAsync(connection, assetKind, "catalog_signal_0020", cancellationToken),
            await ReadDistinctAssetHexValuesAsync(connection, assetKind, "catalog_signal_002c", cancellationToken),
            await ReadDistinctAssetHexValuesAsync(connection, assetKind, "catalog_signal_0030", cancellationToken),
            await ReadDistinctAssetHexValuesAsync(connection, assetKind, "catalog_signal_0034", cancellationToken));
    }

    private async Task<IReadOnlyList<IndexedPackageRecord>> GetIndexedPackagesFromDatabaseAsync(string databasePath, IReadOnlyList<Guid> dataSourceIds, CancellationToken cancellationToken)
    {
        var sourceIds = dataSourceIds.Select(id => id.ToString("D")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();

        if (sourceIds.Length == 0)
        {
            command.CommandText =
                """
                SELECT p.data_source_id, ds.kind, p.package_path, p.file_size, p.last_write_utc, COUNT(a.id)
                FROM packages p
                JOIN data_sources ds ON ds.id = p.data_source_id
                LEFT JOIN assets a ON a.data_source_id = p.data_source_id AND a.package_path = p.package_path
                GROUP BY p.data_source_id, ds.kind, p.package_path, p.file_size, p.last_write_utc
                ORDER BY p.package_path;
                """;
        }
        else
        {
            var parameterNames = new List<string>(sourceIds.Length);
            for (var index = 0; index < sourceIds.Length; index++)
            {
                var parameterName = $"$sourceId{index}";
                parameterNames.Add(parameterName);
                command.Parameters.AddWithValue(parameterName, sourceIds[index]);
            }

            command.CommandText =
                $"""
                SELECT p.data_source_id, ds.kind, p.package_path, p.file_size, p.last_write_utc, COUNT(a.id)
                FROM packages p
                JOIN data_sources ds ON ds.id = p.data_source_id
                LEFT JOIN assets a ON a.data_source_id = p.data_source_id AND a.package_path = p.package_path
                WHERE p.data_source_id IN ({string.Join(", ", parameterNames)})
                GROUP BY p.data_source_id, ds.kind, p.package_path, p.file_size, p.last_write_utc
                ORDER BY p.package_path;
                """;
        }

        var results = new List<IndexedPackageRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new IndexedPackageRecord(
                Guid.Parse(reader.GetString(0)),
                Enum.Parse<SourceKind>(reader.GetString(1)),
                reader.GetString(2),
                reader.GetInt64(3),
                DateTimeOffset.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                reader.GetInt32(5)));
        }

        return results;
    }

    private async Task<IReadOnlyList<ResourceMetadata>> GetResourcesByTgiFromDatabaseAsync(string databasePath, string fullTgi, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint
            FROM resources
            WHERE full_tgi = $fullTgi
            ORDER BY package_path, type_name;
            """;
        command.Parameters.AddWithValue("$fullTgi", fullTgi);
        return await ReadResourcesAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<ResourceMetadata>> GetCasPartResourcesByInstancesFromDatabaseAsync(string databasePath, IReadOnlyList<ulong> fullInstances, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        var parameterNames = new List<string>(fullInstances.Count);
        for (var index = 0; index < fullInstances.Count; index++)
        {
            var parameterName = $"$instance{index}";
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, fullInstances[index].ToString("X16"));
        }

        command.CommandText =
            $"""
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint
            FROM resources
            WHERE type_name = 'CASPart' AND instance_hex IN ({string.Join(", ", parameterNames)})
            ORDER BY package_path, full_tgi;
            """;

        return await ReadResourcesAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<SimTemplateFactSummary>> GetSimTemplateFactsByArchetypeFromDatabaseAsync(string databasePath, string archetypeKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT resource_id, data_source_id, source_kind, package_path, root_tgi, archetype_key,
                   species_label, age_label, gender_label, display_name, notes,
                   outfit_category_count, outfit_entry_count, outfit_part_count,
                   body_modifier_count, face_modifier_count, sculpt_count, has_skintone,
                   authoritative_body_driving_outfit_count, authoritative_body_driving_outfit_part_count
            FROM sim_template_facts
            WHERE archetype_key = $archetypeKey
            ORDER BY authoritative_body_driving_outfit_count DESC,
                     authoritative_body_driving_outfit_part_count DESC,
                     outfit_part_count DESC,
                     package_path,
                     root_tgi;
            """;
        command.Parameters.AddWithValue("$archetypeKey", archetypeKey);
        return await ReadSimTemplateFactsAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<SimTemplateBodyPartFact>> GetSimTemplateBodyPartFactsFromDatabaseAsync(string databasePath, Guid resourceId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT resource_id, data_source_id, source_kind, package_path, root_tgi,
                   outfit_category_value, outfit_category_label, outfit_index, part_index,
                   body_type, body_type_label, part_instance_hex, is_body_driving
            FROM sim_template_body_parts
            WHERE resource_id = $resourceId
            ORDER BY outfit_index, part_index;
            """;
        command.Parameters.AddWithValue("$resourceId", resourceId.ToString("D"));
        return await ReadSimTemplateBodyPartFactsAsync(command, cancellationToken);
    }

    private async Task<IReadOnlyList<AssetSummary>> GetIndexedDefaultBodyRecipeAssetsFromDatabaseAsync(
        string databasePath,
        SimInfoSummary metadata,
        string slotCategory,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        var expectedBodyType = slotCategory switch
        {
            "Full Body" => 5,
            "Body" => 5,
            "Top" => 6,
            "Bottom" => 7,
            "Shoes" => 8,
            _ => -1
        };
        if (expectedBodyType < 0)
        {
            return [];
        }

        var prefixPatterns = BuildIndexedBodyRecipePrefixPatterns(metadata, slotCategory);
        var speciesAliases = BuildIndexedBodyRecipeSpeciesAliases(metadata);
        command.CommandText =
            """
            SELECT a.id, a.data_source_id, a.source_kind, a.asset_kind, a.display_name, a.category, a.description, a.package_path, a.root_tgi, a.logical_root_tgi, a.thumbnail_tgi,
                   a.variant_count, a.linked_resource_count, a.has_scene_root, a.has_exact_geometry_candidate, a.has_material_references, a.has_texture_references, a.diagnostics,
                   a.package_name, a.root_type_name, a.thumbnail_type_name, a.primary_geometry_type, a.identity_type, a.category_normalized,
                   a.has_identity_metadata, a.has_rig_reference, a.has_geometry_reference, a.has_material_resource_candidate, a.has_texture_resource_candidate, a.is_package_local_graph, a.has_diagnostics,
                   a.catalog_signal_0020, a.catalog_signal_002c, a.catalog_signal_0030, a.catalog_signal_0034
            FROM assets a
            JOIN cas_part_facts f ON f.asset_id = a.id
            WHERE a.asset_kind = 'Cas'
              AND a.has_scene_root = 1
              AND f.body_type = $bodyType
              AND f.slot_category = $slotCategory
              -- Species filter: require explicit match against the requested species or
              -- one of its aliases. Empirically (probed against ClientFullBuild* CASParts)
              -- 100% of BodyType=5 rows ship with an explicit species_label, so the
              -- previous NULL/empty pass-through wasn't a useful fallback — it was just a
              -- hole that allowed non-human shells to slip into human Sim body assemblies
              -- (the dog-body-on-female-Sim regression).
              AND (
                    lower(f.species_label) = lower($speciesLabel) OR
                    (length($speciesAlias0) > 0 AND lower(f.species_label) = lower($speciesAlias0)) OR
                    (length($speciesAlias1) > 0 AND lower(f.species_label) = lower($speciesAlias1))
                  )
              AND (f.age_label IS NULL OR f.age_label = '' OR lower(f.age_label) = 'unknown' OR lower(f.age_label) LIKE '%' || lower($ageLabel) || '%')
              AND (f.gender_label IS NULL OR f.gender_label = '' OR lower(f.gender_label) = 'unknown' OR lower(f.gender_label) = 'unisex' OR lower(f.gender_label) LIKE '%' || lower($genderLabel) || '%')
              AND (
                    f.has_naked_link = 1 OR
                    f.default_body_type = 1 OR
                    ($genderLabel = 'Female' AND f.default_body_type_female = 1) OR
                    ($genderLabel = 'Male' AND f.default_body_type_male = 1) OR
                    lower(coalesce(f.internal_name, '')) LIKE $prefixPattern0 OR
                    lower(coalesce(f.internal_name, '')) LIKE $prefixPattern1 OR
                    lower(coalesce(f.internal_name, '')) LIKE $prefixPattern2 OR
                    lower(coalesce(f.internal_name, '')) LIKE $prefixPattern3 OR
                    lower(coalesce(a.display_name, '')) LIKE $prefixPattern0 OR
                    lower(coalesce(a.display_name, '')) LIKE $prefixPattern1 OR
                    lower(coalesce(a.display_name, '')) LIKE $prefixPattern2 OR
                    lower(coalesce(a.display_name, '')) LIKE $prefixPattern3 OR
                    lower(coalesce(f.internal_name, '')) LIKE '%nude%' OR
                    lower(coalesce(a.display_name, '')) LIKE '%nude%' OR
                    lower(coalesce(a.description, '')) LIKE '%nude%'
                  )
            ORDER BY
                CASE
                    WHEN lower(coalesce(f.internal_name, '')) LIKE $prefixPattern0 THEN 0
                    WHEN lower(coalesce(f.internal_name, '')) LIKE $prefixPattern1 THEN 1
                    WHEN lower(coalesce(f.internal_name, '')) LIKE $prefixPattern2 THEN 2
                    WHEN lower(coalesce(f.internal_name, '')) LIKE $prefixPattern3 THEN 3
                    WHEN lower(coalesce(a.display_name, '')) LIKE $prefixPattern0 THEN 4
                    WHEN lower(coalesce(a.display_name, '')) LIKE $prefixPattern1 THEN 5
                    WHEN lower(coalesce(a.display_name, '')) LIKE $prefixPattern2 THEN 6
                    WHEN lower(coalesce(a.display_name, '')) LIKE $prefixPattern3 THEN 7
                    WHEN lower(coalesce(f.internal_name, '')) LIKE '%nude%' THEN 8
                    WHEN lower(coalesce(a.display_name, '')) LIKE '%nude%' THEN 9
                    WHEN lower(coalesce(a.description, '')) LIKE '%nude%' THEN 10
                    WHEN $genderLabel = 'Female' AND f.default_body_type_female = 1 THEN 11
                    WHEN $genderLabel = 'Male' AND f.default_body_type_male = 1 THEN 11
                    WHEN f.default_body_type = 1 THEN 12
                    WHEN f.has_naked_link = 1 THEN 13
                    ELSE 14
                END,
                CASE
                    WHEN lower(coalesce(f.gender_label, '')) = lower($genderLabel) THEN 0
                    WHEN lower(coalesce(f.gender_label, '')) = 'unisex' THEN 1
                    ELSE 2
                END,
                CASE
                    WHEN lower(coalesce(f.age_label, '')) = lower($ageLabel) THEN 0
                    WHEN lower(coalesce(f.age_label, '')) LIKE '%' || lower($ageLabel) || '%' THEN 1
                    ELSE 2
                END,
                f.sort_layer,
                a.display_name,
                a.package_path
            LIMIT 96;
            """;
        command.Parameters.AddWithValue("$bodyType", expectedBodyType);
        command.Parameters.AddWithValue("$slotCategory", slotCategory);
        command.Parameters.AddWithValue("$speciesLabel", metadata.SpeciesLabel ?? string.Empty);
        command.Parameters.AddWithValue("$speciesAlias0", speciesAliases[0]);
        command.Parameters.AddWithValue("$speciesAlias1", speciesAliases[1]);
        command.Parameters.AddWithValue("$ageLabel", metadata.AgeLabel ?? string.Empty);
        command.Parameters.AddWithValue("$genderLabel", metadata.GenderLabel ?? string.Empty);
        for (var index = 0; index < 4; index++)
        {
            command.Parameters.AddWithValue($"$prefixPattern{index}", prefixPatterns[index]);
        }

        return await ReadAssetsAsync(command, cancellationToken);
    }

    private static string[] BuildIndexedBodyRecipePrefixPatterns(SimInfoSummary metadata, string slotCategory)
    {
        var prefixes = slotCategory switch
        {
            "Full Body" or "Body" => BuildIndexedBodyRecipeNamePrefixes(metadata, "Body"),
            _ => []
        };
        return prefixes
            .Take(4)
            .Select(static prefix => $"{prefix.ToLowerInvariant()}%")
            .Concat(Enumerable.Repeat(string.Empty, 4))
            .Take(4)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildIndexedBodyRecipeNamePrefixes(SimInfoSummary metadata, string slotStem)
    {
        if (string.IsNullOrWhiteSpace(slotStem))
        {
            return [];
        }

        if (string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(metadata.AgeLabel, "Infant", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var prefixes = new List<string>();
        if (TryBuildIndexedBodyRecipePrefix(metadata, out var exactPrefix))
        {
            prefixes.Add($"{exactPrefix}{slotStem}_");
            prefixes.Add($"{exactPrefix}{slotStem}");
        }

        foreach (var genericPrefix in BuildGenericIndexedBodyRecipePrefixes(metadata))
        {
            prefixes.Add($"{genericPrefix}{slotStem}_");
            prefixes.Add($"{genericPrefix}{slotStem}");
        }

        return prefixes
            .Where(static prefix => !string.IsNullOrWhiteSpace(prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string[] BuildIndexedBodyRecipeSpeciesAliases(SimInfoSummary metadata)
    {
        if (string.Equals(metadata.SpeciesLabel, "Little Dog", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(metadata.AgeLabel, "Child", StringComparison.OrdinalIgnoreCase))
        {
            return ["Dog", string.Empty];
        }

        return [string.Empty, string.Empty];
    }

    private static bool TryBuildIndexedBodyRecipePrefix(SimInfoSummary metadata, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrWhiteSpace(metadata.SpeciesLabel) ||
            string.IsNullOrWhiteSpace(metadata.AgeLabel) ||
            string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new StringBuilder();
        if (string.Equals(metadata.AgeLabel, "Infant", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append('i');
        }
        else if (string.Equals(metadata.AgeLabel, "Toddler", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase) ? 'p' : 'c');
        }
        else if (string.Equals(metadata.AgeLabel, "Child", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append('c');
        }
        else
        {
            builder.Append(string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase) ? 'y' : 'a');
        }

        if (!string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(GetIndexedNonHumanBodyPrefix(metadata.SpeciesLabel, metadata.AgeLabel));
        }
        else if (string.Equals(metadata.AgeLabel, "Baby", StringComparison.OrdinalIgnoreCase) ||
                 string.IsNullOrWhiteSpace(metadata.GenderLabel) ||
                 string.Equals(metadata.GenderLabel, "Unknown", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(metadata.GenderLabel, "Unisex", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(metadata.AgeLabel, "Infant", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append('u');
        }
        else
        {
            builder.Append(string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase) ? 'm' : 'f');
        }

        prefix = builder.ToString();
        return prefix.Length > 0;
    }

    private static IReadOnlyList<string> BuildGenericIndexedBodyRecipePrefixes(SimInfoSummary metadata)
    {
        if (!string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(metadata.AgeLabel) ||
            string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(metadata.AgeLabel, "Baby", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (string.Equals(metadata.AgeLabel, "Infant", StringComparison.OrdinalIgnoreCase))
        {
            return ["iu"];
        }

        if (string.Equals(metadata.AgeLabel, "Toddler", StringComparison.OrdinalIgnoreCase))
        {
            var prefixes = new List<string> { "pu" };
            if (string.Equals(metadata.GenderLabel, "Female", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add("pf");
            }
            else if (string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add("pm");
            }

            return prefixes;
        }

        if (string.Equals(metadata.AgeLabel, "Child", StringComparison.OrdinalIgnoreCase))
        {
            var prefixes = new List<string> { "cu" };
            if (string.Equals(metadata.GenderLabel, "Female", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add("cf");
            }
            else if (string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add("cm");
            }

            return prefixes;
        }

        var adultPrefixes = new List<string> { "ac", "ah" };
        if (string.Equals(metadata.GenderLabel, "Female", StringComparison.OrdinalIgnoreCase))
        {
            adultPrefixes.Add("af");
        }
        else if (string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase))
        {
            adultPrefixes.Add("am");
        }

        return adultPrefixes;
    }

    private static char GetIndexedNonHumanBodyPrefix(string speciesLabel, string ageLabel)
    {
        if (string.Equals(ageLabel, "Child", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(speciesLabel, "Little Dog", StringComparison.OrdinalIgnoreCase))
        {
            return 'd';
        }

        return speciesLabel switch
        {
            "Dog" => 'd',
            "Cat" => 'c',
            "Little Dog" => 'l',
            "Fox" => 'f',
            "Horse" => 'h',
            _ => 'a'
        };
    }

    private async Task<IReadOnlyList<AssetVariantSummary>> GetAssetVariantsFromDatabaseAsync(string databasePath, Guid assetId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(databasePath, SqliteConnectionProfile.LiveServing, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT asset_id, data_source_id, source_kind, asset_kind, package_path, root_tgi,
                   variant_index, variant_kind, display_label, swatch_hex, thumbnail_tgi, diagnostics
            FROM asset_variants
            WHERE asset_id = $assetId
            ORDER BY variant_kind, variant_index;
            """;
        command.Parameters.AddWithValue("$assetId", assetId.ToString("D"));

        var results = new List<AssetVariantSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AssetVariantSummary(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<SourceKind>(reader.GetString(2)),
                Enum.Parse<AssetKind>(reader.GetString(3)),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetInt32(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetString(11)));
        }

        return results;
    }

    private static IEnumerable<ResourceMetadata> OrderByResource(IEnumerable<ResourceMetadata> items, RawResourceSort sort) => sort switch
    {
        RawResourceSort.PackagePath => items.OrderBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.Key.TypeName, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.Key.FullTgi, StringComparer.OrdinalIgnoreCase),
        RawResourceSort.Tgi => items.OrderBy(static item => item.Key.FullTgi, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.Key.TypeName, StringComparer.OrdinalIgnoreCase),
        _ => items.OrderBy(static item => item.Key.TypeName, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
    };

    private static IEnumerable<AssetSummary> OrderByAsset(IEnumerable<AssetSummary> items, AssetBrowserSort sort) => sort switch
    {
        AssetBrowserSort.Category => items.OrderBy(static item => item.Category ?? string.Empty, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase),
        AssetBrowserSort.Package => items.OrderBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase),
        _ => items.OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.PackagePath, StringComparer.OrdinalIgnoreCase).ThenBy(static item => item.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase)
    };

    private Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken) =>
        OpenConnectionAsync(cacheService.DatabasePath, SqliteConnectionProfile.LiveServing, cancellationToken);

    private static async Task EnsureSchemaAsync(SqliteConnection connection, bool includeSearchStructures, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS data_sources (
                id TEXT PRIMARY KEY,
                display_name TEXT NOT NULL,
                root_path TEXT NOT NULL,
                kind TEXT NOT NULL,
                is_enabled INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cache_metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS packages (
                data_source_id TEXT NOT NULL,
                package_path TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                last_write_utc TEXT NOT NULL,
                indexed_utc TEXT NOT NULL,
                PRIMARY KEY (data_source_id, package_path)
            );

            CREATE TABLE IF NOT EXISTS resources (
                id TEXT NOT NULL,
                data_source_id TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                package_path TEXT NOT NULL,
                scan_token TEXT NULL,
                type_hex TEXT NOT NULL,
                type_name TEXT NOT NULL,
                group_hex TEXT NOT NULL,
                instance_hex TEXT NOT NULL,
                full_tgi TEXT NOT NULL,
                name TEXT NULL,
                description TEXT NULL,
                catalog_signal_0020 INTEGER NULL,
                catalog_signal_002c INTEGER NULL,
                catalog_signal_0030 INTEGER NULL,
                catalog_signal_0034 INTEGER NULL,
                compressed_size INTEGER NULL,
                uncompressed_size INTEGER NULL,
                is_compressed INTEGER NULL,
                preview_kind TEXT NOT NULL,
                is_previewable INTEGER NOT NULL,
                is_export_capable INTEGER NOT NULL,
                asset_linkage_summary TEXT NOT NULL,
                diagnostics TEXT NOT NULL,
                scene_root_tgi_hint TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS assets (
                id TEXT NOT NULL,
                data_source_id TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                asset_kind TEXT NOT NULL,
                display_name TEXT NOT NULL,
                category TEXT NULL,
                description TEXT NULL,
                catalog_signal_0020 INTEGER NULL,
                catalog_signal_002c INTEGER NULL,
                catalog_signal_0030 INTEGER NULL,
                catalog_signal_0034 INTEGER NULL,
                category_normalized TEXT NULL,
                package_path TEXT NOT NULL,
                scan_token TEXT NULL,
                is_canonical INTEGER NOT NULL DEFAULT 1,
                package_name TEXT NULL,
                root_tgi TEXT NOT NULL,
                logical_root_tgi TEXT NULL,
                root_type_name TEXT NULL,
                thumbnail_tgi TEXT NULL,
                thumbnail_type_name TEXT NULL,
                primary_geometry_type TEXT NULL,
                identity_type TEXT NULL,
                variant_count INTEGER NOT NULL,
                linked_resource_count INTEGER NOT NULL,
                has_scene_root INTEGER NULL,
                has_exact_geometry_candidate INTEGER NULL,
                has_material_references INTEGER NULL,
                has_texture_references INTEGER NULL,
                has_identity_metadata INTEGER NULL,
                has_rig_reference INTEGER NULL,
                has_geometry_reference INTEGER NULL,
                has_material_resource_candidate INTEGER NULL,
                has_texture_resource_candidate INTEGER NULL,
                is_package_local_graph INTEGER NULL,
                has_diagnostics INTEGER NULL,
                diagnostics TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS asset_variants (
                asset_id TEXT NOT NULL,
                data_source_id TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                asset_kind TEXT NOT NULL,
                package_path TEXT NOT NULL,
                scan_token TEXT NULL,
                root_tgi TEXT NOT NULL,
                variant_index INTEGER NOT NULL,
                variant_kind TEXT NOT NULL,
                display_label TEXT NOT NULL,
                swatch_hex TEXT NULL,
                thumbnail_tgi TEXT NULL,
                diagnostics TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sim_template_facts (
                resource_id TEXT NOT NULL PRIMARY KEY,
                data_source_id TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                package_path TEXT NOT NULL,
                root_tgi TEXT NOT NULL,
                archetype_key TEXT NOT NULL,
                species_label TEXT NOT NULL,
                age_label TEXT NOT NULL,
                gender_label TEXT NOT NULL,
                display_name TEXT NOT NULL,
                notes TEXT NOT NULL,
                outfit_category_count INTEGER NOT NULL,
                outfit_entry_count INTEGER NOT NULL,
                outfit_part_count INTEGER NOT NULL,
                body_modifier_count INTEGER NOT NULL,
                face_modifier_count INTEGER NOT NULL,
                sculpt_count INTEGER NOT NULL,
                has_skintone INTEGER NOT NULL,
                authoritative_body_driving_outfit_count INTEGER NOT NULL,
                authoritative_body_driving_outfit_part_count INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sim_template_body_parts (
                resource_id TEXT NOT NULL,
                data_source_id TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                package_path TEXT NOT NULL,
                root_tgi TEXT NOT NULL,
                outfit_category_value INTEGER NOT NULL,
                outfit_category_label TEXT NOT NULL,
                outfit_index INTEGER NOT NULL,
                part_index INTEGER NOT NULL,
                body_type INTEGER NOT NULL,
                body_type_label TEXT NULL,
                part_instance_hex TEXT NOT NULL,
                is_body_driving INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS cas_part_facts (
                asset_id TEXT NOT NULL,
                data_source_id TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                package_path TEXT NOT NULL,
                root_tgi TEXT NOT NULL,
                slot_category TEXT NOT NULL,
                category_normalized TEXT NULL,
                body_type INTEGER NOT NULL,
                internal_name TEXT NULL,
                default_body_type INTEGER NOT NULL,
                default_body_type_female INTEGER NOT NULL,
                default_body_type_male INTEGER NOT NULL,
                has_naked_link INTEGER NOT NULL,
                restrict_opposite_gender INTEGER NOT NULL,
                restrict_opposite_frame INTEGER NOT NULL,
                sort_layer INTEGER NOT NULL,
                species_label TEXT NULL,
                age_label TEXT NOT NULL,
                gender_label TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureDeferredMetadataSchemaAsync(connection, cancellationToken);
        await EnsureScanTokenSchemaAsync(connection, cancellationToken);
        await EnsureAssetSummarySchemaAsync(connection, cancellationToken);
        await EnsureSeedFactContentVersionAsync(connection, cancellationToken);
        if (!includeSearchStructures)
        {
            return;
        }

        await using (var searchIndexes = connection.CreateCommand())
        {
            searchIndexes.CommandText = SecondaryIndexesSql;
            await searchIndexes.ExecuteNonQueryAsync(cancellationToken);
        }

        var ftsResyncRequired = await EnsureFtsSchemaAsync(connection, cancellationToken);
        if (ftsResyncRequired)
        {
            await SyncFtsTablesAsync(connection, cancellationToken);
        }
    }

    private static async Task EnsureSeedFactContentVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var existingVersion = await GetCacheMetadataValueAsync(
            connection,
            SeedFactContentVersionMetadataKey,
            cancellationToken).ConfigureAwait(false);
        if (string.Equals(existingVersion, SeedFactContentVersion, StringComparison.Ordinal))
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            DELETE FROM sim_template_body_parts;
            DELETE FROM sim_template_facts;
            DELETE FROM cas_part_facts;
            -- Clear package fingerprints so NeedsRescanAsync returns true for every package
            -- on the next index run; otherwise the wiped seed-fact tables would stay empty
            -- until a package's file size or mtime happened to change on disk.
            DELETE FROM packages;
            INSERT INTO cache_metadata(key, value)
            VALUES ($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", SeedFactContentVersionMetadataKey);
        command.Parameters.AddWithValue("$value", SeedFactContentVersion);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task<string?> GetCacheMetadataValueAsync(SqliteConnection connection, string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM cache_metadata WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
    }

    private async Task<SqliteConnection> OpenConnectionAsync(string databasePath, SqliteConnectionProfile profile, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = profile == SqliteConnectionProfile.LiveServing ? SqliteCacheMode.Shared : SqliteCacheMode.Private
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = profile == SqliteConnectionProfile.LiveServing
            ? """
              PRAGMA journal_mode = WAL;
              PRAGMA synchronous = NORMAL;
              PRAGMA temp_store = MEMORY;
              """
            : """
              PRAGMA journal_mode = MEMORY;
              PRAGMA synchronous = OFF;
              PRAGMA temp_store = MEMORY;
              PRAGMA locking_mode = EXCLUSIVE;
              """;
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private void CleanupShadowDatabases()
    {
        if (!Directory.Exists(cacheService.CacheRoot))
        {
            return;
        }

        foreach (var shadowDatabasePath in Directory.EnumerateFiles(cacheService.CacheRoot, ShadowDatabasePattern, SearchOption.TopDirectoryOnly))
        {
            TryDeleteDatabaseArtifacts(shadowDatabasePath);
        }
    }

    private async Task ActivateShadowDatabaseSetAsync(IReadOnlyList<string> shadowDatabasePaths, CancellationToken cancellationToken)
    {
        await RetryFileOperationAsync(
            () =>
            {
                SqliteConnection.ClearAllPools();
                for (var shardIndex = 0; shardIndex < CatalogShardCount; shardIndex++)
                {
                    var activePath = GetShardPath(shardIndex);
                    DeleteFileIfExists(activePath + "-wal");
                    DeleteFileIfExists(activePath + "-shm");
                    DeleteFileIfExists(shadowDatabasePaths[shardIndex] + "-wal");
                    DeleteFileIfExists(shadowDatabasePaths[shardIndex] + "-shm");

                    if (File.Exists(activePath))
                    {
                        File.Replace(shadowDatabasePaths[shardIndex], activePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    }
                    else
                    {
                        File.Move(shadowDatabasePaths[shardIndex], activePath, overwrite: true);
                    }
                }

                foreach (var staleShardPath in Directory.EnumerateFiles(cacheService.CacheRoot, "index.shard*.sqlite", SearchOption.TopDirectoryOnly))
                {
                    var fileName = Path.GetFileName(staleShardPath);
                    if (!Enumerable.Range(1, CatalogShardCount - 1)
                        .Select(index => $"index.shard{index:00}.sqlite")
                        .Contains(fileName, StringComparer.OrdinalIgnoreCase))
                    {
                        TryDeleteDatabaseArtifacts(staleShardPath);
                    }
                }
            },
            cancellationToken);
    }

    private static void TryDeleteDatabaseArtifacts(string databasePath)
    {
        TryDeleteFile(databasePath);
        TryDeleteFile(databasePath + "-wal");
        TryDeleteFile(databasePath + "-shm");
    }

    private static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            DeleteFileIfExists(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static async Task RetryFileOperationAsync(Action action, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                action();
                return;
            }
            catch (IOException ex)
            {
                lastError = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken);
        }

        throw new IOException("Failed to activate rebuilt SQLite catalog after multiple attempts.", lastError);
    }

    private static async Task<IReadOnlyList<ResourceMetadata>> ReadResourcesAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var results = new List<ResourceMetadata>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var typeHex = reader.GetString(4);
            var typeName = reader.GetString(5);
            var groupHex = reader.GetString(6);
            var instanceHex = reader.GetString(7);

            results.Add(new ResourceMetadata(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<SourceKind>(reader.GetString(2)),
                reader.GetString(3),
                new ResourceKeyRecord(
                    Convert.ToUInt32(typeHex, 16),
                    Convert.ToUInt32(groupHex, 16),
                    Convert.ToUInt64(instanceHex, 16),
                    typeName),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(15) ? null : reader.GetInt64(15),
                reader.IsDBNull(16) ? null : reader.GetInt64(16),
                reader.IsDBNull(17) ? null : reader.GetInt32(17) == 1,
                Enum.Parse<PreviewKind>(reader.GetString(18)),
                reader.GetInt32(19) == 1,
                reader.GetInt32(20) == 1,
                reader.GetString(21),
                reader.GetString(22),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : Convert.ToUInt32(reader.GetInt64(11)),
                reader.IsDBNull(12) ? null : Convert.ToUInt32(reader.GetInt64(12)),
                reader.IsDBNull(13) ? null : Convert.ToUInt32(reader.GetInt64(13)),
                reader.IsDBNull(14) ? null : Convert.ToUInt32(reader.GetInt64(14)),
                reader.IsDBNull(23) ? null : reader.GetString(23)));
        }

        return results;
    }

    private static async Task<IReadOnlyList<SimTemplateFactSummary>> ReadSimTemplateFactsAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var results = new List<SimTemplateFactSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SimTemplateFactSummary(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<SourceKind>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetInt32(13),
                reader.GetInt32(14),
                reader.GetInt32(15),
                reader.GetInt32(16),
                reader.GetInt32(17) == 1,
                reader.GetInt32(18),
                reader.GetInt32(19)));
        }

        return results;
    }

    private static async Task<IReadOnlyList<SimTemplateBodyPartFact>> ReadSimTemplateBodyPartFactsAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var results = new List<SimTemplateBodyPartFact>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new SimTemplateBodyPartFact(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<SourceKind>(reader.GetString(2)),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetString(6),
                reader.GetInt32(7),
                reader.GetInt32(8),
                reader.GetInt32(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                Convert.ToUInt64(reader.GetString(11), 16),
                reader.GetInt32(12) == 1));
        }

        return results;
    }

    private static ResourceKeyRecord ParseTgi(string fullTgi, string typeName)
    {
        var parts = fullTgi.Split(':');
        return new ResourceKeyRecord(
            Convert.ToUInt32(parts[0], 16),
            Convert.ToUInt32(parts[1], 16),
            Convert.ToUInt64(parts[2], 16),
            typeName);
    }

    public static IReadOnlyList<IReadOnlyList<T>> Batch<T>(IReadOnlyList<T> values, int batchSize)
    {
        var results = new List<IReadOnlyList<T>>();
        for (var index = 0; index < values.Count; index += batchSize)
        {
            var count = Math.Min(batchSize, values.Count - index);
            var batch = new List<T>(count);
            for (var inner = 0; inner < count; inner++)
            {
                batch.Add(values[index + inner]);
            }

            results.Add(batch);
        }

        return results;
    }

    private static string BuildFingerprintKey(Guid dataSourceId, string packagePath) =>
        $"{dataSourceId:D}|{packagePath}";

    private static async Task<bool> EnsureFtsSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var resourcesOk = await EnsureFtsTableAsync(
            connection,
            "resources_fts",
            """
            CREATE VIRTUAL TABLE resources_fts USING fts5(
                id UNINDEXED,
                data_source_id UNINDEXED,
                package_path,
                type_name,
                full_tgi,
                name,
                description,
                tokenize = "unicode61 remove_diacritics 0 tokenchars '._:-/\\'"
            );
            """,
            ["id", "data_source_id", "package_path", "type_name", "full_tgi", "name", "description"],
            cancellationToken);
        var assetsOk = await EnsureFtsTableAsync(
            connection,
            "assets_fts",
            """
            CREATE VIRTUAL TABLE assets_fts USING fts5(
                id UNINDEXED,
                data_source_id UNINDEXED,
                package_path,
                package_name,
                display_name,
                category,
                description,
                root_type_name,
                identity_type,
                primary_geometry_type,
                root_tgi,
                tokenize = "unicode61 remove_diacritics 0 tokenchars '._:-/\\'"
            );
            """,
            ["id", "data_source_id", "package_path", "package_name", "display_name", "category", "description", "root_type_name", "identity_type", "primary_geometry_type", "root_tgi"],
            cancellationToken);

        return resourcesOk || assetsOk;
    }

    private static async Task<bool> EnsureFtsTableAsync(
        SqliteConnection connection,
        string tableName,
        string createSql,
        IReadOnlyList<string> expectedColumns,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, tableName, cancellationToken))
        {
            await using var create = connection.CreateCommand();
            create.CommandText = createSql;
            await create.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }

        var actualColumns = await ReadTableColumnsAsync(connection, tableName, cancellationToken);
        if (actualColumns.SequenceEqual(expectedColumns, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        await using (var drop = connection.CreateCommand())
        {
            drop.CommandText = $"DROP TABLE IF EXISTS {tableName};";
            await drop.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var recreate = connection.CreateCommand())
        {
            recreate.CommandText = createSql;
            await recreate.ExecuteNonQueryAsync(cancellationToken);
        }

        return true;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private static async Task<IReadOnlyList<string>> ReadTableColumnsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var columns = new List<string>();
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static async Task SyncFtsTablesAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM resources_fts;
            INSERT INTO resources_fts(id, data_source_id, package_path, type_name, full_tgi, name, description)
            SELECT id, data_source_id, package_path, type_name, full_tgi, COALESCE(name, ''), COALESCE(description, '')
            FROM resources;

            DELETE FROM assets_fts;
            INSERT INTO assets_fts(id, data_source_id, package_path, package_name, display_name, category, description, root_type_name, identity_type, primary_geometry_type, root_tgi)
            SELECT id, data_source_id, package_path, COALESCE(package_name, ''), display_name, COALESCE(category, ''), COALESCE(description, ''), COALESCE(root_type_name, ''), COALESCE(identity_type, ''), COALESCE(primary_geometry_type, ''), root_tgi
            FROM assets
            WHERE COALESCE(is_canonical, 1) = 1;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<SqliteConnection> OpenCatalogConnectionAsync(string databasePath, SqliteConnectionProfile profile, CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = profile == SqliteConnectionProfile.LiveServing ? SqliteCacheMode.Shared : SqliteCacheMode.Private
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = profile == SqliteConnectionProfile.LiveServing
            ? """
              PRAGMA journal_mode = WAL;
              PRAGMA synchronous = NORMAL;
              PRAGMA temp_store = MEMORY;
              """
            : """
              PRAGMA journal_mode = MEMORY;
              PRAGMA synchronous = OFF;
              PRAGMA temp_store = MEMORY;
              PRAGMA locking_mode = EXCLUSIVE;
              """;
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static string BuildCanonicalAssetProjectionSql(string databaseAlias) =>
        $"""
        SELECT '{databaseAlias}' AS shard_name,
               rowid AS asset_rowid,
               data_source_id,
               asset_kind,
               root_tgi,
               COALESCE(logical_root_tgi, root_tgi) AS logical_root_tgi,
               display_name,
               package_path,
               COALESCE(linked_resource_count, 0) AS linked_resource_count,
               COALESCE(has_identity_metadata, 0) AS has_identity_metadata,
               COALESCE(has_exact_geometry_candidate, 0) AS has_exact_geometry_candidate,
               COALESCE(has_material_references, 0) AS has_material_references,
               COALESCE(has_texture_references, 0) AS has_texture_references,
               COALESCE(has_rig_reference, 0) AS has_rig_reference,
               COALESCE(has_geometry_reference, 0) AS has_geometry_reference,
               CASE WHEN thumbnail_tgi IS NOT NULL AND thumbnail_tgi <> '' THEN 1 ELSE 0 END AS has_thumbnail
        FROM {databaseAlias}.assets
        """;

    private static string BuildBuildBuyLogicalRootBackfillProjectionSql(string databaseAlias) =>
        $"""
        SELECT '{databaseAlias}' AS shard_name,
               rowid AS asset_rowid,
               data_source_id,
               asset_kind,
               display_name,
               root_tgi,
               COALESCE(logical_root_tgi, root_tgi) AS logical_root_tgi,
               COALESCE(root_type_name, '') AS root_type_name,
               COALESCE(identity_type, '') AS identity_type,
               package_path,
               substr(root_tgi, length(root_tgi) - 15, 16) AS root_instance_hex
        FROM {databaseAlias}.assets
        """;

    private static string BuildBuildBuyLogicalRootResourceProjectionSql(string databaseAlias) =>
        $"""
        SELECT data_source_id,
               instance_hex,
               scene_root_tgi_hint,
               package_path
        FROM {databaseAlias}.resources
        WHERE type_name = 'ObjectDefinition'
          AND scene_root_tgi_hint IS NOT NULL
          AND scene_root_tgi_hint <> ''
        """;

    private static async Task NormalizeBuildBuyLogicalRootsAsync(SqliteConnection connection, IReadOnlyList<string> databaseAliases, CancellationToken cancellationToken)
    {
        if (databaseAliases.Count == 0)
        {
            return;
        }

        var assetUnionSql = string.Join($"{Environment.NewLine}UNION ALL{Environment.NewLine}", databaseAliases.Select(BuildBuildBuyLogicalRootBackfillProjectionSql));
        var resourceUnionSql = string.Join($"{Environment.NewLine}UNION ALL{Environment.NewLine}", databaseAliases.Select(BuildBuildBuyLogicalRootResourceProjectionSql));
        var applySql = string.Join(
            Environment.NewLine + Environment.NewLine,
            databaseAliases.Select(alias =>
                $"""
                UPDATE {alias}.assets AS asset
                SET logical_root_tgi = (
                    SELECT map.logical_root_tgi
                    FROM temp.buildbuy_family_map AS map
                    WHERE map.data_source_id = asset.data_source_id
                      AND map.root_instance_hex = substr(asset.root_tgi, length(asset.root_tgi) - 15, 16))
                WHERE asset.asset_kind = 'BuildBuy'
                  AND COALESCE(asset.identity_type, asset.root_type_name, '') = 'ObjectCatalog'
                  AND (asset.logical_root_tgi IS NULL OR asset.logical_root_tgi = '' OR asset.logical_root_tgi = asset.root_tgi)
                  AND EXISTS (
                    SELECT 1
                    FROM temp.buildbuy_family_map AS map
                    WHERE map.data_source_id = asset.data_source_id
                      AND map.root_instance_hex = substr(asset.root_tgi, length(asset.root_tgi) - 15, 16));
                """));

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            DROP TABLE IF EXISTS temp.buildbuy_family_map;
            CREATE TEMP TABLE buildbuy_family_map (
                data_source_id TEXT NOT NULL,
                root_instance_hex TEXT NOT NULL,
                logical_root_tgi TEXT NOT NULL,
                PRIMARY KEY (data_source_id, root_instance_hex)
            ) WITHOUT ROWID;

            INSERT INTO temp.buildbuy_family_map(data_source_id, root_instance_hex, logical_root_tgi)
            WITH resource_candidates AS (
                SELECT data_source_id,
                       instance_hex AS root_instance_hex,
                       scene_root_tgi_hint AS logical_root_tgi,
                       package_path,
                       ROW_NUMBER() OVER (
                           PARTITION BY data_source_id, instance_hex
                           ORDER BY
                               CASE WHEN LOWER(REPLACE(package_path, '/', '\')) LIKE '%\delta\%' THEN 1 ELSE 0 END ASC,
                               package_path COLLATE NOCASE ASC,
                               scene_root_tgi_hint COLLATE NOCASE ASC
                       ) AS row_number
                FROM (
            {resourceUnionSql}
                )
            ),
            asset_candidates AS (
                SELECT data_source_id,
                       root_instance_hex,
                       logical_root_tgi,
                       package_path,
                       ROW_NUMBER() OVER (
                           PARTITION BY data_source_id, root_instance_hex
                           ORDER BY
                               CASE WHEN LOWER(REPLACE(package_path, '/', '\')) LIKE '%\delta\%' THEN 1 ELSE 0 END ASC,
                               CASE WHEN identity_type = 'ObjectDefinition' THEN 1 ELSE 0 END DESC,
                               CASE WHEN root_type_name = 'ObjectDefinition' THEN 1 ELSE 0 END DESC,
                               package_path COLLATE NOCASE ASC,
                               logical_root_tgi COLLATE NOCASE ASC
                       ) AS row_number
                FROM (
            {assetUnionSql}
                )
                WHERE asset_kind = 'BuildBuy'
                  AND logical_root_tgi IS NOT NULL
                  AND logical_root_tgi <> ''
                  AND (identity_type = 'ObjectDefinition' OR root_type_name = 'ObjectDefinition')
            ),
            candidates AS (
                SELECT data_source_id, root_instance_hex, logical_root_tgi, package_path, 0 AS source_rank
                FROM resource_candidates
                WHERE row_number = 1
                UNION ALL
                SELECT data_source_id, root_instance_hex, logical_root_tgi, package_path, 1 AS source_rank
                FROM asset_candidates
                WHERE row_number = 1
            ),
            ranked AS (
                SELECT data_source_id,
                       root_instance_hex,
                       logical_root_tgi,
                       ROW_NUMBER() OVER (
                           PARTITION BY data_source_id, root_instance_hex
                           ORDER BY
                               source_rank ASC,
                               CASE WHEN LOWER(REPLACE(package_path, '/', '\')) LIKE '%\delta\%' THEN 1 ELSE 0 END ASC,
                               package_path COLLATE NOCASE ASC,
                               logical_root_tgi COLLATE NOCASE ASC
                       ) AS row_number
                FROM candidates
            )
            SELECT data_source_id, root_instance_hex, logical_root_tgi
            FROM ranked
            WHERE row_number = 1;

            {applySql}

            DROP TABLE temp.buildbuy_family_map;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CanonicalizeAssetsAsync(SqliteConnection connection, IReadOnlyList<string> databaseAliases, CancellationToken cancellationToken)
    {
        if (databaseAliases.Count == 0)
        {
            return;
        }

        var unionSql = string.Join($"{Environment.NewLine}UNION ALL{Environment.NewLine}", databaseAliases.Select(BuildCanonicalAssetProjectionSql));
        var resetSql = string.Join(Environment.NewLine, databaseAliases.Select(alias => $"UPDATE {alias}.assets SET is_canonical = 0;"));
        var applySql = string.Join(
            Environment.NewLine + Environment.NewLine,
            databaseAliases.Select(alias =>
                $"""
                UPDATE {alias}.assets
                SET is_canonical = 1
                WHERE rowid IN (
                    SELECT asset_rowid
                    FROM temp.canonical_assets
                    WHERE shard_name = '{alias}');
                """));

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            DROP TABLE IF EXISTS temp.canonical_assets;
            CREATE TEMP TABLE canonical_assets (
                shard_name TEXT NOT NULL,
                asset_rowid INTEGER NOT NULL,
                PRIMARY KEY (shard_name, asset_rowid)
            ) WITHOUT ROWID;

            INSERT INTO temp.canonical_assets(shard_name, asset_rowid)
            WITH ranked AS (
                SELECT shard_name, asset_rowid,
                       ROW_NUMBER() OVER (
                           PARTITION BY data_source_id, asset_kind, logical_root_tgi
                           ORDER BY
                               CASE WHEN display_name GLOB 'CAS Part *' THEN 0 ELSE 1 END DESC,
                               has_identity_metadata DESC,
                               has_exact_geometry_candidate DESC,
                               has_thumbnail DESC,
                               has_geometry_reference DESC,
                               has_rig_reference DESC,
                               has_material_references DESC,
                               has_texture_references DESC,
                               linked_resource_count DESC,
                               CASE WHEN LOWER(REPLACE(package_path, '/', '\')) LIKE '%\delta\%' THEN 1 ELSE 0 END ASC,
                               LENGTH(COALESCE(display_name, '')) DESC,
                               package_path COLLATE NOCASE ASC,
                               shard_name ASC,
                               asset_rowid ASC
                       ) AS row_number
                FROM (
            {unionSql}
                )
            )
            SELECT shard_name, asset_rowid
            FROM ranked
            WHERE row_number = 1;

            {resetSql}

            {applySql}

            DROP TABLE temp.canonical_assets;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task CanonicalizeAssetsAcrossCatalogAsync(IReadOnlyList<string> databasePaths, SqliteConnectionProfile profile, CancellationToken cancellationToken)
    {
        if (databasePaths.Count == 0)
        {
            return;
        }

        SqliteConnection.ClearAllPools();
        await using var connection = await OpenCatalogConnectionAsync(databasePaths[0], profile, cancellationToken);
        var aliases = new List<string>(databasePaths.Count) { "main" };
        for (var index = 1; index < databasePaths.Count; index++)
        {
            var alias = $"shard{index:00}";
            aliases.Add(alias);
            await using var attach = connection.CreateCommand();
            attach.CommandText = $"ATTACH DATABASE $path AS {alias};";
            attach.Parameters.AddWithValue("$path", databasePaths[index]);
            await attach.ExecuteNonQueryAsync(cancellationToken);
        }

        await NormalizeBuildBuyLogicalRootsAsync(connection, aliases, cancellationToken);
        await CanonicalizeAssetsAsync(connection, aliases, cancellationToken);
    }

    private static async Task FinalizeCatalogDatabasesAsync(
        IReadOnlyList<string> databasePaths,
        SqliteConnectionProfile profile,
        IProgress<IndexWriteStageProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (databasePaths.Count == 0)
        {
            return;
        }

        progress?.Report(new IndexWriteStageProgress("finalizing", "Selecting canonical logical assets."));
        await CanonicalizeAssetsAcrossCatalogAsync(databasePaths, profile, cancellationToken);

        SqliteConnection.ClearAllPools();
        for (var index = 0; index < databasePaths.Count; index++)
        {
            var shardPrefix = databasePaths.Count > 1 ? $"Shard {index + 1}/{databasePaths.Count}: " : string.Empty;
            await using var connection = await OpenCatalogConnectionAsync(databasePaths[index], profile, cancellationToken);

            progress?.Report(new IndexWriteStageProgress("finalizing", $"{shardPrefix}Building full-text search catalog."));
            await EnsureFtsSchemaAsync(connection, cancellationToken);
            await SyncFtsTablesAsync(connection, cancellationToken);

            progress?.Report(new IndexWriteStageProgress("finalizing", $"{shardPrefix}Building browse indexes."));
            await using var command = connection.CreateCommand();
            command.CommandText = SecondaryIndexesSql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<int> CountAsync(SqliteConnection connection, string sql, Action<SqliteCommand> bindParameters, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        bindParameters(command);
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<IReadOnlyList<string>> ReadDistinctAssetValuesAsync(SqliteConnection connection, string assetKind, string columnName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT DISTINCT {columnName}
            FROM assets
            WHERE asset_kind = $assetKind AND COALESCE(is_canonical, 1) = 1 AND {columnName} IS NOT NULL AND {columnName} <> ''
            ORDER BY {columnName};
            """;
        command.Parameters.AddWithValue("$assetKind", assetKind);

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(reader.GetString(0));
        }

        return results;
    }

    private static async Task<IReadOnlyList<string>> ReadDistinctAssetHexValuesAsync(SqliteConnection connection, string assetKind, string columnName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT DISTINCT {columnName}
            FROM assets
            WHERE asset_kind = $assetKind AND COALESCE(is_canonical, 1) = 1 AND {columnName} IS NOT NULL
            ORDER BY {columnName};
            """;
        command.Parameters.AddWithValue("$assetKind", assetKind);

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(FormatCatalogSignal(Convert.ToUInt32(reader.GetInt64(0))));
        }

        return results;
    }

    private static string BuildAssetSort(AssetBrowserSort sort) => sort switch
    {
        AssetBrowserSort.Category => "COALESCE(category, ''), display_name, package_path, root_tgi",
        AssetBrowserSort.Package => "package_path, display_name, root_tgi",
        _ => "display_name, package_path, root_tgi"
    };

    private static string BuildRawResourceSort(RawResourceSort sort) => sort switch
    {
        RawResourceSort.PackagePath => "package_path, type_name, full_tgi",
        RawResourceSort.Tgi => "full_tgi, package_path, type_name",
        _ => "type_name, package_path, full_tgi"
    };

    private static (string WhereClause, Action<SqliteCommand> BindParameters) BuildAssetWhereClause(AssetBrowserQuery query)
    {
        var clauses = new List<string>();
        var binders = new List<Action<SqliteCommand>>();

        ApplySourceScope(query.SourceScope, "source_kind", clauses, binders);
        clauses.Add("COALESCE(is_canonical, 1) = 1");

        clauses.Add(query.Domain switch
        {
            AssetBrowserDomain.BuildBuy => "asset_kind = 'BuildBuy'",
            AssetBrowserDomain.Cas => "asset_kind = 'Cas'",
            AssetBrowserDomain.Sim => "asset_kind = 'Sim'",
            AssetBrowserDomain.General3D => "asset_kind = 'General3D'",
            _ => "asset_kind = 'BuildBuy'"
        });

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            clauses.Add("id IN (SELECT id FROM assets_fts WHERE assets_fts MATCH $searchFts)");
            var searchFts = BuildFtsQuery(query.SearchText);
            binders.Add(command => command.Parameters.AddWithValue("$searchFts", searchFts));
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryText))
        {
            AddLikeFilter(query.CategoryText, clauses, binders, "lower(COALESCE(category, '')) LIKE $category");
        }

        if (!string.IsNullOrWhiteSpace(query.RootTypeText))
        {
            AddLikeFilter(query.RootTypeText, clauses, binders, "lower(COALESCE(root_type_name, '')) LIKE $rootType");
        }

        if (!string.IsNullOrWhiteSpace(query.IdentityTypeText))
        {
            AddLikeFilter(query.IdentityTypeText, clauses, binders, "lower(COALESCE(identity_type, '')) LIKE $identityType");
        }

        if (!string.IsNullOrWhiteSpace(query.PrimaryGeometryTypeText))
        {
            AddLikeFilter(query.PrimaryGeometryTypeText, clauses, binders, "lower(COALESCE(primary_geometry_type, '')) LIKE $geometryType");
        }

        if (!string.IsNullOrWhiteSpace(query.ThumbnailTypeText))
        {
            AddLikeFilter(query.ThumbnailTypeText, clauses, binders, "lower(COALESCE(thumbnail_type_name, '')) LIKE $thumbnailType");
        }

        AddCatalogSignalFilter(query.CatalogSignal0020Text, "catalog_signal_0020", "$catalogSignal0020", clauses, binders);
        AddCatalogSignalFilter(query.CatalogSignal002CText, "catalog_signal_002c", "$catalogSignal002C", clauses, binders);
        AddCatalogSignalFilter(query.CatalogSignal0030Text, "catalog_signal_0030", "$catalogSignal0030", clauses, binders);
        AddCatalogSignalFilter(query.CatalogSignal0034Text, "catalog_signal_0034", "$catalogSignal0034", clauses, binders);

        if (!string.IsNullOrWhiteSpace(query.PackageText))
        {
            AddLikeFilter(query.PackageText, clauses, binders, "lower(package_path) LIKE $package");
        }

        if (!string.IsNullOrWhiteSpace(query.PackageRelativeText))
        {
            AddNormalizedPackageFragmentFilter(query.PackageRelativeText, clauses, binders, "lower(replace(package_path, '/', '\\')) LIKE $packageRelative");
        }

        if (query.HasThumbnailOnly)
        {
            clauses.Add("thumbnail_tgi IS NOT NULL AND thumbnail_tgi <> ''");
        }

        if (query.VariantsOnly)
        {
            clauses.Add("variant_count > 1");
        }

        var capabilityFilter = query.CapabilityFilter;
        if (capabilityFilter is not null)
        {
            if (capabilityFilter.RequireSceneRoot)
            {
                clauses.Add("COALESCE(has_scene_root, 0) = 1");
            }

            if (capabilityFilter.RequireExactGeometryCandidate)
            {
                clauses.Add("COALESCE(has_exact_geometry_candidate, 0) = 1");
            }

            if (capabilityFilter.RequireMaterialReferences)
            {
                clauses.Add("COALESCE(has_material_references, 0) = 1");
            }

            if (capabilityFilter.RequireTextureReferences)
            {
                clauses.Add("COALESCE(has_texture_references, 0) = 1");
            }

            if (capabilityFilter.RequireIdentityMetadata)
            {
                clauses.Add("COALESCE(has_identity_metadata, 0) = 1");
            }

            if (capabilityFilter.RequireRigReference)
            {
                clauses.Add("COALESCE(has_rig_reference, 0) = 1");
            }

            if (capabilityFilter.RequireGeometryReference)
            {
                clauses.Add("COALESCE(has_geometry_reference, 0) = 1");
            }

            if (capabilityFilter.RequireMaterialResourceCandidate)
            {
                clauses.Add("COALESCE(has_material_resource_candidate, 0) = 1");
            }

            if (capabilityFilter.RequireTextureResourceCandidate)
            {
                clauses.Add("COALESCE(has_texture_resource_candidate, 0) = 1");
            }

            if (capabilityFilter.RequirePackageLocalGraph)
            {
                clauses.Add("COALESCE(is_package_local_graph, 0) = 1");
            }

            if (capabilityFilter.RequireDiagnostics)
            {
                clauses.Add("COALESCE(has_diagnostics, 0) = 1");
            }
        }

        return (BuildWhereClause(clauses), command =>
        {
            foreach (var binder in binders)
            {
                binder(command);
            }
        });
    }

    private static (string WhereClause, Action<SqliteCommand> BindParameters) BuildRawResourceWhereClause(RawResourceBrowserQuery query)
    {
        var clauses = new List<string>();
        var binders = new List<Action<SqliteCommand>>();

        ApplySourceScope(query.SourceScope, "source_kind", clauses, binders);

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            clauses.Add("id IN (SELECT id FROM resources_fts WHERE resources_fts MATCH $searchFts)");
            var searchFts = BuildFtsQuery(query.SearchText);
            binders.Add(command => command.Parameters.AddWithValue("$searchFts", searchFts));
        }

        if (!string.IsNullOrWhiteSpace(query.TypeNameText))
        {
            AddLikeFilter(query.TypeNameText, clauses, binders, "lower(type_name) LIKE $type");
        }

        if (!string.IsNullOrWhiteSpace(query.PackageText))
        {
            AddLikeFilter(query.PackageText, clauses, binders, "lower(package_path) LIKE $package");
        }

        if (!string.IsNullOrWhiteSpace(query.GroupHexText))
        {
            AddLikeFilter(query.GroupHexText, clauses, binders, "lower(group_hex) LIKE $group");
        }

        if (!string.IsNullOrWhiteSpace(query.InstanceHexText))
        {
            AddLikeFilter(query.InstanceHexText, clauses, binders, "lower(instance_hex) LIKE $instance");
        }

        if (query.PreviewableOnly)
        {
            clauses.Add("is_previewable = 1");
        }

        if (query.ExportCapableOnly)
        {
            clauses.Add("is_export_capable = 1");
        }

        if (query.CompressedKnownOnly)
        {
            clauses.Add("is_compressed IS NOT NULL");
        }

        if (query.LinkFilter == ResourceLinkFilter.LinkedOnly)
        {
            clauses.Add("asset_linkage_summary <> ''");
        }
        else if (query.LinkFilter == ResourceLinkFilter.UnlinkedOnly)
        {
            clauses.Add("asset_linkage_summary = ''");
        }

        var domainClause = query.Domain switch
        {
            RawResourceDomain.Images => "(preview_kind = 'Texture' OR type_name IN ('BuyBuildThumbnail', 'BodyPartThumbnail', 'CASPartThumbnail'))",
            RawResourceDomain.Audio => "(preview_kind = 'Audio' OR lower(type_name) LIKE '%audio%')",
            RawResourceDomain.TextXml => "(preview_kind = 'Text' OR lower(type_name) LIKE '%xml%' OR lower(type_name) LIKE '%tuning%' OR lower(type_name) LIKE '%manifest%' OR lower(type_name) LIKE '%stringtable%')",
            RawResourceDomain.ThreeDRelated => "(preview_kind = 'Scene' OR type_name IN ('Geometry', 'Model', 'ModelLOD', 'Rig', 'MaterialDefinition'))",
            RawResourceDomain.OtherUnknown => "NOT ((preview_kind = 'Texture' OR type_name IN ('BuyBuildThumbnail', 'BodyPartThumbnail', 'CASPartThumbnail')) OR (preview_kind = 'Audio' OR lower(type_name) LIKE '%audio%') OR (preview_kind = 'Text' OR lower(type_name) LIKE '%xml%' OR lower(type_name) LIKE '%tuning%' OR lower(type_name) LIKE '%manifest%' OR lower(type_name) LIKE '%stringtable%') OR (preview_kind = 'Scene' OR type_name IN ('Geometry', 'Model', 'ModelLOD', 'Rig', 'MaterialDefinition')))",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(domainClause))
        {
            clauses.Add(domainClause);
        }

        return (BuildWhereClause(clauses), command =>
        {
            foreach (var binder in binders)
            {
                binder(command);
            }
        });
    }

    private static void AddCatalogSignalFilter(string filterText, string columnName, string parameterName, ICollection<string> clauses, ICollection<Action<SqliteCommand>> binders)
    {
        if (!TryParseCatalogSignal(filterText, out var value))
        {
            return;
        }

        clauses.Add($"{columnName} = {parameterName}");
        binders.Add(command => command.Parameters.AddWithValue(parameterName, value));
    }

    private static bool TryParseCatalogSignal(string? text, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();
        if (normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[2..];
        }

        return uint.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static string FormatCatalogSignal(uint value) => $"0x{value:X8}";

    private static void ApplySourceScope(SourceScope sourceScope, string columnName, ICollection<string> clauses, ICollection<Action<SqliteCommand>> binders)
    {
        var includedKinds = sourceScope.ToSourceKinds();
        if (includedKinds.Count == 0)
        {
            clauses.Add("1 = 0");
            return;
        }

        if (includedKinds.Count == 3)
        {
            return;
        }

        var parameterNames = new List<string>(includedKinds.Count);
        for (var index = 0; index < includedKinds.Count; index++)
        {
            var parameterName = $"$sourceKind{index}";
            parameterNames.Add(parameterName);
            var value = includedKinds[index].ToString();
            binders.Add(command => command.Parameters.AddWithValue(parameterName, value));
        }

        clauses.Add($"{columnName} IN ({string.Join(", ", parameterNames)})");
    }

    private static void AddLikeFilter(string value, ICollection<string> clauses, ICollection<Action<SqliteCommand>> binders, params string[] expressions)
    {
        var parameterName = expressions
            .SelectMany(static expression => expression.Split([' ', ')', '('], StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(static token => token.StartsWith('$'))
            ?? "$type";
        var normalized = $"%{value.Trim().ToLowerInvariant()}%";
        clauses.Add($"({string.Join(" OR ", expressions)})");
        binders.Add(command => command.Parameters.AddWithValue(parameterName, normalized));
    }

    private static void AddNormalizedPackageFragmentFilter(string value, ICollection<string> clauses, ICollection<Action<SqliteCommand>> binders, params string[] expressions)
    {
        var parameterName = expressions
            .SelectMany(static expression => expression.Split([' ', ')', '('], StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(static token => token.StartsWith('$'))
            ?? "$packageRelative";
        var normalized = $"%{value.Trim().Replace('/', '\\').ToLowerInvariant()}%";
        clauses.Add($"({string.Join(" OR ", expressions)})");
        binders.Add(command => command.Parameters.AddWithValue(parameterName, normalized));
    }

    private static string BuildFtsQuery(string value)
    {
        var terms = value
            .Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static term => term.Replace("\"", "\"\""))
            .Where(static term => !string.IsNullOrWhiteSpace(term))
            .ToArray();

        if (terms.Length == 0)
        {
            return "\"\"";
        }

        return string.Join(" AND ", terms.Select(static term => $"\"{term}\"*"));
    }

    private static string BuildWhereClause(IReadOnlyCollection<string> clauses) =>
        clauses.Count == 0 ? string.Empty : $"{Environment.NewLine}WHERE {string.Join($"{Environment.NewLine}  AND ", clauses)}";

    private static async Task<IReadOnlyList<AssetSummary>> ReadAssetsAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        var results = new List<AssetSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AssetSummary(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                Enum.Parse<SourceKind>(reader.GetString(2)),
                Enum.Parse<AssetKind>(reader.GetString(3)),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.GetString(7),
                ParseTgi(reader.GetString(8), string.Empty),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                reader.GetString(17),
                new AssetCapabilitySnapshot(
                    reader.IsDBNull(13) ? true : ReadOptionalBool(reader, 13),
                    ReadOptionalBool(reader, 14),
                    ReadOptionalBool(reader, 15),
                    ReadOptionalBool(reader, 16),
                    HasThumbnail: !reader.IsDBNull(10) && !string.IsNullOrWhiteSpace(reader.GetString(10)),
                    HasVariants: reader.GetInt32(11) > 1,
                    HasIdentityMetadata: ReadOptionalBool(reader, 24),
                    HasRigReference: ReadOptionalBool(reader, 25),
                    HasGeometryReference: ReadOptionalBool(reader, 26),
                    HasMaterialResourceCandidate: ReadOptionalBool(reader, 27),
                    HasTextureResourceCandidate: ReadOptionalBool(reader, 28),
                    IsPackageLocalGraph: ReadOptionalBool(reader, 29),
                    HasDiagnostics: ReadOptionalBool(reader, 30)),
                PackageName: reader.IsDBNull(18) ? null : reader.GetString(18),
                RootTypeName: reader.IsDBNull(19) ? null : reader.GetString(19),
                ThumbnailTypeName: reader.IsDBNull(20) ? null : reader.GetString(20),
                PrimaryGeometryType: reader.IsDBNull(21) ? null : reader.GetString(21),
                IdentityType: reader.IsDBNull(22) ? null : reader.GetString(22),
                CategoryNormalized: reader.IsDBNull(23) ? null : reader.GetString(23),
                Description: reader.IsDBNull(6) ? null : reader.GetString(6),
                CatalogSignal0020: reader.IsDBNull(31) ? null : Convert.ToUInt32(reader.GetInt64(31)),
                CatalogSignal002C: reader.IsDBNull(32) ? null : Convert.ToUInt32(reader.GetInt64(32)),
                CatalogSignal0030: reader.IsDBNull(33) ? null : Convert.ToUInt32(reader.GetInt64(33)),
                CatalogSignal0034: reader.IsDBNull(34) ? null : Convert.ToUInt32(reader.GetInt64(34)),
                LogicalRootTgi: reader.IsDBNull(9) ? null : reader.GetString(9)));
        }

        return results;
    }

    private static async Task EnsureDeferredMetadataSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(resources);";
        var requiresMigration = false;
        await using (var reader = await pragma.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var columnName = reader.GetString(1);
                var isNotNull = reader.GetInt32(3) == 1;
                if (isNotNull && columnName is "compressed_size" or "uncompressed_size" or "is_compressed")
                {
                    requiresMigration = true;
                    break;
                }
            }
        }

        if (!requiresMigration)
        {
            return;
        }

        await using var migration = connection.CreateCommand();
        migration.CommandText =
            """
            ALTER TABLE resources RENAME TO resources_legacy;

            CREATE TABLE resources (
                id TEXT NOT NULL,
                data_source_id TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                package_path TEXT NOT NULL,
                type_hex TEXT NOT NULL,
                type_name TEXT NOT NULL,
                group_hex TEXT NOT NULL,
                instance_hex TEXT NOT NULL,
                full_tgi TEXT NOT NULL,
                name TEXT NULL,
                description TEXT NULL,
                catalog_signal_0020 INTEGER NULL,
                catalog_signal_002c INTEGER NULL,
                catalog_signal_0030 INTEGER NULL,
                catalog_signal_0034 INTEGER NULL,
                compressed_size INTEGER NULL,
                uncompressed_size INTEGER NULL,
                is_compressed INTEGER NULL,
                preview_kind TEXT NOT NULL,
                is_previewable INTEGER NOT NULL,
                is_export_capable INTEGER NOT NULL,
                asset_linkage_summary TEXT NOT NULL,
                diagnostics TEXT NOT NULL,
                scene_root_tgi_hint TEXT NULL
            );

            INSERT INTO resources(
                id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint)
            SELECT
                id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, NULL,
                NULL, NULL, NULL, NULL,
                compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, NULL
            FROM resources_legacy;

            DROP TABLE resources_legacy;

            CREATE UNIQUE INDEX IF NOT EXISTS ux_resources_id ON resources(id);
            CREATE INDEX IF NOT EXISTS ix_resources_search ON resources(type_name, full_tgi, package_path, name);
            CREATE INDEX IF NOT EXISTS ix_resources_source ON resources(data_source_id, package_path);
            """;
        await migration.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool ReadOptionalBool(SqliteDataReader reader, int ordinal) =>
        !reader.IsDBNull(ordinal) && reader.GetInt32(ordinal) == 1;

    private static async Task EnsureScanTokenSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await EnsureColumnAsync(connection, "resources", "scan_token", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "resources", "description", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "resources", "catalog_signal_0020", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "resources", "catalog_signal_002c", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "resources", "catalog_signal_0030", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "resources", "catalog_signal_0034", "INTEGER NULL", cancellationToken);
        await EnsureColumnAsync(connection, "resources", "scene_root_tgi_hint", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "assets", "scan_token", "TEXT NULL", cancellationToken);
        await EnsureColumnAsync(connection, "asset_variants", "scan_token", "TEXT NULL", cancellationToken);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string tableName, string columnName, string declaration, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = $"PRAGMA table_info({tableName});";
            await using var reader = await pragma.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }
        }

        if (columns.Contains(columnName))
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {declaration};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureAssetSummarySchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA table_info(assets);";
            await using var reader = await pragma.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(1));
            }
        }

        await EnsureAssetColumnAsync(connection, columns, "has_scene_root", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_exact_geometry_candidate", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_material_references", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_texture_references", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "description", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "catalog_signal_0020", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "catalog_signal_002c", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "catalog_signal_0030", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "catalog_signal_0034", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "category_normalized", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "is_canonical", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "package_name", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "root_type_name", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "logical_root_tgi", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "thumbnail_type_name", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "primary_geometry_type", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "identity_type", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_identity_metadata", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_rig_reference", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_geometry_reference", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_material_resource_candidate", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_texture_resource_candidate", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "is_package_local_graph", "INTEGER NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "has_diagnostics", "INTEGER NULL", cancellationToken);
    }

    private static async Task EnsureAssetColumnAsync(SqliteConnection connection, ISet<string> columns, string columnName, string declaration, CancellationToken cancellationToken)
    {
        if (columns.Contains(columnName))
        {
            return;
        }

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE assets ADD COLUMN {columnName} {declaration};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
        columns.Add(columnName);
    }

    private sealed class SqliteIndexWriteSession : IIndexWriteSession, IIndexWriteSessionMetricsProvider
    {
        private static readonly string[] ResourceInsertColumns =
        [
            "id", "data_source_id", "source_kind", "package_path", "scan_token", "type_hex", "type_name", "group_hex", "instance_hex", "full_tgi", "name", "description",
            "catalog_signal_0020", "catalog_signal_002c", "catalog_signal_0030", "catalog_signal_0034",
            "compressed_size", "uncompressed_size", "is_compressed", "preview_kind", "is_previewable", "is_export_capable", "asset_linkage_summary", "diagnostics", "scene_root_tgi_hint"
        ];

        private static readonly string[] ResourceInsertParameterBases =
        [
            "id", "dataSourceId", "sourceKind", "packagePath", "scanToken", "typeHex", "typeName", "groupHex", "instanceHex", "fullTgi", "name", "description",
            "catalogSignal0020", "catalogSignal002C", "catalogSignal0030", "catalogSignal0034",
            "compressedSize", "uncompressedSize", "isCompressed", "previewKind", "isPreviewable", "isExportCapable", "assetLinkageSummary", "diagnostics", "sceneRootTgiHint"
        ];

        private static readonly string[] AssetInsertColumns =
        [
            "id", "data_source_id", "source_kind", "asset_kind", "display_name", "category", "description", "catalog_signal_0020", "catalog_signal_002c", "catalog_signal_0030", "catalog_signal_0034", "category_normalized",
            "package_path", "scan_token", "is_canonical", "package_name", "root_tgi", "logical_root_tgi", "root_type_name", "thumbnail_tgi", "thumbnail_type_name",
            "primary_geometry_type", "identity_type", "variant_count", "linked_resource_count", "has_scene_root", "has_exact_geometry_candidate", "has_material_references", "has_texture_references",
            "has_identity_metadata", "has_rig_reference", "has_geometry_reference", "has_material_resource_candidate", "has_texture_resource_candidate", "is_package_local_graph", "has_diagnostics", "diagnostics"
        ];

        private static readonly string[] AssetInsertParameterBases =
        [
            "id", "dataSourceId", "sourceKind", "assetKind", "displayName", "category", "description", "catalogSignal0020", "catalogSignal002C", "catalogSignal0030", "catalogSignal0034", "categoryNormalized",
            "packagePath", "scanToken", "isCanonical", "packageName", "rootTgi", "logicalRootTgi", "rootTypeName", "thumbnailTgi", "thumbnailTypeName",
            "primaryGeometryType", "identityType", "variantCount", "linkedResourceCount", "hasSceneRoot", "hasExactGeometryCandidate", "hasMaterialReferences", "hasTextureReferences",
            "hasIdentityMetadata", "hasRigReference", "hasGeometryReference", "hasMaterialResourceCandidate", "hasTextureResourceCandidate", "isPackageLocalGraph", "hasDiagnostics", "diagnostics"
        ];

        private static readonly string[] AssetVariantInsertColumns =
        [
            "asset_id", "data_source_id", "source_kind", "asset_kind", "package_path", "scan_token", "root_tgi",
            "variant_index", "variant_kind", "display_label", "swatch_hex", "thumbnail_tgi", "diagnostics"
        ];

        private static readonly string[] AssetVariantInsertParameterBases =
        [
            "assetId", "dataSourceId", "sourceKind", "assetKind", "packagePath", "scanToken", "rootTgi",
            "variantIndex", "variantKind", "displayLabel", "swatchHex", "thumbnailTgi", "diagnostics"
        ];

        private static readonly string[] SimTemplateFactInsertColumns =
        [
            "resource_id", "data_source_id", "source_kind", "package_path", "root_tgi", "archetype_key",
            "species_label", "age_label", "gender_label", "display_name", "notes",
            "outfit_category_count", "outfit_entry_count", "outfit_part_count",
            "body_modifier_count", "face_modifier_count", "sculpt_count", "has_skintone",
            "authoritative_body_driving_outfit_count", "authoritative_body_driving_outfit_part_count"
        ];

        private static readonly string[] SimTemplateFactInsertParameterBases =
        [
            "resourceId", "dataSourceId", "sourceKind", "packagePath", "rootTgi", "archetypeKey",
            "speciesLabel", "ageLabel", "genderLabel", "displayName", "notes",
            "outfitCategoryCount", "outfitEntryCount", "outfitPartCount",
            "bodyModifierCount", "faceModifierCount", "sculptCount", "hasSkintone",
            "authoritativeBodyDrivingOutfitCount", "authoritativeBodyDrivingOutfitPartCount"
        ];

        private static readonly string[] SimTemplateBodyPartInsertColumns =
        [
            "resource_id", "data_source_id", "source_kind", "package_path", "root_tgi",
            "outfit_category_value", "outfit_category_label", "outfit_index", "part_index",
            "body_type", "body_type_label", "part_instance_hex", "is_body_driving"
        ];

        private static readonly string[] SimTemplateBodyPartInsertParameterBases =
        [
            "resourceId", "dataSourceId", "sourceKind", "packagePath", "rootTgi",
            "outfitCategoryValue", "outfitCategoryLabel", "outfitIndex", "partIndex",
            "bodyType", "bodyTypeLabel", "partInstanceHex", "isBodyDriving"
        ];

        private static readonly string[] CasPartFactInsertColumns =
        [
            "asset_id", "data_source_id", "source_kind", "package_path", "root_tgi",
            "slot_category", "category_normalized", "body_type", "internal_name",
            "default_body_type", "default_body_type_female", "default_body_type_male",
            "has_naked_link", "restrict_opposite_gender", "restrict_opposite_frame",
            "sort_layer", "species_label", "age_label", "gender_label"
        ];

        private static readonly string[] CasPartFactInsertParameterBases =
        [
            "assetId", "dataSourceId", "sourceKind", "packagePath", "rootTgi",
            "slotCategory", "categoryNormalized", "bodyType", "internalName",
            "defaultBodyType", "defaultBodyTypeFemale", "defaultBodyTypeMale",
            "hasNakedLink", "restrictOppositeGender", "restrictOppositeFrame",
            "sortLayer", "speciesLabel", "ageLabel", "genderLabel"
        ];

        private sealed class BatchMetricsAccumulator
        {
            public TimeSpan DropIndexesElapsed { get; set; }
            public TimeSpan DeleteElapsed { get; set; }
            public TimeSpan InsertResourcesElapsed { get; set; }
            public TimeSpan InsertAssetsElapsed { get; set; }
            public TimeSpan FtsElapsed { get; set; }
            public TimeSpan RebuildIndexesElapsed { get; set; }
            public TimeSpan CommitElapsed { get; set; }
        }

        private sealed record FtsPackageKey(Guid DataSourceId, string PackagePath);

        private sealed record IndexedCasPartFact(
            Guid AssetId,
            Guid DataSourceId,
            SourceKind SourceKind,
            string PackagePath,
            string RootTgi,
            string SlotCategory,
            string? CategoryNormalized,
            int BodyType,
            string? InternalName,
            bool DefaultForBodyType,
            bool DefaultForBodyTypeFemale,
            bool DefaultForBodyTypeMale,
            bool HasNakedLink,
            bool RestrictOppositeGender,
            bool RestrictOppositeFrame,
            int SortLayer,
            string? SpeciesLabel,
            string AgeLabel,
            string GenderLabel);

        private sealed class PreparedInsertCommand(SqliteCommand command, SqliteParameter[] parameters) : IAsyncDisposable
        {
            public SqliteCommand Command { get; } = command;
            public SqliteParameter[] Parameters { get; } = parameters;

            public async ValueTask DisposeAsync() => await Command.DisposeAsync();
        }

        private sealed class PreparedBatchPackageScopeCommand(SqliteCommand command, SqliteParameter[] parameters) : IAsyncDisposable
        {
            public SqliteCommand Command { get; } = command;
            public SqliteParameter[] Parameters { get; } = parameters;

            public async ValueTask DisposeAsync() => await Command.DisposeAsync();
        }

        private readonly SqliteConnection connection;
        private readonly SqliteWriteSessionMode mode;
        private readonly string? shadowDatabasePath;
        private readonly Func<string, CancellationToken, Task>? activateShadowDatabaseAsync;
        private readonly SqliteCommand deletePackageResourcesCommand;
        private readonly SqliteCommand deletePackageAssetsCommand;
        private readonly SqliteCommand deletePackageAssetVariantsCommand;
        private readonly SqliteCommand deletePackageSimTemplateFactsCommand;
        private readonly SqliteCommand deletePackageSimTemplateBodyPartsCommand;
        private readonly SqliteCommand deletePackageCasPartFactsCommand;
        private readonly SqliteCommand upsertPackageCommand;
        private readonly PreparedInsertCommand preparedResourceInsertCommand;
        private readonly PreparedInsertCommand preparedAssetInsertCommand;
        private readonly PreparedInsertCommand preparedAssetVariantInsertCommand;
        private readonly PreparedInsertCommand preparedSimTemplateFactInsertCommand;
        private readonly PreparedInsertCommand preparedSimTemplateBodyPartInsertCommand;
        private readonly PreparedInsertCommand preparedCasPartFactInsertCommand;
        private IndexWriteMetrics? lastMetrics;
        private bool secondaryIndexesPending;
        private bool deferFtsSyncUntilFinalize;
        private bool ftsSyncPending;
        private bool commandsDisposed;
        private bool finalized;
        private readonly BatchMetricsAccumulator lifetimeMetrics = new();
        private readonly Dictionary<int, PreparedBatchPackageScopeCommand> preparedDeleteResourcesFtsCommands = [];
        private readonly Dictionary<int, PreparedBatchPackageScopeCommand> preparedDeleteAssetsFtsCommands = [];
        private readonly Dictionary<int, PreparedBatchPackageScopeCommand> preparedSyncResourcesFtsCommands = [];
        private readonly Dictionary<int, PreparedBatchPackageScopeCommand> preparedSyncAssetsFtsCommands = [];

        public SqliteIndexWriteSession(
            SqliteConnection connection,
            SqliteWriteSessionMode mode,
            string? shadowDatabasePath = null,
            Func<string, CancellationToken, Task>? activateShadowDatabaseAsync = null)
        {
            this.connection = connection;
            this.mode = mode;
            this.shadowDatabasePath = shadowDatabasePath;
            this.activateShadowDatabaseAsync = activateShadowDatabaseAsync;
            deletePackageResourcesCommand = CreateDeletePackageResourcesCommand(connection);
            deletePackageAssetsCommand = CreateDeletePackageAssetsCommand(connection);
            deletePackageAssetVariantsCommand = CreateDeletePackageAssetVariantsCommand(connection);
            deletePackageSimTemplateFactsCommand = CreateDeletePackageSimTemplateFactsCommand(connection);
            deletePackageSimTemplateBodyPartsCommand = CreateDeletePackageSimTemplateBodyPartsCommand(connection);
            deletePackageCasPartFactsCommand = CreateDeletePackageCasPartFactsCommand(connection);
            upsertPackageCommand = CreateUpsertPackageCommand(connection);
            preparedResourceInsertCommand = CreatePreparedInsertCommand("resources", ResourceInsertColumns, ResourceInsertParameterBases);
            preparedAssetInsertCommand = CreatePreparedInsertCommand("assets", AssetInsertColumns, AssetInsertParameterBases);
            preparedAssetVariantInsertCommand = CreatePreparedInsertCommand("asset_variants", AssetVariantInsertColumns, AssetVariantInsertParameterBases);
            preparedSimTemplateFactInsertCommand = CreatePreparedInsertCommand("sim_template_facts", SimTemplateFactInsertColumns, SimTemplateFactInsertParameterBases);
            preparedSimTemplateBodyPartInsertCommand = CreatePreparedInsertCommand("sim_template_body_parts", SimTemplateBodyPartInsertColumns, SimTemplateBodyPartInsertParameterBases);
            preparedCasPartFactInsertCommand = CreatePreparedInsertCommand("cas_part_facts", CasPartFactInsertColumns, CasPartFactInsertParameterBases);
            PrepareCommands();
        }

        internal string DatabasePath => connection.DataSource;

        public async Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
        {
            var metrics = new BatchMetricsAccumulator();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            var packageKey = await ReplacePackageCoreAsync(transaction, packageScan, assets, cancellationToken, metrics);
            if (deferFtsSyncUntilFinalize)
            {
                ftsSyncPending = true;
            }
            else
            {
                var ftsStopwatch = Stopwatch.StartNew();
                await SyncFtsForPackagesAsync(transaction, [packageKey], cancellationToken);
                ftsStopwatch.Stop();
                metrics.FtsElapsed += ftsStopwatch.Elapsed;
            }
            var commitStopwatch = Stopwatch.StartNew();
            await transaction.CommitAsync(cancellationToken);
            commitStopwatch.Stop();
            metrics.CommitElapsed += commitStopwatch.Elapsed;
            lastMetrics = new IndexWriteMetrics(
                lifetimeMetrics.DropIndexesElapsed + metrics.DropIndexesElapsed,
                metrics.DeleteElapsed,
                metrics.InsertResourcesElapsed,
                metrics.InsertAssetsElapsed,
                metrics.FtsElapsed,
                lifetimeMetrics.RebuildIndexesElapsed + metrics.RebuildIndexesElapsed,
                metrics.CommitElapsed,
                packageScan.Resources.Count,
                assets.Count,
                1);
        }

        public async Task ReplacePackagesAsync(IReadOnlyList<(PackageScanResult PackageScan, IReadOnlyList<AssetSummary> Assets)> batch, CancellationToken cancellationToken)
        {
            if (batch.Count == 0)
            {
                return;
            }

            var metrics = new BatchMetricsAccumulator();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            var packagesToSync = new List<FtsPackageKey>(batch.Count);
            foreach (var item in batch)
            {
                packagesToSync.Add(await ReplacePackageCoreAsync(transaction, item.PackageScan, item.Assets, cancellationToken, metrics));
            }

            if (deferFtsSyncUntilFinalize)
            {
                ftsSyncPending = true;
            }
            else
            {
                var ftsStopwatch = Stopwatch.StartNew();
                await SyncFtsForPackagesAsync(transaction, packagesToSync, cancellationToken);
                ftsStopwatch.Stop();
                metrics.FtsElapsed += ftsStopwatch.Elapsed;
            }

            var commitStopwatch = Stopwatch.StartNew();
            await transaction.CommitAsync(cancellationToken);
            commitStopwatch.Stop();
            metrics.CommitElapsed += commitStopwatch.Elapsed;
            lastMetrics = new IndexWriteMetrics(
                lifetimeMetrics.DropIndexesElapsed + metrics.DropIndexesElapsed,
                metrics.DeleteElapsed,
                metrics.InsertResourcesElapsed,
                metrics.InsertAssetsElapsed,
                metrics.FtsElapsed,
                lifetimeMetrics.RebuildIndexesElapsed + metrics.RebuildIndexesElapsed,
                metrics.CommitElapsed,
                batch.Sum(static item => item.PackageScan.Resources.Count),
                batch.Sum(static item => item.Assets.Count),
                batch.Count);
        }

        public async Task PrepareForBulkWriteAsync(CancellationToken cancellationToken)
        {
            deferFtsSyncUntilFinalize = true;
            if (secondaryIndexesPending)
            {
                return;
            }

            secondaryIndexesPending = true;
            if (mode == SqliteWriteSessionMode.ShadowRebuild)
            {
                // A fresh rebuild still needs empty FTS tables created and populated during finalize.
                ftsSyncPending = true;
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                DROP INDEX IF EXISTS ix_resources_search;
                DROP INDEX IF EXISTS ix_resources_package_instance;
                DROP INDEX IF EXISTS ix_assets_search;
                DROP INDEX IF EXISTS ix_assets_canonical_root;
                DROP INDEX IF EXISTS ix_asset_variants_asset;
                DROP INDEX IF EXISTS ix_asset_variants_source;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            stopwatch.Stop();
            lifetimeMetrics.DropIndexesElapsed += stopwatch.Elapsed;
        }

        private async Task<FtsPackageKey> ReplacePackageCoreAsync(SqliteTransaction transaction, PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken, BatchMetricsAccumulator metrics)
        {
            var scanToken = Guid.NewGuid().ToString("N");

            if (mode == SqliteWriteSessionMode.LiveReplace)
            {
                var deleteStopwatch = Stopwatch.StartNew();
                Bind(deletePackageResourcesCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
                await deletePackageResourcesCommand.ExecuteNonQueryAsync(cancellationToken);

                Bind(deletePackageAssetsCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
                await deletePackageAssetsCommand.ExecuteNonQueryAsync(cancellationToken);

                Bind(deletePackageAssetVariantsCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
                await deletePackageAssetVariantsCommand.ExecuteNonQueryAsync(cancellationToken);

                Bind(deletePackageSimTemplateFactsCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
                await deletePackageSimTemplateFactsCommand.ExecuteNonQueryAsync(cancellationToken);

                Bind(deletePackageSimTemplateBodyPartsCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
                await deletePackageSimTemplateBodyPartsCommand.ExecuteNonQueryAsync(cancellationToken);

                Bind(deletePackageCasPartFactsCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
                await deletePackageCasPartFactsCommand.ExecuteNonQueryAsync(cancellationToken);
                deleteStopwatch.Stop();
                metrics.DeleteElapsed += deleteStopwatch.Elapsed;
            }

            Bind(upsertPackageCommand, transaction, packageScan);
            await upsertPackageCommand.ExecuteNonQueryAsync(cancellationToken);

            var resourceInsertStopwatch = Stopwatch.StartNew();
            BulkInsertResources(transaction, packageScan.Resources, scanToken, cancellationToken);
            resourceInsertStopwatch.Stop();
            metrics.InsertResourcesElapsed += resourceInsertStopwatch.Elapsed;

            var assetInsertStopwatch = Stopwatch.StartNew();
            BulkInsertAssets(transaction, assets, scanToken, cancellationToken);
            var assetVariants = MaterializeAssetVariants(packageScan, assets);
            BulkInsertAssetVariants(transaction, assetVariants, scanToken, cancellationToken);
            BulkInsertSimTemplateFacts(transaction, packageScan.SimTemplateFacts, cancellationToken);
            BulkInsertSimTemplateBodyParts(transaction, packageScan.SimTemplateBodyPartFacts, cancellationToken);
            var casPartFacts = MaterializeCasPartFacts(packageScan, assets);
            BulkInsertCasPartFacts(transaction, casPartFacts, cancellationToken);
            assetInsertStopwatch.Stop();
            metrics.InsertAssetsElapsed += assetInsertStopwatch.Elapsed;

            return new FtsPackageKey(packageScan.DataSourceId, packageScan.PackagePath);
        }

        public async Task FinalizeAsync(IProgress<IndexWriteStageProgress>? progress, CancellationToken cancellationToken)
        {
            if (finalized)
            {
                return;
            }

            var ftsElapsed = TimeSpan.Zero;
            if (ftsSyncPending || secondaryIndexesPending)
            {
                progress?.Report(new IndexWriteStageProgress("finalizing", "Selecting canonical logical assets."));
                await CanonicalizeAssetsAsync(connection, ["main"], cancellationToken);
            }

            if (ftsSyncPending)
            {
                progress?.Report(new IndexWriteStageProgress("finalizing", "Building full-text search catalog."));
                var ftsStopwatch = Stopwatch.StartNew();
                await EnsureFtsSchemaAsync(connection, cancellationToken);
                await SyncFtsTablesAsync(connection, cancellationToken);
                ftsStopwatch.Stop();
                ftsElapsed = ftsStopwatch.Elapsed;
                ftsSyncPending = false;
            }

            if (secondaryIndexesPending)
            {
                progress?.Report(new IndexWriteStageProgress("finalizing", "Building browse indexes."));
                await RebuildSecondaryIndexesAsync(cancellationToken);
            }

            if (mode == SqliteWriteSessionMode.ShadowRebuild &&
                shadowDatabasePath is not null &&
                activateShadowDatabaseAsync is not null)
            {
                progress?.Report(new IndexWriteStageProgress("finalizing", "Activating rebuilt catalog."));
                await DisposeCommandsAsync();
                await activateShadowDatabaseAsync(shadowDatabasePath, cancellationToken);
            }

            lastMetrics = new IndexWriteMetrics(
                lifetimeMetrics.DropIndexesElapsed,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                ftsElapsed,
                lifetimeMetrics.RebuildIndexesElapsed,
                TimeSpan.Zero,
                0,
                0,
                0);
            finalized = true;
        }

        internal async Task ReleaseForExternalFinalizeAsync()
        {
            finalized = true;
            await DisposeCommandsAsync();
        }

        public IndexWriteMetrics? ConsumeLastMetrics()
        {
            var metrics = lastMetrics;
            lastMetrics = null;
            return metrics;
        }

        private void BulkInsertResources(SqliteTransaction transaction, IReadOnlyList<ResourceMetadata> resources, string scanToken, CancellationToken cancellationToken)
        {
            preparedResourceInsertCommand.Command.Transaction = transaction;
            for (var index = 0; index < resources.Count; index++)
            {
                if ((index & 255) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                SetResourceParameterValues(preparedResourceInsertCommand.Parameters, resources[index], scanToken);
                preparedResourceInsertCommand.Command.ExecuteNonQuery();
            }
        }

        private void BulkInsertAssets(SqliteTransaction transaction, IReadOnlyList<AssetSummary> assets, string scanToken, CancellationToken cancellationToken)
        {
            preparedAssetInsertCommand.Command.Transaction = transaction;
            for (var index = 0; index < assets.Count; index++)
            {
                if ((index & 255) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                SetAssetParameterValues(preparedAssetInsertCommand.Parameters, assets[index], scanToken);
                preparedAssetInsertCommand.Command.ExecuteNonQuery();
            }
        }

        private void BulkInsertAssetVariants(SqliteTransaction transaction, IReadOnlyList<AssetVariantSummary> variants, string scanToken, CancellationToken cancellationToken)
        {
            preparedAssetVariantInsertCommand.Command.Transaction = transaction;
            for (var index = 0; index < variants.Count; index++)
            {
                if ((index & 255) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                SetAssetVariantParameterValues(preparedAssetVariantInsertCommand.Parameters, variants[index], scanToken);
                preparedAssetVariantInsertCommand.Command.ExecuteNonQuery();
            }
        }

        private void BulkInsertSimTemplateFacts(SqliteTransaction transaction, IReadOnlyList<SimTemplateFactSummary> facts, CancellationToken cancellationToken)
        {
            preparedSimTemplateFactInsertCommand.Command.Transaction = transaction;
            for (var index = 0; index < facts.Count; index++)
            {
                if ((index & 255) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                SetSimTemplateFactParameterValues(preparedSimTemplateFactInsertCommand.Parameters, facts[index]);
                preparedSimTemplateFactInsertCommand.Command.ExecuteNonQuery();
            }
        }

        private void BulkInsertSimTemplateBodyParts(SqliteTransaction transaction, IReadOnlyList<SimTemplateBodyPartFact> bodyParts, CancellationToken cancellationToken)
        {
            preparedSimTemplateBodyPartInsertCommand.Command.Transaction = transaction;
            for (var index = 0; index < bodyParts.Count; index++)
            {
                if ((index & 255) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                SetSimTemplateBodyPartParameterValues(preparedSimTemplateBodyPartInsertCommand.Parameters, bodyParts[index]);
                preparedSimTemplateBodyPartInsertCommand.Command.ExecuteNonQuery();
            }
        }

        private void BulkInsertCasPartFacts(SqliteTransaction transaction, IReadOnlyList<IndexedCasPartFact> facts, CancellationToken cancellationToken)
        {
            preparedCasPartFactInsertCommand.Command.Transaction = transaction;
            for (var index = 0; index < facts.Count; index++)
            {
                if ((index & 255) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                SetCasPartFactParameterValues(preparedCasPartFactInsertCommand.Parameters, facts[index]);
                preparedCasPartFactInsertCommand.Command.ExecuteNonQuery();
            }
        }

        private static IReadOnlyList<AssetVariantSummary> MaterializeAssetVariants(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets)
        {
            if (packageScan.AssetVariants.Count == 0 || assets.Count == 0)
            {
                return [];
            }

            var assetsByRoot = assets.ToDictionary(
                static asset => $"{asset.AssetKind}|{asset.RootKey.FullTgi}",
                StringComparer.OrdinalIgnoreCase);
            var variants = new List<AssetVariantSummary>(packageScan.AssetVariants.Count);
            foreach (var variant in packageScan.AssetVariants)
            {
                if (!assetsByRoot.TryGetValue($"{variant.AssetKind}|{variant.RootKey.FullTgi}", out var asset))
                {
                    continue;
                }

                variants.Add(new AssetVariantSummary(
                    asset.Id,
                    variant.DataSourceId,
                    variant.SourceKind,
                    variant.AssetKind,
                    variant.PackagePath,
                    variant.RootKey.FullTgi,
                    variant.VariantIndex,
                    variant.VariantKind,
                    variant.DisplayLabel,
                    variant.SwatchHex,
                    variant.ThumbnailTgi,
                    variant.Diagnostics));
            }

            return variants;
        }

        private static IReadOnlyList<IndexedCasPartFact> MaterializeCasPartFacts(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets)
        {
            if (packageScan.CasPartFacts.Count == 0 || assets.Count == 0)
            {
                return [];
            }

            var assetsByRoot = assets
                .Where(static asset => asset.AssetKind == AssetKind.Cas)
                .ToDictionary(
                    static asset => $"{asset.DataSourceId:D}|{asset.PackagePath}|{asset.RootKey.FullTgi}",
                    StringComparer.OrdinalIgnoreCase);
            var facts = new List<IndexedCasPartFact>(packageScan.CasPartFacts.Count);
            foreach (var fact in packageScan.CasPartFacts)
            {
                if (!assetsByRoot.TryGetValue($"{fact.DataSourceId:D}|{fact.PackagePath}|{fact.RootTgi}", out var asset))
                {
                    continue;
                }

                facts.Add(new IndexedCasPartFact(
                    asset.Id,
                    fact.DataSourceId,
                    fact.SourceKind,
                    fact.PackagePath,
                    fact.RootTgi,
                    fact.SlotCategory,
                    fact.CategoryNormalized,
                    fact.BodyType,
                    fact.InternalName,
                    fact.DefaultForBodyType,
                    fact.DefaultForBodyTypeFemale,
                    fact.DefaultForBodyTypeMale,
                    fact.HasNakedLink,
                    fact.RestrictOppositeGender,
                    fact.RestrictOppositeFrame,
                    fact.SortLayer,
                    fact.SpeciesLabel,
                    fact.AgeLabel,
                    fact.GenderLabel));
            }

            return facts;
        }

        private async Task SyncFtsForPackagesAsync(SqliteTransaction transaction, IReadOnlyList<FtsPackageKey> packageKeys, CancellationToken cancellationToken)
        {
            // Keep FTS incremental for just the touched packages so large runs do not end with a full-table rebuild.
            var distinctPackages = packageKeys
                .Distinct()
                .ToArray();
            if (distinctPackages.Length == 0)
            {
                return;
            }

            var deleteResourcesCommand = GetOrCreatePreparedBatchFtsDeleteCommand(
                preparedDeleteResourcesFtsCommands,
                "resources_fts",
                distinctPackages.Length);
            BindBatchPackageScope(deleteResourcesCommand.Parameters, distinctPackages);
            deleteResourcesCommand.Command.Transaction = transaction;
            await deleteResourcesCommand.Command.ExecuteNonQueryAsync(cancellationToken);

            var deleteAssetsCommand = GetOrCreatePreparedBatchFtsDeleteCommand(
                preparedDeleteAssetsFtsCommands,
                "assets_fts",
                distinctPackages.Length);
            BindBatchPackageScope(deleteAssetsCommand.Parameters, distinctPackages);
            deleteAssetsCommand.Command.Transaction = transaction;
            await deleteAssetsCommand.Command.ExecuteNonQueryAsync(cancellationToken);

            var syncResourcesCommand = GetOrCreatePreparedBatchFtsInsertCommand(
                preparedSyncResourcesFtsCommands,
                "resources_fts",
                "resources",
                "r",
                "r.id, r.data_source_id, r.package_path, r.type_name, r.full_tgi, COALESCE(r.name, ''), COALESCE(r.description, '')",
                distinctPackages.Length);
            BindBatchPackageScope(syncResourcesCommand.Parameters, distinctPackages);
            syncResourcesCommand.Command.Transaction = transaction;
            await syncResourcesCommand.Command.ExecuteNonQueryAsync(cancellationToken);

            var syncAssetsCommand = GetOrCreatePreparedBatchFtsInsertCommand(
                preparedSyncAssetsFtsCommands,
                "assets_fts",
                "assets",
                "a",
                "a.id, a.data_source_id, a.package_path, COALESCE(a.package_name, ''), a.display_name, COALESCE(a.category, ''), COALESCE(a.description, ''), COALESCE(a.root_type_name, ''), COALESCE(a.identity_type, ''), COALESCE(a.primary_geometry_type, ''), a.root_tgi",
                distinctPackages.Length);
            BindBatchPackageScope(syncAssetsCommand.Parameters, distinctPackages);
            syncAssetsCommand.Command.Transaction = transaction;
            await syncAssetsCommand.Command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (!finalized && mode == SqliteWriteSessionMode.LiveReplace && ftsSyncPending)
            {
                try
                {
                    await EnsureFtsSchemaAsync(connection, CancellationToken.None);
                    await SyncFtsTablesAsync(connection, CancellationToken.None);
                    ftsSyncPending = false;
                }
                catch
                {
                }
            }

            if (!finalized && mode == SqliteWriteSessionMode.LiveReplace && secondaryIndexesPending)
            {
                try
                {
                    await RebuildSecondaryIndexesAsync(CancellationToken.None);
                }
                catch
                {
                }
            }

            await DisposeCommandsAsync();
            if (!finalized && mode == SqliteWriteSessionMode.ShadowRebuild && shadowDatabasePath is not null)
            {
                TryDeleteDatabaseArtifacts(shadowDatabasePath);
            }
        }

        private async Task DisposeCommandsAsync()
        {
            if (commandsDisposed)
            {
                return;
            }

            await preparedResourceInsertCommand.DisposeAsync();
            await preparedAssetInsertCommand.DisposeAsync();
            await preparedAssetVariantInsertCommand.DisposeAsync();
            await preparedSimTemplateFactInsertCommand.DisposeAsync();
            await preparedSimTemplateBodyPartInsertCommand.DisposeAsync();
            await preparedCasPartFactInsertCommand.DisposeAsync();

            foreach (var prepared in preparedDeleteResourcesFtsCommands.Values)
            {
                await prepared.DisposeAsync();
            }

            foreach (var prepared in preparedDeleteAssetsFtsCommands.Values)
            {
                await prepared.DisposeAsync();
            }

            foreach (var prepared in preparedSyncResourcesFtsCommands.Values)
            {
                await prepared.DisposeAsync();
            }

            foreach (var prepared in preparedSyncAssetsFtsCommands.Values)
            {
                await prepared.DisposeAsync();
            }

            await upsertPackageCommand.DisposeAsync();
            await deletePackageResourcesCommand.DisposeAsync();
            await deletePackageAssetsCommand.DisposeAsync();
            await deletePackageAssetVariantsCommand.DisposeAsync();
            await deletePackageSimTemplateFactsCommand.DisposeAsync();
            await deletePackageSimTemplateBodyPartsCommand.DisposeAsync();
            await deletePackageCasPartFactsCommand.DisposeAsync();
            await connection.DisposeAsync();
            commandsDisposed = true;
        }

        private async Task RebuildSecondaryIndexesAsync(CancellationToken cancellationToken)
        {
            if (!secondaryIndexesPending)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            await using var command = connection.CreateCommand();
            command.CommandText = SecondaryIndexesSql;
            await command.ExecuteNonQueryAsync(cancellationToken);
            stopwatch.Stop();
            lifetimeMetrics.RebuildIndexesElapsed += stopwatch.Elapsed;
            secondaryIndexesPending = false;
        }

        private void PrepareCommands()
        {
            deletePackageResourcesCommand.Prepare();
            deletePackageAssetsCommand.Prepare();
            deletePackageAssetVariantsCommand.Prepare();
            deletePackageSimTemplateFactsCommand.Prepare();
            deletePackageSimTemplateBodyPartsCommand.Prepare();
            deletePackageCasPartFactsCommand.Prepare();
            upsertPackageCommand.Prepare();
        }

        private static string BuildInsertSql(string tableName, IReadOnlyList<string> columns, IReadOnlyList<string> parameterBases)
        {
            var builder = new StringBuilder();
            builder.Append("INSERT INTO ")
                .Append(tableName)
                .Append('(')
                .Append(string.Join(", ", columns))
                .AppendLine(")")
                .Append("VALUES (");
            for (var parameterIndex = 0; parameterIndex < parameterBases.Count; parameterIndex++)
            {
                if (parameterIndex > 0)
                {
                    builder.Append(", ");
                }

                builder.Append('$')
                    .Append(parameterBases[parameterIndex]);
            }

            builder.Append(");");
            return builder.ToString();
        }

        private PreparedInsertCommand CreatePreparedInsertCommand(
            string tableName,
            IReadOnlyList<string> columns,
            IReadOnlyList<string> parameterBases)
        {
            var command = connection.CreateCommand();
            command.CommandText = BuildInsertSql(tableName, columns, parameterBases);
            var parameters = new SqliteParameter[parameterBases.Count];
            for (var index = 0; index < parameterBases.Count; index++)
            {
                var parameter = new SqliteParameter($"${parameterBases[index]}", DBNull.Value);
                command.Parameters.Add(parameter);
                parameters[index] = parameter;
            }

            command.Prepare();
            return new PreparedInsertCommand(command, parameters);
        }

        private PreparedBatchPackageScopeCommand GetOrCreatePreparedBatchFtsDeleteCommand(
            IDictionary<int, PreparedBatchPackageScopeCommand> cache,
            string tableName,
            int packageCount)
        {
            if (cache.TryGetValue(packageCount, out var existing))
            {
                return existing;
            }

            var command = connection.CreateCommand();
            var batchScope = BuildBatchPackageScope(packageCount, command, out var parameters);
            command.CommandText =
                $"""
                WITH batch(data_source_id, package_path) AS (
                    {batchScope}
                )
                DELETE FROM {tableName}
                WHERE EXISTS (
                    SELECT 1
                    FROM batch b
                    WHERE b.data_source_id = {tableName}.data_source_id
                      AND b.package_path = {tableName}.package_path
                );
                """;
            command.Prepare();
            var prepared = new PreparedBatchPackageScopeCommand(command, parameters);
            cache[packageCount] = prepared;
            return prepared;
        }

        private PreparedBatchPackageScopeCommand GetOrCreatePreparedBatchFtsInsertCommand(
            IDictionary<int, PreparedBatchPackageScopeCommand> cache,
            string tableName,
            string sourceTable,
            string sourceAlias,
            string selectList,
            int packageCount)
        {
            if (cache.TryGetValue(packageCount, out var existing))
            {
                return existing;
            }

            var command = connection.CreateCommand();
            var batchScope = BuildBatchPackageScope(packageCount, command, out var parameters);
            command.CommandText =
                $"""
                WITH batch(data_source_id, package_path) AS (
                    {batchScope}
                )
                INSERT INTO {tableName}
                SELECT {selectList}
                FROM {sourceTable} {sourceAlias}
                JOIN batch b ON b.data_source_id = {sourceAlias}.data_source_id
                           AND b.package_path = {sourceAlias}.package_path;
                """;
            command.Prepare();
            var prepared = new PreparedBatchPackageScopeCommand(command, parameters);
            cache[packageCount] = prepared;
            return prepared;
        }

        private static void BindBatchPackageScope(SqliteParameter[] parameters, IReadOnlyList<FtsPackageKey> packageKeys)
        {
            for (var index = 0; index < packageKeys.Count; index++)
            {
                var offset = index * 2;
                SetParameterValue(parameters[offset], packageKeys[index].DataSourceId.ToString("D"));
                SetParameterValue(parameters[offset + 1], packageKeys[index].PackagePath);
            }
        }

        private static void SetResourceParameterValues(SqliteParameter[] parameters, ResourceMetadata resource, string scanToken)
        {
            SetParameterValue(parameters[0], resource.Id.ToString("D"));
            SetParameterValue(parameters[1], resource.DataSourceId.ToString("D"));
            SetParameterValue(parameters[2], resource.SourceKind.ToString());
            SetParameterValue(parameters[3], resource.PackagePath);
            SetParameterValue(parameters[4], scanToken);
            SetParameterValue(parameters[5], $"{resource.Key.Type:X8}");
            SetParameterValue(parameters[6], resource.Key.TypeName);
            SetParameterValue(parameters[7], $"{resource.Key.Group:X8}");
            SetParameterValue(parameters[8], $"{resource.Key.FullInstance:X16}");
            SetParameterValue(parameters[9], resource.Key.FullTgi);
            SetParameterValue(parameters[10], resource.Name);
            SetParameterValue(parameters[11], resource.Description);
            SetParameterValue(parameters[12], resource.CatalogSignal0020);
            SetParameterValue(parameters[13], resource.CatalogSignal002C);
            SetParameterValue(parameters[14], resource.CatalogSignal0030);
            SetParameterValue(parameters[15], resource.CatalogSignal0034);
            SetParameterValue(parameters[16], resource.CompressedSize);
            SetParameterValue(parameters[17], resource.UncompressedSize);
            SetParameterValue(parameters[18], resource.IsCompressed.HasValue ? (resource.IsCompressed.Value ? 1 : 0) : null);
            SetParameterValue(parameters[19], resource.PreviewKind.ToString());
            SetParameterValue(parameters[20], resource.IsPreviewable ? 1 : 0);
            SetParameterValue(parameters[21], resource.IsExportCapable ? 1 : 0);
            SetParameterValue(parameters[22], resource.AssetLinkageSummary);
            SetParameterValue(parameters[23], resource.Diagnostics);
            SetParameterValue(parameters[24], resource.SceneRootTgiHint);
        }

        private static void SetAssetParameterValues(SqliteParameter[] parameters, AssetSummary asset, string scanToken)
        {
            SetParameterValue(parameters[0], asset.Id.ToString("D"));
            SetParameterValue(parameters[1], asset.DataSourceId.ToString("D"));
            SetParameterValue(parameters[2], asset.SourceKind.ToString());
            SetParameterValue(parameters[3], asset.AssetKind.ToString());
            SetParameterValue(parameters[4], asset.DisplayName);
            SetParameterValue(parameters[5], asset.Category);
            SetParameterValue(parameters[6], asset.Description);
            SetParameterValue(parameters[7], asset.CatalogSignal0020);
            SetParameterValue(parameters[8], asset.CatalogSignal002C);
            SetParameterValue(parameters[9], asset.CatalogSignal0030);
            SetParameterValue(parameters[10], asset.CatalogSignal0034);
            SetParameterValue(parameters[11], asset.CategoryNormalized);
            SetParameterValue(parameters[12], asset.PackagePath);
            SetParameterValue(parameters[13], scanToken);
            SetParameterValue(parameters[14], 1);
            SetParameterValue(parameters[15], asset.PackageName);
            SetParameterValue(parameters[16], asset.RootKey.FullTgi);
            SetParameterValue(parameters[17], asset.LogicalRootTgi);
            SetParameterValue(parameters[18], asset.RootTypeName);
            SetParameterValue(parameters[19], asset.ThumbnailTgi);
            SetParameterValue(parameters[20], asset.ThumbnailTypeName);
            SetParameterValue(parameters[21], asset.PrimaryGeometryType);
            SetParameterValue(parameters[22], asset.IdentityType);
            SetParameterValue(parameters[23], asset.VariantCount);
            SetParameterValue(parameters[24], asset.LinkedResourceCount);
            SetParameterValue(parameters[25], asset.CapabilitySnapshot.HasSceneRoot ? 1 : 0);
            SetParameterValue(parameters[26], asset.CapabilitySnapshot.HasExactGeometryCandidate ? 1 : 0);
            SetParameterValue(parameters[27], asset.CapabilitySnapshot.HasMaterialReferences ? 1 : 0);
            SetParameterValue(parameters[28], asset.CapabilitySnapshot.HasTextureReferences ? 1 : 0);
            SetParameterValue(parameters[29], asset.CapabilitySnapshot.HasIdentityMetadata ? 1 : 0);
            SetParameterValue(parameters[30], asset.CapabilitySnapshot.HasRigReference ? 1 : 0);
            SetParameterValue(parameters[31], asset.CapabilitySnapshot.HasGeometryReference ? 1 : 0);
            SetParameterValue(parameters[32], asset.CapabilitySnapshot.HasMaterialResourceCandidate ? 1 : 0);
            SetParameterValue(parameters[33], asset.CapabilitySnapshot.HasTextureResourceCandidate ? 1 : 0);
            SetParameterValue(parameters[34], asset.CapabilitySnapshot.IsPackageLocalGraph ? 1 : 0);
            SetParameterValue(parameters[35], asset.CapabilitySnapshot.HasDiagnostics ? 1 : 0);
            SetParameterValue(parameters[36], asset.Diagnostics);
        }

        private static void SetAssetVariantParameterValues(SqliteParameter[] parameters, AssetVariantSummary variant, string scanToken)
        {
            SetParameterValue(parameters[0], variant.AssetId.ToString("D"));
            SetParameterValue(parameters[1], variant.DataSourceId.ToString("D"));
            SetParameterValue(parameters[2], variant.SourceKind.ToString());
            SetParameterValue(parameters[3], variant.AssetKind.ToString());
            SetParameterValue(parameters[4], variant.PackagePath);
            SetParameterValue(parameters[5], scanToken);
            SetParameterValue(parameters[6], variant.RootTgi);
            SetParameterValue(parameters[7], variant.VariantIndex);
            SetParameterValue(parameters[8], variant.VariantKind);
            SetParameterValue(parameters[9], variant.DisplayLabel);
            SetParameterValue(parameters[10], variant.SwatchHex);
            SetParameterValue(parameters[11], variant.ThumbnailTgi);
            SetParameterValue(parameters[12], variant.Diagnostics);
        }

        private static void SetSimTemplateFactParameterValues(SqliteParameter[] parameters, SimTemplateFactSummary fact)
        {
            SetParameterValue(parameters[0], fact.ResourceId.ToString("D"));
            SetParameterValue(parameters[1], fact.DataSourceId.ToString("D"));
            SetParameterValue(parameters[2], fact.SourceKind.ToString());
            SetParameterValue(parameters[3], fact.PackagePath);
            SetParameterValue(parameters[4], fact.RootTgi);
            SetParameterValue(parameters[5], fact.ArchetypeKey);
            SetParameterValue(parameters[6], fact.SpeciesLabel);
            SetParameterValue(parameters[7], fact.AgeLabel);
            SetParameterValue(parameters[8], fact.GenderLabel);
            SetParameterValue(parameters[9], fact.DisplayName);
            SetParameterValue(parameters[10], fact.Notes);
            SetParameterValue(parameters[11], fact.OutfitCategoryCount);
            SetParameterValue(parameters[12], fact.OutfitEntryCount);
            SetParameterValue(parameters[13], fact.OutfitPartCount);
            SetParameterValue(parameters[14], fact.BodyModifierCount);
            SetParameterValue(parameters[15], fact.FaceModifierCount);
            SetParameterValue(parameters[16], fact.SculptCount);
            SetParameterValue(parameters[17], fact.HasSkintone ? 1 : 0);
            SetParameterValue(parameters[18], fact.AuthoritativeBodyDrivingOutfitCount);
            SetParameterValue(parameters[19], fact.AuthoritativeBodyDrivingOutfitPartCount);
        }

        private static void SetSimTemplateBodyPartParameterValues(SqliteParameter[] parameters, SimTemplateBodyPartFact bodyPart)
        {
            SetParameterValue(parameters[0], bodyPart.ResourceId.ToString("D"));
            SetParameterValue(parameters[1], bodyPart.DataSourceId.ToString("D"));
            SetParameterValue(parameters[2], bodyPart.SourceKind.ToString());
            SetParameterValue(parameters[3], bodyPart.PackagePath);
            SetParameterValue(parameters[4], bodyPart.RootTgi);
            SetParameterValue(parameters[5], bodyPart.OutfitCategoryValue);
            SetParameterValue(parameters[6], bodyPart.OutfitCategoryLabel);
            SetParameterValue(parameters[7], bodyPart.OutfitIndex);
            SetParameterValue(parameters[8], bodyPart.PartIndex);
            SetParameterValue(parameters[9], bodyPart.BodyType);
            SetParameterValue(parameters[10], bodyPart.BodyTypeLabel);
            SetParameterValue(parameters[11], bodyPart.PartInstance.ToString("X16"));
            SetParameterValue(parameters[12], bodyPart.IsBodyDriving ? 1 : 0);
        }

        private static void SetCasPartFactParameterValues(SqliteParameter[] parameters, IndexedCasPartFact fact)
        {
            SetParameterValue(parameters[0], fact.AssetId.ToString("D"));
            SetParameterValue(parameters[1], fact.DataSourceId.ToString("D"));
            SetParameterValue(parameters[2], fact.SourceKind.ToString());
            SetParameterValue(parameters[3], fact.PackagePath);
            SetParameterValue(parameters[4], fact.RootTgi);
            SetParameterValue(parameters[5], fact.SlotCategory);
            SetParameterValue(parameters[6], fact.CategoryNormalized);
            SetParameterValue(parameters[7], fact.BodyType);
            SetParameterValue(parameters[8], fact.InternalName);
            SetParameterValue(parameters[9], fact.DefaultForBodyType ? 1 : 0);
            SetParameterValue(parameters[10], fact.DefaultForBodyTypeFemale ? 1 : 0);
            SetParameterValue(parameters[11], fact.DefaultForBodyTypeMale ? 1 : 0);
            SetParameterValue(parameters[12], fact.HasNakedLink ? 1 : 0);
            SetParameterValue(parameters[13], fact.RestrictOppositeGender ? 1 : 0);
            SetParameterValue(parameters[14], fact.RestrictOppositeFrame ? 1 : 0);
            SetParameterValue(parameters[15], fact.SortLayer);
            SetParameterValue(parameters[16], fact.SpeciesLabel);
            SetParameterValue(parameters[17], fact.AgeLabel);
            SetParameterValue(parameters[18], fact.GenderLabel);
        }

        private static void SetParameterValue(SqliteParameter parameter, object? value) =>
            parameter.Value = value ?? DBNull.Value;

        private static string BuildBatchPackageScope(int packageCount, SqliteCommand command, out SqliteParameter[] parameters)
        {
            var builder = new StringBuilder();
            parameters = new SqliteParameter[packageCount * 2];
            var parameterIndex = 0;
            for (var index = 0; index < packageCount; index++)
            {
                if (index > 0)
                {
                    builder.AppendLine(",");
                    builder.Append("    ");
                }
                else
                {
                    builder.Append("VALUES ");
                }

                var dataSourceParameter = $"$ftsDataSourceId{index}";
                var packageParameter = $"$ftsPackagePath{index}";
                builder.Append('(')
                    .Append(dataSourceParameter)
                    .Append(", ")
                    .Append(packageParameter)
                    .Append(')');

                var dataSourceSqliteParameter = new SqliteParameter(dataSourceParameter, SqliteType.Text);
                var packageSqliteParameter = new SqliteParameter(packageParameter, SqliteType.Text);
                command.Parameters.Add(dataSourceSqliteParameter);
                command.Parameters.Add(packageSqliteParameter);
                parameters[parameterIndex++] = dataSourceSqliteParameter;
                parameters[parameterIndex++] = packageSqliteParameter;
            }

            return builder.ToString();
        }

        private static SqliteCommand CreateDeletePackageResourcesCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM resources WHERE data_source_id = $dataSourceId AND package_path = $packagePath;";
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        internal static SqliteCommand CreateDeletePackageAssetsCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM assets WHERE data_source_id = $dataSourceId AND package_path = $packagePath;";
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        internal static SqliteCommand CreateDeletePackageAssetVariantsCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM asset_variants WHERE data_source_id = $dataSourceId AND package_path = $packagePath;";
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        internal static SqliteCommand CreateDeletePackageSimTemplateFactsCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM sim_template_facts WHERE data_source_id = $dataSourceId AND package_path = $packagePath;";
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        internal static SqliteCommand CreateDeletePackageSimTemplateBodyPartsCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM sim_template_body_parts WHERE data_source_id = $dataSourceId AND package_path = $packagePath;";
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        internal static SqliteCommand CreateDeletePackageCasPartFactsCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM cas_part_facts WHERE data_source_id = $dataSourceId AND package_path = $packagePath;";
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        private static SqliteCommand CreateDeleteResourcesFtsCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM resources_fts WHERE data_source_id = $dataSourceId AND package_path = $packagePath;";
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        internal static SqliteCommand CreateDeleteAssetsFtsCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM assets_fts WHERE data_source_id = $dataSourceId AND package_path = $packagePath;";
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        private static SqliteCommand CreateUpsertPackageCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO packages(data_source_id, package_path, file_size, last_write_utc, indexed_utc)
                VALUES ($dataSourceId, $packagePath, $fileSize, $lastWriteUtc, $indexedUtc)
                ON CONFLICT(data_source_id, package_path) DO UPDATE SET
                    file_size = excluded.file_size,
                    last_write_utc = excluded.last_write_utc,
                    indexed_utc = excluded.indexed_utc;
                """;
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            command.Parameters.Add("$fileSize", SqliteType.Integer);
            command.Parameters.Add("$lastWriteUtc", SqliteType.Text);
            command.Parameters.Add("$indexedUtc", SqliteType.Text);
            return command;
        }

        private static SqliteCommand CreateInsertResourceCommand(SqliteConnection connection, bool insertOnly = false)
        {
            var command = connection.CreateCommand();
            command.CommandText = insertOnly
                ? """
                  INSERT INTO resources(
                      id, data_source_id, source_kind, package_path, scan_token, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                      catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                      compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint)
                  VALUES(
                      $id, $dataSourceId, $sourceKind, $packagePath, $scanToken, $typeHex, $typeName, $groupHex, $instanceHex, $fullTgi, $name, $description,
                      $catalogSignal0020, $catalogSignal002C, $catalogSignal0030, $catalogSignal0034,
                      $compressedSize, $uncompressedSize, $isCompressed, $previewKind, $isPreviewable, $isExportCapable, $assetLinkageSummary, $diagnostics, $sceneRootTgiHint);
                  """
                : """
                  INSERT INTO resources(
                      id, data_source_id, source_kind, package_path, scan_token, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                      catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                      compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics, scene_root_tgi_hint)
                  VALUES(
                      $id, $dataSourceId, $sourceKind, $packagePath, $scanToken, $typeHex, $typeName, $groupHex, $instanceHex, $fullTgi, $name, $description,
                      $catalogSignal0020, $catalogSignal002C, $catalogSignal0030, $catalogSignal0034,
                      $compressedSize, $uncompressedSize, $isCompressed, $previewKind, $isPreviewable, $isExportCapable, $assetLinkageSummary, $diagnostics, $sceneRootTgiHint)
                  ON CONFLICT(id) DO UPDATE SET
                      data_source_id = excluded.data_source_id,
                      source_kind = excluded.source_kind,
                      package_path = excluded.package_path,
                      scan_token = excluded.scan_token,
                      type_hex = excluded.type_hex,
                      type_name = excluded.type_name,
                      group_hex = excluded.group_hex,
                      instance_hex = excluded.instance_hex,
                      full_tgi = excluded.full_tgi,
                      name = excluded.name,
                      description = excluded.description,
                      catalog_signal_0020 = excluded.catalog_signal_0020,
                      catalog_signal_002c = excluded.catalog_signal_002c,
                      catalog_signal_0030 = excluded.catalog_signal_0030,
                      catalog_signal_0034 = excluded.catalog_signal_0034,
                      compressed_size = excluded.compressed_size,
                      uncompressed_size = excluded.uncompressed_size,
                      is_compressed = excluded.is_compressed,
                      preview_kind = excluded.preview_kind,
                      is_previewable = excluded.is_previewable,
                      is_export_capable = excluded.is_export_capable,
                      asset_linkage_summary = excluded.asset_linkage_summary,
                      diagnostics = excluded.diagnostics,
                      scene_root_tgi_hint = excluded.scene_root_tgi_hint;
                  """;
            foreach (var parameterName in new[] { "$id", "$dataSourceId", "$sourceKind", "$packagePath", "$scanToken", "$typeHex", "$typeName", "$groupHex", "$instanceHex", "$fullTgi", "$name", "$description", "$catalogSignal0020", "$catalogSignal002C", "$catalogSignal0030", "$catalogSignal0034", "$compressedSize", "$uncompressedSize", "$isCompressed", "$previewKind", "$isPreviewable", "$isExportCapable", "$assetLinkageSummary", "$diagnostics", "$sceneRootTgiHint" })
            {
                command.Parameters.Add(new SqliteParameter(parameterName, DBNull.Value));
            }

            return command;
        }

        private static SqliteCommand CreateSyncResourcesFtsForPackageCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO resources_fts(id, data_source_id, package_path, type_name, full_tgi, name, description)
                SELECT id, data_source_id, package_path, type_name, full_tgi, COALESCE(name, ''), COALESCE(description, '')
                FROM resources
                WHERE data_source_id = $dataSourceId AND package_path = $packagePath;
                """;
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        internal static SqliteCommand CreateInsertAssetCommand(SqliteConnection connection, bool insertOnly = false)
        {
            var command = connection.CreateCommand();
            command.CommandText = insertOnly
                ? """
                  INSERT INTO assets(
                      id, data_source_id, source_kind, asset_kind, display_name, category, description, catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034, category_normalized, package_path, scan_token, is_canonical, package_name, root_tgi, logical_root_tgi, root_type_name, thumbnail_tgi, thumbnail_type_name,
                      primary_geometry_type, identity_type, variant_count, linked_resource_count, has_scene_root, has_exact_geometry_candidate, has_material_references, has_texture_references,
                      has_identity_metadata, has_rig_reference, has_geometry_reference, has_material_resource_candidate, has_texture_resource_candidate, is_package_local_graph, has_diagnostics, diagnostics)
                  VALUES(
                      $id, $dataSourceId, $sourceKind, $assetKind, $displayName, $category, $description, $catalogSignal0020, $catalogSignal002C, $catalogSignal0030, $catalogSignal0034, $categoryNormalized, $packagePath, $scanToken, $isCanonical, $packageName, $rootTgi, $logicalRootTgi, $rootTypeName, $thumbnailTgi, $thumbnailTypeName,
                      $primaryGeometryType, $identityType, $variantCount, $linkedResourceCount, $hasSceneRoot, $hasExactGeometryCandidate, $hasMaterialReferences, $hasTextureReferences,
                      $hasIdentityMetadata, $hasRigReference, $hasGeometryReference, $hasMaterialResourceCandidate, $hasTextureResourceCandidate, $isPackageLocalGraph, $hasDiagnostics, $diagnostics);
                  """
                : """
                  INSERT INTO assets(
                      id, data_source_id, source_kind, asset_kind, display_name, category, description, catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034, category_normalized, package_path, scan_token, is_canonical, package_name, root_tgi, logical_root_tgi, root_type_name, thumbnail_tgi, thumbnail_type_name,
                      primary_geometry_type, identity_type, variant_count, linked_resource_count, has_scene_root, has_exact_geometry_candidate, has_material_references, has_texture_references,
                      has_identity_metadata, has_rig_reference, has_geometry_reference, has_material_resource_candidate, has_texture_resource_candidate, is_package_local_graph, has_diagnostics, diagnostics)
                  VALUES(
                      $id, $dataSourceId, $sourceKind, $assetKind, $displayName, $category, $description, $catalogSignal0020, $catalogSignal002C, $catalogSignal0030, $catalogSignal0034, $categoryNormalized, $packagePath, $scanToken, $isCanonical, $packageName, $rootTgi, $logicalRootTgi, $rootTypeName, $thumbnailTgi, $thumbnailTypeName,
                      $primaryGeometryType, $identityType, $variantCount, $linkedResourceCount, $hasSceneRoot, $hasExactGeometryCandidate, $hasMaterialReferences, $hasTextureReferences,
                      $hasIdentityMetadata, $hasRigReference, $hasGeometryReference, $hasMaterialResourceCandidate, $hasTextureResourceCandidate, $isPackageLocalGraph, $hasDiagnostics, $diagnostics)
                  ON CONFLICT(id) DO UPDATE SET
                      data_source_id = excluded.data_source_id,
                      source_kind = excluded.source_kind,
                      asset_kind = excluded.asset_kind,
                      display_name = excluded.display_name,
                      category = excluded.category,
                      description = excluded.description,
                      catalog_signal_0020 = excluded.catalog_signal_0020,
                      catalog_signal_002c = excluded.catalog_signal_002c,
                      catalog_signal_0030 = excluded.catalog_signal_0030,
                      catalog_signal_0034 = excluded.catalog_signal_0034,
                      category_normalized = excluded.category_normalized,
                      package_path = excluded.package_path,
                      scan_token = excluded.scan_token,
                      is_canonical = excluded.is_canonical,
                      package_name = excluded.package_name,
                      root_tgi = excluded.root_tgi,
                      logical_root_tgi = excluded.logical_root_tgi,
                      root_type_name = excluded.root_type_name,
                      thumbnail_tgi = excluded.thumbnail_tgi,
                      thumbnail_type_name = excluded.thumbnail_type_name,
                      primary_geometry_type = excluded.primary_geometry_type,
                      identity_type = excluded.identity_type,
                      variant_count = excluded.variant_count,
                      linked_resource_count = excluded.linked_resource_count,
                      has_scene_root = excluded.has_scene_root,
                      has_exact_geometry_candidate = excluded.has_exact_geometry_candidate,
                      has_material_references = excluded.has_material_references,
                      has_texture_references = excluded.has_texture_references,
                      has_identity_metadata = excluded.has_identity_metadata,
                      has_rig_reference = excluded.has_rig_reference,
                      has_geometry_reference = excluded.has_geometry_reference,
                      has_material_resource_candidate = excluded.has_material_resource_candidate,
                      has_texture_resource_candidate = excluded.has_texture_resource_candidate,
                      is_package_local_graph = excluded.is_package_local_graph,
                      has_diagnostics = excluded.has_diagnostics,
                      diagnostics = excluded.diagnostics;
                  """;
            foreach (var parameterName in new[] { "$id", "$dataSourceId", "$sourceKind", "$assetKind", "$displayName", "$category", "$description", "$catalogSignal0020", "$catalogSignal002C", "$catalogSignal0030", "$catalogSignal0034", "$categoryNormalized", "$packagePath", "$scanToken", "$isCanonical", "$packageName", "$rootTgi", "$logicalRootTgi", "$rootTypeName", "$thumbnailTgi", "$thumbnailTypeName", "$primaryGeometryType", "$identityType", "$variantCount", "$linkedResourceCount", "$hasSceneRoot", "$hasExactGeometryCandidate", "$hasMaterialReferences", "$hasTextureReferences", "$hasIdentityMetadata", "$hasRigReference", "$hasGeometryReference", "$hasMaterialResourceCandidate", "$hasTextureResourceCandidate", "$isPackageLocalGraph", "$hasDiagnostics", "$diagnostics" })
            {
                command.Parameters.Add(new SqliteParameter(parameterName, DBNull.Value));
            }

            return command;
        }

        internal static SqliteCommand CreateSyncAssetsFtsForPackageCommand(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO assets_fts(id, data_source_id, package_path, package_name, display_name, category, description, root_type_name, identity_type, primary_geometry_type, root_tgi)
                SELECT id, data_source_id, package_path, COALESCE(package_name, ''), display_name, COALESCE(category, ''), COALESCE(description, ''), COALESCE(root_type_name, ''), COALESCE(identity_type, ''), COALESCE(primary_geometry_type, ''), root_tgi
                FROM assets
                WHERE data_source_id = $dataSourceId AND package_path = $packagePath AND COALESCE(is_canonical, 1) = 1;
                """;
            command.Parameters.Add("$dataSourceId", SqliteType.Text);
            command.Parameters.Add("$packagePath", SqliteType.Text);
            return command;
        }

        internal static void Bind(SqliteCommand command, SqliteTransaction transaction, Guid dataSourceId, string packagePath)
        {
            command.Transaction = transaction;
            command.Parameters["$dataSourceId"].Value = dataSourceId.ToString("D");
            command.Parameters["$packagePath"].Value = packagePath;
        }

        internal static void Bind(SqliteCommand command, SqliteTransaction transaction, Guid dataSourceId, string packagePath, string scanToken)
        {
            command.Transaction = transaction;
            command.Parameters["$dataSourceId"].Value = dataSourceId.ToString("D");
            command.Parameters["$packagePath"].Value = packagePath;
            command.Parameters["$scanToken"].Value = scanToken;
        }

        private static void Bind(SqliteCommand command, SqliteTransaction transaction, PackageScanResult packageScan)
        {
            command.Transaction = transaction;
            command.Parameters["$dataSourceId"].Value = packageScan.DataSourceId.ToString("D");
            command.Parameters["$packagePath"].Value = packageScan.PackagePath;
            command.Parameters["$fileSize"].Value = packageScan.FileSize;
            command.Parameters["$lastWriteUtc"].Value = packageScan.LastWriteTimeUtc.ToString("O");
            command.Parameters["$indexedUtc"].Value = DateTimeOffset.UtcNow.ToString("O");
        }

        private static void Bind(SqliteCommand command, ResourceMetadata resource, string scanToken)
        {
            command.Parameters["$id"].Value = resource.Id.ToString("D");
            command.Parameters["$dataSourceId"].Value = resource.DataSourceId.ToString("D");
            command.Parameters["$sourceKind"].Value = resource.SourceKind.ToString();
            command.Parameters["$packagePath"].Value = resource.PackagePath;
            command.Parameters["$scanToken"].Value = scanToken;
            command.Parameters["$typeHex"].Value = $"{resource.Key.Type:X8}";
            command.Parameters["$typeName"].Value = resource.Key.TypeName;
            command.Parameters["$groupHex"].Value = $"{resource.Key.Group:X8}";
            command.Parameters["$instanceHex"].Value = $"{resource.Key.FullInstance:X16}";
            command.Parameters["$fullTgi"].Value = resource.Key.FullTgi;
            command.Parameters["$name"].Value = (object?)resource.Name ?? DBNull.Value;
            command.Parameters["$description"].Value = (object?)resource.Description ?? DBNull.Value;
            command.Parameters["$catalogSignal0020"].Value = (object?)resource.CatalogSignal0020 ?? DBNull.Value;
            command.Parameters["$catalogSignal002C"].Value = (object?)resource.CatalogSignal002C ?? DBNull.Value;
            command.Parameters["$catalogSignal0030"].Value = (object?)resource.CatalogSignal0030 ?? DBNull.Value;
            command.Parameters["$catalogSignal0034"].Value = (object?)resource.CatalogSignal0034 ?? DBNull.Value;
            command.Parameters["$compressedSize"].Value = (object?)resource.CompressedSize ?? DBNull.Value;
            command.Parameters["$uncompressedSize"].Value = (object?)resource.UncompressedSize ?? DBNull.Value;
            command.Parameters["$isCompressed"].Value = resource.IsCompressed.HasValue ? (resource.IsCompressed.Value ? 1 : 0) : DBNull.Value;
            command.Parameters["$previewKind"].Value = resource.PreviewKind.ToString();
            command.Parameters["$isPreviewable"].Value = resource.IsPreviewable ? 1 : 0;
            command.Parameters["$isExportCapable"].Value = resource.IsExportCapable ? 1 : 0;
            command.Parameters["$assetLinkageSummary"].Value = resource.AssetLinkageSummary;
            command.Parameters["$diagnostics"].Value = resource.Diagnostics;
        }

        internal static void Bind(SqliteCommand command, AssetSummary asset, string scanToken)
        {
            command.Parameters["$id"].Value = asset.Id.ToString("D");
            command.Parameters["$dataSourceId"].Value = asset.DataSourceId.ToString("D");
            command.Parameters["$sourceKind"].Value = asset.SourceKind.ToString();
            command.Parameters["$assetKind"].Value = asset.AssetKind.ToString();
            command.Parameters["$displayName"].Value = asset.DisplayName;
            command.Parameters["$category"].Value = (object?)asset.Category ?? DBNull.Value;
            command.Parameters["$description"].Value = (object?)asset.Description ?? DBNull.Value;
            command.Parameters["$catalogSignal0020"].Value = (object?)asset.CatalogSignal0020 ?? DBNull.Value;
            command.Parameters["$catalogSignal002C"].Value = (object?)asset.CatalogSignal002C ?? DBNull.Value;
            command.Parameters["$catalogSignal0030"].Value = (object?)asset.CatalogSignal0030 ?? DBNull.Value;
            command.Parameters["$catalogSignal0034"].Value = (object?)asset.CatalogSignal0034 ?? DBNull.Value;
            command.Parameters["$categoryNormalized"].Value = (object?)asset.CategoryNormalized ?? DBNull.Value;
            command.Parameters["$packagePath"].Value = asset.PackagePath;
            command.Parameters["$scanToken"].Value = scanToken;
            command.Parameters["$isCanonical"].Value = 1;
            command.Parameters["$packageName"].Value = (object?)asset.PackageName ?? DBNull.Value;
            command.Parameters["$rootTgi"].Value = asset.RootKey.FullTgi;
            command.Parameters["$rootTypeName"].Value = (object?)asset.RootTypeName ?? DBNull.Value;
            command.Parameters["$thumbnailTgi"].Value = (object?)asset.ThumbnailTgi ?? DBNull.Value;
            command.Parameters["$thumbnailTypeName"].Value = (object?)asset.ThumbnailTypeName ?? DBNull.Value;
            command.Parameters["$primaryGeometryType"].Value = (object?)asset.PrimaryGeometryType ?? DBNull.Value;
            command.Parameters["$identityType"].Value = (object?)asset.IdentityType ?? DBNull.Value;
            command.Parameters["$variantCount"].Value = asset.VariantCount;
            command.Parameters["$linkedResourceCount"].Value = asset.LinkedResourceCount;
            command.Parameters["$hasSceneRoot"].Value = asset.CapabilitySnapshot.HasSceneRoot ? 1 : 0;
            command.Parameters["$hasExactGeometryCandidate"].Value = asset.CapabilitySnapshot.HasExactGeometryCandidate ? 1 : 0;
            command.Parameters["$hasMaterialReferences"].Value = asset.CapabilitySnapshot.HasMaterialReferences ? 1 : 0;
            command.Parameters["$hasTextureReferences"].Value = asset.CapabilitySnapshot.HasTextureReferences ? 1 : 0;
            command.Parameters["$hasIdentityMetadata"].Value = asset.CapabilitySnapshot.HasIdentityMetadata ? 1 : 0;
            command.Parameters["$hasRigReference"].Value = asset.CapabilitySnapshot.HasRigReference ? 1 : 0;
            command.Parameters["$hasGeometryReference"].Value = asset.CapabilitySnapshot.HasGeometryReference ? 1 : 0;
            command.Parameters["$hasMaterialResourceCandidate"].Value = asset.CapabilitySnapshot.HasMaterialResourceCandidate ? 1 : 0;
            command.Parameters["$hasTextureResourceCandidate"].Value = asset.CapabilitySnapshot.HasTextureResourceCandidate ? 1 : 0;
            command.Parameters["$isPackageLocalGraph"].Value = asset.CapabilitySnapshot.IsPackageLocalGraph ? 1 : 0;
            command.Parameters["$hasDiagnostics"].Value = asset.CapabilitySnapshot.HasDiagnostics ? 1 : 0;
            command.Parameters["$diagnostics"].Value = asset.Diagnostics;
        }
    }

    private sealed class ShardedIndexWriteSession : IIndexWriteSession, IIndexWriteSessionMetricsProvider
    {
        private readonly SqliteIndexWriteSession[] sessions;
        private readonly string[]? shadowDatabasePaths;
        private readonly Func<IReadOnlyList<string>, CancellationToken, Task>? activateShadowSetAsync;
        private IndexWriteMetrics? lastMetrics;
        private bool finalized;
        private bool sessionsDisposed;

        public ShardedIndexWriteSession(
            SqliteIndexWriteSession[] sessions,
            string[]? shadowDatabasePaths,
            Func<IReadOnlyList<string>, CancellationToken, Task>? activateShadowSetAsync)
        {
            this.sessions = sessions;
            this.shadowDatabasePaths = shadowDatabasePaths;
            this.activateShadowSetAsync = activateShadowSetAsync;
        }

        public Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken) =>
            ReplacePackagesAsync([(packageScan, assets)], cancellationToken);

        public async Task ReplacePackagesAsync(IReadOnlyList<(PackageScanResult PackageScan, IReadOnlyList<AssetSummary> Assets)> batch, CancellationToken cancellationToken)
        {
            if (batch.Count == 0)
            {
                return;
            }

            var grouped = new Dictionary<int, List<(PackageScanResult PackageScan, IReadOnlyList<AssetSummary> Assets)>>();
            foreach (var item in batch)
            {
                var shardIndex = GetShardIndex(item.PackageScan.PackagePath) % sessions.Length;
                if (!grouped.TryGetValue(shardIndex, out var shardBatch))
                {
                    shardBatch = [];
                    grouped[shardIndex] = shardBatch;
                }

                shardBatch.Add(item);
            }

            await Task.WhenAll(grouped.Select(pair => sessions[pair.Key].ReplacePackagesAsync(pair.Value, cancellationToken)));
            var metrics = grouped.Keys
                .Select(index => ((IIndexWriteSessionMetricsProvider)sessions[index]).ConsumeLastMetrics())
                .Where(static metrics => metrics is not null)
                .Select(static metrics => metrics!)
                .ToArray();
            lastMetrics = metrics.Length == 0 ? null : AggregateMetrics(metrics);
        }

        public async Task FinalizeAsync(IProgress<IndexWriteStageProgress>? progress, CancellationToken cancellationToken)
        {
            if (finalized)
            {
                return;
            }

            var databasePaths = sessions.Select(static session => session.DatabasePath).ToArray();
            for (var index = 0; index < sessions.Length; index++)
            {
                await sessions[index].ReleaseForExternalFinalizeAsync();
            }
            sessionsDisposed = true;

            var profile = SqliteConnectionProfile.LiveServing;
            await FinalizeCatalogDatabasesAsync(databasePaths, profile, progress, cancellationToken);

            if (shadowDatabasePaths is not null && activateShadowSetAsync is not null)
            {
                progress?.Report(new IndexWriteStageProgress("finalizing", "Activating rebuilt catalog shards."));
                await activateShadowSetAsync(shadowDatabasePaths, cancellationToken);
            }

            finalized = true;
        }

        public IndexWriteMetrics? ConsumeLastMetrics()
        {
            var metrics = lastMetrics;
            lastMetrics = null;
            return metrics;
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeSessionsAsync();

            if (!finalized && shadowDatabasePaths is not null)
            {
                foreach (var shadowDatabasePath in shadowDatabasePaths)
                {
                    TryDeleteDatabaseArtifacts(shadowDatabasePath);
                }
            }
        }

        private static IndexWriteMetrics AggregateMetrics(IReadOnlyList<IndexWriteMetrics> metrics) =>
            new(
                metrics.Aggregate(TimeSpan.Zero, static (sum, metric) => sum + metric.DropIndexesElapsed),
                metrics.Aggregate(TimeSpan.Zero, static (sum, metric) => sum + metric.DeletePackageRowsElapsed),
                metrics.Aggregate(TimeSpan.Zero, static (sum, metric) => sum + metric.InsertResourcesElapsed),
                metrics.Aggregate(TimeSpan.Zero, static (sum, metric) => sum + metric.InsertAssetsElapsed),
                metrics.Aggregate(TimeSpan.Zero, static (sum, metric) => sum + metric.FtsElapsed),
                metrics.Aggregate(TimeSpan.Zero, static (sum, metric) => sum + metric.RebuildIndexesElapsed),
                metrics.Aggregate(TimeSpan.Zero, static (sum, metric) => sum + metric.CommitElapsed),
                metrics.Sum(static metric => metric.ResourceRowCount),
                metrics.Sum(static metric => metric.AssetRowCount),
                metrics.Sum(static metric => metric.PackageCount));

        private async Task DisposeSessionsAsync()
        {
            if (sessionsDisposed)
            {
                return;
            }

            foreach (var session in sessions)
            {
                await session.DisposeAsync();
            }

            sessionsDisposed = true;
        }
    }
}

public sealed class ResourceMetadataEnrichmentService : IResourceMetadataEnrichmentService
{
    private readonly IResourceCatalogService resourceCatalogService;

    public ResourceMetadataEnrichmentService(IResourceCatalogService resourceCatalogService)
    {
        this.resourceCatalogService = resourceCatalogService;
    }

    public async Task<ResourceMetadata> EnrichAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        if (resource.Name is not null &&
            resource.CompressedSize is not null &&
            resource.UncompressedSize is not null &&
            resource.IsCompressed is not null &&
            (!Ts4StructuredResourceMetadataExtractor.RequiresStructuredDescription(resource.Key.TypeName) ||
             !string.IsNullOrWhiteSpace(resource.Description)))
        {
            return resource;
        }

        // Runtime browse/open/export paths may enrich metadata for the current operation,
        // but they must not mutate the persisted serving catalog.
        return await resourceCatalogService.EnrichResourceAsync(resource, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PackageIndexCoordinator
{
    private const int ParallelSeedEnrichmentThreshold = 256;
    private const int MaxSeedEnrichmentPackageHandles = 8;
    private readonly IPackageScanner packageScanner;
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IAssetGraphBuilder assetGraphBuilder;
    private readonly IIndexStore indexStore;
    private readonly IndexingRunOptions options;
    private readonly ConcurrentDictionary<string, Task<IReadOnlyDictionary<uint, string>>> englishStringLookups = new(StringComparer.OrdinalIgnoreCase);
    private PackageByteCache packageByteCache = new(IndexingRunOptions.DefaultPackageByteCacheBudgetBytes);

    public PackageIndexCoordinator(
        IPackageScanner packageScanner,
        IResourceCatalogService resourceCatalogService,
        IAssetGraphBuilder assetGraphBuilder,
        IIndexStore indexStore,
        IndexingRunOptions? options = null)
    {
        this.packageScanner = packageScanner;
        this.resourceCatalogService = resourceCatalogService;
        this.assetGraphBuilder = assetGraphBuilder;
        this.indexStore = indexStore;
        this.options = options ?? IndexingRunOptions.CreateDefault();
    }

    public async Task RunAsync(
        IEnumerable<DataSourceDefinition> sources,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken,
        int? requestedWorkerCount = null,
        long? requestedPackageByteCacheBudgetBytes = null)
    {
        var effectiveOptions = requestedWorkerCount.HasValue
            ? options.WithWorkerCount(requestedWorkerCount.Value)
            : options;
        if (requestedPackageByteCacheBudgetBytes.HasValue)
        {
            effectiveOptions = effectiveOptions.WithPackageByteCacheBudgetBytes(requestedPackageByteCacheBudgetBytes.Value);
        }

        packageByteCache = new PackageByteCache(effectiveOptions.PackageByteCacheBudgetBytes);
        var persistSliceResourceTarget = GetPersistSliceResourceTarget(effectiveOptions.SqliteBatchSize);
        var state = new IndexingRunState(effectiveOptions);
        var configuredSources = sources.ToArray();
        var activeSources = configuredSources.Where(static source => source.IsEnabled).ToArray();
        using var reporterCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var reportLoop = RunReportingLoopAsync(progress, state, effectiveOptions, reporterCts.Token);

        state.SetStage("preparing", $"Preparing clean index rebuild for {activeSources.Length} active source(s).");
        progress?.Report(state.CreateSnapshot("preparing", $"Preparing clean index rebuild for {activeSources.Length} active source(s)."));

        var dataSourceStopwatch = Stopwatch.StartNew();
        await indexStore.UpsertDataSourcesAsync(configuredSources, cancellationToken).ConfigureAwait(false);
        dataSourceStopwatch.Stop();
        state.RecordRunPhase("source sync", dataSourceStopwatch.Elapsed);
        state.SetStage("scope", "Discovering package scope.");
        progress?.Report(state.CreateSnapshot("scope", "Discovering package scope."));

        var discoveredWorkStopwatch = Stopwatch.StartNew();
        var discoveredWorkItems = await DiscoverScopeAsync(activeSources, state, cancellationToken).ConfigureAwait(false);
        discoveredWorkStopwatch.Stop();
        state.RecordRunPhase("scope discovery", discoveredWorkStopwatch.Elapsed);
        progress?.Report(state.CreateSnapshot(
            "scope",
            $"Scope defined: {discoveredWorkItems.Count:N0} package(s), {FormatByteCount(discoveredWorkItems.Sum(static item => item.FileSize))}."));

        try
        {
            progress?.Report(state.CreateSnapshot("preparing", "Opening shadow SQLite rebuild session."));

            var scanChannel = System.Threading.Channels.Channel.CreateUnbounded<PackageWorkItem>(new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = false
            });

            var persistChannel = System.Threading.Channels.Channel.CreateBounded<PersistWorkItem>(new System.Threading.Channels.BoundedChannelOptions(Math.Max(1, effectiveOptions.PackageQueueCapacity))
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            });

            var writerSessionStopwatch = Stopwatch.StartNew();
            await using var writeSession = await indexStore.OpenRebuildSessionAsync(configuredSources, cancellationToken).ConfigureAwait(false);
            writerSessionStopwatch.Stop();
            state.RecordRunPhase("writer session open", writerSessionStopwatch.Elapsed);
            state.SetStage("indexing", $"Starting index build for {discoveredWorkItems.Count:N0} scoped package(s).");
            progress?.Report(state.CreateSnapshot("indexing", $"Starting index build for {discoveredWorkItems.Count:N0} scoped package(s)."));

            var producer = EnqueueDiscoveredWorkAsync(discoveredWorkItems, scanChannel.Writer, state, cancellationToken);
            var workers = Enumerable.Range(0, effectiveOptions.MaxPackageConcurrency)
                .Select(workerId => RunWorkerAsync(workerId + 1, scanChannel.Reader, persistChannel.Writer, state, persistSliceResourceTarget, cancellationToken))
                .ToArray();
            var writer = RunWriterAsync(persistChannel.Reader, writeSession, state, effectiveOptions.SqliteBatchSize, cancellationToken);

            await producer.ConfigureAwait(false);
            await Task.WhenAll(workers).ConfigureAwait(false);
            persistChannel.Writer.TryComplete();
            await writer.ConfigureAwait(false);
            state.SetStage("finalizing", "Finalizing rebuilt SQLite catalog.");
            progress?.Report(state.CreateSnapshot("finalizing", "Finalizing rebuilt SQLite catalog."));
            var finalizationProgress = progress is null
                ? null
                : new Progress<IndexWriteStageProgress>(stageProgress =>
                {
                    state.SetStage(stageProgress.Stage, stageProgress.Message);
                    progress.Report(state.CreateSnapshot(stageProgress.Stage, stageProgress.Message));
                });
            await writeSession.FinalizeAsync(finalizationProgress, cancellationToken).ConfigureAwait(false);
            if (writeSession is IIndexWriteSessionMetricsProvider finalMetricsProvider &&
                finalMetricsProvider.ConsumeLastMetrics() is { } finalWriteMetrics)
            {
                state.RecordWriteMetrics(finalWriteMetrics);
            }

            progress?.Report(state.CreateSnapshot("complete", "Indexing completed.", state.CreateSummary()));
        }
        catch (OperationCanceledException)
        {
            state.RecordActivity("cancel", "Cancellation requested.");
            progress?.Report(state.CreateSnapshot("canceled", "Indexing canceled."));
            throw;
        }
        finally
        {
            reporterCts.Cancel();
            try
            {
                await reportLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task<IReadOnlyList<PackageWorkItem>> DiscoverScopeAsync(
        IReadOnlyList<DataSourceDefinition> activeSources,
        IndexingRunState state,
        CancellationToken cancellationToken)
    {
        var discoveredItems = new List<PackageWorkItem>();
        try
        {
            await foreach (var discoveredPackage in packageScanner.DiscoverPackagesAsync(activeSources, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.MarkDiscovered(discoveredPackage.PackagePath, discoveredPackage.FileSize);
                discoveredItems.Add(new PackageWorkItem(discoveredPackage.Source, discoveredPackage.PackagePath, discoveredPackage.FileSize, discoveredPackage.LastWriteTimeUtc));
            }
        }
        finally
        {
            state.MarkDiscoveryCompleted();
        }

        return discoveredItems;
    }

    private async Task EnqueueDiscoveredWorkAsync(
        IReadOnlyList<PackageWorkItem> discoveredWorkItems,
        System.Threading.Channels.ChannelWriter<PackageWorkItem> writer,
        IndexingRunState state,
        CancellationToken cancellationToken)
    {
        var queueElapsed = TimeSpan.Zero;
        try
        {
            foreach (var workItem in discoveredWorkItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.MarkQueued(workItem.PackagePath, workItem.FileSize);
                var queueStart = Stopwatch.StartNew();
                await writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);
                queueStart.Stop();
                queueElapsed += queueStart.Elapsed;
            }
        }
        finally
        {
            state.RecordRunPhase("queueing", queueElapsed);
            writer.TryComplete();
        }
    }

    private async Task RunWorkerAsync(
        int workerId,
        System.Threading.Channels.ChannelReader<PackageWorkItem> reader,
        System.Threading.Channels.ChannelWriter<PersistWorkItem> persistWriter,
        IndexingRunState state,
        int persistSliceResourceTarget,
        CancellationToken cancellationToken)
    {
        await foreach (var workItem in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            state.WorkerStarted(workerId, workItem.PackagePath);
            try
            {
                state.UpdatePackageProgress(workItem.PackagePath, new PackageScanProgress("opening package", 0, 0, 0, TimeSpan.Zero));
                var progress = new Progress<PackageScanProgress>(value => state.UpdatePackageProgress(workItem.PackagePath, value));
                var scanStopwatch = Stopwatch.StartNew();
                var packageScan = await resourceCatalogService.ScanPackageAsync(workItem.Source, workItem.PackagePath, progress, cancellationToken).ConfigureAwait(false);
                scanStopwatch.Stop();
                state.RecordRunPhase("worker scan package", scanStopwatch.Elapsed);
                state.UpdatePackageProgress(workItem.PackagePath, new PackageScanProgress(
                    "seed metadata enrichment",
                    packageScan.Resources.Count,
                    packageScan.Resources.Count,
                    0,
                    state.GetElapsed(workItem.PackagePath)));
                var seedEnrichmentStopwatch = Stopwatch.StartNew();
                packageScan = await EnrichSeedTechnicalNamesAsync(packageScan, workItem.Source.RootPath, cancellationToken).ConfigureAwait(false);
                seedEnrichmentStopwatch.Stop();
                state.RecordRunPhase("worker seed metadata enrichment", seedEnrichmentStopwatch.Elapsed);
                IReadOnlyList<AssetSummary> assets = [];
                if (ShouldBuildAssetSummaries(packageScan))
                {
                    state.UpdatePackageProgress(workItem.PackagePath, new PackageScanProgress(
                        "building asset summaries",
                        packageScan.Resources.Count,
                        packageScan.Resources.Count,
                        0,
                        state.GetElapsed(workItem.PackagePath)));
                    var assetBuildStopwatch = Stopwatch.StartNew();
                    assets = assetGraphBuilder.BuildAssetSummaries(packageScan);
                    assetBuildStopwatch.Stop();
                    state.RecordRunPhase("worker build asset summaries", assetBuildStopwatch.Elapsed);
                }
                var persistEnqueueStopwatch = Stopwatch.StartNew();
                var persistItems = SlicePersistWorkItems(workItem, packageScan, assets, persistSliceResourceTarget);
                foreach (var persistItem in persistItems)
                {
                    await persistWriter.WriteAsync(persistItem, cancellationToken).ConfigureAwait(false);
                }
                persistEnqueueStopwatch.Stop();
                state.RecordRunPhase("worker persist enqueue wait", persistEnqueueStopwatch.Elapsed);
                state.MarkPersistQueued(workItem.PackagePath, persistItems.Count);

                state.UpdatePackageProgress(workItem.PackagePath, new PackageScanProgress(
                    "queued for DB write",
                    packageScan.Resources.Count,
                    packageScan.Resources.Count,
                    0,
                    state.GetElapsed(workItem.PackagePath),
                    persistItems.Count > 1
                        ? $"Queued {persistItems.Count} SQLite write chunks."
                        : null));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                state.MarkFailure(workItem.PackagePath, ex.Message, workerId);
            }
            finally
            {
                state.WorkerStopped(workerId);
            }
        }
    }

    private async Task<PackageScanResult> EnrichSeedTechnicalNamesAsync(PackageScanResult packageScan, string sourceRootPath, CancellationToken cancellationToken)
    {
        var resources = packageScan.Resources;
        var sameInstanceHasObjectCatalog = resources
            .Where(static resource => resource.Key.TypeName == "ObjectCatalog")
            .Select(static resource => resource.Key.FullInstance)
            .ToHashSet();
        var seedCandidates = resources
            .Select(static (resource, index) => (resource, index))
            .Where(candidate => ShouldSeedEnrichTechnicalName(candidate.resource, sameInstanceHasObjectCatalog))
            .ToArray();
        if (seedCandidates.Length == 0)
        {
            return packageScan;
        }

        if (!ShouldParallelizeSeedEnrichment(seedCandidates.Length))
        {
            List<ResourceMetadata>? enrichedResources = null;
            List<DiscoveredAssetVariant>? discoveredVariants = null;
            List<SimTemplateFactSummary>? discoveredSimTemplateFacts = null;
            List<SimTemplateBodyPartFact>? discoveredSimTemplateBodyParts = null;
            List<DiscoveredCasPartFact>? discoveredCasPartFacts = null;
            await using var package = await TryOpenPackageForSeedEnrichmentAsync(packageScan.PackagePath, cancellationToken).ConfigureAwait(false);

            foreach (var (resource, index) in seedCandidates)
            {
                var enrichment = await EnrichSeedResourceAsync(resource, package, sourceRootPath, cancellationToken).ConfigureAwait(false);
                if (enrichment.Resource == resource &&
                    enrichment.AssetVariants.Count == 0 &&
                    enrichment.SimTemplateFacts.Count == 0 &&
                    enrichment.SimTemplateBodyPartFacts.Count == 0 &&
                    enrichment.CasPartFacts.Count == 0)
                {
                    continue;
                }

                if (enrichment.Resource != resource)
                {
                    enrichedResources ??= [.. resources];
                    enrichedResources[index] = enrichment.Resource;
                }

                if (enrichment.AssetVariants.Count > 0)
                {
                    discoveredVariants ??= [];
                    discoveredVariants.AddRange(enrichment.AssetVariants);
                }

                if (enrichment.SimTemplateFacts.Count > 0)
                {
                    discoveredSimTemplateFacts ??= [];
                    discoveredSimTemplateFacts.AddRange(enrichment.SimTemplateFacts);
                }

                if (enrichment.SimTemplateBodyPartFacts.Count > 0)
                {
                    discoveredSimTemplateBodyParts ??= [];
                    discoveredSimTemplateBodyParts.AddRange(enrichment.SimTemplateBodyPartFacts);
                }

                if (enrichment.CasPartFacts.Count > 0)
                {
                    discoveredCasPartFacts ??= [];
                    discoveredCasPartFacts.AddRange(enrichment.CasPartFacts);
                }
            }

            return enrichedResources is null &&
                   discoveredVariants is null &&
                   discoveredSimTemplateFacts is null &&
                   discoveredSimTemplateBodyParts is null &&
                   discoveredCasPartFacts is null
                ? packageScan
                : packageScan with
                {
                    Resources = enrichedResources ?? resources,
                    AssetVariants = discoveredVariants ?? packageScan.AssetVariants,
                    SimTemplateFacts = discoveredSimTemplateFacts ?? packageScan.SimTemplateFacts,
                    SimTemplateBodyPartFacts = discoveredSimTemplateBodyParts ?? packageScan.SimTemplateBodyPartFacts,
                    CasPartFacts = discoveredCasPartFacts ?? packageScan.CasPartFacts
                };
        }

        var enrichedBuffer = resources.ToArray();
        var discoveredVariantBuffer = new ConcurrentBag<DiscoveredAssetVariant>();
        var discoveredSimTemplateFactBuffer = new ConcurrentBag<SimTemplateFactSummary>();
        var discoveredSimTemplateBodyPartBuffer = new ConcurrentBag<SimTemplateBodyPartFact>();
        var discoveredCasPartFactBuffer = new ConcurrentBag<DiscoveredCasPartFact>();
        var chunkSize = Math.Max(1, (int)Math.Ceiling(seedCandidates.Length / (double)GetSeedEnrichmentParallelism(seedCandidates.Length)));
        var tasks = new List<Task>();
        for (var offset = 0; offset < seedCandidates.Length; offset += chunkSize)
        {
            var start = offset;
            var end = Math.Min(seedCandidates.Length, offset + chunkSize);
            tasks.Add(ProcessSeedEnrichmentChunkAsync(start, end));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);

        async Task ProcessSeedEnrichmentChunkAsync(int start, int end)
        {
            await using var package = await TryOpenPackageForSeedEnrichmentAsync(packageScan.PackagePath, cancellationToken).ConfigureAwait(false);
            for (var index = start; index < end; index++)
            {
                var candidate = seedCandidates[index];
                var enrichment = await EnrichSeedResourceAsync(candidate.resource, package, sourceRootPath, cancellationToken).ConfigureAwait(false);
                enrichedBuffer[candidate.index] = enrichment.Resource;
                foreach (var variant in enrichment.AssetVariants)
                {
                    discoveredVariantBuffer.Add(variant);
                }

                foreach (var fact in enrichment.SimTemplateFacts)
                {
                    discoveredSimTemplateFactBuffer.Add(fact);
                }

                foreach (var bodyPart in enrichment.SimTemplateBodyPartFacts)
                {
                    discoveredSimTemplateBodyPartBuffer.Add(bodyPart);
                }

                foreach (var fact in enrichment.CasPartFacts)
                {
                    discoveredCasPartFactBuffer.Add(fact);
                }
            }
        }

        return packageScan with
        {
            Resources = enrichedBuffer,
            AssetVariants = discoveredVariantBuffer
                .OrderBy(static variant => variant.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static variant => variant.VariantKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static variant => variant.VariantIndex)
                .ToArray(),
            SimTemplateFacts = discoveredSimTemplateFactBuffer
                .OrderBy(static fact => fact.ArchetypeKey, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(static fact => fact.AuthoritativeBodyDrivingOutfitCount)
                .ThenByDescending(static fact => fact.AuthoritativeBodyDrivingOutfitPartCount)
                .ThenBy(static fact => fact.PackagePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static fact => fact.RootTgi, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SimTemplateBodyPartFacts = discoveredSimTemplateBodyPartBuffer
                .OrderBy(static fact => fact.PackagePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static fact => fact.RootTgi, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static fact => fact.OutfitIndex)
                .ThenBy(static fact => fact.PartIndex)
                .ToArray(),
            CasPartFacts = discoveredCasPartFactBuffer
                .OrderBy(static fact => fact.PackagePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static fact => fact.RootTgi, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static fact => fact.BodyType)
                .ThenBy(static fact => fact.InternalName, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
    }

    private async Task<SeedEnrichmentOutcome> EnrichSeedResourceAsync(
        ResourceMetadata resource,
        DataBasePackedFile? package,
        string sourceRootPath,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = package is not null
                ? await ReadPackageBytesAsync(package, resource.Key, cancellationToken).ConfigureAwait(false)
                : await resourceCatalogService.GetResourceBytesAsync(
                    resource.PackagePath,
                    resource.Key,
                    raw: false,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return new SeedEnrichmentOutcome(resource, []);
        }
        catch (IOException)
        {
            return new SeedEnrichmentOutcome(resource, []);
        }
        catch (NotSupportedException)
        {
            return new SeedEnrichmentOutcome(resource, []);
        }

        if (resource.Key.TypeName == "CASPart")
        {
            var casSeedMetadata = Ts4SeedMetadataExtractor.TryExtractCasPartSeedMetadata(resource, bytes);
            if (casSeedMetadata is not null)
            {
                var enrichedResource = resource with
                {
                    Name = string.IsNullOrWhiteSpace(casSeedMetadata.TechnicalName)
                        ? resource.Name
                        : casSeedMetadata.TechnicalName,
                    Description = string.IsNullOrWhiteSpace(casSeedMetadata.Description)
                        ? resource.Description
                        : casSeedMetadata.Description
                };
                return new SeedEnrichmentOutcome(
                    enrichedResource,
                    casSeedMetadata.Variants,
                    [],
                    [],
                    casSeedMetadata.Fact is null ? [] : [casSeedMetadata.Fact]);
            }

            return new SeedEnrichmentOutcome(resource, []);
        }

        if (resource.Key.TypeName == "ObjectDefinition")
        {
            var objectDefinitionSeedMetadata = Ts4SeedMetadataExtractor.TryExtractObjectDefinitionSeedMetadata(bytes);
            if (objectDefinitionSeedMetadata is null)
            {
                return new SeedEnrichmentOutcome(resource, []);
            }

            var enrichedResource = resource with
            {
                Name = string.IsNullOrWhiteSpace(objectDefinitionSeedMetadata.TechnicalName)
                    ? resource.Name
                    : objectDefinitionSeedMetadata.TechnicalName,
                SceneRootTgiHint = string.IsNullOrWhiteSpace(objectDefinitionSeedMetadata.SceneRootTgiHint)
                    ? resource.SceneRootTgiHint
                    : objectDefinitionSeedMetadata.SceneRootTgiHint
            };
            return new SeedEnrichmentOutcome(enrichedResource, []);
        }

        if (resource.Key.TypeName == "SimInfo")
        {
            var simInfoSeedMetadata = Ts4SeedMetadataExtractor.TryExtractSimInfoSeedMetadata(resource, bytes);
            if (simInfoSeedMetadata is null)
            {
                return new SeedEnrichmentOutcome(resource, []);
            }

            var simTemplateSeedMetadata = Ts4SeedMetadataExtractor.TryExtractSimTemplateSeedMetadata(resource, bytes);

            return new SeedEnrichmentOutcome(
                resource with
                {
                    Name = string.IsNullOrWhiteSpace(simInfoSeedMetadata.DisplayName) ? resource.Name : simInfoSeedMetadata.DisplayName,
                    Description = string.IsNullOrWhiteSpace(simInfoSeedMetadata.Description) ? resource.Description : simInfoSeedMetadata.Description
                },
                [],
                simTemplateSeedMetadata is null ? [] : [simTemplateSeedMetadata.Fact],
                simTemplateSeedMetadata?.BodyPartFacts ?? [],
                []);
        }

        if (Ts4StructuredResourceMetadataExtractor.RequiresStructuredDescription(resource.Key.TypeName))
        {
            var structuredMetadata = Ts4StructuredResourceMetadataExtractor.Describe(resource.Key.TypeName, bytes);
            if (!string.IsNullOrWhiteSpace(structuredMetadata.Description) ||
                !string.IsNullOrWhiteSpace(structuredMetadata.SuggestedName))
            {
                return new SeedEnrichmentOutcome(
                    resource with
                    {
                        Name = string.IsNullOrWhiteSpace(structuredMetadata.SuggestedName)
                            ? resource.Name
                            : structuredMetadata.SuggestedName,
                        Description = string.IsNullOrWhiteSpace(structuredMetadata.Description)
                            ? resource.Description
                            : structuredMetadata.Description
                    },
                    []);
            }

            return new SeedEnrichmentOutcome(resource, []);
        }

        var technicalName = Ts4SeedMetadataExtractor.TryExtractTechnicalName(resource, bytes);
        if (!string.IsNullOrWhiteSpace(technicalName))
        {
            return new SeedEnrichmentOutcome(resource with { Name = technicalName }, []);
        }

        if (resource.Key.TypeName != "ObjectCatalog")
        {
            return new SeedEnrichmentOutcome(resource, []);
        }

        var localizedMetadata = await TryResolveObjectCatalogLocalizedMetadataAsync(resource, bytes, sourceRootPath, cancellationToken).ConfigureAwait(false);
        var rawSignals = Ts4SeedMetadataExtractor.TryExtractObjectCatalogSignals(bytes);
        if (string.IsNullOrWhiteSpace(localizedMetadata.DisplayName) &&
            string.IsNullOrWhiteSpace(localizedMetadata.Description) &&
            rawSignals is (null, null, null, null))
        {
            return new SeedEnrichmentOutcome(resource, []);
        }

        return new SeedEnrichmentOutcome(
            resource with
            {
                Name = string.IsNullOrWhiteSpace(localizedMetadata.DisplayName) ? resource.Name : localizedMetadata.DisplayName,
                Description = string.IsNullOrWhiteSpace(localizedMetadata.Description) ? resource.Description : localizedMetadata.Description,
                CatalogSignal0020 = rawSignals.Signal0020 ?? resource.CatalogSignal0020,
                CatalogSignal002C = rawSignals.Signal002C ?? resource.CatalogSignal002C,
                CatalogSignal0030 = rawSignals.Signal0030 ?? resource.CatalogSignal0030,
                CatalogSignal0034 = rawSignals.Signal0034 ?? resource.CatalogSignal0034
            },
            []);
    }

    private async Task<DataBasePackedFile?> TryOpenPackageForSeedEnrichmentAsync(string packagePath, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await packageByteCache.GetBytesAsync(packagePath, cancellationToken).ConfigureAwait(false);
            Stream stream = bytes is null
                ? new FileStream(
                    packagePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 131072,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan)
                : new MemoryStream(bytes, writable: false);
            return await DataBasePackedFile.FromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadPackageBytesAsync(DataBasePackedFile package, ResourceKeyRecord key, CancellationToken cancellationToken)
    {
        var llamaKey = new ResourceKey((ResourceType)key.Type, key.Group, key.FullInstance);
        try
        {
            return (await package.GetAsync(llamaKey, false, cancellationToken).ConfigureAwait(false)).ToArray();
        }
        catch (Exception ex) when (ex.Message.Contains("marked as deleted", StringComparison.OrdinalIgnoreCase))
        {
            return (await package.GetAsync(llamaKey, true, cancellationToken).ConfigureAwait(false)).ToArray();
        }
    }

    private static bool ShouldSeedEnrichTechnicalName(ResourceMetadata resource, ISet<ulong> sameInstanceHasObjectCatalog)
    {
        if (resource.Key.TypeName == "ObjectDefinition")
        {
            return resource.Name is null || string.IsNullOrWhiteSpace(resource.SceneRootTgiHint);
        }

        if (resource.Key.TypeName == "SimInfo")
        {
            return string.IsNullOrWhiteSpace(resource.Name) || string.IsNullOrWhiteSpace(resource.Description);
        }

        if (Ts4StructuredResourceMetadataExtractor.RequiresStructuredDescription(resource.Key.TypeName))
        {
            return string.IsNullOrWhiteSpace(resource.Name) || string.IsNullOrWhiteSpace(resource.Description);
        }

        if (resource.Name is not null)
        {
            return false;
        }

        return resource.Key.TypeName switch
        {
            "ObjectCatalog" => true,
            "CASPart" => true,
            _ => false
        };
    }

    private static bool ShouldParallelizeSeedEnrichment(int seedResourceCount) =>
        seedResourceCount >= ParallelSeedEnrichmentThreshold &&
        GetSeedEnrichmentParallelism(seedResourceCount) > 1;

    private static int GetSeedEnrichmentParallelism(int seedResourceCount)
    {
        var processorBound = Math.Clamp(Environment.ProcessorCount, 1, MaxSeedEnrichmentPackageHandles);
        var workloadBound = Math.Clamp((int)Math.Ceiling(seedResourceCount / (double)ParallelSeedEnrichmentThreshold), 1, MaxSeedEnrichmentPackageHandles);
        return Math.Min(processorBound, workloadBound);
    }

    private async Task<(string? DisplayName, string? Description)> TryResolveObjectCatalogLocalizedMetadataAsync(
        ResourceMetadata resource,
        byte[] bytes,
        string sourceRootPath,
        CancellationToken cancellationToken)
    {
        var nameHash = Ts4SeedMetadataExtractor.TryExtractObjectCatalogNameHash(bytes);
        var descriptionHash = Ts4SeedMetadataExtractor.TryExtractObjectCatalogDescriptionHash(bytes);
        if (!nameHash.HasValue && !descriptionHash.HasValue)
        {
            return (null, null);
        }

        var lookup = await GetEnglishStringLookupAsync(sourceRootPath, cancellationToken).ConfigureAwait(false);
        string? displayName = null;
        if (nameHash.HasValue &&
            lookup.TryGetValue(nameHash.Value, out var nameValue) &&
            !string.IsNullOrWhiteSpace(nameValue))
        {
            displayName = nameValue;
        }

        string? description = null;
        if (descriptionHash.HasValue &&
            lookup.TryGetValue(descriptionHash.Value, out var descriptionValue) &&
            !string.IsNullOrWhiteSpace(descriptionValue))
        {
            description = descriptionValue;
        }

        return (displayName, description);
    }

    private Task<IReadOnlyDictionary<uint, string>> GetEnglishStringLookupAsync(string sourceRootPath, CancellationToken cancellationToken) =>
        englishStringLookups.GetOrAdd(
            sourceRootPath,
            root => LoadEnglishStringLookupAsync(root, cancellationToken));

    private async Task<IReadOnlyDictionary<uint, string>> LoadEnglishStringLookupAsync(string sourceRootPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceRootPath))
        {
            return new Dictionary<uint, string>();
        }

        var packagePaths = Directory
            .EnumerateFiles(sourceRootPath, "Strings_ENG_US.package", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (packagePaths.Length == 0)
        {
            return new Dictionary<uint, string>();
        }

        var values = new Dictionary<uint, string>();
        foreach (var packagePath in packagePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bytes = await packageByteCache.GetBytesAsync(packagePath, cancellationToken).ConfigureAwait(false);
            await using Stream stream = bytes is null
                ? new FileStream(
                    packagePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete,
                    bufferSize: 131072,
                    options: FileOptions.Asynchronous | FileOptions.SequentialScan)
                : new MemoryStream(bytes, writable: false);
            await using var package = await DataBasePackedFile.FromStreamAsync(stream, cancellationToken).ConfigureAwait(false);

            foreach (var key in package.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((uint)key.Type != 0x220557DA)
                {
                    continue;
                }

                try
                {
                    var table = await package.GetStringTableAsync(key, false, cancellationToken).ConfigureAwait(false);
                    foreach (var hash in table.KeyHashes)
                    {
                        if (values.ContainsKey(hash))
                        {
                            continue;
                        }

                        var value = table.Get(hash);
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            values[hash] = value;
                        }
                    }
                }
                catch
                {
                }
            }
        }

        return values;
    }

    private async Task RunWriterAsync(
        System.Threading.Channels.ChannelReader<PersistWorkItem> reader,
        IIndexWriteSession writeSession,
        IndexingRunState state,
        int sqliteBatchSize,
        CancellationToken cancellationToken)
    {
        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var batch = new List<PersistWorkItem>();
            var batchResourceCount = 0;
            while (reader.TryRead(out var item))
            {
                batch.Add(item);
                batchResourceCount += Math.Max(1, item.PackageScan.Resources.Count);
                if (batchResourceCount >= sqliteBatchSize)
                {
                    break;
                }
            }

            if (batch.Count == 0)
            {
                continue;
            }

            try
            {
                state.StartPersistBatch(batch.Count);
                foreach (var item in batch)
                {
                    var resourcesWrittenBeforeSlice = Math.Max(0, item.ResourcesWrittenAfterSlice - item.PackageScan.Resources.Count);
                    state.UpdatePackageProgress(item.WorkItem.PackagePath, new PackageScanProgress(
                        "batching DB writes",
                        item.TotalPackageResourceCount,
                        item.TotalPackageResourceCount,
                        resourcesWrittenBeforeSlice,
                        state.GetElapsed(item.WorkItem.PackagePath),
                        item.SliceCount > 1
                            ? $"Writing SQLite chunk {item.SliceIndex}/{item.SliceCount}."
                            : null));
                }

                var payload = batch
                    .Select(static item => (item.PackageScan, item.Assets))
                    .ToArray();
                await writeSession.ReplacePackagesAsync(payload, cancellationToken).ConfigureAwait(false);
                if (writeSession is IIndexWriteSessionMetricsProvider metricsProvider &&
                    metricsProvider.ConsumeLastMetrics() is { } writeMetrics)
                {
                    state.RecordWriteMetrics(writeMetrics);
                }

                foreach (var item in batch)
                {
                    state.UpdatePackageProgress(item.WorkItem.PackagePath, new PackageScanProgress(
                        item.IsFinalSlice ? "finalizing package" : "writing package chunk",
                        item.TotalPackageResourceCount,
                        item.TotalPackageResourceCount,
                        item.ResourcesWrittenAfterSlice,
                        state.GetElapsed(item.WorkItem.PackagePath),
                        item.IsFinalSlice || item.SliceCount <= 1
                            ? null
                            : $"Persisted {item.ResourcesWrittenAfterSlice:N0} / {item.TotalPackageResourceCount:N0} resource rows."));

                    if (item.IsFinalSlice)
                    {
                        state.MarkCompleted(item.WorkItem.PackagePath, item.TotalPackageResourceCount);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                foreach (var item in batch)
                {
                    state.MarkFailure(item.WorkItem.PackagePath, ex.Message);
                }
            }
            finally
            {
                state.FinishPersistBatch();
            }
        }
    }

    private async Task RunReportingLoopAsync(IProgress<IndexingProgress>? progress, IndexingRunState state, IndexingRunOptions effectiveOptions, CancellationToken cancellationToken)
    {
        if (progress is null)
        {
            return;
        }

        using var timer = new PeriodicTimer(effectiveOptions.ProgressUpdateInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            progress.Report(state.CreateSnapshot(state.GetCurrentStage(), state.DrainLatestMessage()));
        }
    }

    private static int GetPersistSliceResourceTarget(int sqliteBatchSize) =>
        Math.Clamp(sqliteBatchSize / 4, 2048, 8192);

    private static IReadOnlyList<PersistWorkItem> SlicePersistWorkItems(
        PackageWorkItem workItem,
        PackageScanResult packageScan,
        IReadOnlyList<AssetSummary> assets,
        int persistSliceResourceTarget)
    {
        var totalResources = packageScan.Resources.Count;
        var sliceResourceTarget = Math.Max(1, persistSliceResourceTarget);
        if (totalResources == 0)
        {
            return
            [
                new PersistWorkItem(
                    workItem,
                    packageScan,
                    assets,
                    totalResources,
                    0,
                    1,
                    1,
                    true)
            ];
        }

        var sliceCount = Math.Max(1, (int)Math.Ceiling(totalResources / (double)sliceResourceTarget));
        var slices = new List<PersistWorkItem>(sliceCount);
        var resourcesWrittenAfterSlice = 0;
        for (var offset = 0; offset < totalResources; offset += sliceResourceTarget)
        {
            var sliceSize = Math.Min(sliceResourceTarget, totalResources - offset);
            var sliceResources = sliceSize == totalResources
                ? packageScan.Resources
                : packageScan.Resources.Skip(offset).Take(sliceSize).ToArray();
            resourcesWrittenAfterSlice += sliceSize;
            var sliceIndex = slices.Count + 1;
            var isFinalSlice = sliceIndex == sliceCount;
            var slicePackageScan = sliceSize == totalResources
                ? packageScan
                : isFinalSlice
                    ? packageScan with
                    {
                        Resources = sliceResources
                    }
                    : packageScan with
                    {
                        Resources = sliceResources,
                        AssetVariants = [],
                        SimTemplateFacts = [],
                        SimTemplateBodyPartFacts = [],
                        CasPartFacts = []
                    };
            slices.Add(new PersistWorkItem(
                workItem,
                slicePackageScan,
                isFinalSlice ? assets : Array.Empty<AssetSummary>(),
                totalResources,
                resourcesWrittenAfterSlice,
                sliceIndex,
                sliceCount,
                isFinalSlice));
        }

        return slices;
    }

    private static string FormatByteCount(long bytes)
    {
        const double kib = 1024d;
        const double mib = kib * 1024d;
        const double gib = mib * 1024d;

        return bytes switch
        {
            <= 0 => "0 B",
            < 1024L => $"{bytes:N0} B",
            < 1024L * 1024L => $"{bytes / kib:0.0} KiB",
            < 1024L * 1024L * 1024L => $"{bytes / mib:0.0} MiB",
            _ => $"{bytes / gib:0.00} GiB"
        };
    }

    private sealed record PackageWorkItem(DataSourceDefinition Source, string PackagePath, long FileSize, DateTimeOffset LastWriteUtc);

    private sealed record PersistWorkItem(
        PackageWorkItem WorkItem,
        PackageScanResult PackageScan,
        IReadOnlyList<AssetSummary> Assets,
        int TotalPackageResourceCount,
        int ResourcesWrittenAfterSlice,
        int SliceIndex,
        int SliceCount,
        bool IsFinalSlice);

    private sealed record IndexedCasPartFact(
        Guid AssetId,
        Guid DataSourceId,
        SourceKind SourceKind,
        string PackagePath,
        string RootTgi,
        string SlotCategory,
        string? CategoryNormalized,
        int BodyType,
        string? InternalName,
        bool DefaultForBodyType,
        bool DefaultForBodyTypeFemale,
        bool DefaultForBodyTypeMale,
        bool HasNakedLink,
        bool RestrictOppositeGender,
        bool RestrictOppositeFrame,
        int SortLayer,
        string? SpeciesLabel,
        string AgeLabel,
        string GenderLabel);

    private sealed record SeedEnrichmentOutcome(
        ResourceMetadata Resource,
        IReadOnlyList<DiscoveredAssetVariant> AssetVariants,
        IReadOnlyList<SimTemplateFactSummary> SimTemplateFacts,
        IReadOnlyList<SimTemplateBodyPartFact> SimTemplateBodyPartFacts,
        IReadOnlyList<DiscoveredCasPartFact> CasPartFacts)
    {
        public SeedEnrichmentOutcome(ResourceMetadata resource, IReadOnlyList<DiscoveredAssetVariant> assetVariants)
            : this(resource, assetVariants, [], [], [])
        {
        }
    }

    private sealed class IndexingRunState
    {
        private readonly object gate = new();
        private readonly Dictionary<string, PackageState> packages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, WorkerSlotState> workerSlots;
        private readonly List<IndexingActivityEvent> recentEvents = [];
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly IndexingRunOptions options;
        private readonly Dictionary<string, TimeSpan> runPhases = new(StringComparer.OrdinalIgnoreCase);
        private string currentStage = "preparing";
        private string latestMessage = string.Empty;
        private int packagesDiscovered;
        private int packagesQueued;
        private int packagesProcessed;
        private int packagesSkipped;
        private int packagesFailed;
        private int resourcesCompleted;
        private long processedPackageBytes;
        private int persistQueued;
        private int persistActiveBatchCount;
        private TimeSpan writerBusyStartedAt;
        private TimeSpan writerBusyTotal;
        private bool writerBusy;
        private bool discoveryCompleted;

        public IndexingRunState(IndexingRunOptions options)
        {
            this.options = options;
            workerSlots = Enumerable.Range(1, options.MaxPackageConcurrency)
                .ToDictionary(static workerId => workerId, static workerId => new WorkerSlotState(workerId));
        }

        public void MarkDiscovered(string packagePath, long fileSize)
        {
            lock (gate)
            {
                packagesDiscovered++;
                packages.TryAdd(packagePath, new PackageState(packagePath, fileSize));
            }
        }

        public void MarkQueued(string packagePath, long fileSize)
        {
            lock (gate)
            {
                packages[packagePath] = new PackageState(packagePath, fileSize);
                packagesQueued++;
                RecordActivityUnsafe("queue", $"Queued {Path.GetFileName(packagePath)}");
            }
        }

        public void SetStage(string stage, string message)
        {
            lock (gate)
            {
                currentStage = stage;
                latestMessage = message;
            }
        }

        public string GetCurrentStage()
        {
            lock (gate)
            {
                return currentStage;
            }
        }

        public void RecordRunPhase(string phase, TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
            {
                return;
            }

            lock (gate)
            {
                runPhases[phase] = runPhases.TryGetValue(phase, out var current)
                    ? current + elapsed
                    : elapsed;
            }
        }

        public void RecordWriteMetrics(IndexWriteMetrics metrics)
        {
            RecordRunPhase("writer drop secondary indexes", metrics.DropIndexesElapsed);
            RecordRunPhase("writer delete package rows", metrics.DeletePackageRowsElapsed);
            RecordRunPhase("writer insert resources", metrics.InsertResourcesElapsed);
            RecordRunPhase("writer insert assets", metrics.InsertAssetsElapsed);
            RecordRunPhase("writer FTS sync", metrics.FtsElapsed);
            RecordRunPhase("writer rebuild secondary indexes", metrics.RebuildIndexesElapsed);
            RecordRunPhase("writer commit", metrics.CommitElapsed);
        }

        public void MarkDiscoveryCompleted()
        {
            lock (gate)
            {
                discoveryCompleted = true;
                RecordActivityUnsafe("discovery", $"Package discovery completed with {packagesDiscovered:N0} package(s) discovered.");
            }
        }

        public void WorkerStarted(int workerId, string packagePath)
        {
            lock (gate)
            {
                if (!packages.TryGetValue(packagePath, out var state))
                {
                    state = new PackageState(packagePath, 0);
                    packages[packagePath] = state;
                }

                state.Start();
                workerSlots[workerId].Activate(packagePath);
                RecordActivityUnsafe("start", $"Worker {workerId} started {Path.GetFileName(packagePath)}");
            }
        }

        public void WorkerStopped(int workerId)
        {
            lock (gate)
            {
                workerSlots[workerId].Wait();
            }
        }

        public void MarkPersistQueued(string packagePath, int itemCount)
        {
            if (itemCount <= 0)
            {
                return;
            }

            lock (gate)
            {
                persistQueued += itemCount;
                RecordActivityUnsafe(
                    "persist-queued",
                    itemCount == 1
                        ? $"Queued {Path.GetFileName(packagePath)} for SQLite write"
                        : $"Queued {Path.GetFileName(packagePath)} for SQLite write ({itemCount} chunks)");
            }
        }

        public void StartPersistBatch(int batchCount)
        {
            if (batchCount <= 0)
            {
                return;
            }

            lock (gate)
            {
                persistQueued = Math.Max(0, persistQueued - batchCount);
                persistActiveBatchCount = batchCount;
                if (!writerBusy)
                {
                    writerBusy = true;
                    writerBusyStartedAt = stopwatch.Elapsed;
                }
            }
        }

        public void FinishPersistBatch()
        {
            lock (gate)
            {
                persistActiveBatchCount = 0;
                if (!writerBusy)
                {
                    return;
                }

                writerBusyTotal += stopwatch.Elapsed - writerBusyStartedAt;
                writerBusyStartedAt = TimeSpan.Zero;
                writerBusy = false;
            }
        }

        public void UpdatePackageProgress(string packagePath, PackageScanProgress progress)
        {
            lock (gate)
            {
                if (!packages.TryGetValue(packagePath, out var state))
                {
                    state = new PackageState(packagePath, 0);
                    packages[packagePath] = state;
                }

                var stageChanged = state.Update(progress, options.HeartbeatInterval);
                var workerSlot = workerSlots.Values.FirstOrDefault(slot => string.Equals(slot.PackagePath, packagePath, StringComparison.OrdinalIgnoreCase));
                workerSlot?.Update(state);

                if (stageChanged)
                {
                    RecordActivityUnsafe("phase", $"{Path.GetFileName(packagePath)}: {progress.Stage}");
                }
                else if (state.HeartbeatDue)
                {
                    RecordActivityUnsafe("heartbeat", state.BuildHeartbeatMessage());
                    state.MarkHeartbeatLogged();
                }
                else if (!string.IsNullOrWhiteSpace(progress.Message))
                {
                    latestMessage = progress.Message!;
                }
            }
        }

        public TimeSpan GetElapsed(string packagePath)
        {
            lock (gate)
            {
                return packages.TryGetValue(packagePath, out var state) ? state.Elapsed : TimeSpan.Zero;
            }
        }

        public void MarkCompleted(string packagePath, int resourceCount)
        {
            lock (gate)
            {
                if (!packages.TryGetValue(packagePath, out var state))
                {
                    state = new PackageState(packagePath, 0);
                    packages[packagePath] = state;
                }

                state.Complete(resourceCount);
                var workerSlot = workerSlots.Values.FirstOrDefault(slot => string.Equals(slot.PackagePath, packagePath, StringComparison.OrdinalIgnoreCase));
                workerSlot?.Complete(state);
                packagesProcessed++;
                resourcesCompleted += resourceCount;
                processedPackageBytes += state.FileSize;
                RecordActivityUnsafe("complete", $"Indexed {Path.GetFileName(packagePath)} in {state.Elapsed:hh\\:mm\\:ss} ({resourceCount:N0} resources)");
            }
        }

        public void MarkSkipped(string packagePath, long fileSize)
        {
            lock (gate)
            {
                var state = new PackageState(packagePath, fileSize);
                state.Skip();
                packages[packagePath] = state;
                packagesProcessed++;
                packagesSkipped++;
                processedPackageBytes += state.FileSize;
                RecordActivityUnsafe("skip", $"Skipped unchanged package {Path.GetFileName(packagePath)}");
            }
        }

        public void MarkFailure(string packagePath, string reason, int? workerId = null)
        {
            lock (gate)
            {
                if (!packages.TryGetValue(packagePath, out var state))
                {
                    state = new PackageState(packagePath, 0);
                    packages[packagePath] = state;
                }

                if (state.IsTerminal)
                {
                    return;
                }

                state.Fail(reason);
                var workerSlot = workerId.HasValue
                    ? workerSlots[workerId.Value]
                    : workerSlots.Values.FirstOrDefault(slot => string.Equals(slot.PackagePath, packagePath, StringComparison.OrdinalIgnoreCase));
                workerSlot?.Fail(state, reason);
                packagesProcessed++;
                packagesFailed++;
                processedPackageBytes += state.FileSize;
                RecordActivityUnsafe("failed", $"Failed {Path.GetFileName(packagePath)}: {reason}");
            }
        }

        public void RecordActivity(string kind, string message)
        {
            lock (gate)
            {
                RecordActivityUnsafe(kind, message);
            }
        }

        public IndexingProgress CreateSnapshot(string stage, string message, IndexingRunSummary? summary = null)
        {
            lock (gate)
            {
                var activePackages = packages.Values
                    .Where(static package => !package.IsTerminal && package.HasStarted)
                    .OrderByDescending(static package => package.Elapsed)
                    .Select(static package => package.ToSnapshot())
                    .ToArray();

                var resourcesProcessed = resourcesCompleted + activePackages.Sum(static package => package.ResourcesProcessed);
                var packageBytesTotal = packages.Values.Sum(static package => package.FileSize);
                var activePackageBytes = packages.Values
                    .Where(static package => !package.IsTerminal && package.HasStarted)
                    .Sum(static package => package.EstimateProcessedBytes());
                var packageBytesProcessed = processedPackageBytes + activePackageBytes;
                var pendingPackageCount = Math.Max(0, packagesQueued - packagesProcessed - packages.Values.Count(static package => package.HasStarted && !package.IsTerminal));
                var elapsed = stopwatch.Elapsed;
                var overallThroughput = elapsed.TotalSeconds <= 0 ? 0 : resourcesProcessed / elapsed.TotalSeconds;
                var waitingWorkerCount = workerSlots.Values.Count(static slot => slot.Status == WorkerSlotStatus.Waiting);
                var idleWorkerCount = workerSlots.Values.Count(static slot => slot.Status == WorkerSlotStatus.Idle);
                var failedWorkerCount = workerSlots.Values.Count(static slot => slot.Status == WorkerSlotStatus.Failed);
                var writerBusyTime = writerBusy
                    ? writerBusyTotal + (elapsed - writerBusyStartedAt)
                    : writerBusyTotal;
                var writerBusyPercent = elapsed.TotalSeconds <= 0
                    ? 0
                    : Math.Clamp(writerBusyTime.TotalSeconds / elapsed.TotalSeconds * 100d, 0d, 100d);
                var effectiveMessage = string.IsNullOrWhiteSpace(message) ? latestMessage : message;

                return new IndexingProgress(
                    stage,
                    packagesProcessed,
                    packagesDiscovered,
                    packagesProcessed - packagesSkipped - packagesFailed,
                    packagesSkipped,
                    packagesFailed,
                    packageBytesProcessed,
                    packageBytesTotal,
                    processedPackageBytes,
                    resourcesProcessed,
                    resourcesCompleted,
                    effectiveMessage,
                    ActiveWorkerCount: workerSlots.Values.Count(static slot => slot.Status == WorkerSlotStatus.Active),
                    WaitingWorkerCount: waitingWorkerCount,
                    IdleWorkerCount: idleWorkerCount,
                    FailedWorkerCount: failedWorkerCount,
                    PendingPackageCount: pendingPackageCount,
                    PendingPersistCount: persistQueued,
                    ActiveWriterBatchCount: persistActiveBatchCount,
                    WriterBusy: writerBusy,
                    WriterBusyPercent: writerBusyPercent,
                    ConfiguredWorkerCount: options.MaxPackageConcurrency,
                    DiscoveryCompleted: discoveryCompleted,
                    Elapsed: elapsed,
                    OverallThroughput: overallThroughput,
                    WorkerSlots: workerSlots.Values.OrderBy(static slot => slot.WorkerId).Select(static slot => slot.ToSnapshot()).ToArray(),
                    ActivePackages: activePackages,
                    RecentEvents: recentEvents.ToArray(),
                    Summary: summary);
            }
        }

        public string DrainLatestMessage()
        {
            lock (gate)
            {
                var message = latestMessage;
                latestMessage = string.Empty;
                return message;
            }
        }

        public IndexingRunSummary CreateSummary()
        {
            lock (gate)
            {
                var completedPackages = packages.Values.Where(static package => package.IsCompleted || package.IsSkipped || package.IsFailed).ToArray();
                var totalElapsed = stopwatch.Elapsed;
                var averageThroughput = totalElapsed.TotalSeconds <= 0 ? 0 : resourcesCompleted / totalElapsed.TotalSeconds;
                var slowestPackages = completedPackages
                    .OrderByDescending(static package => package.Elapsed)
                    .Take(10)
                    .Select(static package => package.ToRunSummary())
                    .ToArray();
                var failures = completedPackages
                    .Where(static package => package.IsFailed)
                    .Select(static package => new PackageFailureInfo(package.PackagePath, package.FailureReason ?? "Unknown failure"))
                    .ToArray();
                var packagePhaseTotals = completedPackages
                    .SelectMany(static package => package.PhaseTimings)
                    .GroupBy(static phase => phase.Stage, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new KeyValuePair<string, TimeSpan>(group.Key, TimeSpan.FromMilliseconds(group.Sum(static phase => phase.Elapsed.TotalMilliseconds))));
                var phaseBreakdown = packagePhaseTotals
                    .Concat(runPhases)
                    .GroupBy(static phase => phase.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new KeyValuePair<string, TimeSpan>(group.Key, TimeSpan.FromMilliseconds(group.Sum(static phase => phase.Value.TotalMilliseconds))))
                    .OrderByDescending(static phase => phase.Value)
                    .Select(static phase => $"{phase.Key}: {phase.Value:hh\\:mm\\:ss}")
                    .ToArray();
                var averagePackageSeconds = completedPackages.Where(static package => package.IsCompleted).Select(static package => package.Elapsed.TotalSeconds).DefaultIfEmpty(0d).Average();
                var slowOutliers = completedPackages
                    .Where(package => package.IsCompleted && averagePackageSeconds > 0 && package.Elapsed.TotalSeconds >= averagePackageSeconds * 2)
                    .OrderByDescending(static package => package.Elapsed)
                    .Select(package => $"{Path.GetFileName(package.PackagePath)}: {package.Elapsed:hh\\:mm\\:ss}")
                    .ToArray();

                return new IndexingRunSummary(
                    totalElapsed,
                    packagesDiscovered,
                    packagesQueued,
                    packagesProcessed,
                    packagesSkipped,
                    packagesFailed,
                    packages.Values.Sum(static package => package.FileSize),
                    processedPackageBytes,
                    resourcesCompleted,
                    averageThroughput,
                    slowestPackages,
                    failures,
                    phaseBreakdown,
                    slowOutliers);
            }
        }

        private void RecordActivityUnsafe(string kind, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            latestMessage = message;
            recentEvents.Add(new IndexingActivityEvent(DateTimeOffset.Now, kind, message));
            if (recentEvents.Count > options.MaxRecentEvents)
            {
                recentEvents.RemoveAt(0);
            }
        }
    }

    private static string BuildFingerprintKey(Guid dataSourceId, string packagePath) =>
        $"{dataSourceId:D}|{packagePath}";

    private static bool ShouldBuildAssetSummaries(PackageScanResult packageScan) =>
        packageScan.Resources.Any(static resource => resource.Key.TypeName is "ObjectCatalog" or "ObjectDefinition" or "CASPart" or "SimInfo");

    private sealed class PackageState
    {
        private readonly Stopwatch stopwatch = new();
        private readonly List<PackagePhaseTiming> phaseTimings = [];
        private string currentStage = "queued";
        private string? currentMessage;
        private TimeSpan stageStartedAt;
        private TimeSpan lastHeartbeatAt;

        public PackageState(string packagePath, long fileSize)
        {
            PackagePath = packagePath;
            PackageName = Path.GetFileName(packagePath);
            FileSize = Math.Max(0, fileSize);
        }

        public string PackagePath { get; }
        public string PackageName { get; }
        public long FileSize { get; }
        public int ResourcesDiscovered { get; private set; }
        public int ResourcesProcessed { get; private set; }
        public int ResourcesWritten { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool IsSkipped { get; private set; }
        public bool IsFailed { get; private set; }
        public bool IsTerminal => IsCompleted || IsSkipped || IsFailed;
        public bool HasStarted => stopwatch.IsRunning || IsTerminal;
        public string? FailureReason { get; private set; }
        public IReadOnlyList<PackagePhaseTiming> PhaseTimings => phaseTimings;
        public bool HeartbeatDue { get; private set; }
        public TimeSpan Elapsed => stopwatch.Elapsed;
        public string CurrentStage => currentStage;
        public string? CurrentMessage => currentMessage;

        public void Start()
        {
            if (!stopwatch.IsRunning)
            {
                stopwatch.Start();
                stageStartedAt = TimeSpan.Zero;
                lastHeartbeatAt = TimeSpan.Zero;
            }
        }

        public bool Update(PackageScanProgress progress, TimeSpan heartbeatInterval)
        {
            Start();
            var stageChanged = false;

            ResourcesDiscovered = Math.Max(ResourcesDiscovered, progress.ResourcesDiscovered);
            ResourcesProcessed = Math.Max(ResourcesProcessed, progress.ResourcesProcessed);
            ResourcesWritten = Math.Max(ResourcesWritten, progress.ResourcesWritten);

            if (!string.Equals(currentStage, progress.Stage, StringComparison.OrdinalIgnoreCase))
            {
                CompleteCurrentStage();
                currentStage = progress.Stage;
                currentMessage = progress.Message;
                stageStartedAt = stopwatch.Elapsed;
                HeartbeatDue = false;
                stageChanged = true;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(progress.Message))
                {
                    currentMessage = progress.Message;
                }

                HeartbeatDue = stopwatch.Elapsed - lastHeartbeatAt >= heartbeatInterval;
            }

            return stageChanged;
        }

        public void MarkHeartbeatLogged()
        {
            lastHeartbeatAt = stopwatch.Elapsed;
            HeartbeatDue = false;
        }

        public string BuildHeartbeatMessage()
        {
            var throughput = Elapsed.TotalSeconds <= 0 ? 0 : ResourcesProcessed / Elapsed.TotalSeconds;
            var detail = string.IsNullOrWhiteSpace(currentMessage)
                ? $"{ResourcesProcessed:N0} / {Math.Max(ResourcesDiscovered, ResourcesProcessed):N0} resources"
                : currentMessage;
            return $"{PackageName}: {currentStage} {detail}, {throughput:N0} res/sec, elapsed {Elapsed:hh\\:mm\\:ss}";
        }

        public void Complete(int resourceCount)
        {
            ResourcesProcessed = Math.Max(ResourcesProcessed, resourceCount);
            ResourcesWritten = Math.Max(ResourcesWritten, resourceCount);
            IsCompleted = true;
            stopwatch.Stop();
            CompleteCurrentStage();
        }

        public void Skip()
        {
            IsSkipped = true;
        }

        public void Fail(string reason)
        {
            FailureReason = reason;
            IsFailed = true;
            if (stopwatch.IsRunning)
            {
                stopwatch.Stop();
                CompleteCurrentStage();
            }
        }

        public ActivePackageProgress ToSnapshot()
        {
            var throughput = Elapsed.TotalSeconds <= 0 ? 0 : ResourcesProcessed / Elapsed.TotalSeconds;
            return new ActivePackageProgress(
                PackagePath,
                PackageName,
                currentStage,
                ResourcesDiscovered,
                ResourcesProcessed,
                ResourcesWritten,
                Elapsed,
                throughput,
                HeartbeatDue);
        }

        public PackageRunSummary ToRunSummary() =>
            new(
                PackagePath,
                FileSize,
                Math.Max(ResourcesProcessed, ResourcesWritten),
                Elapsed,
                IsSkipped,
                IsFailed,
                phaseTimings.ToArray());

        public long EstimateProcessedBytes()
        {
            if (FileSize <= 0)
            {
                return 0;
            }

            if (IsTerminal)
            {
                return FileSize;
            }

            var denominator = Math.Max(ResourcesDiscovered, ResourcesProcessed);
            if (denominator <= 0)
            {
                return 0;
            }

            var fraction = Math.Clamp((double)ResourcesProcessed / denominator, 0d, 1d);
            return (long)Math.Round(FileSize * fraction, MidpointRounding.AwayFromZero);
        }

        private void CompleteCurrentStage()
        {
            var elapsed = stopwatch.Elapsed - stageStartedAt;
            if (elapsed > TimeSpan.Zero)
            {
                phaseTimings.Add(new PackagePhaseTiming(currentStage, elapsed));
            }
        }
    }

    private sealed class PackageByteCache(long byteBudget)
    {
        private readonly object gate = new();
        private readonly Dictionary<string, CacheEntry> entries = new(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> lru = [];
        private readonly ConcurrentDictionary<string, SemaphoreSlim> loadLocks = new(StringComparer.OrdinalIgnoreCase);
        private long currentBytes;

        public async Task<byte[]?> GetBytesAsync(string packagePath, CancellationToken cancellationToken)
        {
            if (byteBudget <= 0)
            {
                return null;
            }

            if (TryGetCached(packagePath, out var cached))
            {
                return cached;
            }

            var loader = loadLocks.GetOrAdd(packagePath, static _ => new SemaphoreSlim(1, 1));
            await loader.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (TryGetCached(packagePath, out cached))
                {
                    return cached;
                }

                var info = new FileInfo(packagePath);
                if (!info.Exists || info.Length <= 0 || info.Length > byteBudget)
                {
                    return null;
                }

                var bytes = await File.ReadAllBytesAsync(packagePath, cancellationToken).ConfigureAwait(false);
                AddOrUpdate(packagePath, bytes);
                return bytes;
            }
            catch
            {
                return null;
            }
            finally
            {
                loader.Release();
            }
        }

        private bool TryGetCached(string packagePath, out byte[]? bytes)
        {
            lock (gate)
            {
                if (entries.TryGetValue(packagePath, out var entry))
                {
                    MoveToFront(entry.Node);
                    bytes = entry.Bytes;
                    return true;
                }
            }

            bytes = null;
            return false;
        }

        private void AddOrUpdate(string packagePath, byte[] bytes)
        {
            lock (gate)
            {
                if (entries.TryGetValue(packagePath, out var existing))
                {
                    currentBytes -= existing.Bytes.LongLength;
                    existing.Bytes = bytes;
                    currentBytes += bytes.LongLength;
                    MoveToFront(existing.Node);
                }
                else
                {
                    var node = new LinkedListNode<string>(packagePath);
                    lru.AddFirst(node);
                    entries[packagePath] = new CacheEntry(bytes, node);
                    currentBytes += bytes.LongLength;
                }

                while (currentBytes > byteBudget && lru.Last is not null)
                {
                    var key = lru.Last.Value;
                    var node = lru.Last;
                    lru.RemoveLast();
                    if (entries.Remove(key, out var removed))
                    {
                        currentBytes -= removed.Bytes.LongLength;
                    }
                }
            }
        }

        private void MoveToFront(LinkedListNode<string> node)
        {
            var list = node.List;
            if (list is null || ReferenceEquals(list.First, node))
            {
                return;
            }

            list.Remove(node);
            list.AddFirst(node);
        }

        private sealed class CacheEntry(byte[] bytes, LinkedListNode<string> node)
        {
            public byte[] Bytes { get; set; } = bytes;
            public LinkedListNode<string> Node { get; } = node;
        }
    }

    private sealed class WorkerSlotState
    {
        public WorkerSlotState(int workerId)
        {
            WorkerId = workerId;
            Status = WorkerSlotStatus.Waiting;
            Stage = "waiting for work";
        }

        public int WorkerId { get; }
        public WorkerSlotStatus Status { get; private set; }
        public string? PackagePath { get; private set; }
        public string? PackageName { get; private set; }
        public string Stage { get; private set; }
        public int ResourcesDiscovered { get; private set; }
        public int ResourcesProcessed { get; private set; }
        public int ResourcesWritten { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public double Throughput { get; private set; }
        public string? FailureReason { get; private set; }

        public void Activate(string packagePath)
        {
            Status = WorkerSlotStatus.Active;
            PackagePath = packagePath;
            PackageName = Path.GetFileName(packagePath);
            Stage = "opening package";
            ResourcesDiscovered = 0;
            ResourcesProcessed = 0;
            ResourcesWritten = 0;
            Elapsed = TimeSpan.Zero;
            Throughput = 0;
            FailureReason = null;
        }

        public void Update(PackageState packageState)
        {
            Status = WorkerSlotStatus.Active;
            PackagePath = packageState.PackagePath;
            PackageName = packageState.PackageName;
            Stage = string.IsNullOrWhiteSpace(packageState.CurrentMessage)
                ? packageState.CurrentStage
                : $"{packageState.CurrentStage} - {packageState.CurrentMessage}";
            ResourcesDiscovered = packageState.ResourcesDiscovered;
            ResourcesProcessed = packageState.ResourcesProcessed;
            ResourcesWritten = packageState.ResourcesWritten;
            Elapsed = packageState.Elapsed;
            Throughput = Elapsed.TotalSeconds <= 0 ? 0 : packageState.ResourcesProcessed / Elapsed.TotalSeconds;
            FailureReason = packageState.FailureReason;
        }

        public void Complete(PackageState packageState)
        {
            Update(packageState);
            Status = WorkerSlotStatus.Idle;
            Stage = "idle";
            PackagePath = null;
            PackageName = null;
            FailureReason = null;
        }

        public void Wait()
        {
            if (Status == WorkerSlotStatus.Failed)
            {
                return;
            }

            Status = WorkerSlotStatus.Waiting;
            if (string.IsNullOrWhiteSpace(PackageName))
            {
                Stage = "waiting for work";
            }
        }

        public void Fail(PackageState packageState, string reason)
        {
            Update(packageState);
            Status = WorkerSlotStatus.Failed;
            FailureReason = reason;
            Stage = "failed";
        }

        public WorkerSlotProgress ToSnapshot() =>
            new(
                WorkerId,
                Status,
                PackagePath,
                PackageName,
                Stage,
                ResourcesDiscovered,
                ResourcesProcessed,
                ResourcesWritten,
                Elapsed,
                Throughput,
                FailureReason);
    }
}
