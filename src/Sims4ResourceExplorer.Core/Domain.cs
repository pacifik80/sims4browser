using System.Collections.ObjectModel;

namespace Sims4ResourceExplorer.Core;

public enum SourceKind
{
    Game,
    Dlc,
    Mods
}

public enum BrowserMode
{
    RawResources,
    LogicalAssets
}

public enum AssetKind
{
    BuildBuy,
    Cas
}

public enum PreviewKind
{
    Metadata,
    Text,
    Hex,
    Texture,
    Scene,
    Audio,
    Unsupported
}

public sealed record DataSourceDefinition(Guid Id, string DisplayName, string RootPath, SourceKind Kind, bool IsEnabled = true);

public sealed record ResourceKeyRecord(uint Type, uint Group, ulong FullInstance, string TypeName)
{
    public string FullTgi => $"{Type:X8}:{Group:X8}:{FullInstance:X16}";
}

public sealed record ResourceMetadata(
    Guid Id,
    Guid DataSourceId,
    SourceKind SourceKind,
    string PackagePath,
    ResourceKeyRecord Key,
    string? Name,
    long? CompressedSize,
    long? UncompressedSize,
    bool? IsCompressed,
    PreviewKind PreviewKind,
    bool IsPreviewable,
    bool IsExportCapable,
    string AssetLinkageSummary,
    string Diagnostics);

public sealed record DiscoveredPackage(
    DataSourceDefinition Source,
    string PackagePath,
    long FileSize,
    DateTimeOffset LastWriteTimeUtc);

public sealed record PackageFingerprint(
    Guid DataSourceId,
    string PackagePath,
    long FileSize,
    DateTimeOffset LastWriteTimeUtc);

public sealed record PackageScanResult(
    Guid DataSourceId,
    SourceKind SourceKind,
    string PackagePath,
    long FileSize,
    DateTimeOffset LastWriteTimeUtc,
    IReadOnlyList<ResourceMetadata> Resources,
    IReadOnlyList<string> Diagnostics);

public sealed record AssetSummary(
    Guid Id,
    Guid DataSourceId,
    SourceKind SourceKind,
    AssetKind AssetKind,
    string DisplayName,
    string? Category,
    string PackagePath,
    ResourceKeyRecord RootKey,
    string? ThumbnailTgi,
    int VariantCount,
    int LinkedResourceCount,
    string Diagnostics);

public sealed record AssetGraph(
    AssetSummary Summary,
    IReadOnlyList<ResourceMetadata> LinkedResources,
    IReadOnlyList<string> Diagnostics,
    BuildBuyAssetGraph? BuildBuyGraph = null);

public sealed record ResourceQuery(
    string SearchText,
    BrowserMode BrowserMode,
    bool BuildBuyOnly = false,
    bool CasOnly = false,
    bool AudioOnly = false,
    bool PreviewableOnly = false,
    bool ExportCapableOnly = false,
    Guid? DataSourceId = null,
    string? PackagePath = null,
    int Limit = 500);

public sealed record LogicalAssetQuery(
    string SearchText,
    bool BuildBuyOnly = false,
    bool CasOnly = false,
    Guid? DataSourceId = null,
    int Limit = 500);

public sealed record IndexingRunOptions(
    int MaxPackageConcurrency,
    int PackageQueueCapacity,
    int SqliteBatchSize,
    TimeSpan ProgressUpdateInterval,
    TimeSpan HeartbeatInterval,
    int MaxRecentEvents = 200)
{
    public static IndexingRunOptions CreateDefault() =>
        new(
            MaxPackageConcurrency: GetDefaultWorkerCount(),
            PackageQueueCapacity: Math.Clamp(GetDefaultWorkerCount() * 2, 2, 32),
            SqliteBatchSize: 200,
            ProgressUpdateInterval: TimeSpan.FromMilliseconds(400),
            HeartbeatInterval: TimeSpan.FromSeconds(5));

    public static int GetMachineWorkerLimit() => Math.Max(1, Environment.ProcessorCount);

    public static int ClampWorkerCount(int requested) =>
        Math.Clamp(requested, 1, GetMachineWorkerLimit());

    public static int GetDefaultWorkerCount() =>
        Math.Clamp(Environment.ProcessorCount / 2, 1, GetMachineWorkerLimit());

    public IndexingRunOptions WithWorkerCount(int requestedWorkerCount)
    {
        var workerCount = ClampWorkerCount(requestedWorkerCount);
        return this with
        {
            MaxPackageConcurrency = workerCount,
            PackageQueueCapacity = Math.Clamp(workerCount * 2, 2, 32)
        };
    }
}

public sealed record PackageScanProgress(
    string Stage,
    int ResourcesDiscovered,
    int ResourcesProcessed,
    int ResourcesWritten,
    TimeSpan Elapsed,
    string? Message = null);

