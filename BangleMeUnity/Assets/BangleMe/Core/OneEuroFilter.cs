using System;

namespace BangleMe
{
    public struct OneEuroFilter
    {
        public readonly float MinCutoff;
        public readonly float Beta;
        public readonly float DCutoff;

        float lastValue;
        float lastDerivative;
        double lastTimestamp;
        bool hasSample;

        public OneEuroFilter(float minCutoff = 1.0f, float beta = 0.007f, float dCutoff = 1.0f)
        {
            MinCutoff = minCutoff;
            Beta = beta;
            DCutoff = dCutoff;
            lastValue = 0;
            lastDerivative = 0;
            lastTimestamp = 0;
            hasSample = false;
        }

        public float Filter(float value, double timestamp)
        {
            if (!hasSample)
            {
                hasSample = true;
                lastValue = value;
                lastDerivative = 0;
                lastTimestamp = timestamp;
                return value;
            }

            var dt = (float)(timestamp - lastTimestamp);
            if (dt <= 0) return lastValue;
            lastTimestamp = timestamp;

            var dv = (value - lastValue) / dt;
            var dAlpha = SmoothingAlpha(DCutoff, dt);
            var smoothedDerivative = dAlpha * dv + (1 - dAlpha) * lastDerivative;

            var cutoff = MinCutoff + Beta * Math.Abs(smoothedDerivative);
            var alpha = SmoothingAlpha(cutoff, dt);
            var smoothed = alpha * value + (1 - alpha) * lastValue;

            lastValue = smoothed;
            lastDerivative = smoothedDerivative;
            return smoothed;
        }

        internal static float SmoothingAlpha(float cutoff, float dt)
        {
            var tau = 1.0f / (2 * (float)Math.PI * cutoff);
            return 1.0f / (1.0f + tau / dt);
        }
    }
}
