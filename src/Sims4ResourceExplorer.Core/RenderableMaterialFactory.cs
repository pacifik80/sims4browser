namespace Sims4ResourceExplorer.Core;

public static class RenderableMaterialFactory
{
    public static RenderableMaterial FromCanonical(CanonicalMaterial canonical)
    {
        var shaderFamily = canonical.ShaderName ?? "Unknown";
        var normalizedFamily = NormalizeFamily(shaderFamily);
        var alphaSlot = canonical.AlphaTextureSlot;
        var layeredSlots = canonical.LayeredTextureSlots ?? Array.Empty<string>();
        var alphaSlotSet = string.IsNullOrWhiteSpace(alphaSlot)
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(new[] { alphaSlot! }, StringComparer.OrdinalIgnoreCase);
        var layeredSet = new HashSet<string>(layeredSlots, StringComparer.OrdinalIgnoreCase);

        var layers = new List<RenderableLayer>(canonical.Textures.Count);
        var index = 0;
        foreach (var texture in canonical.Textures)
        {
            var role = ClassifyRole(texture, normalizedFamily, alphaSlotSet, layeredSet);
            var blend = ChooseBlend(role, index);
            var contributesAlpha =
                role == RenderableLayerRole.AlphaSource ||
                (role == RenderableLayerRole.BaseColor && (canonical.IsTransparent || alphaSlotSet.Contains(texture.Slot))) ||
                role == RenderableLayerRole.Decal ||
                role == RenderableLayerRole.Overlay;

            layers.Add(new RenderableLayer(
                Slot: texture.Slot,
                Role: role,
                Blend: blend,
                LayerIndex: index++,
                TextureKey: texture.SourceKey,
                TextureFileName: texture.FileName,
                UvChannel: texture.UvChannel,
                UvScaleU: texture.UvScaleU,
                UvScaleV: texture.UvScaleV,
                UvOffsetU: texture.UvOffsetU,
                UvOffsetV: texture.UvOffsetV,
                ContributesAlpha: contributesAlpha,
                IsApproximateUvTransform: texture.IsApproximateUvTransform,
                Note: null));
        }

        var policy = MapAlphaPolicy(canonical.AlphaMode, canonical.IsTransparent);
        var alpha = new RenderableAlpha(policy, alphaSlot, Threshold: null);
        var tint = new RenderableTint(canonical.ViewportTintColor, canonical.ApproximateBaseColor, SwatchMaskSlot: null);
        var notes = string.IsNullOrWhiteSpace(canonical.Approximation)
            ? Array.Empty<string>()
            : new[] { canonical.Approximation! };

        return new RenderableMaterial(
            Name: canonical.Name,
            ShaderFamily: shaderFamily,
            NormalizedFamily: normalizedFamily,
            CoverageTier: canonical.SourceKind.ToString(),
            Layers: layers,
            Alpha: alpha,
            Tint: tint,
            Notes: notes);
    }

    public static string NormalizeFamily(string shaderName)
    {
        if (string.IsNullOrWhiteSpace(shaderName))
        {
            return "Unknown";
        }

        var span = shaderName.AsSpan().Trim();
        var sep = span.IndexOfAny(new[] { '-', '_', ' ' });
        return sep > 0 ? span[..sep].ToString() : span.ToString();
    }

