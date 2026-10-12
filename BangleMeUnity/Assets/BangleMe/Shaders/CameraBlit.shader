Shader "Hidden/BangleMe/CameraBlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            // Rows of the 2x2 matrix mapping centered output UVs to centered source UVs.
            float4 _UVTransform;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float2 centered = v.texcoord - 0.5;
                o.uv = float2(dot(_UVTransform.xy, centered), dot(_UVTransform.zw, centered)) + 0.5;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(tex2D(_MainTex, i.uv).rgb, 1);
            }
            ENDCG
        }
    }
}
