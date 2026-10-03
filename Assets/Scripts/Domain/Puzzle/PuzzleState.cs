using System;
using System.Collections.Generic;
using System.IO;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;

namespace ZipTrip.Domain.Puzzle
{
    /// <summary>
    /// Immutable puzzle state (ADR-0006): every item instance with its current state and single location.
    /// Physical occupancy is always derived from placements, never stored. Construction rejects only structural
    /// impossibilities (identity, references, nesting cycles); board legality is the invariant evaluator's job.
    /// Equality and hash cover the per-state data only; the PuzzleSpec is constant for a level.
    /// </summary>
    public sealed class PuzzleState : IEquatable<PuzzleState>
    {
        private const int FormatVersion = 1;
        private readonly Dictionary<string, PuzzleItem> _byId;
        private readonly byte[] _stableBytes;

        public PuzzleSpec Spec { get; }
        /// <summary>Items in ordinal instance id order.</summary>
        public IReadOnlyList<PuzzleItem> Items { get; }
        public ulong Hash { get; }

        public PuzzleState(PuzzleSpec spec, IEnumerable<PuzzleItem> items)
        {
            if (spec == null)
                throw new ArgumentException("MissingSpec", nameof(spec));
            if (items == null)
                throw new ArgumentException("MissingItems", nameof(items));

            _byId = new Dictionary<string, PuzzleItem>(StringComparer.Ordinal);
            var ordered = new List<PuzzleItem>();
            foreach (var item in items)
            {
                if (item == null)
                    throw new ArgumentException("MissingItems: null item", nameof(items));
                // One record per instance id: an instance can never be in two containers at once.
                if (_byId.ContainsKey(item.InstanceId))
                    throw new ArgumentException("DuplicateInstanceId: " + item.InstanceId, nameof(items));
                _byId.Add(item.InstanceId, item);
                ordered.Add(item);
            }
            ordered.Sort((a, b) => string.CompareOrdinal(a.InstanceId, b.InstanceId));

            // Canonical bytes identify definitions by id, so one id must mean one definition.
            var definitions = new Dictionary<string, ItemSpec>(StringComparer.Ordinal);
            foreach (var item in ordered)
            {
                if (definitions.TryGetValue(item.Definition.Id, out var known) && !known.Equals(item.Definition))
                    throw new ArgumentException("ConflictingDefinition: " + item.Definition.Id, nameof(items));
                definitions[item.Definition.Id] = item.Definition;
                ValidateReferences(spec, item);
            }

            Spec = spec;
            Items = ordered.AsReadOnly();
            _stableBytes = BuildStableBytes();
            Hash = Fnv1a64.Compute(_stableBytes);
        }

        public bool TryGetItem(string instanceId, out PuzzleItem item)
        {
            if (instanceId == null)
            {
                item = null;
                return false;
            }
            return _byId.TryGetValue(instanceId, out item);
        }

        /// <summary>Items in one container kind, ordinal instance id order.</summary>
        public IReadOnlyList<PuzzleItem> GetItems(ItemLocationKind kind)
        {
            var result = new List<PuzzleItem>();
            foreach (var item in Items)
                if (item.Location.Kind == kind)
                    result.Add(item);
            return result.AsReadOnly();
        }

        /// <summary>Directly nested children of a parent, ordinal instance id order.</summary>
        public IReadOnlyList<PuzzleItem> GetChildren(string parentInstanceId)
        {
            var result = new List<PuzzleItem>();
            foreach (var item in Items)
                if (item.Location.Kind == ItemLocationKind.Nested
                    && string.Equals(item.Location.TargetId, parentInstanceId, StringComparison.Ordinal))
                    result.Add(item);
            return result.AsReadOnly();
        }

        /// <summary>Items in a destination, ordinal instance id order.</summary>
        public IReadOnlyList<PuzzleItem> GetDestinationItems(string destinationId)
        {
            var result = new List<PuzzleItem>();
            foreach (var item in Items)
                if (item.Location.Kind == ItemLocationKind.Destination
                    && string.Equals(item.Location.TargetId, destinationId, StringComparison.Ordinal))
                    result.Add(item);
            return result.AsReadOnly();
        }

        /// <summary>Returns a new state with one item replaced (same instance id).</summary>
        public PuzzleState With(PuzzleItem replacement)
        {
            if (replacement == null || !_byId.ContainsKey(replacement.InstanceId))
                throw new ArgumentException("UnknownInstance: " + replacement?.InstanceId, nameof(replacement));
            var items = new List<PuzzleItem>(Items.Count);
            foreach (var item in Items)
                items.Add(item.InstanceId == replacement.InstanceId ? replacement : item);
            return new PuzzleState(Spec, items);
        }

