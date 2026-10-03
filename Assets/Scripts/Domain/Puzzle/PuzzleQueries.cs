using System;
using System.Collections.Generic;
using ZipTrip.Domain.Board;

namespace ZipTrip.Domain.Puzzle
{
    /// <summary>
    /// Whether an item can currently be taken by the player, and why not (ADR-0006 Decisions 12 and 17).
    /// "Accessible" is the suitcase layer notion; Source Tray and staging items are reachable because they are
    /// outside the suitcase, which is reported separately as <see cref="External"/>.
    /// </summary>
    public enum ItemAccess
    {
        Unknown = 0,
        /// <summary>In the suitcase with no blockers, or nested in a parent that can be taken.</summary>
        Accessible = 1,
        /// <summary>In the suitcase with at least one blocker.</summary>
        Blocked = 2,
        /// <summary>Nested in a parent that cannot be taken.</summary>
        ParentBlocked = 3,
        /// <summary>In the Source Tray or staging: not in the suitcase, freely reachable.</summary>
        External = 4,
        /// <summary>In an extraction destination (directly or via its parent): not reachable by normal moves.</summary>
        Terminal = 5
    }

    [Flags]
    public enum AdjacencyKind
    {
        None = 0,
        /// <summary>Some occupied cells are orthogonal XY neighbours at the same z (no diagonals).</summary>
        Lateral = 1,
        /// <summary>Footprints overlap in XY and one item's base layer equals the other's layer + thickness.</summary>
        Vertical = 2
    }

    /// <summary>Read-only access queries derived from a PuzzleState. Results are in ordinal instance id order.</summary>
    public static class AccessQueries
    {
        /// <summary>
        /// blockers(i): suitcase items j != i in the same compartment whose XY footprint overlaps i's and whose base
        /// layer is >= i.layer + i.thickness. Empty for items not in the suitcase.
        /// </summary>
        public static IReadOnlyList<string> GetBlockers(PuzzleState state, string instanceId)
        {
            var result = new List<string>();
            if (state == null || !state.TryGetItem(instanceId, out var item) || item.Location.Kind != ItemLocationKind.Suitcase)
                return result.AsReadOnly();

            var columns = Columns(item);
            var top = item.Location.Placement.Layer + item.State.Thickness;
            foreach (var other in state.Items)
            {
                if (ReferenceEquals(other, item) || other.Location.Kind != ItemLocationKind.Suitcase
                    || other.Location.Placement.Compartment != item.Location.Placement.Compartment
                    || other.Location.Placement.Layer < top)
                    continue;
                if (columns.Overlaps(Columns(other)))
                    result.Add(other.InstanceId);
            }
            return result.AsReadOnly();
        }

        public static ItemAccess GetAccess(PuzzleState state, string instanceId)
        {
            if (state == null || !state.TryGetItem(instanceId, out var item))
                return ItemAccess.Unknown;
            switch (item.Location.Kind)
            {
                case ItemLocationKind.SourceTray:
                case ItemLocationKind.Staging:
                    return ItemAccess.External;
                case ItemLocationKind.Destination:
                    return ItemAccess.Terminal;
                case ItemLocationKind.Suitcase:
                    return GetBlockers(state, instanceId).Count == 0 ? ItemAccess.Accessible : ItemAccess.Blocked;
                case ItemLocationKind.Nested:
                    // PuzzleState guarantees an acyclic parent chain.
                    switch (GetAccess(state, item.Location.TargetId))
                    {
                        case ItemAccess.Accessible:
                        case ItemAccess.External:
                            return ItemAccess.Accessible;
                        case ItemAccess.Terminal:
                            return ItemAccess.Terminal;
                        default:
                            return ItemAccess.ParentBlocked;
                    }
                default:
                    return ItemAccess.Unknown;
            }
        }

        /// <summary>accessible(i) in the ADR sense: in the suitcase (or nested) and currently reachable.</summary>
        public static bool IsAccessible(PuzzleState state, string instanceId) =>
            GetAccess(state, instanceId) == ItemAccess.Accessible;

        /// <summary>True when the player may pick the item up for a move: accessible or outside the suitcase.</summary>
        public static bool CanTake(PuzzleState state, string instanceId)
        {
            var access = GetAccess(state, instanceId);
            return access == ItemAccess.Accessible || access == ItemAccess.External;
        }

        internal static HashSet<Cell> Columns(PuzzleItem item)
        {
            var columns = new HashSet<Cell>();
            foreach (var cell in PuzzleState.GetPhysicalCells(item))
                columns.Add(cell.Column);
            return columns;
        }
    }

    /// <summary>
    /// Read-only adjacency queries (ADR-0006 Decision 12) over suitcase items. Nesting is containment, not adjacency,
    /// and items in different compartments are never adjacent.
    /// </summary>
    public static class AdjacencyQueries
    {
        public static AdjacencyKind GetContact(PuzzleState state, string a, string b)
        {
            if (state == null || string.Equals(a, b, StringComparison.Ordinal)
                || !state.TryGetItem(a, out var first) || !state.TryGetItem(b, out var second))
                return AdjacencyKind.None;
            return GetContact(first, second);
        }

        public static bool AreAdjacent(PuzzleState state, string a, string b) => GetContact(state, a, b) != AdjacencyKind.None;

        /// <summary>Every item touching <paramref name="instanceId"/>, ordinal instance id order.</summary>
        public static IReadOnlyList<string> GetNeighbours(PuzzleState state, string instanceId)
        {
            var result = new List<string>();
            if (state == null || !state.TryGetItem(instanceId, out var item))
                return result.AsReadOnly();
            foreach (var other in state.Items)
                if (!ReferenceEquals(other, item) && GetContact(item, other) != AdjacencyKind.None)
                    result.Add(other.InstanceId);
            return result.AsReadOnly();
        }

        private static AdjacencyKind GetContact(PuzzleItem a, PuzzleItem b)
        {
            if (a.Location.Kind != ItemLocationKind.Suitcase || b.Location.Kind != ItemLocationKind.Suitcase
                || a.Location.Placement.Compartment != b.Location.Placement.Compartment)
                return AdjacencyKind.None;

            var kind = AdjacencyKind.None;
            var aTop = a.Location.Placement.Layer + a.State.Thickness;
            var bTop = b.Location.Placement.Layer + b.State.Thickness;
            if ((b.Location.Placement.Layer == aTop || a.Location.Placement.Layer == bTop)
                && AccessQueries.Columns(a).Overlaps(AccessQueries.Columns(b)))
                kind |= AdjacencyKind.Vertical;

            var bCells = new HashSet<PhysicalCell>(PuzzleState.GetPhysicalCells(b));
            foreach (var cell in PuzzleState.GetPhysicalCells(a))
            {
                if (bCells.Contains(new PhysicalCell(cell.Compartment, cell.X + 1, cell.Y, cell.Z))
                    || bCells.Contains(new PhysicalCell(cell.Compartment, cell.X - 1, cell.Y, cell.Z))
                    || bCells.Contains(new PhysicalCell(cell.Compartment, cell.X, cell.Y + 1, cell.Z))
                    || bCells.Contains(new PhysicalCell(cell.Compartment, cell.X, cell.Y - 1, cell.Z)))
                {
                    kind |= AdjacencyKind.Lateral;
                    break;
                }
            }
            return kind;
        }
    }
}
