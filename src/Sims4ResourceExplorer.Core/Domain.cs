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
    Cas,
    Sim,
    General3D
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
    string Diagnostics,
    string? Description = null,
    uint? CatalogSignal0020 = null,
    uint? CatalogSignal002C = null,
    uint? CatalogSignal0030 = null,
    uint? CatalogSignal0034 = null,
    string? SceneRootTgiHint = null)
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
    IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<DiscoveredAssetVariant> AssetVariants { get; init; } = [];
    public IReadOnlyList<SimTemplateFactSummary> SimTemplateFacts { get; init; } = [];
    public IReadOnlyList<SimTemplateBodyPartFact> SimTemplateBodyPartFacts { get; init; } = [];
    public IReadOnlyList<DiscoveredCasPartFact> CasPartFacts { get; init; } = [];
}

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
    IReadOnlyList<string> ThumbnailTypeNames,
    IReadOnlyList<string>? CatalogSignal0020Values = null,
    IReadOnlyList<string>? CatalogSignal002CValues = null,
    IReadOnlyList<string>? CatalogSignal0030Values = null,
    IReadOnlyList<string>? CatalogSignal0034Values = null);

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
    string? CategoryNormalized = null,
    string? Description = null,
    uint? CatalogSignal0020 = null,
    uint? CatalogSignal002C = null,
    uint? CatalogSignal0030 = null,
    uint? CatalogSignal0034 = null,
    string? LogicalRootTgi = null)
{
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailTgi);
    public bool HasVariants => VariantCount > 1;
    public AssetCapabilitySnapshot CapabilitySnapshot => Capabilities ?? AssetCapabilitySnapshot.Empty;
    public string KindLabel => AssetKind switch
    {
        AssetKind.BuildBuy => "Build/Buy",
        AssetKind.Cas => "CAS",
        AssetKind.Sim => "Sim Archetype",
        AssetKind.General3D => "General 3D",
        _ => AssetKind.ToString()
    };
    public string SupportLabel => AssetDerivedState.BuildSupportLabel(this);
    public string SupportNotes => AssetDerivedState.BuildSupportNotes(this);
    public string CanonicalRootTgi => string.IsNullOrWhiteSpace(LogicalRootTgi) ? RootKey.FullTgi : LogicalRootTgi;
}

public sealed record DiscoveredAssetVariant(
    Guid DataSourceId,
    SourceKind SourceKind,
    AssetKind AssetKind,
    string PackagePath,
    ResourceKeyRecord RootKey,
    int VariantIndex,
    string VariantKind,
    string DisplayLabel,
    string? SwatchHex = null,
    string? ThumbnailTgi = null,
    string Diagnostics = "");

public sealed record DiscoveredCasPartFact(
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

public sealed record AssetVariantSummary(
    Guid AssetId,
    Guid DataSourceId,
    SourceKind SourceKind,
    AssetKind AssetKind,
    string PackagePath,
    string RootTgi,
    int VariantIndex,
    string VariantKind,
    string DisplayLabel,
    string? SwatchHex = null,
    string? ThumbnailTgi = null,
    string Diagnostics = "");

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
    CasAssetGraph? CasGraph = null,
    SimAssetGraph? SimGraph = null,
    General3DAssetGraph? General3DGraph = null);

public sealed record IndexingRunOptions(
    int MaxPackageConcurrency,
    int PackageQueueCapacity,
    int SqliteBatchSize,
    TimeSpan ProgressUpdateInterval,
    TimeSpan HeartbeatInterval,
    int MaxRecentEvents = 200,
    long PackageByteCacheBudgetBytes = 8L * 1024 * 1024 * 1024)
{
    public const long DefaultPackageByteCacheBudgetBytes = 8L * 1024 * 1024 * 1024;

    public static IndexingRunOptions CreateDefault() =>
        new(
            MaxPackageConcurrency: GetDefaultWorkerCount(),
            PackageQueueCapacity: Math.Clamp(GetDefaultWorkerCount() * 4, 4, 256),
            PackageByteCacheBudgetBytes: DefaultPackageByteCacheBudgetBytes,
            SqliteBatchSize: 20000,
            ProgressUpdateInterval: TimeSpan.FromMilliseconds(400),
            HeartbeatInterval: TimeSpan.FromSeconds(5));

    public static int GetMachineWorkerLimit() => Math.Max(1, Environment.ProcessorCount * 2);

    public static int ClampWorkerCount(int requested) =>
        Math.Clamp(requested, 1, GetMachineWorkerLimit());

    public static int GetDefaultWorkerCount() =>
        Math.Clamp(Environment.ProcessorCount, 1, GetMachineWorkerLimit());

    public IndexingRunOptions WithWorkerCount(int requestedWorkerCount)
    {
        var workerCount = ClampWorkerCount(requestedWorkerCount);
        return this with
        {
            MaxPackageConcurrency = workerCount,
            PackageQueueCapacity = Math.Clamp(workerCount * 4, 4, 256)
        };
    }

    public IndexingRunOptions WithPackageByteCacheBudgetBytes(long requestedBudgetBytes) =>
        this with
        {
            PackageByteCacheBudgetBytes = Math.Max(0, requestedBudgetBytes)
        };
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
    long FileSize,
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
    long PackageBytesDiscovered,
    long PackageBytesProcessed,
    int ResourcesProcessed,
    double AverageThroughput,
    IReadOnlyList<PackageRunSummary> SlowestPackages,
    IReadOnlyList<PackageFailureInfo> Failures,
    IReadOnlyList<string> PhaseBreakdown,
    IReadOnlyList<string> MuchSlowerThanAverage);

public sealed record IndexWriteMetrics(
    TimeSpan DropIndexesElapsed,
    TimeSpan DeletePackageRowsElapsed,
    TimeSpan InsertResourcesElapsed,
    TimeSpan InsertAssetsElapsed,
    TimeSpan FtsElapsed,
    TimeSpan RebuildIndexesElapsed,
    TimeSpan CommitElapsed,
    int ResourceRowCount,
    int AssetRowCount,
    int PackageCount);

public sealed record IndexWriteStageProgress(string Stage, string Message);

