using UnityEngine;

namespace BangleMe
{
    // Draws the bracelets into their own image, which the scene camera then lays over the camera
    // picture slightly blurred and grainy. A render is sharper and cleaner than any phone camera,
    // and it is that difference, more than the lighting, that makes it look pasted on. Only the
    // bracelets' own pixels are touched; the camera image stays as it is.
    [RequireComponent(typeof(Camera))]
    public sealed class BraceletLayer : MonoBehaviour
    {
        static readonly int LayerTextureId = Shader.PropertyToID("_LayerTex");
        static readonly int BlurPixelsId = Shader.PropertyToID("_BlurPixels");
        static readonly int GrainId = Shader.PropertyToID("_Grain");

        [SerializeField] Camera sceneCamera;
        [SerializeField] Renderer composite;
        [Tooltip("Blur radius in pixels for every 1000 pixels of screen height.")]
        [SerializeField] float blurPerThousandPixels = 0.25f;
        [SerializeField, Range(0f, 0.2f)] float grain = 0.03f;

        Camera layerCamera;
        RenderTexture target;
        MaterialPropertyBlock block;

        void LateUpdate()
        {
            Sync();
        }

        // For stills rendered outside the player loop.
        // The bracelets alone on a transparent background; alpha marks the pixels they cover.
        public Texture Texture => target;

        public void RenderNow()
        {
            Sync();
            layerCamera.Render();
        }

        void Sync()
        {
            if (layerCamera == null) layerCamera = GetComponent<Camera>();

            var width = sceneCamera.pixelWidth;
            var height = sceneCamera.pixelHeight;
            if (target == null || target.width != width || target.height != height)
            {
                if (target != null) target.Release();
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                {
                    antiAliasing = 4,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                layerCamera.targetTexture = target;
            }

            layerCamera.fieldOfView = sceneCamera.fieldOfView;
            layerCamera.aspect = width / (float)height;
            layerCamera.nearClipPlane = sceneCamera.nearClipPlane;
            layerCamera.farClipPlane = sceneCamera.farClipPlane;
            layerCamera.depth = sceneCamera.depth - 1f;

            block ??= new MaterialPropertyBlock();
            block.SetTexture(LayerTextureId, target);
            block.SetFloat(BlurPixelsId, blurPerThousandPixels * height / 1000f);
            block.SetFloat(GrainId, grain);
            composite.SetPropertyBlock(block);
        }

        void OnDestroy()
        {
            if (layerCamera != null) layerCamera.targetTexture = null;
            if (target != null) target.Release();
        }
    }
}
