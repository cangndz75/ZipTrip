using System;
using System.Collections.Generic;
using NUnit.Framework;
using ZipTrip.Domain;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PlacementPreviewParityTests
    {
        private static ItemDefinition Sneaker()
        {
            var shape = new ItemShape(new[]
            {
                new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2)
            });
            return new ItemDefinition("sneaker", "open",
                new[] { new KeyValuePair<string, ItemShape>("open", shape) },
                new[] { Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180,
                    Rotation.Degrees270 }, Array.Empty<string>());
        }

        [TestCase(1, 1, Rotation.Degrees0)]
        [TestCase(1, 4, Rotation.Degrees0)]
        [TestCase(-1, 1, Rotation.Degrees0)]
        [TestCase(0, 0, Rotation.Degrees0)]
        [TestCase(1, 1, Rotation.Degrees90)]
        [TestCase(4, 1, Rotation.Degrees90)]
        [TestCase(7, 8, Rotation.Degrees180)]
        [TestCase(1, 1, Rotation.Degrees270)]
        public void ExistingAndNonAllocValidators_HaveIdenticalResult(
            int x, int y, Rotation rotation)
        {
            var item = Sneaker();
            var board = new PlacementBoard(ContainerFixtures.CreateCabin(new Cell(0, 0)),
                new[] { new Cell(1, 1), new Cell(2, 3), new Cell(4, 4) });
            AssertParity(board, item, new Cell(x, y), rotation);
        }

        [Test]
        public void MultipleOverlapCells_AreReportedInSameRowMajorOrder()
        {
            var board = new PlacementBoard(ContainerFixtures.CreateCabin(new Cell(0, 0)),
                new[] { new Cell(2, 3), new Cell(1, 1) });
            AssertParity(board, Sneaker(), new Cell(1, 1), Rotation.Degrees0);
        }

        [Test]
        public void EveryRotation_WritesTheExistingCanonicalCells()
        {
            var item = Sneaker();
            var buffer = new Cell[item.ShapeStates["open"].CellCount];
            foreach (var rotation in new[] { Rotation.Degrees0, Rotation.Degrees90,
                Rotation.Degrees180, Rotation.Degrees270 })
            {
                Assert.AreEqual(ItemRotationRejectionReason.None,
                    item.WriteRotatedCells("open", rotation, buffer));
                var existing = item.GetRotatedShape("open", rotation).Shape.OccupiedCells;
                Assert.That(buffer, Is.EqualTo(existing));
            }
        }

        [Test]
        public void NonAllocValidation_AllocatesZeroBytesAfterWarmup()
        {
            var item = Sneaker();
            var board = new PlacementBoard(ContainerFixtures.CreateCabin(new Cell(0, 0)),
                Array.Empty<Cell>());
            var scratch = new PlacementValidationScratch(item.ShapeStates["open"].CellCount);
            for (var i = 0; i < 1000; i++)
                PlacementValidator.ValidateNonAlloc(board, item, new Cell(i % 2, 1),
                    Rotation.Degrees0, "open", scratch);

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 1000; i++)
                PlacementValidator.ValidateNonAlloc(board, item, new Cell(i % 2, 1),
                    Rotation.Degrees0, "open", scratch);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
        }

        private static void AssertParity(PlacementBoard board, ItemDefinition item,
            Cell anchor, Rotation rotation)
        {
            var existing = PlacementValidator.Validate(board, item, anchor, rotation, "open");
            var scratch = new PlacementValidationScratch(item.ShapeStates["open"].CellCount);
            var preview = PlacementValidator.ValidateNonAlloc(board, item, anchor, rotation,
                "open", scratch);
            Assert.AreEqual(existing.IsValid, preview.IsValid);
            Assert.AreEqual(existing.Reason, preview.Reason);
            Assert.AreEqual(existing.OffendingCells.Count, preview.OffendingCellCount);
            for (var i = 0; i < preview.OffendingCellCount; i++)
                Assert.AreEqual(existing.OffendingCells[i], scratch.OffendingCells[i],
                    "offending cell " + i);
        }
    }
}
