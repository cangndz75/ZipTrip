using System;
using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public sealed class PlacedItem
    {
        public string ItemId { get; }
        public Cell Anchor { get; }
        public Rotation Rotation { get; }
        public string ShapeState { get; }
        public IReadOnlyList<Cell> OccupiedCells { get; }

        public PlacedItem(ItemDefinition item, Cell anchor, Rotation rotation, string shapeState)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var rotated = item.GetRotatedShape(shapeState, rotation);
            if (!rotated.IsAccepted)
                throw new ArgumentException("Rotation is not allowed for this item.", nameof(rotation));

            var cells = new List<Cell>();
            foreach (var local in rotated.Shape.OccupiedCells)
            {
                cells.Add(new Cell(checked(anchor.X + local.X), checked(anchor.Y + local.Y)));
            }

            ItemId = item.Id;
            Anchor = anchor;
            Rotation = rotation;
            ShapeState = shapeState;
            OccupiedCells = cells.AsReadOnly();
        }
    }
}
