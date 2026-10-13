using System;
using UnityEngine;

namespace BangleMe
{
    // Finds the forearm's edges in the camera image. Hand landmarks stop at the wrist, so they
    // say nothing about how wide the arm is or which way it runs when the wrist is bent; this
    // scans across the arm behind the wrist for the contrast edges on either side of it.
    public static class WristMeasurer
    {
        const int BytesPerPixel = 4;
        const float MinWidthMeters = 0.03f;
        const float MaxWidthMeters = 0.10f;
        const float MaxHalfScanMeters = 0.065f;
        const float MaxBendSlope = 1.2f;
        const float MaxWidthGrowth = 0.5f;

        // Contrast (color distance across a few pixels) that may be the arm's outline.
        const float MinEdgeContrast = 12f;
        // An edge is the outline only if what lies beyond it is no longer skin: a shadow or a
        // tendon on the arm changes brightness but keeps the chroma, the background does not.
        const float MinChromaChange = 10f;
        const float MinLumaChange = 60f;
        const int BeyondEdgeSteps = 3;
        const int GradientSpan = 2;
        const int MaxScanSteps = 256;
        // Shading changes skin's brightness far more than its chroma.
        const float LumaWeight = 0.6f;

        const float MaxCenterColorChange = 45f;
        const float MaxHandChromaChange = 22f;
        const int MaxMissedLines = 2;
        // How far from the center line toward each edge the skin's brightness is compared.
        const float ShadingSampleFraction = 0.6f;
        const int EnvelopePasses = 3;
        // Fraction of the arm's width, and at least this many pixels.
        const float EnvelopeTolerance = 0.04f;
        const float MinEnvelopeTolerancePx = 1.5f;
        const int MinLinesWithoutHand = 5;
        const float HandOutlierTolerances = 3f;

        // Positive turns toward the perpendicular (-axis.y, axis.x) of the hand axis.
        static readonly float[] AxisTurnsDegrees = { 0f, 25f, -25f, 50f, -50f };
        // A turned scan has nothing but the image to vouch for it, so it needs more of the arm.
        const int MinLinesOffAxis = 4;
        const float SkewPenaltyPerRadian = 6f;
        const int EnoughLines = 6;

        static readonly float[] ScanDistancesMeters = { 0.025f, 0.035f, 0.045f, 0.055f, 0.07f, 0.085f, 0.10f, 0.12f, 0.14f, 0.16f };

        // rgba: rows top-down. imagePoints: normalized, origin top-left. worldPoints: meters.
        public static WristMeasurement Measure(ReadOnlySpan<byte> rgba, int width, int height, Vector2[] imagePoints, Vector3[] worldPoints)
        {
            var size = new Vector2(width, height);
            var wrist = Vector2.Scale(imagePoints[HandLandmark.Wrist], size);
            var knuckle = Vector2.Scale(imagePoints[HandLandmark.MiddleMcp], size);
            var handPixels = (wrist - knuckle).magnitude;
            var handWorld = worldPoints[HandLandmark.Wrist] - worldPoints[HandLandmark.MiddleMcp];
            var handMeters = new Vector2(handWorld.x, handWorld.y).magnitude;
            // A hand pointing at the camera has no usable in-plane axis.
            if (handPixels < 8f || handMeters < 0.02f) return default;

            var pixelsPerMeter = handPixels / handMeters;
            var handAxis = (wrist - knuckle) / handPixels;

            // A straight wrist is the common case and the hand axis already runs down the arm.
            // Bent back or forward, the arm leaves that line within a few centimeters, so the
            // scan is retried along directions turned away from the hand.
            var handSkin = SampleYcc(rgba, width, height, Vector2.Lerp(wrist, knuckle, 0.3f));
            var best = default(WristMeasurement);
            var bestScore = float.NegativeInfinity;
            foreach (var turnDegrees in AxisTurnsDegrees)
            {
                var turn = turnDegrees * Mathf.Deg2Rad;
                var lines = MeasureAlong(rgba, width, height, wrist, handAxis, turn, handSkin, pixelsPerMeter, out var measurement, out var skew);
                if (lines < (turnDegrees == 0f ? 1 : MinLinesOffAxis)) continue;

                // The scan that runs most squarely across the arm sees its true width.
                // Past a handful, more lines only mean the arm stayed in the frame for longer.
                var score = Mathf.Min(lines, EnoughLines) - SkewPenaltyPerRadian * Mathf.Abs(skew);
                if (score <= bestScore) continue;
                best = measurement;
                bestScore = score;
            }
            return best;
        }

