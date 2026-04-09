using System.ComponentModel;
using System.Numerics;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.WinUI.SharpDX;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Sims4ResourceExplorer.App.ViewModels;
using Sims4ResourceExplorer.Core;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Sims4ResourceExplorer.App;

public sealed partial class MainWindow : Window
{
    private readonly Viewport3DX sceneViewport;
    private readonly PerspectiveCamera sceneCamera;
    private readonly DefaultEffectsManager effectsManager = new();

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
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

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentScene) || e.PropertyName == nameof(MainViewModel.PreviewImageSource))
        {
            UpdatePreviewSurface();
        }
    }

    private void UpdatePreviewSurface()
    {
        var scene = ViewModel.CurrentScene;
        if (scene is null)
        {
            sceneViewport.Items.Clear();
            sceneViewport.Visibility = Visibility.Collapsed;
            PreviewImage.Visibility = Visibility.Visible;
            ResetViewButton.IsEnabled = false;
            return;
        }

        RenderScene(scene);
        sceneViewport.Visibility = Visibility.Visible;
        PreviewImage.Visibility = Visibility.Collapsed;
        ResetViewButton.IsEnabled = true;
    }

    private void RenderScene(CanonicalScene scene)
    {
        sceneViewport.Items.Clear();
        sceneViewport.Items.Add(new AmbientLight3D());
        sceneViewport.Items.Add(new DirectionalLight3D { Direction = new Vector3(-0.4f, -1f, -0.3f) });
        sceneViewport.Items.Add(new DirectionalLight3D { Direction = new Vector3(0.3f, -0.4f, 0.5f) });

        for (var meshIndex = 0; meshIndex < scene.Meshes.Count; meshIndex++)
        {
            var mesh = scene.Meshes[meshIndex];
            var material = CreateMaterial(scene, mesh.MaterialIndex);
            var geometry = CreateGeometry(mesh);
            if (geometry.Positions is null || geometry.Positions.Count == 0 || geometry.TriangleIndices is null || geometry.TriangleIndices.Count == 0)
            {
                continue;
            }

            sceneViewport.Items.Add(new MeshGeometryModel3D
            {
                Geometry = geometry,
                Material = material
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

    private static MeshGeometry3D CreateGeometry(CanonicalMesh mesh)
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

        if (mesh.Normals.Count >= 3)
        {
            geometry.Normals = new Vector3Collection();
            for (var index = 0; index + 2 < mesh.Normals.Count; index += 3)
            {
                geometry.Normals.Add(new Vector3(mesh.Normals[index], mesh.Normals[index + 1], mesh.Normals[index + 2]));
            }
        }

        if (mesh.Uvs.Count >= 2)
        {
            geometry.TextureCoordinates = new Vector2Collection();
            for (var index = 0; index + 1 < mesh.Uvs.Count; index += 2)
            {
                geometry.TextureCoordinates.Add(new Vector2(mesh.Uvs[index], 1f - mesh.Uvs[index + 1]));
            }
        }

        foreach (var triangleIndex in mesh.Indices)
        {
            geometry.TriangleIndices.Add(triangleIndex);
        }

        return geometry;
    }

    private static PhongMaterial CreateMaterial(CanonicalScene scene, int materialIndex)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var diffuseTexture = material?.Textures.FirstOrDefault();

        return new PhongMaterial
        {
            DiffuseColor = new Color4(1f, 1f, 1f, 1f),
            AmbientColor = new Color4(0.35f, 0.35f, 0.35f, 1f),
            SpecularColor = new Color4(0.15f, 0.15f, 0.15f, 1f),
            SpecularShininess = 12f,
            RenderDiffuseMap = diffuseTexture is not null,
            DiffuseMap = diffuseTexture is null ? null : new TextureModel(new MemoryStream(diffuseTexture.PngBytes), autoCloseStream: true)
        };
    }

}
