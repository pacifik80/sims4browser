using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview;

public sealed record AppliedLayer(
    string Slot,
    RenderableLayerRole Role,
    RenderableBlendMode Blend,
    int Pass,
    CanonicalTexture? Texture,
    int UvChannel,
    float UvScaleU,
    float UvScaleV,
    float UvOffsetU,
    float UvOffsetV,
    bool ContributesAlpha,
    bool IsApproximateUvTransform);

public sealed record MaterialPlan(
    string AppliedBy,
    string Family,
    IReadOnlyList<AppliedLayer> Layers,
    RenderableAlpha Alpha,
    RenderableTint Tint,
    IReadOnlyList<string> Notes)
{
    public int PassCount => Layers.Count == 0 ? 0 : Layers.Max(layer => layer.Pass) + 1;

    public IEnumerable<AppliedLayer> LayersInPass(int pass) =>
        Layers.Where(layer => layer.Pass == pass);
}

public interface IRenderableMaterialApplier
{
    string Name { get; }
    bool CanHandle(RenderableMaterial material);
    MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures);
}

public static class MaterialApplierRegistry
{
    private static readonly IRenderableMaterialApplier[] DefaultAppliers =
    [
        new SimSkinApplier(),
        new SimGlassApplier(),
        new FoliageApplier(),
        new StandardSurfaceApplier(),
        new RefractionMapApplier(),
        new DecalMapApplier(),
        new ColorMap7Applier(),
        new DefaultMaterialApplier()
    ];

    public static MaterialPlan Apply(
        RenderableMaterial material,
        IReadOnlyList<CanonicalTexture> textures)
    {
        var applier = DefaultAppliers.First(candidate => candidate.CanHandle(material));
        return applier.Apply(material, textures);
    }

    public static MaterialPlan Apply(CanonicalMaterial canonical) =>
        Apply(RenderableMaterialFactory.FromCanonical(canonical), canonical.Textures);

    public static IReadOnlyList<IRenderableMaterialApplier> KnownAppliers => DefaultAppliers;
}

public sealed class DefaultMaterialApplier : IRenderableMaterialApplier
{
    public string Name => nameof(DefaultMaterialApplier);

    public bool CanHandle(RenderableMaterial material) => true;

    public MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures)
    {
        var ordered = material.Layers
            .OrderBy(layer => SortPriority(layer.Role))
            .ThenBy(layer => layer.LayerIndex)
            .ToList();

        var applied = new List<AppliedLayer>(ordered.Count);
        var pass = 0;
        var basePassUsed = false;
        foreach (var layer in ordered)
        {
            int assignedPass;
            if (!basePassUsed && IsBaseColorLayer(layer))
            {
                assignedPass = 0;
                basePassUsed = true;
            }
            else if (LayerIsCompositedPass(layer.Role, layer.Blend))
            {
                pass++;
                assignedPass = pass;
            }
            else
            {
                // Utility layers (mask, alpha source, normal, specular, gloss, emissive) attach to pass 0
                // and are consumed by the renderer as auxiliary inputs, not as their own composited pass.
                assignedPass = 0;
            }

            applied.Add(new AppliedLayer(
                Slot: layer.Slot,
                Role: layer.Role,
                Blend: layer.Blend,
                Pass: assignedPass,
                Texture: ResolveTexture(layer, textures),
                UvChannel: layer.UvChannel,
                UvScaleU: layer.UvScaleU,
                UvScaleV: layer.UvScaleV,
                UvOffsetU: layer.UvOffsetU,
                UvOffsetV: layer.UvOffsetV,
                ContributesAlpha: layer.ContributesAlpha,
                IsApproximateUvTransform: layer.IsApproximateUvTransform));
        }

        return new MaterialPlan(
            AppliedBy: Name,
            Family: material.NormalizedFamily,
            Layers: applied,
            Alpha: material.Alpha,
            Tint: material.Tint,
            Notes: material.Notes);
    }

    private static int SortPriority(RenderableLayerRole role) => role switch
    {
        RenderableLayerRole.BaseColor => 0,
        RenderableLayerRole.AlphaSource => 1,
        RenderableLayerRole.Mask => 2,
        RenderableLayerRole.DetailNormal => 3,
        RenderableLayerRole.Specular => 4,
        RenderableLayerRole.Gloss => 5,
        RenderableLayerRole.Emissive => 6,
        RenderableLayerRole.Overlay => 7,
        RenderableLayerRole.Decal => 8,
        RenderableLayerRole.Refraction => 9,
        RenderableLayerRole.Reveal => 10,
        _ => 11
    };

    private static bool IsBaseColorLayer(RenderableLayer layer) =>
        layer.Role == RenderableLayerRole.BaseColor ||
        (layer.Role == RenderableLayerRole.Unknown && layer.Blend == RenderableBlendMode.Opaque);

    private static bool LayerIsCompositedPass(RenderableLayerRole role, RenderableBlendMode blend) =>
        role is RenderableLayerRole.Overlay or RenderableLayerRole.Decal or RenderableLayerRole.Refraction or RenderableLayerRole.Reveal ||
        blend is RenderableBlendMode.OverlayMix or RenderableBlendMode.DecalOver or RenderableBlendMode.AlphaBlend or RenderableBlendMode.Multiply or RenderableBlendMode.Add;

    private static CanonicalTexture? ResolveTexture(RenderableLayer layer, IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var bySlot = textures.FirstOrDefault(texture =>
            string.Equals(texture.Slot, layer.Slot, StringComparison.OrdinalIgnoreCase));
        if (bySlot is not null)
        {
            return bySlot;
        }

        if (layer.TextureKey is not null)
        {
            return textures.FirstOrDefault(texture =>
                texture.SourceKey is not null &&
                texture.SourceKey.Type == layer.TextureKey.Type &&
                texture.SourceKey.Group == layer.TextureKey.Group &&
                texture.SourceKey.FullInstance == layer.TextureKey.FullInstance);
        }

        return null;
    }
}