        // Scans across the arm along the hand axis turned by `turn` and returns how many lines
        // across the arm it found. skew: angle between the scanned axis and the arm it found.
        static int MeasureAlong(ReadOnlySpan<byte> rgba, int width, int height, Vector2 wrist, Vector2 handAxis, float turn,
            Vector3 handSkin, float pixelsPerMeter, out WristMeasurement measurement, out float skew)
        {
            measurement = default;
            skew = 0;
            var handPerpendicular = new Vector2(-handAxis.y, handAxis.x);
            var axis = handAxis * Mathf.Cos(turn) + handPerpendicular * Mathf.Sin(turn);
            var perpendicular = new Vector2(-axis.y, axis.x);
            var maxHalfScan = Mathf.Min(MaxScanSteps - GradientSpan - BeyondEdgeSteps - 1, Mathf.RoundToInt(MaxHalfScanMeters * pixelsPerMeter));

            Span<float> distances = stackalloc float[ScanDistancesMeters.Length];
            Span<float> rights = stackalloc float[ScanDistancesMeters.Length];
            Span<float> lefts = stackalloc float[ScanDistancesMeters.Length];
            Span<float> contrast = stackalloc float[MaxScanSteps];
            var count = 0;
            var misses = 0;
            var lateralGuess = 0f;
            var previousColor = handSkin;
            float brightRight = 0, brightLeft = 0;

            foreach (var distance in ScanDistancesMeters)
            {
                var center = wrist + axis * (distance * pixelsPerMeter) + perpendicular * lateralGuess;
                if (!InBounds(center, width, height)) break;

                // Past these the scan has left bare skin: a sleeve, a watch, another object. The
                // hand is often lit differently from the arm, so only its hue has to carry over.
                var color = SampleYcc(rgba, width, height, center);
                if (count == 0 ? ChromaDistance(color, handSkin) > MaxHandChromaChange : ColorDistance(color, previousColor) > MaxCenterColorChange) break;

                var left = 0f;
                var found = TryFindEdge(rgba, width, height, center, perpendicular, color, maxHalfScan, contrast, out var right)
                         && TryFindEdge(rgba, width, height, center, -perpendicular, color, maxHalfScan, contrast, out left);
                var lineWidth = found ? (right + left) / pixelsPerMeter : 0f;
                if (!found || lineWidth < MinWidthMeters || lineWidth > MaxWidthMeters)
                {
                    if (++misses >= MaxMissedLines) break;
                    continue;
                }

                misses = 0;
                brightRight += SampleYcc(rgba, width, height, center + perpendicular * (right * ShadingSampleFraction)).x;
                brightLeft += SampleYcc(rgba, width, height, center - perpendicular * (left * ShadingSampleFraction)).x;
                distances[count] = distance * pixelsPerMeter;
                rights[count] = lateralGuess + right;
                lefts[count] = lateralGuess - left;
                count++;
                lateralGuess += (right - left) * 0.5f;
                previousColor = color;
            }

            // One line gives no direction, but it still gives the width, which is all there is
            // to go by when the arm leaves the frame just behind the wrist.
            if (count < 1) return 0;

            // A shadow, a tendon or a vein stops single lines short of the outline, never beyond
            // it, so each side of the arm is the outer envelope of what the lines found.
            float meanWidth = 0;
            for (var i = 0; i < count; i++) meanWidth += (rights[i] - lefts[i]) / count;
            var tolerance = Mathf.Max(MinEnvelopeTolerancePx, EnvelopeTolerance * meanWidth);
            FitOuterLine(distances, rights, count, 1f, tolerance, out var rightAtWrist, out var rightSlope);
            FitOuterLine(distances, lefts, count, -1f, tolerance, out var leftAtWrist, out var leftSlope);

            var bendSlope = (rightSlope + leftSlope) * 0.5f;
            if (Mathf.Abs(bendSlope) > MaxBendSlope) return 0;

            // A line that crosses the arm at a slant is longer than the arm is wide.
            skew = Mathf.Atan(bendSlope);
            var widthGrowth = Mathf.Clamp(rightSlope - leftSlope, 0f, MaxWidthGrowth);
            // Through the middle of the measured stretch, which stays right when the slope is clamped.
            var middle = (distances[0] + distances[count - 1]) * 0.5f;
            var reference = WristMeasurement.ReferenceDistanceMeters * pixelsPerMeter;
            var widthAtMiddle = (rightAtWrist - leftAtWrist) + (rightSlope - leftSlope) * middle;
            var referenceWidth = (widthAtMiddle + widthGrowth * (reference - middle)) * Mathf.Cos(skew);
            if (referenceWidth < MinWidthMeters * pixelsPerMeter || referenceWidth > MaxWidthMeters * pixelsPerMeter) return 0;

            measurement = new WristMeasurement(
                perpendicular * ((rightAtWrist + leftAtWrist) * 0.5f),
                (axis + perpendicular * bendSlope).normalized,
                referenceWidth,
                widthGrowth,
                perpendicular * ((brightRight - brightLeft) / Mathf.Max(1f, brightRight + brightLeft)));
            return count;
        }