public sealed record ActivePackageProgress(
    string PackagePath,
    string PackageName,
    string Stage,
    int ResourcesDiscovered,
    int ResourcesProcessed,
    int ResourcesWritten,
    TimeSpan Elapsed,
    double Throughput,
    bool IsHeartbeat)
{
    public string ProcessedText => $"Processed: {ResourcesProcessed:N0} / {Math.Max(ResourcesDiscovered, ResourcesProcessed):N0}";
    public string WrittenText => $"Written: {ResourcesWritten:N0}";
    public string ThroughputText => $"Throughput: {Throughput:N0} res/sec";
    public string ElapsedText => $"Elapsed: {Elapsed:hh\\:mm\\:ss}";
}

public enum WorkerSlotStatus
{
    Waiting,
    Idle,
    Active,
    Failed
}

public sealed record WorkerSlotProgress(
    int WorkerId,
    WorkerSlotStatus Status,
    string? PackagePath,
    string? PackageName,
    string Stage,
    int ResourcesDiscovered,
    int ResourcesProcessed,
    int ResourcesWritten,
    TimeSpan Elapsed,
    double Throughput,
    string? FailureReason = null);

public sealed record IndexingActivityEvent(DateTimeOffset Timestamp, string Kind, string Message);

public sealed record PackagePhaseTiming(string Stage, TimeSpan Elapsed);

public sealed record PackageFailureInfo(string PackagePath, string Reason);

public sealed record PackageRunSummary(
    string PackagePath,
    int ResourceCount,
    TimeSpan Elapsed,
    bool Skipped,
    bool Failed,
    IReadOnlyList<PackagePhaseTiming> PhaseTimings);

public sealed record IndexingRunSummary(
    TimeSpan TotalElapsed,
    int PackagesDiscovered,
    int PackagesQueued,
    int PackagesProcessed,
    int PackagesSkipped,
    int PackagesFailed,
    int ResourcesProcessed,
    double AverageThroughput,
    IReadOnlyList<PackageRunSummary> SlowestPackages,
    IReadOnlyList<PackageFailureInfo> Failures,
    IReadOnlyList<string> PhaseBreakdown,
    IReadOnlyList<string> MuchSlowerThanAverage);

public sealed record IndexingProgress(
    string Stage,
    int PackagesProcessed,
    int PackagesTotal,
    int PackagesCompleted,
    int PackagesSkipped,
    int PackagesFailed,
    int ResourcesProcessed,
    int CompletedResourcesProcessed,
    string Message,
    int ActiveWorkerCount = 0,
    int PendingPackageCount = 0,
    int ConfiguredWorkerCount = 0,
    TimeSpan Elapsed = default,
    double OverallThroughput = 0,
    IReadOnlyList<WorkerSlotProgress>? WorkerSlots = null,
    IReadOnlyList<ActivePackageProgress>? ActivePackages = null,
    IReadOnlyList<IndexingActivityEvent>? RecentEvents = null,
    IndexingRunSummary? Summary = null);

public abstract record PreviewContent;

public sealed record MetadataPreviewContent(ResourceMetadata Resource) : PreviewContent;

public sealed record TextPreviewContent(ResourceMetadata Resource, string Text) : PreviewContent;

public sealed record HexPreviewContent(ResourceMetadata Resource, string HexDump) : PreviewContent;

public sealed record TexturePreviewContent(
    ResourceMetadata Resource,
    byte[] PngBytes,
    int? Width,
    int? Height,
    string? PixelFormat,
    int? MipLevels,
    string Diagnostics) : PreviewContent;

public sealed record ScenePreviewContent(
    ResourceMetadata Resource,
    CanonicalScene? Scene,
    string Diagnostics) : PreviewContent;

public sealed record AudioPreviewContent(
    ResourceMetadata Resource,
    byte[]? WavBytes,
    string Diagnostics,
    bool CanPlay) : PreviewContent;

public sealed record UnsupportedPreviewContent(ResourceMetadata Resource, string Reason) : PreviewContent;

public sealed record PreviewResult(PreviewKind Kind, PreviewContent Content);

public sealed record TextureDecodeResult(bool Success, byte[]? PngBytes, int? Width, int? Height, string? PixelFormat, int? MipLevels, string Diagnostics);

public sealed record CanonicalScene(
    string Name,
    IReadOnlyList<CanonicalMesh> Meshes,
    IReadOnlyList<CanonicalMaterial> Materials,
    IReadOnlyList<CanonicalBone> Bones,
    Bounds3D Bounds);

public sealed record CanonicalMesh(
    string Name,
    IReadOnlyList<float> Positions,
    IReadOnlyList<float> Normals,
    IReadOnlyList<float> Tangents,
    IReadOnlyList<float> Uvs,
    IReadOnlyList<int> Indices,
    int MaterialIndex,
    IReadOnlyList<VertexWeight> SkinWeights);

public sealed record CanonicalMaterial(
    string Name,
    IReadOnlyList<CanonicalTexture> Textures,
    string? ShaderName = null,
    bool IsTransparent = false,
    string? AlphaMode = null,
    string? Approximation = null);

