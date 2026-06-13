using System;
using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Shaders;

namespace Sims4ResourceExplorer.App.Rendering;

/// <summary>
/// Registers a custom HelixToolkit render technique ("SimSkinComposite") whose pixel shader
/// composites the Sims-4 skin on the GPU from separate layer textures + slider constants,
/// instead of the CPU per-slider atlas recompose (see
/// docs/workflows/material-pipeline/gpu-skin-shader-plan.md).
///
/// The technique reuses HelixToolkit's stock mesh vertex shader + input layout
/// (<see cref="DefaultVSShaderDescriptions.VSMeshDefault"/>), so vertex skinning, transforms,
/// and the per-frame camera/light constant buffers stay byte-identical — only the pixel
/// shader is swapped. The HLSL is compiled to bytecode at runtime via the SharpDX D3D
/// compiler that ships with HelixToolkit (no fxc build step).
///
/// Milestone 1 (this revision): the pixel shader is intentionally trivial — it samples the
/// base skin color and multiplies by a single <c>debugTint</c> constant. This proves the
/// whole plumbing end-to-end (technique registration, SRV bound by reflected name, constant
/// write-through + redraw) before the full albedo equation is grafted on.
/// </summary>
internal static class SimSkinTechnique
{
    public const string TechniqueName = "SimSkinComposite";

    /// <summary>The PS material constant buffer name; sliders write into this by field name.</summary>
    public const string SkinParamsCBuffer = "cbSkinParams";

    // Milestone-1 pixel shader. PSInput MUST match HelixToolkit's stock vsMeshDefault output
    // (Common/DataStructs.hlsl): same field order + semantics. We read input.t (UV) and
    // input.n (world normal). texSkinColor at t6 (free; t0-t5 are the stock material maps);
    // samplerSurface = stock s0; cbSkinParams at b10 (free; b0-b6 are the stock per-frame
    // buffers). Albedo = the (still CPU-composed) skin atlas; lighting = the captured
    // 2-directional rig from the RenderDoc capture (cb0[0/1] dirs, cb0[4/5] colors, ambient
    // band midpoint). All cbuffer vectors are float4 to avoid HLSL packing surprises. The
    // per-pixel layer compositing + height-normal move into this shader in Milestone 2.
    private const string PixelShaderHlsl = @"
struct PSInput
{
    float4 p        : SV_POSITION;
    float4 vEye     : POSITION0;
    float3 n        : NORMAL;
    float4 wp       : POSITION1;
    float4 sp       : TEXCOORD1;
    float2 t        : TEXCOORD0;
    float3 t1       : TANGENT;
    float3 t2       : BINORMAL;
    float4 c        : COLOR;
    float4 c2       : COLOR1;
    float4 cDiffuse : COLOR2;
};

Texture2D texSkinColor : register(t6);
SamplerState samplerSurface : register(s0);

cbuffer cbSkinParams : register(b10)
{
    float4 debugTint;     // multiplies albedo; (1,1,1,1) = passthrough, drives the M1 proof
    float4 lightDir0;     // xyz = toward-light direction (captured cb0[0])
    float4 lightDir1;     // captured cb0[1]
    float4 lightColor0;   // rgb (captured cb0[4])
    float4 lightColor1;   // rgb (captured cb0[5])
    float4 ambientParams; // x = ambient floor
};

float4 main(PSInput input) : SV_Target
{
    float3 albedo = texSkinColor.Sample(samplerSurface, input.t).rgb * debugTint.rgb;
    float3 n = normalize(input.n);
    float ndl0 = saturate(dot(n, normalize(lightDir0.xyz)));
    float ndl1 = saturate(dot(n, normalize(lightDir1.xyz)));
    float3 lit = albedo * (ambientParams.x + ndl0 * lightColor0.rgb + ndl1 * lightColor1.rgb);
    return float4(saturate(lit), 1.0);
}
";

    private static readonly object Gate = new();

    /// <summary>
    /// Idempotently registers the technique on the given effects manager. Safe to call on
    /// every render; registration happens once per manager instance.
    /// </summary>
    public static void EnsureRegistered(IEffectsManager? effectsManager)
    {
        if (effectsManager is null)
        {
            return;
        }
        lock (Gate)
        {
            // GetTechnique returns a non-null NullTechnique sentinel for unregistered names
            // (HelixToolkit null-object pattern), so checking `is not null` would ALWAYS be
            // true and AddTechnique would never run. Use HasTechnique for true existence.
            if (effectsManager.HasTechnique(TechniqueName))
            {
                return;
            }

            byte[] psBytecode;
            using (var result = SharpDX.D3DCompiler.ShaderBytecode.Compile(PixelShaderHlsl, "main", "ps_5_0", SharpDX.D3DCompiler.ShaderFlags.OptimizationLevel3))
            {
                if (result.HasErrors || result.Bytecode is null)
                {
                    throw new InvalidOperationException($"Sim skin pixel shader compile failed: {result.Message}");
                }
                psBytecode = result.Bytecode.Data;
            }

            var psDesc = new ShaderDescription("PSSimSkinComposite", ShaderStage.Pixel, new ShaderReflector(), psBytecode);
            var technique = new TechniqueDescription(TechniqueName)
            {
                InputLayoutDescription = new InputLayoutDescription(DefaultVSShaderByteCodes.VSMeshDefault, DefaultInputLayout.VSInput),
                PassDescriptions = new[]
                {
                    new ShaderPassDescription(DefaultPassNames.Default)
                    {
                        ShaderList = new[]
                        {
                            DefaultVSShaderDescriptions.VSMeshDefault,
                            psDesc,
                        },
                        // Render states are required in practice (matches HelixToolkit's own
                        // CustomShaderDemo mesh pass): without them the pass builds invalid /
                        // GetPass resolves to a NULL pass. RasterStateDescription is left
                        // default (the demo's mesh passes omit it).
                        BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                        DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLess,
                    },
                },
            };
            effectsManager.AddTechnique(technique);
        }
    }
}
