// Stylized grass blades, coloured by the mesh's vertex-colour gradient (dark base -> bright tip)
// with simple sun lighting so it tracks the directional light / day-night. Double-sided. GPU-instancing
// support so a runtime field can draw unlimited blades windowed around the camera. Vertex colours are
// authored linear, so no extra conversion in the fragment shader.
Shader "WildReach/GrassBlade"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                Light mainLight = GetMainLight();
                // grass faces up; soft half-lambert against the sun, plus a little ambient (never black)
                float ndl = saturate(dot(float3(0, 1, 0), mainLight.direction)) * 0.5 + 0.5;
                half3 lit = mainLight.color.rgb * ndl + 0.18;
                return half4(IN.color.rgb * lit, 1);
            }
            ENDHLSL
        }
    }
}
