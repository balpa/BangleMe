using UnityEngine;

namespace BangleMe
{
    // The hand tracker answers some frames after the image it looked at, while the screen shows
    // the newest image; drawn where they were found, the bracelets trail a moving wrist. This
    // carries the last known position forward along its velocity to the moment being drawn.
    public struct MotionPredictor
    {
        // Longer than this the velocity is too old to trust and overshoots when the hand stops.
        public const float MaxLeadSeconds = 0.12f;
        const float VelocitySmoothingSeconds = 0.06f;

        Vector3 position;
        Vector3 velocity;
        double timestamp;
        bool hasPosition;

        public void Reset()
        {
            hasPosition = false;
            velocity = Vector3.zero;
        }

        // timestamp: when the image the position was found in was taken.
        public void Observe(Vector3 observed, double observedTimestamp)
        {
            var dt = (float)(observedTimestamp - timestamp);
            if (hasPosition && dt > 0f)
            {
                var blend = 1f - Mathf.Exp(-dt / VelocitySmoothingSeconds);
                velocity = Vector3.Lerp(velocity, (observed - position) / dt, blend);
            }
            else if (!hasPosition)
            {
                velocity = Vector3.zero;
            }

            position = observed;
            timestamp = observedTimestamp;
            hasPosition = true;
        }

        public Vector3 Predict(double now)
        {
            if (!hasPosition) return position;
            return position + velocity * Mathf.Clamp((float)(now - timestamp), 0f, MaxLeadSeconds);
        }
    }
}
