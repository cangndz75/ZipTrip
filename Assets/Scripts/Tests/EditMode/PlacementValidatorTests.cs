using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PlacementValidatorTests
    {
        private static ItemDefinition Sneaker()
        {
            return Item("sneaker", new ItemShape(new[]
            {
                new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2)
            }), null);
        }

        private static PlacementBoard Cabin(Cell origin, params Cell[] occupied)
        {
            return new PlacementBoard(ContainerFixtures.CreateCabin(origin), occupied);
        }

        [Test]
        public void SneakerOnBlockedCabinCorner_ReportsOnlyOutsideMaskCell()
        {
            var result = PlacementValidator.Validate(Cabin(new Cell(0, 0)), Sneaker(),
                new Cell(0, 0), Rotation.Degrees0, "open");

            AssertInvalid(result, PlacementFailureReason.OutsideMask, new Cell(0, 0));
        }

        [Test]
        public void PartialRightBoundaryOverflow_ReportsActualOutOfBoundsCell()
        {
            var result = PlacementValidator.Validate(Cabin(new Cell(2, 2)), Sneaker(),
                new Cell(7, 3), Rotation.Degrees0, "open");

            AssertInvalid(result, PlacementFailureReason.OutOfBounds, new Cell(8, 5));
        }

        [Test]
        public void NegativeAnchor_ReturnsOutOfBoundsWithoutThrowing()
        {
            var result = PlacementValidator.Validate(Cabin(new Cell(0, 0)), Sneaker(),
                new Cell(-1, 1), Rotation.Degrees0, "open");

            AssertInvalid(result, PlacementFailureReason.OutOfBounds,
                new Cell(-1, 1), new Cell(-1, 2), new Cell(-1, 3));
        }

        [Test]
        public void PartialBottomBoundaryOverflow_ReportsEveryOutOfBoundsCell()
        {
            var result = PlacementValidator.Validate(Cabin(new Cell(2, 2)), Sneaker(),
                new Cell(4, 8), Rotation.Degrees0, "open");

            AssertInvalid(result, PlacementFailureReason.OutOfBounds,
                new Cell(4, 10), new Cell(5, 10));
        }

        [Test]
        public void SingleCellOverlap_ReportsExactlyThatCell()
        {
            var result = PlacementValidator.Validate(Cabin(new Cell(0, 0), new Cell(1, 2)),
                Sneaker(), new Cell(1, 1), Rotation.Degrees0, "open");

            AssertInvalid(result, PlacementFailureReason.Overlap, new Cell(1, 2));
        }

        [Test]
        public void MultiCellOverlap_ReportsOnlyCollisionsInRowMajorOrder()
        {
            var board = Cabin(new Cell(0, 0), new Cell(2, 3), new Cell(4, 4), new Cell(1, 1));
            var result = PlacementValidator.Validate(board, Sneaker(),
                new Cell(1, 1), Rotation.Degrees0, "open");

            AssertInvalid(result, PlacementFailureReason.Overlap,
                new Cell(1, 1), new Cell(2, 3));
        }

        [Test]
        public void FullyFittingPlacement_ReturnsValidWithNoOffendingCells()
        {
            var result = PlacementValidator.Validate(Cabin(new Cell(0, 0), new Cell(4, 4)),
                Sneaker(), new Cell(1, 1), Rotation.Degrees0, "open");

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Reason, Is.EqualTo(PlacementFailureReason.None));
            Assert.That(result.OffendingCells, Is.Empty);
        }

        [Test]
        public void PlacementUsesRequestedRotatedSneakerFootprint()
        {
            var board = Cabin(new Cell(0, 0));
            var item = Sneaker();
            var anchor = new Cell(4, 1);

            var unrotated = PlacementValidator.Validate(board, item, anchor, Rotation.Degrees0, "open");
            var rotated = PlacementValidator.Validate(board, item, anchor, Rotation.Degrees90, "open");

            Assert.That(unrotated.IsValid, Is.True);
            AssertInvalid(rotated, PlacementFailureReason.OutsideMask, new Cell(6, 1));
        }

        [Test]
        public void PlacementUsesAuthoredSweaterShapeState()
        {
            var sweater = Item("sweater", Rectangle(3, 3), Rectangle(2, 4));
            var board = Cabin(new Cell(0, 0));
            var anchor = new Cell(4, 1);

            var folded = PlacementValidator.Validate(board, sweater, anchor, Rotation.Degrees0, "folded");
            var open = PlacementValidator.Validate(board, sweater, anchor, Rotation.Degrees0, "open");

            Assert.That(folded.IsValid, Is.True);
            AssertInvalid(open, PlacementFailureReason.OutsideMask,
                new Cell(6, 1), new Cell(6, 2), new Cell(6, 3));
        }

        [Test]
        public void ValidationDoesNotMutateOccupancyOrItemShape()
        {
            var occupiedInput = new List<Cell> { new Cell(3, 3), new Cell(1, 2) };
            var board = new PlacementBoard(ContainerFixtures.CreateCabin(new Cell(0, 0)), occupiedInput);
            var item = Sneaker();
            var beforeInput = occupiedInput.ToArray();
            var beforeBoard = board.OccupiedCells.ToArray();
            var beforeShape = item.ShapeStates["open"].OccupiedCells.ToArray();

            var result = PlacementValidator.Validate(board, item,
                new Cell(1, 1), Rotation.Degrees0, "open");

            AssertInvalid(result, PlacementFailureReason.Overlap, new Cell(1, 2));
            Assert.That(occupiedInput, Is.EqualTo(beforeInput));
            Assert.That(board.OccupiedCells, Is.EqualTo(beforeBoard));
            Assert.That(item.ShapeStates["open"].OccupiedCells, Is.EqualTo(beforeShape));
        }

        [Test]
        public void RepeatedValidation_ReturnsSameReasonAndOffendingCells()
        {
            var board = Cabin(new Cell(0, 0), new Cell(2, 3), new Cell(1, 1));
            var item = Sneaker();

            var first = PlacementValidator.Validate(board, item, new Cell(1, 1), Rotation.Degrees0, "open");
            var second = PlacementValidator.Validate(board, item, new Cell(1, 1), Rotation.Degrees0, "open");

            AssertInvalid(first, PlacementFailureReason.Overlap, new Cell(1, 1), new Cell(2, 3));
            Assert.That(second.IsValid, Is.EqualTo(first.IsValid));
            Assert.That(second.Reason, Is.EqualTo(first.Reason));
            Assert.That(second.OffendingCells, Is.EqualTo(first.OffendingCells));
        }

        private static ItemDefinition Item(string id, ItemShape open, ItemShape folded)
        {
            var states = new List<KeyValuePair<string, ItemShape>>
            {
                new KeyValuePair<string, ItemShape>("open", open)
            };
            if (folded != null)
                states.Add(new KeyValuePair<string, ItemShape>("folded", folded));

            return new ItemDefinition(id, "open", states, new[]
            {
                Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270
            }, Array.Empty<string>());
        }

        private static ItemShape Rectangle(int width, int height)
        {
            return new ItemShape(from y in Enumerable.Range(0, height)
                                 from x in Enumerable.Range(0, width)
                                 select new Cell(x, y));
        }

        private static void AssertInvalid(PlacementValidationResult result,
            PlacementFailureReason reason, params Cell[] offendingCells)
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Reason, Is.EqualTo(reason));
            Assert.That(result.OffendingCells, Is.EqualTo(offendingCells));
        }
    }
}
