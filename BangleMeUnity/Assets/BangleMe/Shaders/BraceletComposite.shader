// Lays the bracelet layer over the camera image, softened and with a little grain so it sits in
// the picture the way something photographed by the same camera would.
Shader "BangleMe/BraceletComposite"
{
    Properties
    {
        _LayerTex ("Bracelet Layer", 2D) = "black" {}
        _BlurPixels ("Blur Radius (pixels)", Float) = 1.5
        _Grain ("Grain", Range(0, 0.2)) = 0.03
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Geometry+10" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            // The layer is premultiplied: it was cleared to transparent black and drawn opaque.
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_LayerTex);
            SAMPLER(sampler_LayerTex);

            CBUFFER_START(UnityPerMaterial)
                float _BlurPixels;
                float _Grain;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(float4 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS.xyz);
                return output;
            }

            half4 Layer(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_LayerTex, sampler_LayerTex, uv);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(input.positionCS);
                float2 step = _BlurPixels / _ScreenParams.xy;

                half4 color = Layer(uv) * 4.0;
                color += (Layer(uv + float2(step.x, 0)) + Layer(uv - float2(step.x, 0))
                        + Layer(uv + float2(0, step.y)) + Layer(uv - float2(0, step.y))) * 2.0;
                color += Layer(uv + step) + Layer(uv - step)
                       + Layer(uv + float2(step.x, -step.y)) + Layer(uv + float2(-step.x, step.y));
                color /= 16.0;

                float2 cell = floor(input.positionCS.xy);
                half noise = frac(sin(dot(cell, float2(12.9898, 78.233)) + _Time.y * 61.0) * 43758.5453) - 0.5;
                color.rgb = max(color.rgb + noise * _Grain * color.a, 0.0);
                return color;
            }
            ENDHLSL
        }
    }
}
