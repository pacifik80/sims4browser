// Sims4ClothComposite.shader
//
// UNLIT compositing shader for Graphics.Blit. It composites a garment's fabric diffuse OVER the
// wearing Sim's live skin atlas, per-texel, driven by the fabric alpha MASK — reproducing the game's
// proven SimSkin albedo math (RenderDoc eid 602):
//
//   albedo = lerp(skin.rgb, fabric.rgb, fabric.a)
//
// where fabric.a is the authored skin-vs-fabric mask (opaque = fabric, transparent = show live skin).
// The result RenderTexture is then assigned to the garment HDRP/Lit material's _BaseColorMap, so the
// garment is a single OPAQUE lit mesh (no alpha-clip, no second body mesh, no geometry cutting). The
// exposed-skin faces of the garment mesh carry body-UVs, so sampling the skin atlas at the same UV
// fills them with the current live skin — updating automatically whenever the skin atlas is recomposed.
//
// COLOR SPACE (identical contract to Sims4SkinComposite): _MainTex (fabric) imports sRGB and the skin
// atlas RT is sRGB, so both linearize on sample; we lerp in LINEAR space; the compositor renders into an
// sRGB RenderTexture, so Graphics.Blit sRGB-encodes on store and HDRP re-linearizes it as a normal
// _BaseColorMap. No extra gamma conversion here.

Shader "Hidden/Sims4/ClothComposite"
{
    Properties
    {
        _MainTex ("Fabric (RGBA, A=skin/fabric mask)", 2D) = "white" {}
        _SkinTex ("Live skin atlas (RGB)",             2D) = "white" {}
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

            sampler2D _MainTex;
            sampler2D _SkinTex;

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

            float4 frag(v2f i) : SV_Target
            {
                float4 fabric = tex2D(_MainTex, i.uv);
                float3 skin   = tex2D(_SkinTex, i.uv).rgb;
                // fabric.a == 1 -> fabric; fabric.a == 0 -> live skin; anti-aliased band -> clean gradient.
                return float4(lerp(skin, fabric.rgb, fabric.a), 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
