using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.Services;

/// <summary>
/// Rewrites skintone-routed materials in a <see cref="CanonicalScene"/> to bind to the
/// appropriate composited atlas PNG. Pure function; no UI thread or graphics dependency.
/// Body shell materials sample from the body atlas (SkinBlender chain over the skintone's
/// base texture); head shell materials sample from a separate head atlas (the head CASPart's
/// pre-rendered face PNG with Pass 3 HSL shift + face overlays composed on top).
///
/// Used after <c>ISimAssetGraphRenderer.BuildSimSceneAsync</c> produces a scene whose
/// skintone-routed materials carry the "Sim skintone route" approximation tag — this rebinder
/// replaces their BaseColor slot with the matching atlas and clears the swatch tint so the
/// texture is used directly.
/// </summary>
public static class SimSkintoneMaterialBinder
{
    public static CanonicalScene RebindWithAtlas(CanonicalScene scene, byte[] atlasPng) =>
        RebindWithAtlases(scene, bodyAtlas: atlasPng, headAtlas: null);

    public static CanonicalScene RebindWithAtlases(CanonicalScene scene, byte[]? bodyAtlas, byte[]? headAtlas)
    {
        if (scene.Materials.Count == 0)
        {
            return scene;
        }
        if (bodyAtlas is not { Length: > 0 } && headAtlas is not { Length: > 0 })
        {
            return scene;
        }

        var rewritten = scene.Materials
            .Select(material => RewriteOne(material, bodyAtlas, headAtlas))
            .ToList();
        return scene with { Materials = rewritten };
    }

    public static CanonicalMaterial RewriteOne(CanonicalMaterial material, byte[] atlasPng) =>
        RewriteOne(material, bodyAtlas: atlasPng, headAtlas: null);

    public static CanonicalMaterial RewriteOne(CanonicalMaterial material, byte[]? bodyAtlas, byte[]? headAtlas)
    {
        if (string.IsNullOrEmpty(material.Approximation) ||
            !material.Approximation.Contains("Sim skintone route", StringComparison.OrdinalIgnoreCase))
        {
            return material;
        }

        var isHead = material.Approximation.Contains("Head shell", StringComparison.OrdinalIgnoreCase);
        // When a head-specific atlas is supplied, head materials use it. Otherwise head
        // materials fall back to the body atlas — same texture covers both shells via UV
        // (per build 0308: the unified atlas is built from the head CASPart's full-body
        // diffuse). Using the same filename `skin_atlas.png` for both is critical: the
        // viewport renderer detects the PBR skin shader path via that filename, and head
        // + body must take the same shader path or they render with different intensities.
        byte[]? atlas;
        string fileName;
        if (isHead)
        {
            atlas = headAtlas ?? bodyAtlas;
            fileName = headAtlas is { Length: > 0 } ? "head_atlas.png" : "skin_atlas.png";
        }
        else
        {
            atlas = bodyAtlas;
            fileName = "skin_atlas.png";
        }
        if (atlas is not { Length: > 0 })
        {
            // No atlas applicable to this shell — keep the original CASPart diffuse.
            return material;
        }

        var textures = material.Textures;
        var baseIndex = -1;
        for (var i = 0; i < textures.Count; i++)
        {
            if (textures[i].Semantic == CanonicalTextureSemantic.BaseColor)
            {
                baseIndex = i;
                break;
            }
        }
        if (baseIndex < 0 && textures.Count > 0)
        {
            baseIndex = 0;
        }

        var atlasTexture = baseIndex >= 0
            ? textures[baseIndex] with
            {
                FileName = fileName,
                PngBytes = atlas,
                Semantic = CanonicalTextureSemantic.BaseColor
            }
            : new CanonicalTexture(
                Slot: "BaseColor",
                FileName: fileName,
                PngBytes: atlas,
                Semantic: CanonicalTextureSemantic.BaseColor);

        var rewrittenTextures = textures.ToList();
        if (baseIndex >= 0)
        {
            rewrittenTextures[baseIndex] = atlasTexture;
        }
        else
        {
            rewrittenTextures.Add(atlasTexture);
        }

        return material with
        {
            Textures = rewrittenTextures,
            ViewportTintColor = null
        };
    }
}
