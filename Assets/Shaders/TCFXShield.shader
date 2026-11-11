// Tall Crawl chilli fire shield: fresnel rim + scrolling hex energy + noise flames, transparent, double sided.
Shader "TallCrawl/FX Shield"
{
    Properties
    {
        _HexTex ("Hex", 2D) = "black" {}
        _NoiseTex ("Noise", 2D) = "gray" {}
        _Color ("Rim Colour", Color) = (1,0.55,0.1,1)
        _CoreColor ("Core Colour", Color) = (1,0.9,0.4,1)
        _RimPower ("Rim Power", Range(0.5,6)) = 2.2
        _Fill ("Fill", Range(0,1)) = 0.18
        _HexStrength ("Hex Strength", Range(0,2)) = 0.7
        _Scroll ("Scroll (hex xy, noise zw)", Vector) = (0,0.6,0.15,1.4)
        _Opacity ("Opacity", Range(0,1)) = 0.55
        _Alpha ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            sampler2D _HexTex, _NoiseTex; float4 _HexTex_ST, _NoiseTex_ST;
            fixed4 _Color, _CoreColor; half _RimPower, _Fill, _HexStrength, _Opacity, _Alpha; float4 _Scroll;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uvh : TEXCOORD0; float2 uvn : TEXCOORD1; half rim : TEXCOORD2; };
            v2f vert (appdata v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float3 vd = normalize(WorldSpaceViewDir(v.vertex));
                o.rim = pow(1 - abs(dot(n, vd)), _RimPower);
                o.uvh = TRANSFORM_TEX(v.uv, _HexTex) + _Scroll.xy * _Time.y;
                o.uvn = TRANSFORM_TEX(v.uv, _NoiseTex) + _Scroll.zw * _Time.y;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                half hex = tex2D(_HexTex, i.uvh).a;
                half nz = tex2D(_NoiseTex, i.uvn).r;
                half flame = saturate(nz * 1.6 - 0.35);
                half k = saturate(i.rim + _Fill + hex * _HexStrength * (0.35 + nz) * (0.4 + i.rim));
                fixed3 col = lerp(_Color.rgb, _CoreColor.rgb, saturate(flame * (1 - i.rim) + hex * 0.5));
                half a = saturate(k * _Opacity * (0.6 + flame * 0.8)) * _Alpha;
                // premultiplied: mostly additive glow, partly opaque so it reads on bright skies
                return fixed4(col * a * 1.6, a * 0.55);
            }
            ENDCG
        }
    }
    Fallback Off
}
