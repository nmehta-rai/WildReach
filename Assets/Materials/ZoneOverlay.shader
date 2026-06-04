// Editor-only overlay for the Island Builder zone painter. Draws a terrain-conforming mesh
// textured with the zone-colour map, always on top, alpha-blended. Boundary smoothness comes
// from bilinear sampling of the texture, NOT the mesh resolution — so edges are smooth and
// colour-independent regardless of how coarse the conforming mesh is.
Shader "Hidden/WildReach/ZoneOverlay"
{
    Properties { _MainTex ("Zone colours", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return tex2D(_MainTex, i.uv); }
            ENDCG
        }
    }
}
