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
using Sims4ResourceExplorer.App.Services;
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
    private readonly SceneViewportRenderer sceneRenderer = new();
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
    private void ResetView_Click(object sender, RoutedEventArgs e) => sceneRenderer.ResetSceneCamera(sceneCamera, ViewModel.CurrentScene);
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
        var config = new SceneRenderConfig(
            ViewModel.SelectedSceneRenderMode,
            GetSelectedSceneTextureSlot(),
            ViewModel.SelectedSceneUvChannel,
            ViewModel.SelectedSceneVariant);
        sceneRenderer.Render(sceneViewport, sceneCamera, sceneShadowMap, scene, config);
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

    private async Task<IReadOnlyList<UvPreviewPanel>> GenerateUvPreviewPanelsAsync(CanonicalScene scene, CancellationToken cancellationToken)
    {
        var renderMode = ViewModel.SelectedSceneRenderMode;
        var selectedSlot = GetSelectedSceneTextureSlot();
        var uvChannelOverride = GetSceneUvChannelOverride();
        scene = SceneViewportRenderer.ApplySelectedVariantToScene(scene, ViewModel.SelectedSceneVariant);
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
                    .OrderByDescending(texture => SceneViewportRenderer.ScorePrimaryViewportTexture(texture, renderMode))
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
                    var coordinates = SceneViewportRenderer.SelectTextureCoordinates(meshEntry.Mesh, textureEntry.Texture, renderMode, uvChannelOverride);
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
        var selection = SceneViewportRenderer.BuildViewportTextureSelection(material, renderMode, selectedSlot);
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
            SceneViewportRenderer.BuildTextureSamplingKey(texture));
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
