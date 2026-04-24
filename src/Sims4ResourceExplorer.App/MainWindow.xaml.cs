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

        for (var meshIndex = 0; meshIndex < scene.Meshes.Count; meshIndex++)
        {
            var mesh = scene.Meshes[meshIndex];
            var selectedSlot = GetSelectedSceneTextureSlot();
            var material = CreateMaterial(scene, mesh.MaterialIndex, renderMode, selectedSlot);
            var geometry = CreateGeometry(mesh, scene, mesh.MaterialIndex, renderMode, selectedSlot);
            if (geometry.Positions is null || geometry.Positions.Count == 0 || geometry.TriangleIndices is null || geometry.TriangleIndices.Count == 0)
            {
                continue;
            }

            sceneViewport.Items.Add(new MeshGeometryModel3D
            {
                Geometry = geometry,
                Material = material,
                IsTransparent = IsTransparentMaterial(scene, mesh.MaterialIndex, selectedSlot),
                CullMode = SharpDX.Direct3D11.CullMode.None,
                RenderWireframe = renderMode == SceneRenderMode.Wireframe,
                WireframeColor = Microsoft.UI.Colors.Yellow
            });
        }

        ResetSceneCamera(scene);
    }

    private void ShowPreviewFailureDiagnostics(string message, Exception ex)
    {
        uvPreviewRenderCancellation?.Cancel();
        uvPreviewRenderCancellation = null;
        sceneViewport.Items.Clear();
        sceneViewport.Visibility = Visibility.Collapsed;
        PreviewImage.Visibility = Visibility.Collapsed;
        UvPreviewImage.Visibility = Visibility.Collapsed;
        UvPreviewImage.Source = null;

        ViewModel.CurrentScene = null;
        ViewModel.PreviewImageSource = null;
        ViewModel.PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
        ViewModel.PreviewSurfaceTitle = "Diagnostics";
        ViewModel.SelectedPreviewDiagnosticsTabIndex = 0;
        ViewModel.PreviewText = $"{message}{Environment.NewLine}{Environment.NewLine}{ex}";
        ViewModel.StatusMessage = message;
    }

    private void ResetSceneCamera(CanonicalScene? scene)
    {
        if (scene is null)
        {
            return;
        }

        var center = new Vector3(
            (scene.Bounds.MinX + scene.Bounds.MaxX) * 0.5f,
            (scene.Bounds.MinY + scene.Bounds.MaxY) * 0.5f,
            (scene.Bounds.MinZ + scene.Bounds.MaxZ) * 0.5f);
        var size = Math.Max(
            Math.Max(scene.Bounds.MaxX - scene.Bounds.MinX, scene.Bounds.MaxY - scene.Bounds.MinY),
            scene.Bounds.MaxZ - scene.Bounds.MinZ);
        if (size <= 0)
        {
            size = 1f;
        }

        sceneCamera.Position = center + new Vector3(size * 1.5f, size * 0.75f, size * 1.5f);
        sceneCamera.LookDirection = center - sceneCamera.Position;
        sceneCamera.UpDirection = Vector3.UnitY;
    }

    private string? GetSelectedSceneTextureSlot() =>
        string.Equals(ViewModel.SelectedSceneTextureSlot, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : ViewModel.SelectedSceneTextureSlot;

    private static MeshGeometry3D CreateGeometry(CanonicalMesh mesh, CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot)
    {
        var geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(),
            TriangleIndices = new IntCollection()
        };

        for (var index = 0; index + 2 < mesh.Positions.Count; index += 3)
        {
            geometry.Positions.Add(new Vector3(mesh.Positions[index], mesh.Positions[index + 1], mesh.Positions[index + 2]));
        }

        var vertexCount = geometry.Positions.Count;
        if (vertexCount == 0)
        {
            return geometry;
        }

        if (mesh.Normals.Count == vertexCount * 3)
        {
            geometry.Normals = new Vector3Collection();
            for (var index = 0; index + 2 < mesh.Normals.Count; index += 3)
            {
                geometry.Normals.Add(new Vector3(mesh.Normals[index], mesh.Normals[index + 1], mesh.Normals[index + 2]));
            }
        }

        var textureCoordinates = SelectTextureCoordinates(mesh, scene, materialIndex, renderMode, selectedSlot);
        if (textureCoordinates.Count == vertexCount * 2)
        {
            geometry.TextureCoordinates = new Vector2Collection();
            for (var index = 0; index + 1 < textureCoordinates.Count; index += 2)
            {
                geometry.TextureCoordinates.Add(new Vector2(textureCoordinates[index], textureCoordinates[index + 1]));
            }
        }

        for (var index = 0; index + 2 < mesh.Indices.Count; index += 3)
        {
            var a = mesh.Indices[index];
            var b = mesh.Indices[index + 1];
            var c = mesh.Indices[index + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertexCount || b >= vertexCount || c >= vertexCount)
            {
                continue;
            }

            geometry.TriangleIndices.Add(a);
            geometry.TriangleIndices.Add(b);
            geometry.TriangleIndices.Add(c);
        }

        if (geometry.Normals is null || geometry.Normals.Count != vertexCount)
        {
            geometry.Normals = BuildPreviewNormals(geometry.Positions, geometry.TriangleIndices);
        }

        return geometry;
    }

    private static IReadOnlyList<float> SelectTextureCoordinates(CanonicalMesh mesh, CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var primaryTexture = SelectPrimaryViewportTexture(material, renderMode, selectedSlot);
        var hasExplicitTextureUvDirective = HasExplicitTextureUvDirective(primaryTexture);
        var hasConfirmedTextureUvDirective = hasExplicitTextureUvDirective &&
                                            primaryTexture is not null &&
                                            !primaryTexture.IsApproximateUvTransform;
        var requestedChannel = hasConfirmedTextureUvDirective
            ? primaryTexture!.UvChannel
            : mesh.PreferredUvChannel;
        IReadOnlyList<float> coordinates;

        if (requestedChannel == 1 && mesh.Uv1s is { Count: > 0 })
        {
            coordinates = mesh.Uv1s;
        }
        else if (mesh.Uv0s is { Count: > 0 })
        {
            coordinates = mesh.Uv0s;
        }
        else
        {
            coordinates = mesh.Uvs;
        }

        if (requestedChannel == 1 && hasConfirmedTextureUvDirective)
        {
            coordinates = NormalizeUvCoordinatesIfLocalSubspace(coordinates);
        }

        if (renderMode != SceneRenderMode.MaterialUv ||
            primaryTexture is null ||
            primaryTexture.IsApproximateUvTransform ||
            (Math.Abs(primaryTexture.UvScaleU - 1f) < 0.0001f &&
             Math.Abs(primaryTexture.UvScaleV - 1f) < 0.0001f &&
             Math.Abs(primaryTexture.UvOffsetU) < 0.0001f &&
             Math.Abs(primaryTexture.UvOffsetV) < 0.0001f))
        {
            return coordinates;
        }

        var transformed = new float[coordinates.Count];
        for (var index = 0; index + 1 < coordinates.Count; index += 2)
        {
            transformed[index] = (coordinates[index] * primaryTexture.UvScaleU) + primaryTexture.UvOffsetU;
            transformed[index + 1] = (coordinates[index + 1] * primaryTexture.UvScaleV) + primaryTexture.UvOffsetV;
        }

        return transformed;
    }

    private static CanonicalTexture? SelectPrimaryViewportTexture(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
    {
        var textureGroup = SelectViewportTextureGroup(material, renderMode, selectedSlot);
        if (textureGroup.Count == 0)
        {
            return null;
        }

        return textureGroup
            .OrderByDescending(texture => ScorePrimaryViewportTexture(texture, renderMode))
            .ThenBy(texture => texture.Slot, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static IReadOnlyList<float> NormalizeUvCoordinatesIfLocalSubspace(IReadOnlyList<float> coordinates)
    {
        if (coordinates.Count < 4)
        {
            return coordinates;
        }

        var minU = float.PositiveInfinity;
        var maxU = float.NegativeInfinity;
        var minV = float.PositiveInfinity;
        var maxV = float.NegativeInfinity;
        for (var index = 0; index + 1 < coordinates.Count; index += 2)
        {
            minU = Math.Min(minU, coordinates[index]);
            maxU = Math.Max(maxU, coordinates[index]);
            minV = Math.Min(minV, coordinates[index + 1]);
            maxV = Math.Max(maxV, coordinates[index + 1]);
        }

        if (!float.IsFinite(minU) || !float.IsFinite(maxU) || !float.IsFinite(minV) || !float.IsFinite(maxV))
        {
            return coordinates;
        }

        var rangeU = maxU - minU;
        var rangeV = maxV - minV;
        if (rangeU <= 0.0001f || rangeV <= 0.0001f)
        {
            return coordinates;
        }

        // Some TS4 UV1 atlas layouts are stored in a local subspace rather than spanning the
        // full 0..1 range. Normalize them before applying a material-driven atlas transform.
        var normalized = new float[coordinates.Count];
        for (var index = 0; index + 1 < coordinates.Count; index += 2)
        {
            normalized[index] = (coordinates[index] - minU) / rangeU;
            normalized[index + 1] = (coordinates[index + 1] - minV) / rangeV;
        }

        return normalized;
    }

    private static Vector3Collection BuildPreviewNormals(IList<Vector3> positions, IList<int> triangleIndices)
    {
        var normals = Enumerable.Repeat(Vector3.Zero, positions.Count).ToArray();

        for (var index = 0; index + 2 < triangleIndices.Count; index += 3)
        {
            var ia = triangleIndices[index];
            var ib = triangleIndices[index + 1];
            var ic = triangleIndices[index + 2];
            if (ia < 0 || ib < 0 || ic < 0 || ia >= positions.Count || ib >= positions.Count || ic >= positions.Count)
            {
                continue;
            }

            var a = positions[ia];
            var b = positions[ib];
            var c = positions[ic];
            var ab = b - a;
            var ac = c - a;
            var face = Vector3.Cross(ab, ac);
            if (face.LengthSquared() <= 1e-12f)
            {
                continue;
            }

            normals[ia] += face;
            normals[ib] += face;
            normals[ic] += face;
        }

        var result = new Vector3Collection();
        foreach (var normal in normals)
        {
            result.Add(normal.LengthSquared() <= 1e-12f ? Vector3.UnitY : Vector3.Normalize(normal));
        }

        return result;
    }

    private static PhongMaterial CreateMaterial(CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var textureGroup = SelectViewportTextureGroup(material, renderMode, selectedSlot);
        var diffuseTexture = SelectBaseColorTexture(textureGroup);
        var layeredColorTexture = SelectLayeredColorTexture(textureGroup);
        var emissiveTexture = SelectEmissiveTexture(textureGroup, diffuseTexture, layeredColorTexture);
        var opacityTexture = SelectViewportOpacityTexture(material, textureGroup, diffuseTexture, selectedSlot);
        var normalTexture = SelectNormalTexture(textureGroup);
        var specularTexture = SelectSpecularTexture(textureGroup);
        var colorShiftMaskTexture = SelectColorShiftMaskTexture(textureGroup);
        var selectedSlotTexture = !string.IsNullOrWhiteSpace(selectedSlot)
            ? textureGroup.FirstOrDefault(texture => texture.Slot.Equals(selectedSlot, StringComparison.OrdinalIgnoreCase))
            : null;
        var viewportColorTexture = selectedSlotTexture ?? diffuseTexture ?? layeredColorTexture ?? emissiveTexture;
        var uvTransform = BuildUvTransform(viewportColorTexture);
        var viewportTintColor = BuildViewportTintColor(material);
        var approximateBaseColor = BuildApproximateBaseColor(material);
        var effectiveBaseColor = viewportTintColor ?? approximateBaseColor;
        var diffuseColor = effectiveBaseColor ?? new Color4(0.62f, 0.62f, 0.62f, 1f);
        var ambientColor = effectiveBaseColor is null
            ? new Color4(0.3f, 0.3f, 0.3f, 1f)
            : new Color4(
                Math.Clamp(effectiveBaseColor.Value.Red * 0.3f, 0f, 1f),
                Math.Clamp(effectiveBaseColor.Value.Green * 0.3f, 0f, 1f),
                Math.Clamp(effectiveBaseColor.Value.Blue * 0.3f, 0f, 1f),
                1f);

        var isFlat = renderMode == SceneRenderMode.FlatTexture;
        var isWireframe = renderMode == SceneRenderMode.Wireframe;
        var isLit = renderMode == SceneRenderMode.LitTexture;
        var renderDiffuseMap =
            !isWireframe &&
            !isFlat &&
            viewportColorTexture is not null;
        var renderEmissiveMap = (isFlat && viewportColorTexture is not null) || (!isFlat && emissiveTexture is not null);
        var renderAlphaMap = ShouldRenderTransparentViewport(material, textureGroup, diffuseTexture, selectedSlot);
        var forceOpaqueViewportTexture = isLit && !renderAlphaMap;
        var (textureModel, usesSwatchComposite) = CreateViewportTextureModel(
            material,
            viewportColorTexture,
            colorShiftMaskTexture,
            viewportTintColor,
            forceOpaqueViewportTexture);
        var emissiveTextureModel = emissiveTexture is null
            ? null
            : new TextureModel(new MemoryStream(emissiveTexture.PngBytes), autoCloseStream: true);
        var alphaTextureModel = opacityTexture is null
            ? null
            : new TextureModel(new MemoryStream(opacityTexture.PngBytes), autoCloseStream: true);
        var normalTextureModel = normalTexture is null
            ? null
            : new TextureModel(new MemoryStream(normalTexture.PngBytes), autoCloseStream: true);
        var specularTextureModel = specularTexture is null
            ? null
            : new TextureModel(new MemoryStream(specularTexture.PngBytes), autoCloseStream: true);
        var usesOverlayAsSecondaryLayer =
            emissiveTexture is not null &&
            layeredColorTexture is not null &&
            ReferenceEquals(emissiveTexture, layeredColorTexture);
        var hasLitNormalDetail = normalTextureModel is not null;
        var hasLitSpecularDetail = specularTextureModel is not null;
        var useMatteLitShading = isLit && !hasLitNormalDetail && !hasLitSpecularDetail;
        var litAmbientColor = useMatteLitShading
            ? new Color4(0.46f, 0.46f, 0.46f, 1f)
            : new Color4(0.34f, 0.34f, 0.34f, 1f);
        var litSpecularColor = useMatteLitShading
            ? new Color4(0.02f, 0.02f, 0.02f, 1f)
            : new Color4(0.08f, 0.08f, 0.08f, 1f);
        var litSpecularShininess = useMatteLitShading ? 4f : 12f;
        // The viewport tint is metadata for skintone/swatch routing, not a blanket multiplier for
        // already-decoded textured materials. If a texture map is present, keep the lit diffuse
        // multiplier neutral unless a dedicated texture composite has already baked the tint in.
        var effectiveTexturedLitDiffuseColor = new Color4(1f, 1f, 1f, 1f);

        return new PhongMaterial
        {
            DiffuseColor = isWireframe
                ? new Color4(0.95f, 0.95f, 0.95f, 1f)
                : isLit
                    ? (renderDiffuseMap ? effectiveTexturedLitDiffuseColor : diffuseColor)
                    : viewportColorTexture is null
                        ? diffuseColor
                        : new Color4(0f, 0f, 0f, 1f),
            AmbientColor = isFlat
                ? viewportColorTexture is null
                    ? diffuseColor
                    : new Color4(0f, 0f, 0f, 1f)
                : isLit
                    ? (renderDiffuseMap ? litAmbientColor : ambientColor)
                    : ambientColor,
            EmissiveColor = isFlat
                ? new Color4(1f, 1f, 1f, 1f)
                : usesOverlayAsSecondaryLayer
                    ? new Color4(0.45f, 0.45f, 0.45f, 1f)
                    : new Color4(0f, 0f, 0f, 1f),
            SpecularColor = isFlat ? new Color4(0f, 0f, 0f, 1f) : litSpecularColor,
            SpecularShininess = isFlat ? 0f : litSpecularShininess,
            RenderDiffuseMap = renderDiffuseMap,
            DiffuseMap = renderDiffuseMap ? textureModel : null,
            RenderEmissiveMap = renderEmissiveMap,
            EmissiveMap = isFlat
                ? (renderEmissiveMap ? textureModel : null)
                : emissiveTextureModel,
            RenderDiffuseAlphaMap = renderAlphaMap,
            DiffuseAlphaMap = renderAlphaMap ? alphaTextureModel : null,
            RenderNormalMap = isLit && normalTextureModel is not null,
            NormalMap = isLit ? normalTextureModel : null,
            RenderSpecularColorMap = isLit && specularTextureModel is not null,
            SpecularColorMap = isLit ? specularTextureModel : null,
            EnableAutoTangent = isLit && normalTextureModel is not null,
            RenderShadowMap = isLit,
            UVTransform = uvTransform
        };
    }

    private static (TextureModel? TextureModel, bool UsesSwatchComposite) CreateViewportTextureModel(
        CanonicalMaterial? material,
        CanonicalTexture? viewportColorTexture,
        CanonicalTexture? colorShiftMaskTexture,
        Color4? viewportTintColor,
        bool forceOpaqueAlpha)
    {
        if (material?.SourceKind == CanonicalMaterialSourceKind.ApproximateCas &&
            viewportColorTexture is not null &&
            colorShiftMaskTexture is not null &&
            viewportTintColor is not null)
        {
            var compositedPngBytes = ComposeSwatchMaskedPng(
                viewportColorTexture.PngBytes,
                colorShiftMaskTexture.PngBytes,
                viewportTintColor.Value);
            if (compositedPngBytes is not null)
            {
                if (forceOpaqueAlpha)
                {
                    compositedPngBytes = ForceOpaquePngAlpha(compositedPngBytes);
                }

                return (new TextureModel(new MemoryStream(compositedPngBytes), autoCloseStream: true), true);
            }
        }

        return (CreateTextureModel(viewportColorTexture, forceOpaqueAlpha), false);
    }

    private static Color4? BuildApproximateBaseColor(CanonicalMaterial? material)
    {
        if (material?.ApproximateBaseColor is not { } color)
        {
            return null;
        }

        return new Color4(
            Math.Clamp(color.R, 0f, 1f),
            Math.Clamp(color.G, 0f, 1f),
            Math.Clamp(color.B, 0f, 1f),
            Math.Clamp(color.A, 0f, 1f));
    }

    private static Color4? BuildViewportTintColor(CanonicalMaterial? material)
    {
        if (material?.ViewportTintColor is not { } color)
        {
            return null;
        }

        return new Color4(
            Math.Clamp(color.R, 0f, 1f),
            Math.Clamp(color.G, 0f, 1f),
            Math.Clamp(color.B, 0f, 1f),
            Math.Clamp(color.A, 0f, 1f));
    }

    private static TextureModel? CreateTextureModel(CanonicalTexture? texture, bool forceOpaqueAlpha = false)
    {
        if (texture is null)
        {
            return null;
        }

        var pngBytes = forceOpaqueAlpha ? ForceOpaquePngAlpha(texture.PngBytes) : texture.PngBytes;
        return new TextureModel(new MemoryStream(pngBytes), autoCloseStream: true);
    }

    private static byte[] ForceOpaquePngAlpha(byte[] pngBytes)
    {
        using var input = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        input.WriteAsync(pngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
        input.Seek(0);

        var decoder = BitmapDecoder.CreateAsync(input).AsTask().GetAwaiter().GetResult();
        var pixelData = decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult();
        var pixels = pixelData.DetachPixelData();
        for (var offset = 3; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = 0xFF;
        }

        using var output = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            decoder.PixelWidth,
            decoder.PixelHeight,
            decoder.DpiX,
            decoder.DpiY,
            pixels);
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
        output.Seek(0);
        using var stream = output.AsStreamForRead();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static byte[]? ComposeSwatchMaskedPng(byte[] diffusePngBytes, byte[] maskPngBytes, Color4 tintColor)
    {
        try
        {
            using var diffuseStream = new InMemoryRandomAccessStream();
            diffuseStream.WriteAsync(diffusePngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            diffuseStream.Seek(0);

            using var maskStream = new InMemoryRandomAccessStream();
            maskStream.WriteAsync(maskPngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            maskStream.Seek(0);

            var diffuseDecoder = BitmapDecoder.CreateAsync(diffuseStream).AsTask().GetAwaiter().GetResult();
            var maskDecoder = BitmapDecoder.CreateAsync(maskStream).AsTask().GetAwaiter().GetResult();
            var diffusePixels = diffuseDecoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();
            var maskPixels = maskDecoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform
                {
                    ScaledWidth = diffuseDecoder.PixelWidth,
                    ScaledHeight = diffuseDecoder.PixelHeight
                },
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();
            if (diffusePixels.Length != maskPixels.Length)
            {
                return null;
            }

            var tintR = Math.Clamp(tintColor.Red, 0f, 1f);
            var tintG = Math.Clamp(tintColor.Green, 0f, 1f);
            var tintB = Math.Clamp(tintColor.Blue, 0f, 1f);
            var tintA = Math.Clamp(tintColor.Alpha, 0f, 1f);
            var composedPixels = new byte[diffusePixels.Length];
            for (var offset = 0; offset < diffusePixels.Length; offset += 4)
            {
                var baseB = diffusePixels[offset] / 255f;
                var baseG = diffusePixels[offset + 1] / 255f;
                var baseR = diffusePixels[offset + 2] / 255f;
                var baseA = diffusePixels[offset + 3];

                var maskB = maskPixels[offset] / 255f;
                var maskG = maskPixels[offset + 1] / 255f;
                var maskR = maskPixels[offset + 2] / 255f;
                var maskA = maskPixels[offset + 3] / 255f;
                var maskFactor = Math.Clamp(Math.Max(maskA, Math.Max(maskR, Math.Max(maskG, maskB))) * tintA, 0f, 1f);

                var blendedR = baseR * ((1f - maskFactor) + (maskFactor * tintR));
                var blendedG = baseG * ((1f - maskFactor) + (maskFactor * tintG));
                var blendedB = baseB * ((1f - maskFactor) + (maskFactor * tintB));

                composedPixels[offset] = (byte)Math.Clamp((int)Math.Round(blendedB * 255f), 0, 255);
                composedPixels[offset + 1] = (byte)Math.Clamp((int)Math.Round(blendedG * 255f), 0, 255);
                composedPixels[offset + 2] = (byte)Math.Clamp((int)Math.Round(blendedR * 255f), 0, 255);
                composedPixels[offset + 3] = baseA;
            }

            using var output = new InMemoryRandomAccessStream();
            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                diffuseDecoder.PixelWidth,
                diffuseDecoder.PixelHeight,
                diffuseDecoder.DpiX,
                diffuseDecoder.DpiY,
                composedPixels);
            encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
            output.Seek(0);
            using var stream = output.AsStreamForRead();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch
        {
            return null;
        }
    }

    private static UVTransform BuildUvTransform(CanonicalTexture? texture)
    {
        if (texture is null || !HasExplicitTextureUvDirective(texture))
        {
            return new UVTransform(0f);
        }

        return new UVTransform(
            rotation: 0f,
            scalingX: texture.UvScaleU,
            scalingY: texture.UvScaleV,
            translationX: texture.UvOffsetU,
            translationY: texture.UvOffsetV);
    }

    private static bool HasExplicitTextureUvDirective(CanonicalTexture? texture)
    {
        if (texture is null)
        {
            return false;
        }

        return texture.UvChannel != 0 ||
               Math.Abs(texture.UvScaleU - 1f) >= 0.0001f ||
               Math.Abs(texture.UvScaleV - 1f) >= 0.0001f ||
               Math.Abs(texture.UvOffsetU) >= 0.0001f ||
               Math.Abs(texture.UvOffsetV) >= 0.0001f;
    }

    private static IReadOnlyList<CanonicalTexture> SelectViewportTextureGroup(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(selectedSlot))
        {
            return material.Textures
                .Where(texture => texture.Slot.Equals(selectedSlot, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(texture => ScorePrimaryViewportTexture(texture, renderMode))
                .ThenBy(texture => texture.Slot, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return material.Textures
            .GroupBy(BuildTextureSamplingKey, StringComparer.Ordinal)
            .OrderByDescending(group => ScoreTextureGroup(group, renderMode))
            .ThenByDescending(group => group.Count())
            .Select(group => group
                .OrderByDescending(texture => ScorePrimaryViewportTexture(texture, renderMode))
                .ThenBy(texture => texture.Slot, StringComparer.OrdinalIgnoreCase)
                .ToArray())
            .FirstOrDefault() ?? [];
    }

    private static string BuildTextureSamplingKey(CanonicalTexture texture) =>
        FormattableString.Invariant($"{texture.UvChannel}|{texture.UvScaleU:0.####}|{texture.UvScaleV:0.####}|{texture.UvOffsetU:0.####}|{texture.UvOffsetV:0.####}");

    private static int ScoreTextureGroup(IGrouping<string, CanonicalTexture> group, SceneRenderMode renderMode) =>
        group.Sum(texture => ScorePrimaryViewportTexture(texture, renderMode)) + group.Count();

    private static int ScorePrimaryViewportTexture(CanonicalTexture texture, SceneRenderMode renderMode)
    {
        var slot = texture.Slot;
        return texture.Semantic switch
        {
            CanonicalTextureSemantic.BaseColor => renderMode == SceneRenderMode.Wireframe ? 0 : 100,
            CanonicalTextureSemantic.Overlay => 80,
            CanonicalTextureSemantic.Emissive => renderMode == SceneRenderMode.FlatTexture ? 75 : 65,
            CanonicalTextureSemantic.Normal => renderMode == SceneRenderMode.LitTexture ? 35 : 5,
            CanonicalTextureSemantic.Specular => renderMode == SceneRenderMode.LitTexture ? 30 : 5,
            CanonicalTextureSemantic.Gloss => renderMode == SceneRenderMode.LitTexture ? 25 : 4,
            CanonicalTextureSemantic.Opacity => 20,
            _ when slot.Contains("detail", StringComparison.OrdinalIgnoreCase) => 78,
            _ when slot.Contains("decal", StringComparison.OrdinalIgnoreCase) => 76,
            _ when slot.Contains("dirt", StringComparison.OrdinalIgnoreCase) => 72,
            _ when slot.Contains("grime", StringComparison.OrdinalIgnoreCase) => 72,
            _ when slot.Contains("emiss", StringComparison.OrdinalIgnoreCase) => 64,
            _ when slot.Contains("overlay", StringComparison.OrdinalIgnoreCase) => 79,
            _ when slot.Contains("normal", StringComparison.OrdinalIgnoreCase) => renderMode == SceneRenderMode.LitTexture ? 34 : 4,
            _ when slot.Contains("spec", StringComparison.OrdinalIgnoreCase) => renderMode == SceneRenderMode.LitTexture ? 29 : 4,
            _ => 1
        };
    }

    private static bool IsTransparentMaterial(CanonicalScene scene, int materialIndex, string? selectedSlot)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var textureGroup = SelectViewportTextureGroup(material, SceneRenderMode.LitTexture, selectedSlot);
        var diffuseTexture = SelectBaseColorTexture(textureGroup);
        return ShouldRenderTransparentViewport(material, textureGroup, diffuseTexture, selectedSlot);
    }

    private async Task<WriteableBitmap> GenerateUvPreviewAsync(CanonicalScene scene, CancellationToken cancellationToken)
    {
        var renderMode = ViewModel.SelectedSceneRenderMode;
        var selectedSlot = GetSelectedSceneTextureSlot();
        var textureEntries = scene.Materials
            .Select((material, index) => new
            {
                MaterialIndex = index,
                Texture = SelectPrimaryViewportTexture(material, renderMode, selectedSlot)
            })
            .Where(entry => entry.Texture is not null)
            .GroupBy(
                entry => entry.Texture!.SourceKey is { } key
                    ? $"{key.Type:X8}:{key.Group:X8}:{key.FullInstance:X16}"
                    : $"{entry.Texture.FileName}|{Convert.ToHexString(SHA256.HashData(entry.Texture.PngBytes))}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Texture = group.First().Texture!,
                MaterialIndices = group.Select(entry => entry.MaterialIndex).Distinct().ToArray()
            })
            .ToArray();

        if (textureEntries.Length == 0)
        {
            return CreateSolidBitmap(512, 512, 0xFF1E1E1E);
        }

        const int panelPadding = 16;
        const int panelMaxWidth = 1024;
        const int panelMaxHeight = 1024;
        var panels = new List<(CanonicalTexture Texture, int[] MaterialIndices, int SourceWidth, int SourceHeight, int Width, int Height, float Scale, byte[] Pixels)>();
        foreach (var entry in textureEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decoded = await DecodePngAsync(entry.Texture.PngBytes, cancellationToken);
            var scale = MathF.Min(1f, MathF.Min(panelMaxWidth / (float)decoded.Width, panelMaxHeight / (float)decoded.Height));
            var width = Math.Max(1, (int)MathF.Round(decoded.Width * scale));
            var height = Math.Max(1, (int)MathF.Round(decoded.Height * scale));
            panels.Add((entry.Texture, entry.MaterialIndices, decoded.Width, decoded.Height, width, height, scale, decoded.Pixels));
        }

        var canvasWidth = panels.Max(panel => panel.Width) + (panelPadding * 2);
        var canvasHeight = panels.Sum(panel => panel.Height) + (panelPadding * (panels.Count + 1));
        var canvas = new byte[canvasWidth * canvasHeight * 4];
        FillSolid(canvas, canvasWidth, canvasHeight, 0xFF111111);

        var colors = new uint[] { 0xFFFF4FD1, 0xFFFFFF3B, 0xFFFF7F50, 0xFF7CFC00, 0xFFFF69B4, 0xFF87CEFA };
        var offsetY = panelPadding;
        foreach (var (_, materialIndices, sourceWidth, sourceHeight, width, height, _, pixels) in panels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offsetX = panelPadding;
            BlitScaledBgra(canvas, canvasWidth, canvasHeight, pixels, sourceWidth, sourceHeight, width, height, offsetX, offsetY);

            var meshes = scene.Meshes
                .Select((mesh, meshIndex) => new { Mesh = mesh, MeshIndex = meshIndex })
                .Where(entry => Array.IndexOf(materialIndices, entry.Mesh.MaterialIndex) >= 0)
                .ToArray();
            foreach (var meshEntry in meshes)
            {
                var coordinates = SelectTextureCoordinates(meshEntry.Mesh, scene, meshEntry.Mesh.MaterialIndex, renderMode, selectedSlot);
                var color = colors[meshEntry.MeshIndex % colors.Length];
                DrawUvWireframe(canvas, canvasWidth, canvasHeight, coordinates, meshEntry.Mesh.Indices, offsetX, offsetY, width, height, color);
            }

            offsetY += height + panelPadding;
        }

        return await CreateWriteableBitmapAsync(canvasWidth, canvasHeight, canvas, cancellationToken);
    }

    private static void DrawUvWireframe(byte[] canvas, int canvasWidth, int canvasHeight, IReadOnlyList<float> coordinates, IReadOnlyList<int> indices, int offsetX, int offsetY, int panelWidth, int panelHeight, uint color)
    {
        if (coordinates.Count < 4)
        {
            return;
        }

        for (var index = 0; index + 2 < indices.Count; index += 3)
        {
            var ia = indices[index];
            var ib = indices[index + 1];
            var ic = indices[index + 2];
            if (!TryGetUvPoint(coordinates, ia, offsetX, offsetY, panelWidth, panelHeight, out var ax, out var ay) ||
                !TryGetUvPoint(coordinates, ib, offsetX, offsetY, panelWidth, panelHeight, out var bx, out var by) ||
                !TryGetUvPoint(coordinates, ic, offsetX, offsetY, panelWidth, panelHeight, out var cx, out var cy))
            {
                continue;
            }

            DrawLine(canvas, canvasWidth, canvasHeight, ax, ay, bx, by, color);
            DrawLine(canvas, canvasWidth, canvasHeight, bx, by, cx, cy, color);
            DrawLine(canvas, canvasWidth, canvasHeight, cx, cy, ax, ay, color);
        }
    }

    private static bool TryGetUvPoint(IReadOnlyList<float> coordinates, int vertexIndex, int offsetX, int offsetY, int panelWidth, int panelHeight, out int x, out int y)
    {
        var uvIndex = vertexIndex * 2;
        if (uvIndex < 0 || uvIndex + 1 >= coordinates.Count)
        {
            x = 0;
            y = 0;
            return false;
        }

        var u = Math.Clamp(coordinates[uvIndex], 0f, 1f);
        var v = Math.Clamp(coordinates[uvIndex + 1], 0f, 1f);
        x = offsetX + (int)MathF.Round(u * (panelWidth - 1));
        y = offsetY + (int)MathF.Round(v * (panelHeight - 1));
        return true;
    }

    private static void DrawLine(byte[] canvas, int canvasWidth, int canvasHeight, int x0, int y0, int x1, int y1, uint color)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            PlotPixel(canvas, canvasWidth, canvasHeight, x0, y0, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var twiceError = 2 * error;
            if (twiceError >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (twiceError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void PlotPixel(byte[] canvas, int canvasWidth, int canvasHeight, int x, int y, uint color)
    {
        if ((uint)x >= canvasWidth || (uint)y >= canvasHeight)
        {
            return;
        }

        var offset = ((y * canvasWidth) + x) * 4;
        canvas[offset] = (byte)(color & 0xFF);
        canvas[offset + 1] = (byte)((color >> 8) & 0xFF);
        canvas[offset + 2] = (byte)((color >> 16) & 0xFF);
        canvas[offset + 3] = (byte)((color >> 24) & 0xFF);
    }

    private static void FillSolid(byte[] canvas, int width, int height, uint color)
    {
        for (var index = 0; index < width * height; index++)
        {
            var offset = index * 4;
            canvas[offset] = (byte)(color & 0xFF);
            canvas[offset + 1] = (byte)((color >> 8) & 0xFF);
            canvas[offset + 2] = (byte)((color >> 16) & 0xFF);
            canvas[offset + 3] = (byte)((color >> 24) & 0xFF);
        }
    }

    private static void BlitScaledBgra(byte[] destination, int destinationWidth, int destinationHeight, byte[] source, int sourceWidth, int sourceHeight, int scaledWidth, int scaledHeight, int offsetX, int offsetY)
    {
        for (var y = 0; y < scaledHeight; y++)
        {
            for (var x = 0; x < scaledWidth; x++)
            {
                var srcX = sourceWidth == scaledWidth ? x : Math.Clamp((int)((x / (float)scaledWidth) * sourceWidth), 0, sourceWidth - 1);
                var srcY = sourceHeight == scaledHeight ? y : Math.Clamp((int)((y / (float)scaledHeight) * sourceHeight), 0, sourceHeight - 1);
                var srcOffset = ((srcY * sourceWidth) + srcX) * 4;
                var dstX = offsetX + x;
                var dstY = offsetY + y;
                if ((uint)dstX >= destinationWidth || (uint)dstY >= destinationHeight)
                {
                    continue;
                }

                var dstOffset = ((dstY * destinationWidth) + dstX) * 4;
                destination[dstOffset] = source[srcOffset];
                destination[dstOffset + 1] = source[srcOffset + 1];
                destination[dstOffset + 2] = source[srcOffset + 2];
                destination[dstOffset + 3] = source[srcOffset + 3];
            }
        }
    }

    private static async Task<(int Width, int Height, byte[] Pixels)> DecodePngAsync(byte[] pngBytes, CancellationToken cancellationToken)
    {
        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer()).AsTask(cancellationToken);
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask(cancellationToken);
        return ((int)decoder.PixelWidth, (int)decoder.PixelHeight, pixelData.DetachPixelData());
    }

    private static async Task<WriteableBitmap> CreateWriteableBitmapAsync(int width, int height, byte[] pixels, CancellationToken cancellationToken)
    {
        var bitmap = new WriteableBitmap(width, height);
        using var stream = bitmap.PixelBuffer.AsStream();
        await stream.WriteAsync(pixels, cancellationToken);
        bitmap.Invalidate();
        return bitmap;
    }

    private static WriteableBitmap CreateSolidBitmap(int width, int height, uint color)
    {
        var pixels = new byte[width * height * 4];
        FillSolid(pixels, width, height, color);
        var bitmap = new WriteableBitmap(width, height);
        using var stream = bitmap.PixelBuffer.AsStream();
        stream.Write(pixels, 0, pixels.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    private void MainPaneSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        isDraggingMainSplitter = true;
        mainSplitterStartX = e.GetCurrentPoint(MainContentGrid).Position.X;
        mainSplitterStartResultsWidth = ResultsColumn.ActualWidth;
        mainSplitterTotalResizableWidth = ResultsColumn.ActualWidth + PreviewColumn.ActualWidth;
        if (sender is UIElement element)
        {
            element.CapturePointer(e.Pointer);
        }
    }

    private void MainPaneSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isDraggingMainSplitter)
        {
            return;
        }

        var positionX = e.GetCurrentPoint(MainContentGrid).Position.X;
        var delta = positionX - mainSplitterStartX;
        var previewMinWidth = PreviewColumn.MinWidth > 0 ? PreviewColumn.MinWidth : 520;
        var resultsMinWidth = ResultsColumn.MinWidth > 0 ? ResultsColumn.MinWidth : 320;
        var newResultsWidth = Math.Clamp(mainSplitterStartResultsWidth + delta, resultsMinWidth, mainSplitterTotalResizableWidth - previewMinWidth);
        ResultsColumn.Width = new GridLength(newResultsWidth, GridUnitType.Pixel);
        PreviewColumn.Width = new GridLength(Math.Max(previewMinWidth, mainSplitterTotalResizableWidth - newResultsWidth), GridUnitType.Pixel);
    }

    private void MainPaneSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        isDraggingMainSplitter = false;
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }
    }

    private void PreviewPaneSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        isDraggingPreviewSplitter = true;
        previewSplitterStartY = e.GetCurrentPoint(PreviewPaneGrid).Position.Y;
        previewSplitterStartHeight = PreviewViewportRow.ActualHeight;
        if (sender is UIElement element)
        {
            element.CapturePointer(e.Pointer);
        }
    }

    private void PreviewPaneSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isDraggingPreviewSplitter)
        {
            return;
        }

        var positionY = e.GetCurrentPoint(PreviewPaneGrid).Position.Y;
        var delta = positionY - previewSplitterStartY;
        var occupiedHeight =
            PreviewPaneGrid.RowDefinitions[0].ActualHeight +
            PreviewPaneGrid.RowDefinitions[2].ActualHeight +
            PreviewPaneGrid.RowDefinitions[3].ActualHeight;
        var availableResizableHeight = Math.Max(300, PreviewPaneGrid.ActualHeight - occupiedHeight);
        var minPreviewHeight = Math.Max(PreviewViewportRow.MinHeight, 180);
        var minDetailsHeight = Math.Max(PreviewDiagnosticsRow.MinHeight, 120d);
        var newPreviewHeight = Math.Clamp(previewSplitterStartHeight + delta, minPreviewHeight, availableResizableHeight - minDetailsHeight);
        PreviewViewportRow.Height = new GridLength(newPreviewHeight, GridUnitType.Pixel);
    }

    private void PreviewPaneSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        isDraggingPreviewSplitter = false;
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }
    }

    private static bool TextureSupportsAlpha(CanonicalTexture? texture)
    {
        var bytes = texture?.PngBytes;
        if (bytes is null || bytes.Length < 33)
        {
            return false;
        }

        ReadOnlySpan<byte> pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!bytes.AsSpan(0, pngSignature.Length).SequenceEqual(pngSignature))
        {
            return false;
        }

        // PNG IHDR chunk stores the color type at byte 25 of the file.
        // 4 = grayscale+alpha, 6 = RGBA.
        var colorType = bytes[25];
        if (colorType is 4 or 6)
        {
            return true;
        }

        // Palette PNGs may carry transparency through a tRNS chunk.
        for (var offset = 8; offset + 8 <= bytes.Length;)
        {
            var chunkLength = (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
            if (chunkLength < 0 || offset + 12 + chunkLength > bytes.Length)
            {
                break;
            }

            if (bytes[offset + 4] == (byte)'t' &&
                bytes[offset + 5] == (byte)'R' &&
                bytes[offset + 6] == (byte)'N' &&
                bytes[offset + 7] == (byte)'S')
            {
                return true;
            }

            offset += 12 + chunkLength;
        }

        return false;
    }

    private static CanonicalTexture? SelectBaseColorTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        static bool IsPreferredDiffuseSlot(string? slot) =>
            slot is not null &&
            (slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase) ||
             slot.Equals("basecolor", StringComparison.OrdinalIgnoreCase) ||
             slot.Equals("albedo", StringComparison.OrdinalIgnoreCase) ||
             slot.Equals("texture_0", StringComparison.OrdinalIgnoreCase));

        static bool IsNonColorSlot(string? slot) =>
            slot is not null &&
            (slot.Contains("spec", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("normal", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("rough", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("metal", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("gloss", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("overlay", StringComparison.OrdinalIgnoreCase));

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.BaseColor)
            ?? material.Textures.FirstOrDefault(texture => IsPreferredDiffuseSlot(texture.Slot))
            ?? material.Textures.FirstOrDefault(texture => !IsNonColorSlot(texture.Slot))
            ?? material.Textures.FirstOrDefault();
    }

    private static CanonicalTexture? SelectBaseColorTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectBaseColorTexture(scope);
    }

    private static CanonicalTexture? SelectOpacityTexture(CanonicalMaterial? material, CanonicalTexture? baseColorTexture)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null;
        }

        if (!string.IsNullOrWhiteSpace(material.AlphaTextureSlot))
        {
            var explicitAlpha = material.Textures.FirstOrDefault(texture =>
                texture.Slot.Equals(material.AlphaTextureSlot, StringComparison.OrdinalIgnoreCase));
            if (explicitAlpha is not null)
            {
                return explicitAlpha;
            }
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Opacity)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("cutout", StringComparison.OrdinalIgnoreCase))
            ?? (ShouldUseBaseColorAlphaAsOpacityFallback(material, baseColorTexture) ? baseColorTexture : null);
    }

    private static CanonicalTexture? SelectViewportOpacityTexture(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        CanonicalTexture? baseColorTexture,
        string? selectedSlot)
    {
        if (string.IsNullOrWhiteSpace(selectedSlot))
        {
            return SelectDefaultViewportOpacityTexture(material, baseColorTexture);
        }

        var scopedAlphaSlot = !string.IsNullOrWhiteSpace(material?.AlphaTextureSlot) &&
                              string.Equals(material.AlphaTextureSlot, selectedSlot, StringComparison.OrdinalIgnoreCase)
            ? material.AlphaTextureSlot
            : null;
        return SelectOpacityTexture(textures, baseColorTexture, scopedAlphaSlot);
    }

    private static bool HasExplicitOpacityTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(material.AlphaTextureSlot) &&
            material.Textures.Any(texture => texture.Slot.Equals(material.AlphaTextureSlot, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return material.Textures.Any(texture =>
            texture.Semantic == CanonicalTextureSemantic.Opacity ||
            texture.Slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("cutout", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectDefaultViewportOpacityTexture(
        CanonicalMaterial? material,
        CanonicalTexture? baseColorTexture)
    {
        if (material?.IsTransparent == true)
        {
            return SelectOpacityTexture(material, baseColorTexture);
        }

        if (material is null)
        {
            return TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null;
        }

        return ShouldUseBaseColorAlphaAsOpacityFallback(material, baseColorTexture)
            ? baseColorTexture
            : null;
    }

    private static bool ShouldRenderTransparentViewport(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        CanonicalTexture? baseColorTexture,
        string? selectedSlot)
    {
        if (string.IsNullOrWhiteSpace(selectedSlot))
        {
            return SelectDefaultViewportOpacityTexture(material, baseColorTexture) is not null;
        }

        var scopedAlphaSlot = !string.IsNullOrWhiteSpace(material?.AlphaTextureSlot) &&
                              string.Equals(material.AlphaTextureSlot, selectedSlot, StringComparison.OrdinalIgnoreCase)
            ? material.AlphaTextureSlot
            : null;
        return SelectOpacityTexture(textures, baseColorTexture, scopedAlphaSlot) is not null;
    }

    private static CanonicalTexture? SelectOpacityTexture(IReadOnlyList<CanonicalTexture> textures, CanonicalTexture? baseColorTexture, string? explicitAlphaSlot)
    {
        if (textures.Count == 0)
        {
            return TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null;
        }

        var scope = new CanonicalMaterial("Scoped", textures, AlphaTextureSlot: explicitAlphaSlot);
        return SelectOpacityTexture(scope, baseColorTexture);
    }

    private static bool ShouldUseBaseColorAlphaAsOpacityFallback(CanonicalMaterial material, CanonicalTexture? baseColorTexture)
    {
        if (!TextureSupportsAlpha(baseColorTexture))
        {
            return false;
        }

        // Portable fallback-diffuse approximations are useful as color, but their alpha channel
        // is not reliable enough to drive lit preview transparency unless the material explicitly
        // named an alpha/opacity slot.
        if (material.SourceKind == CanonicalMaterialSourceKind.FallbackCandidate)
        {
            return false;
        }

        return true;
    }

    private static CanonicalTexture? SelectEmissiveTexture(CanonicalMaterial? material, CanonicalTexture? baseColorTexture, CanonicalTexture? layeredColorTexture)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Emissive)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("emiss", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("glow", StringComparison.OrdinalIgnoreCase))
            ?? (material.LayeredTextureSlots?.Any(slot => slot.Equals("emissive", StringComparison.OrdinalIgnoreCase)) == true
                ? baseColorTexture
                : null);
    }

    private static CanonicalTexture? SelectEmissiveTexture(IReadOnlyList<CanonicalTexture> textures, CanonicalTexture? baseColorTexture, CanonicalTexture? layeredColorTexture)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectEmissiveTexture(scope, baseColorTexture, layeredColorTexture);
    }

    private static CanonicalTexture? SelectNormalTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Normal)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("normal", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("bump", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectNormalTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectNormalTexture(scope);
    }

    private static CanonicalTexture? SelectSpecularTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Specular)
            ?? material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Gloss)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("spec", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("gloss", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("rough", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("smooth", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectSpecularTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectSpecularTexture(scope);
    }

    private static CanonicalTexture? SelectColorShiftMaskTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        return textures.FirstOrDefault(static texture => texture.Slot.Equals("color_shift_mask", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectLayeredColorTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        if (material.LayeredTextureSlots is { Count: > 0 })
        {
            foreach (var preferredSlot in material.LayeredTextureSlots)
            {
                var explicitSlotMatch = material.Textures.FirstOrDefault(texture =>
                    texture.Slot.Equals(preferredSlot, StringComparison.OrdinalIgnoreCase));
                if (explicitSlotMatch is not null)
                {
                    return explicitSlotMatch;
                }
            }
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Overlay)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("overlay", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("detail", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("decal", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("dirt", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("grime", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectLayeredColorTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectLayeredColorTexture(scope);
    }

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
