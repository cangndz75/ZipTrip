using System;
using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public sealed class ItemShape
    {
        private readonly IReadOnlyList<Cell> _occupiedCells;

        public IReadOnlyList<Cell> OccupiedCells => _occupiedCells;
        public int CellCount => _occupiedCells.Count;

        public ItemShape(IEnumerable<Cell> occupiedCells)
        {
            if (occupiedCells == null)
                throw new ArgumentNullException(nameof(occupiedCells));

            var cells = new List<Cell>();
            var unique = new HashSet<Cell>();
            var minX = int.MaxValue;
            var minY = int.MaxValue;

            foreach (var cell in occupiedCells)
            {
                if (!unique.Add(cell))
                    throw new ArgumentException("Shape contains a duplicate cell.", nameof(occupiedCells));

                cells.Add(cell);
                minX = Math.Min(minX, cell.X);
                minY = Math.Min(minY, cell.Y);
            }

            if (cells.Count == 0)
                throw new ArgumentException("Shape must contain at least one cell.", nameof(occupiedCells));

            for (var i = 0; i < cells.Count; i++)
                cells[i] = new Cell(checked(cells[i].X - minX), checked(cells[i].Y - minY));

            cells.Sort((a, b) =>
            {
                var row = a.Y.CompareTo(b.Y);
                return row != 0 ? row : a.X.CompareTo(b.X);
            });
            _occupiedCells = cells.AsReadOnly();
        }

        public ItemShape Rotate(Rotation rotation)
        {
            var rotated = new Cell[_occupiedCells.Count];

            for (var i = 0; i < rotated.Length; i++)
            {
                var cell = _occupiedCells[i];
                switch (rotation)
                {
                    case Rotation.Degrees0:
                        rotated[i] = cell;
                        break;
                    case Rotation.Degrees90:
                        rotated[i] = new Cell(-cell.Y, cell.X);
                        break;
                    case Rotation.Degrees180:
                        rotated[i] = new Cell(-cell.X, -cell.Y);
                        break;
                    case Rotation.Degrees270:
                        rotated[i] = new Cell(cell.Y, -cell.X);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(rotation));
                }
            }

            return new ItemShape(rotated);
        }
    }
}
