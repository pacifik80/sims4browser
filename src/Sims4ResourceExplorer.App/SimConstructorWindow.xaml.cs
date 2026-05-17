using System;
using System.IO;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sims4ResourceExplorer.App.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace Sims4ResourceExplorer.App;

public sealed partial class SimConstructorWindow : Window
{
    private AppWindow? appWindow;

    public SimConstructorWindow(SimConstructorViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        if (Content is FrameworkElement root)
        {
            root.DataContext = viewModel;
        }
        Activated += OnActivated;
        ConfigureWindow();
    }

    public SimConstructorViewModel ViewModel { get; }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        CenterOnScreen();
    }

    private void SectionNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
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
