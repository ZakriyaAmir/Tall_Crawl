Shader "TallCrawl/SpiderPreBaked"
{
    Properties
    {
        _Color ("Main Color", Color) = (0.75,0.75,0.75,1)
        _Brightness ("Overall Brightness", Range(0.2, 1.5)) = 0.75
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _ShadowDull ("Shadow Dullness", Range(0, 1)) = 0.55
        _EmissionColor ("Emission Color", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150

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

        // Custom lighting: completely ignores directional light angle / specular highlights,
        // so the spider is NOT illuminated by scene lights and never reflects specular highlights.
        // It relies on its pre-baked texture, but respects shadow attenuation (atten)
        // so it gets dull in shadows cast by branches/hazards!
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
