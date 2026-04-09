using System.Collections.ObjectModel;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using Sims4ResourceExplorer.App.Services;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Indexing;
using Windows.Storage.Streams;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IIndexStore indexStore;
    private readonly PackageIndexCoordinator packageIndexCoordinator;
    private readonly IPreviewService previewService;
    private readonly IRawExportService rawExportService;
    private readonly IAssetGraphBuilder assetGraphBuilder;
    private readonly IResourceMetadataEnrichmentService resourceMetadataEnrichmentService;
    private readonly IAudioPlayer audioPlayer;
    private readonly IndexingRunOptions defaultIndexingOptions;
    private readonly IAppPreferencesService appPreferencesService;

    private CancellationTokenSource? indexingCancellationTokenSource;
    private ResourceMetadata? selectedResource;
    private byte[]? previewAudioBytes;

    public MainViewModel(
        IIndexStore indexStore,
        PackageIndexCoordinator packageIndexCoordinator,
        IPreviewService previewService,
        IRawExportService rawExportService,
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
        this.assetGraphBuilder = assetGraphBuilder;
        this.resourceMetadataEnrichmentService = resourceMetadataEnrichmentService;
        this.audioPlayer = audioPlayer;
        this.defaultIndexingOptions = defaultIndexingOptions;
        this.appPreferencesService = appPreferencesService;

        AvailableWorkerCounts = Enumerable.Range(1, IndexingRunOptions.GetMachineWorkerLimit()).ToArray();
        selectedWorkerCount = defaultIndexingOptions.MaxPackageConcurrency;
    }

    public ObservableCollection<DataSourceDefinition> DataSources { get; } = [];
    public ObservableCollection<ResourceMetadata> Resources { get; } = [];
    public ObservableCollection<AssetSummary> Assets { get; } = [];
    public ObservableLog Log { get; } = [];
    public IReadOnlyList<int> AvailableWorkerCounts { get; }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool buildBuyOnly;

    [ObservableProperty]
    private bool casOnly;

    [ObservableProperty]
    private bool audioOnly;

    [ObservableProperty]
    private bool previewableOnly;

    [ObservableProperty]
    private bool exportCapableOnly;

    [ObservableProperty]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

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

    public async Task InitializeAsync()
    {
        await indexStore.InitializeAsync(CancellationToken.None);
        var preferences = await appPreferencesService.LoadAsync(CancellationToken.None);
        SelectedWorkerCount = IndexingRunOptions.ClampWorkerCount(preferences.IndexWorkerCount);
        await ReloadSourcesAsync();
        await RefreshResourcesAsync();
        await RefreshAssetsAsync();
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
            var progress = new Progress<IndexingProgress>(value =>
            {
                ApplyIndexingProgress(value, indexingDialogViewModel);
            });

            var snapshotSources = DataSources.ToArray();
            var selectedWorkerCount = SelectedWorkerCount;
            var indexingToken = indexingCancellationTokenSource.Token;
            await Task.Run(
                async () => await packageIndexCoordinator.RunAsync(snapshotSources, progress, indexingToken, selectedWorkerCount).ConfigureAwait(false),
                indexingToken);
            await RefreshResourcesAsync();
            await RefreshAssetsAsync();
            StatusMessage = $"Index complete. {Resources.Count} raw resources visible, {Assets.Count} logical assets visible.";
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

    public async Task RefreshResourcesAsync()
    {
        var results = await indexStore.QueryResourcesAsync(
            new ResourceQuery(
                SearchText,
                BrowserMode.RawResources,
                BuildBuyOnly,
                CasOnly,
                AudioOnly,
                PreviewableOnly,
                ExportCapableOnly,
                Limit: 1000),
            CancellationToken.None);

        ReplaceCollection(Resources, results);
        StatusMessage = $"Loaded {Resources.Count} raw resources from the index.";
    }

    public async Task RefreshAssetsAsync()
    {
        var results = await indexStore.QueryAssetsAsync(
            new LogicalAssetQuery(SearchText, BuildBuyOnly, CasOnly, Limit: 1000),
            CancellationToken.None);

        ReplaceCollection(Assets, results);
    }

    public async Task SelectResourceAsync(ResourceMetadata? resource)
    {
        selectedResource = resource;
        previewAudioBytes = null;
        PreviewImageSource = null;

        if (resource is null)
        {
            PreviewText = "Select a resource or asset to inspect its preview.";
            DetailsText = "Metadata will appear here.";
            return;
        }

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
        previewAudioBytes = null;
        PreviewImageSource = null;

        if (asset is null)
        {
            PreviewText = "Select a resource or asset to inspect its preview.";
            DetailsText = "Metadata will appear here.";
            return;
        }

        var packageResources = await indexStore.GetPackageResourcesAsync(asset.PackagePath, CancellationToken.None);
        var graph = assetGraphBuilder.BuildAssetGraph(asset, packageResources);
        var root = packageResources.FirstOrDefault(resource => resource.Key.FullTgi == asset.RootKey.FullTgi);
        var assetDetails = BuildAssetDetails(asset, graph);

        if (root is null)
        {
            selectedResource = null;
            PreviewText = string.Join(Environment.NewLine, graph.Diagnostics.DefaultIfEmpty("Asset root resource could not be located."));
            DetailsText = assetDetails;
            return;
        }

        selectedResource = root;
        root = await resourceMetadataEnrichmentService.EnrichAsync(root, CancellationToken.None);
        selectedResource = root;
        var preview = await previewService.CreatePreviewAsync(root, CancellationToken.None);
        await ApplyPreviewAsync(root, preview, assetDetails);
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
    }

    private async Task ApplyPreviewAsync(ResourceMetadata resource, PreviewResult preview, string? leadingDetails)
    {
        DetailsText = string.IsNullOrWhiteSpace(leadingDetails)
            ? BuildResourceDetails(resource)
            : $"{leadingDetails}{Environment.NewLine}{Environment.NewLine}{BuildResourceDetails(resource)}";

        switch (preview.Content)
        {
            case TextPreviewContent text:
                PreviewText = text.Text;
                break;

            case HexPreviewContent hex:
                PreviewText = hex.HexDump;
                break;

            case TexturePreviewContent texture:
                PreviewText = string.IsNullOrWhiteSpace(texture.Diagnostics)
                    ? "Decoded texture preview."
                    : texture.Diagnostics;
                PreviewImageSource = await CreateBitmapAsync(texture.PngBytes);
                break;

            case AudioPreviewContent audio:
                previewAudioBytes = audio.WavBytes;
                PreviewText = audio.Diagnostics;
                break;

            case ScenePreviewContent scene:
                PreviewText = scene.Diagnostics;
                break;

            case UnsupportedPreviewContent unsupported:
                PreviewText = unsupported.Reason;
                break;

            default:
                PreviewText = "No preview available.";
                break;
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

    private static string BuildAssetDetails(AssetSummary asset, AssetGraph graph) =>
        $"""
        Asset: {asset.DisplayName}
        Kind: {asset.AssetKind}
        Category: {asset.Category ?? "(unknown)"}
        Package: {asset.PackagePath}
        Root TGI: {asset.RootKey.FullTgi}
        Linked Resources: {asset.LinkedResourceCount}
        Thumbnail: {asset.ThumbnailTgi ?? "(none)"}
        Diagnostics:
        {string.Join(Environment.NewLine, graph.Diagnostics.DefaultIfEmpty(asset.Diagnostics))}
        """;

    private void AppendLog(string message) => Log.Append(message);

    partial void OnSelectedWorkerCountChanged(int value)
    {
        selectedWorkerCount = IndexingRunOptions.ClampWorkerCount(value);
        OnPropertyChanged(nameof(SelectedWorkerCount));
        _ = appPreferencesService.SaveAsync(new AppPreferences(selectedWorkerCount), CancellationToken.None);
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

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsNotBusy));

    private static void ReplaceCollection<T>(ObservableCollection<T> collection, IEnumerable<T> values)
    {
        collection.Clear();
        foreach (var value in values)
        {
            collection.Add(value);
        }
    }
}
