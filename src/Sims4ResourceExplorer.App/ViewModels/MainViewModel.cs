using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
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
    private readonly AssetBrowserState assetBrowserState = new();
    private readonly RawResourceBrowserState rawResourceBrowserState = new();

    private CancellationTokenSource? indexingCancellationTokenSource;
    private CancellationTokenSource? assetSearchDebounceTokenSource;
    private CancellationTokenSource? rawSearchDebounceTokenSource;
    private ResourceMetadata? selectedResource;
    private AssetSummary? selectedAsset;
    private AssetGraph? selectedAssetGraph;
    private ResourceMetadata? selectedAssetSceneRoot;
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
        IAppPreferencesService appPreferencesService)
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
    public ObservableLog Log { get; } = [];
    public IReadOnlyList<int> AvailableWorkerCounts { get; }
    public IReadOnlyList<BrowserMode> BrowserModes { get; }
    public IReadOnlyList<AssetBrowserDomain> AssetDomains { get; }
    public IReadOnlyList<AssetBrowserSort> AssetSortOptions { get; }
    public IReadOnlyList<RawResourceDomain> RawDomains { get; }
    public IReadOnlyList<RawResourceSort> RawSortOptions { get; }
    public IReadOnlyList<ResourceLinkFilter> LinkFilters { get; }

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
    private string assetPackageText = string.Empty;

    [ObservableProperty]
    private bool assetHasThumbnailOnly;

    [ObservableProperty]
    private bool assetVariantsOnly;

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
    private string resultSummary = "Choose a scope to begin.";

    [ObservableProperty]
    private string emptyStateMessage = "Choose a scope to begin.";

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
    public string AssetSearchPlaceholder => "Search asset name, category, or package";
    public string RawSearchPlaceholder => "Search package path, type name, TGI, group, or instance";
    public string AssetSearchHelp => "Examples: chair, comfort, objects.package";
    public string RawSearchHelp => "Examples: objectcatalog, 00B2D882, 00000000, package fragment";

    public async Task InitializeAsync()
    {
        await indexStore.InitializeAsync(CancellationToken.None);
        var preferences = await appPreferencesService.LoadAsync(CancellationToken.None);
        SelectedWorkerCount = IndexingRunOptions.ClampWorkerCount(preferences.IndexWorkerCount);
        await ReloadSourcesAsync();
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

        try
        {
            await Task.Yield();
            await appPreferencesService.SaveAsync(new AppPreferences(SelectedWorkerCount), CancellationToken.None);
            var progress = new Progress<IndexingProgress>(value => ApplyIndexingProgress(value, indexingDialogViewModel));
            var snapshotSources = DataSources.ToArray();
            var selectedWorkerCount = SelectedWorkerCount;
            var indexingToken = indexingCancellationTokenSource.Token;

            await Task.Run(
                async () => await packageIndexCoordinator.RunAsync(snapshotSources, progress, indexingToken, selectedWorkerCount).ConfigureAwait(false),
                indexingToken);

            MarkAllQueriesDirty();
            await RefreshActiveBrowserAsync(resetWindow: true);
            StatusMessage = "Index complete.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Indexing canceled.";
            AppendLog("Indexing canceled by user.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Indexing failed: {ex.Message}";
            AppendLog($"Indexing failed: {ex}");
        }
        finally
        {
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
        previewAudioBytes = null;
        PreviewImageSource = null;
        CurrentScene = null;

        try
        {
            resource = await resourceMetadataEnrichmentService.EnrichAsync(resource, CancellationToken.None);
            selectedResource = resource;
            var preview = await previewService.CreatePreviewAsync(resource, CancellationToken.None);
            await ApplyPreviewAsync(resource, preview, null);
        }
        catch (Exception ex)
        {
            PreviewText = ex.ToString();
            DetailsText = BuildResourceDetails(resource);
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
        previewAudioBytes = null;
        PreviewImageSource = null;
        CurrentScene = null;

        var packageResources = await indexStore.GetPackageResourcesAsync(asset.PackagePath, CancellationToken.None);
        var graph = assetGraphBuilder.BuildAssetGraph(asset, packageResources);
        selectedAssetGraph = graph;
        var assetDetails = BuildAssetDetails(asset, graph, null, null);

        if (asset.AssetKind != AssetKind.BuildBuy)
        {
            PreviewText = string.Join(Environment.NewLine, graph.Diagnostics.DefaultIfEmpty("Logical asset preview is only implemented for the Build/Buy static-object subset in the current build."));
            DetailsText = assetDetails;
            return;
        }

        var buildBuyGraph = graph.BuildBuyGraph;
        if (buildBuyGraph is null || !buildBuyGraph.IsSupported)
        {
            PreviewText = string.Join(Environment.NewLine, graph.Diagnostics.DefaultIfEmpty("This Build/Buy asset is outside the currently supported static-object subset."));
            DetailsText = assetDetails;
            return;
        }

        var sceneRoot = await resourceMetadataEnrichmentService.EnrichAsync(buildBuyGraph.ModelResource, CancellationToken.None);
        selectedResource = sceneRoot;
        selectedAssetSceneRoot = sceneRoot;

        var preview = await previewService.CreatePreviewAsync(sceneRoot, CancellationToken.None);
        await ApplyPreviewAsync(sceneRoot, preview, BuildAssetDetails(asset, graph, sceneRoot, preview.Content as ScenePreviewContent));
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
        if (selectedAsset is null || selectedAssetGraph?.BuildBuyGraph is null)
        {
            StatusMessage = "Select a supported Build/Buy asset first.";
            return;
        }

        if (selectedAssetSceneRoot is null)
        {
            StatusMessage = "The selected Build/Buy asset does not have a resolved scene root.";
            return;
        }

        var preview = await previewService.CreatePreviewAsync(selectedAssetSceneRoot, CancellationToken.None);
        if (preview.Content is not ScenePreviewContent scenePreview || scenePreview.Scene is null)
        {
            StatusMessage = "The selected Build/Buy asset could not be reconstructed into an exportable scene.";
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

    private async Task ReloadSourcesAsync()
    {
        var sources = await indexStore.GetDataSourcesAsync(CancellationToken.None);
        ReplaceCollection(DataSources, sources);
        UpdateBrowsePresentation();
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
        DetailsText = string.IsNullOrWhiteSpace(leadingDetails)
            ? BuildResourceDetails(resource)
            : $"{leadingDetails}{Environment.NewLine}{Environment.NewLine}{BuildResourceDetails(resource)}";

        switch (preview.Content)
        {
            case TextPreviewContent text:
                CurrentScene = null;
                PreviewText = text.Text;
                break;

            case HexPreviewContent hex:
                CurrentScene = null;
                PreviewText = hex.HexDump;
                break;

            case TexturePreviewContent texture:
                CurrentScene = null;
                PreviewText = string.IsNullOrWhiteSpace(texture.Diagnostics) ? "Decoded texture preview." : texture.Diagnostics;
                PreviewImageSource = await CreateBitmapAsync(texture.PngBytes);
                break;

            case AudioPreviewContent audio:
                CurrentScene = null;
                previewAudioBytes = audio.WavBytes;
                PreviewText = audio.Diagnostics;
                break;

            case ScenePreviewContent scene:
                CurrentScene = scene.Scene;
                PreviewText = scene.Diagnostics;
                break;

            case UnsupportedPreviewContent unsupported:
                CurrentScene = null;
                PreviewText = unsupported.Reason;
                break;

            default:
                CurrentScene = null;
                PreviewText = "No preview available.";
                break;
        }
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
        assetBrowserState.PackageText = AssetPackageText;
        assetBrowserState.HasThumbnailOnly = AssetHasThumbnailOnly;
        assetBrowserState.VariantsOnly = AssetVariantsOnly;
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
        AssetPackageText = assetBrowserState.PackageText;
        AssetHasThumbnailOnly = assetBrowserState.HasThumbnailOnly;
        AssetVariantsOnly = assetBrowserState.VariantsOnly;
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

    private static string BuildAssetDetails(AssetSummary asset, AssetGraph graph, ResourceMetadata? sceneRoot, ScenePreviewContent? scenePreview)
    {
        var buildBuyGraph = graph.BuildBuyGraph;
        var scene = scenePreview?.Scene;

        return
            $"""
            Asset: {asset.DisplayName}
            Kind: {asset.AssetKind}
            Category: {asset.Category ?? "(unknown)"}
            Package: {asset.PackagePath}
            Root TGI: {asset.RootKey.FullTgi}
            Identity Resources: {buildBuyGraph?.IdentityResources.Count ?? 0}
            Linked Resources: {asset.LinkedResourceCount}
            Thumbnail: {asset.ThumbnailTgi ?? "(none)"}
            Supported Subset: {buildBuyGraph?.SupportedSubset ?? "(not supported)"}
            Scene Root: {sceneRoot?.Key.FullTgi ?? "(unresolved)"}
            Selected LOD: {ExtractDiagnosticValue(scenePreview?.Diagnostics, "Selected LOD root:") ?? "(not resolved)"}
            Model LOD Candidates: {buildBuyGraph?.ModelLodResources.Count ?? 0}
            Material Candidates: {buildBuyGraph?.MaterialResources.Count ?? 0}
            Texture Candidates: {buildBuyGraph?.TextureResources.Count ?? 0}
            Mesh Count: {scene?.Meshes.Count ?? 0}
            Vertex Count: {(scene?.Meshes.Sum(static mesh => mesh.Positions.Count / 3) ?? 0):N0}
            Index Count: {(scene?.Meshes.Sum(static mesh => mesh.Indices.Count) ?? 0):N0}
            Material Slots: {scene?.Materials.Count ?? 0}
            Texture References: {scene?.Materials.Sum(static material => material.Textures.Count) ?? 0}
            Bounds: {FormatBounds(scene?.Bounds)}
            Diagnostics:
            {string.Join(Environment.NewLine, graph.Diagnostics.Concat(scenePreview is null ? [] : scenePreview.Diagnostics.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)).DefaultIfEmpty(asset.Diagnostics))}
            """;
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

    partial void OnSelectedWorkerCountChanged(int value)
    {
        selectedWorkerCount = IndexingRunOptions.ClampWorkerCount(value);
        OnPropertyChanged(nameof(SelectedWorkerCount));
        _ = appPreferencesService.SaveAsync(new AppPreferences(selectedWorkerCount), CancellationToken.None);
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

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
        UpdateBrowsePresentation();
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

    private void ApplyIndexingProgress(IndexingProgress progress, IndexingDialogViewModel indexingDialogViewModel)
    {
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

    private static string FormatBounds(Bounds3D? bounds) =>
        bounds is null
            ? "(scene unavailable)"
            : $"{bounds.MinX:0.###}, {bounds.MinY:0.###}, {bounds.MinZ:0.###} -> {bounds.MaxX:0.###}, {bounds.MaxY:0.###}, {bounds.MaxZ:0.###}";

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
                material.Approximation,
                material.Textures.Select(static texture => new MaterialTextureEntry(
                    texture.Slot,
                    texture.FileName,
                    texture.SourceKey,
                    texture.SourcePackagePath)).ToArray()))
            .ToArray();

    private static string Slugify(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var normalized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : char.ToLowerInvariant(ch)).ToArray());
        var collapsed = string.Join("_", normalized.Split([' ', '\t', '\r', '\n', '-', '/'], StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(collapsed) ? "buildbuy_asset" : collapsed;
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
}
