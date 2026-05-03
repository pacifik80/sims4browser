namespace Sims4ResourceExplorer.Core;

public enum RenderableLayerRole
{
    Unknown = 0,
    BaseColor,
    Overlay,
    Decal,
    DetailNormal,
    Specular,
    Gloss,
    Emissive,
    Mask,
    AlphaSource,
    Refraction,
    Reveal
}

public enum RenderableBlendMode
{
    Opaque = 0,
    AlphaBlend,
    Multiply,
    Add,
    OverlayMix,
    DecalOver,
    Replace
}

public enum RenderableAlphaPolicy
{
    Opaque = 0,
    AlphaTest,
    AlphaBlend
}

public sealed record RenderableLayer(
    string Slot,
    RenderableLayerRole Role,
    RenderableBlendMode Blend,
    int LayerIndex,
    ResourceKeyRecord? TextureKey,
    string? TextureFileName,
    int UvChannel,
    float UvScaleU,
    float UvScaleV,
    float UvOffsetU,
    float UvOffsetV,
    bool ContributesAlpha,
    bool IsApproximateUvTransform = false,
    string? Note = null);

public sealed record RenderableAlpha(
    RenderableAlphaPolicy Policy,
    string? SourceSlot = null,
    float? Threshold = null);

public sealed record RenderableTint(
    CanonicalColor? ViewportTint = null,
    CanonicalColor? ApproximateBaseColor = null,
    string? SwatchMaskSlot = null);

public sealed record RenderableMaterial(
    string Name,
    string ShaderFamily,
    string NormalizedFamily,
    string CoverageTier,
    IReadOnlyList<RenderableLayer> Layers,
    RenderableAlpha Alpha,
    RenderableTint Tint,
    IReadOnlyList<string> Notes)
{
    public RenderableLayer? FindByRole(RenderableLayerRole role) =>
        Layers.FirstOrDefault(layer => layer.Role == role);

    public IEnumerable<RenderableLayer> WhereRole(RenderableLayerRole role) =>
        Layers.Where(layer => layer.Role == role);
}
