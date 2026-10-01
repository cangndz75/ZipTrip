using NUnit.Framework;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public class GridProjectorTests
    {
        private GameObject _cameraObject;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("Grid projector camera");
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 8f;
            _camera.pixelRect = new Rect(0f, 0f, 800f, 800f);
            _cameraObject.transform.position = new Vector3(4f, 12f, -5f);
            _cameraObject.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_cameraObject);

        [TestCase(0f, 0f, 0, 0)]
        [TestCase(1f, 2f, 1, 2)]
        [TestCase(7f, 9f, 7, 9)]
        public void ScreenProjection_AtKnownBoardAnchor_ReturnsIntegerCell(
            float x, float row, int expectedX, int expectedRow)
        {
            var screen = _camera.WorldToScreenPoint(new Vector3(x, 0f, -row));
            Assert.That(GridProjector.ScreenToAnchor(_camera, screen),
                Is.EqualTo(new Cell(expectedX, expectedRow)));
        }

        [TestCase(0.5f, 0.5f, 0, 0)]
        [TestCase(1.5f, 2.5f, 1, 2)]
        [TestCase(7.5f, 9.5f, 7, 9)]
        public void CellCenter_ProjectsToItsSmallerIndexAnchor(
            float x, float row, int expectedX, int expectedRow)
        {
            Assert.That(GridProjector.WorldToAnchor(new Vector3(x, 0f, -row)),
                Is.EqualTo(new Cell(expectedX, expectedRow)));
        }

        [Test]
        public void ExactHorizontalMidpoint_ChoosesSmallerColumn() =>
            Assert.That(GridProjector.WorldToAnchor(new Vector3(1.5f, 0f, -2f)),
                Is.EqualTo(new Cell(1, 2)));

        [Test]
        public void ExactVerticalMidpoint_ChoosesSmallerRow() =>
            Assert.That(GridProjector.WorldToAnchor(new Vector3(1f, 0f, -2.5f)),
                Is.EqualTo(new Cell(1, 2)));

        [Test]
        public void ExactFourWayMidpoint_ChoosesUpperLeft() =>
            Assert.That(GridProjector.WorldToAnchor(new Vector3(1.5f, 0f, -2.5f)),
                Is.EqualTo(new Cell(1, 2)));

        [TestCase(1.4f, 2.4f, 1, 2)]
        [TestCase(1.6f, 2.6f, 2, 3)]
        public void AroundMidpoint_ChoosesNearestAnchor(
            float x, float row, int expectedX, int expectedRow) =>
            Assert.That(GridProjector.WorldToAnchor(new Vector3(x, 0f, -row)),
                Is.EqualTo(new Cell(expectedX, expectedRow)));

        [Test]
        public void OutsideLeftAndTop_ReturnsNegativeRawAnchor() =>
            Assert.That(GridProjector.WorldToAnchor(new Vector3(-0.6f, 0f, 0.6f)),
                Is.EqualTo(new Cell(-1, -1)));

        [Test]
        public void OutsideRightAndBottom_ReturnsRawAnchorBeyondCanonicalBoard() =>
            Assert.That(GridProjector.WorldToAnchor(new Vector3(7.6f, 0f, -9.6f)),
                Is.EqualTo(new Cell(8, 10)));

        [Test]
        public void ScreenProjection_DoesNotClampOutsideBoard()
        {
            var screen = _camera.WorldToScreenPoint(new Vector3(-1f, 0f, 1f));
            Assert.That(GridProjector.ScreenToAnchor(_camera, screen),
                Is.EqualTo(new Cell(-1, -1)));
        }

        [Test]
        public void RepeatedIdenticalInput_ReturnsIdenticalAnchor()
        {
            var screen = _camera.WorldToScreenPoint(new Vector3(3f, 0f, -4f));
            var first = GridProjector.ScreenToAnchor(_camera, screen);
            for (var i = 0; i < 20; i++)
                Assert.That(GridProjector.ScreenToAnchor(_camera, screen), Is.EqualTo(first));
            Assert.That(first, Is.EqualTo(new Cell(3, 4)));
        }
    }
}
