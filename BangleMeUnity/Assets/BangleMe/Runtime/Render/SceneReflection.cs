using UnityEngine;
using UnityEngine.Rendering;

namespace BangleMe
{
    // Builds the reflection environment of the bracelets from the camera image, so the metal
    // mirrors the room it is seen in rather than a studio it was never in.
    public sealed class SceneReflection : MonoBehaviour
    {
        const int BlurSize = 64;
        // Mip of the blurred camera image to read: about 11 texels across the whole view.
        const float BlurLod = 2.5f;

        static readonly int StudioId = Shader.PropertyToID("_Studio");
        static readonly int FaceForwardId = Shader.PropertyToID("_FaceForward");
        static readonly int FaceRightId = Shader.PropertyToID("_FaceRight");
        static readonly int FaceUpId = Shader.PropertyToID("_FaceUp");
        static readonly int CameraLodId = Shader.PropertyToID("_CameraLod");
        static readonly int SceneGainId = Shader.PropertyToID("_SceneGain");
        static readonly int StudioGainId = Shader.PropertyToID("_StudioGain");
        static readonly int KeyAzimuthId = Shader.PropertyToID("_KeyAzimuth");
        static readonly int LightGainId = Shader.PropertyToID("_LightGain");
        static readonly int KeyLevelId = Shader.PropertyToID("_KeyLevel");
        static readonly int FillLevelId = Shader.PropertyToID("_FillLevel");
        static readonly int RimLevelId = Shader.PropertyToID("_RimLevel");
        static readonly int KeyCoreId = Shader.PropertyToID("_KeyCore");
        static readonly int NeighbourTintId = Shader.PropertyToID("_NeighbourTint");
        static readonly int FarRimShareId = Shader.PropertyToID("_FarRimShare");
        static readonly int NeighbourColorId = Shader.PropertyToID("_NeighbourColor");
        static readonly int StackAxisId = Shader.PropertyToID("_StackAxis");
        static readonly int TowardCameraId = Shader.PropertyToID("_TowardCamera");
        static readonly int CrowdingId = Shader.PropertyToID("_Crowding");
        static readonly int BehindLevelId = Shader.PropertyToID("_BehindLevel");

        // Forward, right and up of each cubemap face, in CubemapFace order.
        static readonly Vector3[,] Faces =
        {
            { Vector3.right, Vector3.back, Vector3.up },
            { Vector3.left, Vector3.forward, Vector3.up },
            { Vector3.up, Vector3.right, Vector3.back },
            { Vector3.down, Vector3.right, Vector3.forward },
            { Vector3.forward, Vector3.right, Vector3.up },
            { Vector3.back, Vector3.left, Vector3.up },
        };

        [SerializeField] Shader shader;
        [SerializeField] Cubemap studio;
        [SerializeField] int faceSize = 256;
        [Tooltip("How much of the camera image's own brightness the metal mirrors between the lights.")]
        [SerializeField] float sceneGain = 0.85f;
        [SerializeField] float studioGain = 0.1f;
        [Tooltip("Brightness of the strip lights, before the scene's level.")]
        [SerializeField] float lightGain = 1f;
        [SerializeField] float softKey = 3.5f;
        [SerializeField] float hardKey = 5.5f;
        [SerializeField] float softFill = 1.3f;
        [SerializeField] float hardFill = 0.25f;
        [SerializeField] float rim = 1.5f;
        [Tooltip("Brightness of the key light's narrow center, relative to its body.")]
        [SerializeField] float keyCore = 1.2f;
        [Tooltip("How much of the bangles' own color tints what they mirror between the lights.")]
        [SerializeField, Range(0f, 1f)] float metalInSurroundings = 0.85f;
        [SerializeField] Color skinInSurroundings = new Color(1f, 0.88f, 0.78f);
        [Tooltip("Brightness of a neighbouring bangle as mirrored in the flank of the one beside it.")]
        [SerializeField] float neighbourLevel = 0.26f;
        [Tooltip("How much of the room's brightness is left in what the bangles mirror of the arm behind them.")]
        [SerializeField, Range(0f, 1f)] float behindLevel = 0.5f;
        [Tooltip("Limits of the key light's angle off the camera's axis, in radians.")]
        [SerializeField] Color interreflectionTint = new Color(0.8f, 0.42f, 0.16f);
        [SerializeField] float minKeyAzimuth = 0.4f;
        [SerializeField] float maxKeyAzimuth = 1.1f;

