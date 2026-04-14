using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Sims4ResourceExplorer.App.Services;
using Sims4ResourceExplorer.App.ViewModels;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Audio;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Export;
using Sims4ResourceExplorer.Indexing;
using Sims4ResourceExplorer.Packages;
using Sims4ResourceExplorer.Preview;

namespace Sims4ResourceExplorer.App;

public partial class App : Application
{
    private readonly IHost host;
    private Window? window;
    private int shutdownRequested;

    public App()
    {
        InitializeComponent();

        host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(IndexingRunOptions.CreateDefault());
                services.AddSingleton<ICacheService, FileSystemCacheService>();
                services.AddSingleton<IAppPreferencesService, JsonAppPreferencesService>();
                services.AddSingleton<IIndexingTelemetryRecorderService, IndexingTelemetryRecorderService>();
                services.AddSingleton<IIndexStore, SqliteIndexStore>();
                services.AddSingleton<IPackageScanner, FileSystemPackageScanner>();
                services.AddSingleton<IResourceCatalogService, LlamaResourceCatalogService>();
                services.AddSingleton<IResourceMetadataEnrichmentService, ResourceMetadataEnrichmentService>();
                services.AddSingleton<IAssetGraphBuilder, ExplicitAssetGraphBuilder>();
                services.AddSingleton<ITextureDecodeService, BasicTextureDecodeService>();
                services.AddSingleton<ISceneBuildService, BuildBuySceneBuildService>();
                services.AddSingleton<IAudioDecodeService, BasicAudioDecodeService>();
                services.AddSingleton<IAudioPlayer, WaveOutAudioPlayer>();
                services.AddSingleton<IPreviewService, ResourcePreviewService>();
                services.AddSingleton<IRawExportService, RawExportService>();
                services.AddSingleton<IFbxExportService, AssimpFbxExportService>();
                services.AddSingleton<PackageIndexCoordinator>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    public static T GetRequiredService<T>() where T : notnull =>
        ((App)Current).host.Services.GetRequiredService<T>();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        window = host.Services.GetRequiredService<MainWindow>();
        window.Closed += OnMainWindowClosed;
        window.Activate();
    }

    private async void OnMainWindowClosed(object sender, WindowEventArgs args)
    {
        if (Interlocked.Exchange(ref shutdownRequested, 1) != 0)
        {
            return;
        }

        try
        {
            if (sender is Window closedWindow)
            {
                closedWindow.Closed -= OnMainWindowClosed;
            }

            await host.StopAsync(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }
        finally
        {
            try
            {
                host.Dispose();
            }
            catch
            {
            }

            Exit();
        }
    }
}
