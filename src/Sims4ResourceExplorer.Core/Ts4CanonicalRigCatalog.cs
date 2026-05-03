namespace Sims4ResourceExplorer.Core;

/// <summary>
/// Resolves the canonical TS4 rig instance for a given (species, age, occult) tuple.
/// Mirrors TS4SimRipper's <c>Form1.GetTS4Rig</c> + <c>PreviewControl.GetRigPrefix</c> at
/// <c>docs/references/external/TS4SimRipper/src/Form1.cs:1404-1421</c> and
/// <c>docs/references/external/TS4SimRipper/src/PreviewControl.cs:2162-2174</c>.
///
/// The rig is named by concatenating an age prefix + species suffix + "Rig" and hashing
/// with FNV-1 64-bit (lowercase ASCII, multiply-then-XOR). Examples:
/// <list type="bullet">
///   <item><c>auRig</c> = adult / teen / young-adult / elder human</item>
///   <item><c>cuRig</c> = child human</item>
///   <item><c>puRig</c> = toddler human</item>
///   <item><c>iuRig</c> = infant human</item>
///   <item><c>acRig</c> = adult cat, <c>adRig</c> = adult dog, <c>alRig</c> = adult little-dog</item>
/// </list>
/// Special-cased instance IDs (not name-hashed): werewolf occult uses
/// <c>0x60FAA42F9B0B4E39</c>; fairy occult uses FNV-64("nuRig").
/// </summary>
public static class Ts4CanonicalRigCatalog
{
    /// <summary>Werewolf rig — TS4SimRipper hardcodes this instance ID rather than hashing a name.</summary>
    public const ulong WerewolfRigInstance = 0x60FAA42F9B0B4E39ul;

    /// <summary>Returns the canonical Rig resource instance for a Sim's species/age/occult, or null when the tuple is unrecognised.</summary>
    public static ulong? GetRigInstance(string? speciesLabel, string? ageLabel, string? occultLabel)
    {
        if (IsOccult(occultLabel, "werewolf")) return WerewolfRigInstance;
        if (IsOccult(occultLabel, "fairy")) return ComputeFnv64("nuRig");

        var prefix = GetRigPrefix(speciesLabel, ageLabel);
        return prefix is null ? null : ComputeFnv64(prefix + "Rig");
    }

    /// <summary>Returns the rig name (e.g. <c>"auRig"</c>) for a Sim's species/age, or null when unrecognised.</summary>
    public static string? GetRigName(string? speciesLabel, string? ageLabel)
    {
        var prefix = GetRigPrefix(speciesLabel, ageLabel);
        return prefix is null ? null : prefix + "Rig";
    }

    /// <summary>FNV-1 64-bit hash of a lowercase ASCII string. Compatible with TS4SimRipper's <c>FNVhash.FNV64</c>.</summary>
    public static ulong ComputeFnv64(string name)
    {
        const ulong offsetBasis = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        var hash = offsetBasis;
        foreach (var b in System.Text.Encoding.ASCII.GetBytes(name.ToLowerInvariant()))
        {
            unchecked { hash *= prime; }
            hash ^= b;
        }
        return hash;
    }

    private static string? GetRigPrefix(string? speciesLabel, string? ageLabel)
    {
        if (string.IsNullOrWhiteSpace(speciesLabel)) return null;
        var species = speciesLabel.Trim().ToLowerInvariant();
        var ageBucket = AgeBucket(ageLabel);
        if (ageBucket is null) return null;

        var ageSpecifier = ageBucket switch
        {
            RigAgeBucket.Infant => species == "human" ? "i" : "c",
            RigAgeBucket.Toddler => species == "human" ? "p" : "c",
            RigAgeBucket.Child => "c",
            RigAgeBucket.Adult => "a",
            _ => null
        };
        if (ageSpecifier is null) return null;

        var speciesSpecifier = species switch
        {
            "human" => "u",
            "cat" => "c",
            "dog" => "d",
            "little dog" or "littledog" => ageBucket == RigAgeBucket.Child ? "d" : "l",
            "horse" => "h",
            "fox" => "f",
            _ => null
        };
        if (speciesSpecifier is null) return null;

        return ageSpecifier + speciesSpecifier;
    }

    private static RigAgeBucket? AgeBucket(string? ageLabel)
    {
        if (string.IsNullOrWhiteSpace(ageLabel)) return null;
        var normalized = ageLabel.Trim().ToLowerInvariant();
        if (normalized.Contains("infant", StringComparison.Ordinal) ||
            normalized.Contains("baby", StringComparison.Ordinal))
        {
            return RigAgeBucket.Infant;
        }
        if (normalized.Contains("toddler", StringComparison.Ordinal)) return RigAgeBucket.Toddler;
        if (normalized.Contains("child", StringComparison.Ordinal)) return RigAgeBucket.Child;
        if (normalized.Contains("teen", StringComparison.Ordinal) ||
            normalized.Contains("young adult", StringComparison.Ordinal) ||
            normalized.Contains("adult", StringComparison.Ordinal) ||
            normalized.Contains("elder", StringComparison.Ordinal))
        {
            return RigAgeBucket.Adult;
        }
        return null;
    }

    private static bool IsOccult(string? occultLabel, string match) =>
        !string.IsNullOrWhiteSpace(occultLabel) &&
        occultLabel.Trim().Equals(match, StringComparison.OrdinalIgnoreCase);

    private enum RigAgeBucket { Infant, Toddler, Child, Adult }
}
