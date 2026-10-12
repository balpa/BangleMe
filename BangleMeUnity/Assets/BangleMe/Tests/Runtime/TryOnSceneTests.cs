using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace BangleMe.Tests
{
    // Runs the real scene on a photo instead of the camera: the whole live path (async
    // detection, fit, filters, UI) has to end with bracelets on screen.
    // Also writes screenshots of the scene with the UI on top to Logs/BangleMePreview/.
    public class TryOnSceneTests
    {
        const string PhotoPath = "Assets/BangleMe/Tests/Editor/Fixtures/fist.jpg";
        const int Width = 780;
        const int Height = 1688;

        [UnityTest]
        public IEnumerator Photo_IsTrackedAndDrawnWithUi()
        {
            yield return SceneManager.LoadSceneAsync("Main");

            var photo = new Texture2D(2, 2);
            photo.LoadImage(File.ReadAllBytes(PhotoPath));
            Object.FindFirstObjectByType<CameraFeed>().UseStillImage(photo);

            var sceneTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Camera.main.targetTexture = sceneTarget;

            var panel = Object.FindFirstObjectByType<UIDocument>().panelSettings;
            var uiTarget = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            panel.targetTexture = uiTarget;
            panel.clearColor = true;
            panel.colorClearValue = Color.clear;

            var pipeline = Object.FindFirstObjectByType<WristTrackingPipeline>();
            var stack = Object.FindFirstObjectByType<BraceletStack>();
            try
            {
                for (var frame = 0; frame < 600 && !pipeline.IsTracking; frame++) yield return null;
                Assert.IsTrue(pipeline.IsTracking, "the hand in the photo was never tracked");

                for (var design = 1; design < BraceletDesign.Catalog.Length; design++) stack.Add(design);
                for (var frame = 0; frame < 30; frame++) yield return null;

                Assert.AreEqual(BraceletDesign.Catalog.Length, stack.Count);
                var visible = 0;
                foreach (var renderer in stack.GetComponentsInChildren<MeshRenderer>())
                {
                    if (renderer.enabled) visible++;
                }
                Assert.Greater(visible, stack.Count, "bracelets and the arm occluder should be drawn");

                SaveScreenshot(sceneTarget, uiTarget, "screen");

                var root = Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
                root.Q("sheet").AddToClassList("sheet--collapsed");
                for (var frame = 0; frame < 5; frame++) yield return null;
                SaveScreenshot(sceneTarget, uiTarget, "screen_collapsed");
            }
            finally
            {
                panel.targetTexture = null;
                panel.clearColor = false;
            }
        }

        static void SaveScreenshot(RenderTexture scene, RenderTexture ui, string name)
        {
            var scenePixels = Read(scene);
            var uiPixels = Read(ui);
            for (var i = 0; i < scenePixels.Length; i++)
            {
                var alpha = uiPixels[i].a / 255f;
                scenePixels[i] = Color32.Lerp(scenePixels[i], uiPixels[i], alpha);
                scenePixels[i].a = 255;
            }

            var composite = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            composite.SetPixels32(scenePixels);
            Directory.CreateDirectory("Logs/BangleMePreview");
            File.WriteAllBytes($"Logs/BangleMePreview/{name}.png", composite.EncodeToPNG());
        }

        static Color32[] Read(RenderTexture source)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            RenderTexture.active = previous;
            return texture.GetPixels32();
        }
    }
}
