using System.Linq;
using NUnit.Framework;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public class MG1ReadabilityPresenterTests
    {
        [Test]
        public void EverySeedPresentsEachItemExactlyOnce()
        {
            foreach (var seed in new[] { 0, 1, 17, 2026, int.MaxValue })
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 },
                    MG1ReadabilityPresenter.ShuffledOrder(seed));
        }

        [Test]
        public void SameSeedReproducesOrder_AndSeedsCanVaryOrder()
        {
            var first = MG1ReadabilityPresenter.ShuffledOrder(17);
            CollectionAssert.AreEqual(first, MG1ReadabilityPresenter.ShuffledOrder(17));
            Assert.That(Enumerable.Range(0, 20)
                    .Select(seed => string.Join(",", MG1ReadabilityPresenter.ShuffledOrder(seed)))
                    .Distinct().Count(), Is.GreaterThan(1));
        }
    }
}
