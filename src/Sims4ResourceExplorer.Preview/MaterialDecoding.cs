namespace Sims4ResourceExplorer.Preview;

internal enum Ts4MaterialFamilyKind
{
    Unknown = 0,
    SeasonalFoliage = 1,
    ColorMap7 = 2,
    StairRailings = 3
}

internal sealed record Ts4MaterialDecodeResult(
    string ShaderProfileName,
    string ShaderFamilyName,
    Ts4MaterialFamilyKind FamilyKind,
    string StrategyName,
    Ts4TextureUvMapping DiffuseUvMapping,
    bool SuggestsAlphaCutout,
    string? AlphaModeHint,
    IReadOnlyList<string> Notes);

internal sealed record Ts4MaterialDecodeState(
    Ts4TextureUvMapping DiffuseUvMapping,
    bool SuggestsAlphaCutout,
    string? AlphaModeHint,
    IReadOnlyList<string> Notes);

internal interface ITs4MaterialDecodeStrategy
{
    string Name { get; }
    Ts4MaterialFamilyKind FamilyKind { get; }
    bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile);
    Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile);
}

internal static class Ts4MaterialDecoder
{
    private static readonly ITs4MaterialDecodeStrategy[] strategies =
    [
        new SeasonalFoliageMaterialDecodeStrategy(),
        new StairRailingsMaterialDecodeStrategy(),
        new ColorMap7MaterialDecodeStrategy(),
        new DefaultMaterialDecodeStrategy()
    ];

    public static Ts4MaterialDecodeResult Decode(MaterialIr material, ShaderBlockProfile? profile)
    {
        var profileName = profile?.Name ?? material.ShaderName;
        var familyName = NormalizeFamilyName(profileName);
        var strategy = strategies.First(strategy => strategy.CanHandle(familyName, material, profile));
        return strategy.Decode(profileName, familyName, material, profile);
    }

    internal static Ts4MaterialDecodeResult DecodeGeneric(
        string profileName,
        string familyName,
        Ts4MaterialFamilyKind familyKind,
        string strategyName,
        MaterialIr material,
        ShaderBlockProfile? profile,
        bool forceAlphaCutout = false,
        string? forcedAlphaReason = null)
    {
        var notes = new List<string>();
        var mapping = material.DiffuseUvMapping;
        var suggestsAlphaCutout = false;
        string? alphaModeHint = null;

        foreach (var property in material.Properties)
        {
            if (property.ValueRepresentation == MaterialValueRepresentation.Scalar &&
                property.FloatValues is { Length: > 0 } scalarValues &&
                Ts4ShaderSemantics.TryInterpretDiffuseUvMappingScalar(
                    Ts4ShaderSemantics.ResolveParameterProfile(property.Hash, profile),
                    scalarValues[0],
                    mapping,
                    out var scalarMapping))
            {
                mapping = scalarMapping;
                continue;
            }

            if (property.ValueRepresentation == MaterialValueRepresentation.FloatVector &&
                property.FloatValues is { Length: > 0 } vectorValues &&
                Ts4ShaderSemantics.TryInterpretDiffuseUvMappingVector(
                    Ts4ShaderSemantics.ResolveParameterProfile(property.Hash, profile),
                    vectorValues,
                    mapping,
                    out var vectorMapping))
            {
                mapping = vectorMapping;
                continue;
            }

            if (string.Equals(property.Name, "uvMapping", StringComparison.OrdinalIgnoreCase) &&
                property.ValueRepresentation == MaterialValueRepresentation.PackedUInt32)
            {
                notes.Add("uvMapping is stored as packed data and is not yet decoded by the generic material pipeline.");
            }

            if (!suggestsAlphaCutout &&
                string.Equals(property.Name, "AlphaMaskThreshold", StringComparison.OrdinalIgnoreCase) &&
                property.ValueRepresentation == MaterialValueRepresentation.Scalar)
            {
                suggestsAlphaCutout = true;
                alphaModeHint = "alpha-test-or-blend";
                notes.Add("AlphaMaskThreshold is present; preview material is flagged for alpha cutout.");
            }
        }

        if (!suggestsAlphaCutout &&
            material.TextureReferences.Any(static reference =>
                reference.Slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                reference.Slot.Contains("mask", StringComparison.OrdinalIgnoreCase)))
        {
            suggestsAlphaCutout = true;
            alphaModeHint = "alpha-test-or-blend";
            notes.Add("Material exposes alpha-related texture slots; preview material is flagged for alpha cutout.");
        }

        if (!suggestsAlphaCutout &&
            profile is not null &&
            profile.Parameters.Any(static parameter =>
                parameter.Name.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
                parameter.Name.Contains("Cutout", StringComparison.OrdinalIgnoreCase) ||
                parameter.Name.Contains("Opacity", StringComparison.OrdinalIgnoreCase)))
        {
            suggestsAlphaCutout = true;
            alphaModeHint = "alpha-test-or-blend";
            notes.Add("Shader profile exposes alpha-oriented parameters; preview material is flagged for alpha cutout.");
        }

        if (!suggestsAlphaCutout && forceAlphaCutout)
        {
            suggestsAlphaCutout = true;
            alphaModeHint = "alpha-test-or-blend";
            if (!string.IsNullOrWhiteSpace(forcedAlphaReason))
            {
                notes.Add(forcedAlphaReason);
            }
        }

        return new Ts4MaterialDecodeResult(
            profileName,
            familyName,
            familyKind,
            strategyName,
            mapping,
            suggestsAlphaCutout,
            alphaModeHint,
            notes);
    }

