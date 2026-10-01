using NUnit.Framework;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public class FixedGameplayCameraTests
    {
        [TestCase(false, 9f / 16f)]
        [TestCase(false, 9f / 19.5f)]
        [TestCase(false, 9f / 20f)]
        [TestCase(true, 9f / 16f)]
        [TestCase(true, 9f / 19.5f)]
        [TestCase(true, 9f / 20f)]
        public void ContainerOuterCorners_AreVisibleAtPortraitAspect(bool cabin, float aspect)
        {
            var container = cabin ? ContainerFixtures.CreateCabin(new Cell(0, 0)) :
                ContainerFixtures.CreateBackpack(new Cell(0, 0));
            var go = new GameObject("Camera test");
            try
            {
                var camera = go.AddComponent<Camera>();
                var framing = go.AddComponent<FixedGameplayCamera>();
                camera.aspect = aspect;
                framing.Configure(container, aspect);
                var bounds = FixedGameplayCamera.OuterBounds(container.Mask);
                Assert.IsTrue(camera.orthographic);
                Assert.That(go.transform.eulerAngles.x, Is.EqualTo(75f).Within(0.001f));
                Assert.That(camera.orthographicSize,
                    Is.EqualTo((bounds.width + 0.8f) / (2f * aspect)).Within(0.0001f));
                Assert.That(Vector3.Distance(go.transform.position,
                    new Vector3(bounds.center.x, 0f, -bounds.center.y)),
                    Is.EqualTo(12f).Within(0.0001f));
                foreach (var x in new[] { bounds.xMin, bounds.xMax })
                foreach (var y in new[] { bounds.yMin, bounds.yMax })
                {
                    var screen = camera.WorldToViewportPoint(new Vector3(x, 0f, -y));
                    Assert.That(screen.z, Is.GreaterThan(0f));
                    Assert.That(screen.x, Is.InRange(0f, 1f));
                    Assert.That(screen.y, Is.InRange(0f, 1f));
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PresentationBelowBoard_ExpandsBoardOnlyFraming()
        {
            var container = ContainerFixtures.CreateBackpack(new Cell(0, 0));
            var go = new GameObject("Camera test");
            try
            {
                var camera = go.AddComponent<Camera>();
                var framing = go.AddComponent<FixedGameplayCamera>();
                var aspect = 9f / 16f;
                camera.aspect = aspect;
                var presentation = new Bounds(new Vector3(2.5f, 0.1f, -4.5f),
                    new Vector3(5f, 0.4f, 9f));
                framing.Configure(container, aspect, presentation);

                Assert.That(camera.orthographicSize,
                    Is.GreaterThan(FixedGameplayCamera.OrthographicSize(5, aspect)));
                var lowerCorner = new Vector3(presentation.min.x, presentation.min.y,
                    presentation.min.z);
                var viewport = camera.WorldToViewportPoint(lowerCorner);
                Assert.That(viewport.y, Is.GreaterThan(0.02f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
