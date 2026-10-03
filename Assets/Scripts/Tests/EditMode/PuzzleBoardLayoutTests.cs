using NUnit.Framework;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PuzzleBoardLayoutTests
    {
        [Test]
        public void CompartmentOrigins_AreSideBySideWithAGap_InBoardOrder()
        {
            var board = new BoardSpec(new[]
            {
                new Compartment("pocket", 2, 1, 1, RectCells(2, 1)),
                new Compartment("main", 5, 3, 2, RectCells(5, 3))
            });
            var origins = PuzzleBoardLayout.CompartmentOrigins(board);
            Assert.That(origins["main"], Is.EqualTo(Vector3.zero));
            Assert.That(origins["pocket"], Is.EqualTo(new Vector3(5f + PuzzleBoardLayout.CompartmentGap, 0f, 0f)));
        }

        [Test]
        public void ItemPosition_UsesAnchorAndLayerElevationOnly()
        {
            var lower = PuzzleBoardLayout.ItemLocalPosition(new Placement("main", new Cell(2, 1), 0, Rotation.Degrees90));
            var upper = PuzzleBoardLayout.ItemLocalPosition(new Placement("main", new Cell(2, 1), 1, Rotation.Degrees90));
            Assert.That(lower, Is.EqualTo(new Vector3(2f, 0f, -1f)));
            Assert.That(upper.x, Is.EqualTo(lower.x));
            Assert.That(upper.z, Is.EqualTo(lower.z), "stacking never changes logical x / y");
            Assert.That(upper.y, Is.EqualTo(PuzzleBoardLayout.LayerHeight));
        }

        [Test]
        public void FootprintCells_FollowTheGridProjectorConvention()
        {
            var center = PuzzleBoardLayout.FootprintCellCenter(new Cell(1, 2));
            Assert.That(center, Is.EqualTo(new Vector3(1.5f, 0f, -2.5f)));
            Assert.That(GridProjector.WorldToAnchor(center - new Vector3(0.5f, 0f, -0.5f)), Is.EqualTo(new Cell(1, 2)));
        }

        [Test]
        public void Thickness_ScalesHeightAndStaysBelowTheNextLayer()
        {
            Assert.That(PuzzleBoardLayout.ItemHeight(1), Is.LessThan(PuzzleBoardLayout.LayerHeight));
            Assert.That(PuzzleBoardLayout.ItemHeight(2), Is.EqualTo(2f * PuzzleBoardLayout.ItemHeight(1)).Within(1e-5f));
            Assert.That(PuzzleBoardLayout.ItemHeight(2), Is.GreaterThan(PuzzleBoardLayout.LayerHeight), "a thick item visibly spans into layer 1");
        }

        [Test]
        public void RotatedFootprint_IsTheDomainItemShapeRotation()
        {
            var state = new Domain.Items.ItemStateSpec("s", new ItemShape(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2) }), 1, AllRotations);
            state.TryGetFootprint(Rotation.Degrees90, out var rotated);
            Assert.That(rotated.OccupiedCells, Is.EqualTo(state.Footprint.Rotate(Rotation.Degrees90).OccupiedCells),
                "presentation reads the Domain footprint; it has no rotation math of its own");
        }
    }
}
