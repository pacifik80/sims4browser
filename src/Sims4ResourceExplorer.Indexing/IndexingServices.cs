using Microsoft.Data.Sqlite;
using LlamaLogic.Packages;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
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
    private readonly ICacheService cacheService;

    public SqliteIndexStore(ICacheService cacheService)
    {
        this.cacheService = cacheService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        cacheService.EnsureCreated();

        await using var connection = await OpenConnectionAsync(cancellationToken);
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

            CREATE TABLE IF NOT EXISTS packages (
                data_source_id TEXT NOT NULL,
                package_path TEXT NOT NULL,
                file_size INTEGER NOT NULL,
                last_write_utc TEXT NOT NULL,
                indexed_utc TEXT NOT NULL,
                PRIMARY KEY (data_source_id, package_path)
            );

            CREATE TABLE IF NOT EXISTS resources (
                id TEXT PRIMARY KEY,
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
                diagnostics TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_resources_search ON resources(type_name, full_tgi, package_path, name);
            CREATE INDEX IF NOT EXISTS ix_resources_source ON resources(data_source_id, package_path);
            CREATE INDEX IF NOT EXISTS ix_resources_package_instance ON resources(package_path, instance_hex, type_name, full_tgi);

            CREATE TABLE IF NOT EXISTS assets (
                id TEXT PRIMARY KEY,
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
                package_name TEXT NULL,
                root_tgi TEXT NOT NULL,
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

            CREATE INDEX IF NOT EXISTS ix_assets_search ON assets(asset_kind, display_name, package_path);

            CREATE VIRTUAL TABLE IF NOT EXISTS resources_fts USING fts5(
                id UNINDEXED,
                data_source_id UNINDEXED,
                package_path,
                type_name,
                full_tgi,
                name,
                description,
                tokenize = "unicode61 remove_diacritics 0 tokenchars '._:-/\\'"
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS assets_fts USING fts5(
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
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureDeferredMetadataSchemaAsync(connection, cancellationToken);
        await EnsureScanTokenSchemaAsync(connection, cancellationToken);
        await EnsureAssetSummarySchemaAsync(connection, cancellationToken);
        var ftsResyncRequired = await EnsureFtsSchemaAsync(connection, cancellationToken);
        if (ftsResyncRequired)
        {
            await SyncFtsTablesAsync(connection, cancellationToken);
        }
    }

    public async Task UpsertDataSourcesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        foreach (var source in sources)
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

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, PackageFingerprint>> LoadPackageFingerprintsAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken)
    {
        var sourceIds = dataSourceIds.Select(id => id.ToString("D")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (sourceIds.Length == 0)
        {
            return new Dictionary<string, PackageFingerprint>(StringComparer.OrdinalIgnoreCase);
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
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

    public async Task<bool> NeedsRescanAsync(Guid dataSourceId, string packagePath, long fileSize, DateTimeOffset lastWriteTimeUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
    }

    public async Task<IIndexWriteSession> OpenWriteSessionAsync(CancellationToken cancellationToken)
    {
        var connection = await OpenConnectionAsync(cancellationToken);
        var session = new SqliteIndexWriteSession(connection);
        await session.PrepareForBulkWriteAsync(cancellationToken);
        return session;
    }

    public async Task<ResourceMetadata> PersistResourceEnrichmentAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE resources
            SET name = $name,
                description = $description,
                compressed_size = $compressedSize,
                uncompressed_size = $uncompressedSize,
                is_compressed = $isCompressed,
                diagnostics = $diagnostics
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", resource.Id.ToString("D"));
        command.Parameters.AddWithValue("$name", (object?)resource.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)resource.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$compressedSize", (object?)resource.CompressedSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$uncompressedSize", (object?)resource.UncompressedSize ?? DBNull.Value);
        command.Parameters.AddWithValue("$isCompressed", resource.IsCompressed.HasValue ? (resource.IsCompressed.Value ? 1 : 0) : DBNull.Value);
        command.Parameters.AddWithValue("$diagnostics", resource.Diagnostics);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var ftsCommand = connection.CreateCommand();
        ftsCommand.CommandText = "UPDATE resources_fts SET name = $name, description = $description, type_name = $typeName, full_tgi = $fullTgi, package_path = $packagePath WHERE id = $id;";
        ftsCommand.Parameters.AddWithValue("$id", resource.Id.ToString("D"));
        ftsCommand.Parameters.AddWithValue("$name", resource.Name ?? string.Empty);
        ftsCommand.Parameters.AddWithValue("$description", resource.Description ?? string.Empty);
        ftsCommand.Parameters.AddWithValue("$typeName", resource.Key.TypeName);
        ftsCommand.Parameters.AddWithValue("$fullTgi", resource.Key.FullTgi);
        ftsCommand.Parameters.AddWithValue("$packagePath", resource.PackagePath);
        await ftsCommand.ExecuteNonQueryAsync(cancellationToken);
        return resource;
    }

    public async Task<WindowedQueryResult<ResourceMetadata>> QueryResourcesAsync(RawResourceBrowserQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var (whereClause, bindParameters) = BuildRawResourceWhereClause(query);

        var totalCount = await CountAsync(connection, $"SELECT COUNT(*) FROM resources{whereClause};", bindParameters, cancellationToken);
        await using var command = connection.CreateCommand();
        bindParameters(command);
        command.CommandText =
            $"""
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics
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

    public async Task<WindowedQueryResult<AssetSummary>> QueryAssetsAsync(AssetBrowserQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var (whereClause, bindParameters) = BuildAssetWhereClause(query);

        var totalCount = await CountAsync(connection, $"SELECT COUNT(*) FROM assets{whereClause};", bindParameters, cancellationToken);
        await using var command = connection.CreateCommand();
        bindParameters(command);
        command.CommandText =
            $"""
            SELECT id, data_source_id, source_kind, asset_kind, display_name, category, description, package_path, root_tgi, thumbnail_tgi, variant_count, linked_resource_count,
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

    public async Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
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
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var kind = assetKind.ToString();
        return new AssetFacetOptions(
            await ReadDistinctAssetValuesAsync(connection, kind, "category", cancellationToken),
            await ReadDistinctAssetValuesAsync(connection, kind, "root_type_name", cancellationToken),
            await ReadDistinctAssetValuesAsync(connection, kind, "identity_type", cancellationToken),
            await ReadDistinctAssetValuesAsync(connection, kind, "primary_geometry_type", cancellationToken),
            await ReadDistinctAssetValuesAsync(connection, kind, "thumbnail_type_name", cancellationToken),
            await ReadDistinctAssetHexValuesAsync(connection, kind, "catalog_signal_0020", cancellationToken),
            await ReadDistinctAssetHexValuesAsync(connection, kind, "catalog_signal_002c", cancellationToken),
            await ReadDistinctAssetHexValuesAsync(connection, kind, "catalog_signal_0030", cancellationToken),
            await ReadDistinctAssetHexValuesAsync(connection, kind, "catalog_signal_0034", cancellationToken));
    }

    public async Task<IReadOnlyList<IndexedPackageRecord>> GetIndexedPackagesAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken)
    {
        var sourceIds = dataSourceIds.Select(id => id.ToString("D")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        await using var connection = await OpenConnectionAsync(cancellationToken);
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

    public async Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics
            FROM resources
            WHERE package_path = $packagePath
            ORDER BY type_name, full_tgi;
            """;
        command.Parameters.AddWithValue("$packagePath", packagePath);

        return await ReadResourcesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceMetadata>> GetResourcesByInstanceAsync(string packagePath, ulong fullInstance, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics
            FROM resources
            WHERE package_path = $packagePath AND instance_hex = $instanceHex
            ORDER BY type_name, full_tgi;
            """;
        command.Parameters.AddWithValue("$packagePath", packagePath);
        command.Parameters.AddWithValue("$instanceHex", fullInstance.ToString("X16"));

        return await ReadResourcesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AssetSummary>> GetPackageAssetsAsync(string packagePath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, asset_kind, display_name, category, description, package_path, root_tgi, thumbnail_tgi,
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
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                   catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics
            FROM resources
            WHERE full_tgi = $fullTgi
            ORDER BY package_path, type_name;
            """;
        command.Parameters.AddWithValue("$fullTgi", fullTgi);
        return await ReadResourcesAsync(command, cancellationToken);
    }

    public async Task UpdatePackageAssetsAsync(Guid dataSourceId, string packagePath, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var scanToken = Guid.NewGuid().ToString("N");
        await using var insertCommand = SqliteIndexWriteSession.CreateInsertAssetCommand(connection);
        await using var syncFtsCommand = SqliteIndexWriteSession.CreateSyncAssetsFtsForPackageCommand(connection);
        await using var deletePackageCommand = SqliteIndexWriteSession.CreateDeletePackageAssetsCommand(connection);
        await using var deleteFtsCommand = SqliteIndexWriteSession.CreateDeleteAssetsFtsCommand(connection);
        insertCommand.Transaction = transaction;
        syncFtsCommand.Transaction = transaction;
        deletePackageCommand.Transaction = transaction;
        deleteFtsCommand.Transaction = transaction;
        insertCommand.Prepare();
        syncFtsCommand.Prepare();
        deletePackageCommand.Prepare();
        deleteFtsCommand.Prepare();
        SqliteIndexWriteSession.Bind(deletePackageCommand, transaction, dataSourceId, packagePath);
        await deletePackageCommand.ExecuteNonQueryAsync(cancellationToken);
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

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = cacheService.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var pragma = connection.CreateCommand();
        pragma.CommandText =
            """
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA temp_store = MEMORY;
            """;
        await pragma.ExecuteNonQueryAsync(cancellationToken);
        return connection;
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
                reader.IsDBNull(14) ? null : Convert.ToUInt32(reader.GetInt64(14))));
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
            FROM assets;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
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
            WHERE asset_kind = $assetKind AND {columnName} IS NOT NULL AND {columnName} <> ''
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
            WHERE asset_kind = $assetKind AND {columnName} IS NOT NULL
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

        if (query.Domain == AssetBrowserDomain.BuildBuy)
        {
            clauses.Add("asset_kind = 'BuildBuy'");
        }
        else
        {
            clauses.Add("asset_kind = 'Cas'");
        }

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
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                reader.GetString(16),
                new AssetCapabilitySnapshot(
                    reader.IsDBNull(12) ? true : ReadOptionalBool(reader, 12),
                    ReadOptionalBool(reader, 13),
                    ReadOptionalBool(reader, 14),
                    ReadOptionalBool(reader, 15),
                    HasThumbnail: !reader.IsDBNull(9) && !string.IsNullOrWhiteSpace(reader.GetString(9)),
                    HasVariants: reader.GetInt32(10) > 1,
                    HasIdentityMetadata: ReadOptionalBool(reader, 23),
                    HasRigReference: ReadOptionalBool(reader, 24),
                    HasGeometryReference: ReadOptionalBool(reader, 25),
                    HasMaterialResourceCandidate: ReadOptionalBool(reader, 26),
                    HasTextureResourceCandidate: ReadOptionalBool(reader, 27),
                    IsPackageLocalGraph: ReadOptionalBool(reader, 28),
                    HasDiagnostics: ReadOptionalBool(reader, 29)),
                PackageName: reader.IsDBNull(17) ? null : reader.GetString(17),
                RootTypeName: reader.IsDBNull(18) ? null : reader.GetString(18),
                ThumbnailTypeName: reader.IsDBNull(19) ? null : reader.GetString(19),
                PrimaryGeometryType: reader.IsDBNull(20) ? null : reader.GetString(20),
                IdentityType: reader.IsDBNull(21) ? null : reader.GetString(21),
                CategoryNormalized: reader.IsDBNull(22) ? null : reader.GetString(22),
                Description: reader.IsDBNull(6) ? null : reader.GetString(6),
                CatalogSignal0020: reader.IsDBNull(30) ? null : Convert.ToUInt32(reader.GetInt64(30)),
                CatalogSignal002C: reader.IsDBNull(31) ? null : Convert.ToUInt32(reader.GetInt64(31)),
                CatalogSignal0030: reader.IsDBNull(32) ? null : Convert.ToUInt32(reader.GetInt64(32)),
                CatalogSignal0034: reader.IsDBNull(33) ? null : Convert.ToUInt32(reader.GetInt64(33))));
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
                id TEXT PRIMARY KEY,
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
                diagnostics TEXT NOT NULL
            );

            INSERT INTO resources(
                id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics)
            SELECT
                id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name, NULL,
                NULL, NULL, NULL, NULL,
                compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics
            FROM resources_legacy;

            DROP TABLE resources_legacy;

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
        await EnsureColumnAsync(connection, "assets", "scan_token", "TEXT NULL", cancellationToken);
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
        await EnsureAssetColumnAsync(connection, columns, "package_name", "TEXT NULL", cancellationToken);
        await EnsureAssetColumnAsync(connection, columns, "root_type_name", "TEXT NULL", cancellationToken);
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

        private readonly SqliteConnection connection;
        private readonly SqliteCommand deletePackageResourcesCommand;
        private readonly SqliteCommand deletePackageAssetsCommand;
        private readonly SqliteCommand deleteResourcesFtsCommand;
        private readonly SqliteCommand deleteAssetsFtsCommand;
        private readonly SqliteCommand upsertPackageCommand;
        private readonly SqliteCommand insertResourceCommand;
        private readonly SqliteCommand insertAssetCommand;
        private bool hasPendingFtsSync;
        private IndexWriteMetrics? lastMetrics;
        private bool secondaryIndexesDropped;
        private readonly BatchMetricsAccumulator lifetimeMetrics = new();

        public SqliteIndexWriteSession(SqliteConnection connection)
        {
            this.connection = connection;
            deletePackageResourcesCommand = CreateDeletePackageResourcesCommand(connection);
            deletePackageAssetsCommand = CreateDeletePackageAssetsCommand(connection);
            deleteResourcesFtsCommand = CreateDeleteResourcesFtsCommand(connection);
            deleteAssetsFtsCommand = CreateDeleteAssetsFtsCommand(connection);
            upsertPackageCommand = CreateUpsertPackageCommand(connection);
            insertResourceCommand = CreateInsertResourceCommand(connection, insertOnly: true);
            insertAssetCommand = CreateInsertAssetCommand(connection, insertOnly: true);
            PrepareCommands();
        }

        public async Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
        {
            var metrics = new BatchMetricsAccumulator();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await ReplacePackageCoreAsync(transaction, packageScan, assets, maintainFtsPerPackage: true, cancellationToken, metrics);
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
            foreach (var item in batch)
            {
                await ReplacePackageCoreAsync(transaction, item.PackageScan, item.Assets, maintainFtsPerPackage: false, cancellationToken, metrics);
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
            if (secondaryIndexesDropped)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                DROP INDEX IF EXISTS ix_resources_search;
                DROP INDEX IF EXISTS ix_resources_source;
                DROP INDEX IF EXISTS ix_resources_package_instance;
                DROP INDEX IF EXISTS ix_assets_search;
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            stopwatch.Stop();
            lifetimeMetrics.DropIndexesElapsed += stopwatch.Elapsed;
            secondaryIndexesDropped = true;
        }

        private async Task ReplacePackageCoreAsync(SqliteTransaction transaction, PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, bool maintainFtsPerPackage, CancellationToken cancellationToken, BatchMetricsAccumulator metrics)
        {
            var scanToken = Guid.NewGuid().ToString("N");

            var deleteStopwatch = Stopwatch.StartNew();
            Bind(deletePackageResourcesCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
            await deletePackageResourcesCommand.ExecuteNonQueryAsync(cancellationToken);

            Bind(deletePackageAssetsCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
            await deletePackageAssetsCommand.ExecuteNonQueryAsync(cancellationToken);
            deleteStopwatch.Stop();
            metrics.DeleteElapsed += deleteStopwatch.Elapsed;

            if (maintainFtsPerPackage)
            {
                var deleteFtsStopwatch = Stopwatch.StartNew();
                Bind(deleteResourcesFtsCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
                await deleteResourcesFtsCommand.ExecuteNonQueryAsync(cancellationToken);

                Bind(deleteAssetsFtsCommand, transaction, packageScan.DataSourceId, packageScan.PackagePath);
                await deleteAssetsFtsCommand.ExecuteNonQueryAsync(cancellationToken);
                deleteFtsStopwatch.Stop();
                metrics.FtsElapsed += deleteFtsStopwatch.Elapsed;
            }

            Bind(upsertPackageCommand, transaction, packageScan);
            await upsertPackageCommand.ExecuteNonQueryAsync(cancellationToken);

            insertResourceCommand.Transaction = transaction;
            var resourceInsertStopwatch = Stopwatch.StartNew();
            foreach (var resource in packageScan.Resources)
            {
                Bind(insertResourceCommand, resource, scanToken);
                await insertResourceCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            resourceInsertStopwatch.Stop();
            metrics.InsertResourcesElapsed += resourceInsertStopwatch.Elapsed;

            insertAssetCommand.Transaction = transaction;
            var assetInsertStopwatch = Stopwatch.StartNew();
            foreach (var asset in assets)
            {
                Bind(insertAssetCommand, asset, scanToken);
                await insertAssetCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            assetInsertStopwatch.Stop();
            metrics.InsertAssetsElapsed += assetInsertStopwatch.Elapsed;

            if (maintainFtsPerPackage)
            {
                var ftsStopwatch = Stopwatch.StartNew();
                await SyncFtsForPackageAsync(transaction, packageScan.DataSourceId, packageScan.PackagePath, cancellationToken);
                ftsStopwatch.Stop();
                metrics.FtsElapsed += ftsStopwatch.Elapsed;
            }
            else
            {
                hasPendingFtsSync = true;
            }
        }

        public async Task FinalizeAsync(CancellationToken cancellationToken)
        {
            if (!hasPendingFtsSync)
            {
                return;
            }

            var ftsStopwatch = Stopwatch.StartNew();
            await SyncFtsTablesAsync(connection, cancellationToken);
            ftsStopwatch.Stop();
            hasPendingFtsSync = false;
            await RebuildSecondaryIndexesAsync(cancellationToken);
            lastMetrics = new IndexWriteMetrics(
                lifetimeMetrics.DropIndexesElapsed,
                TimeSpan.Zero,
                TimeSpan.Zero,
                TimeSpan.Zero,
                ftsStopwatch.Elapsed,
                lifetimeMetrics.RebuildIndexesElapsed,
                TimeSpan.Zero,
                0,
                0,
                0);
        }

        public IndexWriteMetrics? ConsumeLastMetrics()
        {
            var metrics = lastMetrics;
            lastMetrics = null;
            return metrics;
        }

        private async Task SyncFtsForPackageAsync(SqliteTransaction transaction, Guid dataSourceId, string packagePath, CancellationToken cancellationToken)
        {
            Bind(deleteResourcesFtsCommand, transaction, dataSourceId, packagePath);
            await deleteResourcesFtsCommand.ExecuteNonQueryAsync(cancellationToken);

            Bind(deleteAssetsFtsCommand, transaction, dataSourceId, packagePath);
            await deleteAssetsFtsCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var syncResourcesCommand = CreateSyncResourcesFtsForPackageCommand(connection);
            syncResourcesCommand.Transaction = transaction;
            syncResourcesCommand.Prepare();
            Bind(syncResourcesCommand, transaction, dataSourceId, packagePath);
            await syncResourcesCommand.ExecuteNonQueryAsync(cancellationToken);

            await using var syncAssetsCommand = CreateSyncAssetsFtsForPackageCommand(connection);
            syncAssetsCommand.Transaction = transaction;
            syncAssetsCommand.Prepare();
            Bind(syncAssetsCommand, transaction, dataSourceId, packagePath);
            await syncAssetsCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (secondaryIndexesDropped)
            {
                try
                {
                    await RebuildSecondaryIndexesAsync(CancellationToken.None);
                }
                catch
                {
                }
            }

            await insertAssetCommand.DisposeAsync();
            await insertResourceCommand.DisposeAsync();
            await upsertPackageCommand.DisposeAsync();
            await deletePackageAssetsCommand.DisposeAsync();
            await deleteAssetsFtsCommand.DisposeAsync();
            await deletePackageResourcesCommand.DisposeAsync();
            await deleteResourcesFtsCommand.DisposeAsync();
            await connection.DisposeAsync();
        }

        private async Task RebuildSecondaryIndexesAsync(CancellationToken cancellationToken)
        {
            if (!secondaryIndexesDropped)
            {
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE INDEX IF NOT EXISTS ix_resources_search ON resources(type_name, full_tgi, package_path, name);
                CREATE INDEX IF NOT EXISTS ix_resources_source ON resources(data_source_id, package_path);
                CREATE INDEX IF NOT EXISTS ix_resources_package_instance ON resources(package_path, instance_hex, type_name, full_tgi);
                CREATE INDEX IF NOT EXISTS ix_assets_search ON assets(asset_kind, display_name, package_path);
                """;
            await command.ExecuteNonQueryAsync(cancellationToken);
            stopwatch.Stop();
            lifetimeMetrics.RebuildIndexesElapsed += stopwatch.Elapsed;
            secondaryIndexesDropped = false;
        }

        private void PrepareCommands()
        {
            deletePackageResourcesCommand.Prepare();
            deletePackageAssetsCommand.Prepare();
            deleteResourcesFtsCommand.Prepare();
            deleteAssetsFtsCommand.Prepare();
            upsertPackageCommand.Prepare();
            insertResourceCommand.Prepare();
            insertAssetCommand.Prepare();
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
                      compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics)
                  VALUES(
                      $id, $dataSourceId, $sourceKind, $packagePath, $scanToken, $typeHex, $typeName, $groupHex, $instanceHex, $fullTgi, $name, $description,
                      $catalogSignal0020, $catalogSignal002C, $catalogSignal0030, $catalogSignal0034,
                      $compressedSize, $uncompressedSize, $isCompressed, $previewKind, $isPreviewable, $isExportCapable, $assetLinkageSummary, $diagnostics);
                  """
                : """
                  INSERT INTO resources(
                      id, data_source_id, source_kind, package_path, scan_token, type_hex, type_name, group_hex, instance_hex, full_tgi, name, description,
                      catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034,
                      compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics)
                  VALUES(
                      $id, $dataSourceId, $sourceKind, $packagePath, $scanToken, $typeHex, $typeName, $groupHex, $instanceHex, $fullTgi, $name, $description,
                      $catalogSignal0020, $catalogSignal002C, $catalogSignal0030, $catalogSignal0034,
                      $compressedSize, $uncompressedSize, $isCompressed, $previewKind, $isPreviewable, $isExportCapable, $assetLinkageSummary, $diagnostics)
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
                      diagnostics = excluded.diagnostics;
                  """;
            foreach (var parameterName in new[] { "$id", "$dataSourceId", "$sourceKind", "$packagePath", "$scanToken", "$typeHex", "$typeName", "$groupHex", "$instanceHex", "$fullTgi", "$name", "$description", "$catalogSignal0020", "$catalogSignal002C", "$catalogSignal0030", "$catalogSignal0034", "$compressedSize", "$uncompressedSize", "$isCompressed", "$previewKind", "$isPreviewable", "$isExportCapable", "$assetLinkageSummary", "$diagnostics" })
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
                      id, data_source_id, source_kind, asset_kind, display_name, category, description, catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034, category_normalized, package_path, scan_token, package_name, root_tgi, root_type_name, thumbnail_tgi, thumbnail_type_name,
                      primary_geometry_type, identity_type, variant_count, linked_resource_count, has_scene_root, has_exact_geometry_candidate, has_material_references, has_texture_references,
                      has_identity_metadata, has_rig_reference, has_geometry_reference, has_material_resource_candidate, has_texture_resource_candidate, is_package_local_graph, has_diagnostics, diagnostics)
                  VALUES(
                      $id, $dataSourceId, $sourceKind, $assetKind, $displayName, $category, $description, $catalogSignal0020, $catalogSignal002C, $catalogSignal0030, $catalogSignal0034, $categoryNormalized, $packagePath, $scanToken, $packageName, $rootTgi, $rootTypeName, $thumbnailTgi, $thumbnailTypeName,
                      $primaryGeometryType, $identityType, $variantCount, $linkedResourceCount, $hasSceneRoot, $hasExactGeometryCandidate, $hasMaterialReferences, $hasTextureReferences,
                      $hasIdentityMetadata, $hasRigReference, $hasGeometryReference, $hasMaterialResourceCandidate, $hasTextureResourceCandidate, $isPackageLocalGraph, $hasDiagnostics, $diagnostics);
                  """
                : """
                  INSERT INTO assets(
                      id, data_source_id, source_kind, asset_kind, display_name, category, description, catalog_signal_0020, catalog_signal_002c, catalog_signal_0030, catalog_signal_0034, category_normalized, package_path, scan_token, package_name, root_tgi, root_type_name, thumbnail_tgi, thumbnail_type_name,
                      primary_geometry_type, identity_type, variant_count, linked_resource_count, has_scene_root, has_exact_geometry_candidate, has_material_references, has_texture_references,
                      has_identity_metadata, has_rig_reference, has_geometry_reference, has_material_resource_candidate, has_texture_resource_candidate, is_package_local_graph, has_diagnostics, diagnostics)
                  VALUES(
                      $id, $dataSourceId, $sourceKind, $assetKind, $displayName, $category, $description, $catalogSignal0020, $catalogSignal002C, $catalogSignal0030, $catalogSignal0034, $categoryNormalized, $packagePath, $scanToken, $packageName, $rootTgi, $rootTypeName, $thumbnailTgi, $thumbnailTypeName,
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
                      package_name = excluded.package_name,
                      root_tgi = excluded.root_tgi,
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
            foreach (var parameterName in new[] { "$id", "$dataSourceId", "$sourceKind", "$assetKind", "$displayName", "$category", "$description", "$catalogSignal0020", "$catalogSignal002C", "$catalogSignal0030", "$catalogSignal0034", "$categoryNormalized", "$packagePath", "$scanToken", "$packageName", "$rootTgi", "$rootTypeName", "$thumbnailTgi", "$thumbnailTypeName", "$primaryGeometryType", "$identityType", "$variantCount", "$linkedResourceCount", "$hasSceneRoot", "$hasExactGeometryCandidate", "$hasMaterialReferences", "$hasTextureReferences", "$hasIdentityMetadata", "$hasRigReference", "$hasGeometryReference", "$hasMaterialResourceCandidate", "$hasTextureResourceCandidate", "$isPackageLocalGraph", "$hasDiagnostics", "$diagnostics" })
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
                WHERE data_source_id = $dataSourceId AND package_path = $packagePath;
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
}

public sealed class ResourceMetadataEnrichmentService : IResourceMetadataEnrichmentService
{
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IIndexStore indexStore;

    public ResourceMetadataEnrichmentService(IResourceCatalogService resourceCatalogService, IIndexStore indexStore)
    {
        this.resourceCatalogService = resourceCatalogService;
        this.indexStore = indexStore;
    }

    public async Task<ResourceMetadata> EnrichAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        if (resource.Name is not null &&
            resource.CompressedSize is not null &&
            resource.UncompressedSize is not null &&
            resource.IsCompressed is not null)
        {
            return resource;
        }

        var enriched = await resourceCatalogService.EnrichResourceAsync(resource, cancellationToken).ConfigureAwait(false);
        return await indexStore.PersistResourceEnrichmentAsync(enriched, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PackageIndexCoordinator
{
    private const int ParallelSeedEnrichmentThreshold = 256;
    private const int MaxSeedEnrichmentPackageHandles = 8;
    private const long DefaultPackageByteCacheBudgetBytes = 8L * 1024 * 1024 * 1024;
    private readonly IPackageScanner packageScanner;
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IAssetGraphBuilder assetGraphBuilder;
    private readonly IIndexStore indexStore;
    private readonly IndexingRunOptions options;
    private readonly ConcurrentDictionary<string, Task<IReadOnlyDictionary<uint, string>>> englishStringLookups = new(StringComparer.OrdinalIgnoreCase);
    private readonly PackageByteCache packageByteCache = new(DefaultPackageByteCacheBudgetBytes);

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

    public async Task RunAsync(IEnumerable<DataSourceDefinition> sources, IProgress<IndexingProgress>? progress, CancellationToken cancellationToken, int? requestedWorkerCount = null)
    {
        var effectiveOptions = requestedWorkerCount.HasValue
            ? options.WithWorkerCount(requestedWorkerCount.Value)
            : options;
        var state = new IndexingRunState(effectiveOptions);
        var activeSources = sources.Where(static source => source.IsEnabled).ToArray();
        using var reporterCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var reportLoop = RunReportingLoopAsync(progress, state, effectiveOptions, reporterCts.Token);

        progress?.Report(state.CreateSnapshot("preparing", $"Preparing indexing run for {activeSources.Length} source(s)."));

        var dataSourceStopwatch = Stopwatch.StartNew();
        await indexStore.UpsertDataSourcesAsync(activeSources, cancellationToken).ConfigureAwait(false);
        dataSourceStopwatch.Stop();
        state.RecordRunPhase("source sync", dataSourceStopwatch.Elapsed);
        progress?.Report(state.CreateSnapshot("preparing", "Loading cached package fingerprints."));

        var fingerprintStopwatch = Stopwatch.StartNew();
        var fingerprints = await indexStore.LoadPackageFingerprintsAsync(activeSources.Select(static source => source.Id), cancellationToken).ConfigureAwait(false);
        fingerprintStopwatch.Stop();
        state.RecordRunPhase("fingerprint preload", fingerprintStopwatch.Elapsed);
        progress?.Report(state.CreateSnapshot("preparing", $"Loaded {fingerprints.Count:N0} cached package fingerprint(s)."));

        try
        {
            progress?.Report(state.CreateSnapshot("preparing", "Opening SQLite write session."));

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
            await using var writeSession = await indexStore.OpenWriteSessionAsync(cancellationToken).ConfigureAwait(false);
            writerSessionStopwatch.Stop();
            state.RecordRunPhase("writer session open", writerSessionStopwatch.Elapsed);
            progress?.Report(state.CreateSnapshot("discovering packages", "Starting package discovery and scan queue."));

            var producer = ProduceWorkAsync(activeSources, fingerprints, scanChannel.Writer, state, progress, cancellationToken);
            var workers = Enumerable.Range(0, effectiveOptions.MaxPackageConcurrency)
                .Select(workerId => RunWorkerAsync(workerId + 1, scanChannel.Reader, persistChannel.Writer, state, cancellationToken))
                .ToArray();
            var writer = RunWriterAsync(persistChannel.Reader, writeSession, state, effectiveOptions.SqliteBatchSize, cancellationToken);

            await producer.ConfigureAwait(false);
            await Task.WhenAll(workers).ConfigureAwait(false);
            persistChannel.Writer.TryComplete();
            await writer.ConfigureAwait(false);
            await writeSession.FinalizeAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task ProduceWorkAsync(
        IReadOnlyList<DataSourceDefinition> activeSources,
        IReadOnlyDictionary<string, PackageFingerprint> fingerprints,
        System.Threading.Channels.ChannelWriter<PackageWorkItem> writer,
        IndexingRunState state,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken)
    {
        var discoveryStopwatch = Stopwatch.StartNew();
        var queueElapsed = TimeSpan.Zero;
        try
        {
            await foreach (var discoveredPackage in packageScanner.DiscoverPackagesAsync(activeSources, cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                state.MarkDiscovered(discoveredPackage.PackagePath, discoveredPackage.FileSize);

                if (!RequiresRescan(discoveredPackage, fingerprints))
                {
                    state.MarkSkipped(discoveredPackage.PackagePath, discoveredPackage.FileSize);
                    continue;
                }

                state.MarkQueued(discoveredPackage.PackagePath, discoveredPackage.FileSize);
                var queueStart = Stopwatch.StartNew();
                await writer.WriteAsync(new PackageWorkItem(discoveredPackage.Source, discoveredPackage.PackagePath, discoveredPackage.FileSize, discoveredPackage.LastWriteTimeUtc), cancellationToken).ConfigureAwait(false);
                queueStart.Stop();
                queueElapsed += queueStart.Elapsed;
            }
        }
        finally
        {
            discoveryStopwatch.Stop();
            state.RecordRunPhase("discovery", discoveryStopwatch.Elapsed);
            state.RecordRunPhase("queueing", queueElapsed);
            state.MarkDiscoveryCompleted();
            writer.TryComplete();
        }
    }

    private async Task RunWorkerAsync(
        int workerId,
        System.Threading.Channels.ChannelReader<PackageWorkItem> reader,
        System.Threading.Channels.ChannelWriter<PersistWorkItem> persistWriter,
        IndexingRunState state,
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
                await persistWriter.WriteAsync(new PersistWorkItem(workItem, packageScan, assets), cancellationToken).ConfigureAwait(false);
                persistEnqueueStopwatch.Stop();
                state.RecordRunPhase("worker persist enqueue wait", persistEnqueueStopwatch.Elapsed);
                state.MarkPersistQueued(workItem.PackagePath);

                state.UpdatePackageProgress(workItem.PackagePath, new PackageScanProgress(
                    "queued for DB write",
                    packageScan.Resources.Count,
                    packageScan.Resources.Count,
                    0,
                    state.GetElapsed(workItem.PackagePath)));
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
            await using var package = await TryOpenPackageForSeedEnrichmentAsync(packageScan.PackagePath, cancellationToken).ConfigureAwait(false);

            foreach (var (resource, index) in seedCandidates)
            {
                var enriched = await EnrichSeedResourceAsync(resource, package, sourceRootPath, cancellationToken).ConfigureAwait(false);
                if (enriched == resource)
                {
                    continue;
                }

                enrichedResources ??= [.. resources];
                enrichedResources[index] = enriched;
            }

            return enrichedResources is null
                ? packageScan
                : packageScan with { Resources = enrichedResources };
        }

        var enrichedBuffer = resources.ToArray();
        var chunkSize = Math.Max(1, (int)Math.Ceiling(seedCandidates.Length / (double)GetSeedEnrichmentParallelism(seedCandidates.Length)));
        var tasks = new List<Task>();
        for (var offset = 0; offset < seedCandidates.Length; offset += chunkSize)
        {
            var start = offset;
            var end = Math.Min(seedCandidates.Length, offset + chunkSize);
            tasks.Add(ProcessSeedEnrichmentChunkAsync(start, end));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return packageScan with { Resources = enrichedBuffer };

        async Task ProcessSeedEnrichmentChunkAsync(int start, int end)
        {
            await using var package = await TryOpenPackageForSeedEnrichmentAsync(packageScan.PackagePath, cancellationToken).ConfigureAwait(false);
            for (var index = start; index < end; index++)
            {
                var candidate = seedCandidates[index];
                enrichedBuffer[candidate.index] = await EnrichSeedResourceAsync(candidate.resource, package, sourceRootPath, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<ResourceMetadata> EnrichSeedResourceAsync(
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
            return resource;
        }
        catch (IOException)
        {
            return resource;
        }
        catch (NotSupportedException)
        {
            return resource;
        }

        var technicalName = Ts4SeedMetadataExtractor.TryExtractTechnicalName(resource, bytes);
        if (!string.IsNullOrWhiteSpace(technicalName))
        {
            return resource with { Name = technicalName };
        }

        if (resource.Key.TypeName != "ObjectCatalog")
        {
            return resource;
        }

        var localizedMetadata = await TryResolveObjectCatalogLocalizedMetadataAsync(resource, bytes, sourceRootPath, cancellationToken).ConfigureAwait(false);
        var rawSignals = Ts4SeedMetadataExtractor.TryExtractObjectCatalogSignals(bytes);
        if (string.IsNullOrWhiteSpace(localizedMetadata.DisplayName) &&
            string.IsNullOrWhiteSpace(localizedMetadata.Description) &&
            rawSignals is (null, null, null, null))
        {
            return resource;
        }

        return resource with
        {
            Name = string.IsNullOrWhiteSpace(localizedMetadata.DisplayName) ? resource.Name : localizedMetadata.DisplayName,
            Description = string.IsNullOrWhiteSpace(localizedMetadata.Description) ? resource.Description : localizedMetadata.Description,
            CatalogSignal0020 = rawSignals.Signal0020 ?? resource.CatalogSignal0020,
            CatalogSignal002C = rawSignals.Signal002C ?? resource.CatalogSignal002C,
            CatalogSignal0030 = rawSignals.Signal0030 ?? resource.CatalogSignal0030,
            CatalogSignal0034 = rawSignals.Signal0034 ?? resource.CatalogSignal0034
        };
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
        if (resource.Name is not null)
        {
            return false;
        }

        return resource.Key.TypeName switch
        {
            "ObjectCatalog" => true,
            "CASPart" => true,
            "ObjectDefinition" => !sameInstanceHasObjectCatalog.Contains(resource.Key.FullInstance),
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
                    state.UpdatePackageProgress(item.WorkItem.PackagePath, new PackageScanProgress(
                        "batching DB writes",
                        item.PackageScan.Resources.Count,
                        item.PackageScan.Resources.Count,
                        0,
                        state.GetElapsed(item.WorkItem.PackagePath)));
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
                        "finalizing package",
                        item.PackageScan.Resources.Count,
                        item.PackageScan.Resources.Count,
                        item.PackageScan.Resources.Count,
                        state.GetElapsed(item.WorkItem.PackagePath)));

                    state.MarkCompleted(item.WorkItem.PackagePath, item.PackageScan.Resources.Count);
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
            progress.Report(state.CreateSnapshot("indexing", state.DrainLatestMessage()));
        }
    }

    private static bool RequiresRescan(DiscoveredPackage package, IReadOnlyDictionary<string, PackageFingerprint> fingerprints)
    {
        if (!fingerprints.TryGetValue(BuildFingerprintKey(package.Source.Id, package.PackagePath), out var fingerprint))
        {
            return true;
        }

        return fingerprint.FileSize != package.FileSize || fingerprint.LastWriteTimeUtc != package.LastWriteTimeUtc;
    }

    private sealed record PackageWorkItem(DataSourceDefinition Source, string PackagePath, long FileSize, DateTimeOffset LastWriteUtc);

    private sealed record PersistWorkItem(PackageWorkItem WorkItem, PackageScanResult PackageScan, IReadOnlyList<AssetSummary> Assets);

    private sealed class IndexingRunState
    {
        private readonly object gate = new();
        private readonly Dictionary<string, PackageState> packages = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, WorkerSlotState> workerSlots;
        private readonly List<IndexingActivityEvent> recentEvents = [];
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly IndexingRunOptions options;
        private readonly Dictionary<string, TimeSpan> runPhases = new(StringComparer.OrdinalIgnoreCase);
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

        public void MarkPersistQueued(string packagePath)
        {
            lock (gate)
            {
                persistQueued++;
                RecordActivityUnsafe("persist-queued", $"Queued {Path.GetFileName(packagePath)} for SQLite write");
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
        packageScan.Resources.Any(static resource => resource.Key.TypeName is "ObjectCatalog" or "ObjectDefinition" or "CASPart");

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