        Material material;
        RenderTexture blurred;
        RenderTexture environment;

        // level: how bright the scene is, which scales the lights added to the camera image.
        // lightDirection: toward the main light, in camera space. directionality: 0 when the
        // scene is evenly lit, 1 when the light clearly comes from one side.
        // metal: color of the bangles themselves, which is most of what each one mirrors.
        // stackPosition, stackAxis: where the stack is and which way the arm runs, in camera
        // space. crowding: 0 for a bangle on its own, 1 for one with a neighbour on either side.
        public void Refresh(Texture cameraImage, float level, Vector3 lightDirection, float directionality, Color metal, Vector3 stackPosition, Vector3 stackAxis, float crowding)
        {
            if (material == null) Create();

            Graphics.Blit(cameraImage, blurred);
            material.mainTexture = blurred;
            material.SetTexture(StudioId, studio);
            material.SetFloat(CameraLodId, BlurLod);
            material.SetFloat(SceneGainId, sceneGain);
            material.SetFloat(StudioGainId, studioGain * level);

            // Never dead center: a light straight behind the camera is the camera's own shadow.
            var azimuth = Mathf.Atan2(lightDirection.x, -lightDirection.z);
            var side = azimuth < 0f ? -1f : 1f;
            material.SetFloat(KeyAzimuthId, side * Mathf.Clamp(Mathf.Abs(azimuth), minKeyAzimuth, maxKeyAzimuth));
            material.SetFloat(LightGainId, lightGain * level);
            // One-sided light: a strong key and little fill. Even light: the two nearly alike.
            material.SetFloat(KeyLevelId, Mathf.Lerp(softKey, hardKey, directionality));
            material.SetFloat(FillLevelId, Mathf.Lerp(softFill, hardFill, directionality));
            material.SetFloat(RimLevelId, rim);
            material.SetFloat(KeyCoreId, keyCore);
            material.SetColor(NeighbourTintId, Color.Lerp(Color.white, metal, metalInSurroundings) * skinInSurroundings);
            material.SetFloat(FarRimShareId, 1f - directionality);
            // Light that reaches a flank has bounced between gold and skin on the way, which takes
            // out most of its green and nearly all of its blue.
            material.SetColor(NeighbourColorId, metal * interreflectionTint * (neighbourLevel * level));
            material.SetVector(StackAxisId, stackAxis.normalized);
            material.SetVector(TowardCameraId, stackPosition.sqrMagnitude > 0f ? -stackPosition.normalized : Vector3.back);
            material.SetFloat(CrowdingId, crowding);
            material.SetFloat(BehindLevelId, behindLevel);

            var previous = RenderTexture.active;
            for (var face = 0; face < 6; face++)
            {
                material.SetVector(FaceForwardId, Faces[face, 0]);
                material.SetVector(FaceRightId, Faces[face, 1]);
                material.SetVector(FaceUpId, Faces[face, 2]);
                Graphics.SetRenderTarget(environment, 0, (CubemapFace)face);
                DrawFullScreen();
            }
            RenderTexture.active = previous;
            environment.GenerateMips();

            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = environment;
        }

        void Create()
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            blurred = new RenderTexture(BlurSize, BlurSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            environment = new RenderTexture(faceSize, faceSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                dimension = TextureDimension.Cube,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        void DrawFullScreen()
        {
            GL.PushMatrix();
            GL.LoadOrtho();
            material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0f, 0f);
            GL.Vertex3(0f, 0f, 0f);
            GL.TexCoord2(1f, 0f);
            GL.Vertex3(1f, 0f, 0f);
            GL.TexCoord2(1f, 1f);
            GL.Vertex3(1f, 1f, 0f);
            GL.TexCoord2(0f, 1f);
            GL.Vertex3(0f, 1f, 0f);
            GL.End();
            GL.PopMatrix();
        }

        void OnDestroy()
        {
            if (material != null) DestroyImmediate(material);
            if (blurred != null) blurred.Release();
            if (environment != null) environment.Release();
        }
    }
}
