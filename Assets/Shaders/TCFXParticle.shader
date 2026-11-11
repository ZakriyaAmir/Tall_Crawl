// Tall Crawl VFX particle/billboard shader (built-in pipeline, mobile friendly).
// Premultiplied blending with an additive knob: _Additive 0 = normal alpha blend (reads on bright skies),
// 1 = pure additive glow. Tinted by vertex colour (particles) and _TintColor (materials / property blocks).
Shader "TallCrawl/FX Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
        _Additive ("Additive (0 alpha, 1 add)", Range(0,1)) = 0.5
        _Boost ("Brightness", Range(0,4)) = 1
        _DepthPull ("Depth pull toward camera (world units, stops curved bark clipping)", Range(0,2)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; fixed4 _TintColor; half _Additive; half _Boost; float _DepthPull;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                // depth-only pull: same screen position, but depth tested as if _DepthPull units closer to the camera
                float3 vp = UnityObjectToViewPos(v.vertex.xyz); float len = max(length(vp), 1e-3);
                float4 pc = mul(UNITY_MATRIX_P, float4(vp * max(0.05, 1 - _DepthPull / len), 1));
                o.pos.z = pc.z / pc.w * o.pos.w; o.color = v.color * _TintColor; o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                c.rgb *= c.a * _Boost;
                c.a *= (1 - _Additive);
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
