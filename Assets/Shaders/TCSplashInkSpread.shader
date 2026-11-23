Shader "TallCrawl/SplashInkSpread"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BrushIn ("Brush In Progress", Range(0, 1)) = 0
        _BrushOut ("Brush Out Progress", Range(0, 1)) = 0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.2)) = 0.055

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _BrushIn;
            float _BrushOut;
            float _EdgeSoftness;

            inline float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            inline float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
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

            fixed4 frag(v2f i) : SV_Target
            {
                // Strict zero-artifact bounds:
                // When _BrushIn == 0, logo is completely invisible
                // When _BrushOut >= 0.999, logo is completely wiped away
                if (_BrushIn <= 0.0001 || _BrushOut >= 0.999)
                {
                    return fixed4(0, 0, 0, 0);
                }

                fixed4 col = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                if (col.a <= 0.001) return fixed4(0, 0, 0, 0);

                // --- BRUSH STROKE COORDINATE & INK SPREAD TEXTURE ---
                // Directional sweep across the logo (angled top-left to bottom-right)
                float sweep = i.texcoord.x * 0.75 + (1.0 - i.texcoord.y) * 0.25;

                // Organic ink spread noise & painterly bristles
                float inkNoise1 = vnoise(float2(i.texcoord.x * 12.0, i.texcoord.y * 16.0)) - 0.5;
                float inkNoise2 = vnoise(float2(i.texcoord.x * 28.0, i.texcoord.y * 55.0)) - 0.5;
                float bristle = sin(i.texcoord.y * 95.0 + i.texcoord.x * 35.0) * 0.035
                              + sin(i.texcoord.y * 190.0) * 0.020;

                float brushVal = sweep + (inkNoise1 * 0.16 + inkNoise2 * 0.07 + bristle);

                // Threshold bounds mapped to ensure 0 artifacts at edges
                float startThresh = -0.20;
                float endThresh = 1.35;
                float softness = max(0.08, _EdgeSoftness);

                // 1. BRUSH IN: Reveals the logo as _BrushIn goes from 0 -> 1
                float threshIn = lerp(startThresh, endThresh, _BrushIn);
                float inMask = smoothstep(threshIn - softness, threshIn, brushVal);
                inMask = 1.0 - inMask; // pixels where brushVal < threshIn are revealed

                // 2. BRUSH OUT: Wipes away the logo as _BrushOut goes from 0 -> 1
                float threshOut = lerp(startThresh, endThresh, _BrushOut);
                float outMask = smoothstep(threshOut, threshOut + softness, brushVal);

                col.a *= inMask * outMask;

                #ifdef UNITY_UI_ALPHACLIP
                clip (col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
