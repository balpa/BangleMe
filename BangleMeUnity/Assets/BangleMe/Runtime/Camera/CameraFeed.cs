using System.Collections;
using UnityEngine;

namespace BangleMe
{
    // Owns the device camera and exposes its frames as an upright texture, already rotated and
    // mirrored the way the user sees them. Tracking and rendering both read that same texture.
    public sealed class CameraFeed : MonoBehaviour
    {
        static readonly int UVTransformId = Shader.PropertyToID("_UVTransform");

        [SerializeField] Shader blitShader;
        [SerializeField] bool preferFrontFacing;
        [SerializeField] bool mirrorOnDesktop = true;
        [SerializeField] int requestedWidth = 1280;
        [SerializeField] int requestedHeight = 720;
        [SerializeField] int requestedFps = 60;
        [Tooltip("Shown instead of the device camera; for working on the look without holding up a wrist.")]
        [SerializeField] Texture2D stillImage;

        WebCamTexture webCamTexture;
        WebCamDevice device;
        Material blitMaterial;
        bool restarting;

        public RenderTexture Texture { get; private set; }
        public bool IsReady => Texture != null;
        public bool DidUpdateThisFrame { get; private set; }
        public bool IsMirrored { get; private set; }
        public string Error { get; private set; }
        // Field of view along the image's longer side, or 0 when the platform does not say.
        public float LongSideFovDegrees { get; private set; }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern float BangleMeCameraFieldOfView(bool frontFacing);
#endif

        IEnumerator Start()
        {
            blitMaterial = new Material(blitShader);
            if (stillImage != null) yield break;
            yield return RequestPermission();
            if (stillImage == null) yield return StartDevice();
        }

        public void UseStillImage(Texture2D image)
        {
            stillImage = image;
            StopDevice();
        }

        public void SwitchCamera()
        {
            if (restarting || stillImage != null) return;
            preferFrontFacing = !preferFrontFacing;
            StartCoroutine(StartDevice());
        }

        IEnumerator RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string camera = UnityEngine.Android.Permission.Camera;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(camera))
            {
                var answered = false;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => answered = true;
                callbacks.PermissionDenied += _ => answered = true;
                UnityEngine.Android.Permission.RequestUserPermission(camera, callbacks);
                yield return new WaitUntil(() => answered);
            }
#else
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
#endif
        }

        IEnumerator StartDevice()
        {
            restarting = true;
            StopDevice();
            Error = null;

            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Error = "Kamera bulunamadı";
                restarting = false;
                yield break;
            }

            device = devices[0];
            foreach (var candidate in devices)
            {
                if (candidate.isFrontFacing == preferFrontFacing)
                {
                    device = candidate;
                    break;
                }
            }

            webCamTexture = new WebCamTexture(device.name, requestedWidth, requestedHeight, requestedFps);
            webCamTexture.Play();

            // Until the first real frame arrives the texture reports a 16x16 placeholder size.
            var deadline = Time.realtimeSinceStartup + 10f;
            while (webCamTexture != null && webCamTexture.width <= 16 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            if (webCamTexture != null && webCamTexture.width <= 16)
            {
                Error = "Kamera başlatılamadı";
                StopDevice();
            }
#if UNITY_IOS && !UNITY_EDITOR
            else if (webCamTexture != null)
            {
                // Read once the session runs: the format, and with it the field of view, is
                // only chosen then.
                LongSideFovDegrees = BangleMeCameraFieldOfView(device.isFrontFacing);
            }
#endif
            restarting = false;
        }

        void Update()
        {
            DidUpdateThisFrame = false;
            if (stillImage != null)
            {
                EnsureTexture(stillImage.width, stillImage.height);
                IsMirrored = false;
                Graphics.Blit(stillImage, Texture);
                DidUpdateThisFrame = true;
                return;
            }
            if (restarting || webCamTexture == null || !webCamTexture.didUpdateThisFrame) return;

            var rotation = webCamTexture.videoRotationAngle;
            var sideways = rotation == 90 || rotation == 270;
            var width = sideways ? webCamTexture.height : webCamTexture.width;
            var height = sideways ? webCamTexture.width : webCamTexture.height;
            EnsureTexture(width, height);

            IsMirrored = Application.isMobilePlatform ? device.isFrontFacing : mirrorOnDesktop;
            blitMaterial.SetVector(UVTransformId, UVTransform(rotation, IsMirrored, webCamTexture.videoVerticallyMirrored));
            Graphics.Blit(webCamTexture, Texture, blitMaterial);
            DidUpdateThisFrame = true;
        }

        // Maps centered output UVs to centered source UVs: mirror, then undo the sensor rotation
        // (videoRotationAngle is the clockwise turn needed to show the frame upright), then the
        // platform's vertical flip.
        static Vector4 UVTransform(int rotationDegrees, bool mirror, bool verticallyFlipped)
        {
            var radians = rotationDegrees * Mathf.Deg2Rad;
            var cos = Mathf.Round(Mathf.Cos(radians));
            var sin = Mathf.Round(Mathf.Sin(radians));
            var mx = mirror ? -1f : 1f;
            var my = verticallyFlipped ? -1f : 1f;
            return new Vector4(cos * mx, -sin, my * sin * mx, my * cos);
        }

        void EnsureTexture(int width, int height)
        {
            if (Texture != null && Texture.width == width && Texture.height == height) return;

            ReleaseTexture();
            Texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
            {
                name = "CameraFeed",
                wrapMode = TextureWrapMode.Clamp
            };
            Texture.Create();
        }

        void StopDevice()
        {
            LongSideFovDegrees = 0f;
            if (webCamTexture != null)
            {
                webCamTexture.Stop();
                Destroy(webCamTexture);
                webCamTexture = null;
            }
            ReleaseTexture();
        }

        void ReleaseTexture()
        {
            if (Texture == null) return;
            Texture.Release();
            Destroy(Texture);
            Texture = null;
        }

        void OnDestroy()
        {
            StopDevice();
            if (blitMaterial != null) Destroy(blitMaterial);
        }
    }
}
