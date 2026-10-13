using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class DeviceGravityTests
    {
        static void AssertDirection(Vector3 expected, Vector3 actual)
        {
            Assert.Less(Vector3.Angle(expected, actual), 0.1f, $"expected {expected}, was {actual}");
        }

        [Test]
        public void PhoneHeldUpright_DownIsDownTheImage()
        {
            AssertDirection(Vector3.down, DeviceGravity.InView(new Vector3(0, -1, 0), ScreenOrientation.Portrait, false));
        }

        [Test]
        public void BackCameraPointedAtTheFloor_DownIsIntoTheImage()
        {
            AssertDirection(Vector3.forward, DeviceGravity.InView(new Vector3(0, 0, -1), ScreenOrientation.Portrait, false));
        }

        [Test]
        public void FrontCameraPointedAtTheCeiling_DownIsOutOfTheImage()
        {
            AssertDirection(Vector3.back, DeviceGravity.InView(new Vector3(0, 0, -1), ScreenOrientation.Portrait, true));
        }

        // Turned so that the phone's right edge is up: gravity pulls along the phone's -X.
        [Test]
        public void PhoneOnItsSide_DownIsStillDownTheImage()
        {
            AssertDirection(Vector3.down, DeviceGravity.InView(new Vector3(-1, 0, 0), ScreenOrientation.LandscapeLeft, false));
            AssertDirection(Vector3.down, DeviceGravity.InView(new Vector3(1, 0, 0), ScreenOrientation.LandscapeRight, false));
        }

        // Held upside down the phone's top edge points at the floor: gravity pulls along its +Y.
        [Test]
        public void PhoneUpsideDown_DownIsStillDownTheImage()
        {
            AssertDirection(Vector3.down, DeviceGravity.InView(new Vector3(0, 1, 0), ScreenOrientation.PortraitUpsideDown, false));
        }

        [Test]
        public void NoReading_FallsBackToDownTheImage()
        {
            AssertDirection(Vector3.down, DeviceGravity.InView(Vector3.zero, ScreenOrientation.Portrait, false));
        }
    }
}
