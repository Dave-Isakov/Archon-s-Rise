Shader "UI/EnemyCardDissolve"
{
    // uGUI (Canvas) dissolve shader for enemy-card defeat FX (spec 2026-07-21,
    // Spec 2). Same UI plumbing as UI/PulseGlow (stencil/clip/alpha-clip) so it
    // behaves under masks; EnemyCardDefeatFx drives _DissolveAmount 0 -> 1.
    // Noise is procedural, so NO noise texture needs to be assigned.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        // Dissolve controls. _DissolveAmount is animated by EnemyCardDefeatFx:
        // 0 = fully visible, 1 = fully burned away.
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _NoiseScale ("Noise Scale", Float) = 10
        _EdgeWidth ("Edge Width", Range(0,0.5)) = 0.06
        [HDR] _EdgeColor ("Edge Glow Color", Color) = (1, 0.55, 0.1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _DissolveAmount;
            float _NoiseScale;
            float _EdgeWidth;
            fixed4 _EdgeColor;

            // Procedural value noise so no noise texture is required.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f); // smoothstep interpolation
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                float noise = valueNoise(IN.texcoord * _NoiseScale);

                // Keep pixels whose noise still exceeds the burn threshold; the
                // rest have already dissolved away.
                float visible = step(_DissolveAmount, noise); // 1 keep, 0 burned
                color.a *= visible;

                // Glowing frontier: still-visible pixels within EdgeWidth of the
                // threshold, only while actually dissolving.
                float dissolving = step(0.001, _DissolveAmount);
                float edge = visible * (1.0 - step(_DissolveAmount + _EdgeWidth, noise)) * dissolving;
                color.rgb = lerp(color.rgb, _EdgeColor.rgb, saturate(edge * _EdgeColor.a));
                color.a = max(color.a, edge * _EdgeColor.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
