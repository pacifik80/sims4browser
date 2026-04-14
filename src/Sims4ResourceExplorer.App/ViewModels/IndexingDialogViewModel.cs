using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class IndexingDialogViewModel : ObservableObject
{
    private readonly Action cancelAction;

    public IndexingDialogViewModel(int configuredWorkerCount, Action cancelAction)
    {
        this.cancelAction = cancelAction;
        ConfiguredWorkerCount = IndexingRunOptions.ClampWorkerCount(configuredWorkerCount);

        for (var workerId = 1; workerId <= ConfiguredWorkerCount; workerId++)
        {
            Workers.Add(new WorkerSlotItemViewModel(workerId));
        }
    }

    public ObservableCollection<WorkerSlotItemViewModel> Workers { get; } = [];
    public ObservableCollection<IndexActivityItemViewModel> RecentEvents { get; } = [];

    public int ConfiguredWorkerCount { get; }

    [ObservableProperty]
    private double packagesProgressMaximum = 1;

    [ObservableProperty]
    private double packagesProgressValue;

    [ObservableProperty]
    private bool packagesProgressIndeterminate;

    [ObservableProperty]
    private string overallSummary = "Preparing indexing run.";

    [ObservableProperty]
    private string latestEventText = "Waiting for first indexing event.";

    [ObservableProperty]
    private string workerCountText = "Workers: 0";

    [ObservableProperty]
    private string backlogText = "Queue: 0";

    [ObservableProperty]
    private string workerStatesText = "Worker states: 0 waiting | 0 active | 0 idle | 0 failed";

    [ObservableProperty]
    private string persistBacklogText = "Persist queue: 0";

    [ObservableProperty]
    private string writerTelemetryText = "Writer: idle | busy 0% | batch 0";

    [ObservableProperty]
    private string packagesText = "Packages: 0 / 0";

    [ObservableProperty]
    private string resourcesText = "Resources: 0";

    [ObservableProperty]
    private string dataProgressText = "Data: 0 B / 0 B";

    [ObservableProperty]
    private string elapsedText = "Elapsed: 00:00:00";

    [ObservableProperty]
    private string elapsedEtaText = "Elapsed / Remaining ETA: 00:00:00 / --:--:--";

    [ObservableProperty]
    private string throughputText = "Throughput: 0 res/sec";

    [ObservableProperty]
    private string summaryText = "Waiting to start.";

    [ObservableProperty]
    private bool canCancel = true;

    [ObservableProperty]
    private bool canClose;

    public void ApplyProgress(IndexingProgress progress)
    {
        var useByteProgress = progress.DiscoveryCompleted && progress.PackageBytesTotal > 0;
        PackagesProgressMaximum = useByteProgress ? Math.Max(1, progress.PackageBytesTotal) : Math.Max(1, progress.PackagesTotal);
        PackagesProgressValue = useByteProgress
            ? Math.Min(progress.PackageBytesProcessed, progress.PackageBytesTotal)
            : Math.Min(progress.PackagesProcessed, progress.PackagesTotal);
        PackagesProgressIndeterminate = !progress.DiscoveryCompleted;
        OverallSummary = progress.DiscoveryCompleted
            ? $"Packages {progress.PackagesProcessed}/{progress.PackagesTotal} | Data {IndexingDisplayFormat.FormatBytes(progress.PackageBytesProcessed)} / {IndexingDisplayFormat.FormatBytes(progress.PackageBytesTotal)} | Resources {progress.ResourcesProcessed:N0} | Throughput {IndexingDisplayFormat.FormatRate(progress.OverallThroughput)} res/sec"
            : $"Packages {progress.PackagesProcessed} processed / {progress.PackagesTotal} discovered so far | Data {IndexingDisplayFormat.FormatBytes(progress.PackageBytesProcessed)} / {IndexingDisplayFormat.FormatBytes(progress.PackageBytesTotal)} | Resources {progress.ResourcesProcessed:N0} | Throughput {IndexingDisplayFormat.FormatRate(progress.OverallThroughput)} res/sec";
        LatestEventText = string.IsNullOrWhiteSpace(progress.Message)
            ? "Monitoring indexing run."
            : progress.Message;
        WorkerCountText = $"Workers: {progress.ActiveWorkerCount} active / {Math.Max(progress.ConfiguredWorkerCount, ConfiguredWorkerCount)} configured";
        WorkerStatesText = $"Worker states: {progress.WaitingWorkerCount} waiting | {progress.ActiveWorkerCount} active | {progress.IdleWorkerCount} idle | {progress.FailedWorkerCount} failed";
        BacklogText = $"Scan queue: {progress.PendingPackageCount}";
        PersistBacklogText = $"Persist queue: {progress.PendingPersistCount}";
        WriterTelemetryText = $"Writer: {(progress.WriterBusy ? "busy" : "idle")} | busy {progress.WriterBusyPercent:0}% | batch {progress.ActiveWriterBatchCount}";
        PackagesText = progress.DiscoveryCompleted
            ? $"Packages: {progress.PackagesProcessed} / {progress.PackagesTotal}"
            : $"Packages: {progress.PackagesProcessed} processed / {progress.PackagesTotal} discovered so far";
        ResourcesText = $"Resources: {progress.ResourcesProcessed:N0}";
        DataProgressText = $"Data: {IndexingDisplayFormat.FormatBytes(progress.PackageBytesProcessed)} / {IndexingDisplayFormat.FormatBytes(progress.PackageBytesTotal)}";
        ElapsedText = $"Elapsed: {progress.Elapsed:hh\\:mm\\:ss}";
        ElapsedEtaText = $"Elapsed / Remaining ETA: {progress.Elapsed:hh\\:mm\\:ss} / {BuildEtaText(progress)}";
        ThroughputText = $"Throughput: {IndexingDisplayFormat.FormatRate(progress.OverallThroughput)} res/sec";
        SummaryText = BuildSummary(progress);

        ApplyWorkerSlots(progress.WorkerSlots);
        if (progress.Summary is not null || string.Equals(progress.Stage, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            CanCancel = false;
            CanClose = true;
        }
    }

    public void RequestCancel()
    {
        if (CanCancel)
        {
            cancelAction();
        }
    }

    public void MarkCompleted(string? message = null)
    {
        LatestEventText = string.IsNullOrWhiteSpace(message) ? "Indexing completed." : message;
        SummaryText = string.IsNullOrWhiteSpace(message) ? "Indexing completed." : message;
        CanCancel = false;
        CanClose = true;
    }

    public void MarkCanceled(string? message = null)
    {
        LatestEventText = string.IsNullOrWhiteSpace(message) ? "Indexing was canceled." : message;
        SummaryText = string.IsNullOrWhiteSpace(message) ? "Indexing was canceled." : message;
        CanCancel = false;
        CanClose = true;
    }

    public void MarkFailed(string message)
    {
        LatestEventText = message;
        SummaryText = message;
        CanCancel = false;
        CanClose = true;
    }

    private void ApplyWorkerSlots(IReadOnlyList<WorkerSlotProgress>? workerSlots)
    {
        var slots = workerSlots ?? [];
        for (var index = 0; index < Workers.Count; index++)
        {
            var slot = slots.FirstOrDefault(item => item.WorkerId == index + 1);
            Workers[index].Apply(slot);
        }
    }

    private static string BuildSummary(IndexingProgress progress)
    {
        if (progress.Summary is not null)
        {
            return $"Completed in {progress.Summary.TotalElapsed:hh\\:mm\\:ss}. Failed: {progress.Summary.PackagesFailed}. Skipped: {progress.Summary.PackagesSkipped}.";
        }

        if (string.Equals(progress.Stage, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            return "Indexing was canceled.";
        }

        return progress.DiscoveryCompleted
            ? $"{progress.PackagesProcessed}/{progress.PackagesTotal} packages processed."
            : $"Streaming discovery is still active. {progress.PackagesProcessed} packages processed and {progress.PackagesTotal} discovered so far.";
    }

    private static TimeSpan EstimateRemainingEta(IndexingProgress progress)
    {
        if (progress.Summary is not null)
        {
            return TimeSpan.Zero;
        }

        if (progress.DiscoveryCompleted && progress.PackageBytesTotal > 0)
        {
            var remainingBytes = Math.Max(0, progress.PackageBytesTotal - progress.PackageBytesProcessed);
            if (remainingBytes > 0 && progress.Elapsed.TotalSeconds > 0 && progress.PackageBytesProcessed > 0)
            {
                var byteRate = progress.PackageBytesProcessed / progress.Elapsed.TotalSeconds;
                if (byteRate > 0)
                {
                    return TimeSpan.FromSeconds(remainingBytes / byteRate);
                }
            }
        }

        if (!progress.DiscoveryCompleted)
        {
            return TimeSpan.Zero;
        }

        var activeRemainingResources = (progress.WorkerSlots ?? [])
            .Where(static slot => slot.Status == WorkerSlotStatus.Active)
            .Sum(static slot => Math.Max(0, slot.ResourcesDiscovered - slot.ResourcesProcessed));
        if (progress.OverallThroughput > 0)
        {
            var completedPackages = Math.Max(0, progress.PackagesCompleted);
            var pendingPackages = Math.Max(0, progress.PendingPackageCount);

            if (completedPackages <= 0)
            {
                return activeRemainingResources > 0
                    ? TimeSpan.FromSeconds(activeRemainingResources / progress.OverallThroughput)
                    : TimeSpan.Zero;
            }

            var avgResourcesPerCompletedPackage = Math.Max(1d, (double)progress.CompletedResourcesProcessed / completedPackages);
            var estimatedPendingResources = pendingPackages * avgResourcesPerCompletedPackage;
            var estimatedRemainingResources = activeRemainingResources + estimatedPendingResources;
            if (estimatedRemainingResources > 0)
            {
                return TimeSpan.FromSeconds(estimatedRemainingResources / progress.OverallThroughput);
            }
        }

        var remainingPackages = Math.Max(0, progress.PackagesTotal - progress.PackagesProcessed);
        if (remainingPackages <= 0 || progress.PackagesProcessed <= 0 || progress.Elapsed.TotalSeconds <= 0)
        {
            return TimeSpan.Zero;
        }

        var packageRate = progress.PackagesProcessed / progress.Elapsed.TotalSeconds;
        return packageRate <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(remainingPackages / packageRate);
    }

    private static string BuildEtaText(IndexingProgress progress) =>
        progress.DiscoveryCompleted
            ? IndexingDisplayFormat.FormatEta(EstimateRemainingEta(progress))
            : "provisional while discovery is active";
}

public sealed partial class WorkerSlotItemViewModel : ObservableObject
{
    public WorkerSlotItemViewModel(int workerId)
    {
        WorkerId = workerId;
        WorkerLabel = $"Worker {workerId}";
        Apply(null);
    }

    public int WorkerId { get; }
    public string WorkerLabel { get; }

    [ObservableProperty]
    private string statusText = "Waiting";

    [ObservableProperty]
    private string packageNameText = "No package assigned";

    [ObservableProperty]
    private string stageText = "waiting for work";

    [ObservableProperty]
    private string processedText = "Processed: 0 / 0";

    [ObservableProperty]
    private string writtenText = "Written: 0";

    [ObservableProperty]
    private string elapsedText = "Elapsed: 00:00:00";

    [ObservableProperty]
    private string throughputText = "Throughput: 0 res/sec";

    [ObservableProperty]
    private string statusBadgeText = "Waiting";

    [ObservableProperty]
    private string etaText = "Remaining ETA: --:--:--";

    public void Apply(WorkerSlotProgress? progress)
    {
        if (progress is null)
        {
            StatusText = "Waiting";
            StatusBadgeText = "Waiting";
            PackageNameText = "No package assigned";
            StageText = "waiting for work";
            ProcessedText = "Processed: 0 / 0";
            WrittenText = "Written: 0";
            ElapsedText = "Elapsed: 00:00:00";
            ThroughputText = "Throughput: 0 res/sec";
            EtaText = "Remaining ETA: --:--:--";
            return;
        }

        StatusText = progress.Status.ToString();
        StatusBadgeText = progress.Status switch
        {
            WorkerSlotStatus.Active => "Active",
            WorkerSlotStatus.Failed => "Failed",
            WorkerSlotStatus.Idle => "Idle",
            _ => "Waiting"
        };
        PackageNameText = progress.PackageName ?? "No package assigned";
        StageText = string.IsNullOrWhiteSpace(progress.FailureReason)
            ? progress.Stage
            : $"{progress.Stage}: {progress.FailureReason}";
        ProcessedText = $"Processed: {progress.ResourcesProcessed:N0} / {Math.Max(progress.ResourcesDiscovered, progress.ResourcesProcessed):N0}";
        WrittenText = $"Written: {progress.ResourcesWritten:N0}";
        ElapsedText = $"Elapsed: {progress.Elapsed:hh\\:mm\\:ss}";
        ThroughputText = $"Rate: {IndexingDisplayFormat.FormatRate(progress.Throughput)}/sec";
        EtaText = $"ETA: {IndexingDisplayFormat.FormatEta(EstimatePackageRemainingEta(progress))}";
    }

    private static TimeSpan EstimatePackageRemainingEta(WorkerSlotProgress progress)
    {
        if (progress.Status != WorkerSlotStatus.Active || progress.ResourcesDiscovered <= progress.ResourcesProcessed)
        {
            return TimeSpan.Zero;
        }

        var remaining = progress.ResourcesDiscovered - progress.ResourcesProcessed;
        var throughput = progress.Throughput;
        if (throughput <= 0 && progress.Elapsed.TotalSeconds > 0 && progress.ResourcesProcessed > 0)
        {
            throughput = progress.ResourcesProcessed / progress.Elapsed.TotalSeconds;
        }

        return throughput <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(remaining / throughput);
    }
}

internal static class IndexingDisplayFormat
{
    public static string FormatRate(double rate) =>
        rate switch
        {
            <= 0 => "0",
            < 10 => rate.ToString("0.0"),
            _ => rate.ToString("0")
        };

    public static string FormatBytes(long bytes)
    {
        const double kib = 1024d;
        const double mib = kib * 1024d;
        const double gib = mib * 1024d;

        return bytes switch
        {
            <= 0 => "0 B",
            < 1024L => $"{bytes:N0} B",
            < 1024L * 1024L => $"{bytes / kib:0.0} KiB",
            < 1024L * 1024L * 1024L => $"{bytes / mib:0.0} MiB",
            _ => $"{bytes / gib:0.00} GiB"
        };
    }

    public static string FormatEta(TimeSpan eta) =>
        eta <= TimeSpan.Zero ? "--:--:--" : eta.ToString(@"hh\:mm\:ss");
}

public sealed class IndexActivityItemViewModel
{
    public IndexActivityItemViewModel(IndexingActivityEvent activityEvent)
    {
        TimestampText = activityEvent.Timestamp.ToString("HH:mm:ss");
        KindText = activityEvent.Kind;
        Message = activityEvent.Message;
    }

    public string TimestampText { get; }
    public string KindText { get; }
    public string Message { get; }
}
