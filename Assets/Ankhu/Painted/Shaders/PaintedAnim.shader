// Painted ambient motion: the small things that make the painted room breathe (falling spout water,
// pool glints, the orb's glow, dust motes in the light). Unlit. The colour sheet scrolls or pulses,
// a separate static mask holds the shape so the motion never leaves the painted object.
// Blend is set per material: alpha blend for water that replaces paint, additive for light.
Shader "Ankhu/PaintedAnim"
{
    Properties
    {
        _BaseMap ("Colour sheet (scrolls)", 2D) = "white" {}
        _Mask ("Static shape mask (alpha)", 2D) = "white" {}
        _Tile ("Sheet tiles across the quad (xy)", Vector) = (1, 1, 0, 0)
        _Scroll ("Scroll, tiles per second (xy)", Vector) = (0, 0, 0, 0)
        _Tile2 ("Second sample tiles (xy)", Vector) = (1, 1, 0, 0)
        _Scroll2 ("Second sample scroll (xy)", Vector) = (0, 0, 0, 0)
        _Mix2 ("Second sample weight", Float) = 0
        _Gain ("Gain", Float) = 1
        _PulseAmp ("Pulse amount", Float) = 0
        _PulseSpeed ("Pulse speed (rad/s)", Float) = 1
        _Flicker ("Flicker amount", Float) = 0
        _UseSheetAlpha ("Multiply by sheet alpha", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            Name "PaintedAnim"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_Mask); SAMPLER(sampler_Mask);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Mask_ST;
                float4 _Tile;
                float4 _Scroll;
                float4 _Tile2;
                float4 _Scroll2;
                float _Mix2;
                float _Gain;
                float _PulseAmp;
                float _PulseSpeed;
                float _Flicker;
                float _UseSheetAlpha;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                // Each layer gets its own phase from where it sits, so glows never pulse in lockstep.
                float3 w = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(sin(dot(w.xy, float2(12.9898, 78.233))) * 43758.5453);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                half4 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * _Tile.xy + _Scroll.xy * t);
                half4 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv * _Tile2.xy + _Scroll2.xy * t + 0.37);
                half3 rgb = a.rgb + b.rgb * _Mix2;
                half sheetA = lerp(1, saturate(a.a + b.a * _Mix2), _UseSheetAlpha);
                float ph = i.seed * 6.2831;
                float pulse = 1 + _PulseAmp * sin(t * _PulseSpeed + ph)
                            + _Flicker * (sin(t * 7.3 + ph * 3) * 0.6 + sin(t * 13.1 + ph * 5) * 0.4);
                half m = SAMPLE_TEXTURE2D(_Mask, sampler_Mask, i.uv).a;
                return half4(rgb * _Gain * max(pulse, 0), m * sheetA);
            }
            ENDHLSL
        }
    }
}
