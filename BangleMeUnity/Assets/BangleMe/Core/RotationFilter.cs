using UnityEngine;

namespace BangleMe
{
    // One Euro applied to orientation: the cutoff rises with angular speed, so a still
    // wrist stays steady while a fast twist is followed without lag.
    public struct RotationFilter
    {
        public readonly float MinCutoff;
        public readonly float Beta;

        Quaternion last;
        double lastTimestamp;
        bool hasSample;

        public RotationFilter(float minCutoff = 2.0f, float beta = 1.5f)
        {
            MinCutoff = minCutoff;
            Beta = beta;
            last = Quaternion.identity;
            lastTimestamp = 0;
            hasSample = false;
        }

        public Quaternion Filter(Quaternion value, double timestamp)
        {
            if (!hasSample)
            {
                hasSample = true;
                last = value;
                lastTimestamp = timestamp;
                return value;
            }

            var dt = (float)(timestamp - lastTimestamp);
            if (dt <= 0) return last;
            lastTimestamp = timestamp;

            var angularSpeed = Quaternion.Angle(last, value) * Mathf.Deg2Rad / dt;
            var alpha = OneEuroFilter.SmoothingAlpha(MinCutoff + Beta * angularSpeed, dt);
            last = Quaternion.Slerp(last, value, alpha);
            return last;
        }
    }
}
