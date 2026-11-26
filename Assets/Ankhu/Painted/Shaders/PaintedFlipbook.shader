// Painted flipbook: a grid sheet of hand-painted frames played on a card. The standard way to animate any
// painted object in a puzzle room (see docs/PAINTED_ROOM_SPEC.md). Unlit, so the paint keeps its own light.
// Loop mode cycles _FrameStart.._FrameStart+_FrameCount-1 at _Fps. One-shot mode (_Loop 0) plays once from
// _StartTime (set per renderer by FlipbookPlay on enable) and holds the last frame.
// Optional top-weighted sway bends cloth, reeds and flames in the vertex stage without leaving the card.
Shader "Ankhu/PaintedFlipbook"
{
    Properties
    {
        _BaseMap ("Frame sheet (grid, row 0 at top)", 2D) = "white" {}
        _Cols ("Columns", Float) = 4
        _Rows ("Rows", Float) = 4
        _FrameStart ("First frame of this state", Float) = 0
        _FrameCount ("Frames in this state", Float) = 16
        _Fps ("Frames per second (0 holds _FrameStart)", Float) = 8
        _Loop ("Loop (1) or play once (0)", Float) = 1
        _StartTime ("One-shot start time (set by FlipbookPlay)", Float) = 0
        _Blend ("Cross-fade between frames", Range(0, 1)) = 1
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Gain ("Gain", Float) = 1
        _PulseAmp ("Pulse amount", Float) = 0
        _PulseSpeed ("Pulse speed (rad/s)", Float) = 1.5
        _Flicker ("Flicker amount", Float) = 0
        _Sway ("Sway at the top edge (object units)", Float) = 0
        _SwaySpeed ("Sway speed (rad/s)", Float) = 1.2
        _AlphaCut ("Alpha cut", Range(0, 1)) = 0.01
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off
        Pass
        {
            Name "PaintedFlipbook"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Cols, _Rows, _FrameStart, _FrameCount, _Fps, _Loop, _StartTime, _Blend;
                half4 _Tint;
                float _Gain, _PulseAmp, _PulseSpeed, _Flicker, _Sway, _SwaySpeed, _AlphaCut;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float seed : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 w = TransformObjectToWorld(float3(0, 0, 0));
                o.seed = frac(sin(dot(w.xz, float2(12.9898, 78.233))) * 43758.5453);
                float3 p = i.positionOS.xyz;
                float top = saturate(i.uv.y);
                p.x += _Sway * top * top * (sin(_Time.y * _SwaySpeed + o.seed * 6.2831) * 0.7 + sin(_Time.y * _SwaySpeed * 2.3 + o.seed * 11.0) * 0.3);
                o.positionCS = TransformObjectToHClip(p);
                o.uv = i.uv;
                return o;
            }

            float2 Cell(float f, float2 uv)
            {
                float col = fmod(f, _Cols);
                float row = floor(f / _Cols);
                return float2((col + uv.x) / _Cols, 1 - (row + 1 - uv.y) / _Rows);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float count = max(_FrameCount, 1);
                float t = _Loop > 0.5 ? (_Time.y + i.seed * count / max(_Fps, 0.001)) : max(_Time.y - _StartTime, 0);
                float pos = t * _Fps;
                float f0, f1, k;
                if (_Loop > 0.5) { f0 = fmod(floor(pos), count); f1 = fmod(f0 + 1, count); k = frac(pos); }
                else { f0 = min(floor(pos), count - 1); f1 = min(f0 + 1, count - 1); k = frac(pos); }
                if (_Fps <= 0) { f0 = 0; f1 = 0; k = 0; }
                half4 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, Cell(_FrameStart + f0, i.uv));
                half4 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, Cell(_FrameStart + f1, i.uv));
                half4 c = lerp(a, b, k * _Blend);
                float ph = i.seed * 6.2831;
                float pulse = 1 + _PulseAmp * sin(_Time.y * _PulseSpeed + ph)
                            + _Flicker * (sin(_Time.y * 7.3 + ph * 3) * 0.6 + sin(_Time.y * 13.1 + ph * 5) * 0.4);
                c.rgb *= _Tint.rgb * _Gain * max(pulse, 0);
                c.a *= _Tint.a;
                clip(c.a - _AlphaCut);
                return c;
            }
            ENDHLSL
        }
    }
}
