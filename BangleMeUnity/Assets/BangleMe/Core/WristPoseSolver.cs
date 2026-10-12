using UnityEngine;

namespace BangleMe
{
    public readonly struct WristPoseSolver
    {
        const float MinDepthMeters = 0.08f;
        const float MaxDepthMeters = 3.0f;

        public readonly float FocalLengthPx;
        public readonly Vector2 ImageSize;

        public WristPoseSolver(float focalLengthPx, Vector2 imageSize)
        {
            FocalLengthPx = focalLengthPx;
            ImageSize = imageSize;
        }

        // imagePoints: normalized [0,1], origin top-left, +Y down.
        // worldPoints: metric hand-local landmarks in camera orientation (+X right, +Y down, +Z away),
        //              as produced by MediaPipe's world landmarks.
        public WristPose Solve(Vector2[] imagePoints, Vector3[] worldPoints, float confidence)
        {
            if (!TrySolveTranslation(imagePoints, worldPoints, out var translation))
            {
                return WristPose.Identity;
            }

            var wrist = ToCameraSpace(worldPoints[HandLandmark.Wrist], translation);
            var indexMcp = ToCameraSpace(worldPoints[HandLandmark.IndexMcp], translation);
            var middleMcp = ToCameraSpace(worldPoints[HandLandmark.MiddleMcp], translation);
            var pinkyMcp = ToCameraSpace(worldPoints[HandLandmark.PinkyMcp], translation);

            var forearm = wrist - middleMcp;
            var across = indexMcp - pinkyMcp;
            var palmNormal = Vector3.Cross(forearm, across);
            if (forearm.sqrMagnitude < 1e-8f || palmNormal.sqrMagnitude < 1e-12f)
            {
                return WristPose.Identity;
            }

            var rotation = Quaternion.LookRotation(palmNormal.normalized, forearm.normalized);
            return new WristPose(wrist, rotation, confidence);
        }

        // The world landmarks already carry the hand's 3D shape in camera orientation, so only the
        // translation is unknown: least-squares fit of the pinhole projection over all landmarks.
        bool TrySolveTranslation(Vector2[] imagePoints, Vector3[] worldPoints, out Vector3 translation)
        {
            translation = default;
            var n = Mathf.Min(imagePoints.Length, worldPoints.Length);
            if (n < 3 || FocalLengthPx <= 0) return false;

            double f = FocalLengthPx;
            double sumU = 0, sumV = 0, sumUv2 = 0, sumBx = 0, sumBy = 0, sumUvB = 0;
            for (var i = 0; i < n; i++)
            {
                double u = (imagePoints[i].x - 0.5) * ImageSize.x;
                double v = (imagePoints[i].y - 0.5) * ImageSize.y;
                var w = worldPoints[i];
                var bx = u * w.z - f * w.x;
                var by = v * w.z - f * w.y;

                sumU += u;
                sumV += v;
                sumUv2 += u * u + v * v;
                sumBx += bx;
                sumBy += by;
                sumUvB += u * bx + v * by;
            }

            // Normal equations for rows [f, 0, -u] and [0, f, -v], with Tx and Ty eliminated.
            var denominator = sumUv2 - (sumU * sumU + sumV * sumV) / n;
            if (denominator < 1e-9) return false;

            var tz = -(sumUvB - (sumU * sumBx + sumV * sumBy) / n) / denominator;
            var tx = (sumBx + sumU * tz) / (n * f);
            var ty = (sumBy + sumV * tz) / (n * f);
            if (tz < MinDepthMeters || tz > MaxDepthMeters) return false;

            translation = new Vector3((float)tx, (float)ty, (float)tz);
            return true;
        }

        static Vector3 ToCameraSpace(Vector3 worldPoint, Vector3 translation)
        {
            return new Vector3(worldPoint.x + translation.x, -(worldPoint.y + translation.y), worldPoint.z + translation.z);
        }
    }
}