    /// <summary>
    /// Recognizes the GEOM-side <c>SimSkin</c> family (and adjacent <c>SimSkinMask</c> semantics
    /// kept on the same authority branch per the current CAS/Sim authority matrix).
    /// </summary>
    public static bool IsSimSkinFamily(string? family) =>
        !string.IsNullOrWhiteSpace(family) &&
        family.Contains("SimSkin", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Recognizes the GEOM-side <c>SimGlass</c> family (glass-like transparent Sim surfaces:
    /// eyes, contact lenses, visors).
    /// </summary>
    public static bool IsSimGlassFamily(string? family) =>
        !string.IsNullOrWhiteSpace(family) &&
        family.Contains("SimGlass", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Recognizes foliage and hard-alpha-cutout families that require alpha-test rendering.
    /// Covers <c>SeasonalFoliage</c>, <c>Foliage</c>, and the generic <c>AlphaCutout</c> profile.
    /// </summary>
    public static bool IsFoliageOrCutoutFamily(string? family) =>
        !string.IsNullOrWhiteSpace(family) &&
        (family.Contains("Foliage", StringComparison.OrdinalIgnoreCase) ||
         family.Contains("AlphaCutout", StringComparison.OrdinalIgnoreCase) ||
         family.Contains("Cutout", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Recognizes the <c>StandardSurface</c> (OpenPBR-aligned) family used by newer TS4 assets.
    /// </summary>
    public static bool IsStandardSurfaceFamily(string? family) =>
        !string.IsNullOrWhiteSpace(family) &&
        family.Contains("StandardSurface", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Recognizes refraction-capable families: <c>RefractionMap</c> (pool water, glass objects)
    /// and <c>SpecularEnvMap</c> (mirror / env-mapped surfaces).
    /// </summary>
    public static bool IsRefractionFamily(string? family) =>
        !string.IsNullOrWhiteSpace(family) &&
        (family.Contains("Refraction", StringComparison.OrdinalIgnoreCase) ||
         family.Contains("SpecularEnvMap", StringComparison.OrdinalIgnoreCase));

    private static RenderableLayerRole ClassifyRole(
        CanonicalTexture texture,
        string normalizedFamily,
        HashSet<string> alphaSlotSet,
        HashSet<string> layeredSet)
    {
        if (texture.Semantic != CanonicalTextureSemantic.Unknown)
        {
            switch (texture.Semantic)
            {
                case CanonicalTextureSemantic.BaseColor: return RenderableLayerRole.BaseColor;
                case CanonicalTextureSemantic.Normal: return RenderableLayerRole.DetailNormal;
                case CanonicalTextureSemantic.Specular: return RenderableLayerRole.Specular;
                case CanonicalTextureSemantic.Gloss: return RenderableLayerRole.Gloss;
                case CanonicalTextureSemantic.Opacity: return RenderableLayerRole.AlphaSource;
                case CanonicalTextureSemantic.Emissive: return RenderableLayerRole.Emissive;
                case CanonicalTextureSemantic.Overlay: return RenderableLayerRole.Overlay;
            }
        }

        if (alphaSlotSet.Contains(texture.Slot))
        {
            return RenderableLayerRole.AlphaSource;
        }

        if (layeredSet.Contains(texture.Slot))
        {
            return string.Equals(normalizedFamily, "DecalMap", StringComparison.OrdinalIgnoreCase)
                ? RenderableLayerRole.Decal
                : RenderableLayerRole.Overlay;
        }

        var slot = texture.Slot;
        if (LooksLikeNormalSlot(slot)) return RenderableLayerRole.DetailNormal;
        if (LooksLikeSpecularSlot(slot)) return RenderableLayerRole.Specular;
        if (LooksLikeGlossSlot(slot)) return RenderableLayerRole.Gloss;
        if (LooksLikeAlphaSlot(slot)) return RenderableLayerRole.AlphaSource;
        if (LooksLikeEmissiveSlot(slot)) return RenderableLayerRole.Emissive;
        if (LooksLikeMaskSlot(slot)) return RenderableLayerRole.Mask;
        if (LooksLikeOverlaySlot(slot)) return RenderableLayerRole.Overlay;
        if (LooksLikeBaseColorSlot(slot)) return RenderableLayerRole.BaseColor;

        return RenderableLayerRole.Unknown;
    }

    private static RenderableBlendMode ChooseBlend(RenderableLayerRole role, int layerIndex) =>
        role switch
        {
            RenderableLayerRole.BaseColor => layerIndex == 0 ? RenderableBlendMode.Opaque : RenderableBlendMode.AlphaBlend,
            RenderableLayerRole.Overlay => RenderableBlendMode.OverlayMix,
            RenderableLayerRole.Decal => RenderableBlendMode.DecalOver,
            RenderableLayerRole.Mask => RenderableBlendMode.Replace,
            RenderableLayerRole.AlphaSource => RenderableBlendMode.Replace,
            RenderableLayerRole.DetailNormal => RenderableBlendMode.Replace,
            RenderableLayerRole.Specular => RenderableBlendMode.Replace,
            RenderableLayerRole.Gloss => RenderableBlendMode.Replace,
            RenderableLayerRole.Emissive => RenderableBlendMode.Add,
            RenderableLayerRole.Refraction => RenderableBlendMode.Replace,
            RenderableLayerRole.Reveal => RenderableBlendMode.Replace,
            _ => RenderableBlendMode.Opaque
        };

    private static RenderableAlphaPolicy MapAlphaPolicy(string? alphaMode, bool isTransparent)
    {
        if (!string.IsNullOrWhiteSpace(alphaMode))
        {
            switch (alphaMode.ToLowerInvariant())
            {
                case "opaque": return RenderableAlphaPolicy.Opaque;
                case "alpha-test": return RenderableAlphaPolicy.AlphaTest;
                case "alpha-test-or-blend": return isTransparent ? RenderableAlphaPolicy.AlphaBlend : RenderableAlphaPolicy.AlphaTest;
                case "alpha-blend": return RenderableAlphaPolicy.AlphaBlend;
            }
        }

        return isTransparent ? RenderableAlphaPolicy.AlphaBlend : RenderableAlphaPolicy.Opaque;
    }

    private static bool LooksLikeBaseColorSlot(string slot) =>
        slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("basecolor", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("albedo", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("texture_0", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("colorMap", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeNormalSlot(string slot) =>
        slot.Contains("normal", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("bump", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSpecularSlot(string slot) =>
        slot.Contains("specular", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("metal", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeGlossSlot(string slot) =>
        slot.Contains("gloss", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("rough", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAlphaSlot(string slot) =>
        slot.Equals("alpha", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("opacity", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("cutout", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeMaskSlot(string slot) =>
        slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("region_map", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("colorshiftmask", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeEmissiveSlot(string slot) =>
        slot.Contains("emissive", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("emit", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeOverlaySlot(string slot) =>
        slot.Equals("overlay", StringComparison.OrdinalIgnoreCase) ||
        slot.Contains("decal", StringComparison.OrdinalIgnoreCase);
}
