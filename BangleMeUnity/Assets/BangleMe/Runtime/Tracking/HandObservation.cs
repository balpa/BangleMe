using UnityEngine;

namespace BangleMe
{
    public sealed class HandObservation
    {
        // Normalized [0,1], origin top-left of the upright camera image, +Y down.
        public readonly Vector2[] ImagePoints = new Vector2[HandLandmark.Count];
        // Meters, hand-local, camera orientation (+X right, +Y down, +Z away).
        public readonly Vector3[] WorldPoints = new Vector3[HandLandmark.Count];

        public bool Detected;
        public float Confidence;
        public long TimestampMs;
        public double TimestampSeconds => TimestampMs / 1000.0;

        public void CopyFrom(HandObservation other)
        {
            other.ImagePoints.CopyTo(ImagePoints, 0);
            other.WorldPoints.CopyTo(WorldPoints, 0);
            Detected = other.Detected;
            Confidence = other.Confidence;
            TimestampMs = other.TimestampMs;
        }
    }
}
