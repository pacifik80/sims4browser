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
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Sims4ResourceExplorer.App;

public sealed partial class MainWindow : Window
{
    private const string AppTitleBase = "Sims4 Resource Explorer";
    private readonly Viewport3DX sceneViewport;
    private readonly PerspectiveCamera sceneCamera;
    private readonly DefaultEffectsManager effectsManager = new();
    private CancellationTokenSource? uvPreviewRenderCancellation;
    private long previewSurfaceGeneration;
    private bool isDraggingMainSplitter;
    private double mainSplitterStartX;
    private double mainSplitterStartResultsWidth;
    private double mainSplitterTotalResizableWidth;
    private bool isDraggingPreviewSplitter;
    private double previewSplitterStartY;
    private double previewSplitterStartHeight;

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
            Visibility = Visibility.Collapsed
        };
        PreviewSurface.Children.Insert(0, sceneViewport);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Activated += MainWindow_Activated;
        TryApplyWindowIcon();
    }

    public MainViewModel ViewModel { get; }

    private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= MainWindow_Activated;
        await ViewModel.InitializeAsync();
    }

    private async void AddGameSource_Click(object sender, RoutedEventArgs e) => await AddFolderAsync(SourceKind.Game);
    private async void AddDlcSource_Click(object sender, RoutedEventArgs e) => await AddFolderAsync(SourceKind.Dlc);
    private async void AddModsSource_Click(object sender, RoutedEventArgs e) => await AddFolderAsync(SourceKind.Mods);
    private async void Index_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new IndexingDialog(new IndexingDialogViewModel(ViewModel.SelectedWorkerCount, ViewModel.CancelIndexing));
        dialog.Activate();
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
            _ => Enumerable.Empty<string>()
        };

        var text = sender.Text?.Trim() ?? string.Empty;
        var filtered = string.IsNullOrWhiteSpace(text)
            ? source
            : source.Where(value => value.Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();

        sender.ItemsSource = filtered;
        sender.IsSuggestionListOpen = filtered.Any() && (!userInputOnly || !string.Equals(text, "All", StringComparison.OrdinalIgnoreCase));
    }

    private async Task AddFolderAsync(SourceKind kind)
    {
        var path = await PickFolderPathAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            await ViewModel.AddSourceAsync(path, kind);
        }
    }

    private async Task<string?> PickFolderPathAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
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

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentScene) ||
            e.PropertyName == nameof(MainViewModel.PreviewImageSource) ||
            e.PropertyName == nameof(MainViewModel.PreviewSurfaceMode) ||
            e.PropertyName == nameof(MainViewModel.SelectedSceneRenderMode))
        {
            UpdatePreviewSurface();
        }
    }

    private async void UpdatePreviewSurface()
    {
        var generation = Interlocked.Increment(ref previewSurfaceGeneration);
        uvPreviewRenderCancellation?.Cancel();
        uvPreviewRenderCancellation = null;

        if (ViewModel.IsScenePreviewActive && ViewModel.CurrentScene is not null)
        {
            if (ViewModel.SelectedSceneRenderMode == SceneRenderMode.UvMap)
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
                        ViewModel.SelectedSceneRenderMode == SceneRenderMode.UvMap)
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
            case SceneRenderMode.UvMap:
                sceneViewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.White });
                break;
            case SceneRenderMode.FlatTexture:
                sceneViewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.Black });
                break;
            default:
                sceneViewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.DarkGray });
                sceneViewport.Items.Add(new DirectionalLight3D { Direction = new Vector3(-0.55f, -1f, -0.35f), Color = Microsoft.UI.Colors.White });
                sceneViewport.Items.Add(new DirectionalLight3D { Direction = new Vector3(0.65f, -0.2f, 0.45f), Color = Microsoft.UI.Colors.LightGray });
                sceneViewport.Items.Add(new DirectionalLight3D { Direction = new Vector3(0.1f, 0.5f, -1f), Color = Microsoft.UI.Colors.Gray });
                break;
        }

        for (var meshIndex = 0; meshIndex < scene.Meshes.Count; meshIndex++)
        {
            var mesh = scene.Meshes[meshIndex];
            var material = CreateMaterial(scene, mesh.MaterialIndex, renderMode);
            var geometry = CreateGeometry(mesh, scene, mesh.MaterialIndex);
            if (geometry.Positions is null || geometry.Positions.Count == 0 || geometry.TriangleIndices is null || geometry.TriangleIndices.Count == 0)
            {
                continue;
            }

            sceneViewport.Items.Add(new MeshGeometryModel3D
            {
                Geometry = geometry,
                Material = material,
                IsTransparent = IsTransparentMaterial(scene, mesh.MaterialIndex),
                CullMode = SharpDX.Direct3D11.CullMode.None,
                RenderWireframe = renderMode == SceneRenderMode.Wireframe,
                WireframeColor = Microsoft.UI.Colors.Yellow
            });
        }

        ResetSceneCamera(scene);
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

    private static MeshGeometry3D CreateGeometry(CanonicalMesh mesh, CanonicalScene scene, int materialIndex)
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

        var textureCoordinates = SelectTextureCoordinates(mesh, scene, materialIndex);
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

    private static IReadOnlyList<float> SelectTextureCoordinates(CanonicalMesh mesh, CanonicalScene scene, int materialIndex)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var diffuseTexture = SelectBaseColorTexture(material);
        var requestedChannel = diffuseTexture?.UvChannel ?? mesh.PreferredUvChannel;
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

        if (diffuseTexture is null ||
            (Math.Abs(diffuseTexture.UvScaleU - 1f) < 0.0001f &&
             Math.Abs(diffuseTexture.UvScaleV - 1f) < 0.0001f &&
             Math.Abs(diffuseTexture.UvOffsetU) < 0.0001f &&
             Math.Abs(diffuseTexture.UvOffsetV) < 0.0001f))
        {
            return coordinates;
        }

        if (requestedChannel == 1)
        {
            coordinates = NormalizeUvCoordinatesIfLocalSubspace(coordinates);
        }

        var transformed = new float[coordinates.Count];
        var minU = Math.Min(diffuseTexture.UvOffsetU, diffuseTexture.UvOffsetU + diffuseTexture.UvScaleU);
        var maxU = Math.Max(diffuseTexture.UvOffsetU, diffuseTexture.UvOffsetU + diffuseTexture.UvScaleU);
        var minV = Math.Min(diffuseTexture.UvOffsetV, diffuseTexture.UvOffsetV + diffuseTexture.UvScaleV);
        var maxV = Math.Max(diffuseTexture.UvOffsetV, diffuseTexture.UvOffsetV + diffuseTexture.UvScaleV);
        for (var index = 0; index + 1 < coordinates.Count; index += 2)
        {
            var transformedU = (coordinates[index] * diffuseTexture.UvScaleU) + diffuseTexture.UvOffsetU;
            var transformedV = (coordinates[index + 1] * diffuseTexture.UvScaleV) + diffuseTexture.UvOffsetV;

            // Atlas-cropped TS4 materials can leave UVs slightly outside the selected sub-rect.
            // Clamp them so preview sampling does not bleed into neighboring atlas islands.
            transformed[index] = Math.Clamp(transformedU, minU, maxU);
            transformed[index + 1] = Math.Clamp(transformedV, minV, maxV);
        }

        return transformed;
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

    private static PhongMaterial CreateMaterial(CanonicalScene scene, int materialIndex, SceneRenderMode renderMode)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var diffuseTexture = SelectBaseColorTexture(material);
        var opacityTexture = SelectOpacityTexture(material, diffuseTexture);

        var isFlat = renderMode == SceneRenderMode.FlatTexture;
        var isWireframe = renderMode == SceneRenderMode.Wireframe;
        var isLit = renderMode == SceneRenderMode.LitTexture;
        var renderDiffuseMap =
            !isWireframe &&
            !isFlat &&
            diffuseTexture is not null;
        var renderEmissiveMap = isFlat && diffuseTexture is not null;
        var renderAlphaMap = opacityTexture is not null;
        var textureModel = diffuseTexture is null
            ? null
            : new TextureModel(new MemoryStream(diffuseTexture.PngBytes), autoCloseStream: true);
        var alphaTextureModel = opacityTexture is null
            ? null
            : new TextureModel(new MemoryStream(opacityTexture.PngBytes), autoCloseStream: true);

        return new PhongMaterial
        {
            DiffuseColor = isWireframe
                ? new Color4(0.95f, 0.95f, 0.95f, 1f)
                : isLit
                    ? new Color4(1f, 1f, 1f, 1f)
                    : new Color4(0f, 0f, 0f, 1f),
            AmbientColor = isFlat
                ? new Color4(0f, 0f, 0f, 1f)
                : isLit
                    ? new Color4(0.12f, 0.12f, 0.12f, 1f)
                    : new Color4(0.2f, 0.2f, 0.2f, 1f),
            EmissiveColor = isFlat
                ? new Color4(1f, 1f, 1f, 1f)
                : new Color4(0f, 0f, 0f, 1f),
            SpecularColor = isFlat ? new Color4(0f, 0f, 0f, 1f) : new Color4(0.16f, 0.16f, 0.16f, 1f),
            SpecularShininess = isFlat ? 0f : 24f,
            RenderDiffuseMap = renderDiffuseMap,
            DiffuseMap = renderDiffuseMap ? textureModel : null,
            RenderEmissiveMap = renderEmissiveMap,
            EmissiveMap = renderEmissiveMap ? textureModel : null,
            RenderDiffuseAlphaMap = renderAlphaMap,
            DiffuseAlphaMap = renderAlphaMap ? alphaTextureModel : null
        };
    }

    private static bool IsTransparentMaterial(CanonicalScene scene, int materialIndex)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var diffuseTexture = SelectBaseColorTexture(material);
        var opacityTexture = SelectOpacityTexture(material, diffuseTexture);
        return material?.IsTransparent == true || opacityTexture is not null;
    }

    private async Task<WriteableBitmap> GenerateUvPreviewAsync(CanonicalScene scene, CancellationToken cancellationToken)
    {
        var textureEntries = scene.Materials
            .Select((material, index) => new
            {
                MaterialIndex = index,
                Texture = SelectBaseColorTexture(material)
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
                var coordinates = SelectTextureCoordinates(meshEntry.Mesh, scene, meshEntry.Mesh.MaterialIndex);
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
            PreviewPaneGrid.RowDefinitions[3].ActualHeight +
            PreviewPaneGrid.RowDefinitions[4].ActualHeight;
        var availableResizableHeight = Math.Max(300, PreviewPaneGrid.ActualHeight - occupiedHeight);
        var minPreviewHeight = Math.Max(PreviewViewportRow.MinHeight, 180);
        var minDetailsHeight = 120d;
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

    private static CanonicalTexture? SelectOpacityTexture(CanonicalMaterial? material, CanonicalTexture? baseColorTexture)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null;
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Opacity)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("overlay", StringComparison.OrdinalIgnoreCase))
            ?? (TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null);
    }

}
