using System.Collections.ObjectModel;

namespace Sims4ResourceExplorer.Core;

public enum SourceKind
{
    Game,
    Dlc,
    Mods
}

public enum AssetKind
{
    BuildBuy,
    Cas
}

public enum PreviewSurfaceMode
{
    Diagnostics,
    Image,
    Scene
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
    string Diagnostics)
{
    public bool HasKnownCompression => IsCompressed.HasValue;
    public bool IsLinkedToAsset => !string.IsNullOrWhiteSpace(AssetLinkageSummary);
}

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

public sealed record AssetCapabilitySnapshot(
    bool HasSceneRoot,
    bool HasExactGeometryCandidate,
    bool HasMaterialReferences,
    bool HasTextureReferences,
    bool HasThumbnail = false,
    bool HasVariants = false,
    bool HasIdentityMetadata = false,
    bool HasRigReference = false,
    bool HasGeometryReference = false,
    bool HasMaterialResourceCandidate = false,
    bool HasTextureResourceCandidate = false,
    bool IsPackageLocalGraph = false,
    bool HasDiagnostics = false)
{
    public static AssetCapabilitySnapshot Empty { get; } =
        new(true, false, false, false);
}

public sealed record AssetCapabilityFilter(
    bool RequireSceneRoot = false,
    bool RequireExactGeometryCandidate = false,
    bool RequireMaterialReferences = false,
    bool RequireTextureReferences = false,
    bool RequireIdentityMetadata = false,
    bool RequireRigReference = false,
    bool RequireGeometryReference = false,
    bool RequireMaterialResourceCandidate = false,
    bool RequireTextureResourceCandidate = false,
    bool RequirePackageLocalGraph = false,
    bool RequireDiagnostics = false);

public sealed record AssetFacetOptions(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> RootTypeNames,
    IReadOnlyList<string> IdentityTypes,
    IReadOnlyList<string> PrimaryGeometryTypes,
    IReadOnlyList<string> ThumbnailTypeNames);

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
    string Diagnostics,
    AssetCapabilitySnapshot? Capabilities = null,
    string? PackageName = null,
    string? RootTypeName = null,
    string? ThumbnailTypeName = null,
    string? PrimaryGeometryType = null,
    string? IdentityType = null,
    string? CategoryNormalized = null)
{
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailTgi);
    public bool HasVariants => VariantCount > 1;
    public AssetCapabilitySnapshot CapabilitySnapshot => Capabilities ?? AssetCapabilitySnapshot.Empty;
    public string SupportLabel => AssetDerivedState.BuildSupportLabel(CapabilitySnapshot);
    public string SupportNotes => AssetDerivedState.BuildSupportNotes(this);
}

public static class StableEntityIds
{
    public static Guid ForResource(Guid dataSourceId, string packagePath, ResourceKeyRecord key) =>
        Create("resource", dataSourceId.ToString("D"), packagePath, key.FullTgi);

    public static Guid ForAsset(Guid dataSourceId, AssetKind assetKind, string packagePath, ResourceKeyRecord rootKey) =>
        Create("asset", dataSourceId.ToString("D"), assetKind.ToString(), packagePath, rootKey.FullTgi);

    private static Guid Create(params string[] parts)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        var input = string.Join("|", parts);
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = md5.ComputeHash(bytes);
        return new Guid(hash);
    }
}

public sealed record AssetGraph(
    AssetSummary Summary,
    IReadOnlyList<ResourceMetadata> LinkedResources,
    IReadOnlyList<string> Diagnostics,
    BuildBuyAssetGraph? BuildBuyGraph = null,
    CasAssetGraph? CasGraph = null);

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
    bool DiscoveryCompleted = false,
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
    string Diagnostics,
    SceneBuildStatus Status = SceneBuildStatus.Unsupported) : PreviewContent;

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
    string? Approximation = null,
    CanonicalMaterialSourceKind SourceKind = CanonicalMaterialSourceKind.Unknown);

