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
    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        if (Content is FrameworkElement root)
        {
            root.DataContext = viewModel;
        }
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
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshActiveTabAsync();
    private async void BrowserTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) => await RefreshActiveTabAsync();
    private async void ResourcesListView_SelectionChanged(object sender, SelectionChangedEventArgs e) => await ViewModel.SelectResourceAsync(ResourcesListView.SelectedItem as ResourceMetadata);
    private async void AssetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e) => await ViewModel.SelectAssetAsync(AssetsListView.SelectedItem as AssetSummary);

    private async void ExportRaw_Click(object sender, RoutedEventArgs e)
    {
        var output = await PickFolderPathAsync();
        if (output is not null)
        {
            await ViewModel.ExportSelectedRawAsync(output);
        }
    }

    private async void PlayAudio_Click(object sender, RoutedEventArgs e) => await ViewModel.PlayAudioAsync();
    private async void StopAudio_Click(object sender, RoutedEventArgs e) => await ViewModel.StopAudioAsync();

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

    private Task RefreshActiveTabAsync() =>
        BrowserTabs.SelectedIndex == 1
            ? ViewModel.RefreshAssetsAsync()
            : ViewModel.RefreshResourcesAsync();

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

}
