namespace Sims4ResourceExplorer.Preview;

internal enum ShaderParameterCategory
{
    Unknown = 0,
    Sampler = 1,
    Scalar = 2,
    Vector2 = 3,
    Vector3 = 4,
    Vector4 = 5,
    ResourceKey = 6,
    UvMapping = 7,
    BoolLike = 8
}

internal sealed record ShaderParameterProfile(
    uint Hash,
    string Name,
    uint PackedType,
    uint Flags,
    ShaderParameterCategory Category);

internal sealed record ShaderBlockProfile(
    uint Hash,
    string Name,
    IReadOnlyList<ShaderParameterProfile> Parameters);

internal enum MaterialValueRepresentation
{
    None = 0,
    Scalar = 1,
    FloatVector = 2,
    PackedUInt32 = 3,
    ResourceKey = 4
}

internal sealed record MaterialIrProperty(
    uint Hash,
    string Name,
    uint RawType,
    uint Type,
    uint Arity,
    ShaderParameterCategory Category,
    MaterialValueRepresentation ValueRepresentation,
    string? ValueSummary,
    float[]? FloatValues,
    uint[]? PackedUInt32Values,
    Ts4ResourceKey? ResourceKeyValue);

internal sealed record MaterialIr(
    string MaterialName,
    string ShaderName,
    IReadOnlyList<MaterialIrProperty> Properties,
    IReadOnlyList<Ts4TextureReference> TextureReferences,
    Ts4TextureUvMapping DiffuseUvMapping);

