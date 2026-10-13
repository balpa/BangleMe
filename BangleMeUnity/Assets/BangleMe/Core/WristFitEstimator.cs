using UnityEngine;

namespace BangleMe
{
    // Combines the hand tracker's wrist pose with what the image shows of the forearm.
    public sealed class WristFitEstimator
    {
        // How deep the arm is taken to be compared to the width the camera sees. Unknown from one
        // image; it only shapes how the far side of a bangle disappears behind the arm.
        const float ThicknessRatio = 0.85f;
        const float DefaultHalfWidth = 0.027f;
        const float DefaultGrowthPerMeter = 2f;
        const float MinHalfWidth = 0.018f;
        const float MaxHalfWidth = 0.045f;
        // A muscular forearm seen from above widens by half over the length a full stack covers.
        const float MaxGrowthPerMeter = 6f;
        // The hand often tips toward or away from the camera at the wrist while the forearm does
        // not, so only part of the hand's depth tilt is carried over to the arm.
        const float ForearmTiltShare = 0.5f;
        const float BentWristRadians = 0.6f;
        // Seen exactly edge-on a bangle is a straight bar that stops dead at the arm's outline.
        // Arms are never held that square to the lens, and one image cannot tell how far off they
        // are, so the stack is kept at least this far from edge-on and its bangles wrap around.
        const float MinDepthTiltRadians = 0.42f;
        const float TiltSideRadians = 0.05f;
        const float TiltFlipRadians = 0.3f;

        // The arm's direction changes with every movement; its size belongs to the person.
        const float PoseSmoothingSeconds = 0.1f;
        const float SizeSmoothingSeconds = 0.6f;

        // The forearm relative to the hand, so it follows the hand through frames in which the
        // image gives no measurement.
        float lateral;
        float forward;
        float bend;
        float depthTilt;
        float tiltSide = 1f;
        bool hasTilt;
        bool sideChosen;
        float halfWidth = DefaultHalfWidth;
        float growth = DefaultGrowthPerMeter;
        bool hasPose;
        bool hasSize;
        double lastTimestamp;

        // Forget the arm's direction (the hand left the frame) but keep its size.
        public void ResetPose()
        {
            lateral = 0;
            forward = 0;
            bend = 0;
            hasPose = false;
            hasTilt = false;
        }

        // focalLengthPx: of the image the measurement was taken in.
        public WristFit Estimate(WristPose pose, in WristMeasurement measurement, double timestamp, float focalLengthPx)
        {
            var handAxis = pose.Rotation * Vector3.up;
            var inPlane = new Vector2(handAxis.x, handAxis.y);
            var inPlaneLength = inPlane.magnitude;
            if (inPlaneLength < 0.2f)
            {
                return new WristFit(pose.Position, pose.Rotation, halfWidth, halfWidth * ThicknessRatio, growth);
            }

            var along = new Vector3(inPlane.x, inPlane.y, 0f) / inPlaneLength;
            var sideways = new Vector3(along.y, -along.x, 0f);
            var metersPerPixel = pose.Position.z / focalLengthPx;

            var dt = (float)(timestamp - lastTimestamp);
            lastTimestamp = timestamp;
            if (measurement.Valid)
            {
                // The image's +Y runs down; camera space's runs up.
                var offset = new Vector3(measurement.Offset.x, -measurement.Offset.y, 0f) * metersPerPixel;
                var direction = new Vector3(measurement.Direction.x, -measurement.Direction.y, 0f);
                var measuredBend = Mathf.Atan2(Vector3.Dot(direction, sideways), Vector3.Dot(direction, along));

                var poseBlend = hasPose ? Blend(dt, PoseSmoothingSeconds) : 1f;
                lateral = Mathf.Lerp(lateral, Vector3.Dot(offset, sideways), poseBlend);
                forward = Mathf.Lerp(forward, Vector3.Dot(offset, along), poseBlend);
                bend = Mathf.Lerp(bend, measuredBend, poseBlend);
                hasPose = true;
            }

            var forearmInPlane = along * Mathf.Cos(bend) + sideways * Mathf.Sin(bend);
            var position = pose.Position + sideways * lateral + along * forward;

            // Any direction in the plane through the camera and the arm's line in the image projects
            // onto that line, so keeping the axis in it makes a long stack follow the arm on screen
            // whatever the depth tilt turns out to be.
            var imageLinePlane = Vector3.Cross(position, forearmInPlane).normalized;
            // A wrist bent in the image says the hand and the forearm go separate ways, and then
            // the hand's tilt in depth says nothing about the forearm's.
            var tiltShare = ForearmTiltShare * (1f - Mathf.Clamp01(Mathf.Abs(bend) / BentWristRadians));
            var tilted = forearmInPlane * inPlaneLength + Vector3.forward * (handAxis.z * tiltShare);
            var axis = Vector3.ProjectOnPlane(tilted, imageLinePlane).normalized;

            var viewDirection = position.normalized;
            var acrossView = Vector3.ProjectOnPlane(axis, viewDirection).normalized;
            var seenTilt = Mathf.Asin(Mathf.Clamp(Vector3.Dot(axis, viewDirection), -1f, 1f));
            // The hand's tilt is noisy around zero; changing sides on that noise would swing the
            // whole stack through twice the minimum tilt.
            var sideThreshold = sideChosen ? TiltFlipRadians : TiltSideRadians;
            sideChosen = true;
            if (seenTilt * tiltSide < -sideThreshold) tiltSide = -tiltSide;
            var wantedTilt = Mathf.Abs(seenTilt) >= MinDepthTiltRadians ? seenTilt : tiltSide * MinDepthTiltRadians;
            depthTilt = hasTilt ? Mathf.Lerp(depthTilt, wantedTilt, Blend(dt, PoseSmoothingSeconds)) : wantedTilt;
            hasTilt = true;
            axis = acrossView * Mathf.Cos(depthTilt) + viewDirection * Mathf.Sin(depthTilt);

            // Bangles are round, so which way the palm faces changes nothing about them; the frame
            // is turned to the camera instead, and its wide side is exactly what the image shows.
            var normal = Vector3.ProjectOnPlane(-position, axis);
            if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(axis, sideways);
            normal.Normalize();

            if (measurement.Valid)
            {
                var widthMeters = measurement.Width * metersPerPixel;
                var measuredHalfWidth = Mathf.Clamp(widthMeters * 0.5f, MinHalfWidth, MaxHalfWidth);
                var measuredGrowth = Mathf.Clamp(measurement.WidthGrowth / widthMeters, 0f, MaxGrowthPerMeter);

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