public sealed record CanonicalTexture(
    string Slot,
    string FileName,
    byte[] PngBytes,
    ResourceKeyRecord? SourceKey = null,
    string? SourcePackagePath = null);

public sealed record CanonicalBone(string Name, string? ParentName);

public sealed record VertexWeight(int VertexIndex, int BoneIndex, float Weight);

public sealed record Bounds3D(float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ);

public sealed record SceneBuildResult(bool Success, CanonicalScene? Scene, IReadOnlyList<string> Diagnostics);

public sealed record AudioDecodeResult(bool Success, byte[]? WavBytes, string Diagnostics);

public sealed record RawExportRequest(ResourceMetadata Resource, string OutputDirectory);

public sealed record SceneExportRequest(
    string AssetSlug,
    CanonicalScene Scene,
    string OutputDirectory,
    IReadOnlyList<ResourceMetadata> SourceResources,
    IReadOnlyList<CanonicalTexture> Textures,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<MaterialManifestEntry>? MaterialManifest = null);

public sealed record BuildBuyAssetGraph(
    ResourceMetadata ModelResource,
    IReadOnlyList<ResourceMetadata> ModelLodResources,
    IReadOnlyList<ResourceMetadata> TextureResources,
    IReadOnlyList<ResourceKeyRecord> MissingTextureKeys,
    IReadOnlyList<MaterialManifestEntry> Materials,
    IReadOnlyList<string> Diagnostics,
    bool IsSupported,
    string SupportedSubset);

public sealed record MaterialManifestEntry(
    string MaterialName,
    string? ShaderName,
    bool IsTransparent,
    string? AlphaMode,
    string? Approximation,
    IReadOnlyList<MaterialTextureEntry> Textures);

public sealed record MaterialTextureEntry(
    string Slot,
    string FileName,
    ResourceKeyRecord? SourceKey,
    string? SourcePackagePath);

public sealed record ExportedFileResult(bool Success, string? OutputPath, string Message);

public interface IPackageScanner
{
    IAsyncEnumerable<DiscoveredPackage> DiscoverPackagesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken);
}

public interface IResourceCatalogService
{
    Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken);
    Task<ResourceMetadata> EnrichResourceAsync(ResourceMetadata resource, CancellationToken cancellationToken);
    Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken);
    Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken);
    Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken);
}

public interface IAssetGraphBuilder
{
    IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan);
    AssetGraph BuildAssetGraph(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources);
}

public interface IPreviewService
{
    Task<PreviewResult> CreatePreviewAsync(ResourceMetadata resource, CancellationToken cancellationToken);
}

public interface ITextureDecodeService
{
    Task<TextureDecodeResult> DecodeAsync(ResourceMetadata resource, CancellationToken cancellationToken);
}

public interface ISceneBuildService
{
    Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken);
}

public interface IFbxExportService
{
    Task<ExportedFileResult> ExportAsync(SceneExportRequest request, CancellationToken cancellationToken);
}

public interface IAudioDecodeService
{
    Task<AudioDecodeResult> DecodeAsync(ResourceMetadata resource, CancellationToken cancellationToken);
}

public interface IAudioPlayer : IAsyncDisposable
{
    Task PlayAsync(byte[] wavBytes, CancellationToken cancellationToken);
    Task StopAsync();
}

public interface IRawExportService
{
    Task<ExportedFileResult> ExportAsync(RawExportRequest request, CancellationToken cancellationToken);
}

public interface IIndexStore
{
    Task InitializeAsync(CancellationToken cancellationToken);
    Task UpsertDataSourcesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, PackageFingerprint>> LoadPackageFingerprintsAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken);
    Task<bool> NeedsRescanAsync(Guid dataSourceId, string packagePath, long fileSize, DateTimeOffset lastWriteTimeUtc, CancellationToken cancellationToken);
    Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken);
    Task<IIndexWriteSession> OpenWriteSessionAsync(CancellationToken cancellationToken);
    Task<ResourceMetadata> PersistResourceEnrichmentAsync(ResourceMetadata resource, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceMetadata>> QueryResourcesAsync(ResourceQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetSummary>> QueryAssetsAsync(LogicalAssetQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken);
    Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken);
}

public interface IIndexWriteSession : IAsyncDisposable
{
    Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken);
}

public interface IResourceMetadataEnrichmentService
{
    Task<ResourceMetadata> EnrichAsync(ResourceMetadata resource, CancellationToken cancellationToken);
}

public interface ICacheService
{
    string AppRoot { get; }
    string CacheRoot { get; }
    string ExportRoot { get; }
    string DatabasePath { get; }
    void EnsureCreated();
}

public sealed class ObservableLog : ObservableCollection<string>
{
    public void Append(string message) => Add($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");

    public void AppendRange(IEnumerable<string> messages)
    {
        foreach (var message in messages)
        {
            Append(message);
        }
    }
}