        /// <summary>
        /// Physical cells of an item (ADR-0006 Decision 12): rotated footprint of its current state offset by the
        /// anchor, for z in [layer, layer + thickness). Empty for every non-suitcase location. Bounds are not checked.
        /// </summary>
        public static IReadOnlyList<PhysicalCell> GetPhysicalCells(PuzzleItem item)
        {
            if (item == null || item.Location.Kind != ItemLocationKind.Suitcase)
                return Array.Empty<PhysicalCell>();
            return GetPhysicalCells(item.State, item.Location.Placement);
        }

        /// <summary>Physical cells that <paramref name="state"/> would occupy at <paramref name="placement"/>; empty if the rotation is not allowed.</summary>
        public static IReadOnlyList<PhysicalCell> GetPhysicalCells(ItemStateSpec state, Placement placement)
        {
            if (state == null || !state.TryGetFootprint(placement.Rotation, out var footprint))
                return Array.Empty<PhysicalCell>();
            var cells = new List<PhysicalCell>(footprint.CellCount * state.Thickness);
            for (var z = placement.Layer; z < placement.Layer + state.Thickness; z++)
                foreach (var local in footprint.OccupiedCells)
                    cells.Add(new PhysicalCell(placement.Compartment, placement.Anchor.X + local.X, placement.Anchor.Y + local.Y, z));
            return cells.AsReadOnly();
        }

        public byte[] ToStableBytes()
        {
            var copy = new byte[_stableBytes.Length];
            Array.Copy(_stableBytes, copy, _stableBytes.Length);
            return copy;
        }

        public bool Equals(PuzzleState other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other == null || Hash != other.Hash || _stableBytes.Length != other._stableBytes.Length)
                return false;
            for (var i = 0; i < _stableBytes.Length; i++)
                if (_stableBytes[i] != other._stableBytes[i])
                    return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as PuzzleState);

        public override int GetHashCode() => unchecked((int)Hash);

        private void ValidateReferences(PuzzleSpec spec, PuzzleItem item)
        {
            var location = item.Location;
            switch (location.Kind)
            {
                case ItemLocationKind.Suitcase:
                    if (!spec.Board.TryGetCompartment(location.Placement.Compartment, out _))
                        throw new ArgumentException($"UnknownCompartment: {item.InstanceId} {location.Placement.Compartment}");
                    break;
                case ItemLocationKind.Destination:
                    if (!spec.TryGetDestination(location.TargetId, out _))
                        throw new ArgumentException($"UnknownDestination: {item.InstanceId} {location.TargetId}");
                    break;
                case ItemLocationKind.Nested:
                    // Walk the parent chain: every parent must exist and the chain must not loop.
                    var seen = new HashSet<string>(StringComparer.Ordinal) { item.InstanceId };
                    var current = item;
                    while (current.Location.Kind == ItemLocationKind.Nested)
                    {
                        if (!_byId.TryGetValue(current.Location.TargetId, out var parent))
                            throw new ArgumentException($"UnknownNestParent: {current.InstanceId} {current.Location.TargetId}");
                        if (!seen.Add(parent.InstanceId))
                            throw new ArgumentException("NestCycle: " + item.InstanceId);
                        current = parent;
                    }
                    break;
            }
        }

        private byte[] BuildStableBytes()
        {
            using (var stream = new MemoryStream())
            {
                CanonicalWriter.WriteInt32(stream, FormatVersion);
                CanonicalWriter.WriteInt32(stream, Items.Count);
                foreach (var item in Items)
                {
                    CanonicalWriter.WriteString(stream, item.InstanceId);
                    CanonicalWriter.WriteString(stream, item.Definition.Id);
                    CanonicalWriter.WriteString(stream, item.StateId);
                    CanonicalWriter.WriteInt32(stream, (int)item.Role);
                    CanonicalWriter.WriteInt32(stream, (int)item.Location.Kind);
                    switch (item.Location.Kind)
                    {
                        case ItemLocationKind.Suitcase:
                            var placement = item.Location.Placement;
                            CanonicalWriter.WriteString(stream, placement.Compartment);
                            CanonicalWriter.WriteInt32(stream, placement.Anchor.X);
                            CanonicalWriter.WriteInt32(stream, placement.Anchor.Y);
                            CanonicalWriter.WriteInt32(stream, placement.Layer);
                            CanonicalWriter.WriteInt32(stream, (int)placement.Rotation);
                            break;
                        case ItemLocationKind.Nested:
                        case ItemLocationKind.Destination:
                            CanonicalWriter.WriteString(stream, item.Location.TargetId);
                            break;
                    }
                }
                return stream.ToArray();
            }
        }
    }
}
