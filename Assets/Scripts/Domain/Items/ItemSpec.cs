using System;
using System.Collections.Generic;
using System.IO;
using ZipTrip.Domain.Board;

namespace ZipTrip.Domain.Items
{
    /// <summary>State-changing item modifiers (ADR-0006 Decision 8). Nest is containment, not a state change.</summary>
    public enum ItemModifier
    {
        /// <summary>Changes the X/Y footprint; area stays equal or loses at most one cell.</summary>
        Fold = 0,
        /// <summary>Changes Z thickness. Not the legacy Vacuum booster.</summary>
        Compress = 1
    }

    /// <summary>An authored, directed modifier edge between two states of the same item.</summary>
    public readonly struct StateTransition
    {
        public ItemModifier Modifier { get; }
        public string FromStateId { get; }
        public string ToStateId { get; }

        public StateTransition(ItemModifier modifier, string fromStateId, string toStateId)
        {
            Modifier = modifier;
            FromStateId = fromStateId;
            ToStateId = toStateId;
        }

        public override string ToString() => $"{Modifier} {FromStateId}->{ToStateId}";
    }

    /// <summary>
    /// Authored, immutable item definition for the ADR-0006 model: states, default state, tags, Fold/Compress
    /// transitions and nest capability. No placement, layer, objective role or other per-level/runtime data.
    /// Collections are held in ordinal order, so equality and hash do not depend on authoring order.
    /// </summary>
    public sealed class ItemSpec : IEquatable<ItemSpec>
    {
        private const int FormatVersion = 1;
        private readonly Dictionary<string, ItemStateSpec> _statesById;
        private readonly byte[] _stableBytes;

        public string Id { get; }
        public string DefaultStateId { get; }
        public ItemStateSpec DefaultState => _statesById[DefaultStateId];
        /// <summary>States in ordinal id order.</summary>
        public IReadOnlyList<ItemStateSpec> States { get; }
        /// <summary>Tags in ordinal order.</summary>
        public IReadOnlyList<string> Tags { get; }
        /// <summary>Transitions ordered by source state id, then modifier.</summary>
        public IReadOnlyList<StateTransition> Transitions { get; }
        public NestSpec Nest { get; }

        public ItemSpec(string id, string defaultStateId, IEnumerable<ItemStateSpec> states,
            IEnumerable<string> tags = null, IEnumerable<StateTransition> transitions = null, NestSpec nest = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("MissingItemId", nameof(id));
            if (states == null)
                throw new ArgumentException("MissingStates: " + id, nameof(states));

            _statesById = new Dictionary<string, ItemStateSpec>(StringComparer.Ordinal);
            var orderedStates = new List<ItemStateSpec>();
            foreach (var state in states)
            {
                if (state == null)
                    throw new ArgumentException("MissingStates: null state in " + id, nameof(states));
                if (_statesById.ContainsKey(state.Id))
                    throw new ArgumentException($"DuplicateStateId: {id}.{state.Id}", nameof(states));
                _statesById.Add(state.Id, state);
                orderedStates.Add(state);
            }
            if (orderedStates.Count == 0)
                throw new ArgumentException("MissingStates: " + id, nameof(states));
            if (defaultStateId == null || !_statesById.ContainsKey(defaultStateId))
                throw new ArgumentException($"MissingDefaultState: {id}.{defaultStateId}", nameof(defaultStateId));

            var sortedTags = new SortedSet<string>(StringComparer.Ordinal);
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                        throw new ArgumentException("InvalidTag: " + id, nameof(tags));
                    sortedTags.Add(tag);
                }
            }

            var orderedTransitions = ValidateTransitions(id, transitions);

