using System.Text.Json;

namespace Sims4ResourceExplorer.Preview;

internal enum Ts4MaterialCoverageTier
{
    StaticReady = 0,
    Approximate = 1,
    RuntimeDependent = 2
}

internal sealed record Ts4MaterialCoverageMetrics(
    int TotalProfiles,
    int TotalOccurrences,
    int StaticReadyProfiles,
    int StaticReadyOccurrences,
    int ApproximateProfiles,
    int ApproximateOccurrences,
    int RuntimeDependentProfiles,
    int RuntimeDependentOccurrences,
    int ProfilesWithUvSemantics,
    int ProfilesWithLayeredSlots,
    int ProfilesWithDivergentUvRisk,
    int ProfilesUsingScaleAndOffset,
    IReadOnlyList<string> TopApproximateProfiles,
    IReadOnlyList<string> TopRuntimeDependentProfiles);

internal static class Ts4MaterialCoverageAnalyzer
{
    public static Ts4MaterialCoverageMetrics AnalyzeCurrentProfiles()
    {
        var path = Ts4ShaderProfileRegistry.ResolveProfilePath();
        return path is not null && File.Exists(path)
            ? AnalyzeProfileJson(path)
            : new Ts4MaterialCoverageMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], []);
    }

    internal static Ts4MaterialCoverageMetrics AnalyzeProfileJson(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var totals = new CoverageAccumulator();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            var profile = property.Value;
            var profileName = profile.TryGetProperty("name_guess", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() ?? property.Name
                : property.Name;
            var occurrences = profile.TryGetProperty("occurrences", out var occurrencesElement) && occurrencesElement.TryGetInt32(out var parsedOccurrences)
                ? Math.Max(1, parsedOccurrences)
                : 1;
            var parameterNames = ReadParameterNames(profile);
            var tier = ClassifyProfile(profileName, parameterNames);
            var hasUvSemantics = parameterNames.Any(static name => LooksLikeUvSemantic(name));
            var hasLayeredSlots = parameterNames.Any(static name => LooksLikeLayeredSemantic(name));
            var hasDivergentUvRisk = HasDivergentUvRisk(parameterNames);
            var usesScaleAndOffset = parameterNames.Any(static name =>
                name.Contains("ScaleAndOffset", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ScaleOffset", StringComparison.OrdinalIgnoreCase));

            totals.TotalProfiles++;
            totals.TotalOccurrences += occurrences;
            if (hasUvSemantics)
            {
                totals.ProfilesWithUvSemantics++;
            }

            if (hasLayeredSlots)
            {
                totals.ProfilesWithLayeredSlots++;
            }

            if (hasDivergentUvRisk)
            {
                totals.ProfilesWithDivergentUvRisk++;
            }

            if (usesScaleAndOffset)
            {
                totals.ProfilesUsingScaleAndOffset++;
            }

            switch (tier)
            {
                case Ts4MaterialCoverageTier.StaticReady:
                    totals.StaticReadyProfiles++;
                    totals.StaticReadyOccurrences += occurrences;
                    break;
                case Ts4MaterialCoverageTier.Approximate:
                    totals.ApproximateProfiles++;
                    totals.ApproximateOccurrences += occurrences;
                    totals.ApproximateExamples.Add($"{profileName} ({occurrences})");
                    break;
                case Ts4MaterialCoverageTier.RuntimeDependent:
                    totals.RuntimeDependentProfiles++;
                    totals.RuntimeDependentOccurrences += occurrences;
                    totals.RuntimeDependentExamples.Add($"{profileName} ({occurrences})");
                    break;
            }
        }

        return new Ts4MaterialCoverageMetrics(
            totals.TotalProfiles,
            totals.TotalOccurrences,
            totals.StaticReadyProfiles,
            totals.StaticReadyOccurrences,
            totals.ApproximateProfiles,
            totals.ApproximateOccurrences,
            totals.RuntimeDependentProfiles,
            totals.RuntimeDependentOccurrences,
            totals.ProfilesWithUvSemantics,
            totals.ProfilesWithLayeredSlots,
            totals.ProfilesWithDivergentUvRisk,
            totals.ProfilesUsingScaleAndOffset,
            totals.ApproximateExamples
                .GroupBy(static item => item, StringComparer.Ordinal)
                .OrderByDescending(static group => ParseOccurrence(group.Key))
                .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(static group => group.Key)
                .ToArray(),
            totals.RuntimeDependentExamples
                .GroupBy(static item => item, StringComparer.Ordinal)
                .OrderByDescending(static group => ParseOccurrence(group.Key))
                .ThenBy(static group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(static group => group.Key)
                .ToArray());
    }

    internal static Ts4MaterialCoverageTier ClassifyProfile(string profileName, IReadOnlyList<string> parameterNames)
    {
        if (LooksLikeRuntimeDependentProfileName(profileName))
        {
            return Ts4MaterialCoverageTier.RuntimeDependent;
        }

        if (parameterNames.Count == 0)
        {
            return Ts4MaterialCoverageTier.StaticReady;
        }

        var uvParameters = parameterNames.Where(static name => LooksLikeUvSemantic(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (uvParameters.Length == 0)
        {
            return Ts4MaterialCoverageTier.StaticReady;
        }

        if (uvParameters.Any(static name => RequiresRuntimeUv(name)))
        {
            return Ts4MaterialCoverageTier.RuntimeDependent;
        }

        if (HasDivergentUvRisk(parameterNames))
        {
            return Ts4MaterialCoverageTier.Approximate;
        }

        if (uvParameters.Any(static name => !IsStaticPreviewSupportedUvSemantic(name)))
        {
            return Ts4MaterialCoverageTier.Approximate;
        }

        return Ts4MaterialCoverageTier.StaticReady;
    }

    internal static Ts4MaterialCoverageTier ClassifyMaterial(string profileName, IReadOnlyList<string> propertyNames)
    {
        if (propertyNames.Count == 0)
        {
            return Ts4MaterialCoverageTier.StaticReady;
        }

        var uvParameters = propertyNames.Where(static name => LooksLikeUvSemantic(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var animatedControls = propertyNames.Where(static name => LooksLikeAnimatedSemantic(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var hasAnimatedControls = animatedControls.Length > 0;
        var hasStrongAnimatedControls = animatedControls.Any(static name => !IsWeakAnimatedSemantic(name));
        var hasRuntimeUvControls = propertyNames.Any(static name => RequiresRuntimeMaterialUv(name));

        if (hasRuntimeUvControls)
        {
            return Ts4MaterialCoverageTier.RuntimeDependent;
        }

        if (uvParameters.Length == 0)
        {
            return hasStrongAnimatedControls || (hasAnimatedControls && LooksLikeAnimatedFamilyName(profileName))
                ? Ts4MaterialCoverageTier.Approximate
                : Ts4MaterialCoverageTier.StaticReady;
        }

        if (HasDivergentUvRisk(propertyNames))
        {
            return Ts4MaterialCoverageTier.Approximate;
        }

        if (uvParameters.Any(static name => !IsStaticPreviewSupportedUvSemantic(name)))
        {
            return Ts4MaterialCoverageTier.Approximate;
        }

        if (hasStrongAnimatedControls || (hasAnimatedControls && LooksLikeAnimatedFamilyName(profileName)))
        {
            return Ts4MaterialCoverageTier.Approximate;
        }

        return Ts4MaterialCoverageTier.StaticReady;
    }

    private static bool LooksLikeRuntimeDependentProfileName(string profileName) =>
        profileName.Contains("WorldToDepthMapSpace", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("DepthMapSpace", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("ClipSpace", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Project", StringComparison.OrdinalIgnoreCase);

    private static string[] ReadParameterNames(JsonElement profile)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!profile.TryGetProperty("parm_sets", out var parmSetsElement) || parmSetsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        foreach (var parmSetGroup in parmSetsElement.EnumerateArray())
        {
            if (parmSetGroup.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var parmSet in parmSetGroup.EnumerateArray())
            {
                if (!parmSet.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var paramElement in paramsElement.EnumerateArray())
                {
                    if (!paramElement.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var name = nameElement.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }
            }
        }

        return names.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool LooksLikeUvSemantic(string name) =>
        name.Contains("UV", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Atlas", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("MapRect", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "uvMapping", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeLayeredSemantic(string name)
    {
        if (name.Contains("Overlay", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Decal", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Dirt", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Grime", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Emiss", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Opacity", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Cutout", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Contains("Detail", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Spec", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Normal", StringComparison.OrdinalIgnoreCase))
        {
            return LooksLikeTextureSemantic(name);
        }

        return false;
    }

    private static bool LooksLikeAnimatedSemantic(string name) =>
        name.Contains("Scroll", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("VideoVTexture", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Caustic", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Animated", StringComparison.OrdinalIgnoreCase);

    private static bool IsWeakAnimatedSemantic(string name) =>
        name.Contains("WaterScrollSpeedLayer", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAnimatedFamilyName(string profileName) =>
        profileName.Contains("Video", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Caustic", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Scroll", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Water", StringComparison.OrdinalIgnoreCase);

    private static bool HasDivergentUvRisk(IReadOnlyList<string> parameterNames)
    {
        var uvParameters = parameterNames.Where(static name => LooksLikeUvSemantic(name)).ToArray();
        if (uvParameters.Length == 0)
        {
            return false;
        }

        var slotSpecificUvCount = uvParameters.Count(static name =>
            name.Contains("Normal", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Overlay", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Detail", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Decal", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Dirt", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Grime", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Spec", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Emiss", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Opacity", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Mask", StringComparison.OrdinalIgnoreCase));
        if (slotSpecificUvCount > 0)
        {
            return true;
        }

        var layeredCount = parameterNames.Count(static name => LooksLikeLayeredSemantic(name));
        return uvParameters.Length >= 2 && layeredCount >= 2;
    }

    private static bool LooksLikeTextureSemantic(string name) =>
        !name.Contains("Scale", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Offset", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Tileable", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Bias", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Strength", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Intensity", StringComparison.OrdinalIgnoreCase) &&
        (name.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("Map", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("Sampler", StringComparison.OrdinalIgnoreCase));

    private static bool RequiresRuntimeUv(string name)
    {
        if (name.Contains("ScaleAndOffset", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ScaleOffset", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("UVScale", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("UVOffset", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Atlas", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.Contains("gPosToUVDest", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("gPosToUVSrc", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("WorldToDepthMapSpace", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("ClipSpace", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("DepthMapSpace", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresRuntimeMaterialUv(string name) =>
        name.Contains("gPosToUVDest", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("gPosToUVSrc", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("WorldToDepthMapSpace", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("DepthMapSpace", StringComparison.OrdinalIgnoreCase);

    private static bool IsStaticPreviewSupportedUvSemantic(string name) =>
        name.Contains("uvMapping", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("UsesUV1", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("UVChannel", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("UVScale", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("UVOffset", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("UScale", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("VScale", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("UOffset", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("VOffset", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("AtlasMin", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("AtlasMax", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("MapMin", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("MapMax", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("MapAtlas", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Atlas", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("AtlasRect", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ScaleAndOffset", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ScaleOffset", StringComparison.OrdinalIgnoreCase);

    private static int ParseOccurrence(string text)
    {
        var open = text.LastIndexOf('(');
        var close = text.LastIndexOf(')');
        return open >= 0 &&
               close > open &&
               int.TryParse(text[(open + 1)..close], out var occurrences)
            ? occurrences
            : 0;
    }

    private sealed class CoverageAccumulator
    {
        public int TotalProfiles { get; set; }
        public int TotalOccurrences { get; set; }
        public int StaticReadyProfiles { get; set; }
        public int StaticReadyOccurrences { get; set; }
        public int ApproximateProfiles { get; set; }
        public int ApproximateOccurrences { get; set; }
        public int RuntimeDependentProfiles { get; set; }
        public int RuntimeDependentOccurrences { get; set; }
        public int ProfilesWithUvSemantics { get; set; }
        public int ProfilesWithLayeredSlots { get; set; }
        public int ProfilesWithDivergentUvRisk { get; set; }
        public int ProfilesUsingScaleAndOffset { get; set; }
        public List<string> ApproximateExamples { get; } = [];
        public List<string> RuntimeDependentExamples { get; } = [];
    }
}
