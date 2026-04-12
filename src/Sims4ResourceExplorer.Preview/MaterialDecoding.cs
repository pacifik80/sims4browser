namespace Sims4ResourceExplorer.Preview;

internal enum Ts4MaterialFamilyKind
{
    Unknown = 0,
    SeasonalFoliage = 1,
    ColorMap7 = 2,
    StairRailings = 3,
    AlphaCutout = 4,
    StandardSurface = 5,
    ColorMap = 6,
    Projective = 7
}

internal sealed record Ts4MaterialSamplingInstruction(
    string Slot,
    int UvChannel,
    float UvScaleU,
    float UvScaleV,
    float UvOffsetU,
    float UvOffsetV,
    string Source,
    bool IsApproximate);

internal sealed record Ts4MaterialDecodeResult(
    string ShaderProfileName,
    string ShaderFamilyName,
    Ts4MaterialFamilyKind FamilyKind,
    string StrategyName,
    Ts4MaterialCoverageTier CoverageTier,
    Ts4TextureUvMapping DiffuseUvMapping,
    IReadOnlyList<Ts4MaterialSamplingInstruction> SamplingInstructions,
    float[]? ApproximateBaseColor,
    string? AlphaSourceSlot,
    IReadOnlyList<string> LayeredColorSlots,
    IReadOnlyList<string> UtilityTextureSlots,
    bool SuggestsAlphaCutout,
    string? AlphaModeHint,
    IReadOnlyList<string> Notes);

internal sealed record Ts4MaterialDecodeState(
    Ts4TextureUvMapping DiffuseUvMapping,
    IReadOnlyList<Ts4MaterialSamplingInstruction> SamplingInstructions,
    float[]? ApproximateBaseColor,
    string? AlphaSourceSlot,
    IReadOnlyList<string> LayeredColorSlots,
    IReadOnlyList<string> UtilityTextureSlots,
    bool SuggestsAlphaCutout,
    string? AlphaModeHint,
    IReadOnlyList<string> Notes,
    Ts4MaterialCoverageTier CoverageTier = Ts4MaterialCoverageTier.StaticReady);

internal interface ITs4MaterialDecodeStrategy
{
    string Name { get; }
    Ts4MaterialFamilyKind FamilyKind { get; }
    bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile);
    Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile);
}

internal static class Ts4MaterialDecoder
{
    private const uint ColorMapLegacyUvChannelSelectorHash = 0xB95C43EB;
    private const uint ColorMapLegacyAtlasCropHash = 0xCEBA7E8A;

