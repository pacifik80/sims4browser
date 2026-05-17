// Reference: TS4SimRipper (GPL-3.0)
//   docs/references/external/TS4SimRipper/src/Form1.cs:1404-1421 (GetTS4Rig)
//   docs/references/external/TS4SimRipper/src/PreviewControl.cs:2162-2174 (GetRigPrefix)
// Build 0289: Sim character pipeline rewrite — step 1 of the rig+skinning+morph rewrite.
// Resolves the canonical Granny rig instance for a Sim's (species, age, occult) tuple.
//
// The resolution is deterministic — no fallback list, no overlap-based picker. Per the
// reference, every Sim has exactly one canonical rig identified by FNV-1 64-bit hash of
// "<prefix>Rig" (lowercase ASCII). Special-case occult variants override the prefix
// resolution: werewolves use a hard-coded instance, fairies use "nuRig".
//
// Replaces the build 0269+ "try humanFirst → speciesPair → otherPets" picker, whose
// overlap-driven tie-breaking was making opaque selection decisions and silently
// dropping into the wrong rig (e.g. cdRig vs alRig for Little Dog Child).

using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview.SimRender;

/// <summary>
/// Resolves the canonical Granny rig for a Sim's (species, age, occult) tuple. Wraps
/// <see cref="Ts4CanonicalRigCatalog"/> with rig-package probe paths and gives a single,
/// deterministic answer instead of the historic "try N rig names and rank by overlap"
/// picker.
/// </summary>
public static class SimRigCatalog
{
    /// <summary>
    /// Returns the canonical (rig-name, rig-instance-hash) tuple for the given Sim metadata,
    /// or null when the species/age combo is unrecognised.
    /// </summary>
    public static (string Name, ulong InstanceHash)? Resolve(string? speciesLabel, string? ageLabel, string? occultLabel = null)
    {
        var instance = Ts4CanonicalRigCatalog.GetRigInstance(speciesLabel, ageLabel, occultLabel);
        if (instance is null)
        {
            return null;
        }

        // Werewolf is by-instance only; no name. Synthesise a label for diagnostics.
        if (instance == Ts4CanonicalRigCatalog.WerewolfRigInstance)
        {
            return ("werewolfRig", instance.Value);
        }

        // Fairy occult overrides species/age, lands on "nuRig".
        if (string.Equals(occultLabel?.Trim(), "fairy", StringComparison.OrdinalIgnoreCase))
        {
            return ("nuRig", instance.Value);
        }

        var name = Ts4CanonicalRigCatalog.GetRigName(speciesLabel, ageLabel);
        return name is null ? null : (name, instance.Value);
    }

    /// <summary>
    /// Standard package-relative paths under the game install root where canonical rigs
    /// are known to live. Used as a fallback when the index store doesn't have a Rig
    /// resource at the canonical hash (partial indexing or older cache).
    /// </summary>
    public static IReadOnlyList<string> GetRigProbePathsUnderInstall(string installRoot) =>
    [
        Path.Combine(installRoot, "Data", "Client", "ClientDeltaBuild0.package"),
        Path.Combine(installRoot, "Data", "Simulation", "SimulationDeltaBuild0.package"),
        Path.Combine(installRoot, "Data", "Simulation", "SimulationPreload.package"),
    ];
}
