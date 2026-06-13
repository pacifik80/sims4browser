using System.IO;
using System.Numerics;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model;

namespace Sims4ResourceExplorer.App.Rendering;

/// <summary>
/// Builds the GPU skin material core bound to the <see cref="SimSkinTechnique"/> custom
/// technique. Layer textures are bound by their reflected HLSL variable name; slider scalars
/// are written into the <c>cbSkinParams</c> constant buffer by field name. A slider change
/// then just calls <see cref="GenericMaterialCore.SetProperty(string, float)"/> (etc.) and a
/// redraw — no CPU recompose.
///
/// Milestone 1: binds only the base color texture + a debug tint constant.
/// </summary>
internal static class SimSkinCompositeMaterial
{
    /// <summary>
    /// Creates a skin material core, registering the technique on <paramref name="effectsManager"/>
    /// if needed. Returns null when the technique/pass cannot be resolved (caller falls back to
    /// the stock PBR path).
    /// </summary>
    // Captured light rig (RenderDoc eid 602 cb0). Toward-light directions + grey colors;
    // ambient floor at the AO-band midpoint. Same values as the stock Helix light rig in
    // SceneViewportRenderer so the GPU skin matches the rest of the scene.
    private static readonly Vector4 LightDir0 = new(0.417f, 0.460f, 0.784f, 0f);
    private static readonly Vector4 LightDir1 = new(-0.916f, 0.100f, 0.389f, 0f);
    private static readonly Vector4 LightColor0 = new(0.50f, 0.50f, 0.50f, 0f);
    private static readonly Vector4 LightColor1 = new(0.24f, 0.24f, 0.24f, 0f);
    private static readonly Vector4 AmbientParams = new(0.45f, 0f, 0f, 0f);

    public static GenericMeshMaterialCore? TryCreate(IEffectsManager? effectsManager, byte[]? skinColorPng, Vector4 debugTint)
    {
        if (effectsManager is null || skinColorPng is not { Length: > 0 })
        {
            return null;
        }

        SimSkinTechnique.EnsureRegistered(effectsManager);
        var technique = effectsManager.GetTechnique(SimSkinTechnique.TechniqueName);
        var pass = technique?.GetPass(DefaultPassNames.Default);
        if (pass is null || pass.IsNULL)
        {
            return null;
        }

        var core = new GenericMeshMaterialCore(pass, SimSkinTechnique.SkinParamsCBuffer);
        core.SetTexture("texSkinColor", new MemoryStream(skinColorPng));
        core.SetProperty("debugTint", debugTint);
        core.SetProperty("lightDir0", LightDir0);
        core.SetProperty("lightDir1", LightDir1);
        core.SetProperty("lightColor0", LightColor0);
        core.SetProperty("lightColor1", LightColor1);
        core.SetProperty("ambientParams", AmbientParams);
        return core;
    }
}