public sealed class ColorMap7Applier : IRenderableMaterialApplier
{
    private readonly DefaultMaterialApplier inner = new();

    public string Name => nameof(ColorMap7Applier);

    public bool CanHandle(RenderableMaterial material) =>
        material.NormalizedFamily.StartsWith("colorMap", StringComparison.OrdinalIgnoreCase);

    public MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures)
    {
        var inherited = inner.Apply(material, textures);
        return inherited with { AppliedBy = Name };
    }
}

public sealed class DecalMapApplier : IRenderableMaterialApplier
{
    private readonly DefaultMaterialApplier inner = new();

    public string Name => nameof(DecalMapApplier);

    public bool CanHandle(RenderableMaterial material) =>
        material.NormalizedFamily.Equals("DecalMap", StringComparison.OrdinalIgnoreCase);

    public MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures)
    {
        var inherited = inner.Apply(material, textures);
        return inherited with { AppliedBy = Name };
    }
}

public sealed class SimSkinApplier : IRenderableMaterialApplier
{
    private readonly DefaultMaterialApplier inner = new();

    public string Name => nameof(SimSkinApplier);

    public bool CanHandle(RenderableMaterial material) =>
        RenderableMaterialFactory.IsSimSkinFamily(material.NormalizedFamily) ||
        RenderableMaterialFactory.IsSimSkinFamily(material.ShaderFamily);

    public MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures)
    {
        // Skin meshes use PBR (GGX) in the renderer via the IsSimSkinFamily detection in
        // MainWindow.CreateMaterial. This applier is the stable insertion point for future
        // per-family plan logic (SimSkinMask split, GEOM-side MTNF parameters, per-physique
        // detail layer routing, etc.).
        var inherited = inner.Apply(material, textures);
        return inherited with { AppliedBy = Name };
    }
}

public sealed class SimGlassApplier : IRenderableMaterialApplier
{
    private readonly DefaultMaterialApplier inner = new();

    public string Name => nameof(SimGlassApplier);

    public bool CanHandle(RenderableMaterial material) =>
        RenderableMaterialFactory.IsSimGlassFamily(material.NormalizedFamily) ||
        RenderableMaterialFactory.IsSimGlassFamily(material.ShaderFamily);

    public MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures)
    {
        // SimGlass surfaces render as transparent PBR (very low roughness, glass Fresnel) in
        // the renderer. The plan itself is structurally the same as the default; the GPU-side
        // material switch happens in MainWindow.CreateMaterial via IsSimGlassFamily.
        var inherited = inner.Apply(material, textures);
        return inherited with { AppliedBy = Name };
    }
}

public sealed class FoliageApplier : IRenderableMaterialApplier
{
    private readonly DefaultMaterialApplier inner = new();

    public string Name => nameof(FoliageApplier);

    public bool CanHandle(RenderableMaterial material) =>
        RenderableMaterialFactory.IsFoliageOrCutoutFamily(material.NormalizedFamily) ||
        RenderableMaterialFactory.IsFoliageOrCutoutFamily(material.ShaderFamily);

    public MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures)
    {
        // Foliage / AlphaCutout materials use hard alpha-test blending (discard below threshold).
        // The decode strategy already sets AlphaMaskThreshold / suggestsAlphaCutout, which
        // propagates to IsTransparent on the mesh model and enables cutout rendering in Phong.
        var inherited = inner.Apply(material, textures);
        return inherited with { AppliedBy = Name };
    }
}

public sealed class StandardSurfaceApplier : IRenderableMaterialApplier
{
    private readonly DefaultMaterialApplier inner = new();

    public string Name => nameof(StandardSurfaceApplier);

    public bool CanHandle(RenderableMaterial material) =>
        RenderableMaterialFactory.IsStandardSurfaceFamily(material.NormalizedFamily) ||
        RenderableMaterialFactory.IsStandardSurfaceFamily(material.ShaderFamily);

    public MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures)
    {
        // StandardSurface (OpenPBR-aligned) renders as PBR in the renderer. The GPU-side
        // material switch happens in MainWindow.CreateMaterial via IsStandardSurfaceFamily.
        var inherited = inner.Apply(material, textures);
        return inherited with { AppliedBy = Name };
    }
}

public sealed class RefractionMapApplier : IRenderableMaterialApplier
{
    private readonly DefaultMaterialApplier inner = new();

    public string Name => nameof(RefractionMapApplier);

    public bool CanHandle(RenderableMaterial material) =>
        RenderableMaterialFactory.IsRefractionFamily(material.NormalizedFamily) ||
        RenderableMaterialFactory.IsRefractionFamily(material.ShaderFamily);

    public MaterialPlan Apply(RenderableMaterial material, IReadOnlyList<CanonicalTexture> textures)
    {
        // RefractionMap / SpecularEnvMap surfaces render as transparent PBR (low roughness,
        // elevated Fresnel) in the renderer. The GPU-side material switch happens in
        // MainWindow.CreateMaterial via IsRefractionFamily.
        var inherited = inner.Apply(material, textures);
        return inherited with { AppliedBy = Name };
    }
}
