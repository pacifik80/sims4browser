using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sims4ResourceExplorer.App.ViewModels;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Graphics;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Sims4ResourceExplorer.App;

public sealed partial class IndexingDialog : Window
{
    private readonly TaskCompletionSource closeCompletionSource = new();
    private bool allowClose;

    public IndexingDialog(IndexingDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        if (Content is FrameworkElement root)
        {
            root.DataContext = viewModel;
        }

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Activated += OnActivated;
        Closed += OnClosed;
        ConfigureWindow();
    }

    public IndexingDialogViewModel ViewModel { get; }

    public Task WaitForCloseAsync() => closeCompletionSource.Task;

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnActivated;
        CenterOnScreen();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.NotifyDialogClosed();
        closeCompletionSource.TrySetResult();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IndexingDialogViewModel.CanClose) && ViewModel.CanClose)
        {
            allowClose = true;
        }
    }

    private void OnPrimaryActionClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsRunStarted)
        {
            ViewModel.RequestStart();
            return;
        }

        if (ViewModel.CanCancel)
        {
            ViewModel.RequestCancel();
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.CanDismiss)
        {
            allowClose = true;
            Close();
        }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderPathAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            ViewModel.AddOrEnableSource(path);
        }
    }

    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Guid sourceId)
        {
            ViewModel.RemoveSource(sourceId);
        }
    }

    private void SourceEnabled_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox && checkBox.Tag is Guid sourceId)
        {
            ViewModel.SetSourceEnabled(sourceId, checkBox.IsChecked == true);
        }
    }

    private void ConfigureWindow()
    {
        try
        {
            var appWindow = GetAppWindow();
            appWindow.Title = "Update Index";

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMinimizable = false;
                presenter.IsMaximizable = true;
            }

            appWindow.Resize(new SizeInt32(1560, 920));
            appWindow.Closing += OnAppWindowClosing;

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

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!allowClose && ViewModel.IsRunStarted && !ViewModel.CanClose)
        {
            args.Cancel = true;
        }
    }

    private void CenterOnScreen()
    {
        try
        {
            var appWindow = GetAppWindow();
            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            var workArea = displayArea.WorkArea;
            var x = workArea.X + Math.Max(0, (workArea.Width - appWindow.Size.Width) / 2);
            var y = workArea.Y + Math.Max(0, (workArea.Height - appWindow.Size.Height) / 2);
            appWindow.Move(new PointInt32(x, y));
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

    private async Task<string?> PickFolderPathAsync()
    {
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch
        {
            return null;
        }
    }
}
