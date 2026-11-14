Shader "Skybox/Blended 6 Sided" {
Properties {
    _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
    _Tint2 ("Tint Color 2", Color) = (.5, .5, .5, .5)
    [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
    [Gamma] _Exposure2 ("Exposure 2", Range(0, 8)) = 1.0
    _Rotation ("Rotation", Range(0, 360)) = 0
    _Blend ("Blend", Range(0, 1)) = 0

    [NoScaleOffset] _FrontTex ("Front [+Z] 1 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _BackTex ("Back [-Z] 1 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _LeftTex ("Left [+X] 1 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _RightTex ("Right [-X] 1 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _UpTex ("Up [+Y] 1 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _DownTex ("Down [-Y] 1 (HDR)", 2D) = "grey" {}

    [NoScaleOffset] _FrontTex2 ("Front [+Z] 2 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _BackTex2 ("Back [-Z] 2 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _LeftTex2 ("Left [+X] 2 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _RightTex2 ("Right [-X] 2 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _UpTex2 ("Up [+Y] 2 (HDR)", 2D) = "grey" {}
    [NoScaleOffset] _DownTex2 ("Down [-Y] 2 (HDR)", 2D) = "grey" {}
}

SubShader {
    Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
    Cull Off ZWrite Off

    CGINCLUDE
    #include "UnityCG.cginc"

    half4 _Tint;
    half4 _Tint2;
    half _Exposure;
    half _Exposure2;
    float _Rotation;
    float _Blend;

    float3 RotateAroundYInDegrees (float3 vertex, float degrees)
    {
        float alpha = degrees * UNITY_PI / 180.0;
        float sina, cosa;
        sincos(alpha, sina, cosa);
        float2x2 m = float2x2(cosa, -sina, sina, cosa);
        return float3(mul(m, vertex.xz), vertex.y).xzy;
    }

    struct appdata_t {
        float4 vertex : POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f {
        float4 vertex : SV_POSITION;
        float2 texcoord : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    v2f vert (appdata_t v)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        float3 rotated = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
        o.vertex = UnityObjectToClipPos(rotated);
        o.texcoord = v.texcoord;
        return o;
    }

    half4 skybox_frag (v2f i, sampler2D smp1, sampler2D smp2, half4 smpDecode1)
    {
        half4 tex1 = tex2D(smp1, i.texcoord);
        half4 tex2 = tex2D(smp2, i.texcoord);

        half3 c1 = DecodeHDR(tex1, smpDecode1);
        c1 = c1 * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;

        half3 c2 = DecodeHDR(tex2, smpDecode1);
        c2 = c2 * _Tint2.rgb * unity_ColorSpaceDouble.rgb * _Exposure2;

        half3 c = lerp(c1, c2, _Blend);
        return half4(c, 1.0);
    }
    ENDCG

    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 2.0
        sampler2D _FrontTex;
        sampler2D _FrontTex2;
        half4 _FrontTex_HDR;
        half4 frag (v2f i) : SV_Target {
            return skybox_frag(i, _FrontTex, _FrontTex2, _FrontTex_HDR);
        }
        ENDCG
    }
    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 2.0
        sampler2D _BackTex;
        sampler2D _BackTex2;
        half4 _BackTex_HDR;
        half4 frag (v2f i) : SV_Target {
            return skybox_frag(i, _BackTex, _BackTex2, _BackTex_HDR);
        }
        ENDCG
    }
    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 2.0
        sampler2D _LeftTex;
        sampler2D _LeftTex2;
        half4 _LeftTex_HDR;
        half4 frag (v2f i) : SV_Target {
            return skybox_frag(i, _LeftTex, _LeftTex2, _LeftTex_HDR);
        }
        ENDCG
    }
    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 2.0
        sampler2D _RightTex;
        sampler2D _RightTex2;
        half4 _RightTex_HDR;
        half4 frag (v2f i) : SV_Target {
            return skybox_frag(i, _RightTex, _RightTex2, _RightTex_HDR);
        }
        ENDCG
    }
    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 2.0
        sampler2D _UpTex;
        sampler2D _UpTex2;
        half4 _UpTex_HDR;
        half4 frag (v2f i) : SV_Target {
            return skybox_frag(i, _UpTex, _UpTex2, _UpTex_HDR);
        }
        ENDCG
    }
    Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 2.0
        sampler2D _DownTex;
        sampler2D _DownTex2;
        half4 _DownTex_HDR;
        half4 frag (v2f i) : SV_Target {
            return skybox_frag(i, _DownTex, _DownTex2, _DownTex_HDR);
        }
        ENDCG
    }
}
Fallback Off
}
