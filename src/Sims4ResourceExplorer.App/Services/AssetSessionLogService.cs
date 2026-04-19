using System.Reflection;
using System.Text;
using System.Text.Json;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.Services;

public interface IAssetSessionLogService
{
    string SessionId { get; }
    string SessionDirectory { get; }
    string LogPath { get; }
    void RecordAssetDiagnostics(AssetSessionLogEntry entry);
}

public sealed record AssetSessionLogEntry(
    DateTimeOffset Timestamp,
    string Trigger,
    string Build,
    string AssetKind,
    string AssetDisplayName,
    string AssetPackagePath,
    string AssetRootTgi,
    string? AssetCategory,
    string? SelectedResourceTgi,
    string? SelectedResourceType,
    string? SelectedResourcePackagePath,
    string? SelectedSceneRootTgi,
    string PreviewSurfaceMode,
    string PreviewSurfaceTitle,
    string? StatusMessage,
    string? SelectedTemplate,
    string PreviewText,
    string DetailsText);

public sealed class AssetSessionLogService : IAssetSessionLogService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly object gate = new();

    public AssetSessionLogService(ICacheService cacheService)
    {
        ArgumentNullException.ThrowIfNull(cacheService);

        cacheService.EnsureCreated();

        var telemetryRoot = Path.Combine(cacheService.AppRoot, "Telemetry", "AssetSessions");
        Directory.CreateDirectory(telemetryRoot);

        SessionId = $"session_{DateTimeOffset.Now:yyyyMMdd_HHmmss}_{Environment.ProcessId}";
        SessionDirectory = Path.Combine(telemetryRoot, SessionId);
        Directory.CreateDirectory(SessionDirectory);

        LogPath = Path.Combine(SessionDirectory, "asset_openings.log");
        var metadataPath = Path.Combine(SessionDirectory, "metadata.json");
        var metadata = new AssetSessionMetadata(
            SessionId,
            DateTimeOffset.Now,
            GetInformationalVersion(),
            Environment.ProcessId,
            Environment.MachineName);
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata, JsonOptions), Encoding.UTF8);
        File.WriteAllText(
            LogPath,
            $"Asset session log started {metadata.StartedAt:O} | build={metadata.Build} | session={SessionId}{Environment.NewLine}{Environment.NewLine}",
            Encoding.UTF8);
    }

    public string SessionId { get; }
    public string SessionDirectory { get; }
    public string LogPath { get; }

    public void RecordAssetDiagnostics(AssetSessionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var builder = new StringBuilder()
            .AppendLine($"=== {entry.Timestamp:O} | {entry.Trigger} ===")
            .AppendLine($"Build: {entry.Build}")
            .AppendLine($"Asset Kind: {entry.AssetKind}")
            .AppendLine($"Asset: {entry.AssetDisplayName}")
            .AppendLine($"Asset Package: {entry.AssetPackagePath}")
            .AppendLine($"Asset Root TGI: {entry.AssetRootTgi}")
            .AppendLine($"Asset Category: {entry.AssetCategory ?? "(none)"}")
            .AppendLine($"Selected Resource TGI: {entry.SelectedResourceTgi ?? "(none)"}")
            .AppendLine($"Selected Resource Type: {entry.SelectedResourceType ?? "(none)"}")
            .AppendLine($"Selected Resource Package: {entry.SelectedResourcePackagePath ?? "(none)"}")
            .AppendLine($"Selected Scene Root TGI: {entry.SelectedSceneRootTgi ?? "(none)"}")
            .AppendLine($"Preview Surface: {entry.PreviewSurfaceMode} | {entry.PreviewSurfaceTitle}")
            .AppendLine($"Status Message: {entry.StatusMessage ?? "(none)"}")
            .AppendLine($"Selected Template: {entry.SelectedTemplate ?? "(none)"}")
            .AppendLine("Preview Text:")
            .AppendLine(string.IsNullOrWhiteSpace(entry.PreviewText) ? "(empty)" : entry.PreviewText)
            .AppendLine()
            .AppendLine("Details Text:")
            .AppendLine(string.IsNullOrWhiteSpace(entry.DetailsText) ? "(empty)" : entry.DetailsText)
            .AppendLine()
            .AppendLine("---")
            .AppendLine();

        lock (gate)
        {
            File.AppendAllText(LogPath, builder.ToString(), Encoding.UTF8);
        }
    }

    private static string GetInformationalVersion() =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? "unknown";

    private sealed record AssetSessionMetadata(
        string SessionId,
        DateTimeOffset StartedAt,
        string Build,
        int ProcessId,
        string MachineName);
}
