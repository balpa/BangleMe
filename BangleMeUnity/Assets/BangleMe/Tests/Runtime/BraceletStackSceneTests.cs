using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BangleMe.Tests
{
    public class BraceletStackSceneTests
    {
        const float SlideSeconds = 0.5f;

        // Square to the camera, so the arm's own axes are the camera's and +Y is toward the elbow.
        static readonly WristFit Fit = new WristFit(new Vector3(0f, 0f, 0.4f), Quaternion.identity, 0.027f, 0.023f, 2f);

        BraceletStack stack;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Main");

            // Keeps the scene from opening the machine's camera.
            Object.FindFirstObjectByType<CameraFeed>().UseStillImage(new Texture2D(1920, 1080));
            stack = Object.FindFirstObjectByType<BraceletStack>();
        }

        float FirstBangleAlongTheArm(Vector3 gravity)
        {
            stack.Gravity = gravity;
            stack.Apply(Fit, true, SlideSeconds);
            return stack.transform.Find("Bracelet 1").localPosition.y;
        }

        [Test]
        public void GravityTowardTheElbow_SlidesTheFirstBangleFurtherAlongTheArm()
        {
            var towardHand = FirstBangleAlongTheArm(Vector3.down);
            var towardElbow = FirstBangleAlongTheArm(Vector3.up);

            Assert.Greater(towardElbow, towardHand + 0.005f);
        }
    }
}