public sealed record CanonicalTexture(
    string Slot,
    string FileName,
    byte[] PngBytes,
    ResourceKeyRecord? SourceKey = null,
    string? SourcePackagePath = null,
    CanonicalTextureSemantic Semantic = CanonicalTextureSemantic.Unknown,
    CanonicalTextureSourceKind SourceKind = CanonicalTextureSourceKind.Unknown);

public enum CanonicalMaterialSourceKind
{
    Unknown,
    ExplicitMatd,
    MaterialSet,
    FallbackCandidate,
    ApproximateCas,
    Unsupported
}

public enum CanonicalTextureSemantic
{
    Unknown,
    BaseColor,
    Normal,
    Specular,
    Gloss,
    Opacity,
    Emissive,
    Overlay
}

public enum CanonicalTextureSourceKind
{
    Unknown,
    ExplicitLocal,
    ExplicitCompanion,
    ExplicitIndexed,
    FallbackSameInstanceLocal,
    FallbackSameInstanceIndexed
}

public sealed record CanonicalBone(
    string Name,
    string? ParentName,
    float[]? BindPoseMatrix = null,
    float[]? InverseBindPoseMatrix = null,
    uint? NameHash = null);

public sealed record VertexWeight(int VertexIndex, int BoneIndex, float Weight);

public sealed record Bounds3D(float MinX, float MinY, float MinZ, float MaxX, float MaxY, float MaxZ);

public enum SceneBuildStatus
{
    Unsupported,
    Partial,
    SceneReady
}

public sealed record SceneBuildResult(
    bool Success,
    CanonicalScene? Scene,
    IReadOnlyList<string> Diagnostics,
    SceneBuildStatus Status = SceneBuildStatus.Unsupported);

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
    IReadOnlyList<ResourceMetadata> IdentityResources,
    IReadOnlyList<ResourceMetadata> ModelLodResources,
    IReadOnlyList<ResourceMetadata> MaterialResources,
    IReadOnlyList<ResourceMetadata> TextureResources,
    IReadOnlyList<ResourceKeyRecord> MissingTextureKeys,
    IReadOnlyList<MaterialManifestEntry> Materials,
    IReadOnlyList<string> Diagnostics,
    bool IsSupported,
    string SupportedSubset);

public sealed record CasAssetGraph(
    ResourceMetadata CasPartResource,
    ResourceMetadata? GeometryResource,
    ResourceMetadata? RigResource,
    IReadOnlyList<ResourceMetadata> IdentityResources,
    IReadOnlyList<ResourceMetadata> GeometryResources,
    IReadOnlyList<ResourceMetadata> RigResources,
    IReadOnlyList<ResourceMetadata> MaterialResources,
    IReadOnlyList<ResourceMetadata> TextureResources,
    IReadOnlyList<MaterialManifestEntry> Materials,
    string? Category,
    string? SwatchSummary,
    string? SelectedLodLabel,
    bool IsSupported,
    string SupportedSubset);

public sealed record MaterialManifestEntry(
    string MaterialName,
    string? ShaderName,
    bool IsTransparent,
    string? AlphaMode,
    string? Approximation,
    IReadOnlyList<MaterialTextureEntry> Textures,
    CanonicalMaterialSourceKind SourceKind = CanonicalMaterialSourceKind.Unknown);

public sealed record MaterialTextureEntry(
    string Slot,
    string FileName,
    ResourceKeyRecord? SourceKey,
    string? SourcePackagePath,
    CanonicalTextureSemantic Semantic = CanonicalTextureSemantic.Unknown,
    CanonicalTextureSourceKind SourceKind = CanonicalTextureSourceKind.Unknown);

public sealed record ExportedFileResult(bool Success, string? OutputPath, string Message);

public sealed record IndexedPackageRecord(
    Guid DataSourceId,
    SourceKind SourceKind,
    string PackagePath,
    long FileSize,
    DateTimeOffset LastWriteTimeUtc,
    int AssetCount);

