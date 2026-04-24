using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.InteropServices.WindowsRuntime;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.WinUI.SharpDX;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Windowing;
using Sims4ResourceExplorer.App.ViewModels;
using Sims4ResourceExplorer.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Sims4ResourceExplorer.App;

public sealed partial class MainWindow : Window
{
    private const string AppTitleBase = "Sims4 Resource Explorer";
    private readonly Viewport3DX sceneViewport;
    private readonly PerspectiveCamera sceneCamera;
    private readonly DefaultEffectsManager effectsManager = new();
    private readonly ShadowMap3D sceneShadowMap = new()
    {
        Resolution = new Windows.Foundation.Size(4096, 4096),
        Bias = 0.0008,
        Intensity = 0.3,
        Distance = 240,
        OrthoWidth = 240,
        NearFieldDistance = 0.01,
        FarFieldDistance = 480,
        AutoCoverCompleteScene = true,
        IsSceneDynamic = true
    };
    private CancellationTokenSource? uvPreviewRenderCancellation;
    private long previewSurfaceGeneration;
    private bool isDraggingMainSplitter;
    private bool isAdjustingMainContentColumns;
    private double mainSplitterStartX;
    private double mainSplitterStartResultsWidth;
    private double mainSplitterTotalResizableWidth;
    private bool isDraggingPreviewSplitter;
    private double previewSplitterStartY;
    private double previewSplitterStartHeight;
    private IndexingDialog? indexingDialog;
    private int shutdownRequested;

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Title = BuildWindowTitle();
        if (Content is FrameworkElement root)
        {
            root.DataContext = viewModel;
        }
        sceneCamera = new PerspectiveCamera
        {
            Position = new Vector3(0, 0, 8),
            LookDirection = new Vector3(0, 0, -8),
            UpDirection = Vector3.UnitY,
            NearPlaneDistance = 0.01,
            FarPlaneDistance = 10000
        };
        sceneViewport = new Viewport3DX
        {
            Camera = sceneCamera,
            EffectsManager = effectsManager,
            ShowCoordinateSystem = true,
            ShowViewCube = true,
            IsShadowMappingEnabled = true,
            Visibility = Visibility.Collapsed
        };
        PreviewSurface.Children.Insert(0, sceneViewport);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdateBusyUiState();
        Activated += MainWindow_Activated;
        Closed += MainWindow_Closed;
        TryApplyWindowIcon();
    }

    public MainViewModel ViewModel { get; }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        await ViewModel.InitializeAsync();
    }

    private async void Index_Click(object sender, RoutedEventArgs e)
    {
        if (indexingDialog is not null)
        {
            indexingDialog.Activate();
            return;
        }

        var dialog = new IndexingDialog(ViewModel.CreateIndexingDialogViewModel());
        indexingDialog = dialog;
        dialog.Closed += IndexingDialog_Closed;
        dialog.Activate();
        var shouldStart = await dialog.ViewModel.WaitForStartAsync();
        if (!shouldStart)
        {
            await dialog.WaitForCloseAsync();
            return;
        }

        await ViewModel.ApplyIndexingConfigurationAsync(
            dialog.ViewModel.GetConfiguredSources(),
            dialog.ViewModel.SelectedWorkerCount,
            dialog.ViewModel.SelectedMemoryUsagePercent);
        await ViewModel.RunIndexAsync(dialog.ViewModel);
        await dialog.WaitForCloseAsync();
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshActiveBrowserAsync();
    private async void LoadMore_Click(object sender, RoutedEventArgs e) => await ViewModel.LoadMoreAsync();
    private async void ResetFilters_Click(object sender, RoutedEventArgs e) => await ViewModel.ResetActiveFiltersAsync();
    private async void RemoveFilterChip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string key)
        {
            await ViewModel.RemoveActiveFilterAsync(key);
        }
    }

    private async void ResourcesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResourcesListView.SelectedItem is ResourceMetadata resource)
        {
            await ViewModel.SelectResourceAsync(resource);
        }
    }

    private async void AssetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AssetsListView.SelectedItem is AssetSummary asset)
        {
            await ViewModel.SelectAssetAsync(asset);
        }
    }

    private async void ExportRaw_Click(object sender, RoutedEventArgs e)
    {
        var output = await PickFolderPathAsync();
        if (output is not null)
        {
            await ViewModel.ExportSelectedRawAsync(output);
        }
    }

    private async void ExportAsset_Click(object sender, RoutedEventArgs e)
    {
        var output = await PickFolderPathAsync();
        if (output is not null)
        {
            await ViewModel.ExportSelectedAssetAsync(output);
        }
    }

    private async void PlayAudio_Click(object sender, RoutedEventArgs e) => await ViewModel.PlayAudioAsync();
    private async void StopAudio_Click(object sender, RoutedEventArgs e) => await ViewModel.StopAudioAsync();
    private void ResetView_Click(object sender, RoutedEventArgs e) => ResetSceneCamera(ViewModel.CurrentScene);
    private void CopyDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var text = ViewModel.SelectedPreviewDiagnosticsTabIndex == 0
            ? ViewModel.PreviewText
            : ViewModel.DetailsText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
        ViewModel.StatusMessage = "Copied current diagnostics tab to clipboard.";
    }

    private async void CopyPreviewImage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var streamReference = await CapturePreviewSurfaceAsync();
            if (streamReference is null)
            {
                ViewModel.StatusMessage = "Preview image is not ready to copy yet.";
                return;
            }

            var package = new DataPackage();
            package.SetBitmap(streamReference);
            Clipboard.SetContent(package);
            ViewModel.StatusMessage = "Copied current preview image to clipboard.";
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"Failed to copy preview image: {ex.Message}";
        }
    }
    private void AssetFacetSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        UpdateAssetFacetSuggestions(sender, userInputOnly: args.Reason == AutoSuggestionBoxTextChangeReason.UserInput);
    }

    private void AssetFacetSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string selected && string.Equals(selected, "All", StringComparison.Ordinal))
        {
            sender.Text = string.Empty;
        }
    }

    private void AssetFacetSuggestBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is AutoSuggestBox suggestBox)
        {
            UpdateAssetFacetSuggestions(suggestBox, userInputOnly: false);
            suggestBox.IsSuggestionListOpen = true;
        }
    }

    private void UpdateAssetFacetSuggestions(AutoSuggestBox sender, bool userInputOnly)
    {
        var source = sender.Tag switch
        {
            "Category" => ViewModel.AssetCategories,
            "RootType" => ViewModel.AssetRootTypes,
            "IdentityType" => ViewModel.AssetIdentityTypes,
            "GeometryType" => ViewModel.AssetPrimaryGeometryTypes,
            "ThumbnailType" => ViewModel.AssetThumbnailTypes,
            "CatalogSignal0020" => ViewModel.AssetCatalogSignal0020Values,
            "CatalogSignal002C" => ViewModel.AssetCatalogSignal002CValues,
            "CatalogSignal0030" => ViewModel.AssetCatalogSignal0030Values,
            "CatalogSignal0034" => ViewModel.AssetCatalogSignal0034Values,
            _ => Enumerable.Empty<string>()
        };

        var text = sender.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(text)
            ? source
            : source.Where(value => value.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();

        sender.ItemsSource = filtered;
        sender.IsSuggestionListOpen = filtered.Any() && (!userInputOnly || !string.Equals(text, "All", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string?> PickFolderPathAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private void MainContentGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (isAdjustingMainContentColumns ||
            (ResultsColumn.Width.GridUnitType != GridUnitType.Pixel &&
             PreviewColumn.Width.GridUnitType != GridUnitType.Pixel))
        {
            return;
        }

        try
        {
            isAdjustingMainContentColumns = true;
            var resultsMinWidth = ResultsColumn.MinWidth > 0 ? ResultsColumn.MinWidth : 360;
            var previewMinWidth = PreviewColumn.MinWidth > 0 ? PreviewColumn.MinWidth : 460;
            var splitterWidth = MainContentGrid.ColumnDefinitions.Count > 2
                ? MainContentGrid.ColumnDefinitions[2].ActualWidth
                : 0d;
            var availableWidth = Math.Max(resultsMinWidth + previewMinWidth, MainContentGrid.ActualWidth - splitterWidth);
            var targetResizableWidth = Math.Max(resultsMinWidth + previewMinWidth, availableWidth);
            var currentResultsWidth = ResultsColumn.ActualWidth;
            var currentPreviewWidth = PreviewColumn.ActualWidth;
            var currentTotalWidth = currentResultsWidth + currentPreviewWidth;
            if (currentTotalWidth <= 0)
            {
                return;
            }

            var resultsRatio = currentResultsWidth / currentTotalWidth;
            var newResultsWidth = Math.Clamp(targetResizableWidth * resultsRatio, resultsMinWidth, targetResizableWidth - previewMinWidth);
            var newPreviewWidth = Math.Max(previewMinWidth, targetResizableWidth - newResultsWidth);
            if (Math.Abs(newResultsWidth - currentResultsWidth) < 0.5 &&
                Math.Abs(newPreviewWidth - currentPreviewWidth) < 0.5)
            {
                return;
            }

            ResultsColumn.Width = new GridLength(newResultsWidth, GridUnitType.Pixel);
            PreviewColumn.Width = new GridLength(newPreviewWidth, GridUnitType.Pixel);
        }
        catch (Exception ex)
        {
            ShowPreviewFailureDiagnostics("Preview layout update failed during window resize.", ex);
        }
        finally
        {
            isAdjustingMainContentColumns = false;
        }
    }

    private void TryApplyWindowIcon()
    {
        try
        {
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(WindowNative.GetWindowHandle(this));
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
        }
    }

    private static string BuildWindowTitle()
    {
        var assembly = typeof(MainWindow).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return AppTitleBase;
        }

        return $"{AppTitleBase} ({informationalVersion})";
    }

    private async Task<RandomAccessStreamReference?> CapturePreviewSurfaceAsync()
    {
        if (PreviewSurface.ActualWidth <= 1 || PreviewSurface.ActualHeight <= 1)
        {
            return null;
        }

        var renderBitmap = new RenderTargetBitmap();
        await renderBitmap.RenderAsync(PreviewSurface);
        var pixels = await renderBitmap.GetPixelsAsync();
        if (renderBitmap.PixelWidth <= 0 || renderBitmap.PixelHeight <= 0 || pixels.Length == 0)
        {
            return null;
        }

        var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)renderBitmap.PixelWidth,
            (uint)renderBitmap.PixelHeight,
            96,
            96,
            pixels.ToArray());
        await encoder.FlushAsync();
        stream.Seek(0);
        return RandomAccessStreamReference.CreateFromStream(stream);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentScene) ||
            e.PropertyName == nameof(MainViewModel.PreviewImageSource) ||
            e.PropertyName == nameof(MainViewModel.PreviewSurfaceMode) ||
            e.PropertyName == nameof(MainViewModel.SelectedSceneRenderMode) ||
            e.PropertyName == nameof(MainViewModel.SelectedSceneTextureSlot))
        {
            UpdatePreviewSurface();
        }

        if (e.PropertyName == nameof(MainViewModel.IsBusy))
        {
            UpdateBusyUiState();
        }
    }

    private void UpdateBusyUiState()
    {
        if (Content is UIElement root)
        {
            root.IsHitTestVisible = !ViewModel.IsBusy;
        }
    }

    private void IndexingDialog_Closed(object sender, WindowEventArgs args)
    {
        if (sender is not IndexingDialog dialog)
        {
            return;
        }

        dialog.Closed -= IndexingDialog_Closed;
        if (ReferenceEquals(indexingDialog, dialog))
        {
            indexingDialog = null;
        }
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (Interlocked.Exchange(ref shutdownRequested, 1) != 0)
        {
            return;
        }

        Activated -= MainWindow_Activated;
        Closed -= MainWindow_Closed;
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;

        if (indexingDialog is not null)
        {
            try
            {
                indexingDialog.Closed -= IndexingDialog_Closed;
                indexingDialog.PrepareForShutdown();
            }
            catch
            {
            }
            finally
            {
                indexingDialog = null;
            }
        }

        try
        {
            uvPreviewRenderCancellation?.Cancel();
        }
        catch
        {
        }

        try
        {
            uvPreviewRenderCancellation?.Dispose();
        }
        catch
        {
        }
        finally
        {
            uvPreviewRenderCancellation = null;
        }

        try
        {
            sceneViewport.Items.Clear();
            PreviewSurface.Children.Remove(sceneViewport);
        }
        catch
        {
        }

        DisposeSilently(sceneViewport);
        DisposeSilently(sceneShadowMap);
        DisposeSilently(effectsManager);
    }

    private async void UpdatePreviewSurface()
    {
        try
        {
            var generation = Interlocked.Increment(ref previewSurfaceGeneration);
            uvPreviewRenderCancellation?.Cancel();
            uvPreviewRenderCancellation = null;

            if (ViewModel.IsScenePreviewActive && ViewModel.CurrentScene is not null)
            {
                if (ViewModel.SelectedSceneRenderMode is SceneRenderMode.RawUv or SceneRenderMode.MaterialUv)
                {
                    sceneViewport.Items.Clear();
                    sceneViewport.Visibility = Visibility.Collapsed;
                    PreviewImage.Visibility = Visibility.Collapsed;
                    UvPreviewImage.Visibility = Visibility.Visible;
                    UvPreviewImage.Source = null;
                    var cancellation = new CancellationTokenSource();
                    uvPreviewRenderCancellation = cancellation;
                    try
                    {
                        var uvPreview = await GenerateUvPreviewAsync(ViewModel.CurrentScene, cancellation.Token);
                        if (!cancellation.IsCancellationRequested &&
                            generation == Interlocked.Read(ref previewSurfaceGeneration) &&
                            ViewModel.IsScenePreviewActive &&
                            ViewModel.CurrentScene is not null &&
                            ViewModel.SelectedSceneRenderMode is SceneRenderMode.RawUv or SceneRenderMode.MaterialUv)
                        {
                            UvPreviewImage.Source = uvPreview;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    return;
                }

                RenderScene(ViewModel.CurrentScene);
                sceneViewport.Visibility = Visibility.Visible;
                PreviewImage.Visibility = Visibility.Collapsed;
                UvPreviewImage.Visibility = Visibility.Collapsed;
                UvPreviewImage.Source = null;
                return;
            }

            sceneViewport.Items.Clear();
            sceneViewport.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = ViewModel.IsImagePreviewActive ? Visibility.Visible : Visibility.Collapsed;
            UvPreviewImage.Visibility = Visibility.Collapsed;
            UvPreviewImage.Source = null;
        }
        catch (Exception ex)
        {
            ShowPreviewFailureDiagnostics("Preview rendering failed.", ex);
        }
    }

    private void RenderScene(CanonicalScene scene)
    {
        sceneViewport.Items.Clear();
        var renderMode = ViewModel.SelectedSceneRenderMode;
        var sceneCenter = new Vector3(
            (scene.Bounds.MinX + scene.Bounds.MaxX) * 0.5f,
            (scene.Bounds.MinY + scene.Bounds.MaxY) * 0.5f,
            (scene.Bounds.MinZ + scene.Bounds.MaxZ) * 0.5f);
        var sceneSize = Math.Max(
            Math.Max(scene.Bounds.MaxX - scene.Bounds.MinX, scene.Bounds.MaxY - scene.Bounds.MinY),
            scene.Bounds.MaxZ - scene.Bounds.MinZ);
        if (sceneSize <= 0f)
        {
            sceneSize = 1f;
        }

        switch (renderMode)
        {
            case SceneRenderMode.Wireframe:
                sceneViewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.White });
                break;
            case SceneRenderMode.RawUv:
            case SceneRenderMode.MaterialUv:
                sceneViewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.White });
                break;
            case SceneRenderMode.FlatTexture:
                sceneViewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.Black });
                break;
            default:
                sceneViewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.ColorHelper.FromArgb(255, 156, 166, 180) });
                sceneViewport.Items.Add(new DirectionalLight3D
                {
                    Direction = Vector3.Normalize(new Vector3(-0.36f, -0.92f, -0.22f)),
                    Color = Microsoft.UI.ColorHelper.FromArgb(255, 255, 248, 238)
                });
                sceneViewport.Items.Add(new DirectionalLight3D
                {
                    Direction = Vector3.Normalize(new Vector3(0.78f, -0.28f, 0.42f)),
                    Color = Microsoft.UI.ColorHelper.FromArgb(255, 222, 230, 238)
                });
                sceneViewport.Items.Add(new DirectionalLight3D
                {
                    Direction = Vector3.Normalize(new Vector3(0.12f, 0.58f, -0.9f)),
                    Color = Microsoft.UI.ColorHelper.FromArgb(255, 150, 158, 170)
                });
                sceneViewport.Items.Add(sceneShadowMap);
                break;
        }

        var selectedSlot = GetSelectedSceneTextureSlot();
        var renderEntries = BuildViewportRenderPassPlans(scene, renderMode, selectedSlot);
        var geometryCache = new Dictionary<int, MeshGeometry3D>();
        foreach (var entry in renderEntries)
        {
            if (!geometryCache.TryGetValue(entry.OriginalIndex, out var geometry))
            {
                geometry = CreateGeometry(entry.Mesh, scene, entry.Mesh.MaterialIndex, renderMode, selectedSlot);
                if (geometry.Positions is null || geometry.Positions.Count == 0 || geometry.TriangleIndices is null || geometry.TriangleIndices.Count == 0)
                {
                    continue;
                }

                geometryCache[entry.OriginalIndex] = geometry;
            }

            var material = CreateMaterial(
                scene,
                entry.Mesh.MaterialIndex,
                renderMode,
                selectedSlot,
                entry.PassVariant,
                entry.LatePassSceneRelationProfile);
            if (material is null)
            {
                continue;
            }

            sceneViewport.Items.Add(new MeshGeometryModel3D
            {
                Geometry = geometry,
                Material = material,
                IsTransparent = entry.IsTransparent,
                CullMode = SharpDX.Direct3D11.CullMode.None,
                RenderWireframe = entry.RenderWireframe,
                WireframeColor = Microsoft.UI.Colors.Yellow
            });
        }

        ResetSceneCamera(scene);
    }

    private string? GetSelectedSceneTextureSlot() =>
        string.Equals(ViewModel.SelectedSceneTextureSlot, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : ViewModel.SelectedSceneTextureSlot;

    private static void DisposeSilently(IDisposable? disposable)
    {
        if (disposable is null)
        {
            return;
        }

        try
        {
            disposable.Dispose();
        }
        catch
        {
        }
    }

}
