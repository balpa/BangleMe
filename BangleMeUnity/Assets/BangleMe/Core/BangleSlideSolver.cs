using System;
using UnityEngine;

namespace BangleMe
{
    // Where each bangle of a stack is along the arm. They are beads on a rod: one line of
    // travel, a fixed order, each stopped by its neighbours, by the hand, and by the place where
    // the arm grows as wide as the bangle itself.
    public sealed class BangleSlideSolver
    {
        const float MaxStepSeconds = 1f / 120f;
        // Gold on skin. Below the static value a bangle at rest stays at rest.
        const float StaticFriction = 0.35f;
        const float KineticFriction = 0.25f;
        const float DampingPerSecond = 2f;
        const float RestSpeed = 1e-3f;

        readonly float gap;
        float[] along = Array.Empty<float>();
        float[] speed = Array.Empty<float>();
        float[] previous = Array.Empty<float>();

        public BangleSlideSolver(float gapBetweenBangles)
        {
            gap = gapBetweenBangles;
        }

        public int Count => along.Length;

        // Distance of the bangle's middle from the wrist joint, toward the elbow.
        public float Along(int index) => along[index];

        public bool IsAtRest
        {
            get
            {
                foreach (var value in speed)
                {
                    if (Mathf.Abs(value) > RestSpeed) return false;
                }
                return true;
            }
        }

        // Bangles already on the arm stay where they have slid to; new ones start at their home.
        public void Resize(ReadOnlySpan<float> homes)
        {
            var kept = Mathf.Min(along.Length, homes.Length);
            Array.Resize(ref along, homes.Length);
            Array.Resize(ref speed, homes.Length);
            Array.Resize(ref previous, homes.Length);
            for (var i = kept; i < homes.Length; i++)
            {
                along[i] = homes[i];
                speed[i] = 0f;
            }
        }

        // pull: acceleration along the arm, positive toward the elbow. press: how hard the bangles
        // are pressed onto the arm, as an acceleration; it is what friction works with.
        public void Step(float seconds, ReadOnlySpan<float> widths, ReadOnlySpan<float> lowest, ReadOnlySpan<float> highest, float pull, float press)
        {
            if (seconds <= 0f || along.Length == 0) return;
            var steps = Mathf.CeilToInt(seconds / MaxStepSeconds);
            var step = seconds / steps;
            for (var i = 0; i < steps; i++) Substep(step, widths, lowest, highest, pull, Mathf.Max(0f, press));
        }

        void Substep(float step, ReadOnlySpan<float> widths, ReadOnlySpan<float> lowest, ReadOnlySpan<float> highest, float pull, float press)
        {
            var count = along.Length;
            for (var i = 0; i < count; i++)
            {
                previous[i] = along[i];
                var moving = Mathf.Abs(speed[i]) > RestSpeed;
                if (!moving && Mathf.Abs(pull) <= StaticFriction * press)
                {
                    speed[i] = 0f;
                    continue;
                }

                var before = speed[i] + pull * step;
                var drag = KineticFriction * press * step;
                // Friction slows a bangle down; it never sends it back the other way.
                var after = Mathf.Abs(before) <= drag ? 0f : before - Mathf.Sign(before) * drag;
                speed[i] = after * Mathf.Max(0f, 1f - DampingPerSecond * step);
                along[i] += speed[i] * step;
            }

            for (var i = 0; i < count; i++)
            {
                var floor = lowest[i];
                if (i > 0) floor = Mathf.Max(floor, along[i - 1] + Pitch(widths, i - 1));
                along[i] = Mathf.Max(along[i], floor);
            }
            for (var i = count - 1; i >= 0; i--)
            {
                var ceiling = highest[i];
                if (i < count - 1) ceiling = Mathf.Min(ceiling, along[i + 1] - Pitch(widths, i));
                along[i] = Mathf.Min(along[i], ceiling);
            }
            // Two bangles in one place is worse than one a little past where it fits, so order wins
            // if the limits leave no room for both.
            for (var i = 1; i < count; i++) along[i] = Mathf.Max(along[i], along[i - 1] + Pitch(widths, i - 1));

            for (var i = 0; i < count; i++) speed[i] = (along[i] - previous[i]) / step;
        }

        float Pitch(ReadOnlySpan<float> widths, int index)
        {
            return (widths[index] + widths[index + 1]) * 0.5f + gap;
        }
    }
}
