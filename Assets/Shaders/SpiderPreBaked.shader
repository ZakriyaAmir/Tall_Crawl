Shader "TallCrawl/SpiderPreBaked"
{
    Properties
    {
        _Color ("Main Color", Color) = (0.75,0.75,0.75,1)
        _Brightness ("Overall Brightness", Range(0.2, 1.5)) = 0.75
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _ShadowDull ("Shadow Dullness", Range(0, 1)) = 0.55
        _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _OutlineColor ("Outline / Stroke Color", Color) = (0.12, 0.08, 0.10, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 5.0)) = 1.4
        _EdgeThreshold ("Edge Stroke / Inner Contour", Range(0.0, 0.5)) = 0.22
        _CelIntensity ("Cel Shading Intensity", Range(0.0, 1.0)) = 0.30
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150

        // Pass 1: Stylized Japanese anime cartoon inverted-hull silhouette outline
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            ZTest LEqual
            Offset 1, 1
            ColorMask RGBA
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            float _OutlineWidth;
            fixed4 _OutlineColor;

            v2f vert (appdata v)
            {
                v2f o;
                if (_OutlineWidth <= 0.01)
                {
                    o.pos = float4(0, 0, 0, 0);
                    return o;
                }

                o.pos = UnityObjectToClipPos(v.vertex);

                // Normal in view space
                float3 viewNormal = COMPUTE_VIEW_NORMAL;
                float2 normProj = TransformViewToProjection(viewNormal.xy);

                float len = length(normProj);
                if (len > 0.0001)
                    normProj /= len;

                // Uniform pixel width horizontally and vertically
                normProj.x *= (_ScreenParams.y / max(1.0, _ScreenParams.x));

                // Extrude in clip space proportional to o.pos.w for uniform screen pixel stroke
                o.pos.xy += normProj * (_OutlineWidth * 0.0035 * o.pos.w);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(_OutlineColor.rgb, 1.0);
            }
            ENDCG
        }

        // Pass 2: Base anime cel shading with inner edge contour strokes
        CGPROGRAM
        #pragma surface surf AnimeCel keepalpha fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Brightness;
        half _ShadowDull;
        fixed4 _EmissionColor;
        fixed4 _OutlineColor;
        half _EdgeThreshold;
        half _CelIntensity;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        // 2-tone crisp Japanese anime cel shading with smooth wrapped diffuse and shadow attenuation
        half4 LightingAnimeCel (SurfaceOutput s, half3 lightDir, half atten)
        {
            // When _ShadowDull == 1.0 and _CelIntensity == 0.0, completely bypass all shadows and directional darkening
            half wrap = dot(s.Normal, lightDir) * 0.5 + 0.5;
            half celBand = (_CelIntensity > 0.001) ? (smoothstep(0.24, 0.46, wrap) * _CelIntensity + (1.0 - _CelIntensity)) : 1.0;
            half shadowFactor = (_ShadowDull < 0.999) ? lerp(_ShadowDull, 1.0, atten) : 1.0;

            half4 c;
            c.rgb = s.Albedo * (celBand * shadowFactor) + s.Emission;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            half3 baseCol = tex.rgb * _Color.rgb * _Brightness;

            // Japanese cartoon ink stroke along curved edges and creases (only if _EdgeThreshold > 0.01)
            if (_EdgeThreshold > 0.01)
            {
                half NdotV = saturate(dot(o.Normal, normalize(IN.viewDir)));
                half edgeFactor = smoothstep(_EdgeThreshold, _EdgeThreshold + 0.08, NdotV);
                o.Albedo = lerp(_OutlineColor.rgb, baseCol, edgeFactor);
            }
            else
            {
                o.Albedo = baseCol;
            }

            o.Emission = _EmissionColor.rgb;
            o.Alpha = tex.a * _Color.a;
        }
        ENDCG
    }
    Fallback "Mobile/Diffuse"
}
