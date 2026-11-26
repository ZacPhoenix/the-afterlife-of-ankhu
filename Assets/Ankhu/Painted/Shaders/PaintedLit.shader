// Painted 2.5D layer: the colour is authored paint with its lighting baked in. Live point and
// spot lights (flames, lamps) add a little light on top through a normal map built from the paint.
Shader "Ankhu/PaintedLit"
{
    Properties
    {
        _BaseMap ("Paint", 2D) = "white" {}
        _NormalMap ("Normal", 2D) = "bump" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _LightGain ("Live light gain", Range(0, 2)) = 0.35
        _AlphaCut ("Alpha cut", Range(0, 1)) = 0.02
        _ClipMinY ("Clip below world Y", Float) = -100000
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PaintedForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _LightGain;
                half _AlphaCut;
                float _ClipMinY;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Tint;
                clip(c.a - _AlphaCut);
                clip(i.positionWS.y - _ClipMinY);
                half3 n = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv));
                // The layer faces the camera (-Z), so tangent space maps straight to world space.
                half3 nWS = normalize(half3(n.x, n.y, -n.z));
                half3 add = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint count = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(count)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    add += l.color * l.distanceAttenuation * saturate(dot(nWS, l.direction) * 0.6 + 0.4);
                LIGHT_LOOP_END
            #endif
                c.rgb += c.rgb * add * _LightGain;
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
