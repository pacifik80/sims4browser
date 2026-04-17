using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void SourcePathClassifier_InfersModsAndDlcFromPath()
    {
        Assert.Equal(SourceKind.Mods, SourcePathClassifier.InferKind(@"C:\Users\Name\Documents\Electronic Arts\The Sims 4\Mods"));
        Assert.Equal(SourceKind.Dlc, SourcePathClassifier.InferKind(@"C:\Games\The Sims 4\EP04"));
        Assert.Equal(SourceKind.Game, SourcePathClassifier.InferKind(@"C:\Games\The Sims 4"));
    }

    [Fact]
    public void IndexingMemoryBudgetCalculator_CapsRequestedBudgetBySafeAvailableMemory()
    {
        var snapshot = new SystemMemorySnapshot(
            TotalPhysicalBytes: 32UL * 1024 * 1024 * 1024,
            AvailablePhysicalBytes: 8UL * 1024 * 1024 * 1024);

        var budget = IndexingMemoryBudgetCalculator.ResolvePackageByteCacheBudgetBytes(snapshot, 50);

        Assert.Equal((long)Math.Floor(snapshot.AvailablePhysicalBytes * 0.85d), budget);
    }

    [Fact]
    public void IndexingMemoryBudgetCalculator_UsesRequestedPercentWhenEnoughMemoryIsAvailable()
    {
        var snapshot = new SystemMemorySnapshot(
            TotalPhysicalBytes: 16UL * 1024 * 1024 * 1024,
            AvailablePhysicalBytes: 16UL * 1024 * 1024 * 1024);

        var budget = IndexingMemoryBudgetCalculator.ResolvePackageByteCacheBudgetBytes(snapshot, 25);

        Assert.Equal(4L * 1024 * 1024 * 1024, budget);
    }
}
