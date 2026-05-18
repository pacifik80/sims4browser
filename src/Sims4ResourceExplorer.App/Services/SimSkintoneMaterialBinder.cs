using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App.Services;

/// <summary>
/// Rewrites skintone-routed materials in a <see cref="CanonicalScene"/> to bind to the
/// composited skin atlas PNG. Pure function; no UI thread or graphics dependency. The
/// atlas itself is composed by <see cref="SimSkinAtlasComposer"/>.
///
/// Used after <c>ISimAssetGraphRenderer.BuildSimSceneAsync</c> produces a scene whose
/// body materials carry the "Sim skintone route" approximation tag but have no actual
/// texture bytes — this rebinder replaces their BaseColor slot with the atlas and clears
/// the swatch tint so the texture is used directly.
/// </summary>
public static class SimSkintoneMaterialBinder
{
    public static CanonicalScene RebindWithAtlas(CanonicalScene scene, byte[] atlasPng)
    {
        if (atlasPng is not { Length: > 0 } || scene.Materials.Count == 0)
        {
            return scene;
        }

        var rewritten = scene.Materials
            .Select(material => RewriteOne(material, atlasPng))
            .ToList();
        return scene with { Materials = rewritten };
    }

    public static CanonicalMaterial RewriteOne(CanonicalMaterial material, byte[] atlasPng)
    {
        if (string.IsNullOrEmpty(material.Approximation) ||
            !material.Approximation.Contains("Sim skintone route", StringComparison.OrdinalIgnoreCase))
        {
            return material;
        }

        // Head shell materials carry a complete pre-rendered face diffuse on their original
        // CASPart texture (eyes/nose/lips already baked in by EA's CAS shader pre-pass — visible
        // in the dump as the 'region_map' slot file `3E68F8B6F44DA2AA`, which is also bound to
        // the diffuse slot pre-rewrite). The SkinBlender atlas is a *body-skin* composite of the
        // skintone base plus detail/overlay layers and does not include face features. Binding
        // the atlas to the head replaced that face with blotchy hue-amplified body skin. Leave
        // the head material untouched so its original face diffuse + ViewportTintColor route
        // through the PBR skin path (AlbedoMap × skintone) the same way EA's runtime tints it.
        if (material.Approximation.Contains("Head shell", StringComparison.OrdinalIgnoreCase))
        {
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
                FileName = "skin_atlas.png",
                PngBytes = atlasPng,
                Semantic = CanonicalTextureSemantic.BaseColor
            }
            : new CanonicalTexture(
                Slot: "BaseColor",
                FileName: "skin_atlas.png",
                PngBytes: atlasPng,
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
