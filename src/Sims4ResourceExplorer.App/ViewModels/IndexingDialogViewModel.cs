using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.ViewModels;

public sealed partial class IndexingDialogViewModel : ObservableObject
{
    private enum StageCardState
    {
        Pending = 0,
        Active = 1,
        Completed = 2,
        Failed = 3
    }

    private enum UiStage
    {
        Scope = 0,
        Indexing = 1,
        Finalization = 2
    }

    private static readonly Brush PendingStageBackgroundBrush = new SolidColorBrush(Colors.Transparent);
    private static readonly Brush ActiveStageBackgroundBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 245, 204));
    private static readonly Brush CompletedStageBackgroundBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 225, 245, 218));
    private static readonly Brush FailedStageBackgroundBrush = new SolidColorBrush(ColorHelper.FromArgb(255, 251, 223, 223));

    private readonly Action cancelAction;
    private UiStage activeStage = UiStage.Scope;
    private string lastScopeMessage = "Discovering package scope.";
    private string lastIndexingMessage = "Waiting for indexing work to begin.";
    private string lastFinalizationMessage = "Waiting for finalization to begin.";
    private int finalizationStepCurrent;
    private int finalizationStepTotal = 1;

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
    private string stageFlowText = "Stage 1 of 3: defining package scope.";

    [ObservableProperty]
    private string scopeStageStatusText = "Current";

    [ObservableProperty]
    private string indexingStageStatusText = "Next";

    [ObservableProperty]
    private string finalizationStageStatusText = "Next";

    [ObservableProperty]
    private Brush scopeStageBackground = ActiveStageBackgroundBrush;

    [ObservableProperty]
    private Brush indexingStageBackground = PendingStageBackgroundBrush;

    [ObservableProperty]
    private Brush finalizationStageBackground = PendingStageBackgroundBrush;

    [ObservableProperty]
    private Visibility scopeSectionVisibility = Visibility.Visible;

    [ObservableProperty]
    private Visibility indexingSectionVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private Visibility finalizationSectionVisibility = Visibility.Collapsed;

    [ObservableProperty]
    private double scopeProgressMaximum = 1;

    [ObservableProperty]
    private double scopeProgressValue;

    [ObservableProperty]
    private bool scopeProgressIndeterminate = true;

    [ObservableProperty]
    private string scopeSummaryText = "Discovering package scope.";

    [ObservableProperty]
    private string scopePackagesText = "Packages discovered: 0";

    [ObservableProperty]
    private string scopeDataText = "Data discovered: 0 B";

    [ObservableProperty]
    private string scopeElapsedText = "Elapsed: 00:00:00";

    [ObservableProperty]
    private string scopeDetailText = "Waiting to start scope discovery.";

    [ObservableProperty]
    private string overallSummary = "Waiting for indexing work to begin.";

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
    private double finalizationProgressMaximum = 1;

    [ObservableProperty]
    private double finalizationProgressValue;

    [ObservableProperty]
    private bool finalizationProgressIndeterminate = true;

    [ObservableProperty]
    private string finalizationSummaryText = "Waiting for catalog finalization.";

    [ObservableProperty]
    private string finalizationStepText = "Finalization has not started yet.";

    [ObservableProperty]
    private string finalizationProgressText = "Step: waiting";

    [ObservableProperty]
    private string finalizationCatalogText = "Indexed packages: 0 / 0";

    [ObservableProperty]
    private string finalizationResourcesText = "Indexed resources: 0";

    [ObservableProperty]
    private string finalizationElapsedText = "Elapsed: 00:00:00";

    [ObservableProperty]
    private bool canCancel = true;

    [ObservableProperty]
    private bool canClose;

    public void ApplyProgress(IndexingProgress progress)
    {
        if (TryResolveUiStage(progress.Stage, out var stage))
        {
            activeStage = stage;
        }

        UpdateStageFlow(progress);
        UpdateScopeMetrics(progress);
        UpdateIndexingMetrics(progress);
        UpdateFinalizationMetrics(progress);
        ApplyStageVisibility();

        ApplyWorkerSlots(activeStage == UiStage.Indexing ? progress.WorkerSlots : null);
        if (progress.Summary is not null || string.Equals(progress.Stage, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            if (progress.Summary is not null)
            {
                finalizationStepCurrent = finalizationStepTotal;
                FinalizationProgressValue = FinalizationProgressMaximum;
                FinalizationProgressIndeterminate = false;
                FinalizationStepText = "Rebuilt catalog is active.";
            }

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
        StageFlowText = "All 3 stages completed.";
        LatestEventText = string.IsNullOrWhiteSpace(message) ? "Indexing completed." : message;
        SummaryText = string.IsNullOrWhiteSpace(message) ? "Indexing completed." : message;
        ScopeStageStatusText = "Done";
        IndexingStageStatusText = "Done";
        FinalizationStageStatusText = "Done";
        ScopeStageBackground = CompletedStageBackgroundBrush;
        IndexingStageBackground = CompletedStageBackgroundBrush;
        FinalizationStageBackground = CompletedStageBackgroundBrush;
        CanCancel = false;
        CanClose = true;
    }

    public void MarkCanceled(string? message = null)
    {
        StageFlowText = "Indexing was canceled before all stages completed.";
        LatestEventText = string.IsNullOrWhiteSpace(message) ? "Indexing was canceled." : message;
        SummaryText = string.IsNullOrWhiteSpace(message) ? "Indexing was canceled." : message;
        ApplyTerminalStageStatus("Canceled");
        CanCancel = false;
        CanClose = true;
    }

    public void MarkFailed(string message)
    {
        StageFlowText = "Indexing failed before all stages completed.";
        LatestEventText = message;
        SummaryText = message;
        ApplyTerminalStageStatus("Failed");
        CanCancel = false;
        CanClose = true;
    }

    private void UpdateStageFlow(IndexingProgress progress)
    {
        StageFlowText = BuildStageFlowText(progress);
        ScopeStageStatusText = BuildStageStatus(UiStage.Scope, progress);
        IndexingStageStatusText = BuildStageStatus(UiStage.Indexing, progress);
        FinalizationStageStatusText = BuildStageStatus(UiStage.Finalization, progress);
        ScopeStageBackground = GetStageCardBackground(BuildStageCardState(UiStage.Scope, progress));
        IndexingStageBackground = GetStageCardBackground(BuildStageCardState(UiStage.Indexing, progress));
        FinalizationStageBackground = GetStageCardBackground(BuildStageCardState(UiStage.Finalization, progress));
    }

    private void UpdateScopeMetrics(IndexingProgress progress)
    {
        if (activeStage == UiStage.Scope && !string.IsNullOrWhiteSpace(progress.Message))
        {
            lastScopeMessage = progress.Message;
        }

        ScopeProgressMaximum = 1;
        ScopeProgressValue = progress.DiscoveryCompleted ? 1 : 0;
        ScopeProgressIndeterminate = !progress.DiscoveryCompleted;
        ScopeSummaryText = progress.DiscoveryCompleted
            ? "Package scope is locked. Index build can start with stable totals."
            : "Counting packages and total source data before index build starts.";
        ScopePackagesText = $"Packages discovered: {progress.PackagesTotal:N0}";
        ScopeDataText = $"Data discovered: {IndexingDisplayFormat.FormatBytes(progress.PackageBytesTotal)}";
        ScopeElapsedText = $"Elapsed: {progress.Elapsed:hh\\:mm\\:ss}";
        ScopeDetailText = progress.DiscoveryCompleted
            ? $"Scope ready: {progress.PackagesTotal:N0} package(s), {IndexingDisplayFormat.FormatBytes(progress.PackageBytesTotal)}."
            : lastScopeMessage;
    }

    private void UpdateIndexingMetrics(IndexingProgress progress)
    {
        var useByteProgress = progress.PackageBytesTotal > 0;
        PackagesProgressMaximum = useByteProgress ? Math.Max(1, progress.PackageBytesTotal) : Math.Max(1, progress.PackagesTotal);
        PackagesProgressValue = useByteProgress
            ? Math.Min(progress.PackageBytesProcessed, progress.PackageBytesTotal)
            : Math.Min(progress.PackagesProcessed, progress.PackagesTotal);
        PackagesProgressIndeterminate = progress.PackagesTotal <= 0;
        OverallSummary = $"Packages {progress.PackagesProcessed}/{progress.PackagesTotal} | Data {IndexingDisplayFormat.FormatBytes(progress.PackageBytesProcessed)} / {IndexingDisplayFormat.FormatBytes(progress.PackageBytesTotal)} | Resources {progress.ResourcesProcessed:N0} | Throughput {IndexingDisplayFormat.FormatRate(progress.OverallThroughput)} res/sec";
        if (activeStage == UiStage.Indexing && !string.IsNullOrWhiteSpace(progress.Message))
        {
            lastIndexingMessage = progress.Message;
        }

        LatestEventText = lastIndexingMessage;
        WorkerCountText = $"Workers: {progress.ActiveWorkerCount} active / {Math.Max(progress.ConfiguredWorkerCount, ConfiguredWorkerCount)} configured";
        WorkerStatesText = $"Worker states: {progress.WaitingWorkerCount} waiting | {progress.ActiveWorkerCount} active | {progress.IdleWorkerCount} idle | {progress.FailedWorkerCount} failed";
        BacklogText = $"Scan queue: {progress.PendingPackageCount}";
        PersistBacklogText = $"Persist queue: {progress.PendingPersistCount}";
        WriterTelemetryText = $"Writer: {(progress.WriterBusy ? "busy" : "idle")} | busy {progress.WriterBusyPercent:0}% | batch {progress.ActiveWriterBatchCount}";
        PackagesText = $"Packages: {progress.PackagesProcessed:N0} / {progress.PackagesTotal:N0}";
        ResourcesText = $"Resources: {progress.ResourcesProcessed:N0}";
        DataProgressText = $"Data: {IndexingDisplayFormat.FormatBytes(progress.PackageBytesProcessed)} / {IndexingDisplayFormat.FormatBytes(progress.PackageBytesTotal)}";
        ElapsedText = $"Elapsed: {progress.Elapsed:hh\\:mm\\:ss}";
        ElapsedEtaText = $"Elapsed / Remaining ETA: {progress.Elapsed:hh\\:mm\\:ss} / {BuildEtaText(progress)}";
        ThroughputText = $"Throughput: {IndexingDisplayFormat.FormatRate(progress.OverallThroughput)} res/sec";
        SummaryText = BuildIndexingSummary(progress);
    }

    private void UpdateFinalizationMetrics(IndexingProgress progress)
    {
        if (string.Equals(progress.Stage, "finalizing", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(progress.Message) &&
                progress.Message.Contains("Activating rebuilt catalog shards", StringComparison.OrdinalIgnoreCase) &&
                finalizationStepTotal > 1)
            {
                finalizationStepCurrent = finalizationStepTotal;
                lastFinalizationMessage = progress.Message;
            }
            else if (TryParseFinalizationStep(progress.Message, out var stepIndex, out var stepCount, out var stepText))
            {
                finalizationStepCurrent = stepIndex;
                finalizationStepTotal = stepCount;
                lastFinalizationMessage = stepText;
            }
            else if (!string.IsNullOrWhiteSpace(progress.Message))
            {
                lastFinalizationMessage = progress.Message;
            }
        }
        else if (progress.Summary is not null)
        {
            finalizationStepCurrent = Math.Max(1, finalizationStepTotal);
            lastFinalizationMessage = "Rebuilt catalog is active.";
        }

        var indexedPackages = Math.Max(0, progress.PackagesProcessed - progress.PackagesFailed - progress.PackagesSkipped);
        FinalizationSummaryText = progress.Summary is not null
            ? "Finalization completed. Search and browse indexes are ready."
            : "Building search/browse indexes and activating the rebuilt catalog.";
        FinalizationProgressMaximum = Math.Max(1, finalizationStepTotal);
        FinalizationProgressValue = progress.Summary is not null
            ? FinalizationProgressMaximum
            : Math.Clamp(finalizationStepCurrent, 0, finalizationStepTotal);
        FinalizationProgressIndeterminate = progress.Summary is null && finalizationStepCurrent <= 0;
        FinalizationStepText = lastFinalizationMessage;
        FinalizationProgressText = finalizationStepCurrent > 0
            ? $"Step {finalizationStepCurrent} / {finalizationStepTotal}"
            : "Preparing finalization steps.";
        FinalizationCatalogText = $"Indexed packages: {indexedPackages:N0} / {progress.PackagesTotal:N0}";
        FinalizationResourcesText = $"Indexed resources: {progress.CompletedResourcesProcessed:N0}";
        FinalizationElapsedText = $"Elapsed: {progress.Elapsed:hh\\:mm\\:ss}";
    }

    private void ApplyStageVisibility()
    {
        ScopeSectionVisibility = activeStage == UiStage.Scope ? Visibility.Visible : Visibility.Collapsed;
        IndexingSectionVisibility = activeStage == UiStage.Indexing ? Visibility.Visible : Visibility.Collapsed;
        FinalizationSectionVisibility = activeStage == UiStage.Finalization ? Visibility.Visible : Visibility.Collapsed;
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

    private void ApplyTerminalStageStatus(string terminalLabel)
    {
        var scopeState = StageCardState.Pending;
        var indexingState = StageCardState.Pending;
        var finalizationState = StageCardState.Pending;

        switch (activeStage)
        {
            case UiStage.Scope:
                ScopeStageStatusText = terminalLabel;
                scopeState = StageCardState.Failed;
                break;
            case UiStage.Indexing:
                ScopeStageStatusText = "Done";
                IndexingStageStatusText = terminalLabel;
                scopeState = StageCardState.Completed;
                indexingState = StageCardState.Failed;
                break;
            default:
                ScopeStageStatusText = "Done";
                IndexingStageStatusText = "Done";
                FinalizationStageStatusText = terminalLabel;
                scopeState = StageCardState.Completed;
                indexingState = StageCardState.Completed;
                finalizationState = StageCardState.Failed;
                break;
        }

        ScopeStageBackground = GetStageCardBackground(scopeState);
        IndexingStageBackground = GetStageCardBackground(indexingState);
        FinalizationStageBackground = GetStageCardBackground(finalizationState);
    }

    private string BuildStageFlowText(IndexingProgress progress)
    {
        if (progress.Summary is not null)
        {
            return "All 3 stages completed.";
        }

        if (string.Equals(progress.Stage, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            return "Indexing was canceled before all stages completed.";
        }

        return activeStage switch
        {
            UiStage.Scope => "Stage 1 of 3: defining package scope.",
            UiStage.Indexing => "Stage 2 of 3: building catalog shards.",
            UiStage.Finalization => "Stage 3 of 3: finalizing browse/search indexes.",
            _ => "Indexing in progress."
        };
    }

    private string BuildStageStatus(UiStage stage, IndexingProgress progress)
    {
        if (progress.Summary is not null)
        {
            return "Done";
        }

        if (string.Equals(progress.Stage, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            if (stage < activeStage)
            {
                return "Done";
            }

            return stage == activeStage ? "Canceled" : "Next";
        }

        if (stage < activeStage)
        {
            return "Done";
        }

        return stage == activeStage ? "Current" : "Next";
    }

    private StageCardState BuildStageCardState(UiStage stage, IndexingProgress progress)
    {
        if (progress.Summary is not null)
        {
            return StageCardState.Completed;
        }

        if (string.Equals(progress.Stage, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            if (stage < activeStage)
            {
                return StageCardState.Completed;
            }

            return stage == activeStage ? StageCardState.Failed : StageCardState.Pending;
        }

        if (stage < activeStage)
        {
            return StageCardState.Completed;
        }

        return stage == activeStage ? StageCardState.Active : StageCardState.Pending;
    }

    private static Brush GetStageCardBackground(StageCardState state)
    {
        return state switch
        {
            StageCardState.Active => ActiveStageBackgroundBrush,
            StageCardState.Completed => CompletedStageBackgroundBrush,
            StageCardState.Failed => FailedStageBackgroundBrush,
            _ => PendingStageBackgroundBrush
        };
    }

    private static bool TryResolveUiStage(string stage, out UiStage uiStage)
    {
        if (string.Equals(stage, "finalizing", StringComparison.OrdinalIgnoreCase))
        {
            uiStage = UiStage.Finalization;
            return true;
        }

        if (string.Equals(stage, "indexing", StringComparison.OrdinalIgnoreCase))
        {
            uiStage = UiStage.Indexing;
            return true;
        }

        if (string.Equals(stage, "scope", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stage, "preparing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stage, "discovering packages", StringComparison.OrdinalIgnoreCase))
        {
            uiStage = UiStage.Scope;
            return true;
        }

        uiStage = default;
        return false;
    }

    private static bool TryParseFinalizationStep(string? message, out int stepIndex, out int stepCount, out string stepText)
    {
        stepIndex = 0;
        stepCount = 0;
        stepText = "Preparing finalization.";

        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        if (message.StartsWith("Shard ", StringComparison.OrdinalIgnoreCase))
        {
            var separatorIndex = message.IndexOf(':');
            if (separatorIndex <= 6)
            {
                return false;
            }

            var shardPrefix = message[6..separatorIndex].Trim();
            var shardParts = shardPrefix.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (shardParts.Length != 2 ||
                !int.TryParse(shardParts[0], out var shardNumber) ||
                !int.TryParse(shardParts[1], out var shardCount))
            {
                return false;
            }

            var shardMessage = message[(separatorIndex + 1)..].Trim();
            stepCount = shardCount * 2 + 1;
            if (shardMessage.Contains("full-text search", StringComparison.OrdinalIgnoreCase))
            {
                stepIndex = ((shardNumber - 1) * 2) + 1;
            }
            else if (shardMessage.Contains("browse indexes", StringComparison.OrdinalIgnoreCase))
            {
                stepIndex = ((shardNumber - 1) * 2) + 2;
            }
            else
            {
                stepIndex = Math.Max(1, Math.Min(stepCount, shardNumber * 2));
            }

            stepText = message;
            return true;
        }

        if (message.Contains("Activating rebuilt catalog shards", StringComparison.OrdinalIgnoreCase))
        {
            stepIndex = 1;
            stepCount = 1;
            stepText = message;
            return true;
        }

        if (message.Contains("full-text search", StringComparison.OrdinalIgnoreCase))
        {
            stepIndex = 1;
            stepCount = 3;
            stepText = message;
            return true;
        }

        if (message.Contains("browse indexes", StringComparison.OrdinalIgnoreCase))
        {
            stepIndex = 2;
            stepCount = 3;
            stepText = message;
            return true;
        }

        if (message.Contains("Activating rebuilt catalog", StringComparison.OrdinalIgnoreCase))
        {
            stepIndex = 3;
            stepCount = 3;
            stepText = message;
            return true;
        }

        return false;
    }

    private static string BuildIndexingSummary(IndexingProgress progress)
    {
        if (progress.Summary is not null)
        {
            return $"Completed in {progress.Summary.TotalElapsed:hh\\:mm\\:ss}. Failed: {progress.Summary.PackagesFailed}. Skipped: {progress.Summary.PackagesSkipped}.";
        }

        if (string.Equals(progress.Stage, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            return "Indexing was canceled.";
        }

        return $"{progress.PackagesProcessed:N0}/{progress.PackagesTotal:N0} packages indexed so far.";
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