public sealed record PreviewPresentationState(
    PreviewSurfaceMode SurfaceMode,
    string SurfaceTitle,
    bool CanResetView,
    bool HasPrimaryDiagnostics)
{
    public static PreviewPresentationState FromPreviewContent(PreviewContent content) => content switch
    {
        ScenePreviewContent { Scene: not null, Status: SceneBuildStatus.Partial } => new(PreviewSurfaceMode.Scene, "3D Preview (Partial)", true, false),
        ScenePreviewContent { Scene: not null } => new(PreviewSurfaceMode.Scene, "3D Preview", true, false),
        TexturePreviewContent => new(PreviewSurfaceMode.Image, "Image Preview", false, false),
        _ => new(PreviewSurfaceMode.Diagnostics, "Diagnostics", false, true)
    };
}

public static class PreviewInteractionPolicy
{
    public static bool CanExportSelectedAsset(AssetSummary? selectedAsset, AssetGraph? selectedAssetGraph, ResourceMetadata? sceneRoot, CanonicalScene? scene) =>
        selectedAsset is not null &&
        selectedAssetGraph is not null &&
        sceneRoot is not null &&
        scene is not null;
}

public static class AssetDerivedState
{
    public static string BuildSupportLabel(AssetCapabilitySnapshot facts)
    {
        if (facts.HasExactGeometryCandidate && facts.HasTextureReferences)
        {
            return "Geometry+Textures";
        }

        if (facts.HasExactGeometryCandidate)
        {
            return "Geometry";
        }

        if (facts.HasSceneRoot)
        {
            return "Metadata";
        }

        return "Missing Root";
    }

    public static string BuildSupportNotes(AssetSummary asset)
    {
        var facts = asset.CapabilitySnapshot;
        if (!facts.HasSceneRoot)
        {
            return "The cached asset row does not currently resolve a scene root.";
        }

        if (facts.HasExactGeometryCandidate && facts.HasTextureReferences)
        {
            return "Exact-asset geometry and texture candidates are present in cache.";
        }

        if (facts.HasExactGeometryCandidate)
        {
            return "Exact-asset geometry candidates are present in cache, but texture coverage is incomplete.";
        }

        return "The asset is cached for browsing, but no exact geometry candidate is currently indexed.";
    }
}

