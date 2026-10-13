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
        // Skin in its own shadow: far darker, nearly the same chroma.
        static readonly Color32 ShadedSkin = new Color32(128, 78, 52, 255);

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
            return Paint(armCenter, armHalfWidth, 0f, Background);
        }

        // shadedWidth: pixels along the arm's +X edge painted as its shadow side.
        static byte[] Paint(System.Func<int, float> armCenter, System.Func<int, float> armHalfWidth, float shadedWidth, Color32 background)
        {
            var pixels = new byte[Width * Height * 4];
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var skin = y < 125
                        ? y > 20 && Mathf.Abs(x - 165) < 45
                        : Mathf.Abs(x - armCenter(y)) < armHalfWidth(y);
                    var color = skin ? Skin : background;
                    if (skin && y >= 125 && x - armCenter(y) > armHalfWidth(y) - shadedWidth) color = ShadedSkin;
                    var offset = (y * Width + x) * 4;
                    pixels[offset] = color.r;
                    pixels[offset + 1] = color.g;
                    pixels[offset + 2] = color.b;
                    pixels[offset + 3] = 255;
                }
            }
            return pixels;
        }

        // Angle of the measured arm from straight down the image, positive toward +X.
        static float Lean(WristMeasurement measurement)
        {
            return Mathf.Atan2(measurement.Direction.x, measurement.Direction.y);
        }

        [Test]
        public void StraightArm_MeasuresItsWidth()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 28f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(56f, measurement.Width, 3f);
            Assert.AreEqual(0f, measurement.Offset.x, 2f);
            Assert.AreEqual(0f, Lean(measurement), 0.03f);
        }

        // The heel of the hand reaches 3cm past the wrist and is 80px wide, so only the scan line
        // nearest the hand crosses it.
        [Test]
        public void HeelOfTheHandOnTheFirstLine_DoesNotWidenTheArm()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, y => y < 150 ? 40f : 28f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(56f, measurement.Width, 3f);
        }

        [Test]
        public void ShiftedArm_ReportsItsOffset()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 172f, _ => 28f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(12f, measurement.Offset.x, 2f);
        }

        [Test]
        public void SlantedArm_ReportsItsDirection()
        {
            var (image, world) = BuildHand();
            // Drifts 0.3px toward +X per row below the wrist.
            var pixels = Paint(y => 160f + (y - 120) * 0.3f, _ => 28f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(Mathf.Atan(0.3f), Lean(measurement), 0.05f);
        }

        // A wrist bent 40 degrees: the arm is 56mm wide, but a row of the image crosses 73mm of it.
        [Test]
        public void BentWrist_MeasuresAcrossTheArmNotAcrossTheHand()
        {
            var (image, world) = BuildHand();
            var slope = Mathf.Tan(40f * Mathf.Deg2Rad);
            var pixels = Paint(y => 160f + (y - 120) * slope, _ => 28f / Mathf.Cos(40f * Mathf.Deg2Rad));

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(40f * Mathf.Deg2Rad, Lean(measurement), 0.06f);
            Assert.AreEqual(56f, measurement.Width, 5f);
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
        public void ShadedSide_StillMeasuresTheWholeArm()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 28f, 10f, Background);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(56f, measurement.Width, 3f);
            Assert.AreEqual(0f, measurement.Offset.x, 2f);
        }

        [Test]
        public void ShadedSide_PointsTheShadingAtTheLitSide()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 28f, 22f, Background);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.Less(measurement.Shading.x, -0.2f, "the +X side is in shadow, so the light is at -X");
            Assert.AreEqual(0f, measurement.Shading.y, 0.05f);
        }

        [Test]
        public void EvenlyLitArm_HasNoShading()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 28f);

            Assert.AreEqual(0f, WristMeasurer.Measure(pixels, Width, Height, image, world).Shading.magnitude, 0.02f);
        }

        [Test]
        public void DarkSkinTonedBackground_FallsBackToTheBrightnessEdge()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 28f, 0f, ShadedSkin);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(56f, measurement.Width, 3f);
        }

        [Test]
        public void ArmCutOffBehindTheWrist_StillMeasuresItsWidth()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, y => y < 152 ? 28f : 0f);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(56f, measurement.Width, 3f);
            Assert.AreEqual(0f, Lean(measurement), 0.03f);
        }

        [Test]
        public void SkinColoredBackground_IsRejected()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 400f);

            Assert.IsFalse(WristMeasurer.Measure(pixels, Width, Height, image, world).Valid);
        }

        [Test]
        public void ArmInShadowBehindALitHand_IsStillMeasured()
        {
            var (image, world) = BuildHand();
            var pixels = Paint(_ => 160f, _ => 28f, 60f, Background);

            var measurement = WristMeasurer.Measure(pixels, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(56f, measurement.Width, 3f);
        }

        // Covers the rows one scan line samples with a patch that is not skin.
        static void PaintMark(byte[] pixels, int row, int fromX, int toX)
        {
            for (var y = row - 3; y <= row + 3; y++)
            {
                for (var x = fromX; x <= toX; x++)
                {
                    var offset = (y * Width + x) * 4;
                    pixels[offset] = Background.r;
                    pixels[offset + 1] = Background.g;
                    pixels[offset + 2] = Background.b;
                }
            }
        }

        // The scan lines 45mm and 85mm behind the wrist cross rows 165 and 205, where a mark stops
        // them 7px from the center line instead of at the outline.
        [Test]
        public void MarksOnTheArm_DoNotNarrowItsWidth()
        {
            var (image, world) = BuildHand();
            var clean = Paint(_ => 160f, _ => 28f);
            var marked = Paint(_ => 160f, _ => 28f);
            PaintMark(marked, 165, 168, 174);
            PaintMark(marked, 205, 168, 174);

            var unmarkedWidth = WristMeasurer.Measure(clean, Width, Height, image, world).Width;
            var measurement = WristMeasurer.Measure(marked, Width, Height, image, world);

            Assert.IsTrue(measurement.Valid);
            Assert.AreEqual(unmarkedWidth, measurement.Width, 1.5f);
        }
    }
}
