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

    Task<Sims4ResourceExplorer.Core.SimSkintoneRenderSummary?> ResolveSkintoneRenderAsync(
        string ageLabel,
        string genderLabel,
        ulong skintoneInstance,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SkintoneOption>> EnumerateSkintonesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Enumerates all CAS parts in the index at the given body_type. Caller is
    /// responsible for prepending the "None" sentinel when used as a picker source.
    /// </summary>
    Task<IReadOnlyList<Sims4ResourceExplorer.Core.ResourceMetadata>> EnumerateCasPartsByBodyTypeAsync(
        int bodyType,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the diffuse PNG bytes for a CAS part by instance — used to lift
    /// a user-picked face overlay layer into the skin atlas composer.
    /// </summary>
    Task<byte[]?> ResolveCasPartDiffusePngAsync(ulong casPartInstance, CancellationToken cancellationToken);

    IReadOnlyList<string> AvailableHumanAges { get; }
    IReadOnlyList<string> AvailableHumanGenders { get; }
}

/// <summary>
/// One row in the constructor's skintone picker. <see cref="Instance"/> is the TONE
/// resource's full instance id; <see cref="DisplayName"/> is the index'd resource name
/// when present, otherwise a hex fallback. <see cref="PackagePath"/> identifies which
/// indexed package shipped this skintone. <see cref="SwatchArgb"/> is the first entry
/// of the TONE's swatchColors list (ARGB-encoded uint) — used for the visual swatch
/// preview. Null when the TONE failed to parse or had no swatches.
/// </summary>
public sealed record SkintoneOption(
    ulong Instance,
    string DisplayName,
    string PackagePath,
    string FullTgi,
    uint? SwatchArgb);

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
    private readonly Sims4ResourceExplorer.Core.IResourceCatalogService resourceCatalogService;

    public SyntheticSimService(
        Sims4ResourceExplorer.Core.IAssetGraphBuilder graphBuilder,
        Sims4ResourceExplorer.Core.IIndexStore indexStore,
        Sims4ResourceExplorer.Core.IResourceCatalogService resourceCatalogService)
    {
        this.graphBuilder = graphBuilder;
        this.indexStore = indexStore;
        this.resourceCatalogService = resourceCatalogService;
    }

    /// <summary>
    /// Sentinel "no skintone" option (Instance == 0) — when picked the renderer skips atlas
    /// composition and the body materials render without any skintone binding. The UI shows
    /// it as a red-crossed-circle tile.
    /// </summary>
    public static readonly SkintoneOption NoneSkintone = new(0ul, "None (no skintone)", string.Empty, string.Empty, null);

    public async Task<IReadOnlyList<SkintoneOption>> EnumerateSkintonesAsync(CancellationToken cancellationToken)
    {
        var resources = await indexStore.GetResourcesByTypeNameAsync("Skintone", cancellationToken).ConfigureAwait(false);
        // Group by FullInstance — different packages can ship the same TONE; we only need
        // one representative for the picker. Keep the first PackagePath as the canonical
        // source for the swatch-color parse below.
        var grouped = resources
            .GroupBy(r => r.Key.FullInstance)
            .Select(g => g.First())
            .ToArray();
        // Parse swatchColors[0] in parallel; failures degrade to a null SwatchArgb so the
        // picker can still surface the entry with a placeholder colour.
        var parsed = await Task.WhenAll(grouped.Select(async resource =>
        {
            uint? swatch = null;
            try
            {
                var bytes = await resourceCatalogService
                    .GetResourceBytesAsync(resource.PackagePath, resource.Key, raw: false, cancellationToken)
                    .ConfigureAwait(false);
                var tone = Sims4ResourceExplorer.Packages.Ts4StructuredResourceMetadataExtractor.ParseSkintone(bytes);
                if (tone.SwatchColors is { Count: > 0 })
                {
                    swatch = tone.SwatchColors[0];
                }
            }
            catch
            {
                // Best-effort: leave swatch null.
            }
            var name = !string.IsNullOrWhiteSpace(resource.Name)
                ? resource.Name
                : $"Skintone 0x{resource.Key.FullInstance:X16}";
            return new SkintoneOption(resource.Key.FullInstance, name, resource.PackagePath, resource.Key.FullTgi, swatch);
        })).ConfigureAwait(false);
        var ordered = parsed
            .OrderBy(o => o.SwatchArgb is { } a ? -RelativeLuminance(a) : double.MaxValue)
            .ThenBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase);
        return new[] { NoneSkintone }.Concat(ordered).ToArray();
    }

    private static double RelativeLuminance(uint argb)
    {
        var r = ((argb >> 16) & 0xFF) / 255.0;
        var g = ((argb >> 8) & 0xFF) / 255.0;
        var b = (argb & 0xFF) / 255.0;
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    public IReadOnlyList<string> AvailableHumanAges { get; } =
        ["Infant", "Toddler", "Child", "Teen", "Young Adult", "Adult", "Elder"];

    public IReadOnlyList<string> AvailableHumanGenders { get; } =
        ["Female", "Male"];

    public Task<Sims4ResourceExplorer.Core.AssetGraph> BuildHumanAssetGraphAsync(
        SimConstructorSeed seed,
        CancellationToken cancellationToken) =>
        graphBuilder.BuildSyntheticHumanSimGraphAsync(seed.AgeLabel, seed.GenderLabel, seed.SkintoneInstance, cancellationToken);

    public Task<Sims4ResourceExplorer.Core.SimSkintoneRenderSummary?> ResolveSkintoneRenderAsync(
        string ageLabel,
        string genderLabel,
        ulong skintoneInstance,
        CancellationToken cancellationToken) =>
        graphBuilder.ResolveHumanSkintoneAsync(ageLabel, genderLabel, skintoneInstance, cancellationToken);

    public Task<IReadOnlyList<Sims4ResourceExplorer.Core.ResourceMetadata>> EnumerateCasPartsByBodyTypeAsync(
        int bodyType,
        CancellationToken cancellationToken) =>
        indexStore.GetCasPartsByBodyTypeAsync(bodyType, cancellationToken);

    public Task<byte[]?> ResolveCasPartDiffusePngAsync(ulong casPartInstance, CancellationToken cancellationToken) =>
        graphBuilder.ResolveCasPartDiffusePngAsync(casPartInstance, cancellationToken);

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
