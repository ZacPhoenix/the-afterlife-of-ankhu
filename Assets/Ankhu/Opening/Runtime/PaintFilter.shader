// Kuwahara filter for the walk cameras only. The walk's backgrounds are paintings and the cast is 3D. Run over the
// whole frame, this gives both the same brush texture so the figures stop reading as cut-outs on top of the plate.
// Four-quadrant Kuwahara: each pixel takes the mean of whichever neighbouring quadrant has the lowest variance,
// which flattens shading into strokes and keeps edges.
Shader "Ankhu/PaintFilter"
{
    Properties
    {
        _Radius ("Radius (px at 1080p)", Float) = 3
        _Strength ("Strength", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "Paint"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Radius;
            float _Strength;

            void Quadrant(float2 uv, float2 texel, int2 lo, int2 hi, inout float3 bestMean, inout float bestVar)
            {
                float3 sum = 0;
                float3 sq = 0;
                float n = 0;
                [loop] for (int y = lo.y; y <= hi.y; y++)
                {
                    [loop] for (int x = lo.x; x <= hi.x; x++)
                    {
                        float3 c = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + float2(x, y) * texel, 0).rgb;
                        sum += c;
                        sq += c * c;
                        n += 1;
                    }
                }
                float3 mean = sum / n;
                float3 v = abs(sq / n - mean * mean);
                float var = v.r + v.g + v.b;
                if (var < bestVar) { bestVar = var; bestMean = mean; }
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 src = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0);
                // Keep the stroke size the same on screen whatever the resolution.
                int r = (int)clamp(round(_Radius * _ScreenParams.y / 1080.0), 1, 8);
                float2 texel = 1.0 / _ScreenParams.xy;
                float3 best = src.rgb;
                float bestVar = 1e9;
                Quadrant(uv, texel, int2(-r, -r), int2(0, 0), best, bestVar);
                Quadrant(uv, texel, int2(0, -r), int2(r, 0), best, bestVar);
                Quadrant(uv, texel, int2(-r, 0), int2(0, r), best, bestVar);
                Quadrant(uv, texel, int2(0, 0), int2(r, r), best, bestVar);
                return half4(lerp(src.rgb, best, _Strength), src.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
