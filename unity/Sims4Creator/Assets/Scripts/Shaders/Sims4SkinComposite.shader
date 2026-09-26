// Sims4SkinComposite.shader
//
// UNLIT compositing shader for Graphics.Blit. It recomposes a Sims skin albedo into a
// RenderTexture from a base-tone color texture, up to 4 active grayscale detail layers
// (overlay blend, gutter/alpha = coverage) and an eye overlay (source-over). It is NOT
// the lit pass: the output RenderTexture is then assigned to the HDRP/Lit material's
// _BaseColorMap.
//
// The compose math MUST stay byte-for-byte identical to:
//   - the exporter's preview bake (so the default RT matches the baked preview), and
//   - the contract in the task spec.
// Compose (per channel, all values in 0..1):
//   albedo = baseTone.rgb
//   for each ACTIVE detail (g = detail.r broadcast to rgb, a = detail.a coverage):
//       ov     = (albedo < 0.5) ? (2*albedo*g) : (1 - 2*(1-albedo)*(1-g))   // overlay
//       albedo = lerp(albedo, ov, a)                                        // a=0 -> no change
//   albedo = lerp(albedo, eye.rgb, eye.a)                                   // iris source-over
//
// NOTE on color space: base/eye import as sRGB color (Unity linearizes on sample); detail
// layers import linear (raw 8-bit relief/coverage data). The DETAIL overlay math runs in
// GAMMA space (base converted linear→gamma, overlaid with the raw detail values, converted
// back) because the layers are authored for gamma-space Photoshop-overlay and the exporter's
// CPU preview bakes on raw bytes — this matches both. The eye source-over stays in linear
// (validated visually long ago; alpha-lerp is space-tolerant). Output RT is sRGB as before.

Shader "Hidden/Sims4/SkinComposite"
{
    Properties
    {
        _BaseTex  ("Base Tone",   2D) = "white" {}
        _Detail0  ("Detail 0",    2D) = "black" {}
        _Detail1  ("Detail 1",    2D) = "black" {}
        _Detail2  ("Detail 2",    2D) = "black" {}
        _Detail3  ("Detail 3",    2D) = "black" {}
        _EyeTex   ("Eye Overlay",  2D) = "black" {}
        _DetailCount ("Active Detail Count", Float) = 0
        _HasEye      ("Has Eye Overlay",     Float) = 0
        _DetailMode0 ("Detail 0 mode (0=overlay 1=source-over)", Float) = 0
        _DetailMode1 ("Detail 1 mode", Float) = 0
        _DetailMode2 ("Detail 2 mode", Float) = 0
        _DetailMode3 ("Detail 3 mode", Float) = 0
    }

    SubShader
    {
        // Full-screen blit: no cull, no depth, no blend (we write the final composed RGBA).
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _BaseTex;
            sampler2D _Detail0;
            sampler2D _Detail1;
            sampler2D _Detail2;
            sampler2D _Detail3;
            sampler2D _EyeTex;
            float _DetailCount;
            float _HasEye;
            float _DetailMode0;
            float _DetailMode1;
            float _DetailMode2;
            float _DetailMode3;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            // Per-channel overlay blend (Photoshop "Overlay"), g broadcast to rgb by the caller.
            float3 overlay(float3 a, float3 g)
            {
                float3 lo = 2.0 * a * g;
                float3 hi = 1.0 - 2.0 * (1.0 - a) * (1.0 - g);
                // step(0.5, a) == (a >= 0.5) ? 1 : 0 ; lerp picks hi where a>=0.5, lo otherwise.
                return lerp(lo, hi, step(0.5, a));
            }

            // Apply one detail layer. mode 0 = "overlay": grayscale relief (g = detail.r broadcast,
            // Photoshop-overlay). mode 1 = "over": COLORED source-over wash (detail.rgb by alpha,
            // e.g. PsBoss's warm skin tint). Coverage a = detail.a for both.
            float3 applyDetail(float3 albedo, float4 detail, float mode)
            {
                float3 ov  = overlay(albedo, detail.rrr);
                float3 blended = lerp(ov, detail.rgb, mode);
                return lerp(albedo, blended, detail.a);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 albedo = tex2D(_BaseTex, i.uv).rgb;

                // Active detail layers, in declared order. _DetailCount controls how many apply,
                // so unused samplers (bound to "black") never affect the result even if sampled.
                //
                // COLOR SPACE: the detail layers are AUTHORED for gamma-space Photoshop-overlay
                // (and the exporter's CPU preview bakes on raw 8-bit values). The base samples
                // LINEAR here, so convert to gamma for the overlay math and back after — running
                // overlay in linear space desaturates warm skin into gray-green "dirt" and shifts
                // every midtone (the exact artifact this used to produce).
                if (_DetailCount > 0.5)
                {
                    albedo = LinearToGammaSpace(albedo);
                    albedo = applyDetail(albedo, tex2D(_Detail0, i.uv), _DetailMode0);
                    if (_DetailCount > 1.5) albedo = applyDetail(albedo, tex2D(_Detail1, i.uv), _DetailMode1);
                    if (_DetailCount > 2.5) albedo = applyDetail(albedo, tex2D(_Detail2, i.uv), _DetailMode2);
                    if (_DetailCount > 3.5) albedo = applyDetail(albedo, tex2D(_Detail3, i.uv), _DetailMode3);
                    albedo = GammaToLinearSpace(albedo);
                }

                // Eye iris source-over (transparent elsewhere -> no change).
                if (_HasEye > 0.5)
                {
                    float4 eye = tex2D(_EyeTex, i.uv);
                    albedo = lerp(albedo, eye.rgb, eye.a);
                }

                return float4(albedo, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
