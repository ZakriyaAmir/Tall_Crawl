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
        _OutlineWidth ("Outline Width", Range(0.001, 0.05)) = 0.015
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150

        // Pass 1: Stylized Japanese anime cartoon inverted-hull outline
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode"="Always" }
            Cull Front
            ZWrite On
            ColorMask RGB
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
                // Extrude vertex along normal in view space for crisp, uniform cartoon ink strokes
                float4 viewPos = mul(UNITY_MATRIX_MV, v.vertex);
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_ITMV, v.normal);
                viewNormal = normalize(viewNormal);
                viewPos.xyz += viewNormal * _OutlineWidth;
                o.pos = mul(UNITY_MATRIX_P, viewPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }

        // Pass 2: Base surface shading with anime cel / pre-baked lighting
        CGPROGRAM
        #pragma surface surf PreBaked keepalpha fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Brightness;
        half _ShadowDull;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_MainTex;
        };

        half4 LightingPreBaked (SurfaceOutput s, half3 lightDir, half atten)
        {
            half shadowFactor = lerp(_ShadowDull, 1.0, atten);
            half4 c;
            c.rgb = s.Albedo * shadowFactor + s.Emission;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
            o.Albedo = tex.rgb * _Color.rgb * _Brightness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = tex.a * _Color.a;
        }
        ENDCG
    }
    Fallback "Mobile/Diffuse"
}
