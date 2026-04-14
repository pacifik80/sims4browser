using System.Reflection;
using System.Text.Json;
using System.Threading.Channels;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.Services;

public interface IIndexingTelemetryRecorderService
{
    Task<IndexingTelemetrySession> StartSessionAsync(
        IReadOnlyList<DataSourceDefinition> sources,
        int workerCount,
        CancellationToken cancellationToken);
}

public sealed class IndexingTelemetryRecorderService : IIndexingTelemetryRecorderService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly ICacheService cacheService;

    public IndexingTelemetryRecorderService(ICacheService cacheService)
    {
        this.cacheService = cacheService;
    }

    public async Task<IndexingTelemetrySession> StartSessionAsync(
        IReadOnlyList<DataSourceDefinition> sources,
        int workerCount,
        CancellationToken cancellationToken)
    {
        cacheService.EnsureCreated();

        var telemetryRoot = Path.Combine(cacheService.AppRoot, "Telemetry", "Indexing");
        Directory.CreateDirectory(telemetryRoot);

        var runId = $"indexing_{DateTimeOffset.Now:yyyyMMdd_HHmmss}";
        var sessionDirectory = Path.Combine(telemetryRoot, runId);
        Directory.CreateDirectory(sessionDirectory);

        var metadataPath = Path.Combine(sessionDirectory, "metadata.json");
        var timelinePath = Path.Combine(sessionDirectory, "timeline.jsonl");
        var summaryPath = Path.Combine(sessionDirectory, "summary.json");

        var metadata = new IndexingTelemetryMetadata(
            runId,
            DateTimeOffset.Now,
            GetInformationalVersion(),
            workerCount,
            sources.Select(static source => new IndexingTelemetrySource(source.DisplayName, source.RootPath, source.Kind.ToString(), source.IsEnabled)).ToArray());

        await using (var metadataStream = File.Create(metadataPath))
        {
            await JsonSerializer.SerializeAsync(metadataStream, metadata, JsonOptions, cancellationToken);
        }

        return new IndexingTelemetrySession(timelinePath, summaryPath);
    }

    private static string GetInformationalVersion() =>
        Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
        ?? "unknown";

    private sealed record IndexingTelemetryMetadata(
        string RunId,
        DateTimeOffset StartedAt,
        string Build,
        int WorkerCount,
        IReadOnlyList<IndexingTelemetrySource> Sources);

    private sealed record IndexingTelemetrySource(
        string DisplayName,
        string RootPath,
        string Kind,
        bool IsEnabled);
}

public sealed class IndexingTelemetrySession : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly Channel<string> channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly CancellationTokenSource disposeTokenSource = new();
    private readonly Task writerTask;
    private readonly string timelinePath;
    private readonly string summaryPath;
    private int snapshotCount;
    private IndexingProgress? latestProgress;

    public IndexingTelemetrySession(string timelinePath, string summaryPath)
    {
        this.timelinePath = timelinePath;
        this.summaryPath = summaryPath;
        writerTask = RunWriterAsync(disposeTokenSource.Token);
    }

    public string TimelinePath => timelinePath;
    public string SummaryPath => summaryPath;

    public void Record(IndexingProgress progress)
    {
        latestProgress = progress;
        var snapshot = BuildSnapshot(progress, ++snapshotCount);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        channel.Writer.TryWrite(json);
    }

    public async Task CompleteAsync(string outcome, CancellationToken cancellationToken)
    {
        if (latestProgress is not null)
        {
            var summary = BuildSummary(latestProgress, outcome, snapshotCount);
            await using var stream = File.Create(summaryPath);
            await JsonSerializer.SerializeAsync(stream, summary, JsonOptions, cancellationToken);
        }

        channel.Writer.TryComplete();
        disposeTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
        await writerTask.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        channel.Writer.TryComplete();
        disposeTokenSource.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await writerTask.ConfigureAwait(false);
        }
        catch
        {
        }
        finally
        {
            disposeTokenSource.Dispose();
        }
    }

    private async Task RunWriterAsync(CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            timelinePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 65536,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var writer = new StreamWriter(stream);

        await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await writer.WriteLineAsync(line).ConfigureAwait(false);
        }

        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static IndexingTelemetrySnapshot BuildSnapshot(IndexingProgress progress, int sequence)
    {
        var stageCounts = (progress.WorkerSlots ?? [])
            .Where(static slot => slot.Status == WorkerSlotStatus.Active && !string.IsNullOrWhiteSpace(slot.Stage))
            .GroupBy(static slot => slot.Stage)
            .OrderByDescending(static group => group.Count())
            .ToDictionary(static group => group.Key, static group => group.Count());

        return new IndexingTelemetrySnapshot(
            sequence,
            DateTimeOffset.Now,
            progress.Stage,
            progress.Message,
            progress.Elapsed,
            progress.PackagesProcessed,
            progress.PackagesTotal,
            progress.PackageBytesProcessed,
            progress.PackageBytesTotal,
            progress.CompletedPackageBytesProcessed,
            progress.ResourcesProcessed,
            progress.OverallThroughput,
            progress.ActiveWorkerCount,
            progress.WaitingWorkerCount,
            progress.IdleWorkerCount,
            progress.FailedWorkerCount,
            progress.PendingPackageCount,
            progress.PendingPersistCount,
            progress.WriterBusy,
            progress.WriterBusyPercent,
            progress.ActiveWriterBatchCount,
            stageCounts);
    }

    private static IndexingTelemetrySummary BuildSummary(IndexingProgress progress, string outcome, int snapshotCount) =>
        new(
            outcome,
            DateTimeOffset.Now,
            snapshotCount,
            progress.Elapsed,
            progress.PackagesProcessed,
            progress.PackagesTotal,
            progress.PackageBytesProcessed,
            progress.PackageBytesTotal,
            progress.CompletedPackageBytesProcessed,
            progress.ResourcesProcessed,
            progress.OverallThroughput,
            progress.ActiveWorkerCount,
            progress.WaitingWorkerCount,
            progress.IdleWorkerCount,
            progress.FailedWorkerCount,
            progress.PendingPackageCount,
            progress.PendingPersistCount,
            progress.WriterBusyPercent,
            progress.Summary);

    private sealed record IndexingTelemetrySnapshot(
        int Sequence,
        DateTimeOffset Timestamp,
        string Stage,
        string Message,
        TimeSpan Elapsed,
        int PackagesProcessed,
        int PackagesTotal,
        long PackageBytesProcessed,
        long PackageBytesTotal,
        long CompletedPackageBytesProcessed,
        int ResourcesProcessed,
        double OverallThroughput,
        int ActiveWorkerCount,
        int WaitingWorkerCount,
        int IdleWorkerCount,
        int FailedWorkerCount,
        int PendingPackageCount,
        int PendingPersistCount,
        bool WriterBusy,
        double WriterBusyPercent,
        int ActiveWriterBatchCount,
        IReadOnlyDictionary<string, int> ActiveStageCounts);

    private sealed record IndexingTelemetrySummary(
        string Outcome,
        DateTimeOffset FinishedAt,
        int SnapshotCount,
        TimeSpan Elapsed,
        int PackagesProcessed,
        int PackagesTotal,
        long PackageBytesProcessed,
        long PackageBytesTotal,
        long CompletedPackageBytesProcessed,
        int ResourcesProcessed,
        double OverallThroughput,
        int ActiveWorkerCount,
        int WaitingWorkerCount,
        int IdleWorkerCount,
        int FailedWorkerCount,
        int PendingPackageCount,
        int PendingPersistCount,
        double WriterBusyPercent,
        IndexingRunSummary? RunSummary);
}
