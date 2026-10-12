using NUnit.Framework;

namespace BangleMe.Tests
{
    public class OneEuroFilterTests
    {
        [Test]
        public void FirstSample_ReturnsItself()
        {
            var filter = new OneEuroFilter(1.0f, 0.007f);
            Assert.AreEqual(5.0f, filter.Filter(5.0f, 0), 0.0001f);
        }

        [Test]
        public void ConstantSignal_RemainsConstant()
        {
            var filter = new OneEuroFilter(1.0f, 0.007f);
            filter.Filter(10.0f, 0);
            Assert.AreEqual(10.0f, filter.Filter(10.0f, 0.016), 0.01f);
            Assert.AreEqual(10.0f, filter.Filter(10.0f, 0.032), 0.01f);
        }

        [Test]
        public void NoisySignal_IsSmoothedAroundMean()
        {
            var filter = new OneEuroFilter(1.0f, 0.007f);
            var t = 0.0;
            var last = 0f;
            for (var i = 0; i < 30; i++)
            {
                var noise = i % 2 == 0 ? 0.1f : -0.1f;
                last = filter.Filter(5.0f + noise, t);
                t += 1.0 / 60.0;
            }
            Assert.AreEqual(5.0f, last, 0.05f);
        }
    }
}
