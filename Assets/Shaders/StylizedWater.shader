Shader "WildReach/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.35, 0.75, 0.85, 0.6)
        _DeepColor    ("Deep Color",    Color) = (0.05, 0.22, 0.42, 0.9)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _WaveAmp      ("Wave Amplitude", Range(0, 1)) = 0.15
        _WaveFreq     ("Wave Frequency", Range(0, 3)) = 0.6
        _WaveSpeed    ("Wave Speed", Range(0, 3)) = 1.0
        _RippleScale  ("Ripple Scale", Range(1, 40)) = 12.0
        _RippleSpeed  ("Ripple Speed", Range(0, 2)) = 0.4
        _SpecTint     ("Specular Tint", Color) = (1, 1, 1, 1)
        _Smoothness   ("Smoothness", Range(0, 1)) = 0.85
        _Alpha        ("Alpha", Range(0, 1)) = 0.85
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _SpecTint;
                float  _FresnelPower;
                float  _WaveAmp;
                float  _WaveFreq;
                float  _WaveSpeed;
                float  _RippleScale;
                float  _RippleSpeed;
                float  _Smoothness;
                float  _Alpha;
            CBUFFER_END

            float WaveHeight(float3 p, float t)
            {
                float w = sin(p.x * _WaveFreq + t) * cos(p.z * _WaveFreq * 0.8 + t * 1.1);
                w += 0.5 * sin((p.x + p.z) * _WaveFreq * 1.7 + t * 1.4);
                return w * _WaveAmp;
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float t = _Time.y * _WaveSpeed;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float h = WaveHeight(posWS, t);
                posWS.y += h;

                // approximate the surface normal from the wave gradient
                float e = 0.5;
                float hx = WaveHeight(posWS + float3(e, 0, 0), t);
                float hz = WaveHeight(posWS + float3(0, 0, e), t);
                OUT.normalWS = normalize(float3(h - hx, e, h - hz));

                OUT.positionWS  = posWS;
                OUT.positionHCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3 n = normalize(IN.normalWS);
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), _FresnelPower);

                // moving ripple sparkle
                float t = _Time.y * _RippleSpeed;
                float ripple = sin(IN.positionWS.x * _RippleScale + t) * sin(IN.positionWS.z * _RippleScale - t);
                float3 nr = normalize(n + float3(ripple, 0, ripple) * 0.06);

                float3 col = lerp(_DeepColor.rgb, _ShallowColor.rgb, fresnel);

                Light mainLight = GetMainLight();
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float spec = pow(saturate(dot(nr, halfDir)), lerp(8.0, 256.0, _Smoothness));
                col += mainLight.color * _SpecTint.rgb * spec;

                float alpha = lerp(_Alpha, 1.0, fresnel * 0.5);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
