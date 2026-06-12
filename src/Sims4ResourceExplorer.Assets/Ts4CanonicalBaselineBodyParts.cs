namespace Sims4ResourceExplorer.Assets;

/// <summary>
/// Canonical EA-shipped baseline body CASPart instance IDs per (age, gender, slot).
/// These are the "nude" parts the engine selects when a Sim's outfit doesn't include
/// a Top, Bottom, or Shoes for a given slot. Confirmed against EA's local catalog and
/// against community body-replacement mods that override these exact instance IDs.
///
/// Source: docs/workflows/canonical-baseline-bodies.md (mod investigation, build 0237).
/// Mod evidence: <c>wild_guy CmarNudeBottomFemaleDefault.package</c> and friends override
/// <c>0x198C / 0x1990 / 0x19AE</c> directly — confirming those ARE EA's canonical IDs.
/// </summary>
internal static class Ts4CanonicalBaselineBodyParts
{
    // Adult / Teen / Young Adult / Elder.
    public const ulong YfTopNude = 0x000000000000198Cul;
    public const ulong YmTopNude = 0x00000000000019A2ul;
    public const ulong YfBottomNude = 0x0000000000001990ul;
    public const ulong YmBottomNude = 0x00000000000019AEul;
    public const ulong YfShoesNude = 0x000000000000198Ful;
    public const ulong YmShoesNude = 0x00000000000019A3ul;
    public const ulong YfHead = 0x0000000000001B41ul;
    public const ulong YmHead = 0x000000000000229Cul;

    // Child.
    public const ulong CfTopNude = 0x000000000000F3BAul;
    public const ulong CuTopNude = 0x0000000000005635ul;
    public const ulong CuBottomNude = 0x000000000000563Aul;
    public const ulong CuShoesNude = 0x0000000000005602ul;
    public const ulong CuHead = 0x0000000000006CA8ul;

    // Toddler (Preschooler) / Infant.
    public const ulong PuTopNude = 0x00000000000206C7ul;
    public const ulong PuBottomNude = 0x00000000000206CCul;  // Discovered after parser v52 fix (build 0240).
    public const ulong PuShoesNude = 0x00000000000206D2ul;
    public const ulong PuHead = 0x00000000000206C9ul;
    public const ulong IuTopNude = 0x0000000000045585ul;
    public const ulong IuBottomNude = 0x0000000000045587ul;
    public const ulong IuShoesNude = 0x0000000000045588ul;
    public const ulong IuHead = 0x0000000000045580ul;

    /// <summary>Returns the canonical Top (BodyType=6) instance for the Sim's age × gender, or null when unknown.</summary>
    public static ulong? PickTop(string? ageLabel, string? genderLabel)
    {
        var bucket = AgeBucket(ageLabel);
        var female = IsFemale(genderLabel);
        return bucket switch
        {
            AgeBucketKind.Adult => female ? YfTopNude : YmTopNude,
            AgeBucketKind.Child => female ? CfTopNude : CuTopNude,
            AgeBucketKind.Toddler => PuTopNude,
            AgeBucketKind.Infant => IuTopNude,
            _ => null
        };
    }

    /// <summary>Returns the canonical Bottom (BodyType=7) instance for the Sim's age × gender, or null when unknown.</summary>
    public static ulong? PickBottom(string? ageLabel, string? genderLabel)
    {
        var bucket = AgeBucket(ageLabel);
        var female = IsFemale(genderLabel);
        return bucket switch
        {
            AgeBucketKind.Adult => female ? YfBottomNude : YmBottomNude,
            AgeBucketKind.Child => CuBottomNude,
            AgeBucketKind.Toddler => PuBottomNude,
            AgeBucketKind.Infant => IuBottomNude,
            _ => null
        };
    }

    /// <summary>Returns the canonical Shoes (BodyType=8) instance for the Sim's age × gender, or null when unknown.</summary>
    public static ulong? PickShoes(string? ageLabel, string? genderLabel)
    {
        var bucket = AgeBucket(ageLabel);
        var female = IsFemale(genderLabel);
        return bucket switch
        {
            AgeBucketKind.Adult => female ? YfShoesNude : YmShoesNude,
            AgeBucketKind.Child => CuShoesNude,
            AgeBucketKind.Toddler => PuShoesNude,
            AgeBucketKind.Infant => IuShoesNude,
            _ => null
        };
    }

