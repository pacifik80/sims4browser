using System.Text.Json;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.Services;

public sealed record AppPreferences(int IndexWorkerCount);

public interface IAppPreferencesService
{
    Task<AppPreferences> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken);
}

public sealed class JsonAppPreferencesService : IAppPreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ICacheService cacheService;

    public JsonAppPreferencesService(ICacheService cacheService)
    {
        this.cacheService = cacheService;
    }

    public async Task<AppPreferences> LoadAsync(CancellationToken cancellationToken)
    {
        cacheService.EnsureCreated();
        var path = GetSettingsPath();
        if (!File.Exists(path))
        {
            return new AppPreferences(IndexingRunOptions.GetDefaultWorkerCount());
        }

        await using var stream = File.OpenRead(path);
        var preferences = await JsonSerializer.DeserializeAsync<AppPreferences>(stream, JsonOptions, cancellationToken);
        return preferences is null
            ? new AppPreferences(IndexingRunOptions.GetDefaultWorkerCount())
            : preferences with { IndexWorkerCount = IndexingRunOptions.ClampWorkerCount(preferences.IndexWorkerCount) };
    }

    public async Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken)
    {
        cacheService.EnsureCreated();
        var path = GetSettingsPath();
        var normalized = preferences with { IndexWorkerCount = IndexingRunOptions.ClampWorkerCount(preferences.IndexWorkerCount) };
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, normalized, JsonOptions, cancellationToken);
    }

    private string GetSettingsPath() => Path.Combine(cacheService.AppRoot, "preferences.json");
}
