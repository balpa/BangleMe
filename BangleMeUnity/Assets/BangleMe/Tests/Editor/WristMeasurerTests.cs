using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class WristMeasurerTests
    {
        const int Width = 320;
        const int Height = 320;
        // The synthetic hand is 9cm from wrist to middle knuckle and 90px long in the image.
        const float PixelsPerMeter = 1000f;

        static readonly Color32 Skin = new Color32(214, 160, 130, 255);
        static readonly Color32 Background = new Color32(40, 70, 150, 255);

        // Fingers up: wrist at (160, 120), knuckles 90px above it, so the forearm runs down.
        static (Vector2[] image, Vector3[] world) BuildHand()
        {
            var image = new Vector2[HandLandmark.Count];
            var world = new Vector3[HandLandmark.Count];
            void Set(int landmark, float x, float y)
            {
                image[landmark] = new Vector2(x / Width, y / Height);
                world[landmark] = new Vector3((x - 160f) / PixelsPerMeter, (y - 120f) / PixelsPerMeter, 0);
            }

            for (var i = 0; i < HandLandmark.Count; i++) Set(i, 160, 60);
            Set(HandLandmark.Wrist, 160, 120);
            Set(HandLandmark.IndexMcp, 135, 35);
            Set(HandLandmark.MiddleMcp, 160, 30);
            Set(HandLandmark.RingMcp, 180, 35);
            Set(HandLandmark.PinkyMcp, 195, 45);
            return (image, world);
        }

        // Palm block above the wrist, then an arm whose center and half width are given per row.
        static byte[] Paint(System.Func<int, float> armCenter, System.Func<int, float> armHalfWidth)
        {
            var pixels = new byte[Width * Height * 4];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var skin = y < 125
                        ? y > 20 && Mathf.Abs(x - 165) < 45
                        : Mathf.Abs(x - armCenter(y)) < armHalfWidth(y);
                    var color = skin ? Skin : Background;
                    var offset = (y * Width + x) * 4;
                    pixels[offset] = color.r;
                    pixels[offset + 1] = color.g;
                    pixels[offset + 2] = color.b;
                    pixels[offset + 3] = 255;
                }
            }
            return pixels;
        }

        [Test]
        public void StraightArm_MeasuresItsWidth()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 28f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(0.056f, measurement.WidthMeters, 0.003f);
            Assert.AreEqual(0f, measurement.LateralMeters, 0.002f);
            Assert.AreEqual(0f, measurement.BendRadians, 0.03f);
        }

        // The arm axis points down the image (+Y), so its perpendicular (-axis.y, axis.x) is -X:
        // an arm shifted toward +X measures a negative lateral offset.
        [Test]
        public void ShiftedArm_ReportsLateralOffset()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 172f, _ => 28f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(-0.012f, measurement.LateralMeters, 0.002f);
        }

        [Test]
        public void SlantedArm_ReportsBend()
        {
            var (image, world) = BuildHand();
            // Drifts 0.3px toward +X per row below the wrist.
            var pixels = Paint(y => 160f + (y - 120) * 0.3f, _ => 28f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(-Mathf.Atan(0.3f), measurement.BendRadians, 0.05f);
        }

        [Test]
        public void WideningArm_ReportsGrowth()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, y => 26f + (y - 120) * 0.1f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(0.2f, measurement.WidthGrowth, 0.05f);
        }

        [Test]
        public void SkinColoredBackground_IsRejected()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 400f);

            Assert.IsFalse(WristMeasurer.Measure(pixels, Width, Height, image, world).Valid);
        }
    }
}
