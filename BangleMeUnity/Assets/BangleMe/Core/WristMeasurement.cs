using UnityEngine;

namespace BangleMe
{
    // What the camera image shows of the forearm just behind the wrist, in the image's own terms
    // (pixels, +Y down), so that drawing it back with the render camera lands on the same pixels
    // whatever the hand tracker's idea of metric scale is.
    public readonly struct WristMeasurement
    {
        public readonly bool Valid;
        // From the wrist landmark to the forearm's center line.
        public readonly Vector2 Offset;
        // Unit vector along the forearm, toward the elbow.
        public readonly Vector2 Direction;
        // Visible forearm width at ReferenceDistance behind the wrist, and how much wider it gets
        // per unit of length toward the elbow.
        public readonly float Width;
        public readonly float WidthGrowth;
        // Points across the arm to its brighter side; as long as the two sides differ in
        // brightness (0 evenly lit, 1 one side black).
        public readonly Vector2 Shading;

        public const float ReferenceDistanceMeters = 0.03f;

        public WristMeasurement(Vector2 offset, Vector2 direction, float width, float widthGrowth, Vector2 shading = default)
        {
            Shading = shading;
            Valid = true;
            Offset = offset;
            Direction = direction;
            Width = width;
            WidthGrowth = widthGrowth;
        }
    }
}
