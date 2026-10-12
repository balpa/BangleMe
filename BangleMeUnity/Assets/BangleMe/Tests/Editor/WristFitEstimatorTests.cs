using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class WristFitEstimatorTests
    {
        // Fingers up, palm facing the camera: the hand axis points down, toward the elbow.
        static readonly WristPose Pose = new WristPose(
            new Vector3(0f, 0f, 0.4f), Quaternion.LookRotation(Vector3.forward, Vector3.down), 1f);

        [Test]
        public void NoMeasurement_FollowsTheHand()
        {
            var fit = new WristFitEstimator().Estimate(Pose, default, 0);

            Assert.AreEqual(Pose.Position, fit.Position);
            Assert.AreEqual(-1f, (fit.Rotation * Vector3.up).y, 0.001f);
            Assert.Greater(fit.HalfWidth, 0f);
        }

        [Test]
        public void PalmFacingCamera_HalfWidthIsHalfTheVisibleWidth()
        {
            var measurement = new WristMeasurement(0f, 0f, 0.06f, 0.12f);
            var fit = new WristFitEstimator().Estimate(Pose, measurement, 0);

            Assert.AreEqual(0.03f, fit.HalfWidth, 0.0005f);
            Assert.AreEqual(2f, fit.GrowthPerMeter, 0.01f);
            Assert.Less(fit.HalfThickness, fit.HalfWidth);
        }

        // With the axis pointing down, the image-space perpendicular is -X in the image and in
        // camera space alike.
        [Test]
        public void LateralOffset_ShiftsAcrossTheArm()
        {
            var measurement = new WristMeasurement(0.01f, 0f, 0.06f, 0f);
            var fit = new WristFitEstimator().Estimate(Pose, measurement, 0);

            Assert.AreEqual(-0.01f, fit.Position.x, 0.0005f);
            Assert.AreEqual(0f, fit.Position.y, 0.0005f);
        }

        [Test]
        public void Bend_TurnsTheArmAxisTowardThePerpendicular()
        {
            var measurement = new WristMeasurement(0f, 0.3f, 0.06f, 0f);
            var fit = new WristFitEstimator().Estimate(Pose, measurement, 0);

            var axis = fit.Rotation * Vector3.up;
            Assert.AreEqual(-Mathf.Sin(0.3f), axis.x, 0.001f);
            Assert.AreEqual(-Mathf.Cos(0.3f), axis.y, 0.001f);
        }

        [Test]
        public void SizeIsRemembered_WhenMeasurementDropsOut()
        {
            var estimator = new WristFitEstimator();
            estimator.Estimate(Pose, new WristMeasurement(0f, 0f, 0.07f, 0f), 0);
            estimator.ResetPose();

            var fit = estimator.Estimate(Pose, default, 1);
            Assert.AreEqual(0.035f, fit.HalfWidth, 0.0005f);
        }
    }
}
