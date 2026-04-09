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
    private string overallSummary = "Preparing indexing run.";

    [ObservableProperty]
    private string latestEventText = "Waiting for first indexing event.";

    [ObservableProperty]
    private string workerCountText = "Workers: 0";

    [ObservableProperty]
    private string backlogText = "Queue: 0";

    [ObservableProperty]
    private string packagesText = "Packages: 0 / 0";

    [ObservableProperty]
    private string resourcesText = "Resources: 0";

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
        PackagesProgressMaximum = Math.Max(1, progress.PackagesTotal);
        PackagesProgressValue = Math.Min(progress.PackagesProcessed, progress.PackagesTotal);
        OverallSummary = $"Packages {progress.PackagesProcessed}/{progress.PackagesTotal} | Resources {progress.ResourcesProcessed:N0} | Throughput {IndexingDisplayFormat.FormatRate(progress.OverallThroughput)} res/sec";
        LatestEventText = string.IsNullOrWhiteSpace(progress.Message)
            ? "Monitoring indexing run."
            : progress.Message;
        WorkerCountText = $"Workers: {progress.ActiveWorkerCount} active / {Math.Max(progress.ConfiguredWorkerCount, ConfiguredWorkerCount)} configured";
        BacklogText = $"Queue: {progress.PendingPackageCount}";
        PackagesText = $"Packages: {progress.PackagesProcessed} / {progress.PackagesTotal}";
        ResourcesText = $"Resources: {progress.ResourcesProcessed:N0}";
        ElapsedText = $"Elapsed: {progress.Elapsed:hh\\:mm\\:ss}";
        ElapsedEtaText = $"Elapsed / Remaining ETA: {progress.Elapsed:hh\\:mm\\:ss} / {IndexingDisplayFormat.FormatEta(EstimateRemainingEta(progress))}";
        ThroughputText = $"Throughput: {IndexingDisplayFormat.FormatRate(progress.OverallThroughput)} res/sec";
        SummaryText = BuildSummary(progress);

        ApplyWorkerSlots(progress.WorkerSlots);
        ApplyRecentEvents(progress.RecentEvents);

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

    private void ApplyWorkerSlots(IReadOnlyList<WorkerSlotProgress>? workerSlots)
    {
        var slots = workerSlots ?? [];
        for (var index = 0; index < Workers.Count; index++)
        {
            var slot = slots.FirstOrDefault(item => item.WorkerId == index + 1);
            Workers[index].Apply(slot);
        }
    }

    private void ApplyRecentEvents(IReadOnlyList<IndexingActivityEvent>? events)
    {
        RecentEvents.Clear();
        foreach (var activityEvent in (events ?? []).Reverse())
        {
            RecentEvents.Add(new IndexActivityItemViewModel(activityEvent));
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

        return $"{progress.PackagesProcessed}/{progress.PackagesTotal} packages processed.";
    }

    private static TimeSpan EstimateRemainingEta(IndexingProgress progress)
    {
        if (progress.Summary is not null || progress.OverallThroughput <= 0)
        {
            return TimeSpan.Zero;
        }

        var activeRemainingResources = (progress.WorkerSlots ?? [])
            .Where(static slot => slot.Status == WorkerSlotStatus.Active)
            .Sum(static slot => Math.Max(0, slot.ResourcesDiscovered - slot.ResourcesProcessed));
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
        return estimatedRemainingResources <= 0
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(estimatedRemainingResources / progress.OverallThroughput);
    }
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
        if (progress.Status != WorkerSlotStatus.Active || progress.Throughput <= 0 || progress.ResourcesDiscovered <= progress.ResourcesProcessed)
        {
            return TimeSpan.Zero;
        }

        var remaining = progress.ResourcesDiscovered - progress.ResourcesProcessed;
        return TimeSpan.FromSeconds(remaining / progress.Throughput);
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
