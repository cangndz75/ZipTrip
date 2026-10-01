using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ZipTrip.Domain
{
    public enum ItemRotationRejectionReason
    {
        None,
        RotationNotAllowed
    }

    public readonly struct ItemRotationResult
    {
        public bool IsAccepted { get; }
        public ItemShape Shape { get; }
        public ItemRotationRejectionReason RejectionReason { get; }

        private ItemRotationResult(bool isAccepted, ItemShape shape, ItemRotationRejectionReason rejectionReason)
        {
            IsAccepted = isAccepted;
            Shape = shape;
            RejectionReason = rejectionReason;
        }

        internal static ItemRotationResult Accepted(ItemShape shape)
        {
            return new ItemRotationResult(true, shape, ItemRotationRejectionReason.None);
        }

        internal static ItemRotationResult Rejected(ItemRotationRejectionReason reason)
        {
            return new ItemRotationResult(false, null, reason);
        }
    }

    public sealed class ItemDefinition
    {
        private readonly IReadOnlyDictionary<string, ItemShape> _shapeStates;
        private readonly IReadOnlyList<Rotation> _allowedRotations;
        private readonly IReadOnlyList<string> _authoredShapeStateIds;

        public string Id { get; }
        public string BaseStateId { get; }
        public IReadOnlyDictionary<string, ItemShape> ShapeStates => _shapeStates;
        public IReadOnlyList<Rotation> AllowedRotations => _allowedRotations;
        /// <summary>Explicit authored order for stable ordinal content mapping; empty when unspecified.</summary>
        public IReadOnlyList<string> AuthoredShapeStateIds => _authoredShapeStateIds;
        public IReadOnlyList<string> Tags { get; }
        public ItemShape VacuumShape { get; }

        public ItemDefinition(
            string id,
            string baseStateId,
            IEnumerable<KeyValuePair<string, ItemShape>> shapeStates,
            IEnumerable<Rotation> allowedRotations,
            IEnumerable<string> tags,
            ItemShape vacuumShape = null,
            IEnumerable<string> authoredShapeStateIds = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Item id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(baseStateId))
                throw new ArgumentException("Base state id is required.", nameof(baseStateId));
            if (shapeStates == null)
                throw new ArgumentNullException(nameof(shapeStates));
            if (allowedRotations == null)
                throw new ArgumentNullException(nameof(allowedRotations));

            var states = new SortedDictionary<string, ItemShape>(StringComparer.Ordinal);
            foreach (var state in shapeStates)
            {
                if (string.IsNullOrWhiteSpace(state.Key) || state.Value == null || states.ContainsKey(state.Key))
                    throw new ArgumentException("Shape states require unique ids and non-null shapes.", nameof(shapeStates));
                states.Add(state.Key, state.Value);
            }

            if (!states.TryGetValue(baseStateId, out var baseShape))
                throw new ArgumentException("Base state is missing.", nameof(shapeStates));

            var authoredOrder = new List<string>();
            if (authoredShapeStateIds != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var stateId in authoredShapeStateIds)
                {
                    if (string.IsNullOrWhiteSpace(stateId) || !states.ContainsKey(stateId) || !seen.Add(stateId))
                        throw new ArgumentException("Authored state order must list each state id once.",
                            nameof(authoredShapeStateIds));
                    authoredOrder.Add(stateId);
                }
                if (authoredOrder.Count != states.Count)
                    throw new ArgumentException("Authored state order must include every state.",
                        nameof(authoredShapeStateIds));
            }

            foreach (var state in states)
            {
                if (state.Key == baseStateId)
                    continue;

                var areaDifference = baseShape.CellCount - state.Value.CellCount;
                if (areaDifference < 0 || areaDifference > 1)
                    throw new ArgumentException("Fold state area must equal the base area or be one cell smaller.", nameof(shapeStates));
            }

            var rotations = new SortedSet<Rotation>();
            foreach (var rotation in allowedRotations)
            {
                if (!Enum.IsDefined(typeof(Rotation), rotation))
                    throw new ArgumentException("Unsupported rotation.", nameof(allowedRotations));
                rotations.Add(rotation);
            }
            if (rotations.Count == 0)
                throw new ArgumentException("At least one rotation is required.", nameof(allowedRotations));

            var sortedTags = new SortedSet<string>(StringComparer.Ordinal);
            if (tags != null)
            {
                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                        throw new ArgumentException("Tags must be non-empty.", nameof(tags));
                    sortedTags.Add(tag);
                }
            }

            Id = id;
            BaseStateId = baseStateId;
            _shapeStates = new ReadOnlyDictionary<string, ItemShape>(states);
            _allowedRotations = new List<Rotation>(rotations).AsReadOnly();
            _authoredShapeStateIds = authoredOrder.AsReadOnly();
            Tags = new List<string>(sortedTags).AsReadOnly();
            VacuumShape = vacuumShape;
        }

        public ItemRotationResult GetRotatedShape(string shapeStateId, Rotation rotation)
        {
            if (!_shapeStates.TryGetValue(shapeStateId, out var shape))
                throw new ArgumentException("Unknown shape state.", nameof(shapeStateId));

            if (!ContainsRotation(rotation))
                return ItemRotationResult.Rejected(ItemRotationRejectionReason.RotationNotAllowed);

            return ItemRotationResult.Accepted(shape.Rotate(rotation));
        }

        private bool ContainsRotation(Rotation rotation)
        {
            for (var i = 0; i < _allowedRotations.Count; i++)
            {
                if (_allowedRotations[i] == rotation)
                    return true;
            }
            return false;
        }
    }
}
