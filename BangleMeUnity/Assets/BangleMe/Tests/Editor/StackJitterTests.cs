using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class StackJitterTests
    {
        [Test]
        public void SameIndex_GivesTheSamePose()
        {
            var first = StackJitter.At(7);
            var second = StackJitter.At(7);

            Assert.AreEqual(first.TiltDegrees, second.TiltDegrees);
            Assert.AreEqual(first.ShiftMeters, second.ShiftMeters);
            Assert.AreEqual(first.RadiusScale, second.RadiusScale);
        }

        [Test]
        public void EveryIndex_StaysWithinTheLimits()
        {
            for (var index = 0; index < 50; index++)
            {
                var jitter = StackJitter.At(index);

                var largestTilt = StackJitter.MaxTiltDegrees * StackJitter.LargestTiltScale;
                Assert.LessOrEqual(Mathf.Abs(jitter.TiltDegrees.x), largestTilt);
                Assert.LessOrEqual(Mathf.Abs(jitter.TiltDegrees.y), largestTilt);
                Assert.LessOrEqual(Mathf.Abs(jitter.ShiftMeters.x), StackJitter.MaxShiftMeters);
                Assert.LessOrEqual(Mathf.Abs(jitter.ShiftMeters.y), StackJitter.MaxShiftMeters);
                Assert.GreaterOrEqual(jitter.RadiusScale, 1f);
                Assert.LessOrEqual(jitter.RadiusScale, 1.12f);
                Assert.That(jitter.Tone, Is.InRange(1f - StackJitter.MaxToneLoss, 1f));
                Assert.That(jitter.Polish, Is.InRange(1f - StackJitter.MaxPolishLoss, 1f));
            }
        }

        // A settled bangle leaning far from its neighbour would open a gap the arm shows through.
        [Test]
        public void SettledNeighbours_LeanNearlyTogether()
        {
            for (var index = StackJitter.WristEndCount; index < 49; index++)
            {
                if (StackJitter.IsCrosser(index) || StackJitter.IsCrosser(index + 1)) continue;
                var difference = StackJitter.At(index).TiltDegrees - StackJitter.At(index + 1).TiltDegrees;
                Assert.Less(difference.magnitude, StackJitter.MaxTiltDegrees * 0.8f);
            }
        }

        [Test]
        public void WristEnd_SitsLooserThanTheRest()
        {
            Assert.Greater(StackJitter.At(0).RadiusScale, 1.06f);
            Assert.Less(StackJitter.At(20).RadiusScale, 1.04f);
        }

        [Test]
        public void AFewBangles_CrossTheirNeighbours()
        {
            var crossers = 0;
            for (var index = 0; index < 50; index++)
            {
                if (StackJitter.IsCrosser(index)) crossers++;
            }

            Assert.That(crossers, Is.InRange(1, 10));
        }

        [Test]
        public void FreerBangles_CrossMoreOften()
        {
            int settled = 0, free = 0;
            for (var index = 0; index < 200; index++)
            {
                if (StackJitter.IsCrosser(index)) settled++;
                if (StackJitter.IsCrosser(index, 1f)) free++;
            }

            Assert.Greater(free, settled);
        }

        [Test]
        public void FreerBangles_LeanFurther()
        {
            for (var index = 0; index < 50; index++)
            {
                if (StackJitter.IsCrosser(index) || StackJitter.IsCrosser(index, 1f)) continue;
                var settled = StackJitter.At(index).TiltDegrees;
                var free = StackJitter.At(index, 1f).TiltDegrees;

                Assert.AreEqual(settled.x * 1.6f, free.x, 1e-4f);
                Assert.AreEqual(settled.y * 1.6f, free.y, 1e-4f);
            }
        }

        [Test]
        public void Stack_DoesNotLeanAsOne()
        {
            var min = float.MaxValue;
            var max = float.MinValue;
            for (var index = 0; index < 50; index++)
            {
                var tilt = StackJitter.At(index).TiltDegrees.x;
                min = Mathf.Min(min, tilt);
                max = Mathf.Max(max, tilt);
            }

            Assert.Greater(max - min, StackJitter.MaxTiltDegrees, "the lean should wander along the stack");
        }

        [Test]
        public void Stack_LeansBothWays()
        {
            var sum = Vector2.zero;
            for (var index = 0; index < 50; index++) sum += StackJitter.At(index).TiltDegrees;

            Assert.Less((sum / 50f).magnitude, StackJitter.MaxTiltDegrees * 0.4f, "the stack as a whole should not lean");
        }
    }
}
