using NUnit.Framework;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Unity;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PuzzleBoardProjectionTests
    {
        private static readonly CompartmentFrame[] Frames =
        {
            new CompartmentFrame("main", Vector3.zero, 5, 4),
            new CompartmentFrame("pocket", new Vector3(6f, 0f, 0f), 2, 2)
        };

        [Test]
        public void PointInACompartment_GivesItsIdAndLocalAnchor()
        {
            Assert.That(PuzzleBoardProjection.TryProject(Frames, new Vector3(2f, 0f, -1f), Rect(2, 1), out var id, out var anchor), Is.True);
            Assert.That(id, Is.EqualTo("main"));
            Assert.That(anchor, Is.EqualTo(new Cell(2, 1)));
        }

        [Test]
        public void SameLocalAnchorInAnOffsetCompartment_IsResolvedRelativeToThatCompartment()
        {
            Assert.That(PuzzleBoardProjection.TryProject(Frames, new Vector3(7f, 0f, -1f), Rect(1, 1), out var id, out var anchor), Is.True);
            Assert.That(id, Is.EqualTo("pocket"));
            Assert.That(anchor, Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void FootprintCentreOutsideEveryCompartment_HasNoCandidate()
        {
            Assert.That(PuzzleBoardProjection.TryProject(Frames, new Vector3(20f, 0f, -20f), Rect(1, 1), out var id, out _), Is.False);
            Assert.That(id, Is.Null);
            Assert.That(PuzzleBoardProjection.TryProject(Frames, new Vector3(5.1f, 0f, 0f), Rect(1, 1), out _, out _), Is.False, "the gap between compartments");
        }

        [Test]
        public void RotatedFootprint_UsesItsOwnCentre_AndAnchorIsNeverClamped()
        {
            // A vertical 1x3 rod whose anchor hangs one cell left of main still projects into main with a raw anchor.
            var vertical = Rect(3, 1).Rotate(Rotation.Degrees90);
            Assert.That(PuzzleBoardProjection.TryProject(Frames, new Vector3(-0.4f, 0f, 0f), vertical, out var id, out var anchor), Is.True);
            Assert.That(id, Is.EqualTo("main"));
            Assert.That(anchor, Is.EqualTo(new Cell(0, 0)));

            Assert.That(PuzzleBoardProjection.TryProject(Frames, new Vector3(4.2f, 0f, -1f), Rect(1, 1), out _, out anchor), Is.True);
            Assert.That(PuzzleBoardProjection.TryProject(Frames, new Vector3(-0.8f, 0f, -1f), Rect(2, 1), out _, out anchor), Is.True);
            Assert.That(anchor, Is.EqualTo(new Cell(-1, 1)), "raw projector anchor; the Domain reports OutOfBounds");
        }

        [Test]
        public void AuthoredOrigins_AreHonoured()
        {
            var authored = new[] { new CompartmentFrame("pocket", new Vector3(-3f, 0f, 2f), 2, 2) };
            Assert.That(PuzzleBoardProjection.TryProject(authored, new Vector3(-2f, 0f, 1f), Rect(1, 1), out var id, out var anchor), Is.True);
            Assert.That(id, Is.EqualTo("pocket"));
            Assert.That(anchor, Is.EqualTo(new Cell(1, 1)));
        }
    }
}
