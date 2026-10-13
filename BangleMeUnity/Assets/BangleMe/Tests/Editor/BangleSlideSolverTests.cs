using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class BangleSlideSolverTests
    {
        const int Count = 12;
        const float Width = 0.0023f;
        const float Gap = 0.0002f;
        const float Pitch = Width + Gap;
        const float FirstHome = 0.026f;
        const float Gravity = 9.81f;
        const float Frame = 1f / 60f;

        static float[] Widths()
        {
            var widths = new float[Count];
            for (var i = 0; i < Count; i++) widths[i] = Width;
            return widths;
        }

        static float[] Homes()
        {
            var homes = new float[Count];
            for (var i = 0; i < Count; i++) homes[i] = FirstHome + i * Pitch;
            return homes;
        }

        // Every bangle may come 15mm toward the hand and go `room` toward the elbow.
        static (float[] lowest, float[] highest) Limits(float room)
        {
            var homes = Homes();
            var lowest = new float[Count];
            var highest = new float[Count];
            for (var i = 0; i < Count; i++)
            {
                lowest[i] = homes[i] - 0.015f;
                highest[i] = homes[i] + room;
            }
            return (lowest, highest);
        }

        static BangleSlideSolver Solver()
        {
            var solver = new BangleSlideSolver(Gap);
            solver.Resize(Homes());
            return solver;
        }

        static void Run(BangleSlideSolver solver, float seconds, float pull, float press, float room = 0.03f)
        {
            var (lowest, highest) = Limits(room);
            for (var time = 0f; time < seconds; time += Frame) solver.Step(Frame, Widths(), lowest, highest, pull, press);
        }

        [Test]
        public void LevelArm_HoldsTheBanglesWhereTheyAre()
        {
            var solver = Solver();

            Run(solver, 2f, 1f, Gravity);

            for (var i = 0; i < Count; i++) Assert.AreEqual(Homes()[i], solver.Along(i), 1e-6f);
        }

        [Test]
        public void HandRaised_SlidesThemTowardTheElbowUntilTheArmIsTooWide()
        {
            var solver = Solver();

            Run(solver, 3f, Gravity, 0f);

            Assert.AreEqual(Homes()[Count - 1] + 0.03f, solver.Along(Count - 1), 1e-4f);
            Assert.IsTrue(solver.IsAtRest);
        }

        [Test]
        public void HandLowered_PilesThemAgainstTheHand()
        {
            var solver = Solver();

            Run(solver, 3f, -Gravity, 0f);

            for (var i = 0; i < Count; i++) Assert.AreEqual(Homes()[0] - 0.015f + i * Pitch, solver.Along(i), 1e-4f);
            Assert.IsTrue(solver.IsAtRest);
        }

        [Test]
        public void BangleAsWideAsTheArm_DoesNotGoFurtherUpIt()
        {
            var solver = Solver();

            Run(solver, 2f, Gravity, 0f, 0f);

            for (var i = 0; i < Count; i++) Assert.AreEqual(Homes()[i], solver.Along(i), 1e-5f);
        }

        [Test]
        public void ShakenArm_NeverLetsTwoBanglesShareAPlace()
        {
            var solver = Solver();
            var (lowest, highest) = Limits(0.03f);

            for (var step = 0; step < 600; step++)
            {
                var pull = Mathf.Sin(step * 0.37f) * 30f;
                solver.Step(Frame, Widths(), lowest, highest, pull, 2f);
                for (var i = 1; i < Count; i++)
                {
                    Assert.GreaterOrEqual(solver.Along(i) - solver.Along(i - 1), Pitch - 1e-6f, $"step {step}, bangle {i}");
                }
            }
        }

        [Test]
        public void LongFrame_EndsWhereShortFramesDo()
        {
            var smooth = Solver();
            var stalled = Solver();
            var (lowest, highest) = Limits(0.03f);

            Run(smooth, 0.5f, -Gravity, 1f);
            stalled.Step(0.5f, Widths(), lowest, highest, -Gravity, 1f);

            for (var i = 0; i < Count; i++) Assert.AreEqual(smooth.Along(i), stalled.Along(i), 0.001f);
        }

        [Test]
        public void AddedBangle_StartsAtItsHomeAndLeavesTheOthersAlone()
        {
            var solver = Solver();
            Run(solver, 3f, -Gravity, 0f);
            var settled = solver.Along(3);

            var homes = new float[Count + 1];
            Homes().CopyTo(homes, 0);
            homes[Count] = FirstHome + Count * Pitch;
            solver.Resize(homes);

            Assert.AreEqual(settled, solver.Along(3), 1e-6f);
            Assert.AreEqual(homes[Count], solver.Along(Count), 1e-6f);
        }
    }
}