        // Least-squares line through the points, refitted without those lying inside it.
        // side: +1 when larger values are further out, -1 when smaller ones are.
        static void FitOuterLine(ReadOnlySpan<float> x, ReadOnlySpan<float> y, int count, float side, float tolerance,
            out float intercept, out float slope)
        {
            Span<bool> dropped = stackalloc bool[count];
            var kept = count;

            // The line nearest the hand can still cross the heel of the hand, which is wider than
            // the arm: the one case of a point beyond the outline rather than short of it.
            if (count > MinLinesWithoutHand)
            {
                dropped[0] = true;
                FitLine(x, y, dropped, count, out intercept, out slope);
                // Only lines that agree among themselves can say the first one is off.
                var othersAgree = true;
                for (var i = 1; i < count; i++) othersAgree &= Mathf.Abs(y[i] - (intercept + slope * x[i])) <= 2f * tolerance;
                if (othersAgree && side * (y[0] - (intercept + slope * x[0])) > HandOutlierTolerances * tolerance) kept--;
                else dropped[0] = false;
            }

            FitLine(x, y, dropped, count, out intercept, out slope);
            for (var pass = 0; pass < EnvelopePasses && kept > 2; pass++)
            {
                var removed = 0;
                for (var i = 0; i < count; i++)
                {
                    if (dropped[i] || side * (y[i] - (intercept + slope * x[i])) >= -tolerance) continue;
                    if (kept - removed <= 2) break;
                    dropped[i] = true;
                    removed++;
                }
                if (removed == 0) break;
                kept -= removed;
                FitLine(x, y, dropped, count, out intercept, out slope);
            }
        }

