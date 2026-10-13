using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class WristFitEstimatorTests
    {
        // The wrist is 0.4m away, so a meter there spans 2500 pixels.
        const float FocalLengthPx = 1000f;
        const float PixelsPerMeter = 2500f;

        // Fingers up, palm facing the camera: the hand axis points down, toward the elbow.
        static readonly WristPose Pose = new WristPose(
            new Vector3(0f, 0f, 0.4f), Quaternion.LookRotation(Vector3.forward, Vector3.down), 1f);

        static Vector2 InImage(WristFit fit)
        {
            var axis = fit.Rotation * Vector3.up;
            return new Vector2(axis.x, axis.y).normalized;
        }

        [Test]
        public void NoMeasurement_FollowsTheHand()
        {
            var fit = new WristFitEstimator().Estimate(Pose, default, 0, FocalLengthPx);

            Assert.AreEqual(Pose.Position, fit.Position);
            Assert.AreEqual(-1f, InImage(fit).y, 0.001f);
            Assert.Greater(fit.HalfWidth, 0f);
        }

        [Test]
        public void PalmFacingCamera_HalfWidthIsHalfTheVisibleWidth()
        {
            var measurement = new WristMeasurement(Vector2.zero, Vector2.up, 0.06f * PixelsPerMeter, 0.12f);
            var fit = new WristFitEstimator().Estimate(Pose, measurement, 0, FocalLengthPx);

            Assert.AreEqual(0.03f, fit.HalfWidth, 0.0005f);
            Assert.AreEqual(2f, fit.GrowthPerMeter, 0.01f);
            Assert.Less(fit.HalfThickness, fit.HalfWidth);
        }

        // Image X and camera X agree; only Y is flipped.
        [Test]
        public void Offset_ShiftsAcrossTheArm()
        {
            var measurement = new WristMeasurement(new Vector2(-0.01f * PixelsPerMeter, 0f), Vector2.up, 0.06f * PixelsPerMeter, 0f);
            var fit = new WristFitEstimator().Estimate(Pose, measurement, 0, FocalLengthPx);

            Assert.AreEqual(-0.01f, fit.Position.x, 0.0005f);
            Assert.AreEqual(0f, fit.Position.y, 0.0005f);
        }

        // Down the image is toward the elbow, which is down in camera space.
        [Test]
        public void Offset_ShiftsAlongTheArm()
        {
            var measurement = new WristMeasurement(new Vector2(0f, 0.01f * PixelsPerMeter), Vector2.up, 0.06f * PixelsPerMeter, 0f);
            var fit = new WristFitEstimator().Estimate(Pose, measurement, 0, FocalLengthPx);

            Assert.AreEqual(-0.01f, fit.Position.y, 0.0005f);
        }

        [Test]
        public void Direction_TurnsTheArmAxis()
        {
            var measurement = new WristMeasurement(Vector2.zero, new Vector2(-Mathf.Sin(0.3f), Mathf.Cos(0.3f)), 0.06f * PixelsPerMeter, 0f);
            var fit = new WristFitEstimator().Estimate(Pose, measurement, 0, FocalLengthPx);

            var axis = InImage(fit);
            Assert.AreEqual(-Mathf.Sin(0.3f), axis.x, 0.001f);
            Assert.AreEqual(-Mathf.Cos(0.3f), axis.y, 0.001f);
        }

        // Edge-on, every bangle would be a straight bar ending at the arm's outline.
        [Test]
        public void ArmSquareToTheCamera_IsStillSeenSlightlyAlongItsLength()
        {
            var fit = new WristFitEstimator().Estimate(Pose, default, 0, FocalLengthPx);

            var alongView = Vector3.Dot(fit.Rotation * Vector3.up, fit.Position.normalized);
            Assert.Greater(Mathf.Abs(alongView), 0.2f);
        }

        [Test]
        public void TiltWobblingAroundZero_DoesNotSwingTheStack()
        {
            var estimator = new WristFitEstimator();
            var first = estimator.Estimate(Tilted(5f), default, 0, FocalLengthPx);
            estimator.Estimate(Tilted(-5f), default, 1, FocalLengthPx);
            var later = estimator.Estimate(Tilted(-5f), default, 2, FocalLengthPx);

            Assert.Less(Vector3.Angle(first.Rotation * Vector3.up, later.Rotation * Vector3.up), 2f);
        }

        static WristPose Tilted(float degrees)
        {
            return new WristPose(Pose.Position, Quaternion.AngleAxis(degrees, Vector3.right) * Pose.Rotation, 1f);
        }

        [Test]
        public void ArmAlreadyTilted_KeepsItsTilt()
        {
            var tilted = new WristPose(Pose.Position, Quaternion.AngleAxis(-70f, Vector3.right) * Pose.Rotation, 1f);
            var fit = new WristFitEstimator().Estimate(tilted, default, 0, FocalLengthPx);

            var handAxis = tilted.Rotation * Vector3.up;
            var expected = new Vector3(0f, handAxis.y, handAxis.z * 0.5f).normalized;
            Assert.AreEqual(expected.z, (fit.Rotation * Vector3.up).z, 0.001f);
        }

        [Test]
        public void SizeIsRemembered_WhenMeasurementDropsOut()
        {
            var estimator = new WristFitEstimator();
            estimator.Estimate(Pose, new WristMeasurement(Vector2.zero, Vector2.up, 0.07f * PixelsPerMeter, 0f), 0, FocalLengthPx);
            estimator.ResetPose();

            var fit = estimator.Estimate(Pose, default, 1, FocalLengthPx);
            Assert.AreEqual(0.035f, fit.HalfWidth, 0.0005f);
        }
    }
}
