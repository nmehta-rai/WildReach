Shader "WildReach/StudyGrass"
{
    Properties
    {
        _RootColor("Root", Color) = (0.10,0.25,0.10,1)
        _TipColor("Tip", Color) = (0.40,0.68,0.24,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _RootColor;
                half4 _TipColor;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 color:COLOR; half fog:TEXCOORD1; };
            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float tip = saturate(input.positionOS.y / .6);
                p.xz += float2(.035,.018) * sin(_Time.y*1.4+p.x*.9+p.z*.6) * tip*tip;
                output.positionWS=p;
                output.positionCS=TransformWorldToHClip(p);
                output.fog=ComputeFogFactor(output.positionCS.z);
                float tint = saturate(input.color.g / .24);
                output.color=lerp(_RootColor.rgb,_TipColor.rgb,tint);
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow=lerp(.48,1,sun.shadowAttenuation);
                half3 light=SampleSH(float3(0,1,0))*.55 + sun.color*(.6+.4*saturate(sun.direction.y))*shadow;
                return half4(MixFog(input.color*light,input.fog),1);
            }
            ENDHLSL
        }
    }
}
