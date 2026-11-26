// Character look for the painted walk. URP Lit modelled the cast like plastic figures standing on top of the paintings.
// This shades them the way the paintings do: soft two-tone light from each shot light (wrapped, banded, no specular),
// a flat ambient taken from the painting's colours, and a thin moonlit edge on the silhouette.
Shader "Ankhu/PaintedCharacter"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _Wrap ("Light wrap", Range(0, 1)) = 0.45
        _Band ("Band softness", Range(0.02, 1)) = 0.35
        _Shade ("Shadow side level", Range(0, 1)) = 0.32
        _AmbientFlat ("Ambient flatten", Range(0, 1)) = 0.7
        _RimWidth ("Edge width", Range(0.5, 8)) = 3.2
        _RimStrength ("Edge strength", Range(0, 2)) = 0.55
        _Saturation ("Saturation", Range(0, 1.5)) = 0.85
        _Knee ("Highlight knee", Range(0.2, 1)) = 0.55
        // Kept so the opening's property blocks and the Lit passes reused below find what they expect.
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _Cutoff ("Cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Wrap, _Band, _Shade, _AmbientFlat, _RimWidth, _RimStrength, _Saturation, _Smoothness, _Cutoff, _Knee;
            CBUFFER_END
            // Set by ShotLook on every cut: the painting's sky colour, for the moonlit edge.
            half4 _AnkhuRimTint;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Paint(half ndl, half atten)
            {
                // Wrapped and banded: a soft painted terminator instead of a physical falloff.
                half w = saturate((ndl + _Wrap) / (1.0h + _Wrap));
                half b = smoothstep(0.5h - _Band * 0.5h, 0.5h + _Band * 0.5h, w);
                return lerp(_Shade * w, 1.0h, b) * atten;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));

                // Ambient: mostly one flat colour (the average sky), a little of the directional spread.
                half3 shN = SampleSH(n);
                half3 shFlat = SampleSH(half3(0, 1, 0)) * 0.5h + SampleSH(half3(0, -1, 0)) * 0.5h;
                half3 light = lerp(shN, shFlat, _AmbientFlat);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light main = GetMainLight(shadowCoord);
                light += main.color * Paint(dot(n, main.direction), main.shadowAttenuation * main.distanceAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    light += l.color * Paint(dot(n, l.direction), l.distanceAttenuation);
                }
                #endif

                half3 c = albedo.rgb * light;
                half grey = dot(c, half3(0.299h, 0.587h, 0.114h));
                c = lerp(grey.xxx, c, _Saturation);

                // Painted highlights never clip: white linen rolls off to a warm cream above the knee.
                half lum = dot(c, half3(0.299h, 0.587h, 0.114h));
                half over = max(lum - _Knee, 0.0h);
                half rolled = min(lum, _Knee) + over / (1.0h + over * 2.5h);
                c *= lum > 1e-4h ? rolled / lum : 1.0h;
                c = lerp(c, c * half3(1.0h, 0.95h, 0.86h), saturate(over * 3.0h));

                // A thin moonlit edge on the silhouette, as the painters outline figures against the night.
                half rim = pow(1.0h - saturate(dot(n, v)), _RimWidth) * _RimStrength;
                c += _AnkhuRimTint.rgb * rim;

                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
    FallBack "Universal Render Pipeline/Lit"
}
