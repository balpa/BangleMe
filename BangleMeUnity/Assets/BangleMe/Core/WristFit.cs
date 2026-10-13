using UnityEngine;

namespace BangleMe
{
    // The forearm as the bracelets need it: a tapered elliptic tube in camera space.
    // Position is on the arm's center line level with the wrist joint; local +Y runs toward the
    // elbow, local +X spans the width the camera sees and local +Z points at the camera.
    public readonly struct WristFit
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float HalfWidth;
        public readonly float HalfThickness;
        // Fraction by which the arm widens per meter toward the elbow.
        public readonly float GrowthPerMeter;

        public WristFit(Vector3 position, Quaternion rotation, float halfWidth, float halfThickness, float growthPerMeter)
        {
            Position = position;
            Rotation = rotation;
            HalfWidth = halfWidth;
            HalfThickness = halfThickness;
            GrowthPerMeter = growthPerMeter;
        }

        public float HalfWidthAt(float distanceFromWrist)
        {
            return HalfWidth * (1f + GrowthPerMeter * Mathf.Max(0f, distanceFromWrist - WristMeasurement.ReferenceDistanceMeters));
        }
    }
}