public sealed record IndexingProgress(
    string Stage,
    int PackagesProcessed,
    int PackagesTotal,
    int PackagesCompleted,
    int PackagesSkipped,
    int PackagesFailed,
    long PackageBytesProcessed,
    long PackageBytesTotal,
    long CompletedPackageBytesProcessed,
    int ResourcesProcessed,
    int CompletedResourcesProcessed,
    string Message,
    int ActiveWorkerCount = 0,
    int WaitingWorkerCount = 0,
    int IdleWorkerCount = 0,
    int FailedWorkerCount = 0,
    int PendingPackageCount = 0,
    int PendingPersistCount = 0,
    int ActiveWriterBatchCount = 0,
    bool WriterBusy = false,
    double WriterBusyPercent = 0,
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

public enum SimAssemblyBasisKind
{
    None,
    BodyOnly,
    SharedRigResource,
    SharedRigInstance,
    CanonicalBoneFallback
}

public sealed record SimAssemblyPlanSummary(
    SimAssemblyBasisKind BasisKind,
    bool IncludesHeadShell,
    string BasisLabel,
    string Notes);

public sealed record SimAssemblyInputSummary(
    string Label,
    string StatusLabel,
    bool IsAccepted,
    string Notes);

public enum SimAssemblyStageState
{
    Resolved,
    Approximate,
    Pending,
    Unavailable
}

public sealed record SimAssemblyStageSummary(
    string Label,
    int Order,
    SimAssemblyStageState State,
    string Notes);

public sealed record SimAssemblySeamInputSummary(
    string Label,
    string StatusLabel,
    string SourceLabel,
    string SourceResourceTgi,
    string SourcePackagePath,
    int MeshStartIndex,
    int MeshCount,
    int MaterialStartIndex,
    int MaterialCount,
    int BoneCount,
    int RebasedWeightCount,
    int AddedBoneCount,
    string Notes);

public sealed record SimAssemblyOutputSummary(
    string Label,
    string StatusLabel,
    int MeshCount,
    int MaterialCount,
    int BoneCount,
    int AcceptedContributionCount,
    IReadOnlyList<SimAssemblySeamInputSummary> AcceptedInputs,
    string Notes);

public sealed record SimAssemblyContributionSummary(
    string Label,
    string StatusLabel,
    int MeshCount,
    int MaterialCount,
    int BoneCount,
    int RebasedWeightCount,
    int AddedBoneCount,
    string Notes);

public sealed record SimAssemblyPayloadSummary(
    string Label,
    string StatusLabel,
    int AnchorBoneCount,
    int AcceptedContributionCount,
    int MergedMeshCount,
    int RebasedWeightCount,
    int MappedBoneReferenceCount,
    int AddedBoneCount,
    string Notes);

public sealed record SimAssemblyAnchorSummary(
    string Label,
    string StatusLabel,
    string SourceLabel,
    string SourceResourceTgi,
    string SourcePackagePath,
    int BoneCount,
    IReadOnlyList<string> BoneNames,
    string Notes);

public sealed record SimAssemblyBoneMapEntrySummary(
    int SourceBoneIndex,
    int MergedBoneIndex,
    string BoneName);

public sealed record SimAssemblyBoneMapSummary(
    string Label,
    string SourceLabel,
    string SourceResourceTgi,
    string SourcePackagePath,
    int SourceBoneCount,
    int ReusedBoneReferenceCount,
    int AddedBoneCount,
    int RebasedWeightCount,
    IReadOnlyList<SimAssemblyBoneMapEntrySummary> Entries,
    string Notes);

public sealed record SimAssemblyMeshBatchSummary(
    string Label,
    string SourceLabel,
    string SourceResourceTgi,
    string SourcePackagePath,
    int MeshStartIndex,
    int MeshCount,
    int MaterialStartIndex,
    int MaterialCount,
    string Notes);

public enum SimAssemblyPayloadNodeKind
{
    AnchorSkeleton,
    BoneRemapTable,
    MeshSet
}

public sealed record SimAssemblyPayloadNodeSummary(
    string Label,
    int Order,
    SimAssemblyPayloadNodeKind Kind,
    string StatusLabel,
    string Summary,
    string Notes);

public enum SimAssemblyApplicationPassState
{
    Prepared,
    Pending,
    Unavailable
}

public sealed record SimAssemblyApplicationPassSummary(
    string Label,
    int Order,
    SimAssemblyApplicationPassState State,
    string StatusLabel,
    int InputCount,
    string Notes);

public sealed record SimAssemblyApplicationSummary(
    string Label,
    string StatusLabel,
    int PreparedPassCount,
    int PendingPassCount,
    int UnavailablePassCount,
    string Notes);

public sealed record SimAssemblyApplicationTargetSummary(
    string Label,
    string PassLabel,
    string StatusLabel,
    int TargetCount,
    string Notes);

public sealed record SimAssemblyApplicationPlanSummary(
    string Label,
    string PassLabel,
    string StatusLabel,
    int TargetCount,
    int OperationCount,
    string Notes);

public sealed record SimAssemblyApplicationTransformSummary(
    string Label,
    string PassLabel,
    string StatusLabel,
    int TargetCount,
    int OperationCount,
    string Notes);

public sealed record SimAssemblyApplicationOutcomeSummary(
    string Label,
    string PassLabel,
    string StatusLabel,
    int TargetCount,
    int AppliedCount,
    string Notes);

public sealed record SimAssemblyGraphSummary(
    SimAssemblyPlanSummary Plan,
    IReadOnlyList<SimAssemblyInputSummary> Inputs,
    IReadOnlyList<SimAssemblyStageSummary> Stages,
    SimAssemblyPayloadSummary Payload,
    SimAssemblyAnchorSummary PayloadAnchor,
    IReadOnlyList<SimAssemblyBoneMapSummary> PayloadBoneMaps,
    IReadOnlyList<SimAssemblyMeshBatchSummary> PayloadMeshBatches,
    IReadOnlyList<SimAssemblyPayloadNodeSummary> PayloadNodes,
    SimAssemblyApplicationSummary Application,
    IReadOnlyList<SimAssemblyApplicationPassSummary> ApplicationPasses,
    IReadOnlyList<SimAssemblyApplicationTargetSummary> ApplicationTargets,
    IReadOnlyList<SimAssemblyApplicationPlanSummary> ApplicationPlans,
    IReadOnlyList<SimAssemblyApplicationTransformSummary> ApplicationTransforms,
    IReadOnlyList<SimAssemblyApplicationOutcomeSummary> ApplicationOutcomes,
    SimAssemblyOutputSummary Output,
    IReadOnlyList<SimAssemblyContributionSummary> Contributions,
    IReadOnlyList<SimBodyGraphNodeSummary> Nodes);

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
    IReadOnlyList<float>? Uv0s,
    IReadOnlyList<float>? Uv1s,
    int PreferredUvChannel,
    IReadOnlyList<int> Indices,
    int MaterialIndex,
    IReadOnlyList<VertexWeight> SkinWeights);

public sealed record CanonicalMaterial(
    string Name,
    IReadOnlyList<CanonicalTexture> Textures,
    string? ShaderName = null,
    bool IsTransparent = false,
    string? AlphaMode = null,
    string? AlphaTextureSlot = null,
    IReadOnlyList<string>? LayeredTextureSlots = null,
    string? Approximation = null,
    CanonicalMaterialSourceKind SourceKind = CanonicalMaterialSourceKind.Unknown,
    CanonicalColor? ApproximateBaseColor = null,
    CanonicalColor? ViewportTintColor = null);

public sealed record CanonicalColor(float R, float G, float B, float A = 1f);

public sealed record CanonicalTexture(
    string Slot,
    string FileName,
    byte[] PngBytes,
    ResourceKeyRecord? SourceKey = null,
    string? SourcePackagePath = null,
    CanonicalTextureSemantic Semantic = CanonicalTextureSemantic.Unknown,
    CanonicalTextureSourceKind SourceKind = CanonicalTextureSourceKind.Unknown,
    int UvChannel = 0,
    float UvScaleU = 1f,
    float UvScaleV = 1f,
    float UvOffsetU = 0f,
    float UvOffsetV = 0f,
    bool IsApproximateUvTransform = false);

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

public sealed record PreviewBuildProgress(string Status, double? Fraction = null);
public sealed record ResourceReadProgress(string Status, double? Fraction = null);

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
    IReadOnlyList<string> EmbeddedLodLabels,
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
    IReadOnlyList<CasRegionMapSummary> RegionMaps,
    IReadOnlyList<MaterialManifestEntry> Materials,
    string? Category,
    string? SwatchSummary,
    string? SelectedLodLabel,
    bool IsSupported,
    string SupportedSubset);

public sealed record CasRegionMapSummary(
    string Label,
    string ResourceTgi,
    string PackagePath,
    int EntryCount,
    int LinkedKeyCount,
    bool HasReplacementEntries,
    IReadOnlyList<string> RegionLabels,
    string Notes);

public sealed record SimInfoSummary(
    uint Version,
    string SpeciesLabel,
    string AgeLabel,
    string GenderLabel,
    int PronounCount,
    int OutfitCategoryCount,
    int OutfitEntryCount,
    int OutfitPartCount,
    int TraitCount,
    int FaceModifierCount,
    int BodyModifierCount,
    int GeneticFaceModifierCount,
    int GeneticBodyModifierCount,
    int SculptCount,
    int GeneticSculptCount,
    int GeneticPartCount,
    int GrowthPartCount,
    int PeltLayerCount,
    string? SkintoneInstanceHex = null,
    float? SkintoneShift = null);

