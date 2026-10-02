using NUnit.Framework;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public class ReadabilityPortraitFramingTests
    {
        [TestCase(9f / 16f)]
        [TestCase(9f / 19.5f)]
        [TestCase(9f / 20f)]
        [TestCase(1080f / 2340f)]
        public void SuitcaseExteriorFitsPortraitWidth(float aspect)
        {
            var size = ReadabilityPortraitFraming.SizeForAspect(aspect);
            var visibleWidth = 2f * size * aspect;
            Assert.That(visibleWidth,
                Is.GreaterThanOrEqualTo(ReadabilityPortraitFraming.ExteriorWidth +
                    2f * ReadabilityPortraitFraming.SideMargin - 0.0001f));
        }

        [Test]
        public void NineSixteenKeepsApprovedStaticFraming()
        {
            Assert.That(ReadabilityPortraitFraming.SizeForAspect(9f / 16f),
                Is.EqualTo(6.74f).Within(0.0001f));
        }

        [Test]
        public void InvalidAspectIsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                ReadabilityPortraitFraming.SizeForAspect(0f));
        }
    }
}
