using NUnit.Framework;

namespace BangleMe.Tests
{
    public class BraceletDesignTests
    {
        // A matte finish has no polish for beads to catch, so the beaded bangle is shown plain.
        [Test]
        public void PlainOf_TurnsOnlyTheBeadedBangleIntoThePlainOne()
        {
            var beaded = System.Array.FindIndex(BraceletDesign.Catalog, design => design.Name == "Boncuklu");
            var thin = System.Array.FindIndex(BraceletDesign.Catalog, design => design.Name == "İnce");
            for (var design = 0; design < BraceletDesign.Catalog.Length; design++)
            {
                var expected = design == beaded ? thin : design;
                Assert.AreEqual(expected, BraceletDesign.PlainOf(design), BraceletDesign.Catalog[design].Name);
            }
        }

        [Test]
        public void OnlyTheMatteFinish_IsPlain()
        {
            foreach (var finish in BraceletFinish.Catalog)
            {
                Assert.AreEqual(finish.Name == "Mat", finish.Plain, finish.Name);
            }
        }

        [TestCase("Tıraşlı", 0f)]
        [TestCase("İnce", 0.68f)]
        [TestCase("Yassı", 0.92f)]
        public void Freedom_GrowsAsTheBangleGetsThinner(string name, float expected)
        {
            var design = System.Array.Find(BraceletDesign.Catalog, candidate => candidate.Name == name);

            Assert.AreEqual(expected, design.Freedom, 0.001f);
        }
    }
}
