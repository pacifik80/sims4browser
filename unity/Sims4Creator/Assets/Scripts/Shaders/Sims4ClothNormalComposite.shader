// Sims4ClothNormalComposite.shader
//
// UNLIT blit: builds a garment's tangent-space NORMAL map by masking the character's skin-detail
// normal with the fabric alpha — skin texels (alpha≈0) keep the skin normal so the garment's baked
// exposed skin (neck/chest/arms) shades EXACTLY like the head/nude skin (which runs this normal on
// the shared skin material); fabric texels (alpha≈1) are forced FLAT so body relief in the skin
// normal (navel, muscle detail) can never emboss through the cloth.
//
// Output encoding: standard unsigned RGB normal (n*0.5+0.5, alpha 1) in a LINEAR (sRGB=false) RT.
// HDRP's _NormalMap sampler unpacks via UnpackNormalmapRGorAG: a(=1)*=r → xy=(r,g) — correct for
// this encoding. UnpackNormal below handles the SOURCE texture's packing (RGB or DXT5nm/AG).

Shader "Hidden/Sims4/ClothNormalComposite"
{
    Properties
    {
        _MainTex   ("Fabric (RGBA, A=skin/fabric mask)", 2D) = "white" {}
        _SkinNormal("Skin detail normal (packed)",       2D) = "bump" {}
        _UnderA    ("Under-layer fabric A (RGBA)",       2D) = "black" {}
        _UnderB    ("Under-layer fabric B (RGBA)",       2D) = "black" {}
        _HasUnderA ("Has under-layer A", Float) = 0
        _HasUnderB ("Has under-layer B", Float) = 0
    }

    SubShader
    {
        // Full-screen blit: no cull, no depth, no blend.
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
            sampler2D _SkinNormal;
            sampler2D _UnderA;
            sampler2D _UnderB;
            float _HasUnderA;
            float _HasUnderB;

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
                // Combined fabric coverage: the outer garment's alpha PLUS any layered underwear fabric
                // composited beneath it — skin relief must not emboss through EITHER cloth layer.
                float a = tex2D(_MainTex, i.uv).a;                  // 1 = fabric, 0 = baked skin
                if (_HasUnderA > 0.5) a = max(a, tex2D(_UnderA, i.uv).a);
                if (_HasUnderB > 0.5) a = max(a, tex2D(_UnderB, i.uv).a);
                float3 n = UnpackNormal(tex2D(_SkinNormal, i.uv));  // decodes RGB or DXT5nm/AG packing
                n = normalize(lerp(n, float3(0.0, 0.0, 1.0), a));   // fabric → flat
                return float4(n * 0.5 + 0.5, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
