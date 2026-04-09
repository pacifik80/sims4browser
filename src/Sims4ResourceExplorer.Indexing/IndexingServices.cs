using Microsoft.Data.Sqlite;
using Sims4ResourceExplorer.Core;
using System.Diagnostics;
using System.Text;

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
    private const int InsertBatchSize = 200;
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
                type_hex TEXT NOT NULL,
                type_name TEXT NOT NULL,
                group_hex TEXT NOT NULL,
                instance_hex TEXT NOT NULL,
                full_tgi TEXT NOT NULL,
                name TEXT NULL,
                compressed_size INTEGER NOT NULL,
                uncompressed_size INTEGER NOT NULL,
                is_compressed INTEGER NOT NULL,
                preview_kind TEXT NOT NULL,
                is_previewable INTEGER NOT NULL,
                is_export_capable INTEGER NOT NULL,
                asset_linkage_summary TEXT NOT NULL,
                diagnostics TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_resources_search ON resources(type_name, full_tgi, package_path, name);
            CREATE INDEX IF NOT EXISTS ix_resources_source ON resources(data_source_id, package_path);

            CREATE TABLE IF NOT EXISTS assets (
                id TEXT PRIMARY KEY,
                data_source_id TEXT NOT NULL,
                source_kind TEXT NOT NULL,
                asset_kind TEXT NOT NULL,
                display_name TEXT NOT NULL,
                category TEXT NULL,
                package_path TEXT NOT NULL,
                root_tgi TEXT NOT NULL,
                thumbnail_tgi TEXT NULL,
                variant_count INTEGER NOT NULL,
                linked_resource_count INTEGER NOT NULL,
                diagnostics TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_assets_search ON assets(asset_kind, display_name, package_path);
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
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
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, transaction,
            "DELETE FROM resources WHERE data_source_id = $dataSourceId AND package_path = $packagePath;",
            cancellationToken,
            ("$dataSourceId", packageScan.DataSourceId.ToString("D")),
            ("$packagePath", packageScan.PackagePath));

        await ExecuteNonQueryAsync(connection, transaction,
            "DELETE FROM assets WHERE data_source_id = $dataSourceId AND package_path = $packagePath;",
            cancellationToken,
            ("$dataSourceId", packageScan.DataSourceId.ToString("D")),
            ("$packagePath", packageScan.PackagePath));

        await ExecuteNonQueryAsync(connection, transaction,
            """
            INSERT INTO packages(data_source_id, package_path, file_size, last_write_utc, indexed_utc)
            VALUES ($dataSourceId, $packagePath, $fileSize, $lastWriteUtc, $indexedUtc)
            ON CONFLICT(data_source_id, package_path) DO UPDATE SET
                file_size = excluded.file_size,
                last_write_utc = excluded.last_write_utc,
                indexed_utc = excluded.indexed_utc;
            """,
            cancellationToken,
            ("$dataSourceId", packageScan.DataSourceId.ToString("D")),
            ("$packagePath", packageScan.PackagePath),
            ("$fileSize", packageScan.FileSize),
            ("$lastWriteUtc", packageScan.LastWriteTimeUtc.ToString("O")),
            ("$indexedUtc", DateTimeOffset.UtcNow.ToString("O")));

        await InsertResourceBatchAsync(connection, transaction, packageScan.Resources, cancellationToken);
        await InsertAssetBatchAsync(connection, transaction, assets, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ResourceMetadata>> QueryResourcesAsync(ResourceQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics
            FROM resources
            WHERE ($search = '' OR
                   lower(COALESCE(name, '')) LIKE $like OR
                   lower(package_path) LIKE $like OR
                   lower(type_name) LIKE $like OR
                   lower(full_tgi) LIKE $like OR
                   lower(group_hex) LIKE $like OR
                   lower(instance_hex) LIKE $like OR
                   lower(asset_linkage_summary) LIKE $like)
              AND ($dataSourceId = '' OR data_source_id = $dataSourceId)
              AND ($packagePath = '' OR package_path = $packagePath)
              AND ($previewableOnly = 0 OR is_previewable = 1)
              AND ($exportCapableOnly = 0 OR is_export_capable = 1)
              AND ($audioOnly = 0 OR preview_kind = 'Audio')
              AND ($buildBuyOnly = 0 OR type_name IN ('ObjectCatalog', 'ObjectDefinition', 'BuyBuildThumbnail', 'Model', 'ModelLOD', 'Geometry'))
              AND ($casOnly = 0 OR type_name IN ('CASPart', 'CASPartThumbnail', 'BodyPartThumbnail', 'RegionMap', 'Rig', 'Geometry'))
            ORDER BY package_path, type_name, full_tgi
            LIMIT $limit;
            """;

        var search = query.SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
        command.Parameters.AddWithValue("$search", search);
        command.Parameters.AddWithValue("$like", $"%{search}%");
        command.Parameters.AddWithValue("$dataSourceId", query.DataSourceId?.ToString("D") ?? string.Empty);
        command.Parameters.AddWithValue("$packagePath", query.PackagePath ?? string.Empty);
        command.Parameters.AddWithValue("$previewableOnly", query.PreviewableOnly ? 1 : 0);
        command.Parameters.AddWithValue("$exportCapableOnly", query.ExportCapableOnly ? 1 : 0);
        command.Parameters.AddWithValue("$audioOnly", query.AudioOnly ? 1 : 0);
        command.Parameters.AddWithValue("$buildBuyOnly", query.BuildBuyOnly ? 1 : 0);
        command.Parameters.AddWithValue("$casOnly", query.CasOnly ? 1 : 0);
        command.Parameters.AddWithValue("$limit", query.Limit);

        return await ReadResourcesAsync(command, cancellationToken);
    }

    public async Task<IReadOnlyList<AssetSummary>> QueryAssetsAsync(LogicalAssetQuery query, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, asset_kind, display_name, category, package_path, root_tgi, thumbnail_tgi, variant_count, linked_resource_count, diagnostics
            FROM assets
            WHERE ($search = '' OR lower(display_name) LIKE $like OR lower(package_path) LIKE $like OR lower(root_tgi) LIKE $like OR lower(COALESCE(category, '')) LIKE $like)
              AND ($dataSourceId = '' OR data_source_id = $dataSourceId)
              AND ($buildBuyOnly = 0 OR asset_kind = 'BuildBuy')
              AND ($casOnly = 0 OR asset_kind = 'Cas')
            ORDER BY display_name, package_path
            LIMIT $limit;
            """;

        var search = query.SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
        command.Parameters.AddWithValue("$search", search);
        command.Parameters.AddWithValue("$like", $"%{search}%");
        command.Parameters.AddWithValue("$dataSourceId", query.DataSourceId?.ToString("D") ?? string.Empty);
        command.Parameters.AddWithValue("$buildBuyOnly", query.BuildBuyOnly ? 1 : 0);
        command.Parameters.AddWithValue("$casOnly", query.CasOnly ? 1 : 0);
        command.Parameters.AddWithValue("$limit", query.Limit);

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
                reader.GetString(6),
                ParseTgi(reader.GetString(7), string.Empty),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                reader.GetString(11)));
        }

        return results;
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

    public async Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name,
                   compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics
            FROM resources
            WHERE package_path = $packagePath
            ORDER BY type_name, full_tgi;
            """;
        command.Parameters.AddWithValue("$packagePath", packagePath);

        return await ReadResourcesAsync(command, cancellationToken);
    }

    public async Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken)
    {
        var resources = await QueryResourcesAsync(new ResourceQuery(fullTgi, BrowserMode.RawResources, PackagePath: packagePath, Limit: 1), cancellationToken);
        return resources.FirstOrDefault(resource => resource.Key.FullTgi.Equals(fullTgi, StringComparison.OrdinalIgnoreCase));
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

    private static async Task InsertResourceBatchAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<ResourceMetadata> resources, CancellationToken cancellationToken)
    {
        foreach (var batch in Batch(resources, InsertBatchSize))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var sql = new StringBuilder(
                """
                INSERT INTO resources(
                    id, data_source_id, source_kind, package_path, type_hex, type_name, group_hex, instance_hex, full_tgi, name,
                    compressed_size, uncompressed_size, is_compressed, preview_kind, is_previewable, is_export_capable, asset_linkage_summary, diagnostics)
                VALUES
                """);

            for (var index = 0; index < batch.Count; index++)
            {
                if (index > 0)
                {
                    sql.AppendLine(",");
                }

                sql.Append($"($id{index}, $dataSourceId{index}, $sourceKind{index}, $packagePath{index}, $typeHex{index}, $typeName{index}, $groupHex{index}, $instanceHex{index}, $fullTgi{index}, $name{index}, ");
                sql.Append($"$compressedSize{index}, $uncompressedSize{index}, $isCompressed{index}, $previewKind{index}, $isPreviewable{index}, $isExportCapable{index}, $assetLinkageSummary{index}, $diagnostics{index})");

                var resource = batch[index];
                command.Parameters.AddWithValue($"$id{index}", resource.Id.ToString("D"));
                command.Parameters.AddWithValue($"$dataSourceId{index}", resource.DataSourceId.ToString("D"));
                command.Parameters.AddWithValue($"$sourceKind{index}", resource.SourceKind.ToString());
                command.Parameters.AddWithValue($"$packagePath{index}", resource.PackagePath);
                command.Parameters.AddWithValue($"$typeHex{index}", $"{resource.Key.Type:X8}");
                command.Parameters.AddWithValue($"$typeName{index}", resource.Key.TypeName);
                command.Parameters.AddWithValue($"$groupHex{index}", $"{resource.Key.Group:X8}");
                command.Parameters.AddWithValue($"$instanceHex{index}", $"{resource.Key.FullInstance:X16}");
                command.Parameters.AddWithValue($"$fullTgi{index}", resource.Key.FullTgi);
                command.Parameters.AddWithValue($"$name{index}", (object?)resource.Name ?? DBNull.Value);
                command.Parameters.AddWithValue($"$compressedSize{index}", resource.CompressedSize);
                command.Parameters.AddWithValue($"$uncompressedSize{index}", resource.UncompressedSize);
                command.Parameters.AddWithValue($"$isCompressed{index}", resource.IsCompressed ? 1 : 0);
                command.Parameters.AddWithValue($"$previewKind{index}", resource.PreviewKind.ToString());
                command.Parameters.AddWithValue($"$isPreviewable{index}", resource.IsPreviewable ? 1 : 0);
                command.Parameters.AddWithValue($"$isExportCapable{index}", resource.IsExportCapable ? 1 : 0);
                command.Parameters.AddWithValue($"$assetLinkageSummary{index}", resource.AssetLinkageSummary);
                command.Parameters.AddWithValue($"$diagnostics{index}", resource.Diagnostics);
            }

            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task InsertAssetBatchAsync(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken)
    {
        foreach (var batch in Batch(assets, InsertBatchSize))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var sql = new StringBuilder(
                """
                INSERT INTO assets(
                    id, data_source_id, source_kind, asset_kind, display_name, category, package_path, root_tgi, thumbnail_tgi,
                    variant_count, linked_resource_count, diagnostics)
                VALUES
                """);

            for (var index = 0; index < batch.Count; index++)
            {
                if (index > 0)
                {
                    sql.AppendLine(",");
                }

                sql.Append($"($id{index}, $dataSourceId{index}, $sourceKind{index}, $assetKind{index}, $displayName{index}, $category{index}, $packagePath{index}, $rootTgi{index}, $thumbnailTgi{index}, ");
                sql.Append($"$variantCount{index}, $linkedResourceCount{index}, $diagnostics{index})");

                var asset = batch[index];
                command.Parameters.AddWithValue($"$id{index}", asset.Id.ToString("D"));
                command.Parameters.AddWithValue($"$dataSourceId{index}", asset.DataSourceId.ToString("D"));
                command.Parameters.AddWithValue($"$sourceKind{index}", asset.SourceKind.ToString());
                command.Parameters.AddWithValue($"$assetKind{index}", asset.AssetKind.ToString());
                command.Parameters.AddWithValue($"$displayName{index}", asset.DisplayName);
                command.Parameters.AddWithValue($"$category{index}", (object?)asset.Category ?? DBNull.Value);
                command.Parameters.AddWithValue($"$packagePath{index}", asset.PackagePath);
                command.Parameters.AddWithValue($"$rootTgi{index}", asset.RootKey.FullTgi);
                command.Parameters.AddWithValue($"$thumbnailTgi{index}", (object?)asset.ThumbnailTgi ?? DBNull.Value);
                command.Parameters.AddWithValue($"$variantCount{index}", asset.VariantCount);
                command.Parameters.AddWithValue($"$linkedResourceCount{index}", asset.LinkedResourceCount);
                command.Parameters.AddWithValue($"$diagnostics{index}", asset.Diagnostics);
            }

            command.CommandText = sql.ToString();
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, SqliteTransaction transaction, string sql, CancellationToken cancellationToken, params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;

        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
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
                reader.GetInt64(10),
                reader.GetInt64(11),
                reader.GetInt32(12) == 1,
                Enum.Parse<PreviewKind>(reader.GetString(13)),
                reader.GetInt32(14) == 1,
                reader.GetInt32(15) == 1,
                reader.GetString(16),
                reader.GetString(17)));
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
}

public sealed class PackageIndexCoordinator
{
    private readonly IPackageScanner packageScanner;
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IAssetGraphBuilder assetGraphBuilder;
    private readonly IIndexStore indexStore;
    private readonly IndexingRunOptions options;

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
        var activeSources = sources.Where(static source => source.IsEnabled).ToArray();
        await indexStore.UpsertDataSourcesAsync(activeSources, cancellationToken);

        var packagePaths = await packageScanner.DiscoverPackagePathsAsync(activeSources, cancellationToken);
        var workItems = packagePaths
            .Select(path => CreateWorkItem(path, activeSources))
            .Where(static item => item is not null)
            .Select(static item => item!)
            .ToArray();

        var state = new IndexingRunState(workItems.Length, effectiveOptions);
        using var reporterCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var reportLoop = RunReportingLoopAsync(progress, state, effectiveOptions, reporterCts.Token);

        try
        {
            progress?.Report(state.CreateSnapshot("discovering packages", "Preparing package scan queue."));

            var scanChannel = System.Threading.Channels.Channel.CreateBounded<PackageWorkItem>(new System.Threading.Channels.BoundedChannelOptions(effectiveOptions.PackageQueueCapacity)
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                SingleWriter = true,
                SingleReader = false
            });

            var persistChannel = System.Threading.Channels.Channel.CreateBounded<PersistWorkItem>(new System.Threading.Channels.BoundedChannelOptions(Math.Max(1, effectiveOptions.MaxPackageConcurrency))
            {
                FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
                SingleWriter = false,
                SingleReader = true
            });

            var producer = ProduceWorkAsync(workItems, scanChannel.Writer, state, progress, cancellationToken);
            var workers = Enumerable.Range(0, effectiveOptions.MaxPackageConcurrency)
                .Select(workerId => RunWorkerAsync(workerId + 1, scanChannel.Reader, persistChannel.Writer, state, cancellationToken))
                .ToArray();
            var writer = RunWriterAsync(persistChannel.Reader, state, cancellationToken);

            await producer;
            await Task.WhenAll(workers);
            persistChannel.Writer.TryComplete();
            await writer;

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
                await reportLoop;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task ProduceWorkAsync(
        IReadOnlyList<PackageWorkItem> workItems,
        System.Threading.Channels.ChannelWriter<PackageWorkItem> writer,
        IndexingRunState state,
        IProgress<IndexingProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            foreach (var workItem in workItems)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(workItem.PackagePath);

                if (!fileInfo.Exists)
                {
                    state.MarkFailure(workItem.PackagePath, "Package file no longer exists.");
                    continue;
                }

                if (!await indexStore.NeedsRescanAsync(workItem.Source.Id, workItem.PackagePath, fileInfo.Length, fileInfo.LastWriteTimeUtc, cancellationToken))
                {
                    state.MarkSkipped(workItem.PackagePath);
                    progress?.Report(state.CreateSnapshot("skip", $"Skipped unchanged package {Path.GetFileName(workItem.PackagePath)}"));
                    continue;
                }

                state.MarkQueued(workItem.PackagePath);
                await writer.WriteAsync(workItem with { FileSize = fileInfo.Length, LastWriteUtc = fileInfo.LastWriteTimeUtc }, cancellationToken);
            }
        }
        finally
        {
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
        await foreach (var workItem in reader.ReadAllAsync(cancellationToken))
        {
            state.WorkerStarted(workerId, workItem.PackagePath);
            try
            {
                state.UpdatePackageProgress(workItem.PackagePath, new PackageScanProgress("opening package", 0, 0, 0, TimeSpan.Zero));
                var progress = new Progress<PackageScanProgress>(value => state.UpdatePackageProgress(workItem.PackagePath, value));
                var packageScan = await resourceCatalogService.ScanPackageAsync(workItem.Source, workItem.PackagePath, progress, cancellationToken);

                state.UpdatePackageProgress(workItem.PackagePath, new PackageScanProgress(
                    "building asset summaries",
                    packageScan.Resources.Count,
                    packageScan.Resources.Count,
                    0,
                    state.GetElapsed(workItem.PackagePath)));

                var assets = assetGraphBuilder.BuildAssetSummaries(packageScan);
                await persistWriter.WriteAsync(new PersistWorkItem(workItem, packageScan, assets), cancellationToken);

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

    private async Task RunWriterAsync(
        System.Threading.Channels.ChannelReader<PersistWorkItem> reader,
        IndexingRunState state,
        CancellationToken cancellationToken)
    {
        await foreach (var item in reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                state.UpdatePackageProgress(item.WorkItem.PackagePath, new PackageScanProgress(
                    "batching DB writes",
                    item.PackageScan.Resources.Count,
                    item.PackageScan.Resources.Count,
                    0,
                    state.GetElapsed(item.WorkItem.PackagePath)));

                await indexStore.ReplacePackageAsync(item.PackageScan, item.Assets, cancellationToken);

                state.UpdatePackageProgress(item.WorkItem.PackagePath, new PackageScanProgress(
                    "finalizing package",
                    item.PackageScan.Resources.Count,
                    item.PackageScan.Resources.Count,
                    item.PackageScan.Resources.Count,
                    state.GetElapsed(item.WorkItem.PackagePath)));

                state.MarkCompleted(item.WorkItem.PackagePath, item.PackageScan.Resources.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                state.MarkFailure(item.WorkItem.PackagePath, ex.Message);
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
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            progress.Report(state.CreateSnapshot("indexing", state.DrainLatestMessage()));
        }
    }

    private static PackageWorkItem? CreateWorkItem(string packagePath, IReadOnlyList<DataSourceDefinition> sources)
    {
        var source = sources
            .Where(source => packagePath.StartsWith(source.RootPath, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(static source => source.RootPath.Length)
            .FirstOrDefault();

        return source is null
            ? null
            : new PackageWorkItem(source, packagePath, 0, DateTimeOffset.MinValue);
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
        private readonly int totalPackages;
        private string latestMessage = string.Empty;
        private int packagesProcessed;
        private int packagesSkipped;
        private int packagesFailed;
        private int resourcesCompleted;

        public IndexingRunState(int totalPackages, IndexingRunOptions options)
        {
            this.totalPackages = totalPackages;
            this.options = options;
            workerSlots = Enumerable.Range(1, options.MaxPackageConcurrency)
                .ToDictionary(static workerId => workerId, static workerId => new WorkerSlotState(workerId));
        }

        public void MarkQueued(string packagePath)
        {
            lock (gate)
            {
                packages[packagePath] = new PackageState(packagePath);
                RecordActivityUnsafe("queue", $"Queued {Path.GetFileName(packagePath)}");
            }
        }

        public void WorkerStarted(int workerId, string packagePath)
        {
            lock (gate)
            {
                if (!packages.TryGetValue(packagePath, out var state))
                {
                    state = new PackageState(packagePath);
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

        public void UpdatePackageProgress(string packagePath, PackageScanProgress progress)
        {
            lock (gate)
            {
                if (!packages.TryGetValue(packagePath, out var state))
                {
                    state = new PackageState(packagePath);
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
                    state = new PackageState(packagePath);
                    packages[packagePath] = state;
                }

                state.Complete(resourceCount);
                var workerSlot = workerSlots.Values.FirstOrDefault(slot => string.Equals(slot.PackagePath, packagePath, StringComparison.OrdinalIgnoreCase));
                workerSlot?.Complete(state);
                packagesProcessed++;
                resourcesCompleted += resourceCount;
                RecordActivityUnsafe("complete", $"Indexed {Path.GetFileName(packagePath)} in {state.Elapsed:hh\\:mm\\:ss} ({resourceCount:N0} resources)");
            }
        }

        public void MarkSkipped(string packagePath)
        {
            lock (gate)
            {
                var state = new PackageState(packagePath);
                state.Skip();
                packages[packagePath] = state;
                packagesProcessed++;
                packagesSkipped++;
                RecordActivityUnsafe("skip", $"Skipped unchanged package {Path.GetFileName(packagePath)}");
            }
        }

        public void MarkFailure(string packagePath, string reason, int? workerId = null)
        {
            lock (gate)
            {
                if (!packages.TryGetValue(packagePath, out var state))
                {
                    state = new PackageState(packagePath);
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
                var pendingPackageCount = Math.Max(0, totalPackages - packagesProcessed - packages.Values.Count(static package => package.HasStarted && !package.IsTerminal));
                var elapsed = stopwatch.Elapsed;
                var overallThroughput = elapsed.TotalSeconds <= 0 ? 0 : resourcesProcessed / elapsed.TotalSeconds;
                var effectiveMessage = string.IsNullOrWhiteSpace(message) ? latestMessage : message;

                return new IndexingProgress(
                    stage,
                    packagesProcessed,
                    totalPackages,
                    packagesProcessed - packagesSkipped - packagesFailed,
                    packagesSkipped,
                    packagesFailed,
                    resourcesProcessed,
                    resourcesCompleted,
                    effectiveMessage,
                    ActiveWorkerCount: workerSlots.Values.Count(static slot => slot.Status == WorkerSlotStatus.Active),
                    PendingPackageCount: pendingPackageCount,
                    ConfiguredWorkerCount: options.MaxPackageConcurrency,
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
                var phaseBreakdown = completedPackages
                    .SelectMany(static package => package.PhaseTimings)
                    .GroupBy(static phase => phase.Stage, StringComparer.OrdinalIgnoreCase)
                    .Select(group => $"{group.Key}: {TimeSpan.FromMilliseconds(group.Sum(static phase => phase.Elapsed.TotalMilliseconds)):hh\\:mm\\:ss}")
                    .OrderByDescending(static line => line)
                    .ToArray();
                var averagePackageSeconds = completedPackages.Where(static package => package.IsCompleted).Select(static package => package.Elapsed.TotalSeconds).DefaultIfEmpty(0d).Average();
                var slowOutliers = completedPackages
                    .Where(package => package.IsCompleted && averagePackageSeconds > 0 && package.Elapsed.TotalSeconds >= averagePackageSeconds * 2)
                    .OrderByDescending(static package => package.Elapsed)
                    .Select(package => $"{Path.GetFileName(package.PackagePath)}: {package.Elapsed:hh\\:mm\\:ss}")
                    .ToArray();

                return new IndexingRunSummary(
                    totalElapsed,
                    packagesProcessed,
                    packagesSkipped,
                    packagesFailed,
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

    private sealed class PackageState
    {
        private readonly Stopwatch stopwatch = new();
        private readonly List<PackagePhaseTiming> phaseTimings = [];
        private string currentStage = "queued";
        private TimeSpan stageStartedAt;
        private TimeSpan lastHeartbeatAt;

        public PackageState(string packagePath)
        {
            PackagePath = packagePath;
            PackageName = Path.GetFileName(packagePath);
        }

        public string PackagePath { get; }
        public string PackageName { get; }
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
                stageStartedAt = stopwatch.Elapsed;
                HeartbeatDue = false;
                stageChanged = true;
            }
            else
            {
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
            return $"{PackageName}: {currentStage} {ResourcesProcessed:N0} / {Math.Max(ResourcesDiscovered, ResourcesProcessed):N0} resources, {throughput:N0} res/sec, elapsed {Elapsed:hh\\:mm\\:ss}";
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
                Math.Max(ResourcesProcessed, ResourcesWritten),
                Elapsed,
                IsSkipped,
                IsFailed,
                phaseTimings.ToArray());

        private void CompleteCurrentStage()
        {
            var elapsed = stopwatch.Elapsed - stageStartedAt;
            if (elapsed > TimeSpan.Zero)
            {
                phaseTimings.Add(new PackagePhaseTiming(currentStage, elapsed));
            }
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
            Stage = packageState.CurrentStage;
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
