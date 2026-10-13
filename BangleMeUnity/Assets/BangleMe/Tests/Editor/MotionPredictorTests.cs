using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class MotionPredictorTests
    {
        static MotionPredictor Moving(Vector3 velocity, int samples = 60)
        {
            var predictor = new MotionPredictor();
            for (var i = 0; i < samples; i++) predictor.Observe(velocity * (i / 60f), i / 60.0);
            return predictor;
        }

        [Test]
        public void StillHand_StaysWhereItIs()
        {
            var predictor = new MotionPredictor();
            for (var i = 0; i < 30; i++) predictor.Observe(new Vector3(0.1f, 0.2f, 0.4f), i / 60.0);

            Assert.AreEqual(new Vector3(0.1f, 0.2f, 0.4f), predictor.Predict(30 / 60.0 + 0.08));
        }

        [Test]
        public void SteadyMovement_IsCarriedForwardToTheMomentDrawn()
        {
            var predictor = Moving(new Vector3(0.3f, 0f, 0f));
            var last = 59 / 60.0;

            Assert.AreEqual(0.3f * (float)(last + 0.05), predictor.Predict(last + 0.05).x, 0.001f);
        }

        [Test]
        public void LongSilence_DoesNotCarryTheHandAway()
        {
            var predictor = Moving(new Vector3(0.3f, 0f, 0f));
            var last = 59 / 60.0;

            Assert.AreEqual(0.3f * ((float)last + MotionPredictor.MaxLeadSeconds), predictor.Predict(last + 5.0).x, 0.001f);
        }

        [Test]
        public void FirstSample_HasNoVelocity()
        {
            var predictor = new MotionPredictor();
            predictor.Observe(Vector3.one, 10.0);

            Assert.AreEqual(Vector3.one, predictor.Predict(10.1));
        }

        [Test]
        public void Reset_ForgetsTheMovement()
        {
            var predictor = Moving(new Vector3(0.3f, 0f, 0f));
            predictor.Reset();
            predictor.Observe(Vector3.zero, 2.0);

            Assert.AreEqual(Vector3.zero, predictor.Predict(2.1));
        }
    }
}