    /// <summary>Returns the canonical Head (BodyType=3) instance for the Sim's age × gender, or null when unknown.</summary>
    public static ulong? PickHead(string? ageLabel, string? genderLabel)
    {
        var bucket = AgeBucket(ageLabel);
        var female = IsFemale(genderLabel);
        return bucket switch
        {
            AgeBucketKind.Adult => female ? YfHead : YmHead,
            AgeBucketKind.Child => CuHead,
            AgeBucketKind.Toddler => PuHead,
            AgeBucketKind.Infant => IuHead,
            _ => null
        };
    }

    // Default eye color: `yfMakeupEyeColor_Brown` (bt=35), Human, unisex, age-flagged
    // Child→Elder in the base game (ClientFullBuild0). Without an EyeColor part the eye
    // region of the composed skin atlas samples bare tone texture and the eyeballs render
    // skin-colored — the game always equips an EyeColor part on every Sim.
    public const ulong YuEyeColorBrown = 0x0000000000002AD7ul;

    // Default brows (bt=34), Teen→Elder, per gender, base game (ClientFullBuild0). Brow
    // diffuses are authored in full body-atlas UV space (probe-verified 1024×2048), so they
    // composite through the same face-CAS overlay path as eye color.
    public const ulong YfBrowsArchedMediumBrown = 0x0000000000002D14ul;
    public const ulong YmBrowsArchedFullBlack = 0x0000000000005491ul;

    /// <summary>
    /// Returns the canonical default EyeColor (BodyType=35) instance for the Sim's
    /// age × gender, or null when no age-appropriate default is catalogued yet.
    /// Toddler/Infant use separate parts AND the face-overlay compositor currently
    /// age-gates overlays to the Teen+ bucket (adult-face UV layout), so those tuples
    /// stay null until per-age UV-region mapping lands.
    /// </summary>
    public static ulong? PickEyeColor(string? ageLabel, string? genderLabel) =>
        AgeBucket(ageLabel) switch
        {
            AgeBucketKind.Adult or AgeBucketKind.Child => YuEyeColorBrown,
            _ => null
        };

    /// <summary>
    /// Returns the canonical default Brows (BodyType=34) instance for the Sim's
    /// age × gender, or null for ages without a catalogued default. The EA brow parts are
    /// gender-split for the Teen+ bucket; child/toddler defaults stay null until the
    /// face-overlay age gate widens.
    /// </summary>
    public static ulong? PickBrows(string? ageLabel, string? genderLabel) =>
        AgeBucket(ageLabel) switch
        {
            AgeBucketKind.Adult => IsFemale(genderLabel) ? YfBrowsArchedMediumBrown : YmBrowsArchedFullBlack,
            _ => null
        };

    /// <summary>Returns true when the gender label resolves to female; false for male, unisex, or unknown.</summary>
    private static bool IsFemale(string? genderLabel) =>
        !string.IsNullOrWhiteSpace(genderLabel) &&
        genderLabel.Trim().StartsWith("Female", StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps the Sim's age label to a baseline-body bucket.</summary>
    private static AgeBucketKind AgeBucket(string? ageLabel)
    {
        if (string.IsNullOrWhiteSpace(ageLabel)) return AgeBucketKind.Unknown;
        var normalized = ageLabel.Trim().ToLowerInvariant();
        // The label may be a multi-flag string like "Teen / Young Adult / Adult / Elder";
        // any teen-and-older flag maps to the Adult bucket (which covers ages 8/16/32/64).
        if (normalized.Contains("teen", StringComparison.Ordinal) ||
            normalized.Contains("young adult", StringComparison.Ordinal) ||
            normalized.Contains("adult", StringComparison.Ordinal) ||
            normalized.Contains("elder", StringComparison.Ordinal))
        {
            return AgeBucketKind.Adult;
        }
        if (normalized.Contains("child", StringComparison.Ordinal)) return AgeBucketKind.Child;
        if (normalized.Contains("toddler", StringComparison.Ordinal)) return AgeBucketKind.Toddler;
        if (normalized.Contains("infant", StringComparison.Ordinal) ||
            normalized.Contains("baby", StringComparison.Ordinal))
        {
            return AgeBucketKind.Infant;
        }
        return AgeBucketKind.Unknown;
    }

    private enum AgeBucketKind
    {
        Unknown,
        Infant,
        Toddler,
        Child,
        Adult
    }
}
