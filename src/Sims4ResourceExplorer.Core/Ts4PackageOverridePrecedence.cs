namespace Sims4ResourceExplorer.Core;

/// <summary>
/// Central rule for the game's package override order. TS4 ships the same resource key in
/// several packages and the runtime resolves the copy from the package loaded LAST:
/// FullBuild packages load first, in-Data DeltaBuild patch packages override them, the
/// root-level <c>\Delta\</c> patch directory overrides those, and user content under Mods
/// overrides all game content.
///
/// Evidence (2026-06-12 probe session, see docs/planning/current-plan.md "Skin-pipeline
/// ground-truth correction"): the legacy full-body skin diffuse 0x3E68F8B6F44DA2AA carries
/// real content in ClientFullBuild8 but is patched to an empty LRLE in ClientDeltaBuild8;
/// skintone 0x...AFC5 ships as legacy v6 in ClientFullBuild0 and as the modern v12 resource
/// in ClientDeltaBuild0. Code that preferred FullBuild copies (or "package-local" copies
/// living in a FullBuild package) silently rendered pre-patch assets the live game no longer
/// uses.
/// </summary>
public static class Ts4PackageOverridePrecedence
{
    /// <summary>
    /// Higher rank = loaded later by the game = overrides lower ranks. Packages that match
    /// no known game naming pattern are treated as user/override content (Mods, CC), which
    /// the game loads last.
    /// </summary>
    public static int GetRank(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return 0;
        }

        var normalized = packagePath.Replace('/', '\\');
        if (normalized.Contains("\\Delta\\", StringComparison.OrdinalIgnoreCase))
        {
            return 400;
        }

        if (normalized.Contains("DeltaBuild", StringComparison.OrdinalIgnoreCase))
        {
            return 300;
        }

        if (normalized.Contains("Preload", StringComparison.OrdinalIgnoreCase))
        {
            return 200;
        }

        if (normalized.Contains("FullBuild", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        return 500;
    }

    /// <summary>
    /// Orders candidates so the game-effective copy comes FIRST (highest override rank,
    /// then a stable path tiebreak). Use when picking one copy of a resource key that may
    /// exist in several packages.
    /// </summary>
    public static IOrderedEnumerable<T> OrderByGameOverride<T>(
        IEnumerable<T> candidates,
        Func<T, string> packagePathSelector) =>
        candidates
            .OrderByDescending(candidate => GetRank(packagePathSelector(candidate)))
            .ThenBy(candidate => packagePathSelector(candidate), StringComparer.OrdinalIgnoreCase);
}
