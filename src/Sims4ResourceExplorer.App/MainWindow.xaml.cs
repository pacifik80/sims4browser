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
using Sims4ResourceExplorer.Preview;
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
    private bool isPanningUvPreview;
    private Windows.Foundation.Point uvPanStartPosition;
    private double uvPanStartHorizontalOffset;
    private double uvPanStartVerticalOffset;
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
        var preview = ViewModel.PreviewText ?? string.Empty;
        var details = ViewModel.DetailsText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(preview) && string.IsNullOrWhiteSpace(details))
        {
            return;
        }

        var combined = string.IsNullOrWhiteSpace(details)
            ? preview
            : string.IsNullOrWhiteSpace(preview)
                ? details
                : $"=== Preview ==={Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}=== Details ==={Environment.NewLine}{details}";
        var package = new DataPackage();
        package.SetText(combined);
        Clipboard.SetContent(package);
        ViewModel.StatusMessage = "Copied diagnostics (Preview + Details) to clipboard.";
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
        if (ViewModel.IsScenePreviewActive && ViewModel.CurrentScene is not null)
        {
            return await CaptureSceneViewportD3DAsync();
        }

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

    /// <summary>
    /// Reads back the current rendered frame from the D3D11 render target used by the
    /// HelixToolkit viewport (a SwapChain composition surface, invisible to RenderTargetBitmap).
    /// Gets the RenderTargetView from IRenderHost, resolves MSAA if needed, copies to a
    /// CPU-readable staging texture, maps it, and encodes as PNG.
    /// </summary>
    private async Task<RandomAccessStreamReference?> CaptureSceneViewportD3DAsync()
    {
        try
        {
            var renderHost = sceneViewport.RenderHost;
            if (renderHost is null)
            {
                return null;
            }

            if (renderHost.Device is not SharpDX.Direct3D11.Device device)
            {
                return null;
            }

            if (renderHost.RenderTargetBufferView is not SharpDX.Direct3D11.RenderTargetView rtv)
            {
                return null;
            }

            using var colorTex = rtv.ResourceAs<SharpDX.Direct3D11.Texture2D>();
            var srcDesc = colorTex.Description;
            var width = srcDesc.Width;
            var height = srcDesc.Height;

            var stagingDesc = new SharpDX.Direct3D11.Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = srcDesc.Format,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = SharpDX.Direct3D11.ResourceUsage.Staging,
                BindFlags = SharpDX.Direct3D11.BindFlags.None,
                CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.Read,
                OptionFlags = SharpDX.Direct3D11.ResourceOptionFlags.None,
            };

            var ctx = device.ImmediateContext;
            using var staging = new SharpDX.Direct3D11.Texture2D(device, stagingDesc);

            if (srcDesc.SampleDescription.Count > 1)
            {
                // MSAA: resolve to non-MSAA intermediate first, then copy to staging.
                var resolveDesc = stagingDesc;
                resolveDesc.Usage = SharpDX.Direct3D11.ResourceUsage.Default;
                resolveDesc.CpuAccessFlags = SharpDX.Direct3D11.CpuAccessFlags.None;
                using var resolved = new SharpDX.Direct3D11.Texture2D(device, resolveDesc);
                ctx.ResolveSubresource(colorTex, 0, resolved, 0, srcDesc.Format);
                ctx.CopyResource(resolved, staging);
            }
            else
            {
                ctx.CopyResource(colorTex, staging);
            }

            byte[] pixels;
            var box = ctx.MapSubresource(staging, 0, SharpDX.Direct3D11.MapMode.Read, SharpDX.Direct3D11.MapFlags.None);
            try
            {
                pixels = new byte[width * height * 4];
                for (var row = 0; row < height; row++)
                {
                    var src = box.DataPointer + row * box.RowPitch;
                    System.Runtime.InteropServices.Marshal.Copy(src, pixels, row * width * 4, width * 4);
                }
            }
            finally
            {
                ctx.UnmapSubresource(staging, 0);
            }

            var pixelFormat = srcDesc.Format is SharpDX.DXGI.Format.R8G8B8A8_UNorm or SharpDX.DXGI.Format.R8G8B8A8_UNorm_SRgb
                ? BitmapPixelFormat.Rgba8
                : BitmapPixelFormat.Bgra8;

            var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(pixelFormat, BitmapAlphaMode.Premultiplied, (uint)width, (uint)height, 96, 96, pixels);
            await encoder.FlushAsync();
            stream.Seek(0);
            return RandomAccessStreamReference.CreateFromStream(stream);
        }
        catch
        {
            return null;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentScene) ||
            e.PropertyName == nameof(MainViewModel.PreviewImageSource) ||
            e.PropertyName == nameof(MainViewModel.PreviewSurfaceMode) ||
            e.PropertyName == nameof(MainViewModel.SelectedSceneRenderMode) ||
            e.PropertyName == nameof(MainViewModel.SelectedSceneTextureSlot) ||
            e.PropertyName == nameof(MainViewModel.SelectedSceneUvChannel) ||
            e.PropertyName == nameof(MainViewModel.SelectedSceneVariant))
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
                    UvPreviewScroll.Visibility = Visibility.Visible;
                    UvPreviewHost.ItemsSource = null;
                    UpdateUvPreviewHostWidth();
                    var cancellation = new CancellationTokenSource();
                    uvPreviewRenderCancellation = cancellation;
                    try
                    {
                        var uvPanels = await GenerateUvPreviewPanelsAsync(ViewModel.CurrentScene, cancellation.Token);
                        if (!cancellation.IsCancellationRequested &&
                            generation == Interlocked.Read(ref previewSurfaceGeneration) &&
                            ViewModel.IsScenePreviewActive &&
                            ViewModel.CurrentScene is not null &&
                            ViewModel.SelectedSceneRenderMode is SceneRenderMode.RawUv or SceneRenderMode.MaterialUv)
                        {
                            UvPreviewHost.ItemsSource = uvPanels;
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
                UvPreviewScroll.Visibility = Visibility.Collapsed;
                UvPreviewHost.ItemsSource = null;
                return;
            }

            sceneViewport.Items.Clear();
            sceneViewport.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = ViewModel.IsImagePreviewActive ? Visibility.Visible : Visibility.Collapsed;
            UvPreviewScroll.Visibility = Visibility.Collapsed;
            UvPreviewHost.ItemsSource = null;
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
        scene = ApplySelectedVariantToScene(scene, ViewModel.SelectedSceneVariant);
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

        var uvChannelOverride = GetSceneUvChannelOverride();
        for (var meshIndex = 0; meshIndex < scene.Meshes.Count; meshIndex++)
        {
            var mesh = scene.Meshes[meshIndex];
            var selectedSlot = GetSelectedSceneTextureSlot();
            var canonicalMaterial = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.Materials.Count
                ? scene.Materials[mesh.MaterialIndex]
                : null;
            var multiPassPlan = TryBuildMultiPassPlan(canonicalMaterial, renderMode);
            var deferOverlayToSeparatePass = multiPassPlan is not null;
            var material = CreateMaterial(scene, mesh.MaterialIndex, renderMode, selectedSlot, deferOverlayToSeparatePass);
            var geometry = CreateGeometry(mesh, scene, mesh.MaterialIndex, renderMode, selectedSlot, uvChannelOverride);
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

            if (multiPassPlan is not null)
            {
                AddOverlayPasses(mesh, multiPassPlan, renderMode, uvChannelOverride);
            }
        }

        ResetSceneCamera(scene);
    }

    private static MaterialPlan? TryBuildMultiPassPlan(CanonicalMaterial? material, SceneRenderMode renderMode)
    {
        if (material is null)
        {
            return null;
        }

        if (renderMode is not SceneRenderMode.LitTexture and not SceneRenderMode.FlatTexture)
        {
            return null;
        }

        var renderable = RenderableMaterialFactory.FromCanonical(material);
        var family = renderable.NormalizedFamily;
        var familyIsMultiPassCapable =
            family.StartsWith("colorMap", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("DecalMap", StringComparison.OrdinalIgnoreCase);
        if (!familyIsMultiPassCapable)
        {
            return null;
        }

        var plan = MaterialApplierRegistry.Apply(renderable, material.Textures);
        if (plan.PassCount <= 1)
        {
            return null;
        }

        var hasOverlayLayer = plan.Layers.Any(layer =>
            layer.Pass > 0 &&
            layer.Texture is not null &&
            layer.Role is RenderableLayerRole.Overlay or RenderableLayerRole.Decal);
        return hasOverlayLayer ? plan : null;
    }

    private void AddOverlayPasses(CanonicalMesh mesh, MaterialPlan plan, SceneRenderMode renderMode, int? uvChannelOverride)
    {
        for (var pass = 1; pass < plan.PassCount; pass++)
        {
            var primary = plan.LayersInPass(pass)
                .FirstOrDefault(layer =>
                    layer.Texture is not null &&
                    layer.Role is RenderableLayerRole.Overlay or RenderableLayerRole.Decal);
            if (primary is null)
            {
                continue;
            }

            var passMaterial = BuildOverlayPassMaterial(primary);
            var passGeometry = BuildOverlayPassGeometry(mesh, primary.Texture!, renderMode, uvChannelOverride);
            if (passGeometry.Positions is null || passGeometry.Positions.Count == 0 ||
                passGeometry.TriangleIndices is null || passGeometry.TriangleIndices.Count == 0)
            {
                continue;
            }

            sceneViewport.Items.Add(new MeshGeometryModel3D
            {
                Geometry = passGeometry,
                Material = passMaterial,
                IsTransparent = true,
                CullMode = SharpDX.Direct3D11.CullMode.None
            });
        }
    }

    private static PhongMaterial BuildOverlayPassMaterial(AppliedLayer layer)
    {
        var textureModel = layer.Texture is null
            ? null
            : new TextureModel(new MemoryStream(layer.Texture.PngBytes), autoCloseStream: true);
        var uvTransform = BuildUvTransform(layer.Texture);
        return new PhongMaterial
        {
            DiffuseColor = new Color4(1f, 1f, 1f, 1f),
            AmbientColor = new Color4(0f, 0f, 0f, 1f),
            EmissiveColor = new Color4(0f, 0f, 0f, 1f),
            SpecularColor = new Color4(0f, 0f, 0f, 1f),
            SpecularShininess = 0f,
            RenderDiffuseMap = textureModel is not null,
            DiffuseMap = textureModel,
            RenderDiffuseAlphaMap = false,
            UVTransform = uvTransform
        };
    }

    private static MeshGeometry3D BuildOverlayPassGeometry(CanonicalMesh mesh, CanonicalTexture layerTexture, SceneRenderMode renderMode, int? uvChannelOverride = null)
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

        var textureCoordinates = SelectTextureCoordinates(mesh, layerTexture, renderMode, uvChannelOverride);
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

    private void ShowPreviewFailureDiagnostics(string message, Exception ex)
    {
        uvPreviewRenderCancellation?.Cancel();
        uvPreviewRenderCancellation = null;
        sceneViewport.Items.Clear();
        sceneViewport.Visibility = Visibility.Collapsed;
        PreviewImage.Visibility = Visibility.Collapsed;
        UvPreviewScroll.Visibility = Visibility.Collapsed;
        UvPreviewHost.ItemsSource = null;

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

    private int? GetSceneUvChannelOverride() => ViewModel.SelectedSceneUvChannel switch
    {
        SceneUvChannelOverride.Uv0 => 0,
        SceneUvChannelOverride.Uv1 => 1,
        _ => null
    };

    private static CanonicalScene ApplySelectedVariantToScene(CanonicalScene scene, SceneVariantOption? selected)
    {
        if (selected is null || scene.Materials.Count == 0)
        {
            return scene;
        }

        var rewritten = new List<CanonicalMaterial>(scene.Materials.Count);
        var changed = false;
        foreach (var material in scene.Materials)
        {
            if (material.Variants is { Count: > 0 })
            {
                var match = material.Variants.FirstOrDefault(variant => variant.StateNameHash == selected.StateNameHash);
                if (match is not null && !match.IsDefault)
                {
                    rewritten.Add(material with { Textures = match.Textures });
                    changed = true;
                    continue;
                }
            }
            rewritten.Add(material);
        }

        return changed ? scene with { Materials = rewritten } : scene;
    }

    private static MeshGeometry3D CreateGeometry(CanonicalMesh mesh, CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot, int? uvChannelOverride = null)
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

        var textureCoordinates = SelectTextureCoordinates(mesh, scene, materialIndex, renderMode, selectedSlot, uvChannelOverride);
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

    private static IReadOnlyList<float> SelectTextureCoordinates(CanonicalMesh mesh, CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot, int? uvChannelOverride = null)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var primaryTexture = SelectPrimaryViewportTexture(material, renderMode, selectedSlot);
        return SelectTextureCoordinates(mesh, primaryTexture, renderMode, uvChannelOverride);
    }

    private static IReadOnlyList<float> SelectTextureCoordinates(CanonicalMesh mesh, CanonicalTexture? primaryTexture, SceneRenderMode renderMode, int? uvChannelOverride = null)
    {
        var hasExplicitTextureUvDirective = HasExplicitTextureUvDirective(primaryTexture);
        var hasConfirmedTextureUvDirective = hasExplicitTextureUvDirective &&
                                            primaryTexture is not null &&
                                            !primaryTexture.IsApproximateUvTransform;
        var requestedChannel = uvChannelOverride is { } forcedChannel
            ? forcedChannel
            : hasConfirmedTextureUvDirective
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
        return BuildViewportTextureSelection(material, renderMode, selectedSlot).ViewportColorTexture;
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

    private static Material CreateMaterial(CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot) =>
        CreateMaterial(scene, materialIndex, renderMode, selectedSlot, deferOverlayToSeparatePass: false);

    private static Material CreateMaterial(CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot, bool deferOverlayToSeparatePass)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var textureSelection = BuildViewportTextureSelection(material, renderMode, selectedSlot);
        if (deferOverlayToSeparatePass && textureSelection.LayeredColorTexture is not null)
        {
            // Overlay layer is going to be drawn as a separate sibling pass, so suppress its
            // appearances on the base pass: drop the fake-emissive promotion and prefer the
            // genuine base color texture as the viewport diffuse.
            var suppressedEmissive = textureSelection.EmissiveTexture is not null &&
                                     ReferenceEquals(textureSelection.EmissiveTexture, textureSelection.LayeredColorTexture)
                ? null
                : textureSelection.EmissiveTexture;
            var rerouted = ReferenceEquals(textureSelection.ViewportColorTexture, textureSelection.LayeredColorTexture) &&
                           textureSelection.BaseColorTexture is not null
                ? textureSelection.BaseColorTexture
                : textureSelection.ViewportColorTexture;
            textureSelection = textureSelection with
            {
                EmissiveTexture = suppressedEmissive,
                ViewportColorTexture = rerouted,
                LayeredColorTexture = null
            };
        }
        var viewportColorTexture = textureSelection.ViewportColorTexture;
        var uvTransform = BuildUvTransform(textureSelection.ViewportColorTexture);
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
        var renderEmissiveMap = (isFlat && viewportColorTexture is not null) || (!isFlat && textureSelection.EmissiveTexture is not null);
        var renderAlphaMap = textureSelection.RenderAlphaMap;
        var forceOpaqueViewportTexture = isLit && !renderAlphaMap;
        var (textureModel, usesSwatchComposite) = CreateViewportTextureModel(
            material,
            viewportColorTexture,
            textureSelection.ColorShiftMaskTexture,
            viewportTintColor,
            forceOpaqueViewportTexture);
        var emissiveTextureModel = textureSelection.EmissiveTexture is null
            ? null
            : new TextureModel(new MemoryStream(textureSelection.EmissiveTexture.PngBytes), autoCloseStream: true);
        var alphaTextureModel = textureSelection.OpacityTexture is null
            ? null
            : new TextureModel(new MemoryStream(textureSelection.OpacityTexture.PngBytes), autoCloseStream: true);
        var normalTextureModel = textureSelection.NormalTexture is null
            ? null
            : new TextureModel(new MemoryStream(textureSelection.NormalTexture.PngBytes), autoCloseStream: true);
        var specularTextureModel = textureSelection.SpecularTexture is null
            ? null
            : new TextureModel(new MemoryStream(textureSelection.SpecularTexture.PngBytes), autoCloseStream: true);
        var usesOverlayAsSecondaryLayer =
            textureSelection.EmissiveTexture is not null &&
            textureSelection.LayeredColorTexture is not null &&
            ReferenceEquals(textureSelection.EmissiveTexture, textureSelection.LayeredColorTexture);
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
        // EXCEPTION: skin-target materials carry a neutral skin diffuse intended to be tinted by
        // the routed skintone (ViewportTintColor). Three cases qualify for PBR skin rendering:
        //  - True SimSkin / SimSkinMask shader-family materials, recognised by shader name.
        //  - ApproximateCas materials with a non-null ViewportTintColor (set by
        //    `SimSceneComposer.ApplySkintoneRouteToMaterial`). These come out of the Sim assembly
        //    path with `shader=(unknown)`, so the shader-name detector misses them.
        //  - ApproximateCas materials that have been atlas-rewritten by RewriteSkintoneRoutedMaterial.
        //    After atlas injection ViewportTintColor is cleared to prevent swatch masking, but the
        //    material still needs PBR rendering (AlbedoColor = white; atlas carries all skin colour).
        var isSimSkinFamily = material is not null &&
                              RenderableMaterialFactory.IsSimSkinFamily(material.ShaderName);
        var isApproximateCasSkintoneTarget = material is not null &&
                                              material.SourceKind == CanonicalMaterialSourceKind.ApproximateCas &&
                                              (material.ViewportTintColor is not null ||
                                               material.Textures.Any(static t =>
                                                   t.Semantic == CanonicalTextureSemantic.BaseColor &&
                                                   string.Equals(t.FileName, "skin_atlas.png", StringComparison.OrdinalIgnoreCase)));
        var needsViewportTintMultiply = isSimSkinFamily || isApproximateCasSkintoneTarget;
        var effectiveTexturedLitDiffuseColor =
            needsViewportTintMultiply &&
            !usesSwatchComposite &&
            viewportTintColor is { } skintone
                ? skintone
                : new Color4(1f, 1f, 1f, 1f);

        // SimSkin and skin-targeted ApproximateCas materials use HelixToolkit's built-in PBR
        // (GGX Cook-Torrance BRDF) in lit mode. PBR has physically-based specular response and
        // handles multi-directional lighting more accurately than Phong for skin atlases that were
        // designed for a PBR-lit engine. The pre-baked eye-socket / nose / lip contouring in the
        // atlas is also more naturally integrated by GGX than by Phong's sharp cos-power specular.
        if (isLit && renderDiffuseMap && (isSimSkinFamily || isApproximateCasSkintoneTarget))
        {
            return new PBRMaterial
            {
                AlbedoColor = effectiveTexturedLitDiffuseColor,
                EmissiveColor = new Color4(0f, 0f, 0f, 1f),
                MetallicFactor = 0.0f,      // skin is fully dielectric
                RoughnessFactor = 0.85f,    // skin is diffuse-dominant, low specular lobe
                ReflectanceFactor = 0.04f,  // ~4% F0 is the standard value for human skin
                AmbientOcclusionFactor = 1.0f,
                AlbedoMap = textureModel,
                NormalMap = normalTextureModel,
                UVTransform = uvTransform,
                RenderShadowMap = true,
                EnableAutoTangent = normalTextureModel is not null,
            };
        }

        // SimGlass: GEOM-side glass family (eyes, contact lenses, visors).
        // Very low roughness + slightly elevated Fresnel models the glass IOR (~1.5 → F0 ≈ 4%).
        // Shadow casting is suppressed — transparent glass shouldn't project hard opaque shadows.
        if (isLit && renderDiffuseMap && RenderableMaterialFactory.IsSimGlassFamily(material?.ShaderName))
        {
            return new PBRMaterial
            {
                AlbedoColor = effectiveTexturedLitDiffuseColor,
                EmissiveColor = new Color4(0f, 0f, 0f, 1f),
                MetallicFactor = 0.0f,
                RoughnessFactor = 0.05f,    // near-perfect specular glass surface
                ReflectanceFactor = 0.08f,  // IOR ~1.5 → F0 ≈ 4–8%
                AmbientOcclusionFactor = 1.0f,
                AlbedoMap = textureModel,
                NormalMap = normalTextureModel,
                UVTransform = uvTransform,
                RenderShadowMap = false,
                EnableAutoTangent = normalTextureModel is not null,
            };
        }

        // StandardSurface: OpenPBR-aligned family used by newer TS4 packs.
        // Generic dielectric PBR parameters; metallic/roughness varies by asset but these
        // defaults (slightly rough, non-metallic) cover most manufactured surface types.
        if (isLit && renderDiffuseMap && RenderableMaterialFactory.IsStandardSurfaceFamily(material?.ShaderName))
        {
            return new PBRMaterial
            {
                AlbedoColor = effectiveTexturedLitDiffuseColor,
                EmissiveColor = new Color4(0f, 0f, 0f, 1f),
                MetallicFactor = 0.05f,     // mostly dielectric; slight factor for variety
                RoughnessFactor = 0.60f,    // medium roughness covers most manufactured surfaces
                ReflectanceFactor = 0.04f,  // standard dielectric F0
                AmbientOcclusionFactor = 1.0f,
                AlbedoMap = textureModel,
                NormalMap = normalTextureModel,
                UVTransform = uvTransform,
                RenderShadowMap = true,
                EnableAutoTangent = normalTextureModel is not null,
            };
        }

        // RefractionMap / SpecularEnvMap: refraction and env-mapped surfaces (pool water,
        // decorative glass, mirrors). Very smooth, slightly elevated Fresnel, no opaque shadows.
        if (isLit && renderDiffuseMap && RenderableMaterialFactory.IsRefractionFamily(material?.ShaderName))
        {
            return new PBRMaterial
            {
                AlbedoColor = effectiveTexturedLitDiffuseColor,
                EmissiveColor = new Color4(0f, 0f, 0f, 1f),
                MetallicFactor = 0.0f,
                RoughnessFactor = 0.08f,    // mostly smooth refractive or reflective surface
                ReflectanceFactor = 0.08f,  // elevated Fresnel for glass/water interfaces
                AmbientOcclusionFactor = 1.0f,
                AlbedoMap = textureModel,
                NormalMap = normalTextureModel,
                UVTransform = uvTransform,
                RenderShadowMap = false,
                EnableAutoTangent = normalTextureModel is not null,
            };
        }

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

    private readonly record struct ViewportTextureSelection(
        IReadOnlyList<CanonicalTexture> TextureGroup,
        CanonicalTexture? BaseColorTexture,
        CanonicalTexture? LayeredColorTexture,
        CanonicalTexture? EmissiveTexture,
        CanonicalTexture? OpacityTexture,
        CanonicalTexture? NormalTexture,
        CanonicalTexture? SpecularTexture,
        CanonicalTexture? ColorShiftMaskTexture,
        CanonicalTexture? ViewportColorTexture,
        bool RenderAlphaMap);

    private static ViewportTextureSelection BuildViewportTextureSelection(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
    {
        var textureGroup = SelectViewportTextureGroup(material, renderMode, selectedSlot);
        var baseColorTexture = SelectBaseColorTexture(textureGroup);
        var layeredColorTexture = SelectLayeredColorTexture(material, textureGroup);
        var emissiveTexture = SelectEmissiveTexture(textureGroup, baseColorTexture, layeredColorTexture);
        var normalTexture = SelectNormalTexture(textureGroup);
        var specularTexture = SelectSpecularTexture(textureGroup);
        var colorShiftMaskTexture = SelectColorShiftMaskTexture(textureGroup);
        var selectedSlotTexture = !string.IsNullOrWhiteSpace(selectedSlot)
            ? textureGroup.FirstOrDefault(texture => texture.Slot.Equals(selectedSlot, StringComparison.OrdinalIgnoreCase))
            : null;
        var viewportColorTexture = selectedSlotTexture ?? layeredColorTexture ?? baseColorTexture ?? emissiveTexture;
        var opacityTexture = SelectViewportOpacityTexture(material, textureGroup, baseColorTexture, viewportColorTexture, selectedSlot);
        var renderAlphaMap = ShouldRenderTransparentViewport(material, textureGroup, baseColorTexture, viewportColorTexture, selectedSlot);

        return new ViewportTextureSelection(
            textureGroup,
            baseColorTexture,
            layeredColorTexture,
            emissiveTexture,
            opacityTexture,
            normalTexture,
            specularTexture,
            colorShiftMaskTexture,
            viewportColorTexture,
            renderAlphaMap);
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
        return BuildViewportTextureSelection(material, SceneRenderMode.LitTexture, selectedSlot).RenderAlphaMap;
    }

    private async Task<IReadOnlyList<UvPreviewPanel>> GenerateUvPreviewPanelsAsync(CanonicalScene scene, CancellationToken cancellationToken)
    {
        var renderMode = ViewModel.SelectedSceneRenderMode;
        var selectedSlot = GetSelectedSceneTextureSlot();
        var uvChannelOverride = GetSceneUvChannelOverride();
        scene = ApplySelectedVariantToScene(scene, ViewModel.SelectedSceneVariant);
        var textureEntries = scene.Materials
            .SelectMany((material, index) => SelectUvPreviewTextures(material, renderMode, selectedSlot)
                .Select(texture => new UvPreviewTextureEntry(texture, index)))
            .GroupBy(
                entry => BuildUvPreviewTextureKey(entry.Texture),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Texture = group
                    .Select(entry => entry.Texture)
                    .OrderByDescending(texture => ScorePrimaryViewportTexture(texture, renderMode))
                    .ThenBy(texture => texture.Slot, StringComparer.OrdinalIgnoreCase)
                    .First(),
                Entries = group
                    .Distinct()
                    .ToArray()
            })
            .ToArray();

        if (textureEntries.Length == 0)
        {
            return Array.Empty<UvPreviewPanel>();
        }

        const int panelMaxWidth = 1024;
        const int panelMaxHeight = 1024;
        var colors = new uint[] { 0xFFFF4FD1, 0xFFFFFF3B, 0xFFFF7F50, 0xFF7CFC00, 0xFFFF69B4, 0xFF87CEFA };

        var panels = new List<UvPreviewPanel>();
        foreach (var entry in textureEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decoded = await DecodePngAsync(entry.Texture.PngBytes, cancellationToken);
            var scale = MathF.Min(1f, MathF.Min(panelMaxWidth / (float)decoded.Width, panelMaxHeight / (float)decoded.Height));
            var width = Math.Max(1, (int)MathF.Round(decoded.Width * scale));
            var height = Math.Max(1, (int)MathF.Round(decoded.Height * scale));

            var canvas = new byte[width * height * 4];
            BlitScaledBgra(canvas, width, height, decoded.Pixels, decoded.Width, decoded.Height, width, height, 0, 0);

            foreach (var textureEntry in entry.Entries)
            {
                var meshes = scene.Meshes
                    .Select((mesh, meshIndex) => new { Mesh = mesh, MeshIndex = meshIndex })
                    .Where(meshGroup => meshGroup.Mesh.MaterialIndex == textureEntry.MaterialIndex)
                    .ToArray();
                foreach (var meshEntry in meshes)
                {
                    var coordinates = SelectTextureCoordinates(meshEntry.Mesh, textureEntry.Texture, renderMode, uvChannelOverride);
                    var color = colors[meshEntry.MeshIndex % colors.Length];
                    DrawUvWireframe(canvas, width, height, coordinates, meshEntry.Mesh.Indices, 0, 0, width, height, color);
                }
            }

            var bitmap = await CreateWriteableBitmapAsync(width, height, canvas, cancellationToken);
            panels.Add(new UvPreviewPanel
            {
                Label = BuildUvPanelLabel(entry.Texture, decoded.Width, decoded.Height),
                Image = bitmap,
            });
        }

        return panels;
    }

    private static string BuildUvPanelLabel(CanonicalTexture texture, int sourceWidth, int sourceHeight)
    {
        var slot = string.IsNullOrWhiteSpace(texture.Slot) ? "(no slot)" : texture.Slot;
        var dims = $"{sourceWidth}×{sourceHeight}";
        return texture.Semantic == CanonicalTextureSemantic.Unknown
            ? $"{slot}  •  {dims}"
            : $"{slot}  •  {texture.Semantic}  •  {dims}";
    }

    private readonly record struct UvPreviewTextureEntry(CanonicalTexture Texture, int MaterialIndex);

    private static IReadOnlyList<CanonicalTexture> SelectUvPreviewTextures(CanonicalMaterial material, SceneRenderMode renderMode, string? selectedSlot)
    {
        var selection = BuildViewportTextureSelection(material, renderMode, selectedSlot);
        if (selection.TextureGroup.Count == 0)
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(selectedSlot))
        {
            return selection.TextureGroup;
        }

        return selection.ViewportColorTexture is null ? [] : [selection.ViewportColorTexture];
    }

    private static string BuildUvPreviewTextureKey(CanonicalTexture texture)
    {
        var sourceKey = texture.SourceKey is { } key
            ? $"{key.Type:X8}:{key.Group:X8}:{key.FullInstance:X16}"
            : $"{texture.FileName}|{Convert.ToHexString(SHA256.HashData(texture.PngBytes))}";
        return string.Join(
            '|',
            sourceKey,
            texture.Slot,
            texture.Semantic,
            BuildTextureSamplingKey(texture));
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

    // UV preview surface — interaction model mirrors the 3D viewport:
    //   wheel       = zoom (no modifier required)
    //   click-drag  = pan (any mouse button)
    // We constrain the inner ItemsRepeater width to the ScrollViewer's viewport
    // so UniformGridLayout actually wraps into rows × columns. Zooming scales
    // the whole content (ScrollViewer's intrinsic ZoomFactor); panning is
    // implemented manually because ScrollViewer's built-in pan is touch-only.

    private void UvPreviewScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateUvPreviewHostWidth();
    }

    private void UpdateUvPreviewHostWidth()
    {
        var available = UvPreviewScroll.ViewportWidth;
        if (available <= 0)
        {
            available = UvPreviewScroll.ActualWidth;
        }

        // Subtract ItemsRepeater margin (12 each side) so item area fits without overflow.
        var width = Math.Max(0, available - 24);
        if (!double.IsFinite(width) || width <= 0)
        {
            UvPreviewHost.ClearValue(FrameworkElement.WidthProperty);
            return;
        }

        UvPreviewHost.Width = width;
    }

    private void UvPreviewScroll_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(UvPreviewScroll).Properties;
        var delta = properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        var current = UvPreviewScroll.ZoomFactor;
        var step = delta > 0 ? 1.15f : 1f / 1.15f;
        var next = Math.Clamp(current * step, UvPreviewScroll.MinZoomFactor, UvPreviewScroll.MaxZoomFactor);
        if (Math.Abs(next - current) < 0.001f)
        {
            e.Handled = true;
            return;
        }

        // Keep the cursor anchored to the same content point across the zoom step.
        var pointer = e.GetCurrentPoint(UvPreviewScroll).Position;
        var ratio = next / current;
        var newHorizontalOffset = ((UvPreviewScroll.HorizontalOffset + pointer.X) * ratio) - pointer.X;
        var newVerticalOffset = ((UvPreviewScroll.VerticalOffset + pointer.Y) * ratio) - pointer.Y;
        UvPreviewScroll.ChangeView(newHorizontalOffset, newVerticalOffset, next, true);
        e.Handled = true;
    }

    private void UvPreviewScroll_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(UvPreviewScroll);
        if (point.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Mouse)
        {
            return;
        }

        isPanningUvPreview = true;
        uvPanStartPosition = point.Position;
        uvPanStartHorizontalOffset = UvPreviewScroll.HorizontalOffset;
        uvPanStartVerticalOffset = UvPreviewScroll.VerticalOffset;
        UvPreviewScroll.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void UvPreviewScroll_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isPanningUvPreview)
        {
            return;
        }

        var position = e.GetCurrentPoint(UvPreviewScroll).Position;
        var dx = position.X - uvPanStartPosition.X;
        var dy = position.Y - uvPanStartPosition.Y;
        UvPreviewScroll.ChangeView(uvPanStartHorizontalOffset - dx, uvPanStartVerticalOffset - dy, null, true);
    }

    private void UvPreviewScroll_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!isPanningUvPreview)
        {
            return;
        }

        isPanningUvPreview = false;
        UvPreviewScroll.ReleasePointerCapture(e.Pointer);
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
        CanonicalTexture? viewportColorTexture,
        string? selectedSlot)
    {
        if (TextureSupportsOwnAlphaAsOpacity(viewportColorTexture, baseColorTexture, selectedSlot))
        {
            return viewportColorTexture;
        }

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
        CanonicalTexture? viewportColorTexture,
        string? selectedSlot)
    {
        if (TextureSupportsOwnAlphaAsOpacity(viewportColorTexture, baseColorTexture, selectedSlot))
        {
            return true;
        }

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

    private static bool TextureSupportsOwnAlphaAsOpacity(
        CanonicalTexture? viewportColorTexture,
        CanonicalTexture? baseColorTexture,
        string? selectedSlot)
    {
        if (viewportColorTexture is null || !TextureSupportsAlpha(viewportColorTexture))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selectedSlot))
        {
            return true;
        }

        return !ReferenceEquals(viewportColorTexture, baseColorTexture) &&
               viewportColorTexture.Semantic is CanonicalTextureSemantic.Overlay or CanonicalTextureSemantic.Emissive;
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

        // Skintone-routed CAS materials (recognized via the structured note set by
        // SimSceneComposer.ApplySkintoneRouteToMaterial) use the diffuse texture's alpha channel
        // as a region/skin mask, NOT as portable opacity. Promoting that alpha to viewport
        // opacity makes the body render transparent wherever the mask is zero (visible as a
        // near-black viewport). Both body-shell materials (now using the base skin texture) and
        // head-shell materials (still using the CASPart diffuse) fall under this rule.
        if (material.SourceKind == CanonicalMaterialSourceKind.ApproximateCas &&
            material.Approximation is { } approximation &&
            approximation.Contains("Sim skintone route", StringComparison.OrdinalIgnoreCase))
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

    private static CanonicalTexture? SelectLayeredColorTexture(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        if (material?.LayeredTextureSlots is { Count: > 0 })
        {
            foreach (var preferredSlot in material.LayeredTextureSlots)
            {
                var explicitSlotMatch = textures.FirstOrDefault(texture =>
                    texture.Slot.Equals(preferredSlot, StringComparison.OrdinalIgnoreCase));
                if (explicitSlotMatch is not null)
                {
                    return explicitSlotMatch;
                }
            }
        }

        return SelectLayeredColorTexture(textures);
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

public sealed class UvPreviewPanel
{
    public string Label { get; init; } = string.Empty;
    public WriteableBitmap Image { get; init; } = default!;
}
