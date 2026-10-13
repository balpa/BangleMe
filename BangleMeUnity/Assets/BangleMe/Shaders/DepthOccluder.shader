// The arm stand-in. Writes depth so the camera image stays visible where the arm hides a
// bracelet, and darkens the camera image next to the bracelets like a contact shadow on skin.
Shader "BangleMe/DepthOccluder"
{
    Properties
    {
        _ShadowColor ("Shadow Color", Color) = (0.30, 0.17, 0.11, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.4
        // Skin under the stack sees almost none of the room past the bangles.
        _CoveredStrength ("Shadow Strength Under The Stack", Range(0, 1)) = 0.93
        _ShadowFalloff ("Shadow Falloff (m)", Float) = 0.007
        // How many times longer the shadow gets on the side of the stack away from the light.
        _CastLength ("Cast Shadow Length", Float) = 1.5
        // Along the arm toward the elbow, how much the light comes from that side (-1..1).
        _LightAlong ("Light Along Arm", Float) = 0
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
                half _CoveredStrength;
                float _ShadowFalloff;
                float _StackStart;
                float _StackEnd;
                float _CastLength;
                float _LightAlong;
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
                // The stack throws its shadow down the arm, away from the light.
                float towardHand = _StackStart - input.along;
                float towardElbow = input.along - _StackEnd;
                float outside = max(max(towardHand, towardElbow), 0.0);
                float cast = towardHand > 0.0 ? saturate(_LightAlong) : saturate(-_LightAlong);
                half shadow = 1.0 - smoothstep(0.0, _ShadowFalloff * (1.0 + _CastLength * cast), outside);

                // The stand-in only approximates the arm's outline; fading toward its silhouette
                // keeps the shadow off the background when it is slightly too wide.
                half facing = saturate(dot(normalize(input.normalWS), normalize(GetCameraPositionWS() - input.positionWS)));
                // Deepest well inside the stack; its first bangles sit loose and let light in.
                half covered = smoothstep(0.0, _ShadowFalloff, min(input.along - _StackStart, _StackEnd - input.along));
                shadow *= smoothstep(0.15, 0.7, facing) * lerp(_ShadowStrength, _CoveredStrength, covered);

                return half4(lerp(half3(1, 1, 1), _ShadowColor.rgb, shadow), 1);
            }
            ENDHLSL
        }
    }
}
