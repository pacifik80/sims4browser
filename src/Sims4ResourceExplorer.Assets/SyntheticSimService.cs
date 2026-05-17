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

    IReadOnlyList<string> AvailableHumanAges { get; }
    IReadOnlyList<string> AvailableHumanGenders { get; }
}

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

    public SyntheticSimService(Sims4ResourceExplorer.Core.IAssetGraphBuilder graphBuilder)
    {
        this.graphBuilder = graphBuilder;
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
