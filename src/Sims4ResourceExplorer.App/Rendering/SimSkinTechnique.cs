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

    /// <summary>
    /// The PS material constant buffer name. MUST be the stock <c>cbMesh</c> (register b1):
    /// HelixToolkit's MeshRenderCore unconditionally writes a 144-byte ModelStruct into the
    /// FRONT of the named material cbuffer every frame and copies <c>StructSize − 144</c>
    /// bytes for the tail — so the buffer must be ≥ the stock 352-byte cbMesh body or that
    /// length goes negative and ArrayStorage.Read overflows (the build-0324 crash). Slider
    /// fields are appended AFTER the 352-byte stock body and set by reflected field name.
    /// </summary>
    public const string SkinParamsCBuffer = "cbMesh";

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

// MILESTONE-1 STEP-0: prove the custom technique draws without ANY custom-constant math.
// The material cbuffer MUST be the stock cbMesh @ b1, >= 352 bytes (the mesh render core
// prepends a 144-byte ModelStruct and tail-copies StructSize-144 bytes; a smaller buffer
// makes that negative -> ArrayStorage.Read overflow, the build-0324 crash). We reserve the
// full 352-byte stock body as opaque padding (written by the render core, read by the stock
// VS for mWorld; we never write it) and set ZERO custom properties this step. The green
// tint + captured light rig are HLSL literals here; Step 1 appends real slider fields after
// the pad and drives them via SetProperty.
cbuffer cbMesh : register(b1)
{
    float4 _stockMeshReserved[22]; // 22*16 = 352 bytes == stock PhongPBRMaterialStruct body
};

float4 main(PSInput input) : SV_Target
{
    // M1 STEP-0b BISECTION: do NOT sample the texture. Output a lit green body using ONLY the
    // interpolated normal. If the body turns shaded green -> VS runs, mesh draws, PS executes,
    // normals interpolate; the prior all-black was purely texSkinColor.Sample() returning 0
    // (no surface sampler bound for a custom GenericMaterial). If it's STILL black, the draw
    // itself isn't happening (cull/depth/blend) and the texture is a red herring.
    float3 n = normalize(input.n);
    float ndl0 = saturate(dot(n, normalize(float3(0.417, 0.460, 0.784))));   // captured cb0[0]
    float ndl1 = saturate(dot(n, normalize(float3(-0.916, 0.100, 0.389))));  // captured cb0[1]
    float3 albedo = float3(0.35, 0.85, 0.35);        // synthetic green albedo (no texture)
    float3 lit = albedo * (0.45 + ndl0 * float3(0.5, 0.5, 0.5) + ndl1 * float3(0.24, 0.24, 0.24));
    lit += _stockMeshReserved[0].xyz * 0.0;          // keep the pad alive (don't strip cbMesh)
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

            // Build a MINIMAL custom technique (a single Default pass) — exactly HelixToolkit's
            // own CustomShaderDemo "NoiseMesh" recipe. This technique is consumed by a custom
            // SimSkinMeshNode whose OnCreateRenderTechnique returns it (see SimSkinMeshNode), so
            // node.technique == material.pass.technique — the consistency BOTH working Helix
            // demos rely on. The prior approaches put our pass on a STOCK MeshGeometryModel3D
            // node (whose own RenderMesh technique drove a DepthPrepass that black-out our color
            // pass, AND mismatched node-vs-pass technique) and drew nothing — even after cloning
            // RenderMesh's states. Because THIS technique has NO DepthPrepass pass, no prepass
            // runs for the node and the color pass draws on cleared depth.
            effectsManager.AddTechnique(BuildMinimalTechnique(psDesc));
        }
    }

    /// <summary>
    /// Builds the minimal SimSkinComposite technique: a single Default pass = stock mesh vertex
    /// shader + our custom pixel shader, with the stock mesh render states (BSAlphaBlend,
    /// DSSDepthLessEqual) and the stock VSInput layout. No DepthPrepass/Shadow/Wireframe passes
    /// — matching HelixToolkit's CustomShaderDemo. The custom node (SimSkinMeshNode) selects
    /// this technique so node and material agree on it.
    /// </summary>
    private static TechniqueDescription BuildMinimalTechnique(ShaderDescription psDesc) =>
        new(TechniqueName)
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
                    BlendStateDescription = DefaultBlendStateDescriptions.BSAlphaBlend,
                    DepthStencilStateDescription = DefaultDepthStencilDescriptions.DSSDepthLessEqual,
                },
            },
        };
}
