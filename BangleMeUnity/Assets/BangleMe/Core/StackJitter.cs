using UnityEngine;

namespace BangleMe
{
    // How one bangle of a stack sits off the ideal: loose bangles lean, slide sideways and differ
    // slightly in size, and a stack without that reads as a single machined tube.
    public readonly struct StackJitter
    {
        // Lean of a bangle settled among its neighbours; the loose ones lean up to
        // LargestTiltScale times further.
        public const float MaxTiltDegrees = 2.5f;
        public const float WristEndTiltScale = 1.8f;
        public const float CrosserTiltScale = 1.3f;
        public const float LargestTiltScale = 2.6f;
        public const int WristEndCount = 5;
        const float WristEndRadiusGrowth = 0.07f;
        const float CrosserShare = 0.045f;
        const float FreedomTilt = 0.6f;
        const float FreedomCrossers = 1f;
        public const float MaxShiftMeters = 0.0005f;
        public const float MaxRadiusGrowth = 0.035f;
        public const float MaxToneLoss = 0.24f;
        public const float MaxPolishLoss = 0.07f;
        const int LeanPeriod = 6;
        const float OwnLeanShare = 0.2f;

        // Around the wrist's wide (x) and thin (z) axes.
        public readonly Vector2 TiltDegrees;
        public readonly Vector2 ShiftMeters;
        // Never below 1: a smaller bangle would cut into the arm.
        public readonly float RadiusScale;
        // No two bangles of a set are polished or worn alike: multipliers for color and smoothness.
        public readonly float Tone;
        public readonly float Polish;
        // Turn about the bangle's own axis, so that patterns do not line up from one to the next.
        public readonly float SpinDegrees;

        StackJitter(Vector2 tiltDegrees, Vector2 shiftMeters, float radiusScale, float tone, float polish, float spinDegrees)
        {
            SpinDegrees = spinDegrees;
            Tone = tone;
            Polish = polish;
            TiltDegrees = tiltDegrees;
            ShiftMeters = shiftMeters;
            RadiusScale = radiusScale;
        }

        // Depends only on the position in the stack, so a bangle keeps its pose while others are
        // added or removed and from frame to frame.
        // freedom: 0 for a heavy bangle that stays where it is put, 1 for a thin light one that
        // rides up over its neighbours at every movement.
        public static StackJitter At(int index, float freedom = 0f)
        {
            // The first few bangles have the thin wrist to themselves and the hand to rest on:
            // they sit wider and lean further than those packed in behind them.
            var free = Mathf.Clamp01((WristEndCount - index) / (float)WristEndCount);
            var tiltScale = (1f + free * (WristEndTiltScale - 1f)) * (1f + FreedomTilt * freedom);
            var lean = new Vector2(Lean(index, 0), Lean(index, 1));
            // Here and there one rides up over its neighbours and crosses them.
            if (IsCrosser(index, freedom)) lean = new Vector2(Signed(index, 10), Signed(index, 11)) * CrosserTiltScale;

            return new StackJitter(
                lean * (MaxTiltDegrees * tiltScale),
                new Vector2(Signed(index, 2), Signed(index, 3)) * MaxShiftMeters,
                1f + Unit(index, 4) * MaxRadiusGrowth + free * WristEndRadiusGrowth,
                1f - Unit(index, 5) * MaxToneLoss,
                1f - Unit(index, 6) * MaxPolishLoss,
                Unit(index, 12) * 360f);
        }

        public static bool IsCrosser(int index, float freedom = 0f)
        {
            return index >= WristEndCount && Unit(index, 9) < CrosserShare * (1f + FreedomCrossers * freedom);
        }

        // Bangles rest against their neighbours, so the lean wanders along the stack rather
        // than jumping from one bangle to the next; only a small part is each bangle's own.
        static float Lean(int index, int channel)
        {
            var knot = Mathf.FloorToInt(index / (float)LeanPeriod);
            var t = index / (float)LeanPeriod - knot;
            var shared = Mathf.Lerp(Signed(knot, channel + 8), Signed(knot + 1, channel + 8), t * t * (3f - 2f * t));
            return shared * (1f - OwnLeanShare) + Signed(index, channel) * OwnLeanShare;
        }

        static float Signed(int index, int channel)
        {
            return Unit(index, channel) * 2f - 1f;
        }

        static float Unit(int index, int channel)
        {
            unchecked
            {
                var hash = (uint)index * 0x9E3779B1u + (uint)channel * 0x85EBCA6Bu + 0xC2B2AE35u;
                hash ^= hash >> 15;
                hash *= 0x2C1B3C6Du;
                hash ^= hash >> 12;
                hash *= 0x297A2D39u;
                hash ^= hash >> 15;
                return (hash & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }
    }
}
