using UnityEngine;

namespace BangleMe
{
    // Keeps the render camera's projection identical to the (assumed) pinhole model of the device
    // camera and shows the camera image on a quad filling the screen. Because tracking and
    // rendering share one focal length, the bracelet lands on the wrist's pixels even when the
    // assumed field of view is off; only its apparent depth changes.
    public sealed class ArCameraRig : MonoBehaviour
    {
        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        [SerializeField] Camera renderCamera;
        [SerializeField] Renderer background;
        [Tooltip("Field of view along the longer side of the camera image.")]
        [SerializeField] float assumedLongSideFovDegrees = 65f;
        [SerializeField] float backgroundDistance = 20f;

        MaterialPropertyBlock propertyBlock;
        Texture configuredImage;
        Vector2Int configuredImageSize;
        Vector2Int configuredScreenSize;

        public float FocalLengthPx { get; private set; }

        public void Configure(Texture image)
        {
            var imageSize = new Vector2Int(image.width, image.height);
            var screenSize = new Vector2Int(renderCamera.pixelWidth, renderCamera.pixelHeight);
            if (image == configuredImage && imageSize == configuredImageSize && screenSize == configuredScreenSize) return;

            configuredImage = image;
            configuredImageSize = imageSize;
            configuredScreenSize = screenSize;

            var longSide = Mathf.Max(imageSize.x, imageSize.y);
            FocalLengthPx = longSide * 0.5f / Mathf.Tan(assumedLongSideFovDegrees * 0.5f * Mathf.Deg2Rad);

            // Aspect fill: the screen shows the full image along one axis and crops the other.
            var tanHalfVertical = imageSize.y * 0.5f / FocalLengthPx;
            var tanHalfHorizontal = imageSize.x * 0.5f / FocalLengthPx;
            var screenAspect = screenSize.x / (float)screenSize.y;
            var visibleTanHalfVertical = Mathf.Min(tanHalfVertical, tanHalfHorizontal / screenAspect);
            renderCamera.fieldOfView = 2f * Mathf.Atan(visibleTanHalfVertical) * Mathf.Rad2Deg;

            var quad = background.transform;
            quad.localPosition = new Vector3(0, 0, backgroundDistance);
            quad.localRotation = Quaternion.identity;
            quad.localScale = new Vector3(2f * backgroundDistance * tanHalfHorizontal, 2f * backgroundDistance * tanHalfVertical, 1f);
            propertyBlock ??= new MaterialPropertyBlock();
            propertyBlock.SetTexture(BaseMapId, image);
            background.SetPropertyBlock(propertyBlock);
        }
    }
}
