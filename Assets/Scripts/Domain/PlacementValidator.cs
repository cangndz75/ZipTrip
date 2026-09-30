using System;
using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public enum PlacementFailureReason
    {
        None,
        OutOfBounds,
        OutsideMask,
        Overlap
    }

    public sealed class PlacementValidationResult
    {
        public bool IsValid { get; }
        public PlacementFailureReason Reason { get; }
        public IReadOnlyList<Cell> OffendingCells { get; }

        private PlacementValidationResult(bool isValid, PlacementFailureReason reason, IReadOnlyList<Cell> offendingCells)
        {
            IsValid = isValid;
            Reason = reason;
            OffendingCells = offendingCells;
        }

        internal static PlacementValidationResult Valid()
        {
            return new PlacementValidationResult(true, PlacementFailureReason.None, Array.Empty<Cell>());
        }

        internal static PlacementValidationResult Invalid(PlacementFailureReason reason, List<Cell> offendingCells)
        {
            return new PlacementValidationResult(false, reason, offendingCells.AsReadOnly());
        }
    }

    public static class PlacementValidator
    {
        public static PlacementValidationResult Validate(
            PlacementBoard board,
            ItemDefinition item,
            Cell anchor,
            Rotation rotation,
            string shapeState)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var rotated = item.GetRotatedShape(shapeState, rotation);
            if (!rotated.IsAccepted)
                throw new ArgumentException("Rotation is not allowed for this item.", nameof(rotation));

            var outOfBounds = new List<Cell>();
            var outsideMask = new List<Cell>();
            var overlaps = new List<Cell>();

            foreach (var local in rotated.Shape.OccupiedCells)
            {
                var x = (long)anchor.X + local.X;
                var y = (long)anchor.Y + local.Y;
                if (x < 0 || x >= GridSize.Width || y < 0 || y >= GridSize.Height)
                {
                    outOfBounds.Add(new Cell(ClampToInt(x), ClampToInt(y)));
                    continue;
                }

                var cell = new Cell((int)x, (int)y);
                if (!board.Container.Mask.IsValid(cell))
                    outsideMask.Add(cell);
                if (board.IsOccupied(cell))
                    overlaps.Add(cell);
            }

            // Keep the precedence explicit until a mixed-failure policy is authored.
            if (outOfBounds.Count != 0)
                return PlacementValidationResult.Invalid(PlacementFailureReason.OutOfBounds, OrderDistinct(outOfBounds));
            if (outsideMask.Count != 0)
                return PlacementValidationResult.Invalid(PlacementFailureReason.OutsideMask, OrderDistinct(outsideMask));
            if (overlaps.Count != 0)
                return PlacementValidationResult.Invalid(PlacementFailureReason.Overlap, OrderDistinct(overlaps));

            return PlacementValidationResult.Valid();
        }

        private static int ClampToInt(long value)
        {
            if (value < int.MinValue)
                return int.MinValue;
            if (value > int.MaxValue)
                return int.MaxValue;
            return (int)value;
        }

        private static List<Cell> OrderDistinct(List<Cell> cells)
        {
            var ordered = new List<Cell>(new HashSet<Cell>(cells));
            ordered.Sort((a, b) =>
            {
                var row = a.Y.CompareTo(b.Y);
                return row != 0 ? row : a.X.CompareTo(b.X);
            });
            return ordered;
        }
    }
}
