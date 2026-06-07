Shader "WildReach/Stars"
{
    // Star field for the inside of a sky dome. Additive (black surround adds nothing), rendered in the background
    // behind all geometry, with a gentle twinkle and a _NightFade that the Stars component drives off the sun so
    // they only appear at night. HDR-bright so the scene bloom haloes the brightest stars.
    Properties
    {
        _BaseMap   ("Stars", 2D) = "black" {}
        _Intensity ("Intensity", Float) = 2.5
        _NightFade ("Night fade", Range(0,1)) = 1
        _Twinkle   ("Twinkle", Range(0,1)) = 0.4
    }
    SubShader
    {
        Tags { "Queue"="Background+10" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Front          // render the INSIDE of the dome sphere

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            float  _Intensity, _NightFade, _Twinkle;

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half3 s = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                float tw = 1.0 - _Twinkle * (0.5 + 0.5 * sin(_Time.y * 3.0 + IN.uv.x * 250.0 + IN.uv.y * 130.0));
                return half4(s * _Intensity * _NightFade * tw, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
