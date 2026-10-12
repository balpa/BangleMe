namespace BangleMe
{
    // What the camera image says about the forearm just behind the wrist, relative to the hand
    // axis (wrist minus middle knuckle) and in the hand tracker's metric scale.
    public readonly struct WristMeasurement
    {
        public readonly bool Valid;
        // Sideways shift of the forearm's center line at the wrist, along the image-space
        // perpendicular (-axis.y, axis.x) of the hand axis.
        public readonly float LateralMeters;
        // Angle from the hand axis to the forearm, positive toward that perpendicular.
        public readonly float BendRadians;
        // Visible forearm width at ReferenceDistance and how fast it widens toward the elbow.
        public readonly float WidthMeters;
        public readonly float WidthGrowth;

        public const float ReferenceDistanceMeters = 0.03f;

        public WristMeasurement(float lateralMeters, float bendRadians, float widthMeters, float widthGrowth)
        {
            Valid = true;
            LateralMeters = lateralMeters;
            BendRadians = bendRadians;
            WidthMeters = widthMeters;
            WidthGrowth = widthGrowth;
        }
    }
}