            orderedStates.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Id = id;
            DefaultStateId = defaultStateId;
            States = orderedStates.AsReadOnly();
            Tags = new List<string>(sortedTags).AsReadOnly();
            Transitions = orderedTransitions.AsReadOnly();
            Nest = nest ?? NestSpec.None;
            _stableBytes = BuildStableBytes();
        }

        public bool TryGetState(string stateId, out ItemStateSpec state)
        {
            if (stateId == null)
            {
                state = null;
                return false;
            }
            return _statesById.TryGetValue(stateId, out state);
        }

        /// <summary>Authored target of <paramref name="modifier"/> from a state. Never throws; false when none is authored.</summary>
        public bool TryGetTransition(string fromStateId, ItemModifier modifier, out ItemStateSpec target)
        {
            foreach (var transition in Transitions)
            {
                if (transition.Modifier == modifier && string.Equals(transition.FromStateId, fromStateId, StringComparison.Ordinal))
                    return _statesById.TryGetValue(transition.ToStateId, out target);
            }
            target = null;
            return false;
        }

        public bool HasTag(string tag) => NestSpec.Contains(Tags, tag);

        public byte[] ToStableBytes()
        {
            var copy = new byte[_stableBytes.Length];
            Array.Copy(_stableBytes, copy, _stableBytes.Length);
            return copy;
        }

        public bool Equals(ItemSpec other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other == null || _stableBytes.Length != other._stableBytes.Length)
                return false;
            for (var i = 0; i < _stableBytes.Length; i++)
                if (_stableBytes[i] != other._stableBytes[i])
                    return false;
            return true;
        }

        public override bool Equals(object obj) => Equals(obj as ItemSpec);

        public override int GetHashCode() => unchecked((int)Fnv1a64.Compute(_stableBytes));

        private List<StateTransition> ValidateTransitions(string id, IEnumerable<StateTransition> transitions)
        {
            var result = new List<StateTransition>();
            if (transitions == null)
                return result;

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var transition in transitions)
            {
                if (!Enum.IsDefined(typeof(ItemModifier), transition.Modifier))
                    throw new ArgumentException($"UnsupportedModifier: {id} {transition}", nameof(transitions));
                if (transition.FromStateId == null || transition.ToStateId == null
                    || !_statesById.TryGetValue(transition.FromStateId, out var from)
                    || !_statesById.TryGetValue(transition.ToStateId, out var to))
                    throw new ArgumentException($"MissingTransitionState: {id} {transition}", nameof(transitions));
                if (ReferenceEquals(from, to))
                    throw new ArgumentException($"SelfTransition: {id} {transition}", nameof(transitions));
                // One target per (source, modifier): the modifier action is deterministic.
                if (!keys.Add((int)transition.Modifier + "|" + transition.FromStateId))
                    throw new ArgumentException($"DuplicateTransition: {id} {transition}", nameof(transitions));

                if (transition.Modifier == ItemModifier.Fold && IsSameFootprintUpToRotation(from.Footprint, to.Footprint))
                    throw new ArgumentException($"FoldGeometryUnchanged: {id} {transition}", nameof(transitions));
                if (transition.Modifier == ItemModifier.Compress && from.Thickness == to.Thickness)
                    throw new ArgumentException($"CompressThicknessUnchanged: {id} {transition}", nameof(transitions));

                result.Add(transition);
            }

            ValidateFoldArea(id, result);
            result.Sort((a, b) =>
            {
                var order = string.CompareOrdinal(a.FromStateId, b.FromStateId);
                return order != 0 ? order : a.Modifier.CompareTo(b.Modifier);
            });
            return result;
        }

        // Blueprint §8: Fold keeps area or loses at most one cell. Checked over every Fold-connected group of states
        // (largest minus smallest area <= 1), so chained or reversed folds can never become a free size reduction.
        private void ValidateFoldArea(string id, List<StateTransition> transitions)
        {
            var neighbours = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var transition in transitions)
            {
                if (transition.Modifier != ItemModifier.Fold)
                    continue;
                AddNeighbour(neighbours, transition.FromStateId, transition.ToStateId);
                AddNeighbour(neighbours, transition.ToStateId, transition.FromStateId);
            }

            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (var start in neighbours.Keys)
            {
                if (!visited.Add(start))
                    continue;
                int min = int.MaxValue, max = int.MinValue;
                var pending = new Stack<string>();
                pending.Push(start);
                while (pending.Count > 0)
                {
                    var area = _statesById[pending.Peek()].Footprint.CellCount;
                    min = Math.Min(min, area);
                    max = Math.Max(max, area);
                    foreach (var next in neighbours[pending.Pop()])
                        if (visited.Add(next))
                            pending.Push(next);
                }
                if (max - min > 1)
                    throw new ArgumentException($"FoldAreaLossTooLarge: {id} Fold states span areas {min}..{max}",
                        nameof(transitions));
            }
        }

        private static void AddNeighbour(Dictionary<string, List<string>> neighbours, string from, string to)
        {
            if (!neighbours.TryGetValue(from, out var list))
                neighbours.Add(from, list = new List<string>());
            list.Add(to);
        }

        private static bool IsSameFootprintUpToRotation(ItemShape a, ItemShape b)
        {
            if (a.CellCount != b.CellCount)
                return false;
            foreach (Rotation rotation in Enum.GetValues(typeof(Rotation)))
            {
                var rotated = a.Rotate(rotation).OccupiedCells;
                var same = true;
                for (var i = 0; i < rotated.Count && same; i++)
                    same = rotated[i] == b.OccupiedCells[i];
                if (same)
                    return true;
            }
            return false;
        }

        private byte[] BuildStableBytes()
        {
            using (var stream = new MemoryStream())
            {
                CanonicalWriter.WriteInt32(stream, FormatVersion);
                CanonicalWriter.WriteString(stream, Id);
                CanonicalWriter.WriteString(stream, DefaultStateId);
                CanonicalWriter.WriteInt32(stream, States.Count);
                foreach (var state in States)
                    state.WriteStableBytes(stream);
                CanonicalWriter.WriteInt32(stream, Tags.Count);
                foreach (var tag in Tags)
                    CanonicalWriter.WriteString(stream, tag);
                CanonicalWriter.WriteInt32(stream, Transitions.Count);
                foreach (var transition in Transitions)
                {
                    CanonicalWriter.WriteInt32(stream, (int)transition.Modifier);
                    CanonicalWriter.WriteString(stream, transition.FromStateId);
                    CanonicalWriter.WriteString(stream, transition.ToStateId);
                }
                Nest.WriteStableBytes(stream);
                return stream.ToArray();
            }
        }
    }
}
