namespace BangleMe
{
    // User layers set up by the project setup. The bracelets are drawn by their own camera so
    // they can be softened to match the camera image; the arm stand-in is drawn by both.
    public static class RenderLayers
    {
        public const int Bracelets = 6;
        public const int ArmOccluder = 7;

        public const string BraceletsName = "Bracelets";
        public const string ArmOccluderName = "Arm Occluder";
    }
}
