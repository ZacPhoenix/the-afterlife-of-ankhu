// Gold rim shell for GoldMagic. Drawn as a second, additive copy of an object's mesh on top of it:
// a fresnel rim, a soft band of light that sweeps upward through the object (world Y), and a faint fill.
// Unlit pass with no LightMode tag (URP draws it as SRPDefaultUnlit), no keywords, included via Resources.
// All parameters are driven per renderer through a MaterialPropertyBlock.
Shader "Ankhu/Realms/GoldRim"
{
    Properties
    {
        [HDR] _Color ("Gold", Color) = (2.4, 1.6, 0.5, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.6
        _Rim ("Rim Strength", Float) = 1
        _Fill ("Fill Strength", Float) = 0.08
        _Band ("Sweep Band Strength", Float) = 0
        _SweepY ("Sweep Band World Y", Float) = -10000
        _SweepWidth ("Sweep Band Width", Float) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Cull Back
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color;
            half _RimPower;
            half _Rim;
            half _Fill;
            half _Band;
            float _SweepY;
            float _SweepWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                half fres = pow(1.0 - saturate(abs(dot(n, viewDir))), _RimPower);
                float d = (i.worldPos.y - _SweepY) / max(_SweepWidth, 0.001);
                half band = exp(-d * d);
                half k = fres * _Rim + band * _Band * (0.35 + 0.65 * fres) + _Fill;
                return half4(_Color.rgb * k, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
