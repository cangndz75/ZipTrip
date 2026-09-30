using System;
using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public sealed class GameState
    {
        public ContainerDefinition Container { get; }
        public IReadOnlyList<PlacedItem> Placements { get; }
        /// <summary>Row-major, read-only projection of Placements; not independent gameplay state.</summary>
        public IReadOnlyList<Cell> Occupancy { get; }
        public IReadOnlyList<string> Tray { get; }
        public IReadOnlyList<string> Targets { get; }

        public GameState(ContainerDefinition container, IEnumerable<PlacedItem> placements,
            IEnumerable<string> tray, IEnumerable<string> targets)
        {
            Container = container ?? throw new ArgumentNullException(nameof(container));
            if (placements == null)
                throw new ArgumentNullException(nameof(placements));
            if (tray == null)
                throw new ArgumentNullException(nameof(tray));
            if (targets == null)
                throw new ArgumentNullException(nameof(targets));

            var placed = new List<PlacedItem>();
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var cells = new List<Cell>();
            var occupied = new HashSet<Cell>();
            foreach (var placement in placements)
            {
                if (placement == null || !itemIds.Add(placement.ItemId))
                    throw new ArgumentException("Placements require unique item ids.", nameof(placements));
                placed.Add(placement);
                foreach (var cell in placement.OccupiedCells)
                {
                    if (!container.Mask.IsValid(cell) || !occupied.Add(cell))
                        throw new ArgumentException("Placements must occupy distinct valid container cells.", nameof(placements));
                    cells.Add(cell);
                }
            }
            placed.Sort((a, b) => StringComparer.Ordinal.Compare(a.ItemId, b.ItemId));
            cells.Sort((a, b) =>
            {
                var row = a.Y.CompareTo(b.Y);
                return row != 0 ? row : a.X.CompareTo(b.X);
            });

            var traySlots = CopyIds(tray, nameof(tray));
            var targetIds = CopyIds(targets, nameof(targets));
            targetIds.Sort(StringComparer.Ordinal);

            Placements = placed.AsReadOnly();
            Occupancy = cells.AsReadOnly();
            Tray = traySlots.AsReadOnly();
            Targets = targetIds.AsReadOnly();
        }

        private static List<string> CopyIds(IEnumerable<string> ids, string parameterName)
        {
            var copy = new List<string>();
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                    throw new ArgumentException("Item ids must be non-empty.", parameterName);
                copy.Add(id);
            }
            return copy;
        }
    }
}
