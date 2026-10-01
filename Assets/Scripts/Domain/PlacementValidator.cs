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

    public readonly struct PlacementValidationSummary
    {
        public bool IsValid { get; }
        public PlacementFailureReason Reason { get; }
        public int OffendingCellCount { get; }

        internal PlacementValidationSummary(PlacementFailureReason reason, int count)
        {
            IsValid = reason == PlacementFailureReason.None;
            Reason = reason;
            OffendingCellCount = count;
        }
    }

    // Caller-owned storage. Allocate once for the largest shape used by a preview.
    public sealed class PlacementValidationScratch
    {
        internal readonly Cell[] Rotated;
        internal readonly Cell[] OutOfBounds;
        internal readonly Cell[] OutsideMask;
        internal readonly Cell[] Overlaps;
        public Cell[] RotatedCells => Rotated;
        public Cell[] OffendingCells { get; internal set; }

        public PlacementValidationScratch(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Rotated = new Cell[capacity];
            OutOfBounds = new Cell[capacity];
            OutsideMask = new Cell[capacity];
            Overlaps = new Cell[capacity];
            OffendingCells = OutOfBounds;
        }

        public int Capacity => Rotated.Length;
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

            if (!item.ShapeStates.TryGetValue(shapeState, out var shape))
                throw new ArgumentException("Unknown shape state.", nameof(shapeState));
            var scratch = new PlacementValidationScratch(shape.CellCount);
            var summary = ValidateNonAlloc(board, item, anchor, rotation, shapeState, scratch);
            if (summary.IsValid)
                return PlacementValidationResult.Valid();
            var cells = new List<Cell>(summary.OffendingCellCount);
            for (var i = 0; i < summary.OffendingCellCount; i++)
                cells.Add(scratch.OffendingCells[i]);
            return PlacementValidationResult.Invalid(summary.Reason, cells);
        }

        public static PlacementValidationSummary ValidateNonAlloc(
            PlacementBoard board, ItemDefinition item, Cell anchor, Rotation rotation,
            string shapeState, PlacementValidationScratch scratch)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            if (scratch == null)
                throw new ArgumentNullException(nameof(scratch));
            if (!item.ShapeStates.TryGetValue(shapeState, out var shape))
                throw new ArgumentException("Unknown shape state.", nameof(shapeState));
            if (scratch.Capacity < shape.CellCount)
                throw new ArgumentException("Scratch capacity is too small.", nameof(scratch));
            if (item.WriteRotatedCells(shapeState, rotation, scratch.Rotated) !=
                ItemRotationRejectionReason.None)
                throw new ArgumentException("Rotation is not allowed for this item.", nameof(rotation));

            var outCount = 0;
            var maskCount = 0;
            var overlapCount = 0;
            for (var i = 0; i < shape.CellCount; i++)
            {
                var local = scratch.Rotated[i];
                var x = (long)anchor.X + local.X;
                var y = (long)anchor.Y + local.Y;
                if (x < 0 || x >= GridSize.Width || y < 0 || y >= GridSize.Height)
                {
                    scratch.OutOfBounds[outCount++] = new Cell(ClampToInt(x), ClampToInt(y));
                    continue;
                }

                var cell = new Cell((int)x, (int)y);
                if (!board.Container.Mask.IsValid(cell))
                    scratch.OutsideMask[maskCount++] = cell;
                if (board.IsOccupied(cell))
                    scratch.Overlaps[overlapCount++] = cell;
            }

            // Keep the precedence explicit until a mixed-failure policy is authored.
            if (outCount != 0)
                return Summary(PlacementFailureReason.OutOfBounds, scratch.OutOfBounds, outCount, scratch);
            if (maskCount != 0)
                return Summary(PlacementFailureReason.OutsideMask, scratch.OutsideMask, maskCount, scratch);
            if (overlapCount != 0)
                return Summary(PlacementFailureReason.Overlap, scratch.Overlaps, overlapCount, scratch);

            scratch.OffendingCells = scratch.OutOfBounds;
            return new PlacementValidationSummary(PlacementFailureReason.None, 0);
        }

        private static PlacementValidationSummary Summary(PlacementFailureReason reason,
            Cell[] cells, int count, PlacementValidationScratch scratch)
        {
            // Rotated input is row-major. Stable in-place dedup retains the old result order.
            var uniqueCount = 0;
            for (var i = 0; i < count; i++)
            {
                var duplicate = false;
                for (var j = 0; j < uniqueCount; j++)
                    if (cells[j] == cells[i]) { duplicate = true; break; }
                if (!duplicate)
                    cells[uniqueCount++] = cells[i];
            }
            scratch.OffendingCells = cells;
            return new PlacementValidationSummary(reason, uniqueCount);
        }

        private static int ClampToInt(long value)
        {
            if (value < int.MinValue)
                return int.MinValue;
            if (value > int.MaxValue)
                return int.MaxValue;
            return (int)value;
        }

    }
}