    private static readonly ITs4MaterialDecodeStrategy[] strategies =
    [
        new SeasonalFoliageMaterialDecodeStrategy(),
        new ProjectiveMaterialDecodeStrategy(),
        new AlphaCutoutMaterialDecodeStrategy(),
        new StairRailingsMaterialDecodeStrategy(),
        new ColorMap7MaterialDecodeStrategy(),
        new ColorMapMaterialDecodeStrategy(),
        new StandardSurfaceMaterialDecodeStrategy(),
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
        var coverageTier = Ts4MaterialCoverageAnalyzer.ClassifyMaterial(
            profileName,
            material.Properties
                .Select(static property => property.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());

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
                if (TryInterpretPackedUvProperty(property, mapping, out var packedMapping, out var packedNote))
                {
                    mapping = packedMapping;
                    notes.Add(packedNote);
                }
                else
                {
                    notes.Add("uvMapping is stored as packed data and is not yet decoded by the generic material pipeline.");
                }
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

        var animatedProperties = material.Properties
            .Where(static property => IsAnimatedUvSemantic(property.Name))
            .Select(static property => property.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (animatedProperties.Any(static name => !IsWeakAnimatedSemantic(name)) ||
            (animatedProperties.Length > 0 &&
             coverageTier != Ts4MaterialCoverageTier.StaticReady &&
             (LooksLikeAnimatedFamilyName(profileName) || LooksLikeAnimatedFamilyName(familyName))))
        {
            notes.Add("Animated UV controls are ignored by static preview and currently fall back to a still-frame approximation.");
        }

        if (material.Properties.Any(static property => IsStrongProjectiveUvSemantic(property.Name)) ||
            (coverageTier == Ts4MaterialCoverageTier.RuntimeDependent &&
             (LooksLikeProjectiveFamilyName(profileName) || LooksLikeProjectiveFamilyName(familyName))))
        {
            notes.Add("Projective or world-space UV controls are only partially approximated in the current preview pipeline.");
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
            coverageTier,
            mapping,
            BuildSamplingInstructions(material, mapping, profileName, familyName, coverageTier),
            DetermineApproximateBaseColor(material),
            DetermineAlphaSourceSlot(material),
            DetermineLayeredColorSlots(material),
            DetermineUtilityTextureSlots(material),
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
            state.CoverageTier,
            state.DiffuseUvMapping,
            state.SamplingInstructions,
            state.ApproximateBaseColor,
            state.AlphaSourceSlot,
            state.LayeredColorSlots,
            state.UtilityTextureSlots,
            state.SuggestsAlphaCutout,
            state.AlphaModeHint,
            state.Notes);

    internal static Ts4MaterialDecodeState ToState(this Ts4MaterialDecodeResult result) =>
        new(result.DiffuseUvMapping, result.SamplingInstructions, result.ApproximateBaseColor, result.AlphaSourceSlot, result.LayeredColorSlots, result.UtilityTextureSlots, result.SuggestsAlphaCutout, result.AlphaModeHint, result.Notes, result.CoverageTier);

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

    internal static bool IsColorMapFamily(string familyName, string profileName) =>
        familyName.StartsWith("colorMap", StringComparison.OrdinalIgnoreCase) ||
        profileName.StartsWith("colorMap", StringComparison.OrdinalIgnoreCase);

    internal static bool IsFoliageFamily(string familyName, string profileName) =>
        familyName.Contains("Foliage", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Foliage", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Plant", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Plant", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Leaf", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Leaf", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Grass", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Grass", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
        familyName.Contains("Bush", StringComparison.OrdinalIgnoreCase) ||
        profileName.Contains("Bush", StringComparison.OrdinalIgnoreCase);

    internal static bool IsProjectiveFamily(string familyName, string profileName, MaterialIr material) =>
        LooksLikeProjectiveFamilyName(familyName) ||
        LooksLikeProjectiveFamilyName(profileName) ||
        Ts4MaterialCoverageAnalyzer.ClassifyProfile(
            profileName,
            material.Properties
                .Select(static property => property.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()) == Ts4MaterialCoverageTier.RuntimeDependent &&
        material.Properties.Any(static property => IsProjectiveUvSemantic(property.Name));

    internal static bool IsAlphaCutoutFamily(string familyName, string profileName, MaterialIr material, ShaderBlockProfile? profile)
    {
        if (familyName.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
            profileName.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
            familyName.Contains("Cutout", StringComparison.OrdinalIgnoreCase) ||
            profileName.Contains("Cutout", StringComparison.OrdinalIgnoreCase) ||
            IsFoliageFamily(familyName, profileName))
        {
            return true;
        }

        if (material.TextureReferences.Any(static reference =>
                reference.Slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                reference.Slot.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
                reference.Slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
                reference.Slot.Contains("cutout", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return profile is not null &&
               profile.Parameters.Any(static parameter =>
                   parameter.Name.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
                   parameter.Name.Contains("Cutout", StringComparison.OrdinalIgnoreCase) ||
                   parameter.Name.Contains("Opacity", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsStandardSurfaceFamily(string familyName, string profileName) =>
        familyName.StartsWith("Phong", StringComparison.OrdinalIgnoreCase) ||
        profileName.StartsWith("Phong", StringComparison.OrdinalIgnoreCase) ||
        familyName.StartsWith("DiffuseMap", StringComparison.OrdinalIgnoreCase) ||
        profileName.StartsWith("DiffuseMap", StringComparison.OrdinalIgnoreCase) ||
        familyName.Equals("Interior", StringComparison.OrdinalIgnoreCase) ||
        profileName.Equals("Interior", StringComparison.OrdinalIgnoreCase) ||
        familyName.Equals("ObjectWithPerfectLight", StringComparison.OrdinalIgnoreCase) ||
        profileName.Equals("ObjectWithPerfectLight", StringComparison.OrdinalIgnoreCase) ||
        familyName.Equals("CASRoom", StringComparison.OrdinalIgnoreCase) ||
        profileName.Equals("CASRoom", StringComparison.OrdinalIgnoreCase) ||
        familyName.Equals("SelfIllumination", StringComparison.OrdinalIgnoreCase) ||
        profileName.Equals("SelfIllumination", StringComparison.OrdinalIgnoreCase) ||
        familyName.Equals("TextureCompositorOverlay", StringComparison.OrdinalIgnoreCase) ||
        profileName.Equals("TextureCompositorOverlay", StringComparison.OrdinalIgnoreCase) ||
        familyName.Equals("BlockPreview", StringComparison.OrdinalIgnoreCase) ||
        profileName.Equals("BlockPreview", StringComparison.OrdinalIgnoreCase);

    internal static Ts4MaterialDecodeState ApplyLegacyColorMapRules(Ts4MaterialDecodeState state, MaterialIr material)
    {
        var notes = state.Notes.ToList();
        var mapping = state.DiffuseUvMapping;

        var uvChannelSelector = material.Properties.FirstOrDefault(static property => property.Hash == ColorMapLegacyUvChannelSelectorHash);
        if (uvChannelSelector?.ValueRepresentation == MaterialValueRepresentation.Scalar &&
            uvChannelSelector.FloatValues is { Length: > 0 } selectorValues)
        {
            var rounded = (int)MathF.Round(selectorValues[0]);
            if (rounded is 0 or 1)
            {
                mapping = mapping with { UvChannel = rounded };
                notes.Add($"colorMap legacy UV channel selector applied from 0x{ColorMapLegacyUvChannelSelectorHash:X8} -> UV{rounded}.");
            }
        }

        var packedAtlas = material.Properties.FirstOrDefault(static property => property.Hash == ColorMapLegacyAtlasCropHash);
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
                notes.Add($"colorMap legacy atlas crop applied from 0x{ColorMapLegacyAtlasCropHash:X8}.");
            }
        }

        return state with
        {
            DiffuseUvMapping = mapping,
            SamplingInstructions = BuildSamplingInstructions(material, mapping, string.Empty, string.Empty, state.CoverageTier),
            Notes = notes
        };
    }

    internal static Ts4MaterialDecodeState ApplyProjectiveStillApproximation(Ts4MaterialDecodeState state, MaterialIr material)
    {
        if (TryBuildProjectiveStillMapping(material, state.DiffuseUvMapping, out var mapping, out var note))
        {
            var notes = state.Notes.ToList();
            notes.Add(note);
            return state with
            {
                DiffuseUvMapping = mapping,
                SamplingInstructions = state.SamplingInstructions
                    .Select(instruction => instruction with
                    {
                        UvChannel = mapping.UvChannel,
                        UvScaleU = mapping.UvScaleU,
                        UvScaleV = mapping.UvScaleV,
                        UvOffsetU = mapping.UvOffsetU,
                        UvOffsetV = mapping.UvOffsetV,
                        Source = "projective-still-approximation",
                        IsApproximate = true
                    })
                    .ToArray(),
                CoverageTier = Ts4MaterialCoverageTier.Approximate,
                Notes = notes
            };
        }

        return state;
    }

    internal static float[]? DetermineApproximateBaseColor(MaterialIr material)
    {
        MaterialIrProperty? bestCandidate = null;
        var bestScore = int.MinValue;

        foreach (var property in material.Properties)
        {
            var isNamedColorSemantic = LooksLikeColorSemantic(property.Name);
            if (!isNamedColorSemantic && !LooksLikeFallbackColorSemantic(property))
            {
                continue;
            }

            if (!TryExtractApproximateColor(property, out var color))
            {
                continue;
            }

            var score = isNamedColorSemantic
                ? ScoreColorCandidate(property.Name, property.Category)
                : ScoreFallbackColorCandidate(property.Category, color);
            if (score <= bestScore)
            {
                continue;
            }

            if (!isNamedColorSemantic && color.Length >= 4 && color[3] <= 0.05f)
            {
                color[3] = 1f;
            }

            bestScore = score;
            bestCandidate = property with { FloatValues = color };
        }

        return bestCandidate?.FloatValues;
    }

    internal static string? DetermineAlphaSourceSlot(MaterialIr material)
    {
        static bool Matches(string slot, params string[] expected) =>
            expected.Any(candidate => slot.Equals(candidate, StringComparison.OrdinalIgnoreCase));

        var explicitOpacity = material.TextureReferences
            .FirstOrDefault(static reference =>
                reference.Slot.Equals("opacity", StringComparison.OrdinalIgnoreCase) ||
                reference.Slot.Equals("alpha", StringComparison.OrdinalIgnoreCase) ||
                reference.Slot.Equals("mask", StringComparison.OrdinalIgnoreCase) ||
                reference.Slot.Equals("cutout", StringComparison.OrdinalIgnoreCase));
        if (explicitOpacity != default)
        {
            return explicitOpacity.Slot;
        }

        var overlayAlphaHint = material.Properties.Any(static property =>
            property.Name.Contains("Overlay", StringComparison.OrdinalIgnoreCase) &&
            (property.Name.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
             property.Name.Contains("Opacity", StringComparison.OrdinalIgnoreCase) ||
             property.Name.Contains("Mask", StringComparison.OrdinalIgnoreCase)));
        if (overlayAlphaHint && material.TextureReferences.Any(static reference => reference.Slot.Equals("overlay", StringComparison.OrdinalIgnoreCase)))
        {
            return "overlay";
        }

        var diffuseAlphaHint = material.Properties.Any(static property =>
            property.Name.Contains("AlphaMaskThreshold", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("DiffuseAlpha", StringComparison.OrdinalIgnoreCase));
        if (diffuseAlphaHint && material.TextureReferences.Any(static reference => Matches(reference.Slot, "diffuse", "basecolor", "albedo")))
        {
            return material.TextureReferences.First(reference => Matches(reference.Slot, "diffuse", "basecolor", "albedo")).Slot;
        }

        return null;
    }

    internal static IReadOnlyList<string> DetermineLayeredColorSlots(MaterialIr material) =>
        material.TextureReferences
            .Select(static reference => reference.Slot)
            .Where(IsLayeredColorSlotName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static slot => slot, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static IReadOnlyList<string> DetermineUtilityTextureSlots(MaterialIr material) =>
        material.TextureReferences
            .Select(static reference => reference.Slot)
            .Where(IsUtilityTextureSlotName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static slot => slot, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static IReadOnlyList<Ts4MaterialSamplingInstruction> BuildSamplingInstructions(
        MaterialIr material,
        Ts4TextureUvMapping diffuseMapping,
        string profileName,
        string familyName,
        Ts4MaterialCoverageTier coverageTier)
    {
        if (material.TextureReferences.Count == 0)
        {
            return [];
        }

        var instructions = new List<Ts4MaterialSamplingInstruction>(material.TextureReferences.Count);
        foreach (var reference in material.TextureReferences)
        {
            var usesDiffuse = UsesSharedMaterialUvTransform(reference.Slot);
            var mapping = usesDiffuse
                ? diffuseMapping
                : new Ts4TextureUvMapping(0, 1f, 1f, 0f, 0f);
            mapping = ApplySlotSpecificSamplingRules(reference.Slot, mapping, material.Properties);
            var (source, isApproximate) = DescribeSamplingSource(reference.Slot, usesDiffuse, material.Properties, profileName, familyName, coverageTier);
            instructions.Add(new Ts4MaterialSamplingInstruction(
                reference.Slot,
                mapping.UvChannel,
                mapping.UvScaleU,
                mapping.UvScaleV,
                mapping.UvOffsetU,
                mapping.UvOffsetV,
                source,
                IsApproximate: isApproximate));
        }

        return instructions
            .GroupBy(static instruction => instruction.Slot, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static instruction => instruction.Slot, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool UsesSharedMaterialUvTransform(string slot) =>
        slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("basecolor", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("albedo", StringComparison.OrdinalIgnoreCase) ||
        slot.StartsWith("texture_", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("alpha", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("opacity", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("mask", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("overlay", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("cutout", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("specular", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("emissive", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("detail", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("decal", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("dirt", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("grime", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("ao", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("occlusion", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("height", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("displacement", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("gloss", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("roughness", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("smoothness", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("metallic", StringComparison.OrdinalIgnoreCase);

    internal static Ts4MaterialSamplingInstruction CreateSyntheticSamplingInstruction(
        MaterialIr material,
        string slot,
        Ts4TextureUvMapping mapping,
        string profileName,
        string familyName,
        Ts4MaterialCoverageTier coverageTier,
        Ts4MaterialSamplingInstruction? preferredInstruction = null)
    {
        var source = preferredInstruction?.Source;
        var isApproximate = preferredInstruction?.IsApproximate;
        if (string.IsNullOrWhiteSpace(source) || isApproximate is null)
        {
            var described = DescribeSamplingSource(
                slot,
                usesDiffuse: true,
                material.Properties,
                profileName,
                familyName,
                coverageTier);
            source = described.Source;
            isApproximate = described.IsApproximate;
        }

        return new Ts4MaterialSamplingInstruction(
            slot,
            mapping.UvChannel,
            mapping.UvScaleU,
            mapping.UvScaleV,
            mapping.UvOffsetU,
            mapping.UvOffsetV,
            source!,
            isApproximate!.Value);
    }

    private static Ts4TextureUvMapping ApplySlotSpecificSamplingRules(
        string slot,
        Ts4TextureUvMapping current,
        IReadOnlyList<MaterialIrProperty> properties)
    {
        var mapping = current;
        foreach (var property in properties)
        {
            if (!PropertyTargetsTextureSlot(property.Name, slot))
            {
                continue;
            }

            var parameter = new ShaderParameterProfile(
                property.Hash,
                property.Name,
                0,
                0,
                property.Category);

            if (property.ValueRepresentation == MaterialValueRepresentation.Scalar &&
                property.FloatValues is { Length: > 0 } scalarValues &&
                Ts4ShaderSemantics.TryInterpretDiffuseUvMappingScalar(parameter, scalarValues[0], mapping, out var scalarMapping))
            {
                mapping = scalarMapping;
                continue;
            }

            if (property.ValueRepresentation == MaterialValueRepresentation.FloatVector &&
                property.FloatValues is { Length: > 0 } vectorValues &&
                Ts4ShaderSemantics.TryInterpretDiffuseUvMappingVector(parameter, vectorValues, mapping, out var vectorMapping))
            {
                mapping = vectorMapping;
                continue;
            }

            if (property.ValueRepresentation == MaterialValueRepresentation.PackedUInt32 &&
                PropertyLooksLikeUvInstruction(property.Name) &&
                TryInterpretPackedUvProperty(property, mapping, out var packedMapping, out _))
            {
                mapping = packedMapping;
                continue;
            }
        }

        return mapping;
    }

    private static (string Source, bool IsApproximate) DescribeSamplingSource(
        string slot,
        bool usesDiffuse,
        IReadOnlyList<MaterialIrProperty> properties,
        string profileName,
        string familyName,
        Ts4MaterialCoverageTier coverageTier)
    {
        var relevantProperties = GetRelevantSamplingProperties(slot, usesDiffuse, properties);
        if (relevantProperties.Any(static property => IsStrongProjectiveUvSemantic(property.Name)) ||
            coverageTier == Ts4MaterialCoverageTier.RuntimeDependent && (LooksLikeProjectiveFamilyName(profileName) || LooksLikeProjectiveFamilyName(familyName)))
        {
            return ("projective-uv-approximation", true);
        }

        var animatedProperties = relevantProperties
            .Where(static property => IsAnimatedUvSemantic(property.Name))
            .Select(static property => property.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (animatedProperties.Any(static name => !IsWeakAnimatedSemantic(name)) ||
            (animatedProperties.Length > 0 &&
             coverageTier != Ts4MaterialCoverageTier.StaticReady &&
             (LooksLikeAnimatedFamilyName(profileName) || LooksLikeAnimatedFamilyName(familyName))))
        {
            return ("animated-still-frame", true);
        }

        return usesDiffuse
            ? ("diffuse-material-path", false)
            : ("default-uv0", true);
    }

    private static IEnumerable<MaterialIrProperty> GetRelevantSamplingProperties(
        string slot,
        bool usesDiffuse,
        IReadOnlyList<MaterialIrProperty> properties)
    {
        foreach (var property in properties)
        {
            if (PropertyTargetsTextureSlot(property.Name, slot))
            {
                yield return property;
                continue;
            }

            if (usesDiffuse &&
                (PropertyLooksLikeUvInstruction(property.Name) ||
                 IsAnimatedUvSemantic(property.Name) ||
                 IsProjectiveUvSemantic(property.Name)) &&
                !LooksSlotQualified(property.Name))
            {
                yield return property;
            }
        }
    }

    private static bool PropertyTargetsTextureSlot(string propertyName, string slot)
    {
        if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(slot))
        {
            return false;
        }

        var normalizedSlot = slot.ToLowerInvariant();
        return normalizedSlot switch
        {
            "normal" => propertyName.Contains("Normal", StringComparison.OrdinalIgnoreCase),
            "specular" => propertyName.Contains("Spec", StringComparison.OrdinalIgnoreCase),
            "emissive" => propertyName.Contains("Emiss", StringComparison.OrdinalIgnoreCase),
            "overlay" =>
                propertyName.Contains("Overlay", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Ramp", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Paint", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Variation", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("FoliageColor", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("WallTopBottomShadow", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("GhostNoise", StringComparison.OrdinalIgnoreCase),
            "detail" => propertyName.Contains("Detail", StringComparison.OrdinalIgnoreCase),
            "decal" =>
                propertyName.Contains("Decal", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("LotPaint", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Mural", StringComparison.OrdinalIgnoreCase),
            "dirt" =>
                propertyName.Contains("Dirt", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Grime", StringComparison.OrdinalIgnoreCase),
            "alpha" or "opacity" or "mask" or "cutout" =>
                propertyName.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Opacity", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Cutout", StringComparison.OrdinalIgnoreCase),
            "diffuse" or "basecolor" or "albedo" =>
                propertyName.Contains("Diffuse", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("BaseColor", StringComparison.OrdinalIgnoreCase) ||
                propertyName.Contains("Albedo", StringComparison.OrdinalIgnoreCase),
            _ => propertyName.Contains(slot, StringComparison.OrdinalIgnoreCase)
        };
    }

    private static bool PropertyLooksLikeUvInstruction(string propertyName) =>
        propertyName.Contains("UV", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Atlas", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("MapRect", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(propertyName, "uvMapping", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeColorSemantic(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName) ||
            PropertyLooksLikeUvInstruction(propertyName) ||
            IsAnimatedUvSemantic(propertyName) ||
            IsProjectiveUvSemantic(propertyName))
        {
            return false;
        }

        return propertyName.Contains("Color", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Tint", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Diffuse", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("BaseColor", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("Albedo", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("SeasonalVariation", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Contains("VariationColor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractApproximateColor(MaterialIrProperty property, out float[] color)
    {
        color = Array.Empty<float>();
        if (property.ValueRepresentation != MaterialValueRepresentation.FloatVector ||
            property.FloatValues is not { Length: >= 3 } values)
        {
            return false;
        }

        var candidate = values.Length >= 4
            ? new[] { values[0], values[1], values[2], values[3] }
            : new[] { values[0], values[1], values[2], 1f };

        for (var index = 0; index < candidate.Length; index++)
        {
            var minValue = index == 3 ? -0.05f : -0.001f;
            if (!float.IsFinite(candidate[index]) || candidate[index] < minValue || candidate[index] > 4f)
            {
                return false;
            }
        }

        var rgb = candidate[..3];
        if (rgb.All(static component => component < 0.001f))
        {
            return false;
        }

        if (rgb.Any(static component => component > 1.5f))
        {
            return false;
        }

        candidate[0] = Math.Clamp(candidate[0], 0f, 1f);
        candidate[1] = Math.Clamp(candidate[1], 0f, 1f);
        candidate[2] = Math.Clamp(candidate[2], 0f, 1f);
        candidate[3] = Math.Clamp(candidate[3], 0f, 1f);
        color = candidate;
        return true;
    }

    private static int ScoreColorCandidate(string propertyName, ShaderParameterCategory category)
    {
        var score = category is ShaderParameterCategory.Vector3 or ShaderParameterCategory.Vector4 ? 20 : 0;
        if (propertyName.Contains("BaseColor", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("DiffuseColor", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Albedo", StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }
        else if (propertyName.Contains("Tint", StringComparison.OrdinalIgnoreCase))
        {
            score += 80;
        }
        else if (propertyName.Contains("FoliageColor", StringComparison.OrdinalIgnoreCase) ||
                 propertyName.Contains("SeasonalColor", StringComparison.OrdinalIgnoreCase) ||
                 propertyName.Contains("VariationColor", StringComparison.OrdinalIgnoreCase))
        {
            score += 70;
        }
        else if (propertyName.Contains("Color", StringComparison.OrdinalIgnoreCase))
        {
            score += 60;
        }

        if (propertyName.Contains("Spec", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Emiss", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Shadow", StringComparison.OrdinalIgnoreCase) ||
            propertyName.Contains("Fog", StringComparison.OrdinalIgnoreCase))
        {
            score -= 50;
        }

        return score;
    }

    private static bool LooksLikeFallbackColorSemantic(MaterialIrProperty property)
    {
        if (property.ValueRepresentation != MaterialValueRepresentation.FloatVector ||
            property.FloatValues is not { Length: >= 3 })
        {
            return false;
        }

        if (PropertyLooksLikeUvInstruction(property.Name) ||
            IsAnimatedUvSemantic(property.Name) ||
            IsProjectiveUvSemantic(property.Name) ||
            property.Category is ShaderParameterCategory.Sampler or ShaderParameterCategory.UvMapping or ShaderParameterCategory.ResourceKey)
        {
            return false;
        }

        var name = property.Name;
        return name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Prop_", StringComparison.OrdinalIgnoreCase) ||
               property.Category is ShaderParameterCategory.Vector3 or ShaderParameterCategory.Vector4 or ShaderParameterCategory.Unknown;
    }

    private static int ScoreFallbackColorCandidate(ShaderParameterCategory category, float[] color)
    {
        var score = category is ShaderParameterCategory.Vector3 or ShaderParameterCategory.Vector4 ? 15 : 5;
        var rgb = color[..3];
        if (rgb.DistinctBy(static component => MathF.Round(component, 3)).Count() > 1)
        {
            score += 5;
        }

        if (rgb.All(static component => component >= 0f && component <= 1f))
        {
            score += 5;
        }

        return score;
    }

    private static bool LooksSlotQualified(string propertyName) =>
        propertyName.Contains("Diffuse", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("BaseColor", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Albedo", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Normal", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Spec", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Emiss", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Overlay", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Detail", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Decal", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Dirt", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Grime", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Alpha", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Opacity", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Mask", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Cutout", StringComparison.OrdinalIgnoreCase);

    private static bool IsAnimatedUvSemantic(string propertyName) =>
        propertyName.Contains("Scroll", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("VideoVTexture", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Caustics", StringComparison.OrdinalIgnoreCase);

    private static bool IsWeakAnimatedSemantic(string propertyName) =>
        propertyName.Contains("WaterScrollSpeedLayer", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAnimatedFamilyName(string profileOrFamilyName) =>
        profileOrFamilyName.Contains("Video", StringComparison.OrdinalIgnoreCase) ||
        profileOrFamilyName.Contains("Caustic", StringComparison.OrdinalIgnoreCase) ||
        profileOrFamilyName.Contains("Scroll", StringComparison.OrdinalIgnoreCase) ||
        profileOrFamilyName.Contains("Water", StringComparison.OrdinalIgnoreCase);

    private static bool IsProjectiveUvSemantic(string propertyName) =>
        propertyName.Contains("ClipSpace", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("DepthMapSpace", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("WorldToDepthMapSpace", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("gPosToUVDest", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("gPosToUVSrc", StringComparison.OrdinalIgnoreCase);

    private static bool IsStrongProjectiveUvSemantic(string propertyName) =>
        propertyName.Contains("gPosToUVDest", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("gPosToUVSrc", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("DepthMapSpace", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("WorldToDepthMapSpace", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeProjectiveFamilyName(string profileOrFamilyName) =>
        profileOrFamilyName.Contains("Project", StringComparison.OrdinalIgnoreCase) ||
        profileOrFamilyName.Contains("ClipSpace", StringComparison.OrdinalIgnoreCase) ||
        profileOrFamilyName.Contains("DepthMapSpace", StringComparison.OrdinalIgnoreCase) ||
        profileOrFamilyName.Contains("WorldToDepthMapSpace", StringComparison.OrdinalIgnoreCase);

    private static bool IsLayeredColorSlotName(string slot) =>
        slot.Equals("overlay", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("emissive", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("detail", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("decal", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("dirt", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("grime", StringComparison.OrdinalIgnoreCase);

    private static bool IsUtilityTextureSlotName(string slot) =>
        slot.Equals("normal", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("specular", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("gloss", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("roughness", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("smoothness", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("metallic", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("ao", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("occlusion", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("height", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("displacement", StringComparison.OrdinalIgnoreCase);

    internal static bool TryInterpretPackedUvMapping(MaterialIrProperty property, Ts4TextureUvMapping current, out Ts4TextureUvMapping updated, out string note) =>
        TryInterpretPackedUvProperty(property, current, out updated, out note);

    internal static bool TryInterpretPackedUvProperty(MaterialIrProperty property, Ts4TextureUvMapping current, out Ts4TextureUvMapping updated, out string note)
    {
        updated = current;
        note = string.Empty;
        var parameter = new ShaderParameterProfile(
            property.Hash,
            property.Name,
            0,
            0,
            property.Category);

        if (!TryDecodePackedUvVector(property, out var packedVector, out var encoding))
        {
            return false;
        }

        if (Ts4ShaderSemantics.TryInterpretDiffuseUvMappingVector(parameter, packedVector, current, out var semanticMapping))
        {
            updated = semanticMapping;
            note = $"Packed {property.Name} decoded from {encoding} window.";
            return true;
        }

        if (packedVector.Length >= 4 &&
            string.Equals(property.Name, "uvMapping", StringComparison.OrdinalIgnoreCase))
        {
            updated = current with
            {
                UvScaleU = packedVector[0],
                UvScaleV = packedVector[1],
                UvOffsetU = packedVector[2],
                UvOffsetV = packedVector[3]
            };
            note = $"Packed uvMapping decoded from {encoding} window -> scale=({packedVector[0]:0.###}, {packedVector[1]:0.###}) offset=({packedVector[2]:0.###}, {packedVector[3]:0.###}).";
            return true;
        }

        return false;
    }

    private static bool TryDecodePackedUvVector(MaterialIrProperty property, out float[] vector, out string encoding)
    {
        vector = [];
        encoding = string.Empty;
        if (property.PackedUInt32Values is not { Length: > 0 } packed)
        {
            return false;
        }

        var expectedLength = property.Arity >= 4
            ? 4
            : property.Arity == 3
                ? 3
                : property.Arity == 2
                    ? 2
                    : 4;

        if (TryExtractPlausibleUvVector(EnumerateHalfFloatWindows(packed, expectedLength), expectedLength, out var halfVector))
        {
            vector = halfVector;
            encoding = "half-float";
            return true;
        }

        if (TryExtractPlausibleUvVector(EnumerateNormalizedUInt16Windows(packed, expectedLength), expectedLength, out var normalizedVector))
        {
            vector = normalizedVector;
            encoding = "normalized-uint16";
            return true;
        }

        return false;
    }

    private static bool TryBuildProjectiveStillMapping(
        MaterialIr material,
        Ts4TextureUvMapping current,
        out Ts4TextureUvMapping updated,
        out string note)
    {
        updated = current;
        note = string.Empty;

        if (!current.IsIdentity)
        {
            return false;
        }

        var uvMapping = material.FindProperty("uvMapping");
        var atlasSampler = material.FindProperty("samplerCASMedatorGridTexture");
        if (atlasSampler?.FloatValues is not { Length: >= 4 } ||
            uvMapping?.FloatValues is not { Length: >= 4 } uvValues)
        {
            return false;
        }

        var mapping = current;
        var changed = false;

        if (atlasSampler?.FloatValues is { Length: >= 4 } samplerValues)
        {
            if (mapping.UvScaleU == 1f &&
                IsNormalizedWindowValue(samplerValues[0], requireNonZero: true))
            {
                mapping = mapping with { UvScaleU = samplerValues[0] };
                changed = true;
            }

            if (mapping.UvScaleV == 1f &&
                IsNormalizedWindowValue(samplerValues[1], requireNonZero: true))
            {
                mapping = mapping with { UvScaleV = samplerValues[1] };
                changed = true;
            }

            if (mapping.UvOffsetU == 0f &&
                IsNormalizedWindowOffsetValue(samplerValues[2]))
            {
                mapping = mapping with { UvOffsetU = samplerValues[2] };
                changed = true;
            }

            if (mapping.UvOffsetV == 0f &&
                IsNormalizedWindowOffsetValue(samplerValues[3]))
            {
                mapping = mapping with { UvOffsetV = samplerValues[3] };
                changed = true;
            }
        }

        if (uvValues is { Length: >= 4 } vectorValues)
        {
            if (mapping.UvScaleU == 1f &&
                IsNormalizedWindowValue(vectorValues[0], requireNonZero: true))
            {
                mapping = mapping with { UvScaleU = vectorValues[0] };
                changed = true;
            }

            if (mapping.UvScaleV == 1f &&
                IsNormalizedWindowValue(vectorValues[1], requireNonZero: true))
            {
                mapping = mapping with { UvScaleV = vectorValues[1] };
                changed = true;
            }

            if (mapping.UvOffsetU == 0f &&
                IsNormalizedWindowOffsetValue(vectorValues[2]))
            {
                mapping = mapping with { UvOffsetU = vectorValues[2] };
                changed = true;
            }

            if (mapping.UvOffsetV == 0f &&
                IsNormalizedWindowOffsetValue(vectorValues[3]))
            {
                mapping = mapping with { UvOffsetV = vectorValues[3] };
                changed = true;
            }
        }

        if (!changed || mapping.IsIdentity)
        {
            return false;
        }

        updated = mapping;
        note = FormattableString.Invariant(
            $"Projective family fell back to a still atlas approximation -> UV{mapping.UvChannel} scale=({mapping.UvScaleU:0.###}, {mapping.UvScaleV:0.###}) offset=({mapping.UvOffsetU:0.###}, {mapping.UvOffsetV:0.###}).");
        return true;
    }

    private static bool IsNormalizedWindowValue(float value, bool requireNonZero) =>
        float.IsFinite(value) &&
        value >= 0f &&
        value <= 1f &&
        (!requireNonZero || value > 0.0001f);

    private static bool IsNormalizedWindowOffsetValue(float value) =>
        float.IsFinite(value) &&
        value >= 0f &&
        value < 1f;

    private static IEnumerable<float[]> EnumerateHalfFloatWindows(uint[] packed, int windowSize)
    {
        var halves = UnpackUInt16Values(packed);
        if (halves.Length < windowSize)
        {
            yield break;
        }

        var values = new float[halves.Length];
        for (var i = 0; i < halves.Length; i++)
        {
            values[i] = ConvertHalfToSingle(halves[i]);
        }

        for (var i = 0; i + windowSize - 1 < values.Length; i++)
        {
            var slice = new float[windowSize];
            Array.Copy(values, i, slice, 0, windowSize);
            yield return slice;
        }
    }

    private static IEnumerable<float[]> EnumerateNormalizedUInt16Windows(uint[] packed, int windowSize)
    {
        var halves = UnpackUInt16Values(packed);
        if (halves.Length < windowSize)
        {
            yield break;
        }

        var values = new float[halves.Length];
        for (var i = 0; i < halves.Length; i++)
        {
            values[i] = halves[i] / 65535f;
        }

        for (var i = 0; i + windowSize - 1 < values.Length; i++)
        {
            var slice = new float[windowSize];
            Array.Copy(values, i, slice, 0, windowSize);
            yield return slice;
        }
    }

    private static ushort[] UnpackUInt16Values(uint[] packed)
    {
        var values = new ushort[packed.Length * 2];
        for (var i = 0; i < packed.Length; i++)
        {
            values[i * 2] = (ushort)(packed[i] & 0xFFFF);
            values[(i * 2) + 1] = (ushort)(packed[i] >> 16);
        }

        return values;
    }

    private static bool TryExtractPlausibleUvVector(IEnumerable<float[]> candidates, int expectedLength, out float[] vector)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.Length < expectedLength)
            {
                continue;
            }

            if (!IsPlausiblePackedUvValue(candidate[0], requirePositive: expectedLength >= 4) ||
                !IsPlausiblePackedUvValue(candidate[1], requirePositive: expectedLength >= 4))
            {
                continue;
            }

            if (expectedLength >= 3 &&
                !IsPlausiblePackedUvValue(candidate[2], requirePositive: false))
            {
                continue;
            }

            if (expectedLength >= 4 &&
                !IsPlausiblePackedUvValue(candidate[3], requirePositive: false))
            {
                continue;
            }

            if (expectedLength >= 4 &&
                (candidate[0] <= 0.0001f || candidate[1] <= 0.0001f))
            {
                continue;
            }

            vector = candidate;
            return true;
        }

        vector = [];
        return false;
    }

    private static bool IsPlausiblePackedUvValue(float value, bool requirePositive)
    {
        if (!float.IsFinite(value))
        {
            return false;
        }

        if (requirePositive && value < 0f)
        {
            return false;
        }

        return MathF.Abs(value) <= 8f;
    }

    private static float ConvertHalfToSingle(ushort value)
    {
        var sign = (value >> 15) & 0x1;
        var exponent = (value >> 10) & 0x1F;
        var fraction = value & 0x03FF;

        if (exponent == 0)
        {
            if (fraction == 0)
            {
                return sign == 0 ? 0f : -0f;
            }

            return (float)((sign == 0 ? 1.0 : -1.0) * Math.Pow(2, -14) * (fraction / 1024.0));
        }

        if (exponent == 31)
        {
            return fraction == 0
                ? (sign == 0 ? float.PositiveInfinity : float.NegativeInfinity)
                : float.NaN;
        }

        return (float)((sign == 0 ? 1.0 : -1.0) * Math.Pow(2, exponent - 15) * (1.0 + (fraction / 1024.0)));
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

internal sealed class AlphaCutoutMaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(AlphaCutoutMaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.AlphaCutout;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.IsAlphaCutoutFamily(familyName, profile?.Name ?? familyName, material, profile);

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.DecodeGeneric(
            profileName,
            familyName,
            FamilyKind,
            Name,
            material,
            profile,
            forceAlphaCutout: true,
            forcedAlphaReason: "Profile family indicates alpha-tested or cutout surface handling.");
}

internal sealed class ProjectiveMaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(ProjectiveMaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.Projective;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.IsProjectiveFamily(familyName, profile?.Name ?? familyName, material);

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile)
    {
        var state = Ts4MaterialDecoder.DecodeGeneric(
            profileName,
            familyName,
            FamilyKind,
            Name,
            material,
            profile).ToState();
        state = Ts4MaterialDecoder.ApplyProjectiveStillApproximation(state, material);
        return Ts4MaterialDecoder.BuildResult(profileName, familyName, FamilyKind, Name, state);
    }
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
        var state = Ts4MaterialDecoder.ApplyLegacyColorMapRules(
            Ts4MaterialDecoder.DecodeGeneric(profileName, familyName, FamilyKind, Name, material, profile).ToState(),
            material);
        return Ts4MaterialDecoder.BuildResult(
            profileName,
            familyName,
            FamilyKind,
            Name,
            state);
    }
}

internal sealed class ColorMapMaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(ColorMapMaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.ColorMap;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.IsColorMapFamily(familyName, profile?.Name ?? familyName);

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile)
    {
        var state = Ts4MaterialDecoder.ApplyLegacyColorMapRules(
            Ts4MaterialDecoder.DecodeGeneric(profileName, familyName, FamilyKind, Name, material, profile).ToState(),
            material);
        return Ts4MaterialDecoder.BuildResult(profileName, familyName, FamilyKind, Name, state);
    }
}

internal sealed class StandardSurfaceMaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(StandardSurfaceMaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.StandardSurface;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.IsStandardSurfaceFamily(familyName, profile?.Name ?? familyName);

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.DecodeGeneric(profileName, familyName, FamilyKind, Name, material, profile);
}

internal sealed class DefaultMaterialDecodeStrategy : ITs4MaterialDecodeStrategy
{
    public string Name => nameof(DefaultMaterialDecodeStrategy);
    public Ts4MaterialFamilyKind FamilyKind => Ts4MaterialFamilyKind.Unknown;

    public bool CanHandle(string familyName, MaterialIr material, ShaderBlockProfile? profile) => true;

    public Ts4MaterialDecodeResult Decode(string profileName, string familyName, MaterialIr material, ShaderBlockProfile? profile) =>
        Ts4MaterialDecoder.DecodeGeneric(profileName, familyName, FamilyKind, Name, material, profile);
}
