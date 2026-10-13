using UnityEngine;

namespace BangleMe
{
    // Turns the accelerometer's reading into "down" as the camera image shows it.
    public static class DeviceGravity
    {
        // acceleration: in the device's own frame, as the sensor reports it: x toward the right
        // edge and y toward the top edge of the phone held upright, z out of the screen.
        // Returns a unit vector in camera space (x right, y up, z into the image), or straight
        // down the image if the phone is in free fall and reports nothing.
        public static Vector3 InView(Vector3 acceleration, ScreenOrientation orientation, bool frontFacing)
        {
            Vector2 screen;
            switch (orientation)
            {
                case ScreenOrientation.PortraitUpsideDown:
                    screen = new Vector2(-acceleration.x, -acceleration.y);
                    break;
                case ScreenOrientation.LandscapeLeft:
                    screen = new Vector2(-acceleration.y, acceleration.x);
                    break;
                case ScreenOrientation.LandscapeRight:
                    screen = new Vector2(acceleration.y, -acceleration.x);
                    break;
                default:
                    screen = new Vector2(acceleration.x, acceleration.y);
                    break;
            }

            // The back camera looks into the screen, the front one out of it.
            var view = new Vector3(screen.x, screen.y, frontFacing ? acceleration.z : -acceleration.z);
            return view.sqrMagnitude > 1e-6f ? view.normalized : Vector3.down;
        }
    }
}
