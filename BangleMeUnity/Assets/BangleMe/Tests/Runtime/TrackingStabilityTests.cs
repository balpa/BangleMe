using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace BangleMe.Tests
{
    // Stands in for a live camera: the same photo fed frame after frame with sensor noise, then
    // panned from side to side. What the bracelets do on screen is compared with what the image
    // did, which is the part of the live path a single still cannot show: jitter and lag.
    public class TrackingStabilityTests
    {
        const string PhotoPath = "Assets/BangleMe/Tests/Editor/Fixtures/fist.jpg";
        const int FrameRate = 60;
        const int StillFrames = 90;
        const int PanFrames = 180;
        const int PanPeriodFrames = 90;
        const int PanPixels = 14;
        const int NoiseLevel = 6;

        [UnityTest]
        public IEnumerator NoisyAndMovingImage_KeepsTheBraceletsSteadyAndOnTheWrist()
        {
            var previousFrameRate = Application.targetFrameRate;
            var previousVSync = QualitySettings.vSyncCount;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = FrameRate;
            yield return SceneManager.LoadSceneAsync("Main");

            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(PhotoPath));
            var clean = source.GetPixels32();
            var width = source.width;
            var height = source.height;
            var frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[clean.Length];
            var random = new System.Random(7);

            void Show(int shift)
            {
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var from = clean[y * width + Mathf.Clamp(x - shift, 0, width - 1)];
                        var noise = random.Next(-NoiseLevel, NoiseLevel + 1);
                        pixels[y * width + x] = new Color32(Noisy(from.r, noise), Noisy(from.g, noise), Noisy(from.b, noise), 255);
                    }
                }
                frame.SetPixels32(pixels);
                frame.Apply();
            }

            Show(0);
            Object.FindFirstObjectByType<CameraFeed>().UseStillImage(frame);
            var pipeline = Object.FindFirstObjectByType<WristTrackingPipeline>();
            var stack = Object.FindFirstObjectByType<BraceletStack>();
            var rig = Object.FindFirstObjectByType<ArCameraRig>();

            try
            {
                for (var i = 0; i < 600 && !pipeline.IsTracking; i++) yield return null;
                Assert.IsTrue(pipeline.IsTracking, "the hand in the photo was never tracked");
                for (var i = 0; i < 60; i++)
                {
                    Show(0);
                    yield return null;
                }

                // Where the stack is drawn, in image pixels from the image's center.
                float ScreenX() => rig.FocalLengthPx * stack.transform.localPosition.x / stack.transform.localPosition.z;

                var still = new List<float>();
                var sizes = new List<float>();
                for (var i = 0; i < StillFrames; i++)
                {
                    Show(0);
                    yield return null;
                    still.Add(ScreenX());
                    // The stack's first child is the arm stand-in, whose scale never changes.
                    sizes.Add(stack.transform.Find("Bracelet 1").localScale.x);
                }
                var rest = Mean(still);
                var jitter = Deviation(still);
                var sizeJitter = Deviation(sizes) / Mean(sizes);

                var shown = new List<float>();
                var drawn = new List<float>();
                for (var i = 0; i < PanFrames; i++)
                {
                    var shift = Mathf.RoundToInt(PanPixels * Mathf.Sin(i * 2f * Mathf.PI / PanPeriodFrames));
                    Show(shift);
                    yield return null;
                    shown.Add(shift);
                    drawn.Add(ScreenX() - rest);
                }

                // The delay at which what was drawn best matches what was shown.
                var bestLag = 0;
                var bestError = float.MaxValue;
                for (var lag = 0; lag <= 20; lag++)
                {
                    float sum = 0;
                    var count = 0;
                    for (var i = PanPeriodFrames; i < PanFrames; i++)
                    {
                        var difference = drawn[i] - shown[i - lag];
                        sum += difference * difference;
                        count++;
                    }
                    var error = Mathf.Sqrt(sum / count);
                    if (error >= bestError) continue;
                    bestError = error;
                    bestLag = lag;
                }
                float lateSum = 0;
                for (var i = PanPeriodFrames; i < PanFrames; i++) lateSum += Mathf.Abs(drawn[i] - shown[i]);
                var offWrist = lateSum / (PanFrames - PanPeriodFrames);

                // A hand that moves steadily and then stops: carrying its position forward along
                // its velocity must not throw the bracelets past where the wrist came to rest.
                for (var i = 0; i < 45; i++)
                {
                    Show(0);
                    yield return null;
                }
                for (var shift = 1; shift <= PanPixels; shift++)
                {
                    Show(shift);
                    yield return null;
                }
                var overshoot = 0f;
                var settledAfter = -1;
                for (var i = 0; i < 60; i++)
                {
                    Show(PanPixels);
                    yield return null;
                    var past = ScreenX() - rest - PanPixels;
                    overshoot = Mathf.Max(overshoot, past);
                    if (Mathf.Abs(past) > 0.5f) settledAfter = i;
                }
                Debug.Log($"[BangleMe] stopping after {FrameRate} px/s: overshoot {overshoot:0.00}px, settled {(settledAfter + 1) * 1000f / FrameRate:0}ms after the stop");
                Assert.Less(overshoot, MaxOvershootPixels, "the bracelets fly past a wrist that stops");

                Debug.Log($"[BangleMe] stability on a {width}px wide image: jitter {jitter:0.00}px, size jitter {sizeJitter * 100f:0.00}%, " +
                          $"lag {bestLag} frames ({bestLag * 1000f / FrameRate:0}ms), follows within {bestError:0.00}px once the lag is taken out, " +
                          $"off the wrist by {offWrist:0.00}px on average while moving");

                Assert.Less(jitter, MaxJitterPixels, "the bracelets shake on a still wrist");
                Assert.Less(sizeJitter, MaxSizeJitter, "the bracelets pulse in size on a still wrist");
                Assert.Less(bestError, MaxFollowErrorPixels, "the bracelets do not follow the wrist's movement");
                Assert.LessOrEqual(bestLag, MaxLagFrames, "the bracelets trail the wrist");
            }
            finally
            {
                Application.targetFrameRate = previousFrameRate;
                QualitySettings.vSyncCount = previousVSync;
            }
        }

        // About three times what the editor measures on a desktop (0.2px, 0%, 0.5px, 2 frames), so
        // that a slower machine passes and a real regression does not. The spec's budget is 50ms
        // from frame to screen: three frames at 60fps.
        const float MaxJitterPixels = 0.6f;
        const float MaxSizeJitter = 0.01f;
        const float MaxFollowErrorPixels = 1.5f;
        const int MaxLagFrames = 3;
        // Measured 1.3px on a wrist about 90px wide.
        const float MaxOvershootPixels = 3f;

        static byte Noisy(byte value, int noise)
        {
            return (byte)Mathf.Clamp(value + noise, 0, 255);
        }

        static float Mean(List<float> values)
        {
            float sum = 0;
            foreach (var value in values) sum += value;
            return sum / values.Count;
        }

        static float Deviation(List<float> values)
        {
            var mean = Mean(values);
            float sum = 0;
            foreach (var value in values) sum += (value - mean) * (value - mean);
            return Mathf.Sqrt(sum / values.Count);
        }
    }
}
