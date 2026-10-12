using UnityEngine;

namespace BangleMe
{
    // Camera-space pose of the wrist: +X right, +Y up, +Z away from the camera, in meters.
    // Local +Y runs along the forearm (toward the elbow), local +Z is the palm normal.
    public readonly struct WristPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly float Confidence;

        public static readonly WristPose Identity = new WristPose(Vector3.zero, Quaternion.identity, 0);

        public WristPose(Vector3 position, Quaternion rotation, float confidence)
        {
            Position = position;
            Rotation = rotation;
            Confidence = confidence;
        }
    }
}
