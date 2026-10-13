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
    }
}
