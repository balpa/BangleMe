using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BangleMe.Tests
{
    public class WristTrackingPipelineTests
    {
        WristTrackingPipeline pipeline;
        BraceletStack stack;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Main");

            // Keeps the scene from opening the machine's camera.
            Object.FindFirstObjectByType<CameraFeed>().UseStillImage(new Texture2D(1920, 1080));
            pipeline = Object.FindFirstObjectByType<WristTrackingPipeline>();
            stack = Object.FindFirstObjectByType<BraceletStack>();
        }

        [TestCase(1f)]
        [TestCase(-1f)]
        public void ShadingAcrossTheArm_TurnsTheLightToItsBrighterSide(float side)
        {
            var measurement = new WristMeasurement(Vector2.zero, Vector2.up, 150f, 0f, new Vector2(0.2f * side, 0f));

            pipeline.MatchSceneLighting(new byte[64 * 64 * 4], measurement, 1f);

            Assert.Greater(stack.LightDirection.x * side, 0.3f);
        }
    }
}
