using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public static class ContainerFixtures
    {
        public static ContainerDefinition CreateCabin(Cell origin)
        {
            return new ContainerDefinition(
                "cabin_std",
                CreateMask(origin, 6, 8),
                ZipperEdge.Top);
        }

        public static ContainerDefinition CreateBackpack(Cell origin)
        {
            return new ContainerDefinition(
                "backpack_std",
                CreateMask(origin, 5, 7),
                ZipperEdge.Top);
        }

        private static ContainerMask CreateMask(Cell origin, int width, int height)
        {
            var cells = new List<Cell>();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var isCorner =
                        (x == 0 && y == 0) ||
                        (x == width - 1 && y == 0) ||
                        (x == 0 && y == height - 1) ||
                        (x == width - 1 && y == height - 1);

                    if (!isCorner)
                    {
                        cells.Add(new Cell(
                            origin.X + x,
                            origin.Y + y));
                    }
                }
            }

            return new ContainerMask(cells);
        }
    }
}