        // Walks outward from a point on the arm to its outline and returns the distance. Fails when
        // the scan leaves the image or the arm does not stand out from what is behind it.
        static bool TryFindEdge(ReadOnlySpan<byte> rgba, int width, int height, Vector2 start, Vector2 direction,
            Vector3 armColor, int maxSteps, Span<float> contrast, out float edge)
        {
            edge = 0;
            var steps = 0;
            for (var step = GradientSpan; step <= maxSteps; step++)
            {
                var inner = start + direction * (step - GradientSpan);
                var outer = start + direction * (step + GradientSpan + BeyondEdgeSteps);
                if (!InBounds(outer, width, height)) break;

                outer = start + direction * (step + GradientSpan);
                contrast[step] = ColorDistance(SampleYcc(rgba, width, height, inner), SampleYcc(rgba, width, height, outer));
                steps = step;
            }

            var darkEdge = 0;
            for (var step = GradientSpan; step <= steps; step++)
            {
                if (contrast[step] < MinEdgeContrast) continue;
                // Follow the edge to its sharpest point.
                while (step < steps && contrast[step + 1] > contrast[step]) step++;

                var beyond = SampleYcc(rgba, width, height, start + direction * (step + GradientSpan + BeyondEdgeSteps)) - armColor;
                var chromaChange = Mathf.Sqrt(beyond.y * beyond.y + beyond.z * beyond.z);
                if (chromaChange >= MinChromaChange || beyond.x >= MinLumaChange)
                {
                    edge = step;
                    return true;
                }
                // Much darker but still skin-colored is usually the arm's own shadow side, with
                // the outline further out; it is the outline only if nothing better follows.
                if (darkEdge == 0 && beyond.x <= -MinLumaChange) darkEdge = step;
            }

            edge = darkEdge;
            return darkEdge > 0;
        }

        static bool InBounds(Vector2 point, int width, int height)
        {
            return point.x >= 1f && point.y >= 1f && point.x < width - 1 && point.y < height - 1;
        }

        static float ChromaDistance(Vector3 a, Vector3 b)
        {
            var d = a - b;
            return Mathf.Sqrt(d.y * d.y + d.z * d.z);
        }

        static float ColorDistance(Vector3 a, Vector3 b)
        {
            var d = a - b;
            d.x *= LumaWeight;
            return d.magnitude;
        }

        // Average of the 3x3 pixels around a point, as luma and chroma.
        static Vector3 SampleYcc(ReadOnlySpan<byte> rgba, int width, int height, Vector2 point)
        {
            var centerX = Mathf.Clamp(Mathf.RoundToInt(point.x), 1, width - 2);
            var centerY = Mathf.Clamp(Mathf.RoundToInt(point.y), 1, height - 2);
            float r = 0, g = 0, b = 0;
            for (var dy = -1; dy <= 1; dy++)
            {
                var offset = ((centerY + dy) * width + centerX - 1) * BytesPerPixel;
                for (var dx = 0; dx < 3; dx++, offset += BytesPerPixel)
                {
                    r += rgba[offset];
                    g += rgba[offset + 1];
                    b += rgba[offset + 2];
                }
            }
            r /= 9f;
            g /= 9f;
            b /= 9f;
            return new Vector3(
                0.299f * r + 0.587f * g + 0.114f * b,
                128f - 0.168736f * r - 0.331264f * g + 0.5f * b,
                128f + 0.5f * r - 0.418688f * g - 0.081312f * b);
        }

        static void FitLine(ReadOnlySpan<float> x, ReadOnlySpan<float> y, ReadOnlySpan<bool> dropped, int count, out float intercept, out float slope)
        {
            float sumX = 0, sumY = 0, sumXx = 0, sumXy = 0;
            var used = 0;
            for (var i = 0; i < count; i++)
            {
                if (dropped[i]) continue;
                sumX += x[i];
                sumY += y[i];
                sumXx += x[i] * x[i];
                sumXy += x[i] * y[i];
                used++;
            }
            var denominator = used * sumXx - sumX * sumX;
            slope = Mathf.Abs(denominator) > 1e-6f ? (used * sumXy - sumX * sumY) / denominator : 0f;
            intercept = (sumY - slope * sumX) / used;
        }
    }
}
