// Additive glow for the words of power: glyph quads, particles, flares, light shafts.
// Plain unlit pass with no LightMode tag, so URP draws it as SRPDefaultUnlit. No keywords, so no
// variant can be stripped. It lives under Resources, which makes Unity include it in player builds.
// Texture is white RGB with the shape in alpha. Colour can be HDR (above 1) to feed URP Bloom.
Shader "Ankhu/Realms/MagicAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseMap ("Texture (URP alias, unused)", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _BaseColor ("Tint (URP alias)", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                half4 c = t * i.color;
                return half4(c.rgb, saturate(c.a));
            }
            ENDCG
        }
    }
    Fallback Off
}