public static class AssetDetailsFormatter
{
    public static string BuildAssetDetails(AssetSummary asset, AssetGraph graph, ResourceMetadata? sceneRoot, ScenePreviewContent? scenePreview)
    {
        var buildBuyGraph = graph.BuildBuyGraph;
        var casGraph = graph.CasGraph;
        var scene = scenePreview?.Scene;
        var diagnostics = graph.Diagnostics
            .Concat(scenePreview is null ? [] : scenePreview.Diagnostics.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
            .DefaultIfEmpty(asset.Diagnostics);

        if (buildBuyGraph is not null)
        {
            return
                $"""
                Asset: {asset.DisplayName}
                Kind: {asset.AssetKind}
                Category: {asset.Category ?? "(unknown)"}
                Package: {asset.PackagePath}
                Root TGI: {asset.RootKey.FullTgi}
                Support Status: {asset.SupportLabel}
                Support Notes: {asset.SupportNotes}
                Package Name: {asset.PackageName ?? Path.GetFileName(asset.PackagePath)}
                Root Type: {asset.RootTypeName ?? "(unknown)"}
                Thumbnail Type: {asset.ThumbnailTypeName ?? "(none)"}
                Geometry Type: {asset.PrimaryGeometryType ?? "(unknown)"}
                Identity Type: {asset.IdentityType ?? "(unknown)"}
                Has Scene Root: {FormatYesNo(asset.CapabilitySnapshot.HasSceneRoot)}
                Has Exact Geometry Candidate: {FormatYesNo(asset.CapabilitySnapshot.HasExactGeometryCandidate)}
                Has Material References: {FormatYesNo(asset.CapabilitySnapshot.HasMaterialReferences)}
                Has Texture References: {FormatYesNo(asset.CapabilitySnapshot.HasTextureReferences)}
                Has Identity Metadata: {FormatYesNo(asset.CapabilitySnapshot.HasIdentityMetadata)}
                Has Geometry Reference: {FormatYesNo(asset.CapabilitySnapshot.HasGeometryReference)}
                Has Rig Reference: {FormatYesNo(asset.CapabilitySnapshot.HasRigReference)}
                Has Material Resource Candidate: {FormatYesNo(asset.CapabilitySnapshot.HasMaterialResourceCandidate)}
                Has Texture Resource Candidate: {FormatYesNo(asset.CapabilitySnapshot.HasTextureResourceCandidate)}
                Package-Local Graph: {FormatYesNo(asset.CapabilitySnapshot.IsPackageLocalGraph)}
                Has Diagnostics: {FormatYesNo(asset.CapabilitySnapshot.HasDiagnostics)}
                Scene Reconstruction: {BuildSceneReconstructionStatus(sceneRoot, scenePreview)}
                Identity Resources: {buildBuyGraph.IdentityResources.Count}
                Linked Resources: {asset.LinkedResourceCount}
                Thumbnail: {asset.ThumbnailTgi ?? "(none)"}
                Supported Subset: {buildBuyGraph.SupportedSubset}
                Scene Root: {sceneRoot?.Key.FullTgi ?? "(unresolved)"}
                Selected LOD: {ExtractDiagnosticValue(scenePreview?.Diagnostics, "Selected LOD root:") ?? "(not resolved)"}
                Model LOD Candidates: {buildBuyGraph.ModelLodResources.Count}
                Material Candidates: {buildBuyGraph.MaterialResources.Count}
                Texture Candidates: {buildBuyGraph.TextureResources.Count}
                Mesh Count: {scene?.Meshes.Count ?? 0}
                Vertex Count: {(scene?.Meshes.Sum(static mesh => mesh.Positions.Count / 3) ?? 0):N0}
                Index Count: {(scene?.Meshes.Sum(static mesh => mesh.Indices.Count) ?? 0):N0}
                Material Slots: {scene?.Materials.Count ?? 0}
                Texture References: {scene?.Materials.Sum(static material => material.Textures.Count) ?? 0}
                Bone Count: {scene?.Bones.Count ?? 0}
                Bounds: {FormatBounds(scene?.Bounds)}
                Diagnostics:
                {string.Join(Environment.NewLine, diagnostics)}
                """;
        }

        return
            $"""
            Asset: {asset.DisplayName}
            Kind: {asset.AssetKind}
            Category: {casGraph?.Category ?? asset.Category ?? "(unknown)"}
            Package: {asset.PackagePath}
            Root TGI: {asset.RootKey.FullTgi}
            Support Status: {asset.SupportLabel}
            Support Notes: {asset.SupportNotes}
            Package Name: {asset.PackageName ?? Path.GetFileName(asset.PackagePath)}
            Root Type: {asset.RootTypeName ?? "(unknown)"}
            Thumbnail Type: {asset.ThumbnailTypeName ?? "(none)"}
            Geometry Type: {asset.PrimaryGeometryType ?? "(unknown)"}
            Identity Type: {asset.IdentityType ?? "(unknown)"}
            Has Scene Root: {FormatYesNo(asset.CapabilitySnapshot.HasSceneRoot)}
            Has Exact Geometry Candidate: {FormatYesNo(asset.CapabilitySnapshot.HasExactGeometryCandidate)}
            Has Material References: {FormatYesNo(asset.CapabilitySnapshot.HasMaterialReferences)}
            Has Texture References: {FormatYesNo(asset.CapabilitySnapshot.HasTextureReferences)}
            Has Identity Metadata: {FormatYesNo(asset.CapabilitySnapshot.HasIdentityMetadata)}
            Has Geometry Reference: {FormatYesNo(asset.CapabilitySnapshot.HasGeometryReference)}
            Has Rig Reference: {FormatYesNo(asset.CapabilitySnapshot.HasRigReference)}
            Has Material Resource Candidate: {FormatYesNo(asset.CapabilitySnapshot.HasMaterialResourceCandidate)}
            Has Texture Resource Candidate: {FormatYesNo(asset.CapabilitySnapshot.HasTextureResourceCandidate)}
            Package-Local Graph: {FormatYesNo(asset.CapabilitySnapshot.IsPackageLocalGraph)}
            Has Diagnostics: {FormatYesNo(asset.CapabilitySnapshot.HasDiagnostics)}
            Scene Reconstruction: {BuildSceneReconstructionStatus(sceneRoot, scenePreview)}
            Swatch/Variant: {casGraph?.SwatchSummary ?? "(unknown)"}
            Identity Resources: {casGraph?.IdentityResources.Count ?? 0}
            Geometry Candidates: {casGraph?.GeometryResources.Count ?? 0}
            Rig Candidates: {casGraph?.RigResources.Count ?? 0}
            Texture Candidates: {casGraph?.TextureResources.Count ?? 0}
            Supported Subset: {casGraph?.SupportedSubset ?? "(not supported)"}
            Scene Root: {sceneRoot?.Key.FullTgi ?? "(unresolved)"}
            Selected LOD: {casGraph?.SelectedLodLabel ?? "(not resolved)"}
            Mesh Count: {scene?.Meshes.Count ?? 0}
            Vertex Count: {(scene?.Meshes.Sum(static mesh => mesh.Positions.Count / 3) ?? 0):N0}
            Index Count: {(scene?.Meshes.Sum(static mesh => mesh.Indices.Count) ?? 0):N0}
            Material Slots: {scene?.Materials.Count ?? 0}
            Texture References: {scene?.Materials.Sum(static material => material.Textures.Count) ?? 0}
            Bone Count: {scene?.Bones.Count ?? 0}
            Bounds: {FormatBounds(scene?.Bounds)}
            Diagnostics:
            {string.Join(Environment.NewLine, diagnostics)}
            """;
    }

    private static string BuildSceneReconstructionStatus(ResourceMetadata? sceneRoot, ScenePreviewContent? scenePreview)
    {
        if (sceneRoot is null)
        {
            return "Not attempted (no supported scene root resolved)";
        }

        if (scenePreview is null)
        {
            return "Pending";
        }

        return scenePreview.Status switch
        {
            SceneBuildStatus.SceneReady => "SceneReady",
            SceneBuildStatus.Partial => "Partial",
            _ => scenePreview.Scene is not null ? "Succeeded" : "Failed"
        };
    }

    private static string? ExtractDiagnosticValue(string? diagnostics, string prefix)
    {
        if (string.IsNullOrWhiteSpace(diagnostics))
        {
            return null;
        }

        foreach (var line in diagnostics.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                return line[prefix.Length..].Trim();
            }
        }

        return null;
    }