public sealed record SimSkintoneRenderSummary(
    string? SkintoneInstanceHex,
    float? SkintoneShift,
    string? SkintoneResourceTgi,
    string? SkintonePackagePath,
    string? BaseTextureResourceTgi,
    string? BaseTexturePackagePath,
    int OverlayTextureCount,
    int SwatchColorCount,
    CanonicalColor? ViewportTintColor,
    string Notes);

public sealed record SimSlotGroupSummary(
    string Label,
    int Count,
    string Notes);

public sealed record SimBodyFoundationSummary(
    string Label,
    int Count,
    string Notes);

public sealed record SimBodySourceSummary(
    string Label,
    int Count,
    string Notes);

public enum SimBodyCandidateSourceKind
{
    ExactPartLink,
    IndexedDefaultBodyRecipe,
    CanonicalFoundation,
    BodyTypeFallback,
    ArchetypeCompatibilityFallback
}

public sealed record SimBodyCandidateSummary(
    string Label,
    int Count,
    string Notes,
    SimBodyCandidateSourceKind SourceKind,
    IReadOnlyList<SimCasSlotOptionSummary> Candidates);

public enum SimBodyAssemblyMode
{
    None,
    FullBodyShell,
    BodyShell,
    BodyUnderlayWithSplitLayers,
    SplitBodyLayers,
    FallbackSingleLayer
}

public enum SimBodyAssemblyLayerState
{
    Active,
    Available,
    Blocked
}

public sealed record SimBodyAssemblyLayerSummary(
    string Label,
    int CandidateCount,
    string Contribution,
    SimBodyCandidateSourceKind SourceKind,
    SimBodyAssemblyLayerState State,
    string StateNotes,
    IReadOnlyList<SimCasSlotOptionSummary> Candidates);

public enum SimBodyGraphNodeState
{
    Resolved,
    Approximate,
    Pending,
    Unavailable
}

public sealed record SimBodyGraphNodeSummary(
    string Label,
    int Order,
    SimBodyGraphNodeState State,
    string Notes);

public sealed record SimBodyAssemblySummary(
    SimBodyAssemblyMode Mode,
    string Summary,
    IReadOnlyList<SimBodyAssemblyLayerSummary> Layers,
    IReadOnlyList<SimBodyGraphNodeSummary> GraphNodes);

public static class SimBodyAssemblyPolicy
{
    public static bool IsShellFamilyLabel(string? label) => label switch
    {
        "Full Body" => true,
        "Body" => true,
        _ => false
    };

    public static bool IsOverlayFamilyLabel(string? label) =>
        string.Equals(label, "Shoes", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlySet<string> ResolveActiveLabels(IEnumerable<string> availableLabels)
    {
        ArgumentNullException.ThrowIfNull(availableLabels);

        var labels = availableLabels
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (labels.Count == 0)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var preferredShell = labels.Contains("Full Body")
            ? "Full Body"
            : labels.Contains("Body")
                ? "Body"
                : null;

        if (labels.Contains("Top") && labels.Contains("Bottom"))
        {
            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Top", "Bottom" };
            if (preferredShell is not null)
            {
                active.Add(preferredShell);
            }

            if (labels.Contains("Shoes"))
            {
                active.Add("Shoes");
            }

            return active;
        }

        if (labels.Contains("Top"))
        {
            if (preferredShell is not null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { preferredShell };
            }

            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Top" };
            if (labels.Contains("Shoes"))
            {
                active.Add("Shoes");
            }

            return active;
        }

        if (labels.Contains("Bottom"))
        {
            if (preferredShell is not null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { preferredShell };
            }

            var active = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Bottom" };
            if (labels.Contains("Shoes"))
            {
                active.Add("Shoes");
            }

            return active;
        }

        if (labels.Contains("Full Body"))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Full Body" };
        }

        if (labels.Contains("Body"))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Body" };
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static SimBodyAssemblyMode GetMode(IReadOnlySet<string> activeLabels)
    {
        ArgumentNullException.ThrowIfNull(activeLabels);

        if (activeLabels.Count == 0)
        {
            return SimBodyAssemblyMode.None;
        }

        var hasShell = activeLabels.Contains("Full Body") || activeLabels.Contains("Body");
        var hasSplitLayers = activeLabels.Contains("Top") && activeLabels.Contains("Bottom");
        if (hasShell && hasSplitLayers)
        {
            return SimBodyAssemblyMode.BodyUnderlayWithSplitLayers;
        }

        if (activeLabels.Contains("Full Body"))
        {
            return SimBodyAssemblyMode.FullBodyShell;
        }

        if (activeLabels.Contains("Body"))
        {
            return SimBodyAssemblyMode.BodyShell;
        }

        return activeLabels.Count > 1
            ? SimBodyAssemblyMode.SplitBodyLayers
            : SimBodyAssemblyMode.FallbackSingleLayer;
    }

    public static SimBodyAssemblyLayerState GetLayerState(string label, IReadOnlySet<string> activeLabels)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(activeLabels);

        if (activeLabels.Contains(label))
        {
            return SimBodyAssemblyLayerState.Active;
        }

        var hasSplitLayers = activeLabels.Contains("Top") && activeLabels.Contains("Bottom");
        if (hasSplitLayers && (activeLabels.Contains("Full Body") || activeLabels.Contains("Body")))
        {
            return SimBodyAssemblyLayerState.Available;
        }

        if (activeLabels.Contains("Full Body"))
        {
            return string.Equals(label, "Shoes", StringComparison.OrdinalIgnoreCase)
                ? SimBodyAssemblyLayerState.Available
                : SimBodyAssemblyLayerState.Blocked;
        }

        if (activeLabels.Contains("Body"))
        {
            return string.Equals(label, "Top", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(label, "Bottom", StringComparison.OrdinalIgnoreCase)
                ? SimBodyAssemblyLayerState.Blocked
                : SimBodyAssemblyLayerState.Available;
        }

        return SimBodyAssemblyLayerState.Available;
    }
}

public sealed record SimMorphGroupSummary(
    string Label,
    int Count,
    string Notes);

public enum SimCasSlotCandidateSourceKind
{
    ExactPartLink,
    CompatibilityFallback
}

public sealed record SimCasSlotCandidateSummary(
    string Label,
    int Count,
    string Notes,
    SimCasSlotCandidateSourceKind SourceKind,
    IReadOnlyList<SimCasSlotOptionSummary> Candidates);

public sealed record SimCasSlotOptionSummary(
    Guid AssetId,
    string DisplayName,
    string? PackagePath,
    string? PackageName,
    string? RootTgi);

public sealed record SimTemplateOptionSummary(
    Guid ResourceId,
    string DisplayName,
    string PackagePath,
    string? PackageName,
    string RootTgi,
    bool IsRepresentative,
    string Notes,
    int OutfitCategoryCount,
    int OutfitEntryCount,
    int OutfitPartCount,
    int BodyModifierCount,
    int FaceModifierCount,
    int SculptCount,
    bool HasSkintone,
    int? AuthoritativeBodyDrivingOutfitCount = null,
    int? AuthoritativeBodyDrivingOutfitPartCount = null)
{
    public bool HasAuthoritativeBodyParts =>
        OutfitPartCount > 0 || OutfitEntryCount > 0 || OutfitCategoryCount > 0;

    public bool HasIndexedAuthoritativeBodyDrivingFacts =>
        AuthoritativeBodyDrivingOutfitCount.HasValue &&
        AuthoritativeBodyDrivingOutfitPartCount.HasValue;
}

public sealed record SimTemplateFactSummary(
    Guid ResourceId,
    Guid DataSourceId,
    SourceKind SourceKind,
    string PackagePath,
    string RootTgi,
    string ArchetypeKey,
    string SpeciesLabel,
    string AgeLabel,
    string GenderLabel,
    string DisplayName,
    string Notes,
    int OutfitCategoryCount,
    int OutfitEntryCount,
    int OutfitPartCount,
    int BodyModifierCount,
    int FaceModifierCount,
    int SculptCount,
    bool HasSkintone,
    int AuthoritativeBodyDrivingOutfitCount,
    int AuthoritativeBodyDrivingOutfitPartCount);

public sealed record SimTemplateBodyPartFact(
    Guid ResourceId,
    Guid DataSourceId,
    SourceKind SourceKind,
    string PackagePath,
    string RootTgi,
    int OutfitCategoryValue,
    string OutfitCategoryLabel,
    int OutfitIndex,
    int PartIndex,
    int BodyType,
    string? BodyTypeLabel,
    ulong PartInstance,
    bool IsBodyDriving);

public static class SimTemplateSelectionPolicy
{
    public static bool HasExplicitBodyDrivingRecipe(SimTemplateOptionSummary template) =>
        (template.AuthoritativeBodyDrivingOutfitCount ?? 0) > 0;

