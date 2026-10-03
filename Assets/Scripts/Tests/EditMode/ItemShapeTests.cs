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

        private static void AssertCells(ItemShape shape, params Cell[] expected)
        {
            Assert.That(shape.OccupiedCells, Is.EqualTo(expected));
        }
    }
}
