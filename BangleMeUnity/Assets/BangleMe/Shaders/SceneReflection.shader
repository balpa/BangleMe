// Fills one face of the reflection cubemap the bracelets are lit by: the blurred camera image
// wrapped around the scene, so gold picks up the colors and brightness of the real room, plus
// the studio map for the crisp highlights a blurred image cannot give.
Shader "Hidden/BangleMe/SceneReflection"
{
    Properties
    {
        _MainTex ("Blurred Camera Image", 2D) = "gray" {}
        _Studio ("Studio", Cube) = "black" {}
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
            samplerCUBE _Studio;
            half4 _Studio_HDR;
            float3 _FaceForward;
            float3 _FaceRight;
            float3 _FaceUp;
            float _CameraLod;
            float _SceneGain;
            float _StudioGain;
            float _KeyAzimuth;
            float _LightGain;
            float _KeyLevel;
            float _FillLevel;
            float _RimLevel;
            float _FarRimShare;
            float _KeyCore;
            half4 _NeighbourTint;
            half4 _NeighbourColor;
            float3 _StackAxis;
            float3 _TowardCamera;
            float _Crowding;
            float _BehindLevel;

            // A tall light centered at an azimuth (radians from straight behind the camera,
            // positive toward +X), with the given half widths in azimuth and elevation.
            half Strip(float azimuth, float elevation, float center, float width, float height)
            {
                float offset = azimuth - center;
                offset -= 6.2831853 * round(offset / 6.2831853);
                return exp(-(offset * offset) / (width * width)) * exp(-(elevation * elevation) / (height * height));
            }

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float2 face = i.uv * 2.0 - 1.0;
                float3 direction = normalize(_FaceForward + _FaceRight * face.x + _FaceUp * face.y);

                // The camera only sees what is in front of it; what is behind is assumed to look
                // alike, so both hemispheres fold onto the same image.
                float2 cameraUv = 0.5 + 0.5 * direction.xy / (1.0 + abs(direction.z));
                half3 scene = tex2Dlod(_MainTex, float4(cameraUv, 0, _CameraLod)).rgb;
                half3 studio = DecodeHDR(texCUBElod(_Studio, float4(direction, 0)), _Studio_HDR);

                // Polished metal reads as metal through contrast: a few bright, well-defined
                // lights with darker surroundings between them. A blurred camera image has neither,
                // so it only supplies the surroundings' color and level, and the lights are set up
                // the way jewelry is photographed: tall strips. A bangle stack is a cylinder facing
                // the camera, which mirrors the whole circle around it, so a strip behind the
                // camera lands as a band down the stack's face and strips to the sides as rims.
                float azimuth = atan2(direction.x, -direction.z);
                float elevation = asin(clamp(direction.y, -1.0, 1.0));
                half3 lightColor = lerp(half3(1, 1, 1), scene / max(dot(scene, half3(0.2126, 0.7152, 0.0722)), 0.05), 0.4);

                // A soft body with a hard core: the core is what flashes off beads and edges. A
                // cylinder mirrors twice the angle it turns through, so the key has to be this wide
                // for its band to take up a fifth of the stack rather than a hairline.
                half key = Strip(azimuth, elevation, _KeyAzimuth, 0.6, 2.4) + _KeyCore * Strip(azimuth, elevation, _KeyAzimuth, 0.14, 2.0);
                half fill = Strip(azimuth, elevation, _KeyAzimuth - sign(_KeyAzimuth) * 1.25, 0.34, 1.1);
                // The rim on the key light's side stays; the one across from a hard light fades, so the
                // stack falls into shadow there as the arm does.
                half nearRim = Strip(azimuth, elevation, sign(_KeyAzimuth) * 2.45, 0.16, 1.0);
                half farRim = Strip(azimuth, elevation, -sign(_KeyAzimuth) * 2.45, 0.16, 1.0);
                half rims = nearRim + farRim * _FarRimShare;
                half sky = lerp(0.7, 1.25, saturate(direction.y * 0.5 + 0.5));

                // Between the lights a bangle mostly mirrors its neighbours and the skin, which is
                // why gold's dark tones are a warm brown rather than the olive of dimmed yellow,
                // and why the tint follows the metal the bangles are made of.
                half3 surroundings = (scene * _SceneGain * sky + studio * _StudioGain) * _NeighbourTint.rgb;
                // The lights have to be far brighter than the screen's white to survive as thin
                // highlights, and gold that bright clips to white in every channel. Tinting them
                // by the metal once more lets them clip toward yellow instead, as film does.
                half3 lights = lightColor * _NeighbourTint.rgb * _LightGain * (key * _KeyLevel + fill * _FillLevel + rims * _RimLevel);
                half3 color = surroundings + lights;

                // What lies past the wrist is the arm the bangles sit on, in their own shadow.
                color *= lerp(1.0, _BehindLevel, smoothstep(0.1, 0.9, direction.z) * _Crowding);

                // The flanks of a bangle in a stack face the bangles beside it, not the room: a
                // surface turned 25 degrees or more along the arm shows metal that is itself
                // mostly in shadow. That is where gold gets its deep orange between the crests.
                // The surface that mirrors this direction to the camera faces halfway between the two.
                float3 surfaceNormal = normalize(direction + _TowardCamera);
                float alongArm = abs(dot(surfaceNormal, _StackAxis));
                half neighbour = smoothstep(0.5, 0.85, alongArm) * _Crowding;
                color = lerp(color, _NeighbourColor.rgb, neighbour);
                return half4(color, 1);
            }
            ENDCG
        }
    }
}
