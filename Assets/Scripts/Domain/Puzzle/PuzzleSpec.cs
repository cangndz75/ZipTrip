using System;
using System.Collections.Generic;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;

namespace ZipTrip.Domain.Puzzle
{
    /// <summary>
    /// Level-defined external objective sink (ADR-0006 Decision 16): id, capacity and authored acceptance by item
    /// definition id, tag and/or objective role. No geometry; outside the suitcase rule domain.
    /// </summary>
    public sealed class ExtractionDestinationSpec
    {
        public string Id { get; }
        public int Capacity { get; }
        /// <summary>Accepted item definition ids (ItemSpec.Id), ordinal order.</summary>
        public IReadOnlyList<string> AcceptedItemIds { get; }
        public IReadOnlyList<string> AcceptedTags { get; }
        public IReadOnlyList<ObjectiveRole> AcceptedRoles { get; }

        public ExtractionDestinationSpec(string id, int capacity, IEnumerable<string> acceptedItemIds = null,
            IEnumerable<string> acceptedTags = null, IEnumerable<ObjectiveRole> acceptedRoles = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("MissingDestinationId", nameof(id));
            if (capacity < 1)
                throw new ArgumentException($"InvalidDestinationCapacity: {id} has {capacity}", nameof(capacity));

            var ids = SortedStrings(acceptedItemIds, id);
            var tags = SortedStrings(acceptedTags, id);
            var roles = new SortedSet<ObjectiveRole>();
            if (acceptedRoles != null)
                foreach (var role in acceptedRoles)
                {
                    if (!Enum.IsDefined(typeof(ObjectiveRole), role) || role == ObjectiveRole.None)
                        throw new ArgumentException("MalformedDestinationAcceptance: invalid role in " + id, nameof(acceptedRoles));
                    roles.Add(role);
                }
            if (ids.Count + tags.Count + roles.Count == 0)
                throw new ArgumentException("MalformedDestinationAcceptance: " + id + " accepts nothing");

            Id = id;
            Capacity = capacity;
            AcceptedItemIds = new List<string>(ids).AsReadOnly();
            AcceptedTags = new List<string>(tags).AsReadOnly();
            AcceptedRoles = new List<ObjectiveRole>(roles).AsReadOnly();
        }

        /// <summary>Authored acceptance only (not remaining capacity). Never throws.</summary>
        public bool Accepts(PuzzleItem item)
        {
            if (item == null)
                return false;
            foreach (var role in AcceptedRoles)
                if (role == item.Role)
                    return true;
            if (NestSpec.Contains(AcceptedItemIds, item.Definition.Id))
                return true;
            foreach (var tag in AcceptedTags)
                if (item.Definition.HasTag(tag))
                    return true;
            return false;
        }

        private static SortedSet<string> SortedStrings(IEnumerable<string> values, string id)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            if (values != null)
                foreach (var value in values)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        throw new ArgumentException("MalformedDestinationAcceptance: blank entry in " + id);
                    set.Add(value);
                }
            return set;
        }
    }

    /// <summary>
    /// Fixed structural configuration of one puzzle (ADR-0006 Decisions 11, 12, 16): board, staging capacity and
    /// extraction destinations. Constant for every state of a level, so it is not part of the state hash.
    /// </summary>
    public sealed class PuzzleSpec
    {
        private readonly Dictionary<string, ExtractionDestinationSpec> _destinationsById;

        public BoardSpec Board { get; }
        public int StagingCapacity { get; }
        /// <summary>Destinations in ordinal id order.</summary>
        public IReadOnlyList<ExtractionDestinationSpec> Destinations { get; }

        public PuzzleSpec(BoardSpec board, int stagingCapacity, IEnumerable<ExtractionDestinationSpec> destinations = null)
        {
            if (board == null)
                throw new ArgumentException("MissingBoard", nameof(board));
            if (stagingCapacity < 0)
                throw new ArgumentException($"InvalidStagingCapacity: {stagingCapacity}", nameof(stagingCapacity));

            _destinationsById = new Dictionary<string, ExtractionDestinationSpec>(StringComparer.Ordinal);
            var ordered = new List<ExtractionDestinationSpec>();
            if (destinations != null)
                foreach (var destination in destinations)
                {
                    if (destination == null || _destinationsById.ContainsKey(destination.Id))
                        throw new ArgumentException("DuplicateDestinationId: " + destination?.Id, nameof(destinations));
                    _destinationsById.Add(destination.Id, destination);
                    ordered.Add(destination);
                }
            ordered.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            Board = board;
            StagingCapacity = stagingCapacity;
            Destinations = ordered.AsReadOnly();
        }

        public bool TryGetDestination(string id, out ExtractionDestinationSpec destination)
        {
            if (id == null)
            {
                destination = null;
                return false;
            }
            return _destinationsById.TryGetValue(id, out destination);
        }
    }
}
