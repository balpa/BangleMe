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
        Emboss,
        Beads
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
            return new BraceletDesign(name, BraceletShape.Strands, extent, extent, 0.9f, strandCount, twists, default, default, 0);
        }

        static BraceletDesign Band(string name, BandProfile profile, float width, float thickness, float smoothness,
            BandPattern pattern = BandPattern.None, float patternLength = 0)
        {
            return new BraceletDesign(name, BraceletShape.Band, width, thickness, smoothness, 0, 0, profile, pattern, patternLength);
        }

        public const float StrandCoreOffset = 0.92f;

        // Thin light bangles move about on the arm; wide heavy ones stay put.
        public float Freedom => Mathf.Clamp01((SettledWidth - Width) / (SettledWidth - LightestWidth));

        const float SettledWidth = 0.004f;
        const float LightestWidth = 0.0015f;

        public float StrandRadius => Width / (2f * (1f + StrandCoreOffset));

        const int ThinIndex = 6;
        const int BeadedIndex = 7;
        // Which of every ten bangles of a thin set are beaded.
        static readonly bool[] BeadedInSet = { false, false, true, false, true, true, false, false, false, true };

        public static bool IsThinSet(int designIndex)
        {
            return designIndex == ThinIndex || designIndex == BeadedIndex;
        }

        // The same bangle without its beading.
        public static int PlainOf(int designIndex)
        {
            return designIndex == BeadedIndex ? ThinIndex : designIndex;
        }

        public static int ThinSetDesign(int position)
        {
            return BeadedInSet[position % BeadedInSet.Length] ? BeadedIndex : ThinIndex;
        }

        public static readonly BraceletDesign[] Catalog =
        {
            Strands("Burma", 2, 0.0023f, 9),
            Band("Ajda", BandProfile.DomedWithRims, 0.012f, 0.0032f, 0.92f, BandPattern.Emboss, 0.008f),
            Band("Hasır", BandProfile.Flat, 0.010f, 0.0018f, 0.86f, BandPattern.Weave, 0.006f),
            Band("Tıraşlı", BandProfile.HalfRound, 0.0045f, 0.0020f, 0.95f, BandPattern.Facets, 0.0045f),
            Band("Düz", BandProfile.HalfRound, 0.0032f, 0.0018f, 0.95f),
            Strands("İnce Burma", 3, 0.0011f, 16),
            Band("İnce", BandProfile.HalfRound, 0.0023f, 0.0021f, 0.94f),
            Band("Boncuklu", BandProfile.HalfRound, 0.0023f, 0.0021f, 0.92f, BandPattern.Beads, 0.0023f),
            Band("Yassı", BandProfile.Flat, 0.0017f, 0.0011f, 0.96f),
        };
    }

    public readonly struct BraceletFinish
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly float SmoothnessScale;
        // Matte sets are made plain: beading is cut to catch light a matte surface does not give.
        public bool Plain => SmoothnessScale < 1f;

        public BraceletFinish(string name, Color color, float smoothnessScale = 1f)
        {
            Name = name;
            Color = color;
            SmoothnessScale = smoothnessScale;
        }

        // sRGB reflectance of the polished metal.
        public static readonly BraceletFinish[] Catalog =
        {
            new BraceletFinish("Sarı", new Color(1.00f, 0.83f, 0.49f)),
            new BraceletFinish("Rose", new Color(0.94f, 0.64f, 0.52f)),
            new BraceletFinish("Beyaz", new Color(0.93f, 0.92f, 0.89f)),
            new BraceletFinish("Mat", new Color(1.00f, 0.85f, 0.55f), 0.6f),
        };
    }
}