    internal static Ts4MaterialDecodeResult BuildResult(
        string profileName,
        string familyName,
        Ts4MaterialFamilyKind familyKind,
        string strategyName,
        Ts4MaterialDecodeState state) =>
        new(
            profileName,
            familyName,
            familyKind,
            strategyName,
            state.DiffuseUvMapping,
            state.SuggestsAlphaCutout,
            state.AlphaModeHint,
            state.Notes);

    internal static Ts4MaterialDecodeState ToState(this Ts4MaterialDecodeResult result) =>
        new(result.DiffuseUvMapping, result.SuggestsAlphaCutout, result.AlphaModeHint, result.Notes);

    internal static string NormalizeFamilyName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return "Unknown";
        }

        var span = profileName.AsSpan().Trim();
        var separators = span.IndexOfAny(['-', '_', ' ']);
        return separators > 0 ? span[..separators].ToString() : span.ToString();
    }
}

internal sealed class SeasonalFoliageMaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(SeasonalFoliageMaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.SeasonalFoliage;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        familyName.Equals("SeasonalFoliage", StringComparison.OrdinalIgnoreCase);

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.DecodeGeneric(
            profileName,
            familyName,
            FamilyKind,
            Name,
            material,
            profile,
            forceAlphaCutout: true,
            forcedAlphaReason: "SeasonalFoliage defaults to alpha-tested silhouettes in preview.");
}

internal sealed class StairRailingsMaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(StairRailingsMaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.StairRailings;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        familyName.Equals("StairRailings", StringComparison.OrdinalIgnoreCase);

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.DecodeGeneric(profileName, familyName, FamilyKind, Name, material, profile);
}

internal sealed class ColorMap7MaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(ColorMap7MaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.ColorMap7;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        familyName.Equals("colorMap7", StringComparison.OrdinalIgnoreCase);

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile)
    {
        var state = Ts4MaterialDecoder.DecodeGeneric(profileName, familyName, FamilyKind, Name, material, profile).ToState();
        var notes = state.Notes.ToList();
        var mapping = state.DiffuseUvMapping;

        // Legacy colorMap7 handling from the original preview path, now centralized in the
        // family decoder rather than hardcoded in MATD parse.
        var uvChannelSelector = material.Properties.FirstOrDefault(static property => property.Hash == 0xB95C43EB);
        if (uvChannelSelector?.ValueRepresentation == MaterialValueRepresentation.Scalar &&
            uvChannelSelector.FloatValues is { Length: > 0 } selectorValues)
        {
            var rounded = (int)MathF.Round(selectorValues[0]);
            if (rounded is 0 or 1)
            {
                mapping = mapping with { UvChannel = rounded };
                notes.Add($"colorMap7 legacy UV channel selector applied from 0xB95C43EB -> UV{rounded}.");
            }
        }

        var packedAtlas = material.Properties.FirstOrDefault(static property => property.Hash == 0xCEBA7E8A);
        if (packedAtlas?.ValueRepresentation == MaterialValueRepresentation.FloatVector &&
            packedAtlas.FloatValues is { Length: >= 4 } atlasValues &&
            mapping.UvChannel == 1)
        {
            var scaleU = atlasValues[0];
            var scaleV = atlasValues[1];
            var offsetV = atlasValues[3];
            if (scaleU > 0f && scaleU <= 1f &&
                scaleV > 0f && scaleV <= 1f &&
                float.IsFinite(offsetV))
            {
                mapping = mapping with
                {
                    UvScaleU = scaleU,
                    UvOffsetU = scaleU,
                    UvScaleV = scaleV,
                    UvOffsetV = Math.Clamp(offsetV, 0f, 1f)
                };
                notes.Add("colorMap7 legacy atlas crop applied from 0xCEBA7E8A.");
            }
        }

        return Ts4MaterialDecoder.BuildResult(
            profileName,
            familyName,
            FamilyKind,
            Name,
            state with
            {
                DiffuseUvMapping = mapping,
                Notes = notes
            });
    }
}

internal sealed class DefaultMaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(DefaultMaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.Unknown;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) => true;

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.DecodeGeneric(profileName, familyName, FamilyKind, Name, material, profile);
}
