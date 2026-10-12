using UnityEngine;

namespace BangleMe
{
    public enum BraceletShape
    {
        // Round wires twisted around each other: burma.
        Strands,
        // A solid band swept around the wrist, detailed by a normal map.
        Band
    }

    public enum BandProfile
    {
        HalfRound,
        // Low dome between two beaded rims.
        DomedWithRims,
        Flat
    }

    public enum BandPattern
    {
        None,
        Facets,
        Weave,
        Emboss
    }

    public readonly struct BraceletDesign
    {
        public readonly string Name;
        public readonly BraceletShape Shape;
        // Extent along the arm and outward from the skin, in meters.
        public readonly float Width;
        public readonly float Thickness;
        public readonly float Smoothness;

        public readonly int StrandCount;
        public readonly int Twists;

        public readonly BandProfile Profile;
        public readonly BandPattern Pattern;
        // Length of one pattern repeat around the wrist, in meters.
        public readonly float PatternLength;

        BraceletDesign(string name, BraceletShape shape, float width, float thickness, float smoothness,
            int strandCount, int twists, BandProfile profile, BandPattern pattern, float patternLength)
        {
            Name = name;
            Shape = shape;
            Width = width;
            Thickness = thickness;
            Smoothness = smoothness;
            StrandCount = strandCount;
            Twists = twists;
            Profile = profile;
            Pattern = pattern;
            PatternLength = patternLength;
        }

        static BraceletDesign Strands(string name, int strandCount, float strandRadius, int twists)
        {
            // Strand centers sit just under one radius from the core so neighbours press together.
            var extent = 2f * strandRadius * (1f + StrandCoreOffset);
            return new BraceletDesign(name, BraceletShape.Strands, extent, extent, 0.93f, strandCount, twists, default, default, 0);
        }

        static BraceletDesign Band(string name, BandProfile profile, float width, float thickness, float smoothness,
            BandPattern pattern = BandPattern.None, float patternLength = 0)
        {
            return new BraceletDesign(name, BraceletShape.Band, width, thickness, smoothness, 0, 0, profile, pattern, patternLength);
        }

        public const float StrandCoreOffset = 0.92f;

        public float StrandRadius => Width / (2f * (1f + StrandCoreOffset));

        public static readonly BraceletDesign[] Catalog =
        {
            Strands("Burma", 2, 0.0023f, 9),
            Band("Ajda", BandProfile.DomedWithRims, 0.012f, 0.0032f, 0.92f, BandPattern.Emboss, 0.008f),
            Band("Hasır", BandProfile.Flat, 0.010f, 0.0018f, 0.86f, BandPattern.Weave, 0.006f),
            Band("Tıraşlı", BandProfile.HalfRound, 0.0045f, 0.0020f, 0.95f, BandPattern.Facets, 0.0045f),
            Band("Düz", BandProfile.HalfRound, 0.0032f, 0.0018f, 0.95f),
            Strands("İnce Burma", 3, 0.0011f, 16),
        };
    }

    public readonly struct BraceletFinish
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly float SmoothnessScale;

        public BraceletFinish(string name, Color color, float smoothnessScale = 1f)
        {
            Name = name;
            Color = color;
            SmoothnessScale = smoothnessScale;
        }

        // sRGB reflectance of the polished metal.
        public static readonly BraceletFinish[] Catalog =
        {
            new BraceletFinish("Sarı", new Color(1.00f, 0.80f, 0.42f)),
            new BraceletFinish("Rose", new Color(0.97f, 0.71f, 0.60f)),
            new BraceletFinish("Beyaz", new Color(0.93f, 0.92f, 0.89f)),
            new BraceletFinish("Mat", new Color(1.00f, 0.80f, 0.42f), 0.6f),
        };
    }
}
