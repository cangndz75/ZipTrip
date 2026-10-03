using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;

namespace ZipTrip.Tests.EditMode
{
    public sealed class ContainerMaskTests
    {
        [Test]
        public void Cell_UsesIntegerCoordinatesForEqualityAndHash()
        {
            var cell = new Cell(3, 4);
            var same = new Cell(3, 4);
            var different = new Cell(4, 3);

            Assert.That(cell, Is.EqualTo(same));
            Assert.That(cell == same, Is.True);
            Assert.That(cell != different, Is.True);
            Assert.That(cell.GetHashCode(), Is.EqualTo(same.GetHashCode()));
            Assert.That(cell.Equals(different), Is.False);
        }

        [Test]
        public void OutOfBoundsCell_ReturnsFalse()
        {
            var mask = new ContainerMask(8, 10, new[]
            {
                new Cell(0, 0)
            });

            Assert.That(mask.IsValid(new Cell(-1, 0)), Is.False);
            Assert.That(mask.IsValid(new Cell(8, 0)), Is.False);
            Assert.That(mask.IsValid(new Cell(0, 10)), Is.False);
        }

        [Test]
        public void InBoundsCellOutsideMask_ReturnsFalse()
        {
            var mask = new ContainerMask(6, 8, new[] { new Cell(1, 1) });

            Assert.That(mask.IsWithinBounds(new Cell(5, 7)), Is.True);
            Assert.That(mask.IsValid(new Cell(5, 7)), Is.False);
            Assert.That(mask.IsValid(new Cell(1, 1)), Is.True);
        }

        [Test]
        public void ValidCells_AreReturnedInRowMajorOrder()
        {
            var mask = new ContainerMask(8, 10, new[]
            {
                new Cell(2, 1),
                new Cell(0, 0),
                new Cell(1, 0)
            });

            var cells = mask.GetValidCells().ToArray();

            Assert.That(cells, Is.EqualTo(new[]
            {
                new Cell(0, 0),
                new Cell(1, 0),
                new Cell(2, 1)
            }));
        }

        [Test]
        public void StableBytes_AreRepeatable()
        {
            var cells = new[]
            {
                new Cell(0, 0),
                new Cell(3, 2),
                new Cell(7, 9)
            };

            var mask = new ContainerMask(8, 10, cells);
            var first = mask.ToStableBytes();
            var second = new ContainerMask(8, 10, cells).ToStableBytes();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EqualTo(new byte[] { 1, 0, 8, 0, 0, 0, 0, 0, 0, 128 }));

            var reordered = new ContainerMask(8, 10, cells.Reverse()).ToStableBytes();
            Assert.That(reordered, Is.EqualTo(first));

            first[0] = 0;
            Assert.That(mask.ToStableBytes()[0], Is.EqualTo(1));
        }

        [Test]
        public void FullGridIteration_IncludesEveryCellInRowMajorOrder()
        {
            var cells = (from y in Enumerable.Range(0, 10)
                         from x in Enumerable.Range(0, 8)
                         select new Cell(x, y)).ToArray();
            var mask = new ContainerMask(8, 10, cells.Reverse());

            Assert.That(mask.ValidCellCount, Is.EqualTo(80));
            Assert.That(mask.GetValidCells().ToArray(), Is.EqualTo(cells));
            Assert.That(mask.GetValidCells().ToArray(), Is.EqualTo(cells));
        }

        [Test]
        public void SizedMask_UsesItsOwnBoundsAndRowMajorOrder()
        {
            var mask = new ContainerMask(3, 2, new[] { new Cell(2, 1), new Cell(0, 0), new Cell(1, 1) });

            Assert.That(mask.ValidCellCount, Is.EqualTo(3));
            Assert.That(mask.GetValidCells().ToArray(),
                Is.EqualTo(new[] { new Cell(0, 0), new Cell(1, 1), new Cell(2, 1) }));
            Assert.That(mask.IsValid(new Cell(2, 1)), Is.True);
            Assert.That(mask.IsValid(new Cell(1, 0)), Is.False);
            Assert.That(mask.IsValid(new Cell(3, 0)), Is.False);
            Assert.That(mask.IsValid(new Cell(0, 2)), Is.False);
            Assert.That(mask.IsValid(new Cell(-1, 0)), Is.False);
            Assert.That(mask.ToStableBytes(), Is.EqualTo(new byte[] { 0b0011_0001 }));
            Assert.That(() => new ContainerMask(3, 2, new[] { new Cell(3, 0) }),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => new ContainerMask(0, 2, new Cell[0]),
                Throws.InstanceOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void SizedMask_SupportsLargerDimensions()
        {
            var mask = new ContainerMask(12, 11, new[] { new Cell(11, 10) });
            Assert.That(mask.IsValid(new Cell(11, 10)), Is.True);
            Assert.That(mask.GetValidCells().Single(), Is.EqualTo(new Cell(11, 10)));
            Assert.That(mask.ToStableBytes().Length, Is.EqualTo((12 * 11 + 7) / 8));
        }
    }
}
