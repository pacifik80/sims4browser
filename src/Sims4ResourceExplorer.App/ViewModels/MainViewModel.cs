using System.Collections.ObjectModel;
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
    private readonly AssetBrowserState assetBrowserState = new();
    private readonly RawResourceBrowserState rawResourceBrowserState = new();

    private CancellationTokenSource? indexingCancellationTokenSource;
    private CancellationTokenSource? assetSearchDebounceTokenSource;
    private CancellationTokenSource? rawSearchDebounceTokenSource;
    private ResourceMetadata? selectedResource;
    private AssetSummary? selectedAsset;
    private AssetGraph? selectedAssetGraph;
    private ResourceMetadata? selectedAssetSceneRoot;
    private bool suppressSceneLodSelectionPreview;
    private byte[]? previewAudioBytes;
    private bool assetQueryDirty = true;
    private bool rawQueryDirty = true;
    private bool assetBrowserHasLoaded;
    private bool rawBrowserHasLoaded;
    private int assetTotalCount;
    private int rawTotalCount;

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
        IIndexingTelemetryRecorderService indexingTelemetryRecorderService)
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

        AvailableWorkerCounts = Enumerable.Range(1, IndexingRunOptions.GetMachineWorkerLimit()).ToArray();
        BrowserModes = Enum.GetValues<BrowserMode>();
        AssetDomains = Enum.GetValues<AssetBrowserDomain>();
        AssetSortOptions = Enum.GetValues<AssetBrowserSort>();
        RawDomains = Enum.GetValues<RawResourceDomain>();
        RawSortOptions = Enum.GetValues<RawResourceSort>();
        LinkFilters = Enum.GetValues<ResourceLinkFilter>();
        selectedWorkerCount = defaultIndexingOptions.MaxPackageConcurrency;
        UpdateBrowsePresentation();
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
    public ObservableLog Log { get; } = [];
    public IReadOnlyList<int> AvailableWorkerCounts { get; }
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
    private string resultSummary = "Choose a scope to begin.";

    [ObservableProperty]
    private string emptyStateMessage = "Choose a scope to begin.";

    [ObservableProperty]
    private SceneRenderMode selectedSceneRenderMode = SceneRenderMode.LitTexture;

    [ObservableProperty]
    private SceneLodOption? selectedSceneLod;

    [ObservableProperty]
    private string selectedSceneTextureSlot = "All";

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
    public string AssetSearchPlaceholder => AssetDomain == AssetBrowserDomain.BuildBuy
        ? "Search build/buy name, description, category, or package"
        : "Search CAS name, description, type, or package";
    public string RawSearchPlaceholder => "Search package path, type name, TGI, group, or instance";
    public string AssetSearchHelp => AssetDomain == AssetBrowserDomain.BuildBuy
        ? "Build/Buy uses identity, structure, raw catalog signals, and capability filters."
        : "CAS uses identity, structure, and capability filters; raw catalog signals are hidden.";
    public string RawSearchHelp => "Examples: objectcatalog, 00B2D882, 00000000, package fragment";
    public string AssetFacetSummaryText => AssetDomain == AssetBrowserDomain.BuildBuy
        ? "Build/Buy catalog filters combine human-facing metadata with raw ObjectCatalog signals."
        : "CAS filters stay focused on identity, structure, and preview support.";
    public Visibility AssetCatalogSignalsVisibility => AssetDomain == AssetBrowserDomain.BuildBuy ? Visibility.Visible : Visibility.Collapsed;
    public string EffectivePreviewLoadStatus => string.IsNullOrWhiteSpace(PreviewLoadStatus) ? "Ready" : PreviewLoadStatus;
    public Brush PreviewLoadBrush => new SolidColorBrush(PreviewLoadCompleted && !IsPreviewLoading ? Colors.ForestGreen : Colors.DodgerBlue);
    public bool IsScenePreviewActive => PreviewSurfaceMode == PreviewSurfaceMode.Scene && CurrentScene is not null;
    public bool IsImagePreviewActive => PreviewSurfaceMode == PreviewSurfaceMode.Image && PreviewImageSource is not null;
    public bool IsDiagnosticsPreviewActive => PreviewSurfaceMode == PreviewSurfaceMode.Diagnostics;
    public Visibility DiagnosticsPreviewVisibility => IsDiagnosticsPreviewActive ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DiagnosticsTabsVisibility => !string.IsNullOrWhiteSpace(PreviewText) || !string.IsNullOrWhiteSpace(DetailsText) ? Visibility.Visible : Visibility.Collapsed;
    public bool CanResetView => CurrentScene is not null;
    public bool CanExportSelectedAsset => PreviewInteractionPolicy.CanExportSelectedAsset(selectedAsset, selectedAssetGraph, selectedAssetSceneRoot, CurrentScene);
    public bool HasSceneLodOptions => AvailableSceneLods.Count > 0;
    public bool CanSelectSceneLod => AvailableSceneLods.Count > 1;
    public bool HasSceneTextureSlotOptions => AvailableSceneTextureSlots.Count > 0;
    public bool CanSelectSceneTextureSlot => AvailableSceneTextureSlots.Count > 1;

    public async Task InitializeAsync()
    {
        await indexStore.InitializeAsync(CancellationToken.None);
        var preferences = await appPreferencesService.LoadAsync(CancellationToken.None);
        SelectedWorkerCount = IndexingRunOptions.ClampWorkerCount(preferences.IndexWorkerCount);
        await ReloadSourcesAsync();
        await ReloadAssetFacetOptionsAsync();
        await RefreshActiveBrowserAsync(resetWindow: true);
    }

    public async Task AddSourceAsync(string rootPath, SourceKind kind)
    {
        if (DataSources.Any(source => string.Equals(source.RootPath, rootPath, StringComparison.OrdinalIgnoreCase)))
        {
            AppendLog($"Source already added: {rootPath}");
            return;
        }

        var displayName = $"{kind}: {Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar))}";
        DataSources.Add(new DataSourceDefinition(Guid.NewGuid(), displayName, rootPath, kind));
        await indexStore.UpsertDataSourcesAsync(DataSources, CancellationToken.None);
        MarkAllQueriesDirty();
        UpdateBrowsePresentation();
        AppendLog($"Added {kind} source {rootPath}");
    }

    public async Task RunIndexAsync(IndexingDialogViewModel indexingDialogViewModel)
    {
        if (IsBusy)
        {
            return;
        }

        if (DataSources.Count == 0)
        {
            StatusMessage = "Add at least one Game, DLC, or Mods folder first.";
            return;
        }

        IsBusy = true;
        indexingCancellationTokenSource = new CancellationTokenSource();
        StatusMessage = $"Indexing started with {SelectedWorkerCount} workers.";
        IndexingTelemetrySession? telemetrySession = null;

        try
        {
            await Task.Yield();
            await appPreferencesService.SaveAsync(new AppPreferences(SelectedWorkerCount), CancellationToken.None);
            var snapshotSources = DataSources.ToArray();
            var selectedWorkerCount = SelectedWorkerCount;
            var indexingToken = indexingCancellationTokenSource.Token;
            telemetrySession = await indexingTelemetryRecorderService
                .StartSessionAsync(snapshotSources, selectedWorkerCount, indexingToken);
            AppendLog($"Index telemetry: {telemetrySession.TimelinePath}");
            var progress = new Progress<IndexingProgress>(value => ApplyIndexingProgress(value, indexingDialogViewModel, telemetrySession));

            await Task.Run(
                async () => await packageIndexCoordinator.RunAsync(snapshotSources, progress, indexingToken, selectedWorkerCount).ConfigureAwait(false),
                indexingToken);

            await telemetrySession.CompleteAsync("completed", CancellationToken.None);
            indexingDialogViewModel.MarkCompleted("Indexing completed. Refreshing browser results...");
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
            StatusMessage = $"Indexing failed: {ex.Message}";
            AppendLog($"Indexing failed: {ex}");
            indexingDialogViewModel.MarkFailed($"Indexing failed: {ex.Message}");
        }
        finally
        {
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

    public Task RefreshActiveBrowserAsync(bool resetWindow = true) =>
        SelectedBrowserMode == BrowserMode.AssetBrowser
            ? RefreshAssetBrowserAsync(resetWindow, append: !resetWindow)
            : RefreshRawBrowserAsync(resetWindow, append: !resetWindow);

    public async Task LoadMoreAsync()
    {
        if (!HasMoreResults || IsQueryingResults)
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
        selectedAssetSceneRoot = null;
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
        selectedAssetSceneRoot = null;
        selectedResource = null;
        ResetPreviewState();
        BeginPreviewLoad(1d, "Scanning index...");
        await YieldToUiAsync();

        await AdvancePreviewLoadAsync(0.04, "Scanning index...");
        var packageResources = await indexStore.GetResourcesByInstanceAsync(asset.PackagePath, asset.RootKey.FullInstance, CancellationToken.None);
        await AdvancePreviewLoadAsync(0.10, "Building asset graph...");
        var graph = await assetGraphBuilder.BuildAssetGraphAsync(asset, packageResources, CancellationToken.None);
        selectedAssetGraph = graph;
        var assetDetails = AssetDetailsFormatter.BuildAssetDetails(asset, graph, null, null);
        var fallbackPreviewResource = FindAssetFallbackPreviewResource(asset, graph, packageResources);
        await AdvancePreviewLoadAsync(0.16, "Resolving scene root...");

        ResourceMetadata? sceneRoot = null;
        string unsupportedMessage;
        if (asset.AssetKind == AssetKind.BuildBuy)
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
        }
        else
        {
            var casGraph = graph.CasGraph;
            if (casGraph is null || !casGraph.IsSupported || casGraph.GeometryResource is null)
            {
                unsupportedMessage = "This CAS asset is outside the currently supported adult/young-adult human skinned subset.";
            }
            else
            {
                sceneRoot = casGraph.GeometryResource;
                unsupportedMessage = string.Empty;
            }
        }

        if (sceneRoot is null)
        {
            SetSceneLodOptions([]);
            await AdvancePreviewLoadAsync(4, "Building fallback preview...");
            var fallbackPreviewApplied = await TryApplyAssetFallbackPreviewAsync(
                fallbackPreviewResource,
                assetDetails,
                unsupportedMessage,
                graph.Diagnostics);
            if (!fallbackPreviewApplied)
            {
                PreviewText = string.Join(Environment.NewLine, graph.Diagnostics.DefaultIfEmpty(unsupportedMessage));
                DetailsText = assetDetails;
                PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
                PreviewSurfaceTitle = "Diagnostics";
                NotifyPreviewStateChanged();
            }

            CompletePreviewLoad();

            return;
        }

        selectedResource = sceneRoot;
        selectedAssetSceneRoot = sceneRoot;
        SetSceneLodOptions(BuildSceneLodOptions(graph, sceneRoot), sceneRoot.Key.FullTgi);

        await AdvancePreviewLoadAsync(0.22, "Preparing scene build...");
        var previewProgress = CreatePreviewBuildProgressReporter(0.22, 0.72);
        await AdvancePreviewLoadAsync(0.24, "Building 3D preview...");
        var preview = await previewService.CreatePreviewAsync(sceneRoot, CancellationToken.None, previewProgress);
        var assetDetailsWithScene = AssetDetailsFormatter.BuildAssetDetails(asset, graph, sceneRoot, preview.Content as ScenePreviewContent);
        if (preview.Content is ScenePreviewContent { Scene: null } failedScenePreview)
        {
            await AdvancePreviewLoadAsync(0.96, "Applying fallback preview...");
            var fallbackPreviewApplied = await TryApplyAssetFallbackPreviewAsync(
                fallbackPreviewResource,
                $"{assetDetailsWithScene}{Environment.NewLine}{Environment.NewLine}Fallback: scene reconstruction failed, showing asset image preview instead.",
                failedScenePreview.Diagnostics,
                graph.Diagnostics);
            if (fallbackPreviewApplied)
            {
                CompletePreviewLoad();
                return;
            }
        }

        await AdvancePreviewLoadAsync(0.96, "Applying preview...");
        await ApplyPreviewAsync(sceneRoot, preview, assetDetailsWithScene);
        CompletePreviewLoad();
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
        var assetDetails = AssetDetailsFormatter.BuildAssetDetails(selectedAsset, selectedAssetGraph, resource, preview.Content as ScenePreviewContent);
        await AdvancePreviewLoadAsync(0.96, "Applying preview...");
        await ApplyPreviewAsync(resource, preview, assetDetails);
        CompletePreviewLoad();
    }

    private async Task ReloadSourcesAsync()
    {
        var sources = await indexStore.GetDataSourcesAsync(CancellationToken.None);
        ReplaceCollection(DataSources, sources);
        UpdateBrowsePresentation();
    }

    private async Task ReloadAssetFacetOptionsAsync()
    {
        var assetKind = AssetDomain == AssetBrowserDomain.BuildBuy ? AssetKind.BuildBuy : AssetKind.Cas;
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
    }

    private void MarkAllQueriesDirty()
    {
        assetQueryDirty = true;
        rawQueryDirty = true;
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
            ResultSummary = "No sources added.";
            EmptyStateMessage = "No sources added. Add a Game, DLC, or Mods folder to begin.";
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

    private async Task<bool> TryApplyAssetFallbackPreviewAsync(
        ResourceMetadata? fallbackPreviewResource,
        string assetDetails,
        string primaryReason,
        IReadOnlyList<string> diagnostics)
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

        var fallbackDetails =
            $"""
            {assetDetails}

            Preview Fallback:
            {primaryReason}
            {string.Join(Environment.NewLine, diagnostics.Where(static diagnostic => !string.IsNullOrWhiteSpace(diagnostic)))}
            """;
        await ApplyPreviewAsync(fallbackPreviewResource, fallbackPreview, fallbackDetails);
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
            return [new SceneLodOption("Geometry root", graph.CasGraph.GeometryResource)];
        }

        return [new SceneLodOption(selectedSceneRoot.Key.TypeName, selectedSceneRoot)];
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
        _ = appPreferencesService.SaveAsync(new AppPreferences(selectedWorkerCount), CancellationToken.None);
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
