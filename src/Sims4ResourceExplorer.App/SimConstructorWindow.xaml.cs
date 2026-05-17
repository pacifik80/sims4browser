using System;
using System.ComponentModel;
using System.IO;
using System.Numerics;
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.WinUI.SharpDX;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sims4ResourceExplorer.App.Services;
using Sims4ResourceExplorer.App.ViewModels;
using Sims4ResourceExplorer.Core;
using Windows.Graphics;
using WinRT.Interop;

namespace Sims4ResourceExplorer.App;

public sealed partial class SimConstructorWindow : Window
{
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
    private readonly SceneViewportRenderer sceneRenderer = new();
    private AppWindow? appWindow;
    // First render reframes the camera to fit the scene bounds; every subsequent
    // render preserves the user's current rotation/zoom so a skintone (or any other
    // knob) change doesn't snap the view back to default.
    private bool hasRenderedScene;

    public SimConstructorWindow(SimConstructorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        if (Content is FrameworkElement root)
        {
            root.DataContext = viewModel;
        }
        sceneCamera = new PerspectiveCamera
        {
            Position = new Vector3(0, 1, 4),
            LookDirection = new Vector3(0, 0, -4),
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
        Activated += OnActivated;
        ConfigureWindow();
    }

    public SimConstructorViewModel ViewModel { get; }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        CenterOnScreen();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SimConstructorViewModel.CurrentScene) or nameof(SimConstructorViewModel.SelectedRenderMode))
        {
            UpdateViewport();
        }
    }

    private void UpdateViewport()
    {
        var scene = ViewModel.CurrentScene;
        if (scene is null)
        {
            sceneViewport.Items.Clear();
            sceneViewport.Visibility = Visibility.Collapsed;
            return;
        }

        sceneViewport.Visibility = Visibility.Visible;
        var config = new SceneRenderConfig(
            ViewModel.SelectedRenderMode,
            TextureSlot: null,
            UvChannel: SceneUvChannelOverride.Auto,
            Variant: null);
        try
        {
            sceneRenderer.Render(sceneViewport, sceneCamera, sceneShadowMap, scene, config, resetCamera: !hasRenderedScene);
            hasRenderedScene = true;
        }
        catch (Exception ex)
        {
            sceneViewport.Items.Clear();
            sceneViewport.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine($"SimConstructorWindow render failed: {ex}");
        }
    }

    private void SkintonePickerGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        // Close the flyout only on real user clicks — SelectionChanged also fires when the
        // SelectedSkintone binding pushes a value during flyout open, and slamming Hide()
        // mid-open leaves the light-dismiss overlay stuck (which silently captures all
        // subsequent clicks on the right panel). ItemClick never fires from a binding push.
        try
        {
            SkintonePickerFlyout?.Hide();
        }
        catch
        {
        }
    }

    private void SectionNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // NavigationView SelectionChanged can fire during InitializeComponent if the
        // IsSelected="True" item is materialised before KnobSectionTitle/Stub later in the
        // markup. Same defensive guard as anywhere else in this window's init path.
        if (KnobSectionTitle is null || KnobSectionStub is null)
        {
            return;
        }
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string sectionTag)
        {
            KnobSectionTitle.Text = sectionTag;
            KnobSectionStub.Text = sectionTag switch
            {
                "Genetics" => "Skintone, body and face morph sliders land in P1. CAS part pickers (hair, top, bottom, shoes, accessories, makeup) land in P2. Idle clip controls land in P3.",
                "Outfits" => "Hair, top, bottom, shoes, accessories, and makeup pickers will appear here once P2 lands.",
                "Animation" => "Idle clip picker and playback controls will appear here once P3 lands.",
                _ => string.Empty
            };
        }
    }

    private void ConfigureWindow()
    {
        try
        {
            appWindow = GetAppWindow();
            appWindow.Title = "Sim Character Constructor";

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMinimizable = true;
                presenter.IsMaximizable = true;
            }

            appWindow.Resize(new SizeInt32(1600, 1000));

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

    private void CenterOnScreen()
    {
        try
        {
            var window = appWindow ?? GetAppWindow();
            var displayArea = DisplayArea.GetFromWindowId(window.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            var x = workArea.X + Math.Max(0, (workArea.Width - window.Size.Width) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - window.Size.Height) / 2);
            window.Move(new PointInt32(x, y));
        }
        catch
        {
        }
    }

    private AppWindow GetAppWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }
}
