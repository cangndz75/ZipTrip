using System;
using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public sealed class PlacementBoard
    {
        private readonly HashSet<Cell> _occupied;

        public ContainerDefinition Container { get; }
        public IReadOnlyList<Cell> OccupiedCells { get; }

        public PlacementBoard(ContainerDefinition container, IEnumerable<Cell> occupiedCells)
        {
            Container = container ?? throw new ArgumentNullException(nameof(container));
            if (occupiedCells == null)
                throw new ArgumentNullException(nameof(occupiedCells));

            _occupied = new HashSet<Cell>(occupiedCells);
            var ordered = new List<Cell>(_occupied);
            ordered.Sort((a, b) =>
            {
                var row = a.Y.CompareTo(b.Y);
                return row != 0 ? row : a.X.CompareTo(b.X);
            });
            OccupiedCells = ordered.AsReadOnly();
        }

        internal bool IsOccupied(Cell cell)
        {
            return _occupied.Contains(cell);
        }
    }
}
