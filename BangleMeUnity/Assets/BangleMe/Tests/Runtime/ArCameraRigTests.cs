using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BangleMe.Tests
{
    public class ArCameraRigTests
    {
        const int LongSide = 1920;
        const int ShortSide = 1080;

        ArCameraRig rig;
        Texture2D image;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync("Main");

            image = new Texture2D(LongSide, ShortSide);
            // Keeps the scene from opening the machine's camera.
            Object.FindFirstObjectByType<CameraFeed>().UseStillImage(image);
            rig = Object.FindFirstObjectByType<ArCameraRig>();
        }

        [Test]
        public void ReportedFieldOfView_SetsTheFocalLength()
        {
            rig.Configure(image, 90f);

            Assert.AreEqual(LongSide * 0.5f / Mathf.Tan(45f * Mathf.Deg2Rad), rig.FocalLengthPx, 0.01f);
        }

        [TestCase(10f)]
        [TestCase(150f)]
        public void ImplausibleFieldOfView_FallsBackToTheAssumedOne(float reported)
        {
            rig.Configure(image);
            var assumed = rig.FocalLengthPx;
            // A plausible one in between, so falling back has to change the rig again.
            rig.Configure(image, 90f);

            rig.Configure(image, reported);

            Assert.AreEqual(assumed, rig.FocalLengthPx, 0.01f);
        }
    }
}
