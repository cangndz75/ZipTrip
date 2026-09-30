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
        public void CanonicalGrid_HasExpectedSizeAndCorners()
        {
            Assert.That(GridSize.Width, Is.EqualTo(8));
            Assert.That(GridSize.Height, Is.EqualTo(10));
            Assert.That(GridSize.CellCount, Is.EqualTo(80));

            var corners = new[]
            {
                new Cell(0, 0), new Cell(7, 0),
                new Cell(0, 9), new Cell(7, 9)
            };
            Assert.That(corners.All(GridSize.IsWithinBounds), Is.True);
            Assert.That(corners.Select(GridSize.ToRowMajorIndex).ToArray(),
                Is.EqualTo(new[] { 0, 7, 72, 79 }));
        }

        [Test]
        public void OutOfBoundsCell_ReturnsFalse()
        {
            var mask = new ContainerMask(new[]
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
            var cabin = ContainerFixtures.CreateCabin(new Cell(0, 0));
            var backpack = ContainerFixtures.CreateBackpack(new Cell(0, 0));

            Assert.That(GridSize.IsWithinBounds(new Cell(6, 4)), Is.True);
            Assert.That(cabin.Mask.IsValid(new Cell(6, 4)), Is.False);
            Assert.That(GridSize.IsWithinBounds(new Cell(5, 3)), Is.True);
            Assert.That(backpack.Mask.IsValid(new Cell(5, 3)), Is.False);
        }

        [Test]
        public void ValidCells_AreReturnedInRowMajorOrder()
        {
            var mask = new ContainerMask(new[]
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

            var mask = new ContainerMask(cells);
            var first = mask.ToStableBytes();
            var second = new ContainerMask(cells).ToStableBytes();

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first, Is.EqualTo(new byte[] { 1, 0, 8, 0, 0, 0, 0, 0, 0, 128 }));

            var reordered = new ContainerMask(cells.Reverse()).ToStableBytes();
            Assert.That(reordered, Is.EqualTo(first));

            first[0] = 0;
            Assert.That(mask.ToStableBytes()[0], Is.EqualTo(1));
        }

        [Test]
        public void FullGridIteration_IncludesEveryCellInRowMajorOrder()
        {
            var cells = (from y in Enumerable.Range(0, GridSize.Height)
                         from x in Enumerable.Range(0, GridSize.Width)
                         select new Cell(x, y)).ToArray();
            var mask = new ContainerMask(cells.Reverse());

            Assert.That(mask.ValidCellCount, Is.EqualTo(80));
            Assert.That(mask.GetValidCells().ToArray(), Is.EqualTo(cells));
            Assert.That(mask.GetValidCells().ToArray(), Is.EqualTo(cells));
        }

        [Test]
        public void FixtureIteration_IsCompleteAndRowMajor()
        {
            var fixtures = new[]
            {
                (Definition: ContainerFixtures.CreateCabin(new Cell(0, 0)), Width: 6, Height: 8),
                (Definition: ContainerFixtures.CreateBackpack(new Cell(0, 0)), Width: 5, Height: 7)
            };

            foreach (var fixture in fixtures)
            {
                var expected = (from y in Enumerable.Range(0, fixture.Height)
                                from x in Enumerable.Range(0, fixture.Width)
                                where !((x == 0 || x == fixture.Width - 1)
                                    && (y == 0 || y == fixture.Height - 1))
                                select new Cell(x, y)).ToArray();

                Assert.That(fixture.Definition.Mask.ValidCellCount, Is.EqualTo(expected.Length));
                Assert.That(fixture.Definition.Mask.GetValidCells().ToArray(), Is.EqualTo(expected));
                Assert.That(fixture.Definition.Mask.GetValidCells().ToArray(), Is.EqualTo(expected));
            }
        }

        [Test]
        public void CabinFixture_HasExpectedShape()
        {
            var cabin = ContainerFixtures.CreateCabin(new Cell(0, 0));

            Assert.That(cabin.Id, Is.EqualTo("cabin_std"));
            Assert.That(cabin.ZipperEdge, Is.EqualTo(ZipperEdge.Top));
            Assert.That(cabin.Mask.ValidCellCount, Is.EqualTo(44));

            Assert.That(cabin.Mask.IsValid(new Cell(0, 0)), Is.False);
            Assert.That(cabin.Mask.IsValid(new Cell(5, 0)), Is.False);
            Assert.That(cabin.Mask.IsValid(new Cell(0, 7)), Is.False);
            Assert.That(cabin.Mask.IsValid(new Cell(5, 7)), Is.False);

            Assert.That(cabin.Mask.IsValid(new Cell(1, 1)), Is.True);
        }

        [Test]
        public void BackpackFixture_HasExpectedShape()
        {
            var backpack = ContainerFixtures.CreateBackpack(new Cell(0, 0));

            Assert.That(backpack.Id, Is.EqualTo("backpack_std"));
            Assert.That(backpack.ZipperEdge, Is.EqualTo(ZipperEdge.Top));
            Assert.That(backpack.Mask.ValidCellCount, Is.EqualTo(31));

            Assert.That(backpack.Mask.IsValid(new Cell(0, 0)), Is.False);
            Assert.That(backpack.Mask.IsValid(new Cell(4, 0)), Is.False);
            Assert.That(backpack.Mask.IsValid(new Cell(0, 6)), Is.False);
            Assert.That(backpack.Mask.IsValid(new Cell(4, 6)), Is.False);

            Assert.That(backpack.Mask.IsValid(new Cell(1, 1)), Is.True);
        }
    }
}
