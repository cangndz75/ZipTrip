using System;
using System.Collections.Generic;
using System.IO;

namespace ZipTrip.Domain.Board
{
    /// <summary>
    /// One independently sized grid of the suitcase board (ADR-0006 Decision 12): W x H columns,
    /// L layers, an XY valid-cell mask and XY-column zone metadata. Immutable; no occupancy.
    /// Zones belong to an XY column, never to a layer: a zone on (x, y) applies to every z of that column.
    /// Layer-sensitive play is expressed through Layer / Access, not per-layer zones. At most one zone per column.
    /// </summary>
    public sealed class Compartment : IEquatable<Compartment>
    {
        private readonly string[] _columnZones; // One entry per XY column, row-major; null = no zone. Not per layer.
        private readonly byte[] _stableBytes;

        public string Id { get; }
        public int Width => Mask.Width;
        public int Height => Mask.Height;
        public int Layers { get; }
        public ContainerMask Mask { get; }
        /// <summary>Distinct zone ids used by this compartment's XY columns, ordinal order.</summary>
        public IReadOnlyList<string> ColumnZoneIds { get; }

        public Compartment(string id, int width, int height, int layers, IEnumerable<Cell> validCells,
            IEnumerable<KeyValuePair<Cell, string>> columnZones = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Compartment id is required.", nameof(id));
            if (layers <= 0)
                throw new ArgumentOutOfRangeException(nameof(layers), layers, "Layer count must be positive.");

            Id = id;
            Layers = layers;
            Mask = new ContainerMask(width, height, validCells);
            _columnZones = new string[width * height];

            var zoneIds = new SortedSet<string>(StringComparer.Ordinal);
            if (columnZones != null)
            {
                foreach (var entry in columnZones)
                {
                    if (string.IsNullOrWhiteSpace(entry.Value))
                        throw new ArgumentException("Zone id is required.", nameof(columnZones));
                    if (!Mask.IsValid(entry.Key))
                        throw new ArgumentOutOfRangeException(nameof(columnZones), entry.Key, "Zone column must be a valid mask cell.");

                    var index = entry.Key.Y * width + entry.Key.X;
                    if (_columnZones[index] != null && !string.Equals(_columnZones[index], entry.Value, StringComparison.Ordinal))
                        throw new ArgumentException($"Column {entry.Key} is assigned to more than one zone.", nameof(columnZones));

                    _columnZones[index] = entry.Value;
                    zoneIds.Add(entry.Value);
                }
            }

            ColumnZoneIds = new List<string>(zoneIds).AsReadOnly();
            _stableBytes = BuildStableBytes();
        }

        public bool IsValidColumn(Cell column) => Mask.IsValid(column);

        public bool IsWithinLayers(int z) => z >= 0 && z < Layers;

        /// <summary>True when the cell belongs to this compartment, its column is a valid mask cell and z is in range.</summary>
        public bool Contains(PhysicalCell cell)
        {
            return string.Equals(cell.Compartment, Id, StringComparison.Ordinal)
                && IsWithinLayers(cell.Z)
                && Mask.IsValid(cell.Column);
        }

        /// <summary>Zone of an XY column; null when the column has no zone or is not a valid mask cell.</summary>
        public string GetColumnZone(Cell column)
        {
            return Mask.IsValid(column) ? _columnZones[column.Y * Width + column.X] : null;
        }

        /// <summary>Zone of the physical cell's XY column (identical for every z); null when the cell is not contained.</summary>
        public string GetZone(PhysicalCell cell)
        {
            return Contains(cell) ? _columnZones[cell.Y * Width + cell.X] : null;
        }

        /// <summary>Every physical cell: layers ascending, then row-major columns.</summary>
        public IEnumerable<PhysicalCell> GetPhysicalCells()
        {
            for (var z = 0; z < Layers; z++)
                foreach (var column in Mask.GetValidCells())
                    yield return new PhysicalCell(Id, column, z);
        }

        public byte[] ToStableBytes()
        {
            var copy = new byte[_stableBytes.Length];
            Array.Copy(_stableBytes, copy, _stableBytes.Length);
            return copy;
        }

        public bool Equals(Compartment other)
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

        public override bool Equals(object obj) => Equals(obj as Compartment);

        public override int GetHashCode() => unchecked((int)Fnv1a64.Compute(_stableBytes));

        private byte[] BuildStableBytes()
        {
            using (var stream = new MemoryStream())
            {
                CanonicalWriter.WriteString(stream, Id);
                CanonicalWriter.WriteInt32(stream, Width);
                CanonicalWriter.WriteInt32(stream, Height);
                CanonicalWriter.WriteInt32(stream, Layers);
                CanonicalWriter.WriteBytes(stream, Mask.ToStableBytes());

                var zoned = 0;
                foreach (var zone in _columnZones)
                    if (zone != null) zoned++;
                CanonicalWriter.WriteInt32(stream, zoned);
                foreach (var column in Mask.GetValidCells()) // Row-major.
                {
                    var zone = _columnZones[column.Y * Width + column.X];
                    if (zone == null)
                        continue;
                    CanonicalWriter.WriteInt32(stream, column.X);
                    CanonicalWriter.WriteInt32(stream, column.Y);
                    CanonicalWriter.WriteString(stream, zone);
                }
                return stream.ToArray();
            }
        }
    }
}
