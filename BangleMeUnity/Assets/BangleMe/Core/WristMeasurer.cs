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

        // Past these the scan has left bare skin: a sleeve, a watch, another object.
        const float MaxCenterColorChange = 45f;
        const float MaxWidthChange = 0.35f;

        static readonly float[] ScanDistancesMeters = { 0.025f, 0.035f, 0.045f, 0.055f, 0.07f, 0.085f, 0.10f, 0.12f };

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
            var axis = (wrist - knuckle) / handPixels;
            var perpendicular = new Vector2(-axis.y, axis.x);
            var maxHalfScan = Mathf.Min(MaxScanSteps - GradientSpan - BeyondEdgeSteps - 1, Mathf.RoundToInt(MaxHalfScanMeters * pixelsPerMeter));

            Span<float> distances = stackalloc float[ScanDistancesMeters.Length];
            Span<float> laterals = stackalloc float[ScanDistancesMeters.Length];
            Span<float> widths = stackalloc float[ScanDistancesMeters.Length];
            Span<float> contrast = stackalloc float[MaxScanSteps];
            var count = 0;
            var lateralGuess = 0f;
            var previousColor = SampleYcc(rgba, width, height, Vector2.Lerp(wrist, knuckle, 0.3f));
            var previousWidth = 0f;

            // Lines must be found in order from the wrist; nothing beyond the first failure is
            // trustworthy.
            foreach (var distance in ScanDistancesMeters)
            {
                var center = wrist + axis * (distance * pixelsPerMeter) + perpendicular * lateralGuess;
                if (!InBounds(center, width, height)) break;

                var color = SampleYcc(rgba, width, height, center);
                if (ColorDistance(color, previousColor) > MaxCenterColorChange) break;
                if (!TryFindEdge(rgba, width, height, center, perpendicular, color, maxHalfScan, contrast, out var right)) break;
                if (!TryFindEdge(rgba, width, height, center, -perpendicular, color, maxHalfScan, contrast, out var left)) break;

                var lineWidth = (right + left) / pixelsPerMeter;
                if (lineWidth < MinWidthMeters || lineWidth > MaxWidthMeters) break;
                if (count > 0 && Mathf.Abs(lineWidth - previousWidth) > MaxWidthChange * previousWidth) break;

                lateralGuess += (right - left) * 0.5f;
                distances[count] = distance;
                laterals[count] = lateralGuess / pixelsPerMeter;
                widths[count] = lineWidth;
                count++;
                previousColor = color;
                previousWidth = lineWidth;
            }

            if (count < 2) return default;

            FitLine(distances, laterals, count, out var lateralAtWrist, out var bendSlope);
            FitLine(distances, widths, count, out var widthAtWrist, out var widthGrowth);
            if (Mathf.Abs(bendSlope) > MaxBendSlope) return default;

            widthGrowth = Mathf.Clamp(widthGrowth, 0f, MaxWidthGrowth);
            var referenceWidth = Mathf.Clamp(
                widthAtWrist + widthGrowth * WristMeasurement.ReferenceDistanceMeters, MinWidthMeters, MaxWidthMeters);
            return new WristMeasurement(lateralAtWrist, Mathf.Atan(bendSlope), referenceWidth, widthGrowth);
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

            for (var step = GradientSpan; step <= steps; step++)
            {
                if (contrast[step] < MinEdgeContrast) continue;
                // Follow the edge to its sharpest point.
                while (step < steps && contrast[step + 1] > contrast[step]) step++;

                var beyond = SampleYcc(rgba, width, height, start + direction * (step + GradientSpan + BeyondEdgeSteps)) - armColor;
                var chromaChange = Mathf.Sqrt(beyond.y * beyond.y + beyond.z * beyond.z);
                if (chromaChange >= MinChromaChange || Mathf.Abs(beyond.x) >= MinLumaChange)
                {
                    edge = step;
                    return true;
                }
            }
            return false;
        }

        static bool InBounds(Vector2 point, int width, int height)
        {
            return point.x >= 1f && point.y >= 1f && point.x < width - 1 && point.y < height - 1;
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

        static void FitLine(ReadOnlySpan<float> x, ReadOnlySpan<float> y, int count, out float intercept, out float slope)
        {
            float sumX = 0, sumY = 0, sumXx = 0, sumXy = 0;
            for (var i = 0; i < count; i++)
            {
                sumX += x[i];
                sumY += y[i];
                sumXx += x[i] * x[i];
                sumXy += x[i] * y[i];
            }
            var denominator = count * sumXx - sumX * sumX;
            slope = Mathf.Abs(denominator) > 1e-12f ? (count * sumXy - sumX * sumY) / denominator : 0f;
            intercept = (sumY - slope * sumX) / count;
        }
    }
}
