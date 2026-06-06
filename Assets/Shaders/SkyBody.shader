Shader "WildReach/SkyBody"
{
    // A flat sun/moon disc from a black-background texture. Alpha is derived from the texture's brightness, so the
    // black surround becomes fully transparent (no visible quad/square) and the bright disc stays solid. The HDR
    // tint pushes it above 1.0 so the scene's bloom haloes it. Always faces the camera (set up by SkyBillboard).
    Properties
    {
        _BaseMap   ("Disc texture (black bg)", 2D) = "black" {}
        [HDR] _BaseColor ("Tint (HDR)", Color) = (1,1,1,1)
        _Softness  ("Edge softness", Range(0.001,0.2)) = 0.04
        _Gain      ("Brightness gain", Range(0.5,4)) = 1.15
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _Softness;
            half   _Gain;

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                o.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                half  lum = max(tex.r, max(tex.g, tex.b));

                // Two masks multiplied:
                //  - radial: a generous circle that just clips the very corners (and guarantees a solid centre),
                //  - bright: cuts the texture's DARK smoke ring + black surround so there's no dark halo, while the
                //    bright disc stays fully opaque (centre is brightest -> always solid -> no hole).
                half2 d = IN.uv - 0.5;
                half  radial = 1.0 - smoothstep(0.47 - _Softness, 0.47, length(d));
                half  bright = smoothstep(0.20, 0.40, lum);
                half  a = radial * bright;

                half3 col = tex * _BaseColor.rgb * _Gain;
                return half4(col, a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
