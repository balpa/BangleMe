using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BangleMe.Tests
{
    // Runs the real MediaPipe model on a photo, guarding the image orientation handed to it.
    public class HandTrackerTests
    {
        const string ModelPath = "Assets/BangleMe/Models/hand_landmarker.bytes";
        const string PhotoPath = "Assets/BangleMe/Tests/Editor/Fixtures/pointing_up.jpg";

        HandTracker tracker;
        Texture2D photo;

        [SetUp]
        public void SetUp()
        {
            var model = AssetDatabase.LoadAssetAtPath<TextAsset>(ModelPath);
            tracker = new HandTracker(model.bytes, liveStream: false);
            photo = new Texture2D(2, 2);
            photo.LoadImage(File.ReadAllBytes(PhotoPath));
        }

        [TearDown]
        public void TearDown()
        {
            tracker.Dispose();
            Object.DestroyImmediate(photo);
        }

        // In the photo a raised hand sits in the upper right: index fingertip near (0.71, 0.07),
        // wrist near (0.80, 0.33), measured from the top-left corner.
        [Test]
        public void Photo_LandmarksMatchWhereTheHandIs()
        {
            var observation = new HandObservation();
            Assert.IsTrue(tracker.Detect(photo, observation));

            var tip = observation.ImagePoints[HandLandmark.IndexTip];
            var wrist = observation.ImagePoints[HandLandmark.Wrist];
            Assert.AreEqual(0.71f, tip.x, 0.05f);
            Assert.AreEqual(0.07f, tip.y, 0.05f);
            Assert.AreEqual(0.80f, wrist.x, 0.07f);
            Assert.AreEqual(0.33f, wrist.y, 0.07f);
        }

        [Test]
        public void Photo_SolvesWristPoseInFrontOfCamera()
        {
            var observation = new HandObservation();
            Assert.IsTrue(tracker.Detect(photo, observation));

            var focalLengthPx = photo.width * 0.5f / Mathf.Tan(32.5f * Mathf.Deg2Rad);
            var solver = new WristPoseSolver(focalLengthPx, new Vector2(photo.width, photo.height));
            var pose = solver.Solve(observation.ImagePoints, observation.WorldPoints, observation.Confidence);

            Assert.Greater(pose.Confidence, 0);
            Assert.That(pose.Position.z, Is.InRange(0.2f, 2.5f));
            Assert.Greater(pose.Position.x, 0, "hand is right of the image center");
            Assert.Greater(pose.Position.y, 0, "hand is above the image center");
            Assert.Less((pose.Rotation * Vector3.up).y, -0.5f, "forearm runs downward from the wrist");
        }
    }
}
