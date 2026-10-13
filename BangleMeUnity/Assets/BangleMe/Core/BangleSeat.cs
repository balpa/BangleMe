using UnityEngine;

namespace BangleMe
{
    // How a rigid bangle a little wider than the arm sits on it: it hangs from the arm's upper
    // side, and the more room it has, the more freely it leans.
    public readonly struct BangleSeat
    {
        const float TiltPerSlack = 5f;
        const float MaxTiltScale = 1.6f;

        // Sideways shift across the arm's width (x) and depth (y), in meters.
        public readonly Vector2 Hang;
        public readonly float TiltScale;

        BangleSeat(Vector2 hang, float tiltScale)
        {
            Hang = hang;
            TiltScale = tiltScale;
        }

        // gravity: unit vector in the arm's frame (x across its width, y along it, z across its depth).
        public static BangleSeat At(float radius, float armHalfWidth, float armHalfThickness, Vector3 gravity)
        {
            var slack = new Vector2(Mathf.Max(0f, radius - armHalfWidth), Mathf.Max(0f, radius - armHalfThickness));
            var hang = new Vector2(gravity.x * slack.x, gravity.z * slack.y);
            var tiltScale = Mathf.Min(MaxTiltScale, 1f + TiltPerSlack * slack.x / radius);
            return new BangleSeat(hang, tiltScale);
        }
    }
}
