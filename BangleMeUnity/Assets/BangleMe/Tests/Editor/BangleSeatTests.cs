using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class BangleSeatTests
    {
        [Test]
        public void SnugBangle_SitsCentered()
        {
            var seat = BangleSeat.At(0.03f, 0.03f, 0.03f, Vector3.left);

            Assert.AreEqual(0f, seat.Hang.magnitude, 1e-6f);
            Assert.AreEqual(1f, seat.TiltScale, 1e-6f);
        }

        // Gravity toward -X: the bangle drops until its +X side rests on the arm.
        [Test]
        public void LooseBangle_HangsFromTheArmsUpperSide()
        {
            var seat = BangleSeat.At(0.03f, 0.024f, 0.02f, Vector3.left);

            Assert.AreEqual(-0.006f, seat.Hang.x, 1e-6f);
            Assert.AreEqual(0f, seat.Hang.y, 1e-6f);
            Assert.Greater(seat.TiltScale, 1f);
        }

        [Test]
        public void UprightArm_DoesNotPullTheBangleSideways()
        {
            var seat = BangleSeat.At(0.03f, 0.024f, 0.02f, Vector3.down);

            Assert.AreEqual(0f, seat.Hang.magnitude, 1e-6f);
        }

        [Test]
        public void BangleTighterThanTheArm_DoesNotHangTheOtherWay()
        {
            Assert.AreEqual(0f, BangleSeat.At(0.03f, 0.035f, 0.03f, Vector3.left).Hang.x, 1e-6f);
        }
    }
}
