using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Sims4ResourceExplorer.App.Services;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Indexing;
using Windows.Storage.Streams;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const int DefaultResultWindowSize = 200;
    private const int SimBodyCandidateProbeBatchSize = 3;

    private readonly IIndexStore indexStore;
    private readonly PackageIndexCoordinator packageIndexCoordinator;
    private readonly IPreviewService previewService;
    private readonly IRawExportService rawExportService;
    private readonly IFbxExportService fbxExportService;
    private readonly IAssetGraphBuilder assetGraphBuilder;
    private readonly IResourceMetadataEnrichmentService resourceMetadataEnrichmentService;
    private readonly IAudioPlayer audioPlayer;
    private readonly IndexingRunOptions defaultIndexingOptions;
    private readonly IAppPreferencesService appPreferencesService;
    private readonly IIndexingTelemetryRecorderService indexingTelemetryRecorderService;
    private readonly IAssetSessionLogService assetSessionLogService;
    private readonly ISystemMemoryService systemMemoryService;
    private readonly AssetBrowserState assetBrowserState = new();
    private readonly RawResourceBrowserState rawResourceBrowserState = new();
    private readonly Dictionary<string, CanonicalTexture?> assetVariantTextureCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<AssetSummary?>> simBodyCandidateAssetCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<(string PackagePath, ulong FullInstance), Task<IReadOnlyList<ResourceMetadata>>> simBodyPackageInstanceResourceCache = new();
    private readonly ConcurrentDictionary<string, Task<SimBodyCandidatePreviewBuildResult?>> simBodyCandidatePreviewCache = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? indexingCancellationTokenSource;
    private CancellationTokenSource? assetSearchDebounceTokenSource;
    private CancellationTokenSource? rawSearchDebounceTokenSource;
    private ResourceMetadata? selectedResource;
    private AssetSummary? selectedAsset;
    private AssetGraph? selectedAssetGraph;
    private ScenePreviewContent? selectedAssetBaseScenePreview;
    private ScenePreviewContent? selectedAssetScenePreview;
    private string? selectedAssetDetailsSupplement;
    private IReadOnlyList<AssetVariantSummary> selectedAssetVariants = [];
    private ResourceMetadata? selectedAssetSceneRoot;
    private bool suppressSceneLodSelectionPreview;
    private bool suppressAssetVariantSelectionUpdates;
    private long assetVariantThumbnailLoadGeneration;
    private long assetVariantPreviewLoadGeneration;
    private byte[]? previewAudioBytes;
    private bool assetQueryDirty = true;
    private bool rawQueryDirty = true;
    private bool assetBrowserHasLoaded;
    private bool rawBrowserHasLoaded;
    private int assetTotalCount;
    private int rawTotalCount;
    private bool suspendBrowserQueriesDuringIndexing;
    private bool suppressSimTemplateSelectionUpdates;
    private string? lastSimBodyProxyDiagnostics;
    private SimAssemblyPlanSummary? lastSimAssemblyPlan;
    private SimAssemblyGraphSummary? lastSimAssemblyGraph;

    private sealed record SimBodyCandidatePreviewBuildResult(
        ScenePreviewContent? Preview,
        ResourceMetadata? SceneRoot,
        IReadOnlyList<ResourceMetadata> RigResources,
        IReadOnlyList<CasRegionMapSummary> RegionMaps,
        string Diagnostics)
    {
        public bool IsUsable => Preview?.Scene is not null && SceneRoot is not null;
    }

    private sealed record SimBodyPreviewLayerResolutionResult(
        AssetSummary Asset,
        ScenePreviewContent Preview,
        ResourceMetadata SceneRoot,
        IReadOnlyList<ResourceMetadata> RigResources,
        IReadOnlyList<CasRegionMapSummary> RegionMaps,
        IReadOnlyList<string> Diagnostics);

    private sealed record SimBodyPreviewLayerProbeResult(
        SimBodyPreviewLayerResolutionResult? Resolution,
        IReadOnlyList<string> Diagnostics);

    public MainViewModel(
        IIndexStore indexStore,
        PackageIndexCoordinator packageIndexCoordinator,
        IPreviewService previewService,
        IRawExportService rawExportService,
        IFbxExportService fbxExportService,
        IAssetGraphBuilder assetGraphBuilder,
        IResourceMetadataEnrichmentService resourceMetadataEnrichmentService,
        IAudioPlayer audioPlayer,
        IndexingRunOptions defaultIndexingOptions,
        IAppPreferencesService appPreferencesService,
        IIndexingTelemetryRecorderService indexingTelemetryRecorderService,
        IAssetSessionLogService assetSessionLogService,
        ISystemMemoryService systemMemoryService)
    {
        this.indexStore = indexStore;
        this.packageIndexCoordinator = packageIndexCoordinator;
        this.previewService = previewService;
        this.rawExportService = rawExportService;
        this.fbxExportService = fbxExportService;
        this.assetGraphBuilder = assetGraphBuilder;
        this.resourceMetadataEnrichmentService = resourceMetadataEnrichmentService;
        this.audioPlayer = audioPlayer;
        this.defaultIndexingOptions = defaultIndexingOptions;
        this.appPreferencesService = appPreferencesService;
        this.indexingTelemetryRecorderService = indexingTelemetryRecorderService;
        this.assetSessionLogService = assetSessionLogService;
        this.systemMemoryService = systemMemoryService;

        AvailableWorkerCounts = Enumerable.Range(1, IndexingRunOptions.GetMachineWorkerLimit()).ToArray();
        AvailableMemoryUsagePercents = IndexingMemoryBudgetCalculator.SupportedPercents.ToArray();
        BrowserModes = Enum.GetValues<BrowserMode>();
        AssetDomains = Enum.GetValues<AssetBrowserDomain>();
        AssetSortOptions = Enum.GetValues<AssetBrowserSort>();
        RawDomains = Enum.GetValues<RawResourceDomain>();
        RawSortOptions = Enum.GetValues<RawResourceSort>();
        LinkFilters = Enum.GetValues<ResourceLinkFilter>();
        selectedWorkerCount = defaultIndexingOptions.MaxPackageConcurrency;
        selectedMemoryUsagePercent = IndexingMemoryBudgetCalculator.NormalizePercent(25);
        UpdateBrowsePresentation();
        AppendLog($"Asset session log: {assetSessionLogService.LogPath}");
    }

    public ObservableCollection<DataSourceDefinition> DataSources { get; } = [];
    public ObservableCollection<ResourceMetadata> RawResources { get; } = [];
    public ObservableCollection<AssetSummary> AssetResults { get; } = [];
    public ObservableCollection<FilterChip> ActiveFilterChips { get; } = [];
    public ObservableCollection<string> AssetCategories { get; } = [];
    public ObservableCollection<string> AssetRootTypes { get; } = [];
    public ObservableCollection<string> AssetIdentityTypes { get; } = [];
    public ObservableCollection<string> AssetPrimaryGeometryTypes { get; } = [];
    public ObservableCollection<string> AssetThumbnailTypes { get; } = [];
    public ObservableCollection<string> AssetCatalogSignal0020Values { get; } = [];
    public ObservableCollection<string> AssetCatalogSignal002CValues { get; } = [];
    public ObservableCollection<string> AssetCatalogSignal0030Values { get; } = [];
    public ObservableCollection<string> AssetCatalogSignal0034Values { get; } = [];
    public ObservableCollection<SceneLodOption> AvailableSceneLods { get; } = [];
    public ObservableCollection<string> AvailableSceneTextureSlots { get; } = [];
    public ObservableCollection<AssetVariantPickerItem> AssetVariantItems { get; } = [];
    public ObservableCollection<SimBodyFoundationSummary> SelectedSimBodyFoundation { get; } = [];
    public ObservableCollection<SimBodySourceSummary> SelectedSimBodySources { get; } = [];
    public ObservableCollection<SimBodyCandidateSummary> SelectedSimBodyCandidates { get; } = [];
    public ObservableCollection<SimBodyCandidateFamilyPickerItem> SelectedSimBodyCandidateFamilies { get; } = [];
    public ObservableCollection<SimBodyAssemblyRecipeItem> SelectedSimBodyAssemblyRecipe { get; } = [];
    public ObservableCollection<SimBodyGraphNodeSummary> SelectedSimBodyGraphNodes { get; } = [];
    public ObservableCollection<SimBodyGraphLayerItem> SelectedSimBodyGraphLayers { get; } = [];
    public ObservableCollection<SimBodyPreviewLayerItem> SelectedSimBodyPreviewLayers { get; } = [];
    public ObservableCollection<SimBodyPreviewCoverageItem> SelectedSimBodyPreviewCoverage { get; } = [];
    public ObservableCollection<SimTemplatePickerItem> SelectedSimTemplateOptions { get; } = [];
    public ObservableCollection<SimSlotGroupSummary> SelectedSimSlotGroups { get; } = [];
    public ObservableCollection<SimMorphGroupSummary> SelectedSimMorphGroups { get; } = [];
    public ObservableCollection<SimCasSlotCandidateSummary> SelectedSimCasSlotCandidates { get; } = [];
    public ObservableCollection<SimCasSlotFamilyPickerItem> SelectedSimCasSlotFamilies { get; } = [];
    public ObservableLog Log { get; } = [];
    public IReadOnlyList<int> AvailableWorkerCounts { get; }
    public IReadOnlyList<int> AvailableMemoryUsagePercents { get; }
    public IReadOnlyList<BrowserMode> BrowserModes { get; }
    public IReadOnlyList<AssetBrowserDomain> AssetDomains { get; }
    public IReadOnlyList<AssetBrowserSort> AssetSortOptions { get; }
    public IReadOnlyList<RawResourceDomain> RawDomains { get; }
    public IReadOnlyList<RawResourceSort> RawSortOptions { get; }
    public IReadOnlyList<ResourceLinkFilter> LinkFilters { get; }
    public IReadOnlyList<SceneRenderMode> SceneRenderModes { get; } = Enum.GetValues<SceneRenderMode>();

    [ObservableProperty]
    private BrowserMode selectedBrowserMode = BrowserMode.AssetBrowser;

    [ObservableProperty]
    private bool includeGameSources = true;

    [ObservableProperty]
    private bool includeDlcSources = true;

    [ObservableProperty]
    private bool includeModsSources = true;

    [ObservableProperty]
    private string assetSearchText = string.Empty;

    [ObservableProperty]
    private AssetBrowserDomain assetDomain = AssetBrowserDomain.BuildBuy;

    [ObservableProperty]
    private string assetCategoryText = string.Empty;

    [ObservableProperty]
    private string assetRootTypeText = string.Empty;

    [ObservableProperty]
    private string assetIdentityTypeText = string.Empty;

    [ObservableProperty]
    private string assetPrimaryGeometryTypeText = string.Empty;

    [ObservableProperty]
    private string assetThumbnailTypeText = string.Empty;

    [ObservableProperty]
    private string assetCatalogSignal0020Text = string.Empty;

    [ObservableProperty]
    private string assetCatalogSignal002CText = string.Empty;

    [ObservableProperty]
    private string assetCatalogSignal0030Text = string.Empty;

    [ObservableProperty]
    private string assetCatalogSignal0034Text = string.Empty;

    [ObservableProperty]
    private string assetPackageText = string.Empty;

    [ObservableProperty]
    private string assetPackageRelativeText = string.Empty;

    [ObservableProperty]
    private bool assetHasThumbnailOnly;

    [ObservableProperty]
    private bool assetVariantsOnly;

    [ObservableProperty]
    private bool assetRequireSceneRoot;

    [ObservableProperty]
    private bool assetRequireExactGeometryCandidate;

    [ObservableProperty]
    private bool assetRequireMaterialReferences;

    [ObservableProperty]
    private bool assetRequireTextureReferences;

    [ObservableProperty]
    private bool assetRequireIdentityMetadata;

    [ObservableProperty]
    private bool assetRequireRigReference;

    [ObservableProperty]
    private bool assetRequireGeometryReference;

    [ObservableProperty]
    private bool assetRequireMaterialResourceCandidate;

    [ObservableProperty]
    private bool assetRequireTextureResourceCandidate;

    [ObservableProperty]
    private bool assetRequirePackageLocalGraph;

    [ObservableProperty]
    private bool assetRequireDiagnostics;

    [ObservableProperty]
    private AssetBrowserSort assetSort = AssetBrowserSort.Name;

    [ObservableProperty]
    private string rawSearchText = string.Empty;

    [ObservableProperty]
    private RawResourceDomain rawDomain = RawResourceDomain.All;

    [ObservableProperty]
    private string rawTypeNameText = string.Empty;

    [ObservableProperty]
    private string rawPackageText = string.Empty;

    [ObservableProperty]
    private string rawGroupHexText = string.Empty;

    [ObservableProperty]
    private string rawInstanceHexText = string.Empty;

    [ObservableProperty]
    private bool rawPreviewableOnly;

    [ObservableProperty]
    private bool rawExportCapableOnly;

    [ObservableProperty]
    private bool rawCompressedKnownOnly;

    [ObservableProperty]
    private ResourceLinkFilter rawLinkFilter;

    [ObservableProperty]
    private RawResourceSort rawSort = RawResourceSort.TypeName;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isQueryingResults;

    [ObservableProperty]
    private string statusMessage = "Ready.";

    [ObservableProperty]
    private int selectedWorkerCount;

    [ObservableProperty]
    private int selectedMemoryUsagePercent;

    [ObservableProperty]
    private string previewText = "Select a resource or asset to inspect its preview.";

    [ObservableProperty]
    private string detailsText = "Metadata will appear here.";

    [ObservableProperty]
    private BitmapImage? previewImageSource;

    [ObservableProperty]
    private CanonicalScene? currentScene;

    [ObservableProperty]
    private PreviewSurfaceMode previewSurfaceMode = PreviewSurfaceMode.Diagnostics;

    [ObservableProperty]
    private string previewSurfaceTitle = "Diagnostics";

    [ObservableProperty]
    private bool isPreviewLoading;

    [ObservableProperty]
    private string previewLoadStatus = string.Empty;

    [ObservableProperty]
    private double previewLoadProgressMaximum = 1d;

    [ObservableProperty]
    private double previewLoadProgressValue;

    [ObservableProperty]
    private bool previewLoadProgressIndeterminate;

    [ObservableProperty]
    private bool previewLoadCompleted;

    [ObservableProperty]
    private int selectedPreviewDiagnosticsTabIndex;

    [ObservableProperty]
    private int selectedSimArchetypeTabIndex;

    [ObservableProperty]
    private SimTemplatePickerItem? selectedSimTemplateOption;

    [ObservableProperty]
    private string resultSummary = "Choose a scope to begin.";

    [ObservableProperty]
    private string emptyStateMessage = "Choose a scope to begin.";

    [ObservableProperty]
    private SceneRenderMode selectedSceneRenderMode = SceneRenderMode.LitTexture;

    [ObservableProperty]
    private SceneLodOption? selectedSceneLod;

    [ObservableProperty]
    private string selectedSceneTextureSlot = "All";

    [ObservableProperty]
    private AssetVariantPickerItem? selectedAssetVariant;

    [ObservableProperty]
    private BitmapImage? selectedAssetVariantThumbnailSource;

    [ObservableProperty]
    private string selectedAssetVariantThumbnailText = "Select a logical asset to inspect indexed variant data.";

    public bool IsNotBusy => !IsBusy;
    public Visibility AssetBrowserVisibility => SelectedBrowserMode == BrowserMode.AssetBrowser ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RawBrowserVisibility => SelectedBrowserMode == BrowserMode.RawResourceBrowser ? Visibility.Visible : Visibility.Collapsed;
    public bool IsAssetBrowserActive => SelectedBrowserMode == BrowserMode.AssetBrowser;
    public bool IsRawResourceBrowserActive => SelectedBrowserMode == BrowserMode.RawResourceBrowser;
    public bool HasMoreResults => SelectedBrowserMode == BrowserMode.AssetBrowser
        ? AssetResults.Count < assetTotalCount
        : RawResources.Count < rawTotalCount;
    public bool HasVisibleResults => SelectedBrowserMode == BrowserMode.AssetBrowser
        ? AssetResults.Count > 0
        : RawResources.Count > 0;
    public Visibility EmptyStateVisibility => HasVisibleResults ? Visibility.Collapsed : Visibility.Visible;
    public string AssetSearchPlaceholder => AssetDomain switch
    {
        AssetBrowserDomain.BuildBuy => "Search build/buy name, description, category, or package",
        AssetBrowserDomain.Cas => "Search CAS name, description, type, or package",
        AssetBrowserDomain.Sim => "Search Sim archetypes, templates, species, age, gender, traits, or package",
        AssetBrowserDomain.General3D => "Search General 3D name, root type, geometry type, or package",
        _ => "Search assets"
    };
    public string RawSearchPlaceholder => "Search package path, type name, TGI, group, or instance";
    public string AssetSearchHelp => AssetDomain switch
    {
        AssetBrowserDomain.BuildBuy => "Build/Buy uses identity, structure, raw catalog signals, and capability filters.",
        AssetBrowserDomain.Cas => "CAS uses slot categories, compatibility metadata, structure, and capability filters; raw catalog signals are hidden.",
        AssetBrowserDomain.Sim => "Sim Archetypes groups classified SimInfo rows into top-level species/age/gender archetypes with template variations, body-first metadata, and first-pass CAS slot-family metadata.",
        AssetBrowserDomain.General3D => "General 3D focuses on standalone Model, ModelLOD, and Geometry roots plus package-local structure.",
        _ => "Asset Browser uses identity and structure filters."
    };
    public string RawSearchHelp => "Examples: objectcatalog, 00B2D882, 00000000, package fragment";
    public string AssetFacetSummaryText => AssetDomain switch
    {
        AssetBrowserDomain.BuildBuy => "Build/Buy catalog filters combine human-facing metadata with raw ObjectCatalog signals.",
        AssetBrowserDomain.Cas => "CAS filters now expose slot-family categories plus indexed compatibility metadata, alongside identity, structure, and preview support.",
        AssetBrowserDomain.Sim => "Sim Archetypes currently exposes grouped SimInfo-derived archetypes with template variations, a body-first inspector, and first-pass human CAS slot families while unclassified SimInfo rows stay hidden; real named characters, presets, occult forms, slot editing, and full outfit/body assembly are still in progress.",
        AssetBrowserDomain.General3D => "General 3D surfaces standalone Model, ModelLOD, and Geometry roots that are not currently harmonized into Build/Buy or CAS.",
        _ => "Asset filters stay focused on identity, structure, and preview support."
    };
    public Visibility AssetCatalogSignalsVisibility => AssetDomain == AssetBrowserDomain.BuildBuy ? Visibility.Visible : Visibility.Collapsed;
    public string EffectivePreviewLoadStatus => string.IsNullOrWhiteSpace(PreviewLoadStatus) ? "Ready" : PreviewLoadStatus;
    public Brush PreviewLoadBrush => new SolidColorBrush(PreviewLoadCompleted && !IsPreviewLoading ? Colors.ForestGreen : Colors.DodgerBlue);
    public bool IsScenePreviewActive => PreviewSurfaceMode == PreviewSurfaceMode.Scene && CurrentScene is not null;
    public bool IsImagePreviewActive => PreviewSurfaceMode == PreviewSurfaceMode.Image && PreviewImageSource is not null;
    public bool IsDiagnosticsPreviewActive => PreviewSurfaceMode == PreviewSurfaceMode.Diagnostics;
    public Visibility DiagnosticsPreviewVisibility => IsDiagnosticsPreviewActive ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DiagnosticsTabsVisibility => !string.IsNullOrWhiteSpace(PreviewText) || !string.IsNullOrWhiteSpace(DetailsText) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SimArchetypeInspectorVisibility =>
        selectedAsset?.AssetKind == AssetKind.Sim && (selectedAssetGraph?.SimGraph is not null || SelectedSimBodyFoundation.Count > 0 || SelectedSimBodySources.Count > 0 || SelectedSimBodyCandidateFamilies.Count > 0 || SelectedSimBodyAssemblyRecipe.Count > 0 || SelectedSimBodyGraphNodes.Count > 0 || SelectedSimBodyGraphLayers.Count > 0 || SelectedSimSlotGroups.Count > 0 || SelectedSimMorphGroups.Count > 0 || SelectedSimCasSlotFamilies.Count > 0)
            ? Visibility.Visible
            : Visibility.Collapsed;
    public bool CanResetView => CurrentScene is not null;
    public bool CanExportSelectedAsset => PreviewInteractionPolicy.CanExportSelectedAsset(selectedAsset, selectedAssetGraph, selectedAssetSceneRoot, CurrentScene);
    public bool HasSceneLodOptions => AvailableSceneLods.Count > 0;
    public bool CanSelectSceneLod => AvailableSceneLods.Count > 1;
    public bool HasSceneTextureSlotOptions => AvailableSceneTextureSlots.Count > 0;
    public bool CanSelectSceneTextureSlot => AvailableSceneTextureSlots.Count > 1;
    public bool HasAssetVariantOptions => AssetVariantItems.Count > 0;
    public bool CanSelectAssetVariant => AssetVariantItems.Count > 1;
    public Visibility AssetVariantPickerVisibility => HasAssetVariantOptions ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectedAssetVariantThumbnailVisibility => SelectedAssetVariantThumbnailSource is null ? Visibility.Collapsed : Visibility.Visible;
    public string AssetVariantPickerSummary => BuildAssetVariantPickerSummary();
    public string SimArchetypeInspectorSummary => BuildSimArchetypeInspectorSummary();
    public string SimBodyAssemblyModeText => BuildSimBodyAssemblyModeText();
    public string SimBodyGraphSummaryText => BuildSimBodyGraphSummaryText();
    public string SimResolvedBodyGraphSummaryText => BuildSimResolvedBodyGraphSummaryText();
    public string SimResolvedBodyGraphDetailsText => BuildSimResolvedBodyGraphDetailsText();
    public string SimBodyPreviewCoverageText => BuildSimBodyPreviewCoverageText();
    public string SelectedSimTemplateNotesText => SelectedSimTemplateOption?.Notes ?? "No template metadata is available yet.";
    public string SelectedSimTemplateDetailsText => SelectedSimTemplateOption?.DetailsText ?? string.Empty;
    public string SelectedAssetVariantDescriptor => BuildSelectedAssetVariantDescriptor();
    public string SelectedAssetVariantMetadataText => BuildSelectedAssetVariantMetadataText();

    public async Task InitializeAsync()
    {
        await indexStore.InitializeAsync(CancellationToken.None);
        var preferences = await appPreferencesService.LoadAsync(CancellationToken.None);
        SelectedWorkerCount = IndexingRunOptions.ClampWorkerCount(preferences.IndexWorkerCount);
        SelectedMemoryUsagePercent = IndexingMemoryBudgetCalculator.NormalizePercent(preferences.IndexMemoryUsagePercent);
        await ReloadSourcesAsync();
        await ReloadAssetFacetOptionsAsync();
        await RefreshActiveBrowserAsync(resetWindow: true);
    }

    public IndexingDialogViewModel CreateIndexingDialogViewModel() =>
        new(
            DataSources.ToArray(),
            AvailableWorkerCounts,
            SelectedWorkerCount,
            SelectedMemoryUsagePercent,
            BuildIndexingMemoryBudgetSummary,
            CancelIndexing);

    public async Task ApplyIndexingConfigurationAsync(
        IReadOnlyList<DataSourceDefinition> configuredSources,
        int workerCount,
        int memoryUsagePercent)
    {
        ReplaceCollection(DataSources, configuredSources);
        SelectedWorkerCount = IndexingRunOptions.ClampWorkerCount(workerCount);
        SelectedMemoryUsagePercent = IndexingMemoryBudgetCalculator.NormalizePercent(memoryUsagePercent);
        await indexStore.UpsertDataSourcesAsync(DataSources, CancellationToken.None);
        await appPreferencesService.SaveAsync(
            new AppPreferences(SelectedWorkerCount, SelectedMemoryUsagePercent),
            CancellationToken.None);
        MarkAllQueriesDirty();
        UpdateBrowsePresentation();
        AppendLog($"Saved {DataSources.Count:N0} configured index folder(s) | workers={SelectedWorkerCount} | memory={SelectedMemoryUsagePercent}%.");
    }

    public async Task RunIndexAsync(IndexingDialogViewModel indexingDialogViewModel)
    {
        if (IsBusy)
        {
            return;
        }

        if (DataSources.Count == 0)
        {
            StatusMessage = "Open Update Index and add at least one folder first.";
            return;
        }

        IsBusy = true;
        suspendBrowserQueriesDuringIndexing = true;
        CancelPendingBrowserQueries();
        ClearSimPreviewCaches();
        indexingCancellationTokenSource = new CancellationTokenSource();
        var packageByteCacheBudgetBytes = ResolvePackageByteCacheBudgetBytes(SelectedMemoryUsagePercent);
        StatusMessage = $"Indexing started with {SelectedWorkerCount} workers.";
        IndexingTelemetrySession? telemetrySession = null;

        try
        {
            await Task.Yield();
            await appPreferencesService.SaveAsync(new AppPreferences(SelectedWorkerCount, SelectedMemoryUsagePercent), CancellationToken.None);
            var snapshotSources = DataSources.ToArray();
            var selectedWorkerCount = SelectedWorkerCount;
            var indexingToken = indexingCancellationTokenSource.Token;
            AppendLog($"Index config: workers={selectedWorkerCount}, package-read cache={IndexingDisplayFormat.FormatBytes(packageByteCacheBudgetBytes)} ({SelectedMemoryUsagePercent}% target).");
            telemetrySession = await indexingTelemetryRecorderService
                .StartSessionAsync(snapshotSources, selectedWorkerCount, indexingToken);
            AppendLog($"Index telemetry: {telemetrySession.TimelinePath}");
            var progress = new Progress<IndexingProgress>(value => ApplyIndexingProgress(value, indexingDialogViewModel, telemetrySession));

            await Task.Run(
                async () => await packageIndexCoordinator.RunAsync(
                    snapshotSources,
                    progress,
                    indexingToken,
                    selectedWorkerCount,
                    packageByteCacheBudgetBytes).ConfigureAwait(false),
                indexingToken);

            await telemetrySession.CompleteAsync("completed", CancellationToken.None);
            indexingDialogViewModel.MarkCompleted("Indexing completed. Refreshing browser results...");
            suspendBrowserQueriesDuringIndexing = false;
            ClearSimPreviewCaches();
            MarkAllQueriesDirty();
            await ReloadAssetFacetOptionsAsync();
            await RefreshActiveBrowserAsync(resetWindow: true);
            StatusMessage = "Index complete.";
            indexingDialogViewModel.MarkCompleted("Indexing completed.");
        }
        catch (OperationCanceledException)
        {
            if (telemetrySession is not null)
            {
                await telemetrySession.CompleteAsync("canceled", CancellationToken.None);
            }
            suspendBrowserQueriesDuringIndexing = false;
            await RefreshBrowseStateAfterIndexingAsync();
            StatusMessage = "Indexing canceled.";
            AppendLog("Indexing canceled by user.");
            indexingDialogViewModel.MarkCanceled();
        }
        catch (Exception ex)
        {
            if (telemetrySession is not null)
            {
                await telemetrySession.CompleteAsync("failed", CancellationToken.None);
            }
            suspendBrowserQueriesDuringIndexing = false;
            await RefreshBrowseStateAfterIndexingAsync();
            StatusMessage = $"Indexing failed: {ex.Message}";
            AppendLog($"Indexing failed: {ex}");
            indexingDialogViewModel.MarkFailed($"Indexing failed: {ex.Message}");
        }
        finally
        {
            suspendBrowserQueriesDuringIndexing = false;
            if (telemetrySession is not null)
            {
                await telemetrySession.DisposeAsync();
            }
            IsBusy = false;
            indexingCancellationTokenSource?.Dispose();
            indexingCancellationTokenSource = null;
        }
    }

    public void CancelIndexing() => indexingCancellationTokenSource?.Cancel();

    public Task RefreshActiveBrowserAsync(bool resetWindow = true)
    {
        if (suspendBrowserQueriesDuringIndexing)
        {
            return Task.CompletedTask;
        }

        return SelectedBrowserMode == BrowserMode.AssetBrowser
            ? RefreshAssetBrowserAsync(resetWindow, append: !resetWindow)
            : RefreshRawBrowserAsync(resetWindow, append: !resetWindow);
    }

    public async Task LoadMoreAsync()
    {
        if (suspendBrowserQueriesDuringIndexing || !HasMoreResults || IsQueryingResults)
        {
            return;
        }

        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            await RefreshAssetBrowserAsync(resetWindow: false, append: true);
        }
        else
        {
            await RefreshRawBrowserAsync(resetWindow: false, append: true);
        }
    }

    public async Task ResetActiveFiltersAsync()
    {
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            assetBrowserState.ResetFilters();
            SyncAssetStateToProperties();
            assetQueryDirty = true;
        }
        else
        {
            rawResourceBrowserState.ResetFilters();
            SyncRawStateToProperties();
            rawQueryDirty = true;
        }

        UpdateBrowsePresentation();
        await RefreshActiveBrowserAsync(resetWindow: true);
    }

    public async Task RemoveActiveFilterAsync(string key)
    {
        if (key == "sourceScope")
        {
            IncludeGameSources = true;
            IncludeDlcSources = true;
            IncludeModsSources = true;
        }
        else if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            assetBrowserState.RemoveFilter(key);
            SyncAssetStateToProperties();
            assetQueryDirty = true;
        }
        else
        {
            rawResourceBrowserState.RemoveFilter(key);
            SyncRawStateToProperties();
            rawQueryDirty = true;
        }

        UpdateBrowsePresentation();
        await RefreshActiveBrowserAsync(resetWindow: true);
    }

    public async Task SelectResourceAsync(ResourceMetadata? resource)
    {
        if (resource is null)
        {
            return;
        }

        selectedResource = resource;
        selectedAsset = null;
        selectedAssetGraph = null;
        selectedAssetBaseScenePreview = null;
        selectedAssetScenePreview = null;
        selectedAssetDetailsSupplement = null;
        selectedAssetSceneRoot = null;
        ClearAssetVariantState();
        ClearSimArchetypeInspectorState();
        SetSceneLodOptions([]);
        ResetPreviewState();
        BeginPreviewLoad(3, "Resolving resource metadata...");
        await YieldToUiAsync();

        try
        {
            await AdvancePreviewLoadAsync(1, "Resolving resource metadata...");
            resource = await resourceMetadataEnrichmentService.EnrichAsync(resource, CancellationToken.None);
            selectedResource = resource;
            await AdvancePreviewLoadAsync(2, "Building preview...");
            var preview = await previewService.CreatePreviewAsync(resource, CancellationToken.None);
            await AdvancePreviewLoadAsync(3, "Applying preview...");
            await ApplyPreviewAsync(resource, preview, null);
            CompletePreviewLoad();
        }
        catch (Exception ex)
        {
            PreviewText = ex.ToString();
            DetailsText = BuildResourceDetails(resource);
            CompletePreviewLoad();
        }
    }

    public async Task SelectAssetAsync(AssetSummary? asset)
    {
        if (asset is null)
        {
            return;
        }

        selectedAsset = asset;
        selectedAssetGraph = null;
        selectedAssetBaseScenePreview = null;
        selectedAssetScenePreview = null;
        selectedAssetDetailsSupplement = null;
        selectedAssetVariants = [];
        selectedAssetSceneRoot = null;
        selectedResource = null;
        assetVariantTextureCache.Clear();
        ClearAssetVariantState();
        ClearSimArchetypeInspectorState();
        ResetPreviewState();
        BeginPreviewLoad(1d, "Scanning index...");
        await YieldToUiAsync();

        await AdvancePreviewLoadAsync(0.04, "Scanning index...");
        var packageResources = await indexStore.GetResourcesByInstanceAsync(asset.PackagePath, asset.RootKey.FullInstance, CancellationToken.None);
        await AdvancePreviewLoadAsync(0.08, "Loading variant data...");
        selectedAssetVariants = await indexStore.GetAssetVariantsAsync(asset.Id, CancellationToken.None);
        SetAssetVariantOptions(selectedAssetVariants);
        await AdvancePreviewLoadAsync(0.12, "Building asset graph...");
        var graph = asset.AssetKind == AssetKind.Sim
            ? await assetGraphBuilder.BuildPreviewGraphAsync(asset, packageResources, CancellationToken.None)
            : await assetGraphBuilder.BuildAssetGraphAsync(asset, packageResources, CancellationToken.None);
        selectedAssetGraph = graph;
        SetSimArchetypeInspectorState(graph.SimGraph, graph.SimGraph?.SimInfoResource.Id);
        if (asset.AssetKind == AssetKind.Sim &&
            graph.SimGraph is not null &&
            SelectedSimTemplateOption is not null &&
            SelectedSimTemplateOption.ResourceId != graph.SimGraph.SimInfoResource.Id)
        {
            await AdvancePreviewLoadAsync(0.14, "Selecting body-driving SimInfo template...");
            var refreshedGraph = await RebuildSimPreviewGraphForSelectedTemplateAsync(CancellationToken.None);
            if (refreshedGraph?.SimGraph is not null)
            {
                graph = refreshedGraph;
                selectedAssetGraph = graph;
                SetSimArchetypeInspectorState(graph.SimGraph, SelectedSimTemplateOption.ResourceId);
            }
        }

        var assetDetails = BuildCurrentAssetDetails();
        var fallbackPreviewResource = FindAssetFallbackPreviewResource(asset, graph, packageResources);
        await AdvancePreviewLoadAsync(0.18, "Resolving scene root...");

        ResourceMetadata? sceneRoot = null;
        string unsupportedMessage;
        switch (asset.AssetKind)
        {
            case AssetKind.BuildBuy:
            {
                var buildBuyGraph = graph.BuildBuyGraph;
                if (buildBuyGraph is null || !buildBuyGraph.IsSupported)
                {
                    unsupportedMessage = "This Build/Buy asset is outside the currently supported static-object subset.";
                }
                else
                {
                    sceneRoot = buildBuyGraph.ModelResource;
                    unsupportedMessage = string.Empty;
                }

                break;
            }

            case AssetKind.Cas:
            {
                var casGraph = graph.CasGraph;
                if (casGraph is null || !casGraph.IsSupported || casGraph.GeometryResource is null)
                {
                    unsupportedMessage = "This CAS asset is outside the currently supported human skinned subset.";
                }
                else
                {
                    sceneRoot = casGraph.GeometryResource;
                    unsupportedMessage = string.Empty;
                }

                break;
            }

            case AssetKind.Sim:
            {
                var simGraph = graph.SimGraph;
                selectedResource = simGraph?.SimInfoResource;
                unsupportedMessage = "This Sim archetype does not currently resolve a renderable body-first preview from the selected SimInfo template.";
                break;
            }

            case AssetKind.General3D:
            {
                var general3DGraph = graph.General3DGraph;
                if (general3DGraph is null || !general3DGraph.IsSupported)
                {
                    unsupportedMessage = "This General 3D asset does not currently expose a supported Model, ModelLOD, or Geometry scene root.";
                }
                else
                {
                    sceneRoot = general3DGraph.SceneRootResource;
                    unsupportedMessage = string.Empty;
                }

                break;
            }

            default:
                unsupportedMessage = $"Unsupported asset kind: {asset.AssetKind}.";
                break;
        }

        if (sceneRoot is null)
        {
            SetSceneLodOptions([]);
            selectedAssetScenePreview = null;
            if (asset.AssetKind == AssetKind.Sim)
            {
                if (await TryApplySimBodyProxyPreviewAsync(CancellationToken.None))
                {
                    CompletePreviewLoad();
                    return;
                }

                selectedAssetDetailsSupplement = string.Join(
                    Environment.NewLine,
                    new[] { BuildSelectedSimTemplateSupplement(), lastSimBodyProxyDiagnostics }
                        .Where(static text => !string.IsNullOrWhiteSpace(text)));
                assetDetails = BuildCurrentDisplayedAssetDetails();
                PreviewText = string.Join(
                    Environment.NewLine,
                    new[] { lastSimBodyProxyDiagnostics }
                        .Where(static text => !string.IsNullOrWhiteSpace(text))
                        .Concat(graph.Diagnostics.DefaultIfEmpty(unsupportedMessage)));
                DetailsText = assetDetails;
                PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
                PreviewSurfaceTitle = "Diagnostics";
                NotifyPreviewStateChanged();
                RecordCurrentAssetSessionDiagnostics("SelectAssetAsync:SimUnresolved");
                CompletePreviewLoad();
                return;
            }

            selectedAssetDetailsSupplement = BuildAssetPreviewFallbackSupplement(unsupportedMessage, graph.Diagnostics);
            assetDetails = BuildCurrentAssetDetails();
            await AdvancePreviewLoadAsync(4, "Building fallback preview...");
            var fallbackPreviewApplied = await TryApplyAssetFallbackPreviewAsync(fallbackPreviewResource, assetDetails);
            if (!fallbackPreviewApplied)
            {
                PreviewText = string.Join(Environment.NewLine, graph.Diagnostics.DefaultIfEmpty(unsupportedMessage));
                DetailsText = assetDetails;
                PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
                PreviewSurfaceTitle = "Diagnostics";
                NotifyPreviewStateChanged();
                RecordCurrentAssetSessionDiagnostics("SelectAssetAsync:AssetUnresolved");
            }

            CompletePreviewLoad();

            return;
        }

        selectedResource = sceneRoot;
        selectedAssetSceneRoot = sceneRoot;
        SetSceneLodOptions(BuildSceneLodOptions(graph, sceneRoot), sceneRoot.Key.FullTgi);

        await AdvancePreviewLoadAsync(0.24, "Preparing scene build...");
        var previewProgress = CreatePreviewBuildProgressReporter(0.24, 0.70);
        await AdvancePreviewLoadAsync(0.26, "Building 3D preview...");
        var preview = asset.AssetKind == AssetKind.Cas && graph.CasGraph is not null
            ? await BuildCasPreviewResultAsync(graph.CasGraph, CancellationToken.None, previewProgress)
            : await previewService.CreatePreviewAsync(sceneRoot, CancellationToken.None, previewProgress);
        selectedAssetBaseScenePreview = preview.Content as ScenePreviewContent;
        selectedAssetScenePreview = await BuildDisplayedScenePreviewAsync(selectedAssetBaseScenePreview, CancellationToken.None);
        selectedAssetDetailsSupplement = null;
        var assetDetailsWithScene = BuildCurrentAssetDetails();
        if (selectedAssetScenePreview is ScenePreviewContent { Scene: null } failedScenePreview)
        {
            selectedAssetDetailsSupplement = "Fallback: scene reconstruction failed, showing asset image preview instead.";
            assetDetailsWithScene = BuildCurrentAssetDetails();
            await AdvancePreviewLoadAsync(0.96, "Applying fallback preview...");
            var fallbackPreviewApplied = await TryApplyAssetFallbackPreviewAsync(fallbackPreviewResource, assetDetailsWithScene);
            if (fallbackPreviewApplied)
            {
                CompletePreviewLoad();
                return;
            }
        }

        await AdvancePreviewLoadAsync(0.96, "Applying preview...");
        await ApplyPreviewAsync(
            sceneRoot,
            new PreviewResult(preview.Kind, selectedAssetScenePreview ?? preview.Content),
            assetDetailsWithScene);
        CompletePreviewLoad();
    }

    private async Task RefreshSelectedSimBodyPreviewAsync()
    {
        if (selectedAsset?.AssetKind != AssetKind.Sim || selectedAssetGraph?.SimGraph is null)
        {
            return;
        }

        BeginPreviewLoad(1d, "Refreshing base-body preview...");
        await YieldToUiAsync();

        var applied = await TryApplySimBodyProxyPreviewAsync(CancellationToken.None);
        if (!applied)
        {
            selectedAssetSceneRoot = null;
            selectedAssetBaseScenePreview = null;
            selectedAssetScenePreview = null;
            SetSceneLodOptions([]);
            ReplaceCollection(SelectedSimBodyPreviewLayers, []);
            RebuildSimResolvedBodyGraphState();
            RebuildSimBodyPreviewCoverageState();
            NotifySimArchetypeInspectorStateChanged();
            selectedAssetDetailsSupplement = string.Join(
                Environment.NewLine,
                new[] { BuildSelectedSimTemplateSupplement(), lastSimBodyProxyDiagnostics }
                    .Where(static text => !string.IsNullOrWhiteSpace(text)));
            PreviewText = string.Join(
                Environment.NewLine,
                new[] { lastSimBodyProxyDiagnostics }
                    .Where(static text => !string.IsNullOrWhiteSpace(text))
                    .Concat(selectedAssetGraph.Diagnostics.DefaultIfEmpty("No base-body preview source is currently resolved for this Sim archetype.")));
            DetailsText = BuildCurrentDisplayedAssetDetails();
            PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
            PreviewSurfaceTitle = "Diagnostics";
            NotifyPreviewStateChanged();
            RecordCurrentAssetSessionDiagnostics("RefreshSelectedSimBodyPreviewAsync:Unresolved");
        }

        CompletePreviewLoad();
    }

    private async Task RefreshSelectedSimTemplateAsync()
    {
        if (selectedAsset?.AssetKind != AssetKind.Sim || SelectedSimTemplateOption is null)
        {
            return;
        }

        BeginPreviewLoad(1d, "Loading Sim archetype template...");
        await YieldToUiAsync();

        try
        {
            var refreshedGraph = await RebuildSimPreviewGraphForSelectedTemplateAsync(CancellationToken.None);
            if (refreshedGraph?.SimGraph is null)
            {
                return;
            }

            selectedAssetGraph = refreshedGraph;
            SetSimArchetypeInspectorState(refreshedGraph.SimGraph, SelectedSimTemplateOption.ResourceId);

            selectedResource = refreshedGraph.SimGraph.SimInfoResource;
            selectedAssetSceneRoot = null;
            selectedAssetBaseScenePreview = null;
            selectedAssetScenePreview = null;
            SetSceneLodOptions([]);

            var applied = await TryApplySimBodyProxyPreviewAsync(CancellationToken.None);
            if (!applied)
            {
                ReplaceCollection(SelectedSimBodyPreviewLayers, []);
                RebuildSimResolvedBodyGraphState();
                RebuildSimBodyPreviewCoverageState();
                NotifySimArchetypeInspectorStateChanged();
                selectedAssetDetailsSupplement = string.Join(
                    Environment.NewLine,
                    new[] { BuildSelectedSimTemplateSupplement(), lastSimBodyProxyDiagnostics }
                        .Where(static text => !string.IsNullOrWhiteSpace(text)));
                PreviewText = string.Join(
                    Environment.NewLine,
                    new[] { lastSimBodyProxyDiagnostics }
                        .Where(static text => !string.IsNullOrWhiteSpace(text))
                        .Concat(refreshedGraph.Diagnostics.DefaultIfEmpty("No base-body preview source is currently resolved for the selected SimInfo template.")));
                DetailsText = BuildCurrentDisplayedAssetDetails();
                PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
                PreviewSurfaceTitle = "Diagnostics";
                NotifyPreviewStateChanged();
                RecordCurrentAssetSessionDiagnostics("RefreshSelectedSimTemplateAsync:Unresolved");
            }
        }
        finally
        {
            CompletePreviewLoad();
        }
    }

    private async Task<bool> TryApplySimBodyProxyPreviewAsync(CancellationToken cancellationToken)
    {
        if (selectedAsset?.AssetKind != AssetKind.Sim || selectedAssetGraph?.SimGraph is null)
        {
            return false;
        }

        lastSimBodyProxyDiagnostics = null;
        lastSimAssemblyPlan = null;
        lastSimAssemblyGraph = null;
        var preferredBodyFamilies = GetPreferredSimBodyPreviewFamilies();
        if (preferredBodyFamilies.Count == 0)
        {
            return false;
        }

        await AdvancePreviewLoadAsync(0.30, preferredBodyFamilies.Count == 1 ? "Resolving active base-body layer..." : "Resolving active base-body layers...");

        ResourceMetadata? primarySceneRoot = null;
        var successfulBodyLayers = new List<(
            SimBodyCandidateFamilyPickerItem Family,
            AssetSummary Asset,
            ScenePreviewContent Preview,
            ResourceMetadata SceneRoot,
            IReadOnlyList<ResourceMetadata> RigResources,
            IReadOnlyList<CasRegionMapSummary> RegionMaps)>();
        var previewDiagnostics = new List<string>();
        for (var familyIndex = 0; familyIndex < preferredBodyFamilies.Count; familyIndex++)
        {
            var preferredFamily = preferredBodyFamilies[familyIndex];
            var familyStepStart = InterpolatePreviewStep(0.30, 0.58, familyIndex, preferredBodyFamilies.Count);
            var familyStepSearch = InterpolatePreviewStep(0.30, 0.58, familyIndex, preferredBodyFamilies.Count, 0.18);
            var familyStepEnd = InterpolatePreviewStep(0.30, 0.58, familyIndex, preferredBodyFamilies.Count, 0.95);
            await AdvancePreviewLoadAsync(familyStepStart, $"Resolving {preferredFamily.Label} layer...");
            await AdvancePreviewLoadAsync(familyStepSearch, $"Building {preferredFamily.Label} layer graph...");
            var resolvedLayerProbe = await ResolveSimBodyPreviewLayerAsync(
                preferredFamily,
                familyStepSearch,
                familyStepEnd,
                "Resolving",
                cancellationToken);
            previewDiagnostics.AddRange(resolvedLayerProbe.Diagnostics);
            var resolvedLayer = resolvedLayerProbe.Resolution;
            if (resolvedLayer is null)
            {
                previewDiagnostics.Add($"No usable body asset preview was built for active layer family {preferredFamily.Label}.");
                continue;
            }
            successfulBodyLayers.Add((
                preferredFamily,
                resolvedLayer.Asset,
                resolvedLayer.Preview,
                resolvedLayer.SceneRoot,
                resolvedLayer.RigResources,
                resolvedLayer.RegionMaps));
            primarySceneRoot ??= resolvedLayer.SceneRoot;
        }

        if (primarySceneRoot is null || successfulBodyLayers.Count == 0)
        {
            previewDiagnostics.Add($"No usable base-body preview was built for {preferredBodyFamilies.Count} active layer family/families.");
            lastSimBodyProxyDiagnostics = string.Join(
                Environment.NewLine,
                previewDiagnostics.Where(static text => !string.IsNullOrWhiteSpace(text)));
            lastSimAssemblyPlan = null;
            lastSimAssemblyGraph = null;
            ReplaceCollection(SelectedSimBodyPreviewLayers, []);
            RebuildSimResolvedBodyGraphState();
            RebuildSimBodyPreviewCoverageState();
            NotifySimArchetypeInspectorStateChanged();
            return false;
        }

        selectedResource = primarySceneRoot;
        selectedAssetSceneRoot = null;
        SetSceneLodOptions([]);

        var successfulPreview = successfulBodyLayers.Count == 1
            ? successfulBodyLayers[0].Preview
            : CanonicalSceneComposer.Compose(
                $"{selectedAsset.DisplayName} Base Body Shell",
                successfulBodyLayers.Select(static layer => layer.Preview).ToArray());
        var successfulBodyRigResources = successfulBodyLayers
            .SelectMany(static layer => layer.RigResources)
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var successfulBodyRegionMaps = successfulBodyLayers
            .SelectMany(static layer => layer.RegionMaps)
            .GroupBy(static summary => summary.ResourceTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        if (successfulBodyLayers.Count > 1)
        {
            previewDiagnostics.Add(
                $"Composed split base-body preview from {successfulBodyLayers.Count:N0} active layer(s): {string.Join(", ", successfulBodyLayers.Select(static layer => layer.Family.Label))}.");
        }

        var previewLayers = successfulBodyLayers
            .Select((layer, index) => SimBodyPreviewLayerItem.Create(
                layer.Family.Label,
                layer.Asset,
                layer.SceneRoot,
                isPrimaryLayer: index == 0))
            .ToList();
        var previewAssets = successfulBodyLayers
            .Select(static layer => layer.Asset)
            .ToList();
        ScenePreviewContent? successfulHeadPreview = null;
        AssetSummary? successfulHeadAsset = null;
        ResourceMetadata? successfulHeadSceneRoot = null;
        IReadOnlyList<ResourceMetadata> successfulHeadRigResources = [];
        IReadOnlyList<CasRegionMapSummary> successfulHeadRegionMaps = [];

        var headFamily = GetPreferredSimBodyPreviewHeadFamily();
        if (headFamily?.SelectedOption is not null)
        {
            await AdvancePreviewLoadAsync(0.58, "Resolving head shell...");
            var resolvedHeadLayerProbe = await ResolveSimBodyPreviewLayerAsync(
                headFamily,
                0.58,
                0.72,
                "Resolving",
                cancellationToken);
            previewDiagnostics.AddRange(resolvedHeadLayerProbe.Diagnostics);
            var resolvedHeadLayer = resolvedHeadLayerProbe.Resolution;
            if (resolvedHeadLayer is not null)
            {
                successfulHeadPreview = resolvedHeadLayer.Preview;
                successfulHeadAsset = resolvedHeadLayer.Asset;
                successfulHeadSceneRoot = resolvedHeadLayer.SceneRoot;
                successfulHeadRigResources = resolvedHeadLayer.RigResources;
                successfulHeadRegionMaps = resolvedHeadLayer.RegionMaps;
            }
        }

        var assembledPreview = SimSceneComposer.ComposeBodyAndHead(
            $"{selectedAsset.DisplayName} Base Body",
            successfulPreview,
            successfulBodyRigResources,
            successfulHeadPreview,
            successfulHeadRigResources,
            selectedAssetGraph.SimGraph.Metadata,
            selectedAssetGraph.SimGraph.MorphGroups,
            selectedAssetGraph.SimGraph.SkintoneRender,
            successfulBodyRegionMaps,
            successfulHeadRegionMaps);
        lastSimAssemblyPlan = assembledPreview.Plan;
        lastSimAssemblyGraph = assembledPreview.Graph;
        if (assembledPreview.IncludesHeadShell &&
            successfulHeadAsset is not null &&
            successfulHeadSceneRoot is not null &&
            headFamily is not null)
        {
            previewAssets.Add(successfulHeadAsset);
            previewLayers.Add(
                SimBodyPreviewLayerItem.Create(
                    headFamily.Label,
                    successfulHeadAsset,
                    successfulHeadSceneRoot,
                    isPrimaryLayer: false));
        }

        selectedAssetBaseScenePreview = assembledPreview.Preview with
        {
            Diagnostics = JoinDistinctDiagnosticLines(
                assembledPreview.Preview.Diagnostics,
                string.Join(Environment.NewLine, previewDiagnostics.Where(static text => !string.IsNullOrWhiteSpace(text))))
        };
        ReplaceCollection(SelectedSimBodyPreviewLayers, previewLayers);
        RebuildSimResolvedBodyGraphState();
        RebuildSimBodyPreviewCoverageState();
        NotifySimArchetypeInspectorStateChanged();

        await AdvancePreviewLoadAsync(0.74, "Preparing assembled Sim preview...");
        await AdvancePreviewLoadAsync(0.82, "Adapting assembled scene for display...");
        selectedAssetScenePreview = await BuildDisplayedScenePreviewAsync(selectedAssetBaseScenePreview, cancellationToken);
        selectedAssetDetailsSupplement = string.Join(
            Environment.NewLine,
            new[]
            {
                BuildSelectedSimTemplateSupplement(),
                BuildSimBodyProxySourceSummary(previewAssets),
                BuildSimBodyPreviewLayerSummary(SelectedSimBodyPreviewLayers),
                selectedAssetBaseScenePreview.Diagnostics
            }.Where(static text => !string.IsNullOrWhiteSpace(text)));
        lastSimBodyProxyDiagnostics = selectedAssetBaseScenePreview.Diagnostics;
        var assetDetails = BuildCurrentAssetDetails();

        await AdvancePreviewLoadAsync(0.90, "Preparing diagnostics and scene details...");
        await AdvancePreviewLoadAsync(0.96, "Applying base-body preview...");
        await ApplyPreviewAsync(
            primarySceneRoot,
            new PreviewResult(PreviewKind.Scene, selectedAssetScenePreview ?? selectedAssetBaseScenePreview!),
            assetDetails);
        return true;
    }

    private async Task<AssetGraph?> RebuildSimGraphForSelectedTemplateAsync(CancellationToken cancellationToken)
    {
        if (selectedAsset?.AssetKind != AssetKind.Sim || SelectedSimTemplateOption is null)
        {
            return null;
        }

        await AdvancePreviewLoadAsync(0.18, "Resolving selected SimInfo template...");
        var templateResource = await indexStore.GetResourceByTgiAsync(
            SelectedSimTemplateOption.PackagePath,
            SelectedSimTemplateOption.RootTgi,
            cancellationToken);
        if (templateResource is null)
        {
            return null;
        }

        await AdvancePreviewLoadAsync(0.28, "Loading template package resources...");
        var packageResources = await indexStore.GetResourcesByInstanceAsync(
            templateResource.PackagePath,
            templateResource.Key.FullInstance,
            cancellationToken);
        var templateSummary = selectedAsset with
        {
            PackagePath = templateResource.PackagePath,
            PackageName = Path.GetFileName(templateResource.PackagePath),
            RootKey = templateResource.Key
        };

        await AdvancePreviewLoadAsync(0.40, "Building template body graph...");
        return await assetGraphBuilder.BuildAssetGraphAsync(templateSummary, packageResources, cancellationToken);
    }

    private async Task<AssetGraph?> RebuildSimPreviewGraphForSelectedTemplateAsync(CancellationToken cancellationToken)
    {
        if (selectedAsset?.AssetKind != AssetKind.Sim || SelectedSimTemplateOption is null)
        {
            return null;
        }

        await AdvancePreviewLoadAsync(0.18, "Resolving selected SimInfo template...");
        var templateResource = await indexStore.GetResourceByTgiAsync(
            SelectedSimTemplateOption.PackagePath,
            SelectedSimTemplateOption.RootTgi,
            cancellationToken);
        if (templateResource is null)
        {
            return null;
        }

        await AdvancePreviewLoadAsync(0.28, "Loading template package resources...");
        var packageResources = await indexStore.GetResourcesByInstanceAsync(
            templateResource.PackagePath,
            templateResource.Key.FullInstance,
            cancellationToken);
        var templateSummary = selectedAsset with
        {
            PackagePath = templateResource.PackagePath,
            PackageName = Path.GetFileName(templateResource.PackagePath),
            RootKey = templateResource.Key
        };

        await AdvancePreviewLoadAsync(0.40, "Building template preview graph...");
        return await assetGraphBuilder.BuildPreviewGraphAsync(templateSummary, packageResources, cancellationToken);
    }

    private string? BuildSelectedSimTemplateSupplement()
    {
        if (SelectedSimTemplateOption is null)
        {
            return null;
        }

        return $"Selected SimInfo template: {SelectedSimTemplateOption.DisplayLabel} from {SelectedSimTemplateOption.PackageName ?? Path.GetFileName(SelectedSimTemplateOption.PackagePath)}.";
    }

    private IReadOnlyList<SimBodyCandidateFamilyPickerItem> GetPreferredSimBodyPreviewFamilies()
    {
        var selectedFamilies = SelectedSimBodyCandidateFamilies
            .Where(static family => family.SelectedOption is not null)
            .ToArray();
        if (selectedFamilies.Length == 0)
        {
            return [];
        }

        var activeLabels = SimBodyAssemblyPolicy.ResolveActiveLabels(selectedFamilies.Select(static family => family.Label));
        return selectedFamilies
            .Where(family => activeLabels.Contains(family.Label))
            .OrderBy(static family => GetBodyRecipeSortOrder(family.Label))
            .ThenBy(static family => family.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private SimBodyCandidateFamilyPickerItem? GetPreferredSimBodyPreviewShellFamily() =>
        GetPreferredSimBodyPreviewFamilies()
            .FirstOrDefault(family => SimBodyAssemblyPolicy.IsShellFamilyLabel(family.Label));

    private SimBodyCandidateFamilyPickerItem? GetPreferredSimBodyPreviewHeadFamily() =>
        SelectedSimBodyCandidateFamilies.FirstOrDefault(family =>
            string.Equals(family.Label, "Head", StringComparison.OrdinalIgnoreCase) &&
            family.SelectedOption is not null);

    private static IReadOnlyList<SimCasSlotOptionPickerItem> GetPreferredSimBodyPreviewOptions(SimBodyCandidateFamilyPickerItem family)
    {
        return family.SelectedOption is null
            ? []
            : [family.SelectedOption];
    }

    private static int GetBodyRecipeSortOrder(string? label) => label switch
    {
        "Full Body" => 0,
        "Body" => 1,
        "Head" => 2,
        "Top" => 3,
        "Bottom" => 4,
        "Shoes" => 5,
        _ => 10
    };

    private async Task<AssetSummary?> ResolveSimBodyCandidateAssetAsync(SimCasSlotOptionPickerItem option, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheKey = BuildSimBodyCandidateCacheKey(option.PackagePath, option.RootTgi, option.AssetId);
        var cachedTask = simBodyCandidateAssetCache.GetOrAdd(
            cacheKey,
            static (_, state) => state.ViewModel.ResolveSimBodyCandidateAssetCoreAsync(state.Option, CancellationToken.None),
            (ViewModel: this, Option: option));

        try
        {
            return await cachedTask.WaitAsync(cancellationToken);
        }
        catch when (cachedTask.IsFaulted || cachedTask.IsCanceled)
        {
            simBodyCandidateAssetCache.TryRemove(new KeyValuePair<string, Task<AssetSummary?>>(cacheKey, cachedTask));
            throw;
        }
    }

    private async Task<AssetSummary?> ResolveSimBodyCandidateAssetCoreAsync(SimCasSlotOptionPickerItem option, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(option.PackagePath))
        {
            var exact = await indexStore.GetPackageAssetByIdAsync(option.PackagePath, option.AssetId, cancellationToken);
            if (exact is not null)
            {
                return exact;
            }
        }

        if (string.IsNullOrWhiteSpace(option.RootTgi))
        {
            return null;
        }

        var query = new AssetBrowserQuery(
            BuildSourceScope(),
            option.RootTgi,
            AssetBrowserDomain.Cas,
            string.Empty,
            option.PackagePath ?? string.Empty,
            string.Empty,
            false,
            false,
            AssetBrowserSort.Name,
            0,
            32);
        var result = await indexStore.QueryAssetsAsync(query, cancellationToken);
        return result.Items.FirstOrDefault(asset =>
            asset.Id == option.AssetId ||
            asset.RootKey.FullTgi.Equals(option.RootTgi, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildSimBodyCandidateCacheKey(string? packagePath, string? rootTgi, Guid assetId) =>
        $"{packagePath ?? string.Empty}|{rootTgi ?? string.Empty}|{assetId:D}";

    private async Task<IReadOnlyList<ResourceMetadata>> GetSimBodyPackageInstanceResourcesAsync(
        string packagePath,
        ulong fullInstance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cachedTask = simBodyPackageInstanceResourceCache.GetOrAdd(
            (packagePath, fullInstance),
            static (key, store) => store.GetResourcesByInstanceAsync(key.PackagePath, key.FullInstance, CancellationToken.None),
            indexStore);

        try
        {
            return await cachedTask.WaitAsync(cancellationToken);
        }
        catch when (cachedTask.IsFaulted || cachedTask.IsCanceled)
        {
            simBodyPackageInstanceResourceCache.TryRemove(
                new KeyValuePair<(string PackagePath, ulong FullInstance), Task<IReadOnlyList<ResourceMetadata>>>((packagePath, fullInstance), cachedTask));
            throw;
        }
    }

    private async Task<SimBodyCandidatePreviewBuildResult?> BuildSimBodyCandidatePreviewAsync(
        AssetSummary asset,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var cacheKey = BuildSimBodyCandidateCacheKey(asset.PackagePath, asset.RootKey.FullTgi, asset.Id);
        var cachedTask = simBodyCandidatePreviewCache.GetOrAdd(
            cacheKey,
            static (_, state) => state.ViewModel.BuildSimBodyCandidatePreviewCoreAsync(state.Asset, CancellationToken.None),
            (ViewModel: this, Asset: asset));

        try
        {
            return await cachedTask.WaitAsync(cancellationToken);
        }
        catch when (cachedTask.IsFaulted || cachedTask.IsCanceled)
        {
            simBodyCandidatePreviewCache.TryRemove(new KeyValuePair<string, Task<SimBodyCandidatePreviewBuildResult?>>(cacheKey, cachedTask));
            throw;
        }
    }

    private async Task<SimBodyCandidatePreviewBuildResult?> BuildSimBodyCandidatePreviewCoreAsync(
        AssetSummary asset,
        CancellationToken cancellationToken)
    {
        var packageResources = await GetSimBodyPackageInstanceResourcesAsync(
            asset.PackagePath,
            asset.RootKey.FullInstance,
            cancellationToken);
        var graph = await assetGraphBuilder.BuildAssetGraphAsync(asset, packageResources, cancellationToken);
        if (graph.CasGraph is null || !graph.CasGraph.IsSupported || graph.CasGraph.GeometryResource is null)
        {
            return new SimBodyCandidatePreviewBuildResult(
                Preview: null,
                SceneRoot: null,
                RigResources: [],
                RegionMaps: [],
                $"Skipped candidate {asset.DisplayName}: no supported CAS geometry root was resolved.");
        }

        var preview = await BuildCasPreviewResultAsync(graph.CasGraph, cancellationToken);
        var sceneRoot = graph.CasGraph.GeometryResources
            .FirstOrDefault(resource => string.Equals(resource.Key.FullTgi, (preview.Content as ScenePreviewContent)?.Resource.Key.FullTgi, StringComparison.OrdinalIgnoreCase))
            ?? graph.CasGraph.GeometryResource;

        return preview.Content switch
        {
            ScenePreviewContent { Scene: not null } scenePreview => new SimBodyCandidatePreviewBuildResult(
                scenePreview,
                sceneRoot,
                graph.CasGraph.RigResources,
                graph.CasGraph.RegionMaps,
                scenePreview.Diagnostics),
            ScenePreviewContent scenePreview => new SimBodyCandidatePreviewBuildResult(
                Preview: scenePreview,
                SceneRoot: sceneRoot,
                graph.CasGraph.RigResources,
                graph.CasGraph.RegionMaps,
                string.IsNullOrWhiteSpace(scenePreview.Diagnostics)
                    ? $"Skipped candidate {asset.DisplayName}: preview did not yield a usable scene."
                    : $"Skipped candidate {asset.DisplayName}: {scenePreview.Diagnostics}"),
            UnsupportedPreviewContent unsupportedPreview => new SimBodyCandidatePreviewBuildResult(
                Preview: null,
                SceneRoot: null,
                RigResources: [],
                RegionMaps: [],
                string.IsNullOrWhiteSpace(unsupportedPreview.Reason)
                    ? $"Skipped candidate {asset.DisplayName}: preview did not yield a usable scene."
                    : $"Skipped candidate {asset.DisplayName}: {unsupportedPreview.Reason}"),
            _ => new SimBodyCandidatePreviewBuildResult(
                Preview: null,
                SceneRoot: null,
                RigResources: [],
                RegionMaps: [],
                $"Skipped candidate {asset.DisplayName}: preview did not yield a usable scene.")
        };
    }

    private static double InterpolatePreviewStep(double start, double end, int index, int total, double offset = 0d)
    {
        if (total <= 0)
        {
            return end;
        }

        var progress = Math.Clamp((index + offset) / total, 0d, 1d);
        return start + ((end - start) * progress);
    }

    private async Task<SimBodyPreviewLayerProbeResult> ResolveSimBodyPreviewLayerAsync(
        SimBodyCandidateFamilyPickerItem family,
        double progressStart,
        double progressEnd,
        string progressVerb,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var options = GetPreferredSimBodyPreviewOptions(family);
        if (options.Count == 0)
        {
            diagnostics.Add($"No candidate options are currently available for active layer family {family.Label}.");
            return new SimBodyPreviewLayerProbeResult(null, diagnostics);
        }

        var seenAssetIds = new HashSet<Guid>();
        for (var batchStart = 0; batchStart < options.Count; batchStart += SimBodyCandidateProbeBatchSize)
        {
            var batch = options
                .Skip(batchStart)
                .Take(SimBodyCandidateProbeBatchSize)
                .ToArray();
            var batchEnd = batchStart + batch.Length;
            var progressMessage = options.Count == 1
                ? $"{progressVerb} {family.Label} layer asset..."
                : $"{progressVerb} {family.Label} candidate(s) {batchStart + 1}-{batchEnd} of {options.Count}...";
            await AdvancePreviewLoadAsync(
                InterpolatePreviewStep(progressStart, progressEnd, batchStart, options.Count),
                progressMessage);

            var resolvedAssets = (await Task.WhenAll(batch.Select(option => ResolveSimBodyCandidateAssetAsync(option, cancellationToken))))
                .Where(static asset => asset is not null)
                .Cast<AssetSummary>()
                .Where(asset => seenAssetIds.Add(asset.Id))
                .ToArray();
            if (resolvedAssets.Length == 0)
            {
                continue;
            }

            var previewCandidates = await Task.WhenAll(
                resolvedAssets.Select(asset => BuildSimBodyCandidatePreviewAsync(asset, cancellationToken)));
            for (var candidateIndex = 0; candidateIndex < resolvedAssets.Length; candidateIndex++)
            {
                var asset = resolvedAssets[candidateIndex];
                var previewCandidate = previewCandidates[candidateIndex];
                if (previewCandidate is null)
                {
                    diagnostics.Add($"Skipped candidate {asset.DisplayName}: preview build produced no result.");
                    continue;
                }

                if (previewCandidate.IsUsable &&
                    previewCandidate.Preview is not null &&
                    previewCandidate.SceneRoot is not null)
                {
                    return new SimBodyPreviewLayerProbeResult(
                        new SimBodyPreviewLayerResolutionResult(
                            asset,
                            previewCandidate.Preview,
                            previewCandidate.SceneRoot,
                            previewCandidate.RigResources,
                            previewCandidate.RegionMaps,
                            diagnostics),
                        diagnostics);
                }

                diagnostics.Add(previewCandidate.Diagnostics);
            }
        }

        diagnostics.Add($"No usable {family.Label} preview candidate was built from {options.Count:N0} option(s).");
        return new SimBodyPreviewLayerProbeResult(null, diagnostics);
    }

    private static string BuildSimBodyProxySourceSummary(IReadOnlyList<AssetSummary> assets)
    {
        if (assets.Count == 0)
        {
            return "Base-body preview source: no resolved body shell asset is currently available.";
        }

        var examples = string.Join(
            ", ",
            assets.Take(4).Select(static asset => $"{asset.DisplayName} ({asset.Category ?? "CAS"})"));
        return assets.Count == 1
            ? $"Base-body preview source: {examples} from {assets[0].PackageName ?? Path.GetFileName(assets[0].PackagePath)}."
            : $"Base-body preview sources: {assets.Count} body-related CAS assets were considered. Examples: {examples}.";
    }

    private static string? BuildSimBodyPreviewLayerSummary(IReadOnlyList<SimBodyPreviewLayerItem> layers)
    {
        if (layers.Count == 0)
        {
            return null;
        }

        return string.Join(
            Environment.NewLine,
            new[]
            {
                "Current base-body preview layers:",
                string.Join(
                    Environment.NewLine,
                    layers.Select(static layer =>
                        $"- {layer.Label}: {layer.AssetLabel} | {layer.PackageLabel} | {layer.RootTgi}"))
            });
    }

    private static string JoinDistinctDiagnosticLines(params string?[] diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .SelectMany(static text => text!.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal));

    private void RecordCurrentAssetSessionDiagnostics(string trigger)
    {
        if (selectedAsset is null)
        {
            return;
        }

        try
        {
            var selectedTemplate = selectedAsset.AssetKind == AssetKind.Sim
                ? SelectedSimTemplateOption?.DisplayLabel
                : null;
            assetSessionLogService.RecordAssetDiagnostics(new AssetSessionLogEntry(
                DateTimeOffset.Now,
                trigger,
                GetAppBuildLabel(),
                selectedAsset.AssetKind.ToString(),
                selectedAsset.DisplayName,
                selectedAsset.PackagePath,
                selectedAsset.RootKey.FullTgi,
                selectedAsset.Category,
                selectedResource?.Key.FullTgi,
                selectedResource?.Key.TypeName,
                selectedResource?.PackagePath,
                selectedAssetSceneRoot?.Key.FullTgi,
                PreviewSurfaceMode.ToString(),
                PreviewSurfaceTitle,
                StatusMessage,
                selectedTemplate,
                PreviewText,
                DetailsText));
        }
        catch (Exception ex)
        {
            AppendLog($"Asset session log write failed: {ex.Message}");
        }
    }

    public async Task ExportSelectedRawAsync(string outputDirectory)
    {
        if (selectedResource is null)
        {
            StatusMessage = "Select a resource or asset root first.";
            return;
        }

        selectedResource = await resourceMetadataEnrichmentService.EnrichAsync(selectedResource, CancellationToken.None);
        var result = await rawExportService.ExportAsync(new RawExportRequest(selectedResource, outputDirectory), CancellationToken.None);
        StatusMessage = result.Message;
        AppendLog(result.OutputPath is null ? result.Message : $"{result.Message} {result.OutputPath}");
    }

    public async Task ExportSelectedAssetAsync(string outputDirectory)
    {
        if (selectedAsset is null || selectedAssetGraph is null)
        {
            StatusMessage = "Select a supported logical asset first.";
            return;
        }

        if (selectedAssetSceneRoot is null)
        {
            StatusMessage = "The selected logical asset does not have a resolved scene root.";
            return;
        }

        var preview = await previewService.CreatePreviewAsync(selectedAssetSceneRoot, CancellationToken.None);
        if (preview.Content is not ScenePreviewContent scenePreview || scenePreview.Scene is null)
        {
            StatusMessage = "The selected logical asset could not be reconstructed into an exportable scene.";
            PreviewText = preview.Content is ScenePreviewContent failedScene ? failedScene.Diagnostics : PreviewText;
            return;
        }

        var export = new SceneExportRequest(
            Slugify(selectedAsset.DisplayName),
            scenePreview.Scene,
            outputDirectory,
            BuildSourceResources(selectedAssetGraph, selectedAssetSceneRoot),
            scenePreview.Scene.Materials.SelectMany(static material => material.Textures).ToArray(),
            scenePreview.Diagnostics.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries),
            BuildMaterialManifest(scenePreview.Scene));

        var result = await fbxExportService.ExportAsync(export, CancellationToken.None);
        StatusMessage = result.Message;
        AppendLog(result.OutputPath is null ? result.Message : $"{result.Message} {result.OutputPath}");
    }

    public async Task PlayAudioAsync()
    {
        if (previewAudioBytes is null)
        {
            StatusMessage = "Current preview does not provide playable WAV audio.";
            return;
        }

        await audioPlayer.PlayAsync(previewAudioBytes, CancellationToken.None);
        StatusMessage = "Audio playback started.";
    }

    public async Task StopAudioAsync()
    {
        await audioPlayer.StopAsync();
        StatusMessage = "Audio playback stopped.";
    }

    public async Task SelectSceneLodAsync(SceneLodOption? option)
    {
        if (option?.Resource is null || selectedAsset is null || selectedAssetGraph is null)
        {
            return;
        }

        var resource = option.Resource;
        selectedResource = resource;
        selectedAssetSceneRoot = resource;
        BeginPreviewLoad(1d, "Building selected LOD preview...");
        await YieldToUiAsync();

        var previewProgress = CreatePreviewBuildProgressReporter(0.10, 0.82);
        await AdvancePreviewLoadAsync(0.10, "Building selected LOD preview...");
        var preview = await previewService.CreatePreviewAsync(resource, CancellationToken.None, previewProgress);
        selectedAssetBaseScenePreview = preview.Content as ScenePreviewContent;
        selectedAssetScenePreview = await BuildDisplayedScenePreviewAsync(selectedAssetBaseScenePreview, CancellationToken.None);
        selectedAssetDetailsSupplement = null;
        var assetDetails = BuildCurrentAssetDetails();
        await AdvancePreviewLoadAsync(0.96, "Applying preview...");
        await ApplyPreviewAsync(
            resource,
            new PreviewResult(preview.Kind, selectedAssetScenePreview ?? preview.Content),
            assetDetails);
        CompletePreviewLoad();
    }

    private async Task ReloadSourcesAsync()
    {
        ClearSimPreviewCaches();
        var sources = await indexStore.GetDataSourcesAsync(CancellationToken.None);
        ReplaceCollection(DataSources, sources);
        UpdateBrowsePresentation();
    }

    private void ClearSimPreviewCaches()
    {
        simBodyCandidateAssetCache.Clear();
        simBodyPackageInstanceResourceCache.Clear();
        simBodyCandidatePreviewCache.Clear();
    }

    private async Task ReloadAssetFacetOptionsAsync()
    {
        var assetKind = AssetDomain switch
        {
            AssetBrowserDomain.BuildBuy => AssetKind.BuildBuy,
            AssetBrowserDomain.Cas => AssetKind.Cas,
            AssetBrowserDomain.Sim => AssetKind.Sim,
            AssetBrowserDomain.General3D => AssetKind.General3D,
            _ => AssetKind.BuildBuy
        };
        var options = await indexStore.GetAssetFacetOptionsAsync(assetKind, CancellationToken.None);
        ReplaceCollection(AssetCategories, PrependAll(options.Categories));
        ReplaceCollection(AssetRootTypes, PrependAll(options.RootTypeNames));
        ReplaceCollection(AssetIdentityTypes, PrependAll(options.IdentityTypes));
        ReplaceCollection(AssetPrimaryGeometryTypes, PrependAll(options.PrimaryGeometryTypes));
        ReplaceCollection(AssetThumbnailTypes, PrependAll(options.ThumbnailTypeNames));
        ReplaceCollection(AssetCatalogSignal0020Values, PrependAll(options.CatalogSignal0020Values ?? []));
        ReplaceCollection(AssetCatalogSignal002CValues, PrependAll(options.CatalogSignal002CValues ?? []));
        ReplaceCollection(AssetCatalogSignal0030Values, PrependAll(options.CatalogSignal0030Values ?? []));
        ReplaceCollection(AssetCatalogSignal0034Values, PrependAll(options.CatalogSignal0034Values ?? []));
        if (!AssetCategories.Contains(AssetCategoryText)) AssetCategoryText = string.Empty;
        if (!AssetRootTypes.Contains(AssetRootTypeText)) AssetRootTypeText = string.Empty;
        if (!AssetIdentityTypes.Contains(AssetIdentityTypeText)) AssetIdentityTypeText = string.Empty;
        if (!AssetPrimaryGeometryTypes.Contains(AssetPrimaryGeometryTypeText)) AssetPrimaryGeometryTypeText = string.Empty;
        if (!AssetThumbnailTypes.Contains(AssetThumbnailTypeText)) AssetThumbnailTypeText = string.Empty;
        if (!AssetCatalogSignal0020Values.Contains(AssetCatalogSignal0020Text)) AssetCatalogSignal0020Text = string.Empty;
        if (!AssetCatalogSignal002CValues.Contains(AssetCatalogSignal002CText)) AssetCatalogSignal002CText = string.Empty;
        if (!AssetCatalogSignal0030Values.Contains(AssetCatalogSignal0030Text)) AssetCatalogSignal0030Text = string.Empty;
        if (!AssetCatalogSignal0034Values.Contains(AssetCatalogSignal0034Text)) AssetCatalogSignal0034Text = string.Empty;
    }

    private async Task RefreshAssetBrowserAsync(bool resetWindow, bool append)
    {
        if (IsQueryingResults || !assetQueryDirty && !resetWindow && !append)
        {
            return;
        }

        IsQueryingResults = true;
        try
        {
            var offset = append ? AssetResults.Count : 0;
            var result = await indexStore.QueryAssetsAsync(assetBrowserState.ToQuery(BuildSourceScope(), offset, DefaultResultWindowSize), CancellationToken.None);

            if (!append)
            {
                ReplaceCollection(AssetResults, result.Items);
            }
            else
            {
                AppendCollection(AssetResults, result.Items);
            }

            assetTotalCount = result.TotalCount;
            assetBrowserHasLoaded = true;
            assetQueryDirty = false;
            StatusMessage = assetTotalCount == 0
                ? "Asset Browser returned no matches for the current scope."
                : $"Asset Browser loaded {AssetResults.Count:N0} of {assetTotalCount:N0} matches.";
        }
        finally
        {
            IsQueryingResults = false;
            UpdateBrowsePresentation();
        }
    }

    private async Task RefreshRawBrowserAsync(bool resetWindow, bool append)
    {
        if (IsQueryingResults || !rawQueryDirty && !resetWindow && !append)
        {
            return;
        }

        IsQueryingResults = true;
        try
        {
            var offset = append ? RawResources.Count : 0;
            var result = await indexStore.QueryResourcesAsync(rawResourceBrowserState.ToQuery(BuildSourceScope(), offset, DefaultResultWindowSize), CancellationToken.None);

            if (!append)
            {
                ReplaceCollection(RawResources, result.Items);
            }
            else
            {
                AppendCollection(RawResources, result.Items);
            }

            rawTotalCount = result.TotalCount;
            rawBrowserHasLoaded = true;
            rawQueryDirty = false;
            StatusMessage = rawTotalCount == 0
                ? "Raw Resource Browser returned no matches for the current scope."
                : $"Raw Resource Browser loaded {RawResources.Count:N0} of {rawTotalCount:N0} matches.";
        }
        finally
        {
            IsQueryingResults = false;
            UpdateBrowsePresentation();
        }
    }

    private async Task ApplyPreviewAsync(ResourceMetadata resource, PreviewResult preview, string? leadingDetails)
    {
        ResetPreviewState();
        DetailsText = string.IsNullOrWhiteSpace(leadingDetails)
            ? BuildResourceDetails(resource)
            : $"{leadingDetails}{Environment.NewLine}{Environment.NewLine}{BuildResourceDetails(resource)}";

        var previewPresentation = PreviewPresentationState.FromPreviewContent(preview.Content);
        PreviewSurfaceMode = previewPresentation.SurfaceMode;
        PreviewSurfaceTitle = previewPresentation.SurfaceTitle;

        switch (preview.Content)
        {
            case TextPreviewContent text:
                PreviewText = text.Text;
                break;

            case HexPreviewContent hex:
                PreviewText = hex.HexDump;
                break;

            case TexturePreviewContent texture:
                PreviewText = string.IsNullOrWhiteSpace(texture.Diagnostics) ? "Decoded texture preview." : texture.Diagnostics;
                PreviewImageSource = await CreateBitmapAsync(texture.PngBytes);
                break;

            case AudioPreviewContent audio:
                previewAudioBytes = audio.WavBytes;
                PreviewText = audio.Diagnostics;
                break;

            case ScenePreviewContent scene:
                CurrentScene = scene.Scene;
                PreviewText = AppendSceneMaterialDiagnostics(scene);
                if (scene.Scene is null)
                {
                    PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
                    PreviewSurfaceTitle = "Diagnostics";
                }
                break;

            case UnsupportedPreviewContent unsupported:
                PreviewText = unsupported.Reason;
                break;

            default:
                PreviewText = "No preview available.";
                break;
        }

        NotifyPreviewStateChanged();
        RecordCurrentAssetSessionDiagnostics("ApplyPreviewAsync");
    }

    private void MarkAllQueriesDirty()
    {
        assetQueryDirty = true;
        rawQueryDirty = true;
    }

    private void CancelPendingBrowserQueries()
    {
        assetSearchDebounceTokenSource?.Cancel();
        rawSearchDebounceTokenSource?.Cancel();
    }

    private async Task RefreshBrowseStateAfterIndexingAsync()
    {
        try
        {
            ClearSimPreviewCaches();
            MarkAllQueriesDirty();
            await ReloadAssetFacetOptionsAsync();
            await RefreshActiveBrowserAsync(resetWindow: true);
        }
        catch (Exception ex)
        {
            AppendLog($"Post-index browser refresh failed: {ex}");
        }
    }

    private void UpdateBrowsePresentation()
    {
        ReplaceCollection(
            ActiveFilterChips,
            SelectedBrowserMode == BrowserMode.AssetBrowser
                ? assetBrowserState.BuildFilterChips(BuildSourceScope())
                : rawResourceBrowserState.BuildFilterChips(BuildSourceScope()));

        if (DataSources.Count == 0)
        {
            ResultSummary = "No index folders configured.";
            EmptyStateMessage = "No index folders configured. Use Update Index to add one or more folders.";
        }
        else if (!BuildSourceScope().HasAny)
        {
            ResultSummary = "Choose a source scope.";
            EmptyStateMessage = "Choose at least one source scope to begin.";
        }
        else if (IsQueryingResults)
        {
            ResultSummary = SelectedBrowserMode == BrowserMode.AssetBrowser
                ? assetBrowserState.BuildSummary(BuildSourceScope(), assetTotalCount, AssetResults.Count)
                : rawResourceBrowserState.BuildSummary(BuildSourceScope(), rawTotalCount, RawResources.Count);
            EmptyStateMessage = "Loading results...";
        }
        else if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            ResultSummary = assetBrowserState.BuildSummary(BuildSourceScope(), assetTotalCount, AssetResults.Count);
            EmptyStateMessage = assetBrowserHasLoaded
                ? "No assets matched the current scope. Try a different domain or fewer filters."
                : "Choose a scope to begin.";
        }
        else
        {
            ResultSummary = rawResourceBrowserState.BuildSummary(BuildSourceScope(), rawTotalCount, RawResources.Count);
            EmptyStateMessage = rawBrowserHasLoaded
                ? "No raw resources matched the current scope. Narrow or change the query."
                : "Choose a scope to begin.";
        }

        OnPropertyChanged(nameof(AssetBrowserVisibility));
        OnPropertyChanged(nameof(RawBrowserVisibility));
        OnPropertyChanged(nameof(IsAssetBrowserActive));
        OnPropertyChanged(nameof(IsRawResourceBrowserActive));
        OnPropertyChanged(nameof(HasMoreResults));
        OnPropertyChanged(nameof(HasVisibleResults));
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    private void SyncAssetStateFromProperties()
    {
        assetBrowserState.SearchText = AssetSearchText;
        assetBrowserState.Domain = AssetDomain;
        assetBrowserState.CategoryText = AssetCategoryText;
        assetBrowserState.RootTypeText = AssetRootTypeText;
        assetBrowserState.IdentityTypeText = AssetIdentityTypeText;
        assetBrowserState.PrimaryGeometryTypeText = AssetPrimaryGeometryTypeText;
        assetBrowserState.ThumbnailTypeText = AssetThumbnailTypeText;
        assetBrowserState.CatalogSignal0020Text = AssetCatalogSignal0020Text;
        assetBrowserState.CatalogSignal002CText = AssetCatalogSignal002CText;
        assetBrowserState.CatalogSignal0030Text = AssetCatalogSignal0030Text;
        assetBrowserState.CatalogSignal0034Text = AssetCatalogSignal0034Text;
        assetBrowserState.PackageText = AssetPackageText;
        assetBrowserState.PackageRelativeText = AssetPackageRelativeText;
        assetBrowserState.HasThumbnailOnly = AssetHasThumbnailOnly;
        assetBrowserState.VariantsOnly = AssetVariantsOnly;
        assetBrowserState.RequireSceneRoot = AssetRequireSceneRoot;
        assetBrowserState.RequireExactGeometryCandidate = AssetRequireExactGeometryCandidate;
        assetBrowserState.RequireMaterialReferences = AssetRequireMaterialReferences;
        assetBrowserState.RequireTextureReferences = AssetRequireTextureReferences;
        assetBrowserState.RequireIdentityMetadata = AssetRequireIdentityMetadata;
        assetBrowserState.RequireRigReference = AssetRequireRigReference;
        assetBrowserState.RequireGeometryReference = AssetRequireGeometryReference;
        assetBrowserState.RequireMaterialResourceCandidate = AssetRequireMaterialResourceCandidate;
        assetBrowserState.RequireTextureResourceCandidate = AssetRequireTextureResourceCandidate;
        assetBrowserState.RequirePackageLocalGraph = AssetRequirePackageLocalGraph;
        assetBrowserState.RequireDiagnostics = AssetRequireDiagnostics;
        assetBrowserState.Sort = AssetSort;
    }

    private void SyncRawStateFromProperties()
    {
        rawResourceBrowserState.SearchText = RawSearchText;
        rawResourceBrowserState.Domain = RawDomain;
        rawResourceBrowserState.TypeNameText = RawTypeNameText;
        rawResourceBrowserState.PackageText = RawPackageText;
        rawResourceBrowserState.GroupHexText = RawGroupHexText;
        rawResourceBrowserState.InstanceHexText = RawInstanceHexText;
        rawResourceBrowserState.PreviewableOnly = RawPreviewableOnly;
        rawResourceBrowserState.ExportCapableOnly = RawExportCapableOnly;
        rawResourceBrowserState.CompressedKnownOnly = RawCompressedKnownOnly;
        rawResourceBrowserState.LinkFilter = RawLinkFilter;
        rawResourceBrowserState.Sort = RawSort;
    }

    private void SyncAssetStateToProperties()
    {
        AssetSearchText = assetBrowserState.SearchText;
        AssetDomain = assetBrowserState.Domain;
        AssetCategoryText = assetBrowserState.CategoryText;
        AssetRootTypeText = assetBrowserState.RootTypeText;
        AssetIdentityTypeText = assetBrowserState.IdentityTypeText;
        AssetPrimaryGeometryTypeText = assetBrowserState.PrimaryGeometryTypeText;
        AssetThumbnailTypeText = assetBrowserState.ThumbnailTypeText;
        AssetCatalogSignal0020Text = assetBrowserState.CatalogSignal0020Text;
        AssetCatalogSignal002CText = assetBrowserState.CatalogSignal002CText;
        AssetCatalogSignal0030Text = assetBrowserState.CatalogSignal0030Text;
        AssetCatalogSignal0034Text = assetBrowserState.CatalogSignal0034Text;
        AssetPackageText = assetBrowserState.PackageText;
        AssetPackageRelativeText = assetBrowserState.PackageRelativeText;
        AssetHasThumbnailOnly = assetBrowserState.HasThumbnailOnly;
        AssetVariantsOnly = assetBrowserState.VariantsOnly;
        AssetRequireSceneRoot = assetBrowserState.RequireSceneRoot;
        AssetRequireExactGeometryCandidate = assetBrowserState.RequireExactGeometryCandidate;
        AssetRequireMaterialReferences = assetBrowserState.RequireMaterialReferences;
        AssetRequireTextureReferences = assetBrowserState.RequireTextureReferences;
        AssetRequireIdentityMetadata = assetBrowserState.RequireIdentityMetadata;
        AssetRequireRigReference = assetBrowserState.RequireRigReference;
        AssetRequireGeometryReference = assetBrowserState.RequireGeometryReference;
        AssetRequireMaterialResourceCandidate = assetBrowserState.RequireMaterialResourceCandidate;
        AssetRequireTextureResourceCandidate = assetBrowserState.RequireTextureResourceCandidate;
        AssetRequirePackageLocalGraph = assetBrowserState.RequirePackageLocalGraph;
        AssetRequireDiagnostics = assetBrowserState.RequireDiagnostics;
        AssetSort = assetBrowserState.Sort;
    }

    private void SyncRawStateToProperties()
    {
        RawSearchText = rawResourceBrowserState.SearchText;
        RawDomain = rawResourceBrowserState.Domain;
        RawTypeNameText = rawResourceBrowserState.TypeNameText;
        RawPackageText = rawResourceBrowserState.PackageText;
        RawGroupHexText = rawResourceBrowserState.GroupHexText;
        RawInstanceHexText = rawResourceBrowserState.InstanceHexText;
        RawPreviewableOnly = rawResourceBrowserState.PreviewableOnly;
        RawExportCapableOnly = rawResourceBrowserState.ExportCapableOnly;
        RawCompressedKnownOnly = rawResourceBrowserState.CompressedKnownOnly;
        RawLinkFilter = rawResourceBrowserState.LinkFilter;
        RawSort = rawResourceBrowserState.Sort;
    }

    public string BuildIndexingMemoryBudgetSummary(int requestedPercent) =>
        IndexingMemoryBudgetCalculator.BuildBudgetSummary(systemMemoryService.GetSnapshot(), requestedPercent);

    private long ResolvePackageByteCacheBudgetBytes(int requestedPercent) =>
        IndexingMemoryBudgetCalculator.ResolvePackageByteCacheBudgetBytes(systemMemoryService.GetSnapshot(), requestedPercent);

    private SourceScope BuildSourceScope() => new(IncludeGameSources, IncludeDlcSources, IncludeModsSources);

    private void AppendLog(string message) => Log.Append(message);

    private void RefreshAssetCapabilityFilterChanged()
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    private async Task DebounceSearchAsync(BrowserMode mode)
    {
        if (suspendBrowserQueriesDuringIndexing)
        {
            return;
        }

        CancellationTokenSource tokenSource;
        if (mode == BrowserMode.AssetBrowser)
        {
            assetSearchDebounceTokenSource?.Cancel();
            assetSearchDebounceTokenSource?.Dispose();
            assetSearchDebounceTokenSource = new CancellationTokenSource();
            tokenSource = assetSearchDebounceTokenSource;
        }
        else
        {
            rawSearchDebounceTokenSource?.Cancel();
            rawSearchDebounceTokenSource?.Dispose();
            rawSearchDebounceTokenSource = new CancellationTokenSource();
            tokenSource = rawSearchDebounceTokenSource;
        }

        try
        {
            await Task.Delay(350, tokenSource.Token);
            if (SelectedBrowserMode == mode)
            {
                await RefreshActiveBrowserAsync(resetWindow: true);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task<BitmapImage> CreateBitmapAsync(byte[] bytes)
    {
        var bitmap = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        await bitmap.SetSourceAsync(stream);
        return bitmap;
    }

    private static ResourceMetadata? FindAssetFallbackPreviewResource(
        AssetSummary asset,
        AssetGraph graph,
        IReadOnlyList<ResourceMetadata> packageResources)
    {
        if (!string.IsNullOrWhiteSpace(asset.ThumbnailTgi))
        {
            var exactThumbnail = packageResources.FirstOrDefault(resource =>
                string.Equals(resource.Key.FullTgi, asset.ThumbnailTgi, StringComparison.OrdinalIgnoreCase));
            if (IsPreviewableAssetFallback(exactThumbnail))
            {
                return exactThumbnail;
            }
        }

        return graph.LinkedResources.FirstOrDefault(IsPreviewableAssetFallback);
    }

    private string BuildCurrentAssetDetails()
    {
        if (selectedAsset is null || selectedAssetGraph is null)
        {
            return "Metadata will appear here.";
        }

        var details = AssetDetailsFormatter.BuildAssetDetails(
            selectedAsset,
            selectedAssetGraph,
            selectedAssetSceneRoot,
            selectedAssetScenePreview,
            selectedAssetVariants,
            SelectedAssetVariant?.Variant);
        return string.IsNullOrWhiteSpace(selectedAssetDetailsSupplement)
            ? details
            : $"{details}{Environment.NewLine}{Environment.NewLine}{selectedAssetDetailsSupplement}";
    }

    private string BuildCurrentDisplayedAssetDetails()
    {
        var assetDetails = BuildCurrentAssetDetails();
        return selectedResource is null
            ? assetDetails
            : $"{assetDetails}{Environment.NewLine}{Environment.NewLine}{BuildResourceDetails(selectedResource)}";
    }

    private async Task<ScenePreviewContent?> BuildDisplayedScenePreviewAsync(ScenePreviewContent? basePreview, CancellationToken cancellationToken)
    {
        if (basePreview is null)
        {
            return null;
        }

        if (basePreview.Scene is null || selectedAsset is null)
        {
            return basePreview;
        }

        var adaptedScene = AssetVariantSceneAdapter.ApplyToScene(
            basePreview.Scene,
            selectedAsset.AssetKind,
            SelectedAssetVariant?.Variant);
        var diagnostics = AssetVariantSceneAdapter.AppendVariantDiagnostics(
            basePreview.Diagnostics,
            selectedAsset.AssetKind,
            SelectedAssetVariant?.Variant);

        if (selectedAsset.AssetKind == AssetKind.Cas &&
            selectedAssetGraph?.CasGraph is { } casGraph)
        {
            var compositeResult = await TryApplyCasVariantTextureCompositeAsync(
                adaptedScene,
                casGraph,
                SelectedAssetVariant?.Variant,
                cancellationToken);
            adaptedScene = compositeResult.Scene;
            if (!string.IsNullOrWhiteSpace(compositeResult.Diagnostics))
            {
                diagnostics = string.IsNullOrWhiteSpace(diagnostics)
                    ? compositeResult.Diagnostics
                    : $"{diagnostics}{Environment.NewLine}{compositeResult.Diagnostics}";
            }
        }

        return basePreview with
        {
            Scene = adaptedScene,
            Diagnostics = diagnostics
        };
    }

    private async Task<CasVariantTextureCompositeResult> TryApplyCasVariantTextureCompositeAsync(
        CanonicalScene scene,
        CasAssetGraph casGraph,
        AssetVariantSummary? selectedVariant,
        CancellationToken cancellationToken)
    {
        if (selectedVariant is null ||
            !string.Equals(selectedVariant.VariantKind, "Swatch", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(selectedVariant.SwatchHex))
        {
            return new CasVariantTextureCompositeResult(scene, string.Empty);
        }

        var manifestMaterial = casGraph.Materials.FirstOrDefault(static material => material.SourceKind == CanonicalMaterialSourceKind.ApproximateCas)
            ?? casGraph.Materials.FirstOrDefault();
        if (manifestMaterial is null || manifestMaterial.Textures.Count == 0)
        {
            return new CasVariantTextureCompositeResult(scene, string.Empty);
        }

        var relevantSlots = manifestMaterial.Textures
            .Where(static texture => texture.Slot is "diffuse" or "color_shift_mask")
            .ToArray();
        if (relevantSlots.Length == 0)
        {
            return new CasVariantTextureCompositeResult(scene, string.Empty);
        }

        var resolvedTextures = new List<CanonicalTexture>(relevantSlots.Length);
        foreach (var manifestTexture in relevantSlots)
        {
            var resolvedTexture = await TryResolveAssetVariantTextureAsync(manifestTexture, casGraph, cancellationToken);
            if (resolvedTexture is not null)
            {
                resolvedTextures.Add(resolvedTexture);
            }
        }

        if (resolvedTextures.Count == 0)
        {
            return new CasVariantTextureCompositeResult(scene, string.Empty);
        }

        var mergedMaterials = scene.Materials
            .Select(material =>
            {
                if (material.SourceKind != CanonicalMaterialSourceKind.ApproximateCas)
                {
                    return material;
                }

                return material with
                {
                    Textures = MergeMaterialTextures(material.Textures, resolvedTextures)
                };
            })
            .ToArray();

        var diagnostics = resolvedTextures.Any(static texture => texture.Slot.Equals("color_shift_mask", StringComparison.OrdinalIgnoreCase))
            ? "CAS swatch preview resolved role-specific diffuse and color-shift mask textures from the indexed material manifest."
            : "CAS swatch preview resolved a role-specific diffuse texture from the indexed material manifest.";
        return new CasVariantTextureCompositeResult(scene with { Materials = mergedMaterials }, diagnostics);
    }

    private async Task<CanonicalTexture?> TryResolveAssetVariantTextureAsync(
        MaterialTextureEntry manifestTexture,
        CasAssetGraph casGraph,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{manifestTexture.SourcePackagePath}|{manifestTexture.SourceKey?.FullTgi}|{manifestTexture.Slot}";
        if (assetVariantTextureCache.TryGetValue(cacheKey, out var cachedTexture))
        {
            return cachedTexture;
        }

        ResourceMetadata? resource = null;
        if (manifestTexture.SourceKey is not null)
        {
            resource = casGraph.TextureResources.FirstOrDefault(candidate =>
                candidate.Key.FullTgi.Equals(manifestTexture.SourceKey.FullTgi, StringComparison.OrdinalIgnoreCase) &&
                (manifestTexture.SourcePackagePath is null ||
                 candidate.PackagePath.Equals(manifestTexture.SourcePackagePath, StringComparison.OrdinalIgnoreCase)));

            if (resource is null && !string.IsNullOrWhiteSpace(manifestTexture.SourcePackagePath))
            {
                resource = await indexStore.GetResourceByTgiAsync(
                    manifestTexture.SourcePackagePath,
                    manifestTexture.SourceKey.FullTgi,
                    cancellationToken);
            }

            resource ??= (await indexStore.GetResourcesByTgiAsync(manifestTexture.SourceKey.FullTgi, cancellationToken))
                .FirstOrDefault(candidate => manifestTexture.SourcePackagePath is null ||
                                             candidate.PackagePath.Equals(manifestTexture.SourcePackagePath, StringComparison.OrdinalIgnoreCase));
        }

        if (resource is null)
        {
            assetVariantTextureCache[cacheKey] = null;
            return null;
        }

        var preview = await previewService.CreatePreviewAsync(resource, cancellationToken);
        if (preview.Content is not TexturePreviewContent texturePreview)
        {
            assetVariantTextureCache[cacheKey] = null;
            return null;
        }

        var canonicalTexture = new CanonicalTexture(
            manifestTexture.Slot,
            manifestTexture.FileName,
            texturePreview.PngBytes,
            manifestTexture.SourceKey,
            resource.PackagePath,
            manifestTexture.Semantic,
            manifestTexture.SourceKind);
        assetVariantTextureCache[cacheKey] = canonicalTexture;
        return canonicalTexture;
    }

    private static IReadOnlyList<CanonicalTexture> MergeMaterialTextures(
        IReadOnlyList<CanonicalTexture> existingTextures,
        IReadOnlyList<CanonicalTexture> overrideTextures)
    {
        var merged = existingTextures.ToDictionary(static texture => texture.Slot, StringComparer.OrdinalIgnoreCase);
        foreach (var overrideTexture in overrideTextures)
        {
            merged[overrideTexture.Slot] = overrideTexture;
        }

        return merged.Values
            .OrderBy(static texture => texture.Slot, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildAssetPreviewFallbackSupplement(string primaryReason, IReadOnlyList<string> diagnostics)
    {
        var lines = diagnostics
            .Where(static diagnostic => !string.IsNullOrWhiteSpace(diagnostic))
            .ToArray();

        return lines.Length == 0
            ? $"Preview Fallback:{Environment.NewLine}{primaryReason}"
            : $"Preview Fallback:{Environment.NewLine}{primaryReason}{Environment.NewLine}{string.Join(Environment.NewLine, lines)}";
    }

    private async Task<bool> TryApplyAssetFallbackPreviewAsync(
        ResourceMetadata? fallbackPreviewResource,
        string assetDetails)
    {
        if (!IsPreviewableAssetFallback(fallbackPreviewResource) || fallbackPreviewResource is null)
        {
            return false;
        }

        selectedResource = fallbackPreviewResource;
        selectedAssetSceneRoot = null;

        var fallbackPreview = await previewService.CreatePreviewAsync(fallbackPreviewResource, CancellationToken.None);
        if (fallbackPreview.Content is UnsupportedPreviewContent)
        {
            return false;
        }

        await ApplyPreviewAsync(fallbackPreviewResource, fallbackPreview, assetDetails);
        return true;
    }

    private static bool IsPreviewableAssetFallback(ResourceMetadata? resource) =>
        resource is not null &&
        resource.IsPreviewable &&
        resource.PreviewKind is PreviewKind.Texture or PreviewKind.Text or PreviewKind.Audio;

    private static string BuildResourceDetails(ResourceMetadata resource) =>
        $"""
        Package: {resource.PackagePath}
        Source: {resource.SourceKind}
        Type: {resource.Key.TypeName} ({resource.Key.Type:X8})
        Group: {resource.Key.Group:X8}
        Instance: {resource.Key.FullInstance:X16}
        TGI: {resource.Key.FullTgi}
        Name: {resource.Name ?? "(deferred)"}
        Description: {resource.Description ?? "(none)"}
        Compressed Size: {FormatSize(resource.CompressedSize)}
        Uncompressed Size: {FormatSize(resource.UncompressedSize)}
        Compressed: {FormatCompressed(resource.IsCompressed)}
        Preview Kind: {resource.PreviewKind}
        Linkage: {resource.AssetLinkageSummary}
        Diagnostics: {resource.Diagnostics}
        """;

    private static string FormatSize(long? value) => value?.ToString() ?? "(deferred)";

    private static string FormatCompressed(bool? value) => value?.ToString() ?? "(deferred)";

    private static string AppendSceneMaterialDiagnostics(ScenePreviewContent scene)
    {
        var buildLine = $"Build: {GetAppBuildLabel()}";
        var materialLines = BuildSceneMaterialDiagnostics(scene.Scene);
        var statusLine = $"Scene Build Status: {scene.Status}";
        if (materialLines.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(scene.Diagnostics))
            {
                return $"{buildLine}{Environment.NewLine}{statusLine}";
            }

            return $"{buildLine}{Environment.NewLine}{statusLine}{Environment.NewLine}{scene.Diagnostics}";
        }

        if (string.IsNullOrWhiteSpace(scene.Diagnostics))
        {
            return $"{buildLine}{Environment.NewLine}{statusLine}{Environment.NewLine}{string.Join(Environment.NewLine, materialLines)}";
        }

        return $"{buildLine}{Environment.NewLine}{statusLine}{Environment.NewLine}{scene.Diagnostics}{Environment.NewLine}{Environment.NewLine}{string.Join(Environment.NewLine, materialLines)}";
    }

    private static string GetAppBuildLabel()
    {
        var informationalVersion = typeof(MainViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return string.IsNullOrWhiteSpace(informationalVersion) ? "build-(unknown)" : informationalVersion;
    }

    private static IReadOnlyList<string> BuildSceneMaterialDiagnostics(CanonicalScene? scene)
    {
        if (scene is null || scene.Materials.Count == 0)
        {
            return [];
        }

        var lines = new List<string> { "Material Diagnostics:" };
        for (var index = 0; index < scene.Materials.Count; index++)
        {
            var material = scene.Materials[index];
            lines.Add(
                $"[{index}] {material.Name} | source={FormatMaterialSourceKind(material.SourceKind)} | shader={material.ShaderName ?? "(unknown)"} | alpha={material.AlphaMode ?? "(none)"}");

            if (!string.IsNullOrWhiteSpace(material.Approximation))
            {
                lines.Add($"    Approximation: {material.Approximation}");
            }

            if (material.Textures.Count == 0)
            {
                lines.Add("    Textures: none");
                continue;
            }

            foreach (var texture in material.Textures)
            {
                lines.Add(
                    $"    {FormatTextureSemantic(texture.Semantic)} ({texture.Slot}) -> {FormatTextureSourceKind(texture.SourceKind)} | {texture.SourceKey?.FullTgi ?? "(no key)"} | {Path.GetFileName(texture.SourcePackagePath ?? string.Empty)}");
            }
        }

        return lines;
    }

    private static string FormatMaterialSourceKind(CanonicalMaterialSourceKind kind) => kind switch
    {
        CanonicalMaterialSourceKind.ExplicitMatd => "explicit-matd",
        CanonicalMaterialSourceKind.MaterialSet => "mtst-material-set",
        CanonicalMaterialSourceKind.FallbackCandidate => "fallback-candidate",
        CanonicalMaterialSourceKind.ApproximateCas => "approximate-cas",
        CanonicalMaterialSourceKind.Unsupported => "unsupported",
        _ => "unknown"
    };

    private static string FormatTextureSemantic(CanonicalTextureSemantic semantic) => semantic switch
    {
        CanonicalTextureSemantic.BaseColor => "BaseColor",
        CanonicalTextureSemantic.Normal => "Normal",
        CanonicalTextureSemantic.Specular => "Specular",
        CanonicalTextureSemantic.Gloss => "Gloss",
        CanonicalTextureSemantic.Opacity => "Opacity",
        CanonicalTextureSemantic.Emissive => "Emissive",
        CanonicalTextureSemantic.Overlay => "Overlay",
        _ => "Unknown"
    };

    private static string FormatTextureSourceKind(CanonicalTextureSourceKind kind) => kind switch
    {
        CanonicalTextureSourceKind.ExplicitLocal => "explicit-local",
        CanonicalTextureSourceKind.ExplicitCompanion => "explicit-companion",
        CanonicalTextureSourceKind.ExplicitIndexed => "explicit-indexed",
        CanonicalTextureSourceKind.FallbackSameInstanceLocal => "fallback-same-instance-local",
        CanonicalTextureSourceKind.FallbackSameInstanceIndexed => "fallback-same-instance-indexed",
        _ => "unknown"
    };

    private void ResetPreviewState()
    {
        previewAudioBytes = null;
        PreviewImageSource = null;
        CurrentScene = null;
        PreviewText = string.Empty;
        DetailsText = string.Empty;
        PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
        PreviewSurfaceTitle = "Diagnostics";
        SelectedPreviewDiagnosticsTabIndex = 0;
        NotifyPreviewStateChanged();
    }

    private void BeginPreviewLoad(double totalSteps, string status)
    {
        PreviewLoadProgressMaximum = Math.Max(1d, totalSteps);
        PreviewLoadProgressValue = 0d;
        PreviewLoadProgressIndeterminate = false;
        PreviewLoadStatus = status;
        PreviewLoadCompleted = false;
        IsPreviewLoading = true;
    }

    private void AdvancePreviewLoad(double stepValue, string status)
    {
        PreviewLoadProgressValue = Math.Clamp(stepValue, 0d, PreviewLoadProgressMaximum);
        PreviewLoadStatus = status;
    }

    private async Task AdvancePreviewLoadAsync(double stepValue, string status)
    {
        AdvancePreviewLoad(stepValue, status);
        await YieldToUiAsync();
    }

    private void CompletePreviewLoad()
    {
        PreviewLoadProgressValue = PreviewLoadProgressMaximum;
        PreviewLoadStatus = "Ready";
        PreviewLoadCompleted = true;
        IsPreviewLoading = false;
    }

    private void ClearAssetVariantState()
    {
        suppressAssetVariantSelectionUpdates = true;
        try
        {
            ReplaceCollection(AssetVariantItems, []);
            SelectedAssetVariant = null;
        }
        finally
        {
            suppressAssetVariantSelectionUpdates = false;
        }

        selectedAssetVariants = [];
        Interlocked.Increment(ref assetVariantThumbnailLoadGeneration);
        SelectedAssetVariantThumbnailSource = null;
        SelectedAssetVariantThumbnailText = "Select a logical asset to inspect indexed variant data.";
        NotifySelectedAssetVariantStateChanged();
    }

    private void ClearSimArchetypeInspectorState()
    {
        lastSimAssemblyPlan = null;
        lastSimAssemblyGraph = null;
        SetSimTemplatePickerItems([]);
        SetSimCasSlotFamilyPickerItems([]);
        SetSimBodyCandidateFamilyPickerItems([]);
        ReplaceCollection(SelectedSimTemplateOptions, []);
        ReplaceCollection(SelectedSimBodyFoundation, []);
        ReplaceCollection(SelectedSimBodySources, []);
        ReplaceCollection(SelectedSimBodyCandidates, []);
        ReplaceCollection(SelectedSimBodyCandidateFamilies, []);
        ReplaceCollection(SelectedSimBodyAssemblyRecipe, []);
        ReplaceCollection(SelectedSimBodyGraphNodes, []);
        ReplaceCollection(SelectedSimBodyGraphLayers, []);
        ReplaceCollection(SelectedSimBodyPreviewLayers, []);
        ReplaceCollection(SelectedSimBodyPreviewCoverage, []);
        ReplaceCollection(SelectedSimSlotGroups, []);
        ReplaceCollection(SelectedSimMorphGroups, []);
        ReplaceCollection(SelectedSimCasSlotCandidates, []);
        ReplaceCollection(SelectedSimCasSlotFamilies, []);
        SelectedSimArchetypeTabIndex = 0;
        NotifySimArchetypeInspectorStateChanged();
    }

    private void SetSimArchetypeInspectorState(SimAssetGraph? simGraph, Guid? preferredTemplateResourceId = null)
    {
        lastSimAssemblyPlan = null;
        lastSimAssemblyGraph = null;
        SetSimTemplatePickerItems(simGraph?.TemplateOptions ?? [], preferredTemplateResourceId);
        ReplaceCollection(SelectedSimBodyFoundation, simGraph?.BodyFoundation ?? []);
        ReplaceCollection(SelectedSimBodySources, simGraph?.BodySources ?? []);
        ReplaceCollection(SelectedSimBodyCandidates, simGraph?.BodyCandidates ?? []);
        SetSimBodyCandidateFamilyPickerItems(simGraph?.BodyCandidates ?? []);
        RebuildSimBodyAssemblyRecipeState();
        ReplaceCollection(SelectedSimBodyGraphNodes, simGraph?.BodyAssembly.GraphNodes ?? []);
        ReplaceCollection(SelectedSimBodyPreviewLayers, []);
        RebuildSimResolvedBodyGraphState();
        RebuildSimBodyPreviewCoverageState();
        ReplaceCollection(SelectedSimSlotGroups, simGraph?.SlotGroups ?? []);
        ReplaceCollection(SelectedSimMorphGroups, simGraph?.MorphGroups ?? []);
        ReplaceCollection(SelectedSimCasSlotCandidates, simGraph?.CasSlotCandidates ?? []);
        SetSimCasSlotFamilyPickerItems(simGraph?.CasSlotCandidates ?? []);
        NotifySimArchetypeInspectorStateChanged();
    }

    private void SetSimTemplatePickerItems(IReadOnlyList<SimTemplateOptionSummary> templates, Guid? preferredTemplateResourceId = null)
    {
        suppressSimTemplateSelectionUpdates = true;
        try
        {
            var pickerItems = templates
                .Select(SimTemplatePickerItem.Create)
                .ToArray();
            var preferredTemplate = !preferredTemplateResourceId.HasValue
                ? SimTemplateSelectionPolicy.SelectPreferredTemplate(pickerItems.Select(static item => item.Option))
                : null;
            ReplaceCollection(SelectedSimTemplateOptions, pickerItems);
            SelectedSimTemplateOption = pickerItems.FirstOrDefault(item =>
                                            preferredTemplateResourceId.HasValue &&
                                            item.ResourceId == preferredTemplateResourceId.Value)
                                        ?? pickerItems.FirstOrDefault(item =>
                                            preferredTemplate is not null &&
                                            item.ResourceId == preferredTemplate.ResourceId)
                                        ?? pickerItems.FirstOrDefault(item => item.IsRepresentative)
                                        ?? pickerItems.FirstOrDefault();
        }
        finally
        {
            suppressSimTemplateSelectionUpdates = false;
        }
    }

    private void SetSimCasSlotFamilyPickerItems(IReadOnlyList<SimCasSlotCandidateSummary> candidates)
    {
        foreach (var family in SelectedSimCasSlotFamilies)
        {
            family.PropertyChanged -= OnSimCasSlotFamilyPickerItemPropertyChanged;
        }

        var pickerItems = candidates
            .Select(static candidate => new SimCasSlotFamilyPickerItem(candidate))
            .ToArray();
        ReplaceCollection(SelectedSimCasSlotFamilies, pickerItems);

        foreach (var family in SelectedSimCasSlotFamilies)
        {
            family.PropertyChanged += OnSimCasSlotFamilyPickerItemPropertyChanged;
        }
    }

    private void SetSimBodyCandidateFamilyPickerItems(IReadOnlyList<SimBodyCandidateSummary> candidates)
    {
        foreach (var family in SelectedSimBodyCandidateFamilies)
        {
            family.PropertyChanged -= OnSimBodyCandidateFamilyPickerItemPropertyChanged;
        }

        var pickerItems = candidates
            .Select(static candidate => new SimBodyCandidateFamilyPickerItem(candidate))
            .ToArray();
        ReplaceCollection(SelectedSimBodyCandidateFamilies, pickerItems);

        foreach (var family in SelectedSimBodyCandidateFamilies)
        {
            family.PropertyChanged += OnSimBodyCandidateFamilyPickerItemPropertyChanged;
        }
    }

    private void OnSimCasSlotFamilyPickerItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            string.Equals(e.PropertyName, nameof(SimCasSlotFamilyPickerItem.SelectedOption), StringComparison.Ordinal) ||
            string.Equals(e.PropertyName, nameof(SimCasSlotFamilyPickerItem.OverviewSelectionText), StringComparison.Ordinal))
        {
            NotifySimArchetypeInspectorStateChanged();
        }
    }

    private void OnSimBodyCandidateFamilyPickerItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.PropertyName) ||
            string.Equals(e.PropertyName, nameof(SimBodyCandidateFamilyPickerItem.SelectedOption), StringComparison.Ordinal))
        {
            RebuildSimBodyAssemblyRecipeState();
            RebuildSimResolvedBodyGraphState();
            RebuildSimBodyPreviewCoverageState();
            NotifySimArchetypeInspectorStateChanged();
            if (selectedAsset?.AssetKind == AssetKind.Sim && selectedAssetGraph?.SimGraph is not null)
            {
                _ = RefreshSelectedSimBodyPreviewAsync();
            }
        }
    }

    private void RebuildSimBodyAssemblyRecipeState()
    {
        var activeLabels = GetPreferredSimBodyPreviewFamilies()
            .Select(static family => family.Label)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mode = SimBodyAssemblyPolicy.GetMode(activeLabels);
        var recipeItems = SelectedSimBodyCandidateFamilies
            .OrderBy(static family => GetBodyRecipeSortOrder(family.Label))
            .ThenBy(static family => family.Label, StringComparer.OrdinalIgnoreCase)
            .Select(family =>
            {
                var state = SimBodyAssemblyPolicy.GetLayerState(family.Label, activeLabels);
                return SimBodyAssemblyRecipeItem.Create(
                    family,
                    state,
                    BuildCurrentSimBodyAssemblyStateNotes(family, state, mode));
            })
            .ToArray();
        ReplaceCollection(SelectedSimBodyAssemblyRecipe, recipeItems);
    }

    private void RebuildSimResolvedBodyGraphState()
    {
        var renderedLabels = SelectedSimBodyPreviewLayers
            .Select(static layer => layer.Label)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var activeLabels = GetPreferredSimBodyPreviewFamilies()
            .Select(static family => family.Label)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var graphItems = SelectedSimBodyCandidateFamilies
            .OrderBy(static family => GetBodyRecipeSortOrder(family.Label))
            .ThenBy(static family => family.Label, StringComparer.OrdinalIgnoreCase)
            .Select(family =>
            {
                var state = SimBodyAssemblyPolicy.GetLayerState(family.Label, activeLabels);
                var isRendered = renderedLabels.Contains(family.Label);
                return SimBodyGraphLayerItem.Create(family, state, isRendered);
            })
            .ToArray();

        ReplaceCollection(SelectedSimBodyGraphLayers, graphItems);
    }

    private void RebuildSimBodyPreviewCoverageState()
    {
        var renderedLabels = SelectedSimBodyPreviewLayers
            .Select(static layer => layer.Label)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var coverageItems = SelectedSimBodyAssemblyRecipe
            .OrderBy(static item => GetBodyRecipeSortOrder(item.Label))
            .ThenBy(static item => item.Label, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var isRendered = renderedLabels.Contains(item.Label);
                var status = item.IsActive
                    ? (isRendered ? "Rendered" : "Missing from current preview")
                    : (isRendered ? "Rendered alternate layer" : "Not part of current preview");
                var notes = item.IsActive
                    ? (isRendered
                        ? "This active body layer is present in the current base-body preview."
                        : "This active body layer is part of the current recipe but did not produce a renderable base-body preview layer.")
                    : (isRendered
                        ? "This non-primary body layer still contributed to the current base-body preview."
                        : item.Notes);
                return new SimBodyPreviewCoverageItem(item.Label, status, notes);
            })
            .ToArray();
        ReplaceCollection(SelectedSimBodyPreviewCoverage, coverageItems);
    }

    private void SetAssetVariantOptions(IReadOnlyList<AssetVariantSummary> variants)
    {
        suppressAssetVariantSelectionUpdates = true;
        try
        {
            if (variants.Count == 0)
            {
                ReplaceCollection(AssetVariantItems, []);
                SelectedAssetVariant = null;
            }
            else
            {
                var options = new List<AssetVariantPickerItem> { AssetVariantPickerItem.CreateDefault(variants.Count) };
                options.AddRange(
                    variants
                        .OrderBy(static variant => variant.VariantKind, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(static variant => variant.VariantIndex)
                        .Select(AssetVariantPickerItem.Create));
                ReplaceCollection(AssetVariantItems, options);
                SelectedAssetVariant = AssetVariantItems.FirstOrDefault();
            }
        }
        finally
        {
            suppressAssetVariantSelectionUpdates = false;
        }

        Interlocked.Increment(ref assetVariantThumbnailLoadGeneration);
        SelectedAssetVariantThumbnailSource = null;
        SelectedAssetVariantThumbnailText = variants.Count == 0
            ? "No indexed variants were found for this logical asset."
            : "Catalog default is selected. Choose a swatch or preset to inspect its indexed metadata.";
        NotifySelectedAssetVariantStateChanged();
    }

    private void NotifySelectedAssetVariantStateChanged()
    {
        OnPropertyChanged(nameof(HasAssetVariantOptions));
        OnPropertyChanged(nameof(CanSelectAssetVariant));
        OnPropertyChanged(nameof(AssetVariantPickerVisibility));
        OnPropertyChanged(nameof(AssetVariantPickerSummary));
        OnPropertyChanged(nameof(SelectedAssetVariantDescriptor));
        OnPropertyChanged(nameof(SelectedAssetVariantMetadataText));
        OnPropertyChanged(nameof(SelectedAssetVariantThumbnailVisibility));
    }

    private void NotifySimArchetypeInspectorStateChanged()
    {
        OnPropertyChanged(nameof(SimArchetypeInspectorVisibility));
        OnPropertyChanged(nameof(SimArchetypeInspectorSummary));
        OnPropertyChanged(nameof(SimBodyAssemblyModeText));
        OnPropertyChanged(nameof(SimBodyGraphSummaryText));
        OnPropertyChanged(nameof(SimResolvedBodyGraphSummaryText));
        OnPropertyChanged(nameof(SimResolvedBodyGraphDetailsText));
        OnPropertyChanged(nameof(SimBodyPreviewCoverageText));
    }

    private string BuildAssetVariantPickerSummary()
    {
        if (selectedAssetVariants.Count == 0)
        {
            return "No indexed swatches or presets were found for the selected logical asset.";
        }

        var kindBreakdown = string.Join(
            ", ",
            selectedAssetVariants
                .GroupBy(static variant => variant.VariantKind, StringComparer.OrdinalIgnoreCase)
                .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static group => $"{group.Count():N0} {group.Key.ToLowerInvariant()}"));

        return $"{selectedAssetVariants.Count:N0} indexed variant rows: {kindBreakdown}.";
    }

    private string BuildSimArchetypeInspectorSummary()
    {
        if (selectedAssetGraph?.SimGraph is not { } simGraph)
        {
            return "Select a Sim archetype to inspect its current slot and morph structure.";
        }

        var selectedBodyChoices = SelectedSimBodyCandidateFamilies.Count(static family => family.SelectedOption is not null);
        var activeBodyLayers = SelectedSimBodyAssemblyRecipe.Count(static item => item.IsActive);
        var activeResolvedBodyGraphLayers = SelectedSimBodyGraphLayers.Count(static layer => layer.IsActive);
        var renderedPreviewLayers = SelectedSimBodyPreviewLayers.Count;
        var missingPreviewLayers = SelectedSimBodyPreviewCoverage.Count(static item => string.Equals(item.StatusLabel, "Missing from current preview", StringComparison.Ordinal));
        var pendingGraphNodes = SelectedSimBodyGraphNodes.Count(static node => node.State == SimBodyGraphNodeState.Pending);
        var selectedSlotChoices = SelectedSimCasSlotFamilies.Count(static family => family.SelectedOption is not null);
        var selectedTemplateText = SelectedSimTemplateOption?.DisplayLabel ?? "No template selected";
        return $"{simGraph.Metadata.SpeciesLabel} | {simGraph.Metadata.AgeLabel} | {simGraph.Metadata.GenderLabel} | {simGraph.TemplateOptions.Count} template variation(s), selected template: {selectedTemplateText} | {simGraph.BodyFoundation.Count} body foundation block(s), {simGraph.BodySources.Count} body source reference block(s), {simGraph.BodyCandidates.Count} resolved body candidate block(s), {selectedBodyChoices} selected body example(s), {activeBodyLayers} active body assembly layer(s), {activeResolvedBodyGraphLayers} active resolved graph layer(s), {renderedPreviewLayers} rendered preview layer(s), {missingPreviewLayers} missing preview layer(s), {pendingGraphNodes} pending body graph node(s), {BuildSimBodyAssemblyModeText()} | {simGraph.MorphGroups.Count} morph group(s), {simGraph.CasSlotCandidates.Count} CAS slot family/families, {selectedSlotChoices} selected CAS example(s)";
    }

    private string BuildSimBodyAssemblyModeText()
    {
        if (selectedAssetGraph?.SimGraph is not { } simGraph)
        {
            return "Assembly mode: unresolved";
        }

        var activeLabels = GetPreferredSimBodyPreviewFamilies()
            .Select(static family => family.Label)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var mode = SimBodyAssemblyPolicy.GetMode(activeLabels);
        var modeLabel = mode switch
        {
            SimBodyAssemblyMode.FullBodyShell => "Assembly mode: full-body shell",
            SimBodyAssemblyMode.BodyShell => "Assembly mode: primary body shell",
            SimBodyAssemblyMode.BodyUnderlayWithSplitLayers => "Assembly mode: shell underlay + split body layers",
            SimBodyAssemblyMode.SplitBodyLayers => "Assembly mode: split body layers",
            SimBodyAssemblyMode.FallbackSingleLayer => "Assembly mode: fallback single layer",
            _ => "Assembly mode: unresolved"
        };

        if (simGraph.BodyAssembly.Layers.Count == 0)
        {
            return modeLabel;
        }

        return $"{modeLabel} | {simGraph.BodyAssembly.Summary}";
    }

    private string BuildSimBodyPreviewCoverageText()
    {
        if (selectedAssetGraph?.SimGraph is not { } || SelectedSimBodyAssemblyRecipe.Count == 0)
        {
            return "Preview coverage: unresolved";
        }

        var activeCount = SelectedSimBodyAssemblyRecipe.Count(static item => item.IsActive);
        var renderedCount = SelectedSimBodyPreviewCoverage.Count(static item => string.Equals(item.StatusLabel, "Rendered", StringComparison.Ordinal));
        var missingCount = SelectedSimBodyPreviewCoverage.Count(static item => string.Equals(item.StatusLabel, "Missing from current preview", StringComparison.Ordinal));

        if (activeCount == 0)
        {
            return "Preview coverage: no active body layers are selected yet";
        }

        if (missingCount == 0)
        {
            return $"Preview coverage: {renderedCount}/{activeCount} active body layer(s) are currently rendered";
        }

        return $"Preview coverage: {renderedCount}/{activeCount} active body layer(s) rendered, {missingCount} still missing from the current base-body preview";
    }

    private string BuildSimBodyGraphSummaryText()
    {
        if (selectedAssetGraph?.SimGraph is not { } || SelectedSimBodyGraphNodes.Count == 0)
        {
            return "Body graph: unresolved";
        }

        var resolved = SelectedSimBodyGraphNodes.Count(static node => node.State == SimBodyGraphNodeState.Resolved);
        var approximate = SelectedSimBodyGraphNodes.Count(static node => node.State == SimBodyGraphNodeState.Approximate);
        var pending = SelectedSimBodyGraphNodes.Count(static node => node.State == SimBodyGraphNodeState.Pending);
        var unavailable = SelectedSimBodyGraphNodes.Count(static node => node.State == SimBodyGraphNodeState.Unavailable);

        return $"Body graph: {resolved} resolved, {approximate} approximate, {pending} pending, {unavailable} unavailable node(s)";
    }

    private string BuildSimResolvedBodyGraphSummaryText()
    {
        if (selectedAssetGraph?.SimGraph is not { } || SelectedSimBodyGraphLayers.Count == 0)
        {
            return "Resolved base-body graph: unresolved";
        }

        var shellLayers = SelectedSimBodyGraphLayers.Count(static layer => string.Equals(layer.RoleLabel, "Geometry shell", StringComparison.Ordinal));
        var overlayLayers = SelectedSimBodyGraphLayers.Count(static layer => string.Equals(layer.RoleLabel, "Footwear overlay", StringComparison.Ordinal));
        var renderedLayers = SelectedSimBodyGraphLayers.Count(static layer => layer.IsRendered);
        var activeLayers = SelectedSimBodyGraphLayers.Count(static layer => layer.IsActive);
        var assemblyBasis = lastSimAssemblyPlan?.BasisLabel ?? "Assembly basis unresolved";

        return $"Resolved base-body graph: {shellLayers} shell layer(s), {overlayLayers} overlay layer(s), {activeLayers} active layer(s), {renderedLayers} rendered layer(s), {assemblyBasis.ToLowerInvariant()}";
    }

    private string BuildSimResolvedBodyGraphDetailsText()
    {
        if (selectedAssetGraph?.SimGraph is not { } || SelectedSimBodyGraphLayers.Count == 0)
        {
            return "No resolved base-body graph layers are currently available.";
        }

        var lines = new List<string>();
        if (lastSimAssemblyPlan is not null)
        {
            lines.Add($"Assembly basis: {lastSimAssemblyPlan.BasisLabel} | {(lastSimAssemblyPlan.IncludesHeadShell ? "head included" : "body only")}");
            lines.Add($"Assembly notes: {lastSimAssemblyPlan.Notes}");
        }
        if (lastSimAssemblyGraph is not null)
        {
            lines.Add("Assembly inputs:");
            lines.AddRange(
                lastSimAssemblyGraph.Inputs.Select(static input =>
                    $"- {input.Label}: {input.StatusLabel} | {(input.IsAccepted ? "accepted" : "not accepted")} | {input.Notes}"));
            lines.Add("Assembly stages:");
            lines.AddRange(
                lastSimAssemblyGraph.Stages
                    .OrderBy(static stage => stage.Order)
                    .Select(static stage => $"- {stage.Label}: {stage.State} | {stage.Notes}"));
            lines.Add("Assembly payload:");
            lines.Add($"- {lastSimAssemblyGraph.Payload.Label}: {lastSimAssemblyGraph.Payload.StatusLabel} | anchor bones {lastSimAssemblyGraph.Payload.AnchorBoneCount}, accepted contributions {lastSimAssemblyGraph.Payload.AcceptedContributionCount}, merged meshes {lastSimAssemblyGraph.Payload.MergedMeshCount}, rebased weights {lastSimAssemblyGraph.Payload.RebasedWeightCount}, mapped bone refs {lastSimAssemblyGraph.Payload.MappedBoneReferenceCount}, added bones {lastSimAssemblyGraph.Payload.AddedBoneCount} | {lastSimAssemblyGraph.Payload.Notes}");
            lines.Add("Payload anchor:");
            lines.Add($"- {lastSimAssemblyGraph.PayloadAnchor.Label}: {lastSimAssemblyGraph.PayloadAnchor.StatusLabel} | bones {lastSimAssemblyGraph.PayloadAnchor.BoneCount} | {string.Join(", ", lastSimAssemblyGraph.PayloadAnchor.BoneNames)} | {lastSimAssemblyGraph.PayloadAnchor.Notes}");
            lines.Add("Payload bone maps:");
            lines.AddRange(
                lastSimAssemblyGraph.PayloadBoneMaps.Select(static boneMap =>
                    $"- {boneMap.Label}: {boneMap.SourceLabel} | source bones {boneMap.SourceBoneCount}, reused refs {boneMap.ReusedBoneReferenceCount}, added bones {boneMap.AddedBoneCount}, rebased weights {boneMap.RebasedWeightCount} | {boneMap.Notes}"));
            lines.Add("Payload mesh batches:");
            lines.AddRange(
                lastSimAssemblyGraph.PayloadMeshBatches.Select(static meshBatch =>
                    $"- {meshBatch.Label}: {meshBatch.SourceLabel} | meshes {meshBatch.MeshCount}, material start {meshBatch.MaterialStartIndex}, material count {meshBatch.MaterialCount} | {meshBatch.Notes}"));
            lines.Add("Payload nodes:");
            lines.AddRange(
                lastSimAssemblyGraph.PayloadNodes
                    .OrderBy(static payloadNode => payloadNode.Order)
                    .Select(static payloadNode =>
                        $"- {payloadNode.Label}: {payloadNode.Kind} | {payloadNode.StatusLabel} | {payloadNode.Summary} | {payloadNode.Notes}"));
            lines.Add("Application passes:");
            lines.Add($"- {lastSimAssemblyGraph.Application.Label}: {lastSimAssemblyGraph.Application.StatusLabel} | prepared {lastSimAssemblyGraph.Application.PreparedPassCount}, pending {lastSimAssemblyGraph.Application.PendingPassCount}, unavailable {lastSimAssemblyGraph.Application.UnavailablePassCount} | {lastSimAssemblyGraph.Application.Notes}");
            lines.AddRange(
                lastSimAssemblyGraph.ApplicationPasses
                    .OrderBy(static pass => pass.Order)
                    .Select(static pass =>
                        $"- {pass.Label}: {pass.State} | {pass.StatusLabel} | inputs {pass.InputCount} | {pass.Notes}"));
            lines.Add("Application targets:");
            lines.AddRange(
                lastSimAssemblyGraph.ApplicationTargets.Select(static target =>
                    $"- {target.Label}: {target.PassLabel} | {target.StatusLabel} | targets {target.TargetCount} | {target.Notes}"));
            lines.Add("Application plans:");
            lines.AddRange(
                lastSimAssemblyGraph.ApplicationPlans
                    .Select(static plan =>
                        $"- {plan.Label}: {plan.PassLabel} | {plan.StatusLabel} | targets {plan.TargetCount}, operations {plan.OperationCount} | {plan.Notes}"));
            lines.Add("Application transforms:");
            lines.AddRange(
                lastSimAssemblyGraph.ApplicationTransforms
                    .Select(static transform =>
                        $"- {transform.Label}: {transform.PassLabel} | {transform.StatusLabel} | targets {transform.TargetCount}, operations {transform.OperationCount} | {transform.Notes}"));
            lines.Add("Application outcomes:");
            lines.AddRange(
                lastSimAssemblyGraph.ApplicationOutcomes
                    .Select(static outcome =>
                        $"- {outcome.Label}: {outcome.PassLabel} | {outcome.StatusLabel} | targets {outcome.TargetCount}, applied {outcome.AppliedCount} | {outcome.Notes}"));
            lines.Add("Assembly output:");
            lines.Add($"- {lastSimAssemblyGraph.Output.Label}: {lastSimAssemblyGraph.Output.StatusLabel} | meshes {lastSimAssemblyGraph.Output.MeshCount}, materials {lastSimAssemblyGraph.Output.MaterialCount}, bones {lastSimAssemblyGraph.Output.BoneCount} | {lastSimAssemblyGraph.Output.Notes}");
            lines.Add("Assembly contributions:");
            lines.AddRange(
                lastSimAssemblyGraph.Contributions.Select(static contribution =>
                    $"- {contribution.Label}: {contribution.StatusLabel} | meshes {contribution.MeshCount}, materials {contribution.MaterialCount}, bones {contribution.BoneCount}, rebased weights {contribution.RebasedWeightCount}, added bones {contribution.AddedBoneCount} | {contribution.Notes}"));
            lines.Add("Assembly graph:");
            lines.AddRange(
                lastSimAssemblyGraph.Nodes
                    .OrderBy(static node => node.Order)
                    .Select(static node => $"- {node.Label}: {node.State} | {node.Notes}"));
        }

        lines.AddRange(
            SelectedSimBodyGraphLayers.Select(static layer =>
                $"- {layer.Label}: {layer.RoleLabel} | {layer.StatusLabel} | {layer.SelectedAssetLabel} | {layer.SourceLabel}"));
        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildCurrentSimBodyAssemblyStateNotes(
        SimBodyCandidateFamilyPickerItem family,
        SimBodyAssemblyLayerState state,
        SimBodyAssemblyMode mode)
    {
        if (family.SelectedOption is null)
        {
            return "No resolved body asset is currently selected for this layer.";
        }

        return state switch
        {
            SimBodyAssemblyLayerState.Active when mode == SimBodyAssemblyMode.BodyUnderlayWithSplitLayers &&
                                              SimBodyAssemblyPolicy.IsShellFamilyLabel(family.Label) =>
                $"Selected as the body underlay beneath active split layers. {family.Notes}",
            SimBodyAssemblyLayerState.Active => family.Notes,
            SimBodyAssemblyLayerState.Blocked when mode == SimBodyAssemblyMode.FullBodyShell =>
                $"Currently suppressed by the selected full-body shell. {family.Notes}",
            SimBodyAssemblyLayerState.Blocked when mode == SimBodyAssemblyMode.BodyShell =>
                $"Currently suppressed by the selected primary body shell. {family.Notes}",
            _ => $"Available as an alternate base-body layer. {family.Notes}"
        };
    }

    private string BuildSelectedAssetVariantDescriptor() =>
        SelectedAssetVariant?.Variant is null
            ? "Catalog default"
            : SelectedAssetVariant.Variant.DisplayLabel;

    private string BuildSelectedAssetVariantMetadataText()
    {
        var variant = SelectedAssetVariant?.Variant;
        if (variant is null)
        {
            return selectedAssetVariants.Count == 0
                ? "The selected asset does not currently expose indexed swatch or preset rows."
                : "The canonical catalog row is selected. Pick a swatch or preset to inspect its indexed data.";
        }

        var details = new List<string>
        {
            $"{variant.VariantKind} {variant.VariantIndex + 1}",
            Path.GetFileName(variant.PackagePath)
        };
        if (!string.IsNullOrWhiteSpace(variant.SwatchHex))
        {
            details.Add(variant.SwatchHex);
        }

        if (!string.IsNullOrWhiteSpace(variant.ThumbnailTgi))
        {
            details.Add($"thumb {variant.ThumbnailTgi}");
        }

        if (!string.IsNullOrWhiteSpace(variant.Diagnostics))
        {
            details.Add(variant.Diagnostics);
        }

        return string.Join(" | ", details);
    }

    private async Task RefreshSelectedAssetVariantThumbnailAsync(AssetVariantPickerItem? option)
    {
        var generation = Interlocked.Increment(ref assetVariantThumbnailLoadGeneration);
        SelectedAssetVariantThumbnailSource = null;

        var variant = option?.Variant;
        if (variant is null)
        {
            SelectedAssetVariantThumbnailText = selectedAssetVariants.Count == 0
                ? "No indexed swatch or preset rows are available for this asset."
                : "Catalog default is selected. Choose a swatch or preset to inspect its indexed thumbnail.";
            return;
        }

        if (string.IsNullOrWhiteSpace(variant.ThumbnailTgi))
        {
            SelectedAssetVariantThumbnailText = "This indexed variant does not currently expose a dedicated thumbnail link.";
            return;
        }

        SelectedAssetVariantThumbnailText = $"Loading indexed thumbnail {variant.ThumbnailTgi}...";
        var thumbnailResource = await indexStore.GetResourceByTgiAsync(variant.PackagePath, variant.ThumbnailTgi, CancellationToken.None);
        if (thumbnailResource is null)
        {
            thumbnailResource = (await indexStore.GetResourcesByTgiAsync(variant.ThumbnailTgi, CancellationToken.None))
                .FirstOrDefault();
        }

        if (generation != assetVariantThumbnailLoadGeneration)
        {
            return;
        }

        if (thumbnailResource is null)
        {
            SelectedAssetVariantThumbnailText = $"Indexed thumbnail {variant.ThumbnailTgi} could not be resolved from the active catalog.";
            return;
        }

        var thumbnailPreview = await previewService.CreatePreviewAsync(thumbnailResource, CancellationToken.None);
        if (generation != assetVariantThumbnailLoadGeneration)
        {
            return;
        }

        if (thumbnailPreview.Content is TexturePreviewContent texturePreview)
        {
            SelectedAssetVariantThumbnailSource = await CreateBitmapAsync(texturePreview.PngBytes);
            SelectedAssetVariantThumbnailText = $"Indexed thumbnail {variant.ThumbnailTgi} from {Path.GetFileName(thumbnailResource.PackagePath)}.";
            return;
        }

        SelectedAssetVariantThumbnailText = $"Indexed thumbnail {variant.ThumbnailTgi} resolved, but it did not decode into an image preview.";
    }

    private void SetSceneLodOptions(IEnumerable<SceneLodOption> options, string? selectedFullTgi = null)
    {
        suppressSceneLodSelectionPreview = true;
        try
        {
            ReplaceCollection(AvailableSceneLods, options);
            SelectedSceneLod = selectedFullTgi is null
                ? AvailableSceneLods.FirstOrDefault()
                : AvailableSceneLods.FirstOrDefault(option => string.Equals(option.Resource.Key.FullTgi, selectedFullTgi, StringComparison.OrdinalIgnoreCase))
                    ?? AvailableSceneLods.FirstOrDefault();
        }
        finally
        {
            suppressSceneLodSelectionPreview = false;
        }

        OnPropertyChanged(nameof(HasSceneLodOptions));
        OnPropertyChanged(nameof(CanSelectSceneLod));
    }

    private void SetSceneTextureSlotOptions(CanonicalScene? scene)
    {
        var slots = scene is null
            ? ["All"]
            : PrependAll(
                scene.Materials
                    .SelectMany(static material => material.Textures)
                    .Select(static texture => texture.Slot)
                    .Where(static slot => !string.IsNullOrWhiteSpace(slot))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static slot => slot, StringComparer.OrdinalIgnoreCase)
                    .ToArray());

        ReplaceCollection(AvailableSceneTextureSlots, slots);
        if (!AvailableSceneTextureSlots.Contains(SelectedSceneTextureSlot, StringComparer.OrdinalIgnoreCase))
        {
            SelectedSceneTextureSlot = "All";
        }

        OnPropertyChanged(nameof(HasSceneTextureSlotOptions));
        OnPropertyChanged(nameof(CanSelectSceneTextureSlot));
    }

    private static IReadOnlyList<SceneLodOption> BuildSceneLodOptions(AssetGraph graph, ResourceMetadata selectedSceneRoot)
    {
        if (graph.BuildBuyGraph is not null)
        {
            var options = new List<SceneLodOption>
            {
                new("Auto (Model root)", graph.BuildBuyGraph.ModelResource)
            };

            foreach (var embeddedLod in graph.BuildBuyGraph.EmbeddedLodLabels
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                options.Add(new SceneLodOption($"{embeddedLod} (embedded in Model root)", graph.BuildBuyGraph.ModelResource));
            }

            foreach (var lod in graph.BuildBuyGraph.ModelLodResources
                         .OrderBy(static resource => resource.Key.Group)
                         .ThenBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase))
            {
                options.Add(new SceneLodOption($"{DescribeBuildBuyLod(lod.Key.Group)} ({lod.Key.FullTgi})", lod));
            }

            return options;
        }

        if (graph.CasGraph?.GeometryResource is not null)
        {
            return graph.CasGraph.GeometryResources
                .OrderBy(static resource => resource.Key.Group)
                .ThenBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Select(resource =>
                    string.Equals(resource.Key.FullTgi, selectedSceneRoot.Key.FullTgi, StringComparison.OrdinalIgnoreCase)
                        ? new SceneLodOption("Geometry root", resource)
                        : new SceneLodOption($"Geometry root ({resource.Key.FullTgi})", resource))
                .GroupBy(static option => option.Resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray();
        }

        if (graph.General3DGraph is not null)
        {
            var options = new List<SceneLodOption>();
            var general3DGraph = graph.General3DGraph;
            if (general3DGraph.SceneRootResource.Key.TypeName == "Model")
            {
                options.Add(new SceneLodOption("Auto (Model root)", general3DGraph.SceneRootResource));
                foreach (var embeddedLod in general3DGraph.EmbeddedLodLabels
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    options.Add(new SceneLodOption($"{embeddedLod} (embedded in Model root)", general3DGraph.SceneRootResource));
                }
            }

            foreach (var lod in general3DGraph.ModelLodResources
                         .OrderBy(static resource => resource.Key.Group)
                         .ThenBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase))
            {
                options.Add(new SceneLodOption($"{DescribeBuildBuyLod(lod.Key.Group)} ({lod.Key.FullTgi})", lod));
            }

            foreach (var geometry in general3DGraph.GeometryResources
                         .Where(resource => !string.Equals(resource.Key.FullTgi, selectedSceneRoot.Key.FullTgi, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase))
            {
                options.Add(new SceneLodOption($"Geometry root ({geometry.Key.FullTgi})", geometry));
            }

            if (options.Count == 0)
            {
                options.Add(new SceneLodOption(general3DGraph.SceneRootResource.Key.TypeName, general3DGraph.SceneRootResource));
            }

            return options
                .GroupBy(static option => option.Resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray();
        }

        return [new SceneLodOption(selectedSceneRoot.Key.TypeName, selectedSceneRoot)];
    }

    private async Task<PreviewResult> BuildCasPreviewResultAsync(
        CasAssetGraph casGraph,
        CancellationToken cancellationToken,
        IProgress<PreviewBuildProgress>? progress = null)
    {
        return await previewService.CreatePreviewAsync(casGraph, cancellationToken, progress);
    }

    private static string DescribeBuildBuyLod(uint group) => group switch
    {
        0x00000000 => "High Detail",
        0x00000001 => "Medium Detail",
        0x00000002 => "Low Detail",
        0x00010000 => "High Detail Shadow",
        0x00010001 => "Medium Detail Shadow",
        0x00010002 => "Low Detail Shadow",
        _ => $"LOD {group:X8}"
    };

    private void NotifyPreviewStateChanged()
    {
        OnPropertyChanged(nameof(IsScenePreviewActive));
        OnPropertyChanged(nameof(IsImagePreviewActive));
        OnPropertyChanged(nameof(IsDiagnosticsPreviewActive));
        OnPropertyChanged(nameof(DiagnosticsPreviewVisibility));
        OnPropertyChanged(nameof(DiagnosticsTabsVisibility));
        OnPropertyChanged(nameof(CanResetView));
        OnPropertyChanged(nameof(CanExportSelectedAsset));
        OnPropertyChanged(nameof(HasSceneLodOptions));
        OnPropertyChanged(nameof(CanSelectSceneLod));
        OnPropertyChanged(nameof(HasSceneTextureSlotOptions));
        OnPropertyChanged(nameof(CanSelectSceneTextureSlot));
        OnPropertyChanged(nameof(EffectivePreviewLoadStatus));
        OnPropertyChanged(nameof(PreviewLoadBrush));
    }

    private IProgress<PreviewBuildProgress> CreatePreviewBuildProgressReporter(double baseStep, double span) =>
        new Progress<PreviewBuildProgress>(progress =>
        {
            var fraction = Math.Clamp(progress.Fraction ?? 0d, 0d, 1d);
            var easedFraction = Math.Pow(fraction, 1.35);
            var mappedStep = baseStep + (easedFraction * span);
            AdvancePreviewLoad(mappedStep, progress.Status);
        });

    private static async Task YieldToUiAsync()
    {
        await Task.Yield();
    }

    partial void OnSelectedWorkerCountChanged(int value)
    {
        selectedWorkerCount = IndexingRunOptions.ClampWorkerCount(value);
        OnPropertyChanged(nameof(SelectedWorkerCount));
    }

    partial void OnSelectedMemoryUsagePercentChanged(int value)
    {
        selectedMemoryUsagePercent = IndexingMemoryBudgetCalculator.NormalizePercent(value);
        OnPropertyChanged(nameof(SelectedMemoryUsagePercent));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }
    partial void OnIsPreviewLoadingChanged(bool value) => NotifyPreviewStateChanged();
    partial void OnPreviewLoadStatusChanged(string value) => NotifyPreviewStateChanged();
    partial void OnPreviewLoadCompletedChanged(bool value) => NotifyPreviewStateChanged();
    partial void OnPreviewSurfaceModeChanged(PreviewSurfaceMode value) => NotifyPreviewStateChanged();
    partial void OnPreviewImageSourceChanged(BitmapImage? value) => NotifyPreviewStateChanged();
    partial void OnSelectedAssetVariantThumbnailSourceChanged(BitmapImage? value) => NotifySelectedAssetVariantStateChanged();
    partial void OnSelectedAssetVariantThumbnailTextChanged(string value) => NotifySelectedAssetVariantStateChanged();
    partial void OnCurrentSceneChanged(CanonicalScene? value)
    {
        SetSceneTextureSlotOptions(value);
        NotifyPreviewStateChanged();
    }
    partial void OnPreviewTextChanged(string value) => NotifyPreviewStateChanged();
    partial void OnDetailsTextChanged(string value) => NotifyPreviewStateChanged();
    partial void OnSelectedSceneRenderModeChanged(SceneRenderMode value) => NotifyPreviewStateChanged();
    partial void OnSelectedSceneTextureSlotChanged(string value) => NotifyPreviewStateChanged();
    partial void OnSelectedSceneLodChanged(SceneLodOption? value)
    {
        if (!suppressSceneLodSelectionPreview && value is not null)
        {
            _ = SelectSceneLodAsync(value);
        }
    }

    partial void OnSelectedSimTemplateOptionChanged(SimTemplatePickerItem? value)
    {
        OnPropertyChanged(nameof(SelectedSimTemplateNotesText));
        OnPropertyChanged(nameof(SelectedSimTemplateDetailsText));
        NotifySimArchetypeInspectorStateChanged();
        if (!suppressSimTemplateSelectionUpdates &&
            value is not null &&
            selectedAsset?.AssetKind == AssetKind.Sim)
        {
            _ = RefreshSelectedSimTemplateAsync();
        }
    }

    partial void OnSelectedAssetVariantChanged(AssetVariantPickerItem? value)
    {
        NotifySelectedAssetVariantStateChanged();
        if (suppressAssetVariantSelectionUpdates)
        {
            return;
        }

        if (selectedAsset is not null && selectedAssetGraph is not null)
        {
            DetailsText = BuildCurrentDisplayedAssetDetails();
        }

        if (selectedAssetSceneRoot is not null && selectedAssetBaseScenePreview is not null)
        {
            _ = RefreshSelectedAssetVariantSceneAsync();
        }

        _ = RefreshSelectedAssetVariantThumbnailAsync(value);
    }

    private async Task RefreshSelectedAssetVariantSceneAsync()
    {
        if (selectedAssetSceneRoot is null || selectedAssetBaseScenePreview is null)
        {
            return;
        }

        var generation = Interlocked.Increment(ref assetVariantPreviewLoadGeneration);
        var preview = await BuildDisplayedScenePreviewAsync(selectedAssetBaseScenePreview, CancellationToken.None);
        if (generation != assetVariantPreviewLoadGeneration || preview is null)
        {
            return;
        }

        selectedAssetScenePreview = preview;
        var assetDetails = BuildCurrentAssetDetails();
        await ApplyPreviewAsync(
            selectedAssetSceneRoot,
            new PreviewResult(PreviewKind.Scene, preview),
            assetDetails);
    }

    partial void OnSelectedBrowserModeChanged(BrowserMode value)
    {
        UpdateBrowsePresentation();
        if (value == BrowserMode.AssetBrowser)
        {
            if (assetQueryDirty || !assetBrowserHasLoaded)
            {
                _ = RefreshActiveBrowserAsync(resetWindow: true);
            }
        }
        else if (rawQueryDirty || !rawBrowserHasLoaded)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnIncludeGameSourcesChanged(bool value)
    {
        MarkAllQueriesDirty();
        UpdateBrowsePresentation();
        _ = RefreshActiveBrowserAsync(resetWindow: true);
    }

    partial void OnIncludeDlcSourcesChanged(bool value)
    {
        MarkAllQueriesDirty();
        UpdateBrowsePresentation();
        _ = RefreshActiveBrowserAsync(resetWindow: true);
    }

    partial void OnIncludeModsSourcesChanged(bool value)
    {
        MarkAllQueriesDirty();
        UpdateBrowsePresentation();
        _ = RefreshActiveBrowserAsync(resetWindow: true);
    }

    partial void OnAssetSearchTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        _ = DebounceSearchAsync(BrowserMode.AssetBrowser);
    }

    partial void OnAssetDomainChanged(AssetBrowserDomain value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        OnPropertyChanged(nameof(AssetSearchPlaceholder));
        OnPropertyChanged(nameof(AssetSearchHelp));
        OnPropertyChanged(nameof(AssetFacetSummaryText));
        OnPropertyChanged(nameof(AssetCatalogSignalsVisibility));
        UpdateBrowsePresentation();
        _ = ReloadAssetFacetOptionsAsync();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetCategoryTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetRootTypeTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetIdentityTypeTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetPrimaryGeometryTypeTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetThumbnailTypeTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetCatalogSignal0020TextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetCatalogSignal002CTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetCatalogSignal0030TextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetCatalogSignal0034TextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetPackageTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetPackageRelativeTextChanged(string value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetHasThumbnailOnlyChanged(bool value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetVariantsOnlyChanged(bool value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnAssetRequireSceneRootChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireExactGeometryCandidateChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireMaterialReferencesChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireTextureReferencesChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireIdentityMetadataChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireRigReferenceChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireGeometryReferenceChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireMaterialResourceCandidateChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireTextureResourceCandidateChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequirePackageLocalGraphChanged(bool value) => RefreshAssetCapabilityFilterChanged();
    partial void OnAssetRequireDiagnosticsChanged(bool value) => RefreshAssetCapabilityFilterChanged();

    partial void OnAssetSortChanged(AssetBrowserSort value)
    {
        SyncAssetStateFromProperties();
        assetQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.AssetBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawSearchTextChanged(string value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        _ = DebounceSearchAsync(BrowserMode.RawResourceBrowser);
    }

    partial void OnRawDomainChanged(RawResourceDomain value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawTypeNameTextChanged(string value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawPackageTextChanged(string value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawGroupHexTextChanged(string value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawInstanceHexTextChanged(string value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawPreviewableOnlyChanged(bool value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawExportCapableOnlyChanged(bool value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawCompressedKnownOnlyChanged(bool value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawLinkFilterChanged(ResourceLinkFilter value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    partial void OnRawSortChanged(RawResourceSort value)
    {
        SyncRawStateFromProperties();
        rawQueryDirty = true;
        UpdateBrowsePresentation();
        if (SelectedBrowserMode == BrowserMode.RawResourceBrowser)
        {
            _ = RefreshActiveBrowserAsync(resetWindow: true);
        }
    }

    private void ApplyIndexingProgress(IndexingProgress progress, IndexingDialogViewModel indexingDialogViewModel, IndexingTelemetrySession telemetrySession)
    {
        telemetrySession.Record(progress);
        indexingDialogViewModel.ApplyProgress(progress);
        StatusMessage = string.IsNullOrWhiteSpace(progress.Message)
            ? $"Indexing: {progress.PackagesProcessed}/{progress.PackagesTotal} packages"
            : progress.Message;

        if (progress.Summary is not null)
        {
            AppendRunSummary(progress.Summary);
        }
    }

    private void AppendRunSummary(IndexingRunSummary summary)
    {
        var indexedPackages = Math.Max(0, summary.PackagesProcessed - summary.PackagesSkipped - summary.PackagesFailed);
        Log.AppendRange(
        [
            $"Index summary: elapsed {summary.TotalElapsed:hh\\:mm\\:ss}, discovered {summary.PackagesDiscovered}, queued {summary.PackagesQueued}, indexed {indexedPackages}, skipped {summary.PackagesSkipped}, failed {summary.PackagesFailed}, resources {summary.ResourcesProcessed:N0}, average {summary.AverageThroughput:N0} res/sec.",
            $"Hot phases: {string.Join("; ", summary.PhaseBreakdown.Take(5))}",
            $"Slowest packages: {string.Join("; ", summary.SlowestPackages.Select(static package => $"{Path.GetFileName(package.PackagePath)} {package.Elapsed:hh\\:mm\\:ss}"))}",
            summary.Failures.Count == 0
                ? "Failures: none."
                : $"Failures: {string.Join("; ", summary.Failures.Select(static failure => $"{Path.GetFileName(failure.PackagePath)} ({failure.Reason})"))}"
        ]);
    }

    private static IReadOnlyList<ResourceMetadata> BuildSourceResources(AssetGraph graph, ResourceMetadata sceneRoot)
    {
        var resources = new List<ResourceMetadata> { sceneRoot };
        if (graph.BuildBuyGraph is not null)
        {
            resources.AddRange(graph.BuildBuyGraph.IdentityResources);
            resources.AddRange(graph.BuildBuyGraph.ModelLodResources);
            resources.AddRange(graph.BuildBuyGraph.MaterialResources);
            resources.AddRange(graph.BuildBuyGraph.TextureResources);
        }
        else if (graph.CasGraph is not null)
        {
            resources.AddRange(graph.CasGraph.IdentityResources);
            resources.AddRange(graph.CasGraph.GeometryResources);
            resources.AddRange(graph.CasGraph.RigResources);
            resources.AddRange(graph.CasGraph.MaterialResources);
            resources.AddRange(graph.CasGraph.TextureResources);
        }
        else if (graph.General3DGraph is not null)
        {
            resources.AddRange(graph.General3DGraph.ModelResources);
            resources.AddRange(graph.General3DGraph.ModelLodResources);
            resources.AddRange(graph.General3DGraph.GeometryResources);
            resources.AddRange(graph.General3DGraph.RigResources);
            resources.AddRange(graph.General3DGraph.MaterialResources);
            resources.AddRange(graph.General3DGraph.TextureResources);
        }

        return resources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<MaterialManifestEntry> BuildMaterialManifest(CanonicalScene scene) =>
        scene.Materials
            .Select(static material => new MaterialManifestEntry(
                material.Name,
                material.ShaderName,
                material.IsTransparent,
                material.AlphaMode,
                material.AlphaTextureSlot,
                material.LayeredTextureSlots,
                material.Approximation,
                material.Textures.Select(static texture => new MaterialTextureEntry(
                    texture.Slot,
                    texture.FileName,
                    texture.SourceKey,
                    texture.SourcePackagePath,
                    texture.Semantic,
                    texture.SourceKind)).ToArray(),
                material.SourceKind))
            .ToArray();

    private static string Slugify(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var normalized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : char.ToLowerInvariant(ch)).ToArray());
        var collapsed = string.Join("_", normalized.Split([' ', '\t', '\r', '\n', '-', '/'], StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(collapsed) ? "logical_asset" : collapsed;
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private static void AppendCollection<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }

    private static IReadOnlyList<string> PrependAll(IReadOnlyList<string> values) =>
        ["All", .. values.Where(static value => !string.Equals(value, "All", StringComparison.Ordinal))];
}

internal sealed record CasVariantTextureCompositeResult(CanonicalScene Scene, string Diagnostics);

public enum SceneRenderMode
{
    Wireframe,
    RawUv,
    MaterialUv,
    FlatTexture,
    LitTexture
}

public sealed record SceneLodOption(string Label, ResourceMetadata Resource)
{
    public override string ToString() => Label;
}