    private static string FormatBounds(Bounds3D? bounds) =>
        bounds is null
            ? "(unavailable)"
            : $"min ({bounds.MinX:0.###}, {bounds.MinY:0.###}, {bounds.MinZ:0.###}) max ({bounds.MaxX:0.###}, {bounds.MaxY:0.###}, {bounds.MaxZ:0.###})";

    private static string FormatYesNo(bool value) => value ? "Yes" : "No";
}

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
    Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken);
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
    Task<WindowedQueryResult<ResourceMetadata>> QueryResourcesAsync(RawResourceBrowserQuery query, CancellationToken cancellationToken);
    Task<WindowedQueryResult<AssetSummary>> QueryAssetsAsync(AssetBrowserQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken);
    Task<AssetFacetOptions> GetAssetFacetOptionsAsync(AssetKind assetKind, CancellationToken cancellationToken);
    Task<IReadOnlyList<IndexedPackageRecord>> GetIndexedPackagesAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetSummary>> GetPackageAssetsAsync(string packagePath, CancellationToken cancellationToken);
    Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken);
    Task UpdatePackageAssetsAsync(Guid dataSourceId, string packagePath, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken);
}

public interface IIndexWriteSession : IAsyncDisposable
{
    Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken);
    Task ReplacePackagesAsync(IReadOnlyList<(PackageScanResult PackageScan, IReadOnlyList<AssetSummary> Assets)> batch, CancellationToken cancellationToken);
    Task FinalizeAsync(CancellationToken cancellationToken);
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
