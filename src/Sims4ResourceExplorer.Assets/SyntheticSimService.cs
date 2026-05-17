namespace Sims4ResourceExplorer.Assets;

/// <summary>
/// Public surface for the App layer to construct a synthetic Sim for the Sim
/// Character Constructor view. Wraps the internal <see cref="Ts4SimInfoBuilder"/>
/// so the App does not need access to the internal <c>Ts4SimInfo</c> record, and
/// delegates AssetGraph construction to <see cref="Sims4ResourceExplorer.Core.IAssetGraphBuilder"/>.
/// </summary>
public interface ISyntheticSimService
{
    SimConstructorSeed CreateHumanSeed(string ageLabel, string genderLabel, ulong skintoneInstance = 0ul);

    Task<Sims4ResourceExplorer.Core.AssetGraph> BuildHumanAssetGraphAsync(
        SimConstructorSeed seed,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SkintoneOption>> EnumerateSkintonesAsync(CancellationToken cancellationToken);

    IReadOnlyList<string> AvailableHumanAges { get; }
    IReadOnlyList<string> AvailableHumanGenders { get; }
}

/// <summary>
/// One row in the constructor's skintone picker. <see cref="Instance"/> is the TONE
/// resource's full instance id; <see cref="DisplayName"/> is the index'd resource name
/// when present, otherwise a hex fallback. <see cref="PackagePath"/> identifies which
/// indexed package shipped this skintone (useful for diagnostics + duplicate detection).
/// </summary>
public sealed record SkintoneOption(
    ulong Instance,
    string DisplayName,
    string PackagePath,
    string FullTgi);

/// <summary>
/// Public seed describing a synthesised Sim. Carries enough metadata for the
/// constructor UI to display + identify it; the internal <c>Ts4SimInfo</c>
/// remains hidden behind the service.
/// </summary>
public sealed record SimConstructorSeed(
    string AgeLabel,
    string GenderLabel,
    string SpeciesLabel,
    ulong SkintoneInstance,
    ulong SyntheticFullInstance,
    int OutfitPartCount,
    string SummaryText);

public sealed class SyntheticSimService : ISyntheticSimService
{
    private readonly Sims4ResourceExplorer.Core.IAssetGraphBuilder graphBuilder;
    private readonly Sims4ResourceExplorer.Core.IIndexStore indexStore;

    public SyntheticSimService(Sims4ResourceExplorer.Core.IAssetGraphBuilder graphBuilder, Sims4ResourceExplorer.Core.IIndexStore indexStore)
    {
        this.graphBuilder = graphBuilder;
        this.indexStore = indexStore;
    }

    public async Task<IReadOnlyList<SkintoneOption>> EnumerateSkintonesAsync(CancellationToken cancellationToken)
    {
        var resources = await indexStore.GetResourcesByTypeNameAsync("Skintone", cancellationToken).ConfigureAwait(false);
        return resources
            .GroupBy(r => r.Key.FullInstance)
            .Select(group =>
            {
                var first = group.First();
                var name = !string.IsNullOrWhiteSpace(first.Name)
                    ? first.Name
                    : $"Skintone 0x{first.Key.FullInstance:X16}";
                return new SkintoneOption(first.Key.FullInstance, name, first.PackagePath, first.Key.FullTgi);
            })
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> AvailableHumanAges { get; } =
        ["Infant", "Toddler", "Child", "Teen", "Young Adult", "Adult", "Elder"];

    public IReadOnlyList<string> AvailableHumanGenders { get; } =
        ["Female", "Male"];

    public Task<Sims4ResourceExplorer.Core.AssetGraph> BuildHumanAssetGraphAsync(
        SimConstructorSeed seed,
        CancellationToken cancellationToken) =>
        graphBuilder.BuildSyntheticHumanSimGraphAsync(seed.AgeLabel, seed.GenderLabel, seed.SkintoneInstance, cancellationToken);

    public SimConstructorSeed CreateHumanSeed(string ageLabel, string genderLabel, ulong skintoneInstance = 0ul)
    {
        var info = Ts4SimInfoBuilder.BuildHuman(ageLabel, genderLabel, skintoneInstance);
        var fullInstance = Ts4SimInfoBuilder.SyntheticFullInstance(ageLabel, genderLabel);
        var summary = info.BuildDescription();

        return new SimConstructorSeed(
            AgeLabel: info.AgeLabel,
            GenderLabel: info.GenderLabel,
            SpeciesLabel: info.SpeciesLabel,
            SkintoneInstance: info.SkintoneInstance,
            SyntheticFullInstance: fullInstance,
            OutfitPartCount: info.OutfitPartCount,
            SummaryText: summary);
    }
}
