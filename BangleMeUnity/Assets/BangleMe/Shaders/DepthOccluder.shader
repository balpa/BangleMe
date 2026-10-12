// The arm stand-in. Writes depth so the camera image stays visible where the arm hides a
// bracelet, and darkens the camera image next to the bracelets like a contact shadow on skin.
Shader "BangleMe/DepthOccluder"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.42, 0.27, 0.22, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.55
        _ShadowFalloff ("Shadow Falloff (m)", Float) = 0.007
        // Extent of the bracelet stack along the arm, set per frame.
        _StackStart ("Stack Start", Float) = 0
        _StackEnd ("Stack End", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry-10" }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShadowColor;
                half _ShadowStrength;
                float _ShadowFalloff;
                float _StackStart;
                float _StackEnd;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float along : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(float4 positionOS : POSITION, float3 normalOS : NORMAL)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.along = positionOS.y;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float outside = max(max(_StackStart - input.along, input.along - _StackEnd), 0.0);
                half shadow = 1.0 - smoothstep(0.0, _ShadowFalloff, outside);

                // The stand-in only approximates the arm's outline; fading toward its silhouette
                // keeps the shadow off the background when it is slightly too wide.
                half facing = saturate(dot(normalize(input.normalWS), normalize(GetCameraPositionWS() - input.positionWS)));
                shadow *= smoothstep(0.15, 0.7, facing) * _ShadowStrength;

                return half4(lerp(half3(1, 1, 1), _ShadowColor.rgb, shadow), 1);
            }
            ENDHLSL
        }
    }
}
