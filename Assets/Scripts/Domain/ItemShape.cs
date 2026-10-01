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
            WriteRotatedCells(rotation, rotated);
            return new ItemShape(rotated);
        }

        // Writes the same normalized, row-major shape without allocating a new ItemShape.
        public void WriteRotatedCells(Rotation rotation, Cell[] destination)
        {
            if (destination == null || destination.Length < _occupiedCells.Count)
                throw new ArgumentException("Destination must fit the shape.", nameof(destination));

            var minX = int.MaxValue;
            var minY = int.MaxValue;

            for (var i = 0; i < _occupiedCells.Count; i++)
            {
                var cell = _occupiedCells[i];
                switch (rotation)
                {
                    case Rotation.Degrees0:
                        destination[i] = cell;
                        break;
                    case Rotation.Degrees90:
                        destination[i] = new Cell(-cell.Y, cell.X);
                        break;
                    case Rotation.Degrees180:
                        destination[i] = new Cell(-cell.X, -cell.Y);
                        break;
                    case Rotation.Degrees270:
                        destination[i] = new Cell(cell.Y, -cell.X);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(rotation));
                }
                minX = Math.Min(minX, destination[i].X);
                minY = Math.Min(minY, destination[i].Y);
            }

            for (var i = 0; i < _occupiedCells.Count; i++)
                destination[i] = new Cell(destination[i].X - minX, destination[i].Y - minY);

            // Shapes are small; insertion sort avoids a comparer or delegate in the hot path.
            for (var i = 1; i < _occupiedCells.Count; i++)
            {
                var value = destination[i];
                var j = i - 1;
                while (j >= 0 && (destination[j].Y > value.Y ||
                    (destination[j].Y == value.Y && destination[j].X > value.X)))
                {
                    destination[j + 1] = destination[j];
                    j--;
                }
                destination[j + 1] = value;
            }
        }
    }
}
