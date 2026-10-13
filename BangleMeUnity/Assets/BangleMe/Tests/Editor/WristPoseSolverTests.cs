using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class WristPoseSolverTests
    {
        const float FocalLengthPx = 1000f;
        static readonly Vector2 ImageSize = new Vector2(1920, 1080);

        // An open hand facing the camera with the fingers pointing up in the image, in MediaPipe's
        // world convention (+Y down): wrist lowest, knuckles 8cm above it.
        static Vector3[] UprightHand()
        {
            var points = new Vector3[HandLandmark.Count];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new Vector3((i % 5 - 2) * 0.015f, -0.02f - (i % 4) * 0.02f, (i % 3 - 1) * 0.01f);
            }
            points[HandLandmark.Wrist] = new Vector3(0, 0.04f, 0);
            points[HandLandmark.IndexMcp] = new Vector3(-0.03f, -0.04f, 0);
            points[HandLandmark.MiddleMcp] = new Vector3(0, -0.04f, 0);
            points[HandLandmark.PinkyMcp] = new Vector3(0.03f, -0.04f, 0);
            return points;
        }

        static Vector2[] Project(Vector3[] worldPoints, Vector3 translation)
        {
            var points = new Vector2[worldPoints.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var p = worldPoints[i] + translation;
                points[i] = new Vector2(
                    FocalLengthPx * p.x / p.z / ImageSize.x + 0.5f,
                    FocalLengthPx * p.y / p.z / ImageSize.y + 0.5f);
            }
            return points;
        }

        [Test]
        public void ProjectedHand_RecoversWristPosition()
        {
            var hand = UprightHand();
            var translation = new Vector3(0.05f, -0.02f, 0.4f);
            var solver = new WristPoseSolver(FocalLengthPx, ImageSize);

            var pose = solver.Solve(Project(hand, translation), hand, 0.9f);

            // Image +Y down becomes camera +Y up.
            var expected = new Vector3(0.05f, -(0.04f - 0.02f), 0.4f);
            Assert.AreEqual(expected.x, pose.Position.x, 0.001f);
            Assert.AreEqual(expected.y, pose.Position.y, 0.001f);
            Assert.AreEqual(expected.z, pose.Position.z, 0.001f);
            Assert.AreEqual(0.9f, pose.Confidence);
        }

        // The tracker's metric hand never matches the image exactly; whatever the fit does with
        // the rest of the hand, the wrist has to land on its own image point.
        [Test]
        public void MismatchedHand_KeepsTheWristOnItsImagePoint()
        {
            var hand = UprightHand();
            var image = Project(hand, new Vector3(0.05f, -0.02f, 0.4f));
            image[HandLandmark.Wrist] += new Vector2(0.02f, 0.03f);
            var solver = new WristPoseSolver(FocalLengthPx, ImageSize);

            var pose = solver.Solve(image, hand, 0.9f);

            var projected = new Vector2(
                FocalLengthPx * pose.Position.x / pose.Position.z / ImageSize.x + 0.5f,
                -FocalLengthPx * pose.Position.y / pose.Position.z / ImageSize.y + 0.5f);
            Assert.AreEqual(image[HandLandmark.Wrist].x, projected.x, 0.0005f);
            Assert.AreEqual(image[HandLandmark.Wrist].y, projected.y, 0.0005f);
        }

        [Test]
        public void HandToRight_YieldsPositiveX()
        {
            var hand = UprightHand();
            var solver = new WristPoseSolver(FocalLengthPx, ImageSize);
            var pose = solver.Solve(Project(hand, new Vector3(0.15f, 0, 0.5f)), hand, 0.8f);
            Assert.Greater(pose.Position.x, 0);
        }

        [Test]
        public void FingersUp_ForearmAxisPointsDown()
        {
            var hand = UprightHand();
            var solver = new WristPoseSolver(FocalLengthPx, ImageSize);
            var pose = solver.Solve(Project(hand, new Vector3(0, 0, 0.4f)), hand, 1f);

            var forearm = pose.Rotation * Vector3.up;
            Assert.AreEqual(-1f, forearm.y, 0.001f);
            var palmNormal = pose.Rotation * Vector3.forward;
            Assert.AreEqual(1f, Mathf.Abs(palmNormal.z), 0.001f);
        }

        [Test]
        public void DegenerateImagePoints_ReturnZeroConfidence()
        {
            var hand = UprightHand();
            var collapsed = new Vector2[HandLandmark.Count];
            for (var i = 0; i < collapsed.Length; i++) collapsed[i] = new Vector2(0.5f, 0.5f);

            var solver = new WristPoseSolver(FocalLengthPx, ImageSize);
            var pose = solver.Solve(collapsed, hand, 0.5f);
            Assert.AreEqual(0, pose.Confidence);
        }
    }
}
