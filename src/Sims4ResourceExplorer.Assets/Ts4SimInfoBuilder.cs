namespace Sims4ResourceExplorer.Assets;

/// <summary>
/// Builds a synthetic <see cref="Ts4SimInfo"/> for a human Sim from (age, gender).
/// Populates the body-driving outfit (category 5 = Nude) with the canonical EA
/// baseline Head/Top/Bottom/Shoes parts for that age × gender so the existing
/// rendering pipeline (BuildSimGraphAsync → BuildBuySceneBuildService) can consume
/// it unchanged.
///
/// FullInstance for the synthesised Sim is a deterministic FNV-1a 64-bit hash of
/// "synthetic:human:{age}:{gender}" — stable across runs, distinct from any real EA
/// SimInfo instance, and distinct per (age, gender) tuple so morph-resolver cache
/// keys don't collide between tuples.
/// </summary>
internal static class Ts4SimInfoBuilder
{
    public const uint DefaultVersion = 33u;
    public const uint HumanSpeciesValue = 1u;
    public const uint NudeOutfitCategoryValue = 5u;
    public const uint BodyTypeHead = 3u;
    public const uint BodyTypeTop = 6u;
    public const uint BodyTypeBottom = 7u;
    public const uint BodyTypeShoes = 8u;
    public const uint BodyTypeBrows = 34u;
    public const uint BodyTypeEyeColor = 35u;

    /// <summary>
    /// Builds a synthetic Ts4SimInfo for a human Sim of (<paramref name="ageLabel"/>,
    /// <paramref name="genderLabel"/>). The body-driving outfit contains canonical
    /// baseline Head/Top/Bottom/Shoes parts for that age × gender (parts are omitted
    /// for tuples the baseline catalog does not cover, e.g. unknown age).
    /// </summary>
    public static Ts4SimInfo BuildHuman(string ageLabel, string genderLabel, ulong skintoneInstance = 0ul)
    {
        var ageFlags = AgeFlagsFromLabel(ageLabel);
        var genderFlags = GenderFlagsFromLabel(genderLabel);
        var nudeOutfit = BuildNudeBodyDrivingOutfit(ageLabel, genderLabel);
        var outfits = nudeOutfit is null
            ? (IReadOnlyList<Ts4SimOutfit>)Array.Empty<Ts4SimOutfit>()
            : new[] { nudeOutfit };
        var outfitParts = nudeOutfit?.Parts ?? (IReadOnlyList<Ts4SimOutfitPart>)Array.Empty<Ts4SimOutfitPart>();

        return new Ts4SimInfo(
            Version: DefaultVersion,
            AgeFlags: ageFlags,
            GenderFlags: genderFlags,
            SpeciesValue: HumanSpeciesValue,
            SkintoneInstance: skintoneInstance,
            SkintoneShift: null,
            PronounCount: 0,
            OutfitCategoryCount: outfits.Count,
            OutfitEntryCount: outfits.Count,
            OutfitPartCount: outfitParts.Count,
            TraitCount: 0,
            FaceModifierCount: 0,
            BodyModifierCount: 0,
            GeneticFaceModifierCount: 0,
            GeneticBodyModifierCount: 0,
            SculptCount: 0,
            GeneticSculptCount: 0,
            GeneticPartCount: 0,
            GrowthPartCount: 0,
            PeltLayerCount: 0,
            PeltLayers: Array.Empty<Ts4SimPeltLayer>(),
            SculptChannels: Array.Empty<byte>(),
            FaceModifiers: Array.Empty<Ts4SimModifierEntry>(),
            BodyModifiers: Array.Empty<Ts4SimModifierEntry>(),
            Outfits: outfits,
            OutfitParts: outfitParts,
            GeneticSculptChannels: Array.Empty<byte>(),
            GeneticFaceModifiers: Array.Empty<Ts4SimModifierEntry>(),
            GeneticBodyModifiers: Array.Empty<Ts4SimModifierEntry>(),
            GeneticPartBodyTypes: Array.Empty<uint>(),
            GrowthPartBodyTypes: Array.Empty<uint>(),
            GeneticParts: Array.Empty<Ts4SimGeneticPart>());
    }

