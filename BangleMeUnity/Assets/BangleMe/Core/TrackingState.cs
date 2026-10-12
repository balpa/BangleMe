using System;

namespace BangleMe
{
    public struct TrackingState
    {
        const float FadeInDuration = 0.2f;
        const float FadeOutDuration = 0.3f;

        public float Opacity { get; private set; }
        public bool IsTracking { get; private set; }

        double lastUpdateTimestamp;
        bool hasUpdate;

        public void Update(bool detected, double timestamp)
        {
            if (hasUpdate)
            {
                var dt = (float)(timestamp - lastUpdateTimestamp);
                Opacity = detected
                    ? Math.Min(1.0f, Opacity + dt / FadeInDuration)
                    : Math.Max(0.0f, Opacity - dt / FadeOutDuration);
            }

            IsTracking = detected;
            lastUpdateTimestamp = timestamp;
            hasUpdate = true;
        }
    }
}
