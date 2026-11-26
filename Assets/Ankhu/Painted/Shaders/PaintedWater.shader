// Painted water: an authored flipbook sheet (frames laid out left to right) tiled in world space,
// so a water quad can be scaled by WaterColumnView without stretching the paint.
Shader "Ankhu/PaintedWater"
{
    Properties
    {
        _Flipbook ("Flipbook sheet", 2D) = "white" {}
        _Frames ("Frames", Float) = 8
        _Fps ("Frames per second", Float) = 8
        _MetresPerTile ("Metres per tile", Float) = 3
        _Tint ("Tint", Color) = (1,1,1,0.9)
        _Deep ("Deep colour", Color) = (0.05, 0.16, 0.2, 1)
        _EdgeSoft ("Side edge softness (m)", Float) = 0.18
        _Surface ("Surface glint colour", Color) = (1, 0.85, 0.6, 1)
        _SurfaceWidth ("Surface glint width (m)", Float) = 0.07
        _SurfaceTex ("Surface strip sheet", 2D) = "black" {}
        _SurfaceHeight ("Surface strip height (m)", Float) = 0.33
        _SurfaceOn ("Use surface strip", Float) = 0
        _BodyAlpha ("Body opacity", Float) = 0.8
        _DepthDark ("Darkening over 4 m", Float) = 0.25
        _ScrollY ("Downward fall, tiles per second", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "PaintedWater"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_Flipbook); SAMPLER(sampler_Flipbook);
            TEXTURE2D(_SurfaceTex); SAMPLER(sampler_SurfaceTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Flipbook_ST;
                float _Frames;
                float _Fps;
                float _MetresPerTile;
                half4 _Tint;
                half4 _Deep;
                float _EdgeSoft;
                half4 _Surface;
                float _SurfaceWidth;
                float _SurfaceHeight;
                float _SurfaceOn;
                float _BodyAlpha;
                float _DepthDark;
                float _ScrollY;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 box : TEXCOORD1; };
            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                // World extents of the column (the quad or cube is unit sized in object space).
                float3 lo = TransformObjectToWorld(float3(-0.5, -0.5, 0));
                float3 hi = TransformObjectToWorld(float3(0.5, 0.5, 0));
                o.box = float4(min(lo.x, hi.x), min(lo.y, hi.y), max(lo.x, hi.x), max(lo.y, hi.y));
                return o;
            }
            half4 SampleFrame(float frame, float2 t)
            {
                return SAMPLE_TEXTURE2D_LOD(_Flipbook, sampler_Flipbook, float2((frame + t.x) / _Frames, t.y), 0);
            }
            half4 frag(Varyings i) : SV_Target
            {
                // Cross-fade neighbouring frames so the flipbook flows instead of stepping.
                float f = _Time.y * _Fps;
                float f0 = floor(fmod(f, _Frames));
                float f1 = fmod(f0 + 1, _Frames);
                float2 t = frac(i.positionWS.xy / _MetresPerTile + float2(0, _Time.y * _ScrollY));
                half4 c = lerp(SampleFrame(f0, t), SampleFrame(f1, t), frac(f)) * _Tint;
                float depth = i.box.w - i.positionWS.y;
                // Darker with depth, like the painted basin.
                c.rgb = lerp(c.rgb, _Deep.rgb, saturate(depth / 4.0) * _DepthDark);
                c.a *= _BodyAlpha;
                if (_SurfaceOn > 0.5)
                {
                    // The master's own splash line and first ripples, pinned to the top of the column.
                    float v = saturate(depth / _SurfaceHeight);
                    float2 st = float2(frac(i.positionWS.x / _MetresPerTile), 1 - v);
                    half4 s0 = SAMPLE_TEXTURE2D_LOD(_SurfaceTex, sampler_SurfaceTex, float2((f0 + st.x) / _Frames, st.y), 0);
                    half4 s1 = SAMPLE_TEXTURE2D_LOD(_SurfaceTex, sampler_SurfaceTex, float2((f1 + st.x) / _Frames, st.y), 0);
                    half4 sc = lerp(s0, s1, frac(f));
                    float w = 1 - smoothstep(0.6, 1.0, v);
                    c.rgb = lerp(c.rgb, sc.rgb, w);
                    c.a = lerp(c.a, 0.95, w);
                }
                // Soft sides so the column sits in the shaft instead of a hard cut-out.
                float side = min(i.positionWS.x - i.box.x, i.box.z - i.positionWS.x);
                c.a *= smoothstep(0, _EdgeSoft, side);
                // A thin warm glint on the surface line.
                float glint = 1 - smoothstep(0, _SurfaceWidth, depth);
                c.rgb += _Surface.rgb * glint * 0.6;
                c.a = max(c.a, glint * 0.7 * smoothstep(0, _EdgeSoft, side));
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
