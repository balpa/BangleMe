using NUnit.Framework;

namespace BangleMe.Tests
{
    public class TrackingStateTests
    {
        [Test]
        public void InitialState_IsLost()
        {
            var state = new TrackingState();
            Assert.AreEqual(0, state.Opacity);
            Assert.IsFalse(state.IsTracking);
        }

        [Test]
        public void DetectedHand_IncreasesOpacityOverFadeInDuration()
        {
            var state = new TrackingState();
            state.Update(true, 0);
            state.Update(true, 0.1);
            Assert.Greater(state.Opacity, 0);
            Assert.Less(state.Opacity, 1);

            state.Update(true, 0.3);
            Assert.AreEqual(1, state.Opacity, 0.01f);
        }

        [Test]
        public void LostHand_FadesOutAfterDelay()
        {
            var state = new TrackingState();
            state.Update(true, 0);
            state.Update(true, 0.3);
            Assert.AreEqual(1, state.Opacity, 0.01f);

            state.Update(false, 0.4);
            Assert.Greater(state.Opacity, 0.5f);

            state.Update(false, 0.71);
            Assert.AreEqual(0, state.Opacity, 0.05f);
        }
    }
}