    public static int GetBodyShellSelectionTier(SimTemplateOptionSummary template) =>
        HasExplicitBodyDrivingRecipe(template)
            ? 0
            : template.HasAuthoritativeBodyParts
                ? 1
                : template.HasIndexedAuthoritativeBodyDrivingFacts
                    ? 2
                    : 3;

    public static IOrderedEnumerable<SimTemplateOptionSummary> OrderTemplates(IEnumerable<SimTemplateOptionSummary> templates) =>
        templates
            .OrderBy(GetBodyShellSelectionTier)
            .ThenByDescending(static template => template.AuthoritativeBodyDrivingOutfitCount ?? 0)
            .ThenByDescending(static template => template.AuthoritativeBodyDrivingOutfitPartCount ?? 0)
            .ThenBy(static template => template.OutfitCategoryCount)
            .ThenBy(static template => template.OutfitEntryCount)
            .ThenBy(static template => template.OutfitPartCount)
            .ThenByDescending(static template => template.BodyModifierCount + template.SculptCount)
            .ThenByDescending(static template => template.FaceModifierCount)
            .ThenByDescending(static template => template.HasSkintone)
            .ThenByDescending(static template => template.IsRepresentative)
            .ThenBy(static template => template.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static template => template.PackagePath, StringComparer.OrdinalIgnoreCase);

    public static SimTemplateOptionSummary? SelectPreferredTemplate(IEnumerable<SimTemplateOptionSummary> templates) =>
        OrderTemplates(templates).FirstOrDefault();

