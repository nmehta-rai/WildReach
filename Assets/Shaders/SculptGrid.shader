Shader "WildReach/SculptGrid"
{
    // An unlit world-space topographic grid for SHAPING terrain by eye: a fine XZ grid plus
    // horizontal contour lines at fixed height intervals, so slopes and elevation read clearly
    // no matter how the scene is lit. Used as a temporary terrain material (Toggle Sculpt View).
    Properties
    {
        _Base    ("Base",            Color) = (0.50, 0.56, 0.62, 1)
        _Line    ("Grid Line",       Color) = (0.16, 0.19, 0.22, 1)
        _Contour ("Contour Line",    Color) = (0.95, 0.98, 1.00, 1)
        _XZ      ("XZ Spacing (m)",  Float) = 8
        _H       ("Contour Step (m)",Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            float4 _Base, _Line, _Contour;
            float  _XZ, _H;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                return OUT;
            }

            // crisp, screen-constant-width line wherever value crosses a multiple of spacing
            float Line(float v, float spacing, float width)
            {
                float g = v / spacing;
                float d = abs(frac(g - 0.5) - 0.5) / max(fwidth(g), 1e-5);
                return 1.0 - saturate(d / width);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float hgrad = saturate(IN.positionWS.y / 140.0);          // subtle low->high tint
                float3 col  = lerp(_Base.rgb, _Base.rgb * 1.5, hgrad);

                float gx = Line(IN.positionWS.x, _XZ, 1.0);
                float gz = Line(IN.positionWS.z, _XZ, 1.0);
                float gh = Line(IN.positionWS.y, _H, 1.3);                 // elevation contours

                col = lerp(col, _Line.rgb,    saturate(gx + gz));
                col = lerp(col, _Contour.rgb, gh);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
