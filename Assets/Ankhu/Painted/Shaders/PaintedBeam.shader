// Painted light beam for LineRenderers: additive, soft across its width and faded at both ends,
// so the live sun shaft and mirror beam read as light in the painting rather than a flat strip.
// LineRenderer UVs (Stretch mode): x runs along the line, y across it. Vertex colour comes from BeamView.
Shader "Ankhu/PaintedBeam"
{
    Properties
    {
        _Warm ("Warm colour", Color) = (1, 0.78, 0.45, 1)
        _WarmMix ("Pull toward warm", Range(0, 1)) = 0.6
        _Gain ("Gain", Range(0, 4)) = 1.2
        _Soft ("Edge softness power", Range(0.5, 6)) = 2.2
        _EndFade ("End fade (share of length)", Range(0, 0.5)) = 0.12
        _Shimmer ("Shimmer", Range(0, 1)) = 0.18
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "PaintedBeam"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Warm;
                half _WarmMix;
                half _Gain;
                half _Soft;
                half _EndFade;
                half _Shimmer;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float3 positionWS : TEXCOORD1; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half across = pow(saturate(sin(saturate(i.uv.y) * PI)), _Soft);
                half along = smoothstep(0, _EndFade, i.uv.x) * smoothstep(1, 1 - _EndFade, i.uv.x);
                half shimmer = 1 - _Shimmer + _Shimmer * (0.5 + 0.5 * sin(i.positionWS.x * 3.1 + i.positionWS.y * 2.3 + _Time.y * 0.9));
                half3 rgb = lerp(i.color.rgb, _Warm.rgb, _WarmMix);
                return half4(rgb * _Gain, saturate(i.color.a * across * along * shimmer));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
