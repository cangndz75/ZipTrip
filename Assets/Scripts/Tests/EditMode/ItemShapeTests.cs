using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;

namespace ZipTrip.Tests.EditMode
{
    public sealed class ItemShapeTests
    {
        private static ItemShape Sneaker => new ItemShape(new[]
        {
            new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2)
        });

        [Test]
        public void Shape_NormalizesOffsetsAndSortsOccupiedCells()
        {
            var shape = new ItemShape(new[]
            {
                new Cell(5, -1), new Cell(4, -2), new Cell(4, -1)
            });

            AssertCells(shape, new Cell(0, 0), new Cell(0, 1), new Cell(1, 1));
            Assert.That(shape.CellCount, Is.EqualTo(3));
        }

        [Test]
        public void Shape_RejectsEmptyAndDuplicateCells()
        {
            Assert.Throws<ArgumentException>(() => new ItemShape(Array.Empty<Cell>()));
            Assert.Throws<ArgumentException>(() => new ItemShape(new[]
            {
                new Cell(2, 3), new Cell(2, 3)
            }));
        }

        [Test]
        public void Shape_RepresentationIsIndependentOfInputOrderAndReadOnly()
        {
            var cells = Sneaker.OccupiedCells.ToArray();
            var reversed = new ItemShape(cells.Reverse());

            Assert.That(reversed.OccupiedCells, Is.EqualTo(cells));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<Cell>)reversed.OccupiedCells)[0] = new Cell(7, 7));
        }

        [Test]
        public void Sneaker_FourRotationsMatchCanonicalCells()
        {
            var sneaker = Sneaker;

            AssertCells(sneaker.Rotate(Rotation.Degrees0),
                new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2));
            AssertCells(sneaker.Rotate(Rotation.Degrees90),
                new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(0, 1));
            AssertCells(sneaker.Rotate(Rotation.Degrees180),
                new Cell(0, 0), new Cell(1, 0), new Cell(1, 1), new Cell(1, 2));
            AssertCells(sneaker.Rotate(Rotation.Degrees270),
                new Cell(2, 0), new Cell(0, 1), new Cell(1, 1), new Cell(2, 1));
        }

        [Test]
        public void FourQuarterTurns_ReturnOriginalCanonicalShape()
        {
            var shape = Sneaker;
            var original = shape.OccupiedCells.ToArray();

            for (var turn = 0; turn < 4; turn++)
                shape = shape.Rotate(Rotation.Degrees90);

            Assert.That(shape.OccupiedCells, Is.EqualTo(original));
        }

        [Test]
        public void SymmetricShape_RotatesToSameCanonicalShape()
        {
            var square = new ItemShape(new[]
            {
                new Cell(0, 0), new Cell(1, 0), new Cell(0, 1), new Cell(1, 1)
            });

            Assert.That(square.Rotate(Rotation.Degrees90).OccupiedCells, Is.EqualTo(square.OccupiedCells));
            Assert.That(square.Rotate(Rotation.Degrees180).OccupiedCells, Is.EqualTo(square.OccupiedCells));
            Assert.That(square.Rotate(Rotation.Degrees270).OccupiedCells, Is.EqualTo(square.OccupiedCells));
        }

        [Test]
        public void RepeatedRotation_ProducesIdenticalCanonicalCells()
        {
            var first = Sneaker.Rotate(Rotation.Degrees270).OccupiedCells;
            var second = Sneaker.Rotate(Rotation.Degrees270).OccupiedCells;

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void UnsupportedRotation_ThrowsForDirectShapeOperation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Sneaker.Rotate((Rotation)45));
        }

        [Test]
        public void DisallowedItemRotation_ReturnsRotationNotAllowedWithoutChangingShape()
        {
            var sneaker = CreateItem("sneaker", Sneaker, null, new[] { Rotation.Degrees0 });
            var original = sneaker.ShapeStates["open"].OccupiedCells.ToArray();

            var rejected = sneaker.GetRotatedShape("open", Rotation.Degrees90);
            var accepted = sneaker.GetRotatedShape("open", Rotation.Degrees0);

            Assert.That(rejected.IsAccepted, Is.False);
            Assert.That(rejected.RejectionReason, Is.EqualTo(ItemRotationRejectionReason.RotationNotAllowed));
            Assert.That(rejected.Shape, Is.Null);
            Assert.That(accepted.IsAccepted, Is.True);
            Assert.That(accepted.RejectionReason, Is.EqualTo(ItemRotationRejectionReason.None));
            Assert.That(accepted.Shape.OccupiedCells, Is.EqualTo(original));
            Assert.That(sneaker.ShapeStates["open"].OccupiedCells, Is.EqualTo(original));
        }

        [Test]
        public void Sweater_FoldedStateIsSeparatelyAuthoredAndRotatesIndependently()
        {
            var open = Rectangle(3, 3);
            var folded = Rectangle(2, 4);
            var sweater = CreateItem("sweater", open, folded,
                new[] { Rotation.Degrees0, Rotation.Degrees90 });

            Assert.That(sweater.ShapeStates["open"], Is.SameAs(open));
            Assert.That(sweater.ShapeStates["folded"], Is.SameAs(folded));
            Assert.That(sweater.ShapeStates["open"].CellCount, Is.EqualTo(9));
            Assert.That(sweater.ShapeStates["folded"].CellCount, Is.EqualTo(8));
            AssertCells(sweater.GetRotatedShape("folded", Rotation.Degrees90).Shape,
                new Cell(0, 0), new Cell(1, 0), new Cell(2, 0), new Cell(3, 0),
                new Cell(0, 1), new Cell(1, 1), new Cell(2, 1), new Cell(3, 1));
            Assert.That(sweater.ShapeStates["folded"].OccupiedCells, Is.EqualTo(folded.OccupiedCells));
        }

        [Test]
        public void FoldArea_EqualAndOneSmallerAreAccepted()
        {
            var baseShape = Rectangle(3, 3);
            Assert.DoesNotThrow(() => CreateItem("equal", baseShape, Rectangle(1, 9),
                new[] { Rotation.Degrees0 }));
            Assert.DoesNotThrow(() => CreateItem("one-smaller", baseShape, Rectangle(2, 4),
                new[] { Rotation.Degrees0 }));
        }

        [Test]
        public void FoldArea_TwoSmallerIsRejected()
        {
            Assert.Throws<ArgumentException>(() => CreateItem("too-small", Rectangle(3, 3),
                Rectangle(1, 7), new[] { Rotation.Degrees0 }));
        }

        [Test]
        public void FoldArea_LargerThanBaseIsRejected()
        {
            Assert.Throws<ArgumentException>(() => CreateItem("too-large", Rectangle(3, 3),
                Rectangle(2, 5), new[] { Rotation.Degrees0 }));
        }

        [Test]
        public void Definition_StoresDeterministicStateRotationTagOrderAndOptionalVacuumShape()
        {
            var vacuum = Rectangle(1, 3);
            var item = new ItemDefinition("sweater", "open", new[]
            {
                new KeyValuePair<string, ItemShape>("folded", Rectangle(2, 4)),
                new KeyValuePair<string, ItemShape>("open", Rectangle(3, 3))
            }, new[] { Rotation.Degrees90, Rotation.Degrees0 },
                new[] { "soft", "clothing" }, vacuum);

            Assert.That(item.Id, Is.EqualTo("sweater"));
            Assert.That(item.BaseStateId, Is.EqualTo("open"));
            Assert.That(item.ShapeStates.Keys, Is.EqualTo(new[] { "folded", "open" }));
            Assert.That(item.AllowedRotations, Is.EqualTo(new[] { Rotation.Degrees0, Rotation.Degrees90 }));
            Assert.That(item.Tags, Is.EqualTo(new[] { "clothing", "soft" }));
            Assert.That(item.VacuumShape, Is.SameAs(vacuum));
        }

        private static ItemDefinition CreateItem(string id, ItemShape open, ItemShape folded,
            IEnumerable<Rotation> allowedRotations)
        {
            var states = new List<KeyValuePair<string, ItemShape>>
            {
                new KeyValuePair<string, ItemShape>("open", open)
            };
            if (folded != null)
                states.Add(new KeyValuePair<string, ItemShape>("folded", folded));

            return new ItemDefinition(id, "open", states, allowedRotations, Array.Empty<string>());
        }

        private static ItemShape Rectangle(int width, int height)
        {
            return new ItemShape(from y in Enumerable.Range(0, height)
                                 from x in Enumerable.Range(0, width)
                                 select new Cell(x, y));
        }

        private static void AssertCells(ItemShape shape, params Cell[] expected)
        {
            Assert.That(shape.OccupiedCells, Is.EqualTo(expected));
        }
    }
}