internal static class MaterialIrExtensions
{
    public static MaterialIrProperty? FindProperty(this MaterialIr material, string name) =>
        material.Properties.FirstOrDefault(property => string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<MaterialIrProperty> FindPropertiesContaining(this MaterialIr material, string fragment) =>
        material.Properties.Where(property => property.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

internal static class Ts4ShaderSemantics
{
    public static ShaderParameterProfile? ResolveParameterProfile(uint propertyHash, ShaderBlockProfile? profile) =>
        profile?.Parameters.FirstOrDefault(parameter => parameter.Hash == propertyHash);

    public static string ResolveMaterialPropertyName(Ts4MatdProperty property, ShaderBlockProfile? profile)
    {
        var match = ResolveParameterProfile(property.Hash, profile);
        if (match is not null)
        {
            return match.Name;
        }

        if (Ts4ShaderProfileRegistry.Instance.TryGetGlobalParameterAlias(property.Hash, out var alias))
        {
            return alias;
        }

        return $"Prop_{property.Hash:X8}";
    }

    public static ShaderParameterCategory ClassifyMatdProperty(uint normalizedType, uint arity) =>
        normalizedType switch
        {
            2 => ShaderParameterCategory.ResourceKey,
            4 => arity switch
            {
                2 => ShaderParameterCategory.Vector2,
                3 => ShaderParameterCategory.Vector3,
                _ => ShaderParameterCategory.Vector4
            },
            1 => arity switch
            {
                1 => ShaderParameterCategory.Scalar,
                2 => ShaderParameterCategory.Vector2,
                3 => ShaderParameterCategory.Vector3,
                _ => ShaderParameterCategory.Vector4
            },
            _ => ShaderParameterCategory.Unknown
        };

    public static ShaderParameterCategory ClassifyPackedParameter(string name, uint packedType, uint flags)
    {
        if (name.Contains("uv", StringComparison.OrdinalIgnoreCase))
        {
            return ShaderParameterCategory.UvMapping;
        }

        if (name.StartsWith("sampler", StringComparison.OrdinalIgnoreCase))
        {
            return ShaderParameterCategory.Sampler;
        }

        var lowWord = packedType & 0xFFFF;
        var midByte = (packedType >> 16) & 0xFF;
        if (lowWord == 0x0601 || lowWord == 0x0602)
        {
            return ShaderParameterCategory.Sampler;
        }

        if ((flags & 0x80000000) != 0 && (flags & 0x0000F000) == 0x00004000)
        {
            return midByte switch
            {
                <= 2 => ShaderParameterCategory.Vector2,
                3 => ShaderParameterCategory.Vector3,
                _ => ShaderParameterCategory.Vector4
            };
        }

        if ((flags & 0x0000F000) == 0x00001000)
        {
            return ShaderParameterCategory.Scalar;
        }

        if ((flags & 0x0000F000) == 0x00003000)
        {
            return ShaderParameterCategory.BoolLike;
        }

        return ShaderParameterCategory.Unknown;
    }

    public static MaterialIr BuildMaterialIr(Ts4MatdChunk chunk)
    {
        Ts4ShaderProfileRegistry.Instance.TryGetProfile(chunk.ShaderNameHash, out var profile);
        var properties = chunk.Properties
            .Select(property => new MaterialIrProperty(
                property.Hash,
                ResolveMaterialPropertyName(property, profile),
                property.RawType,
                property.Type,
                property.Arity,
                property.Category,
                property.ValueRepresentation,
                property.ValueSummary,
                property.FloatValues,
                property.PackedUInt32Values,
                property.ResourceKeyValue))
            .ToArray();

        return new MaterialIr(
            chunk.MaterialName,
            chunk.ShaderName,
            properties,
            chunk.TextureReferences,
            chunk.DiffuseUvMapping);
    }

    public static string ResolveTextureSlotName(Ts4TextureReference reference, ShaderBlockProfile? profile)
    {
        var profileParameter = ResolveParameterProfile(reference.PropertyHash, profile);
        if (profileParameter is null)
        {
            if (TryResolveFamilyFallbackTextureSlot(reference, profile, out var familySlot))
            {
                return familySlot;
            }

            if (ShouldTreatAsWriteDepthMaskAlpha(reference, profile))
            {
                return "alpha";
            }

            return reference.Slot;
        }

        var name = profileParameter.Name;
        if (name.StartsWith("sampler", StringComparison.OrdinalIgnoreCase))
        {
            name = name["sampler".Length..];
        }

        var normalized = name.ToLowerInvariant();
        if (normalized.Contains("diffuse") || normalized.Contains("albedo") || normalized.Contains("basecolor"))
        {
            return "diffuse";
        }

        if (normalized.Contains("sourcetexture"))
        {
            return "diffuse";
        }

        if (normalized.Contains("normal"))
        {
            return "normal";
        }

        if (normalized.Contains("detailnormal"))
        {
            return "normal";
        }

        if (normalized.Contains("rough") || normalized.Contains("gloss") || normalized.Contains("smooth") || normalized.Contains("metal"))
        {
            return "specular";
        }

        if (normalized.Contains("envcube") || normalized.Contains("cubemap"))
        {
            return "specular";
        }

        if (normalized.Contains("reflection"))
        {
            return "specular";
        }

        if (normalized.Contains("spec"))
        {
            return "specular";
        }

        if (normalized.Contains("alpha") || normalized.Contains("opacity") || normalized.Contains("cutout") || normalized.Contains("mask"))
        {
            return "alpha";
        }

        if (normalized.Contains("routingmap"))
        {
            return "alpha";
        }

        if (normalized.Contains("emission") || normalized.Contains("emissive"))
        {
            return "emissive";
        }

        if (normalized.Contains("suntexture"))
        {
            return "emissive";
        }

        if (normalized.Contains("overlay"))
        {
            return "overlay";
        }

        if (normalized.Contains("detail"))
        {
            return "detail";
        }

        if (normalized.Contains("decal") || normalized.Contains("lotpaint") || normalized.Contains("mural"))
        {
            return "decal";
        }

        if (normalized.Contains("dirt") || normalized.Contains("grime"))
        {
            return "dirt";
        }

        if (normalized.Contains("ramp") ||
            normalized.Contains("paint") ||
            normalized.Contains("variation") ||
            normalized.Contains("grid") ||
            normalized.Contains("foliagecolor") ||
            normalized.Contains("lightmap") ||
            normalized.Contains("walltopbottomshadow") ||
            normalized.Contains("ghostnoise"))
        {
            return "overlay";
        }

        if (ShouldTreatAsWriteDepthMaskAlpha(reference, profile))
        {
            return "alpha";
        }

        if (TryResolveFamilyFallbackTextureSlot(reference, profile, out var fallbackSlot))
        {
            return fallbackSlot;
        }

        return reference.Slot;
    }

    private static bool TryResolveFamilyFallbackTextureSlot(Ts4TextureReference reference, ShaderBlockProfile? profile, out string slot)
    {
        slot = string.Empty;
        if (profile is null)
        {
            return false;
        }

        if (!reference.Slot.StartsWith("texture_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (profile.Name.Contains("DecalMap", StringComparison.OrdinalIgnoreCase))
        {
            slot = reference.Slot.Equals("texture_1", StringComparison.OrdinalIgnoreCase)
                ? "alpha"
                : "overlay";
            return true;
        }

        if (profile.Name.Contains("ShaderDayNightParameters", StringComparison.OrdinalIgnoreCase))
        {
            slot = reference.Slot.Equals("texture_1", StringComparison.OrdinalIgnoreCase)
                ? "emissive"
                : "overlay";
            return true;
        }

        if (profile.Name.Contains("UseVertAlpha", StringComparison.OrdinalIgnoreCase))
        {
            slot = reference.Slot.Equals("texture_1", StringComparison.OrdinalIgnoreCase)
                ? "decal"
                : "overlay";
            return true;
        }

        if (profile.Name.Contains("WorldToDepthMapSpaceMatrix", StringComparison.OrdinalIgnoreCase))
        {
            slot = reference.Slot.Equals("texture_1", StringComparison.OrdinalIgnoreCase)
                ? "alpha"
                : "overlay";
            return true;
        }

        if (profile.Name.Contains("colorMap", StringComparison.OrdinalIgnoreCase))
        {
            slot = "alpha";
            return true;
        }

        return false;
    }

    private static bool ShouldTreatAsWriteDepthMaskAlpha(Ts4TextureReference reference, ShaderBlockProfile? profile) =>
        profile is not null &&
        profile.Name.Contains("WriteDepthMask", StringComparison.OrdinalIgnoreCase) &&
        reference.Slot.StartsWith("texture_", StringComparison.OrdinalIgnoreCase);

    public static bool IsLikelyTextureParameter(uint normalizedPropertyType, uint propertyArity, ShaderParameterProfile? parameter)
    {
        if (parameter is null)
        {
            return false;
        }

        if (parameter.Category == ShaderParameterCategory.Sampler)
        {
            return true;
        }

        if (parameter.Category == ShaderParameterCategory.UvMapping)
        {
            return false;
        }

        var name = parameter.Name;
        if (parameter.Category == ShaderParameterCategory.ResourceKey)
        {
            return IsTextureSemanticName(name);
        }

        if (IsTextureSemanticName(name))
        {
            return normalizedPropertyType is 1 or 2 or 4 && propertyArity >= 1;
        }

        return false;
    }

    private static bool IsTextureSemanticName(string name) =>
        !name.Contains("Scale", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Offset", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Tileable", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Bias", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Strength", StringComparison.OrdinalIgnoreCase) &&
        !name.Contains("Intensity", StringComparison.OrdinalIgnoreCase) &&
        (name.StartsWith("sampler", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("texture", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("map", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("diffuse", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("normal", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("spec", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("overlay", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("detail", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("decal", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("emiss", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("ramp", StringComparison.OrdinalIgnoreCase) ||
         name.Contains("paint", StringComparison.OrdinalIgnoreCase));

    public static bool TryInterpretDiffuseUvMappingScalar(
        ShaderParameterProfile? parameter,
        float scalarValue,
        Ts4TextureUvMapping current,
        out Ts4TextureUvMapping updated)
    {
        updated = current;
        if (parameter is null)
        {
            return false;
        }

        var name = parameter.Name;
        if (name.Contains("UsesUV1", StringComparison.OrdinalIgnoreCase))
        {
            if (MathF.Abs(scalarValue) >= 0.5f)
            {
                updated = current with { UvChannel = 1 };
                return true;
            }

            updated = current with { UvChannel = 0 };
            return true;
        }

        if (name.Contains("UVChannel", StringComparison.OrdinalIgnoreCase))
        {
            var rounded = (int)MathF.Round(scalarValue);
            if (rounded is >= 0 and <= 3)
            {
                updated = current with { UvChannel = rounded };
                return true;
            }
        }

        if ((name.Contains("UVScaleU", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("UScale", StringComparison.OrdinalIgnoreCase)) &&
            IsPlausibleUvMagnitude(scalarValue) &&
            scalarValue != 0f)
        {
            updated = current with { UvScaleU = Math.Clamp(MathF.Abs(scalarValue), 0.001f, 1024f) };
            return true;
        }

        if ((name.Contains("UVScaleV", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("VScale", StringComparison.OrdinalIgnoreCase)) &&
            IsPlausibleUvMagnitude(scalarValue) &&
            scalarValue != 0f)
        {
            updated = current with { UvScaleV = Math.Clamp(MathF.Abs(scalarValue), 0.001f, 1024f) };
            return true;
        }

        if (name.Contains("UVScale", StringComparison.OrdinalIgnoreCase) && IsPlausibleUvMagnitude(scalarValue) && scalarValue != 0f)
        {
            var scale = Math.Clamp(MathF.Abs(scalarValue), 0.001f, 1024f);
            updated = current with { UvScaleU = scale, UvScaleV = scale };
            return true;
        }

        if ((name.Contains("UVOffsetU", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("UOffset", StringComparison.OrdinalIgnoreCase)) &&
            IsPlausibleUvMagnitude(scalarValue, allowNegative: true))
        {
            updated = current with { UvOffsetU = scalarValue };
            return true;
        }

        if ((name.Contains("UVOffsetV", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("VOffset", StringComparison.OrdinalIgnoreCase)) &&
            IsPlausibleUvMagnitude(scalarValue, allowNegative: true))
        {
            updated = current with { UvOffsetV = scalarValue };
            return true;
        }

        return false;
    }

    public static bool TryInterpretDiffuseUvMappingVector(
        ShaderParameterProfile? parameter,
        float[] values,
        Ts4TextureUvMapping current,
        out Ts4TextureUvMapping updated) =>
        TryInterpretDiffuseUvMappingVector(parameter, values.AsSpan(), current, out updated);

    public static bool TryInterpretDiffuseUvMappingVector(
        ShaderParameterProfile? parameter,
        ReadOnlySpan<float> values,
        Ts4TextureUvMapping current,
        out Ts4TextureUvMapping updated)
    {
        updated = current;
        if (parameter is null || values.Length == 0)
        {
            return false;
        }

        var name = parameter.Name;
        if ((name.Contains("ScaleAndOffset", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("ScaleOffset", StringComparison.OrdinalIgnoreCase)) &&
            values.Length >= 4 &&
            IsPlausibleUvMagnitude(values[0]) &&
            IsPlausibleUvMagnitude(values[1]) &&
            IsPlausibleUvMagnitude(values[2], allowNegative: true) &&
            IsPlausibleUvMagnitude(values[3], allowNegative: true) &&
            values[0] != 0f &&
            values[1] != 0f)
        {
            updated = current with
            {
                UvScaleU = Math.Clamp(MathF.Abs(values[0]), 0.001f, 1024f),
                UvScaleV = Math.Clamp(MathF.Abs(values[1]), 0.001f, 1024f),
                UvOffsetU = values[2],
                UvOffsetV = values[3]
            };
            return true;
        }

        if ((name.Contains("AtlasMin", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("MapMin", StringComparison.OrdinalIgnoreCase)) &&
            values.Length >= 2 &&
            IsPlausibleUvMagnitude(values[0], allowNegative: true) &&
            IsPlausibleUvMagnitude(values[1], allowNegative: true))
        {
            updated = current with
            {
                UvOffsetU = values[0],
                UvOffsetV = values[1]
            };
            return true;
        }

        if ((name.Contains("AtlasMax", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("MapMax", StringComparison.OrdinalIgnoreCase)) &&
            values.Length >= 2 &&
            IsPlausibleUvMagnitude(values[0], allowNegative: true) &&
            IsPlausibleUvMagnitude(values[1], allowNegative: true) &&
            values[0] != current.UvOffsetU &&
            values[1] != current.UvOffsetV)
        {
            var scaleU = values[0] - current.UvOffsetU;
            var scaleV = values[1] - current.UvOffsetV;
            if (IsPlausibleUvMagnitude(scaleU, allowNegative: true) &&
                IsPlausibleUvMagnitude(scaleV, allowNegative: true) &&
                MathF.Abs(scaleU) > 0.0001f &&
                MathF.Abs(scaleV) > 0.0001f)
            {
                updated = current with
                {
                    UvScaleU = Math.Clamp(MathF.Abs(scaleU), 0.001f, 1024f),
                    UvScaleV = Math.Clamp(MathF.Abs(scaleV), 0.001f, 1024f)
                };
                return true;
            }
        }

        if (name.Contains("uvMapping", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("MapAtlas", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("AtlasRect", StringComparison.OrdinalIgnoreCase) ||
            (name.Contains("Atlas", StringComparison.OrdinalIgnoreCase) &&
             !name.Contains("AtlasMin", StringComparison.OrdinalIgnoreCase) &&
             !name.Contains("AtlasMax", StringComparison.OrdinalIgnoreCase)))
        {
            if (values.Length >= 4 &&
                IsPlausibleUvMagnitude(values[0]) &&
                IsPlausibleUvMagnitude(values[1]) &&
                IsPlausibleUvMagnitude(values[2], allowNegative: true) &&
                IsPlausibleUvMagnitude(values[3], allowNegative: true) &&
                values[0] != 0f &&
                values[1] != 0f)
            {
                updated = current with
                {
                    UvScaleU = Math.Clamp(MathF.Abs(values[0]), 0.001f, 1024f),
                    UvScaleV = Math.Clamp(MathF.Abs(values[1]), 0.001f, 1024f),
                    UvOffsetU = values[2],
                    UvOffsetV = values[3]
                };
                return true;
            }
        }

        if (name.Contains("UVScale", StringComparison.OrdinalIgnoreCase))
        {
            if (values.Length >= 2 &&
                IsPlausibleUvMagnitude(values[0]) &&
                IsPlausibleUvMagnitude(values[1]) &&
                values[0] != 0f &&
                values[1] != 0f)
            {
                updated = current with
                {
                    UvScaleU = Math.Clamp(MathF.Abs(values[0]), 0.001f, 1024f),
                    UvScaleV = Math.Clamp(MathF.Abs(values[1]), 0.001f, 1024f)
                };
                return true;
            }
        }

        if (name.Contains("UVOffset", StringComparison.OrdinalIgnoreCase) && values.Length >= 2)
        {
            if (IsPlausibleUvMagnitude(values[0], allowNegative: true) &&
                IsPlausibleUvMagnitude(values[1], allowNegative: true))
            {
                updated = current with
                {
                    UvOffsetU = values[0],
                    UvOffsetV = values[1]
                };
                return true;
            }
        }

        return false;
    }

    private static bool IsPlausibleUvMagnitude(float value, bool allowNegative = false)
    {
        if (!float.IsFinite(value))
        {
            return false;
        }

        if (!allowNegative && value < 0f)
        {
            return false;
        }

        var abs = MathF.Abs(value);
        return abs <= 1024f;
    }
}
