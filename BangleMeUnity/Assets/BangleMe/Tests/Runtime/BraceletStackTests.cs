using NUnit.Framework;
using UnityEngine;

namespace BangleMe.Tests
{
    public class BraceletStackTests
    {
        const int ThinDesign = 6;
        const int BeadedDesign = 7;

        BraceletStack stack;

        [SetUp]
        public void CreateEmptyStack()
        {
            stack = new GameObject("Stack").AddComponent<BraceletStack>();
            stack.Clear();
        }

        [TearDown]
        public void DestroyStack()
        {
            Object.Destroy(stack.gameObject);
        }

        [Test]
        public void AddSet_AddsThatManyBangles()
        {
            stack.Add(ThinDesign);
            stack.AddSet(12);

            Assert.AreEqual(13, stack.Count);
        }

        // The twelfth bangle after the first is a beaded one in the thin set's pattern.
        [Test]
        public void AddSet_AfterAThinBangle_MixesInBeadedOnes()
        {
            stack.Add(ThinDesign);
            stack.AddSet(12);

            Assert.AreEqual(BeadedDesign, stack.LastDesign);
        }

        [TestCase(0)]
        [TestCase(4)]
        public void AddSet_AfterAnyOtherDesign_RepeatsIt(int design)
        {
            stack.Add(design);
            stack.AddSet(5);

            for (var added = 0; added < 5; added++)
            {
                Assert.AreEqual(design, stack.LastDesign);
                stack.RemoveLast();
            }
        }

        [Test]
        public void AddSet_MoreThanFits_StopsAtMaxCount()
        {
            stack.Add(ThinDesign);
            stack.AddSet(1000);

            Assert.AreEqual(BraceletStack.MaxCount, stack.Count);
        }

        [Test]
        public void Add_MoreThanFits_StopsAtMaxCount()
        {
            stack.Add(ThinDesign);
            stack.Add(ThinDesign, BraceletStack.MaxCount);

            Assert.AreEqual(BraceletStack.MaxCount, stack.Count);
        }
    }
}