    public static bool IsPreferredOver(SimTemplateOptionSummary candidate, SimTemplateOptionSummary? baseline)
    {
        if (baseline is null)
        {
            return true;
        }

        return OrderTemplates([candidate, baseline]).First().ResourceId == candidate.ResourceId;
    }
}

public sealed record SimAssetGraph(
    ResourceMetadata SimInfoResource,
    SimInfoSummary Metadata,
    SimSkintoneRenderSummary? SkintoneRender,
    IReadOnlyList<SimTemplateOptionSummary> TemplateOptions,
    IReadOnlyList<SimBodyFoundationSummary> BodyFoundation,
    IReadOnlyList<SimBodySourceSummary> BodySources,
    IReadOnlyList<SimBodyCandidateSummary> BodyCandidates,
    SimBodyAssemblySummary BodyAssembly,
    IReadOnlyList<SimSlotGroupSummary> SlotGroups,
    IReadOnlyList<SimMorphGroupSummary> MorphGroups,
    IReadOnlyList<SimCasSlotCandidateSummary> CasSlotCandidates,
    IReadOnlyList<ResourceMetadata> IdentityResources,
    IReadOnlyList<string> Diagnostics,
    bool IsSupported,
    string SupportedSubset);

public sealed record General3DAssetGraph(
    ResourceMetadata RootResource,
    ResourceMetadata SceneRootResource,
    IReadOnlyList<ResourceMetadata> ModelResources,
    IReadOnlyList<ResourceMetadata> ModelLodResources,
    IReadOnlyList<string> EmbeddedLodLabels,
    IReadOnlyList<ResourceMetadata> GeometryResources,
    IReadOnlyList<ResourceMetadata> RigResources,
    IReadOnlyList<ResourceMetadata> MaterialResources,
    IReadOnlyList<ResourceMetadata> TextureResources,
    IReadOnlyList<string> Diagnostics,
    bool IsSupported,
    string SupportedSubset);

public sealed record MaterialManifestEntry(
    string MaterialName,
    string? ShaderName,
    bool IsTransparent,
    string? AlphaMode,
    string? AlphaTextureSlot,
    IReadOnlyList<string>? LayeredTextureSlots,
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
    public static string BuildSupportLabel(AssetSummary asset) =>
        BuildSupportLabel(asset.AssetKind, asset.CapabilitySnapshot);

    public static string BuildSupportLabel(AssetKind assetKind, AssetCapabilitySnapshot facts)
    {
        if (assetKind == AssetKind.Sim && facts.HasIdentityMetadata)
        {
            if (facts.HasExactGeometryCandidate && facts.HasTextureReferences)
            {
                return "Geometry+Textures";
            }

            if (facts.HasExactGeometryCandidate)
            {
                return "Geometry";
            }

            return "Metadata";
        }

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
        => BuildSupportNotes(asset.AssetKind, asset.CapabilitySnapshot);

    public static string BuildSupportNotes(AssetKind assetKind, AssetCapabilitySnapshot facts)
    {
        if (assetKind == AssetKind.Sim && facts.HasIdentityMetadata)
        {
            if (facts.HasExactGeometryCandidate && facts.HasTextureReferences)
            {
                return "A rendered body-first Sim preview is available from resolved body/head assembly inputs; full named or preset character assembly is still in progress.";
            }

            if (facts.HasExactGeometryCandidate)
            {
                return "A rendered body-first Sim preview is available, but material or texture coverage is still partial; full named or preset character assembly is still in progress.";
            }

            return "This Sim archetype currently exposes grouped SimInfo metadata plus a body-first assembly inspector. A rendered preview depends on an authoritative body-driving template and resolved shell candidates; full named or preset character assembly is still in progress.";
        }

        if (!facts.HasSceneRoot)
        {
            return "This asset does not currently resolve a scene root.";
        }

        if (facts.HasExactGeometryCandidate && facts.HasTextureReferences)
        {
            return "Exact-asset geometry and texture candidates are available.";
        }

        if (facts.HasExactGeometryCandidate)
        {
            return "Exact-asset geometry candidates are available, but texture coverage is incomplete.";
        }

        return "The asset is available for browsing, but no exact geometry candidate is currently resolved.";
    }
}

public static class AssetDetailsFormatter
{
    public static string BuildAssetDetails(
        AssetSummary asset,
        AssetGraph graph,
        ResourceMetadata? sceneRoot,
        ScenePreviewContent? scenePreview,
        IReadOnlyList<AssetVariantSummary>? variants = null,
        AssetVariantSummary? selectedVariant = null)
    {
        var buildBuyGraph = graph.BuildBuyGraph;
        var casGraph = graph.CasGraph;
        var simGraph = graph.SimGraph;
        var general3DGraph = graph.General3DGraph;
        var scene = scenePreview?.Scene;
        var displayFacts = BuildDisplayCapabilitySnapshot(asset, graph, sceneRoot, scenePreview);
        var supportLabel = AssetDerivedState.BuildSupportLabel(asset.AssetKind, displayFacts);
        var supportNotes = AssetDerivedState.BuildSupportNotes(asset.AssetKind, displayFacts);
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
                Support Status: {supportLabel}
                Support Notes: {supportNotes}
                Package Name: {asset.PackageName ?? Path.GetFileName(asset.PackagePath)}
                Root Type: {asset.RootTypeName ?? "(unknown)"}
                Thumbnail Type: {asset.ThumbnailTypeName ?? "(none)"}
                Geometry Type: {asset.PrimaryGeometryType ?? "(unknown)"}
                Identity Type: {asset.IdentityType ?? "(unknown)"}
                Has Scene Root: {FormatYesNo(displayFacts.HasSceneRoot)}
                Has Exact Geometry Candidate: {FormatYesNo(displayFacts.HasExactGeometryCandidate)}
                Has Material References: {FormatYesNo(displayFacts.HasMaterialReferences)}
                Has Texture References: {FormatYesNo(displayFacts.HasTextureReferences)}
                Has Identity Metadata: {FormatYesNo(displayFacts.HasIdentityMetadata)}
                Has Geometry Reference: {FormatYesNo(displayFacts.HasGeometryReference)}
                Has Rig Reference: {FormatYesNo(displayFacts.HasRigReference)}
                Has Material Resource Candidate: {FormatYesNo(displayFacts.HasMaterialResourceCandidate)}
                Has Texture Resource Candidate: {FormatYesNo(displayFacts.HasTextureResourceCandidate)}
                Package-Local Graph: {FormatYesNo(displayFacts.IsPackageLocalGraph)}
                Has Diagnostics: {FormatYesNo(displayFacts.HasDiagnostics)}
                Scene Reconstruction: {BuildSceneReconstructionStatus(sceneRoot, scenePreview)}
                Variant Count: {asset.VariantCount}
                Selected Variant: {FormatSelectedVariant(selectedVariant)}
                Indexed Variants:
                {FormatAssetVariants(variants, selectedVariant)}
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

        if (asset.AssetKind == AssetKind.Sim)
        {
            return
                $"""
                Asset: {asset.DisplayName}
                Kind: {asset.KindLabel}
                Category: {asset.Category ?? "(unknown)"}
                Package: {asset.PackagePath}
                Root TGI: {asset.RootKey.FullTgi}
                Support Status: {supportLabel}
                Support Notes: {supportNotes}
                Package Name: {asset.PackageName ?? Path.GetFileName(asset.PackagePath)}
                Root Type: {asset.RootTypeName ?? "(unknown)"}
                Thumbnail Type: {asset.ThumbnailTypeName ?? "(none)"}
                Geometry Type: {asset.PrimaryGeometryType ?? "(none)"}
                Identity Type: {asset.IdentityType ?? "(unknown)"}
                Has Scene Root: {FormatYesNo(displayFacts.HasSceneRoot)}
                Has Exact Geometry Candidate: {FormatYesNo(displayFacts.HasExactGeometryCandidate)}
                Has Material References: {FormatYesNo(displayFacts.HasMaterialReferences)}
                Has Texture References: {FormatYesNo(displayFacts.HasTextureReferences)}
                Has Identity Metadata: {FormatYesNo(displayFacts.HasIdentityMetadata)}
                Has Geometry Reference: {FormatYesNo(displayFacts.HasGeometryReference)}
                Has Rig Reference: {FormatYesNo(displayFacts.HasRigReference)}
                Has Material Resource Candidate: {FormatYesNo(displayFacts.HasMaterialResourceCandidate)}
                Has Texture Resource Candidate: {FormatYesNo(displayFacts.HasTextureResourceCandidate)}
                Package-Local Graph: {FormatYesNo(displayFacts.IsPackageLocalGraph)}
                Has Diagnostics: {FormatYesNo(displayFacts.HasDiagnostics)}
                Scene Reconstruction: {BuildSimSceneReconstructionStatus(scenePreview)}
                Variant Count: {asset.VariantCount}
                Selected Variant: {FormatSelectedVariant(selectedVariant)}
                Indexed Variants:
                {FormatAssetVariants(variants, selectedVariant)}
                Identity Resources: {simGraph?.IdentityResources.Count ?? 0}
                Supported Subset: {simGraph?.SupportedSubset ?? "Metadata-only Sim archetypes derived from grouped SimInfo rows."}
                Template Variations: {simGraph?.TemplateOptions.Count.ToString() ?? "(unknown)"}
                SimInfo Version: {simGraph?.Metadata.Version.ToString() ?? "(unknown)"}
                Species: {simGraph?.Metadata.SpeciesLabel ?? "(unknown)"}
                Age: {simGraph?.Metadata.AgeLabel ?? "(unknown)"}
                Gender: {simGraph?.Metadata.GenderLabel ?? "(unknown)"}
                Pronouns: {simGraph?.Metadata.PronounCount.ToString() ?? "(unknown)"}
                Outfit Categories: {simGraph?.Metadata.OutfitCategoryCount.ToString() ?? "(unknown)"}
                Outfit Entries: {simGraph?.Metadata.OutfitEntryCount.ToString() ?? "(unknown)"}
                Outfit Parts: {simGraph?.Metadata.OutfitPartCount.ToString() ?? "(unknown)"}
                Traits: {simGraph?.Metadata.TraitCount.ToString() ?? "(unknown)"}
                Face Modifiers: {simGraph?.Metadata.FaceModifierCount.ToString() ?? "(unknown)"}
                Body Modifiers: {simGraph?.Metadata.BodyModifierCount.ToString() ?? "(unknown)"}
                Genetic Face Modifiers: {simGraph?.Metadata.GeneticFaceModifierCount.ToString() ?? "(unknown)"}
                Genetic Body Modifiers: {simGraph?.Metadata.GeneticBodyModifierCount.ToString() ?? "(unknown)"}
                Sculpts: {simGraph?.Metadata.SculptCount.ToString() ?? "(unknown)"}
                Genetic Sculpts: {simGraph?.Metadata.GeneticSculptCount.ToString() ?? "(unknown)"}
                Genetic Parts: {simGraph?.Metadata.GeneticPartCount.ToString() ?? "(unknown)"}
                Growth Parts: {simGraph?.Metadata.GrowthPartCount.ToString() ?? "(unknown)"}
                Pelt Layers: {simGraph?.Metadata.PeltLayerCount.ToString() ?? "(unknown)"}
                Skintone Instance: {simGraph?.Metadata.SkintoneInstanceHex ?? "(none)"}
                Skintone Shift: {FormatOptionalFloat(simGraph?.Metadata.SkintoneShift)}
                Template Options:
                {FormatSimTemplateOptions(simGraph?.TemplateOptions)}
                Body Foundation:
                {FormatSimBodyFoundation(simGraph?.BodyFoundation)}
                Body Sources:
                {FormatSimBodySources(simGraph?.BodySources)}
                Body Candidates:
                {FormatSimBodyCandidates(simGraph?.BodyCandidates)}
                Body Assembly:
                {FormatSimBodyAssembly(simGraph?.BodyAssembly)}
                Base Body Graph:
                {FormatSimBodyGraphNodes(simGraph?.BodyAssembly)}
                Slot Groups:
                {FormatSimSlotGroups(simGraph?.SlotGroups)}
                Morph Groups:
                {FormatSimMorphGroups(simGraph?.MorphGroups)}
                CAS Slot Candidates:
                {FormatSimCasSlotCandidates(simGraph?.CasSlotCandidates)}
                Diagnostics:
                {string.Join(Environment.NewLine, diagnostics)}
                """;
        }

        if (general3DGraph is not null)
        {
            return
                $"""
                Asset: {asset.DisplayName}
                Kind: {asset.AssetKind}
                Category: {asset.Category ?? "(unknown)"}
                Package: {asset.PackagePath}
                Root TGI: {asset.RootKey.FullTgi}
                Support Status: {supportLabel}
                Support Notes: {supportNotes}
                Package Name: {asset.PackageName ?? Path.GetFileName(asset.PackagePath)}
                Root Type: {asset.RootTypeName ?? "(unknown)"}
                Thumbnail Type: {asset.ThumbnailTypeName ?? "(none)"}
                Geometry Type: {asset.PrimaryGeometryType ?? "(unknown)"}
                Identity Type: {asset.IdentityType ?? "(none)"}
                Has Scene Root: {FormatYesNo(displayFacts.HasSceneRoot)}
                Has Exact Geometry Candidate: {FormatYesNo(displayFacts.HasExactGeometryCandidate)}
                Has Material References: {FormatYesNo(displayFacts.HasMaterialReferences)}
                Has Texture References: {FormatYesNo(displayFacts.HasTextureReferences)}
                Has Identity Metadata: {FormatYesNo(displayFacts.HasIdentityMetadata)}
                Has Geometry Reference: {FormatYesNo(displayFacts.HasGeometryReference)}
                Has Rig Reference: {FormatYesNo(displayFacts.HasRigReference)}
                Has Material Resource Candidate: {FormatYesNo(displayFacts.HasMaterialResourceCandidate)}
                Has Texture Resource Candidate: {FormatYesNo(displayFacts.HasTextureResourceCandidate)}
                Package-Local Graph: {FormatYesNo(displayFacts.IsPackageLocalGraph)}
                Has Diagnostics: {FormatYesNo(displayFacts.HasDiagnostics)}
                Scene Reconstruction: {BuildSceneReconstructionStatus(sceneRoot, scenePreview)}
                Variant Count: {asset.VariantCount}
                Selected Variant: {FormatSelectedVariant(selectedVariant)}
                Indexed Variants:
                {FormatAssetVariants(variants, selectedVariant)}
                Linked Resources: {asset.LinkedResourceCount}
                Thumbnail: {asset.ThumbnailTgi ?? "(none)"}
                Supported Subset: {general3DGraph.SupportedSubset}
                Scene Root: {sceneRoot?.Key.FullTgi ?? "(unresolved)"}
                Model Candidates: {general3DGraph.ModelResources.Count}
                Model LOD Candidates: {general3DGraph.ModelLodResources.Count}
                Embedded LOD Labels: {general3DGraph.EmbeddedLodLabels.Count}
                Geometry Candidates: {general3DGraph.GeometryResources.Count}
                Rig Candidates: {general3DGraph.RigResources.Count}
                Material Candidates: {general3DGraph.MaterialResources.Count}
                Texture Candidates: {general3DGraph.TextureResources.Count}
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
            Support Status: {supportLabel}
            Support Notes: {supportNotes}
            Package Name: {asset.PackageName ?? Path.GetFileName(asset.PackagePath)}
            Root Type: {asset.RootTypeName ?? "(unknown)"}
            Thumbnail Type: {asset.ThumbnailTypeName ?? "(none)"}
            Geometry Type: {asset.PrimaryGeometryType ?? "(unknown)"}
            Identity Type: {asset.IdentityType ?? "(unknown)"}
            Has Scene Root: {FormatYesNo(displayFacts.HasSceneRoot)}
            Has Exact Geometry Candidate: {FormatYesNo(displayFacts.HasExactGeometryCandidate)}
            Has Material References: {FormatYesNo(displayFacts.HasMaterialReferences)}
            Has Texture References: {FormatYesNo(displayFacts.HasTextureReferences)}
            Has Identity Metadata: {FormatYesNo(displayFacts.HasIdentityMetadata)}
            Has Geometry Reference: {FormatYesNo(displayFacts.HasGeometryReference)}
            Has Rig Reference: {FormatYesNo(displayFacts.HasRigReference)}
            Has Material Resource Candidate: {FormatYesNo(displayFacts.HasMaterialResourceCandidate)}
            Has Texture Resource Candidate: {FormatYesNo(displayFacts.HasTextureResourceCandidate)}
            Package-Local Graph: {FormatYesNo(displayFacts.IsPackageLocalGraph)}
            Has Diagnostics: {FormatYesNo(displayFacts.HasDiagnostics)}
            Scene Reconstruction: {BuildSceneReconstructionStatus(sceneRoot, scenePreview)}
            Variant Count: {asset.VariantCount}
            Swatch/Variant: {casGraph?.SwatchSummary ?? "(unknown)"}
            Selected Variant: {FormatSelectedVariant(selectedVariant)}
            Indexed Variants:
            {FormatAssetVariants(variants, selectedVariant)}
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

    private static AssetCapabilitySnapshot BuildDisplayCapabilitySnapshot(
        AssetSummary asset,
        AssetGraph graph,
        ResourceMetadata? sceneRoot,
        ScenePreviewContent? scenePreview)
    {
        var facts = asset.CapabilitySnapshot;
        var isPackageLocalGraph = facts.IsPackageLocalGraph &&
            graph.LinkedResources.All(resource => string.Equals(resource.PackagePath, asset.PackagePath, StringComparison.OrdinalIgnoreCase));
        var hasDiagnostics = facts.HasDiagnostics || graph.Diagnostics.Any(static diagnostic => !string.IsNullOrWhiteSpace(diagnostic));
        var hasDisplayScene = scenePreview?.Scene is not null;
        var hasDisplayMaterials = scenePreview?.Scene?.Materials.Count > 0;
        var hasDisplayTextures = scenePreview?.Scene?.Materials.Any(static material => material.Textures.Count > 0) == true;

        if (graph.BuildBuyGraph is { } buildBuyGraph)
        {
            return facts with
            {
                HasExactGeometryCandidate = facts.HasExactGeometryCandidate || hasDisplayScene || buildBuyGraph.ModelLodResources.Count > 0 || buildBuyGraph.ModelResource is not null,
                HasMaterialReferences = facts.HasMaterialReferences || hasDisplayMaterials || buildBuyGraph.Materials.Count > 0 || buildBuyGraph.MaterialResources.Count > 0,
                HasTextureReferences = facts.HasTextureReferences || hasDisplayTextures || buildBuyGraph.TextureResources.Count > 0 || buildBuyGraph.Materials.Any(static material => material.Textures.Count > 0),
                HasIdentityMetadata = facts.HasIdentityMetadata || buildBuyGraph.IdentityResources.Count > 0,
                HasGeometryReference = facts.HasGeometryReference || hasDisplayScene || buildBuyGraph.ModelLodResources.Count > 0 || buildBuyGraph.ModelResource is not null,
                HasMaterialResourceCandidate = facts.HasMaterialResourceCandidate || hasDisplayMaterials || buildBuyGraph.MaterialResources.Count > 0,
                HasTextureResourceCandidate = facts.HasTextureResourceCandidate || hasDisplayTextures || buildBuyGraph.TextureResources.Count > 0,
                IsPackageLocalGraph = isPackageLocalGraph,
                HasDiagnostics = hasDiagnostics
            };
        }

        if (graph.CasGraph is { } casGraph)
        {
            return facts with
            {
                HasExactGeometryCandidate = facts.HasExactGeometryCandidate || hasDisplayScene || casGraph.GeometryResource is not null,
                HasMaterialReferences = facts.HasMaterialReferences || hasDisplayMaterials || casGraph.MaterialResources.Count > 0 || casGraph.Materials.Count > 0,
                HasTextureReferences = facts.HasTextureReferences || hasDisplayTextures || casGraph.TextureResources.Count > 0 || casGraph.Materials.Any(static material => material.Textures.Count > 0),
                HasIdentityMetadata = facts.HasIdentityMetadata || casGraph.IdentityResources.Count > 0,
                HasRigReference = facts.HasRigReference || casGraph.RigResources.Count > 0,
                HasGeometryReference = facts.HasGeometryReference || hasDisplayScene || casGraph.GeometryResources.Count > 0,
                HasMaterialResourceCandidate = facts.HasMaterialResourceCandidate || hasDisplayMaterials || casGraph.MaterialResources.Count > 0,
                HasTextureResourceCandidate = facts.HasTextureResourceCandidate || hasDisplayTextures || casGraph.TextureResources.Count > 0,
                IsPackageLocalGraph = isPackageLocalGraph,
                HasDiagnostics = hasDiagnostics
            };
        }

        if (graph.General3DGraph is { } general3DGraph)
        {
            return facts with
            {
                HasExactGeometryCandidate = facts.HasExactGeometryCandidate || hasDisplayScene || general3DGraph.SceneRootResource is not null,
                HasMaterialReferences = facts.HasMaterialReferences || hasDisplayMaterials || general3DGraph.MaterialResources.Count > 0,
                HasTextureReferences = facts.HasTextureReferences || hasDisplayTextures || general3DGraph.TextureResources.Count > 0,
                HasIdentityMetadata = facts.HasIdentityMetadata || general3DGraph.RootResource is not null,
                HasRigReference = facts.HasRigReference || general3DGraph.RigResources.Count > 0,
                HasGeometryReference = facts.HasGeometryReference || hasDisplayScene || general3DGraph.GeometryResources.Count > 0 || general3DGraph.ModelLodResources.Count > 0 || general3DGraph.ModelResources.Count > 0,
                HasMaterialResourceCandidate = facts.HasMaterialResourceCandidate || hasDisplayMaterials || general3DGraph.MaterialResources.Count > 0,
                HasTextureResourceCandidate = facts.HasTextureResourceCandidate || hasDisplayTextures || general3DGraph.TextureResources.Count > 0,
                IsPackageLocalGraph = isPackageLocalGraph,
                HasDiagnostics = hasDiagnostics
            };
        }

        if (graph.SimGraph is { } simGraph)
        {
            return facts with
            {
                HasExactGeometryCandidate = facts.HasExactGeometryCandidate || hasDisplayScene,
                HasMaterialReferences = facts.HasMaterialReferences || hasDisplayMaterials,
                HasTextureReferences = facts.HasTextureReferences || hasDisplayTextures,
                HasIdentityMetadata = facts.HasIdentityMetadata || simGraph.IdentityResources.Count > 0,
                HasGeometryReference = facts.HasGeometryReference || hasDisplayScene,
                HasMaterialResourceCandidate = facts.HasMaterialResourceCandidate || hasDisplayMaterials,
                HasTextureResourceCandidate = facts.HasTextureResourceCandidate || hasDisplayTextures,
                IsPackageLocalGraph = isPackageLocalGraph,
                HasDiagnostics = hasDiagnostics
            };
        }

        return facts with
        {
            IsPackageLocalGraph = isPackageLocalGraph,
            HasDiagnostics = hasDiagnostics
        };
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

    private static string BuildSimSceneReconstructionStatus(ScenePreviewContent? scenePreview)
    {
        if (scenePreview is null)
        {
            return "Not attempted (no assembled Sim preview scene is available)";
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

    private static string FormatAssetVariants(IReadOnlyList<AssetVariantSummary>? variants, AssetVariantSummary? selectedVariant)
    {
        if (variants is null || variants.Count == 0)
        {
            return "(none indexed)";
        }

        return string.Join(
            Environment.NewLine,
            variants
                .OrderBy(static variant => variant.VariantKind, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static variant => variant.VariantIndex)
                .Select(variant =>
                {
                    var isSelected = selectedVariant is not null &&
                        string.Equals(selectedVariant.RootTgi, variant.RootTgi, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(selectedVariant.PackagePath, variant.PackagePath, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(selectedVariant.VariantKind, variant.VariantKind, StringComparison.OrdinalIgnoreCase) &&
                        selectedVariant.VariantIndex == variant.VariantIndex;
                    var suffix = new List<string>();
                    if (!string.IsNullOrWhiteSpace(variant.SwatchHex))
                    {
                        suffix.Add(variant.SwatchHex);
                    }

                    if (!string.IsNullOrWhiteSpace(variant.ThumbnailTgi))
                    {
                        suffix.Add($"thumb {variant.ThumbnailTgi}");
                    }

                    if (!string.IsNullOrWhiteSpace(variant.Diagnostics))
                    {
                        suffix.Add(variant.Diagnostics);
                    }

                    var body = suffix.Count == 0
                        ? $"{variant.VariantKind} {variant.VariantIndex + 1}: {variant.DisplayLabel}"
                        : $"{variant.VariantKind} {variant.VariantIndex + 1}: {variant.DisplayLabel} | {string.Join(" | ", suffix)}";

                    return isSelected ? $"> {body}" : $"  {body}";
                }));
    }

    private static string FormatSimSlotGroups(IReadOnlyList<SimSlotGroupSummary>? groups)
    {
        if (groups is null || groups.Count == 0)
        {
            return "(none indexed yet)";
        }

        return string.Join(
            Environment.NewLine,
            groups.Select(static group => $"- {group.Label}: {group.Count} ({group.Notes})"));
    }

    private static string FormatSimBodyFoundation(IReadOnlyList<SimBodyFoundationSummary>? groups)
    {
        if (groups is null || groups.Count == 0)
        {
            return "(body foundation is not summarized yet)";
        }

        return string.Join(
            Environment.NewLine,
            groups.Select(static group => $"- {group.Label}: {group.Count} ({group.Notes})"));
    }

    private static string FormatSimBodySources(IReadOnlyList<SimBodySourceSummary>? groups)
    {
        if (groups is null || groups.Count == 0)
        {
            return "(no concrete body-source references surfaced yet)";
        }

        return string.Join(
            Environment.NewLine,
            groups.Select(static group => $"- {group.Label}: {group.Count} ({group.Notes})"));
    }

    private static string FormatSimTemplateOptions(IReadOnlyList<SimTemplateOptionSummary>? options)
    {
        if (options is null || options.Count == 0)
        {
            return "(no grouped SimInfo templates resolved yet)";
        }

        return string.Join(
            Environment.NewLine,
            options
                .Take(8)
                .Select(static option =>
                    $"- {option.DisplayName}: {(option.PackageName ?? Path.GetFileName(option.PackagePath))}{(option.IsRepresentative ? " | representative" : string.Empty)}"));
    }

    private static string FormatSimBodyCandidates(IReadOnlyList<SimBodyCandidateSummary>? groups)
    {
        if (groups is null || groups.Count == 0)
        {
            return "(no exact body-part assets resolved yet)";
        }

        return string.Join(
            Environment.NewLine,
            groups.Select(static group => $"- {group.Label}: {group.Count} ({group.Notes})"));
    }

    private static string FormatSimBodyAssembly(SimBodyAssemblySummary? summary)
    {
        if (summary is null || summary.Layers.Count == 0)
        {
            return "(base-body assembly recipe is not resolved yet)";
        }

        var modeLabel = summary.Mode switch
        {
            SimBodyAssemblyMode.FullBodyShell => "Full-body shell",
            SimBodyAssemblyMode.BodyShell => "Primary body shell",
            SimBodyAssemblyMode.BodyUnderlayWithSplitLayers => "Body underlay + split layers",
            SimBodyAssemblyMode.SplitBodyLayers => "Split body layers",
            SimBodyAssemblyMode.FallbackSingleLayer => "Fallback single layer",
            _ => "Unresolved"
        };

        var lines = new List<string>
        {
            $"- Mode: {modeLabel} ({summary.Summary})"
        };
        lines.AddRange(
            summary.Layers.Select(static layer =>
                $"- {layer.Label}: {layer.State} | {layer.Contribution} | {layer.CandidateCount} candidate(s) | {layer.StateNotes}"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatSimBodyGraphNodes(SimBodyAssemblySummary? summary)
    {
        if (summary is null || summary.GraphNodes.Count == 0)
        {
            return "(base-body graph nodes are not summarized yet)";
        }

        return string.Join(
            Environment.NewLine,
            summary.GraphNodes
                .OrderBy(static node => node.Order)
                .Select(static node => $"- {node.Order + 1}. {node.Label}: {node.State} ({node.Notes})"));
    }

    private static string FormatSimMorphGroups(IReadOnlyList<SimMorphGroupSummary>? groups)
    {
        if (groups is null || groups.Count == 0)
        {
            return "(none indexed yet)";
        }

        return string.Join(
            Environment.NewLine,
            groups.Select(static group => $"- {group.Label}: {group.Count} ({group.Notes})"));
    }

    private static string FormatSimCasSlotCandidates(IReadOnlyList<SimCasSlotCandidateSummary>? candidates)
    {
        if (candidates is null || candidates.Count == 0)
        {
            return "(no compatible CAS slot families resolved yet)";
        }

        return string.Join(
            Environment.NewLine,
            candidates.Select(static candidate => $"- {candidate.Label}: {candidate.Count} ({candidate.Notes})"));
    }

    private static string FormatSelectedVariant(AssetVariantSummary? selectedVariant) =>
        selectedVariant is null
            ? "Catalog default"
            : $"{selectedVariant.VariantKind} {selectedVariant.VariantIndex + 1}: {selectedVariant.DisplayLabel}";

    private static string FormatOptionalFloat(float? value) =>
        value.HasValue ? value.Value.ToString("0.###") : "(none)";

    private static string FormatYesNo(bool value) => value ? "Yes" : "No";
}

public static class AssetVariantSceneAdapter
{
    public static CanonicalScene ApplyToScene(CanonicalScene scene, AssetKind assetKind, AssetVariantSummary? selectedVariant)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (assetKind != AssetKind.Cas || !TryParseSwatchTint(selectedVariant, out var tint))
        {
            return scene;
        }

        var tintedMaterials = scene.Materials
            .Select(material => material.SourceKind == CanonicalMaterialSourceKind.ApproximateCas
                ? material with { ViewportTintColor = tint }
                : material)
            .ToArray();

        return tintedMaterials.SequenceEqual(scene.Materials)
            ? scene
            : scene with { Materials = tintedMaterials };
    }

    public static string AppendVariantDiagnostics(string diagnostics, AssetKind assetKind, AssetVariantSummary? selectedVariant)
    {
        if (assetKind != AssetKind.Cas || !TryParseSwatchTint(selectedVariant, out _))
        {
            return diagnostics;
        }

        var prefix = $"Selected swatch tint: {selectedVariant!.SwatchHex}";
        if (string.IsNullOrWhiteSpace(diagnostics))
        {
            return prefix;
        }

        return $"{diagnostics}{Environment.NewLine}{prefix}";
    }

    private static bool TryParseSwatchTint(AssetVariantSummary? selectedVariant, out CanonicalColor tint)
    {
        tint = new CanonicalColor(0f, 0f, 0f, 1f);
        if (selectedVariant is null ||
            !string.Equals(selectedVariant.VariantKind, "Swatch", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(selectedVariant.SwatchHex))
        {
            return false;
        }

        var normalized = selectedVariant.SwatchHex.Trim();
        if (normalized.StartsWith('#'))
        {
            normalized = normalized[1..];
        }

        if (!uint.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var argb))
        {
            return false;
        }

        if (normalized.Length == 6)
        {
            tint = new CanonicalColor(
                ((argb >> 16) & 0xFF) / 255f,
                ((argb >> 8) & 0xFF) / 255f,
                (argb & 0xFF) / 255f,
                1f);
            return true;
        }

        if (normalized.Length == 8)
        {
            tint = new CanonicalColor(
                ((argb >> 16) & 0xFF) / 255f,
                ((argb >> 8) & 0xFF) / 255f,
                (argb & 0xFF) / 255f,
                ((argb >> 24) & 0xFF) / 255f);
            return true;
        }

        return false;
    }
}

public interface IPackageScanner
{
    IAsyncEnumerable<DiscoveredPackage> DiscoverPackagesAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken);
}

public interface IResourceCatalogService
{
    Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken);
    Task<ResourceMetadata> EnrichResourceAsync(ResourceMetadata resource, CancellationToken cancellationToken);
    Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null);
    Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken);
    Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null);
}

public interface IAssetGraphBuilder
{
    IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan);
    Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken);
    Task<AssetGraph> BuildPreviewGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken);
}

public interface IPreviewService
{
    Task<PreviewResult> CreatePreviewAsync(ResourceMetadata resource, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null);
    Task<PreviewResult> CreatePreviewAsync(CasAssetGraph casGraph, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null);
}

public interface ITextureDecodeService
{
    Task<TextureDecodeResult> DecodeAsync(ResourceMetadata resource, CancellationToken cancellationToken);
}

public interface ISceneBuildService
{
    Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null);
    Task<SceneBuildResult> BuildSceneAsync(CasAssetGraph casGraph, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null);
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
    Task<IIndexWriteSession> OpenRebuildSessionAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken);
    Task<WindowedQueryResult<ResourceMetadata>> QueryResourcesAsync(RawResourceBrowserQuery query, CancellationToken cancellationToken);
    Task<WindowedQueryResult<AssetSummary>> QueryAssetsAsync(AssetBrowserQuery query, CancellationToken cancellationToken);
    Task<IReadOnlyList<DataSourceDefinition>> GetDataSourcesAsync(CancellationToken cancellationToken);
    Task<AssetFacetOptions> GetAssetFacetOptionsAsync(AssetKind assetKind, CancellationToken cancellationToken);
    Task<IReadOnlyList<IndexedPackageRecord>> GetIndexedPackagesAsync(IEnumerable<Guid> dataSourceIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceMetadata>> GetPackageResourcesAsync(string packagePath, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceMetadata>> GetResourcesByInstanceAsync(string packagePath, ulong fullInstance, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceMetadata>> GetResourcesByFullInstanceAsync(ulong fullInstance, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceMetadata>> GetCasPartResourcesByInstancesAsync(IEnumerable<ulong> fullInstances, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetSummary>> GetPackageAssetsAsync(string packagePath, CancellationToken cancellationToken);
    Task<AssetSummary?> GetPackageAssetByIdAsync(string packagePath, Guid assetId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetVariantSummary>> GetAssetVariantsAsync(Guid assetId, CancellationToken cancellationToken);
    Task<ResourceMetadata?> GetResourceByTgiAsync(string packagePath, string fullTgi, CancellationToken cancellationToken);
    Task<IReadOnlyList<ResourceMetadata>> GetResourcesByTgiAsync(string fullTgi, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssetSummary>> GetIndexedDefaultBodyRecipeAssetsAsync(SimInfoSummary metadata, string slotCategory, CancellationToken cancellationToken);
    Task<IReadOnlyList<SimTemplateFactSummary>> GetSimTemplateFactsByArchetypeAsync(string archetypeKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<SimTemplateBodyPartFact>> GetSimTemplateBodyPartFactsAsync(Guid resourceId, CancellationToken cancellationToken);
    Task UpdatePackageAssetsAsync(Guid dataSourceId, string packagePath, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken);
}

public interface IIndexWriteSession : IAsyncDisposable
{
    Task ReplacePackageAsync(PackageScanResult packageScan, IReadOnlyList<AssetSummary> assets, CancellationToken cancellationToken);
    Task ReplacePackagesAsync(IReadOnlyList<(PackageScanResult PackageScan, IReadOnlyList<AssetSummary> Assets)> batch, CancellationToken cancellationToken);
    Task FinalizeAsync(IProgress<IndexWriteStageProgress>? progress, CancellationToken cancellationToken);
}

public interface IIndexWriteSessionMetricsProvider
{
    IndexWriteMetrics? ConsumeLastMetrics();
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
