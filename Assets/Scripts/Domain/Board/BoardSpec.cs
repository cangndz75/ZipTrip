using System;
using System.Collections.Generic;
using System.IO;

namespace ZipTrip.Domain.Board
{
    /// <summary>
    /// The suitcase board of ADR-0006 Decision 12: a set of compartments, each with its own W x H x L.
    /// Compartments are held in ordinal id order, so equality and serialization do not depend on authoring order.
    /// </summary>
    public sealed class BoardSpec : IEquatable<BoardSpec>
    {
        private const int FormatVersion = 1;
        private readonly Dictionary<string, Compartment> _byId;
        private readonly byte[] _stableBytes;

        public IReadOnlyList<Compartment> Compartments { get; }

        public BoardSpec(IEnumerable<Compartment> compartments)
        {
            if (compartments == null)
                throw new ArgumentNullException(nameof(compartments));

            _byId = new Dictionary<string, Compartment>(StringComparer.Ordinal);
            var ordered = new List<Compartment>();
            foreach (var compartment in compartments)
            {
                if (compartment == null)
                    throw new ArgumentException("Compartment must not be null.", nameof(compartments));
                if (_byId.ContainsKey(compartment.Id))
                    throw new ArgumentException($"Duplicate compartment id '{compartment.Id}'.", nameof(compartments));
                _byId.Add(compartment.Id, compartment);
                ordered.Add(compartment);
            }
            if (ordered.Count == 0)
                throw new ArgumentException("A board needs at least one compartment.", nameof(compartments));

            ordered.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Compartments = ordered.AsReadOnly();
            _stableBytes = BuildStableBytes();
        }

        public bool TryGetCompartment(string id, out Compartment compartment)
        {
            if (id == null)
            {
                compartment = null;
                return false;
            }
            return _byId.TryGetValue(id, out compartment);
        }

        /// <summary>Safe for any input: unknown compartment, out-of-range XY, masked column or z out of range return false.</summary>
        public bool Contains(PhysicalCell cell)
        {
            return TryGetCompartment(cell.Compartment, out var compartment) && compartment.Contains(cell);
        }

        /// <summary>Zone of the physical cell's XY column (zones are column metadata, identical for every z); null when not contained.</summary>
        public string GetZone(PhysicalCell cell)
        {
            return TryGetCompartment(cell.Compartment, out var compartment) ? compartment.GetZone(cell) : null;
        }

        /// <summary>Every physical cell: compartments in ordinal id order, then layers ascending, then row-major.</summary>
        public IEnumerable<PhysicalCell> GetPhysicalCells()
        {
            foreach (var compartment in Compartments)
                foreach (var cell in compartment.GetPhysicalCells())
                    yield return cell;
        }

        public byte[] ToStableBytes()
        {
            var copy = new byte[_stableBytes.Length];
            Array.Copy(_stableBytes, copy, _stableBytes.Length);
            return copy;
        }

        public bool Equals(BoardSpec other)
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

        public override bool Equals(object obj) => Equals(obj as BoardSpec);

        public override int GetHashCode() => unchecked((int)Fnv1a64.Compute(_stableBytes));

        private byte[] BuildStableBytes()
        {
            using (var stream = new MemoryStream())
            {
                CanonicalWriter.WriteInt32(stream, FormatVersion);
                CanonicalWriter.WriteInt32(stream, Compartments.Count);
                foreach (var compartment in Compartments)
                    CanonicalWriter.WriteBytes(stream, compartment.ToStableBytes());
                return stream.ToArray();
            }
        }
    }
}
