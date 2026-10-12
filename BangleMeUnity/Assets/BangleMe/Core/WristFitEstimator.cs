using UnityEngine;

namespace BangleMe
{
    // Combines the hand tracker's wrist pose with what the image shows of the forearm.
    public sealed class WristFitEstimator
    {
        // A wrist is roughly this much thinner (back to palm) than it is wide.
        const float ThicknessRatio = 0.72f;
        const float DefaultHalfWidth = 0.027f;
        const float DefaultGrowthPerMeter = 2f;
        const float MinHalfWidth = 0.018f;
        const float MaxHalfWidth = 0.045f;
        const float MaxGrowthPerMeter = 3.5f;
        // The hand often tips toward or away from the camera at the wrist while the forearm does
        // not, so only part of the hand's depth tilt is carried over to the arm.
        const float ForearmTiltShare = 0.5f;

        // The arm's direction changes with every movement; its size belongs to the person.
        const float PoseSmoothingSeconds = 0.1f;
        const float SizeSmoothingSeconds = 0.6f;

        float lateral;
        float bend;
        float halfWidth = DefaultHalfWidth;
        float growth = DefaultGrowthPerMeter;
        bool hasPose;
        bool hasSize;
        double lastTimestamp;

        // Forget the arm's direction (the hand left the frame) but keep its size.
        public void ResetPose()
        {
            lateral = 0;
            bend = 0;
            hasPose = false;
        }

        public WristFit Estimate(WristPose pose, in WristMeasurement measurement, double timestamp)
        {
            var handAxis = pose.Rotation * Vector3.up;
            var palmNormal = pose.Rotation * Vector3.forward;
            var inPlane = new Vector2(handAxis.x, handAxis.y);
            var inPlaneLength = inPlane.magnitude;
            if (inPlaneLength < 0.2f)
            {
                return new WristFit(pose.Position, pose.Rotation, halfWidth, halfWidth * ThicknessRatio, growth);
            }

            var dt = (float)(timestamp - lastTimestamp);
            lastTimestamp = timestamp;
            if (measurement.Valid)
            {
                var poseBlend = hasPose ? Blend(dt, PoseSmoothingSeconds) : 1f;
                lateral = Mathf.Lerp(lateral, measurement.LateralMeters, poseBlend);
                bend = Mathf.Lerp(bend, measurement.BendRadians, poseBlend);
                hasPose = true;
            }

            // The measurement's image axes are +Y down; camera space is +Y up.
            var along = new Vector3(inPlane.x, inPlane.y, 0f) / inPlaneLength;
            var sideways = new Vector3(along.y, -along.x, 0f);
            var forearmInPlane = along * Mathf.Cos(bend) + sideways * Mathf.Sin(bend);
            var position = pose.Position + sideways * lateral;

            // Any direction in the plane through the camera and the arm's line in the image projects
            // onto that line, so keeping the axis in it makes a long stack follow the arm on screen
            // whatever the depth tilt turns out to be.
            var imageLinePlane = Vector3.Cross(position, forearmInPlane).normalized;
            var tilted = forearmInPlane * inPlaneLength + Vector3.forward * (handAxis.z * ForearmTiltShare);
            var axis = Vector3.ProjectOnPlane(tilted, imageLinePlane).normalized;

            var normal = Vector3.ProjectOnPlane(palmNormal, axis);
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.Cross(axis, sideways);
            normal.Normalize();

            if (measurement.Valid)
            {
                // The image shows the ellipse's silhouette, which is its wide side only when the
                // palm or the back of the hand faces the camera.
                var view = Vector3.ProjectOnPlane(position.normalized, axis).normalized;
                var facing = Mathf.Abs(Vector3.Dot(normal, view));
                var silhouette = Mathf.Sqrt(facing * facing + ThicknessRatio * ThicknessRatio * (1f - facing * facing));
                var measuredHalfWidth = Mathf.Clamp(measurement.WidthMeters * 0.5f / silhouette, MinHalfWidth, MaxHalfWidth);
                var measuredGrowth = Mathf.Clamp(measurement.WidthGrowth / measurement.WidthMeters, 0f, MaxGrowthPerMeter);

                var sizeBlend = hasSize ? Blend(dt, SizeSmoothingSeconds) : 1f;
                halfWidth = Mathf.Lerp(halfWidth, measuredHalfWidth, sizeBlend);
                growth = Mathf.Lerp(growth, measuredGrowth, sizeBlend);
                hasSize = true;
            }

            return new WristFit(position, Quaternion.LookRotation(normal, axis), halfWidth, halfWidth * ThicknessRatio, growth);
        }

        static float Blend(float dt, float smoothingSeconds)
        {
            return dt > 0f ? 1f - Mathf.Exp(-dt / smoothingSeconds) : 0f;
        }
    }
}
