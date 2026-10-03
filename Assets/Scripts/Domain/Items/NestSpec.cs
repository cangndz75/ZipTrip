using System;
using System.Collections.Generic;
using System.IO;
using ZipTrip.Domain.Board;

namespace ZipTrip.Domain.Items
{
    /// <summary>
    /// Authored nesting capability of a parent item (ADR-0006 Decision 8): a finite child count and an authored
    /// compatibility list (item ids and/or tags). Compatibility is never inferred from size; there is no spatial
    /// packing inside the parent. Data only: nested ownership, access and movement are later tickets.
    /// </summary>
    public sealed class NestSpec
    {
        public static readonly NestSpec None = new NestSpec(0, null, null);

        /// <summary>Maximum number of nested children; 0 = cannot contain children.</summary>
        public int Capacity { get; }
        /// <summary>Accepted child item definition ids (ItemSpec.Id), ordinal order. Not instance ids.</summary>
        public IReadOnlyList<string> AcceptedDefinitionIds { get; }
        public IReadOnlyList<string> AcceptedTags { get; }

        public NestSpec(int capacity, IEnumerable<string> acceptedDefinitionIds, IEnumerable<string> acceptedTags)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "InvalidNestCapacity: capacity must be >= 0.");

            var ids = ToSortedSet(acceptedDefinitionIds, nameof(acceptedDefinitionIds));
            var tags = ToSortedSet(acceptedTags, nameof(acceptedTags));
            var hasCompatibility = ids.Count + tags.Count > 0;
            if (capacity == 0 && hasCompatibility)
                throw new ArgumentException("MalformedNestCompatibility: capacity 0 cannot accept children.");
            if (capacity > 0 && !hasCompatibility)
                throw new ArgumentException("MalformedNestCompatibility: a nest needs accepted definition ids or tags.");

            Capacity = capacity;
            AcceptedDefinitionIds = new List<string>(ids).AsReadOnly();
            AcceptedTags = new List<string>(tags).AsReadOnly();
        }

        /// <summary>Authored compatibility only (not remaining capacity). Never throws; null child is not accepted.</summary>
        public bool Accepts(ItemSpec child)
        {
            if (Capacity == 0 || child == null)
                return false;
            if (Contains(AcceptedDefinitionIds, child.Id))
                return true;
            foreach (var tag in AcceptedTags)
                if (child.HasTag(tag))
                    return true;
            return false;
        }

        internal void WriteStableBytes(Stream stream)
        {
            CanonicalWriter.WriteInt32(stream, Capacity);
            CanonicalWriter.WriteInt32(stream, AcceptedDefinitionIds.Count);
            foreach (var id in AcceptedDefinitionIds)
                CanonicalWriter.WriteString(stream, id);
            CanonicalWriter.WriteInt32(stream, AcceptedTags.Count);
            foreach (var tag in AcceptedTags)
                CanonicalWriter.WriteString(stream, tag);
        }

        internal static bool Contains(IReadOnlyList<string> values, string value)
        {
            foreach (var candidate in values)
                if (string.Equals(candidate, value, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static SortedSet<string> ToSortedSet(IEnumerable<string> values, string parameter)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            if (values == null)
                return set;
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("MalformedNestCompatibility: blank entry.", parameter);
                set.Add(value);
            }
            return set;
        }
    }
}