    /// <summary>
    /// Stable synthetic FullInstance id for cache-key purposes. Deterministic FNV-1a 64
    /// of "synthetic:human:{age}:{gender}" (case-insensitive). Distinct per tuple.
    /// </summary>
    public static ulong SyntheticFullInstance(string ageLabel, string genderLabel)
    {
        var ageKey = ageLabel?.Trim().ToLowerInvariant() ?? "unknown";
        var genderKey = genderLabel?.Trim().ToLowerInvariant() ?? "unknown";
        return Fnv1A64($"synthetic:human:{ageKey}:{genderKey}");
    }

    private static Ts4SimOutfit? BuildNudeBodyDrivingOutfit(string ageLabel, string genderLabel)
    {
        var parts = new List<Ts4SimOutfitPart>();
        AppendPart(parts, BodyTypeHead, Ts4CanonicalBaselineBodyParts.PickHead(ageLabel, genderLabel));
        AppendPart(parts, BodyTypeTop, Ts4CanonicalBaselineBodyParts.PickTop(ageLabel, genderLabel));
        AppendPart(parts, BodyTypeBottom, Ts4CanonicalBaselineBodyParts.PickBottom(ageLabel, genderLabel));
        AppendPart(parts, BodyTypeShoes, Ts4CanonicalBaselineBodyParts.PickShoes(ageLabel, genderLabel));
        // Texture-only face parts: feed the skin-atlas compositor's face-CAS overlay path
        // (iris/sclera land in the eye UV region, brows in the brow region). Not geometry.
        AppendPart(parts, BodyTypeBrows, Ts4CanonicalBaselineBodyParts.PickBrows(ageLabel, genderLabel));
        AppendPart(parts, BodyTypeEyeColor, Ts4CanonicalBaselineBodyParts.PickEyeColor(ageLabel, genderLabel));
        return parts.Count == 0 ? null : new Ts4SimOutfit(NudeOutfitCategoryValue, parts);
    }

    private static void AppendPart(List<Ts4SimOutfitPart> parts, uint bodyType, ulong? partInstance)
    {
        if (partInstance is { } instance && instance != 0ul)
        {
            parts.Add(new Ts4SimOutfitPart(bodyType, instance));
        }
    }

    private static uint AgeFlagsFromLabel(string ageLabel)
    {
        if (string.IsNullOrWhiteSpace(ageLabel)) return 0u;
        var normalized = ageLabel.Trim().ToLowerInvariant();
        return normalized switch
        {
            "infant" or "baby" => 0x00000080u,
            "toddler" or "preschooler" => 0x00000002u,
            "child" => 0x00000004u,
            "teen" => 0x00000008u,
            "young adult" or "young-adult" or "ya" => 0x00000010u,
            "adult" => 0x00000020u,
            "elder" => 0x00000040u,
            _ => 0u
        };
    }

    private static uint GenderFlagsFromLabel(string genderLabel)
    {
        if (string.IsNullOrWhiteSpace(genderLabel)) return 0u;
        var normalized = genderLabel.Trim().ToLowerInvariant();
        return normalized switch
        {
            "female" => 0x00002000u,
            "male" => 0x00001000u,
            "unisex" => 0x00003000u,
            _ => 0u
        };
    }

    private static ulong Fnv1A64(string input)
    {
        const ulong fnvPrime = 0x100000001B3ul;
        const ulong fnvOffsetBasis = 0xCBF29CE484222325ul;
        var hash = fnvOffsetBasis;
        foreach (var c in input)
        {
            hash ^= c;
            hash *= fnvPrime;
        }
        return hash;
    }
}
