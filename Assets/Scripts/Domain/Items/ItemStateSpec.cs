using System;
using System.Collections.Generic;
using System.IO;
using ZipTrip.Domain.Board;

namespace ZipTrip.Domain.Items
{
    /// <summary>
    /// One authored state of an item (ADR-0006 Decisions 8 and 12): a canonical 2D footprint, a thickness in
    /// layers and the ADR-0003 rotations allowed for that footprint. Rotated footprints are derived, never authored
    /// as extra states. Holds no layer, placement or other runtime data.
    /// </summary>
    public sealed class ItemStateSpec
    {
        public const int MinThickness = 1;
        public const int MaxThickness = 2; // Launch board L = 2.

        public string Id { get; }
        /// <summary>Canonical, unrotated footprint (ADR-0003 normalization).</summary>
        public ItemShape Footprint { get; }
        public int Thickness { get; }
        /// <summary>Allowed rotations in ascending order.</summary>
        public IReadOnlyList<Rotation> AllowedRotations { get; }

        public ItemStateSpec(string id, ItemShape footprint, int thickness, IEnumerable<Rotation> allowedRotations)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("MissingStateId", nameof(id));
            if (footprint == null)
                throw new ArgumentException("MissingFootprint: " + id, nameof(footprint));
            if (thickness < MinThickness || thickness > MaxThickness)
                throw new ArgumentException($"InvalidThickness: {id} has {thickness}, expected {MinThickness}..{MaxThickness}",
                    nameof(thickness));
            if (allowedRotations == null)
                throw new ArgumentException("MissingRotations: " + id, nameof(allowedRotations));

            var rotations = new SortedSet<Rotation>();
            foreach (var rotation in allowedRotations)
            {
                if (!Enum.IsDefined(typeof(Rotation), rotation))
                    throw new ArgumentException("UnsupportedRotation: " + id, nameof(allowedRotations));
                rotations.Add(rotation);
            }
            if (rotations.Count == 0)
                throw new ArgumentException("MissingRotations: " + id, nameof(allowedRotations));

            Id = id;
            Footprint = footprint;
            Thickness = thickness;
            AllowedRotations = new List<Rotation>(rotations).AsReadOnly();
        }

        public bool AllowsRotation(Rotation rotation)
        {
            for (var i = 0; i < AllowedRotations.Count; i++)
                if (AllowedRotations[i] == rotation)
                    return true;
            return false;
        }

        /// <summary>ADR-0003 rotated footprint via the shared ItemShape math; false when the rotation is not allowed.</summary>
        public bool TryGetFootprint(Rotation rotation, out ItemShape footprint)
        {
            footprint = AllowsRotation(rotation) ? Footprint.Rotate(rotation) : null;
            return footprint != null;
        }

        internal void WriteStableBytes(Stream stream)
        {
            CanonicalWriter.WriteString(stream, Id);
            CanonicalWriter.WriteInt32(stream, Thickness);
            CanonicalWriter.WriteInt32(stream, AllowedRotations.Count);
            foreach (var rotation in AllowedRotations)
                CanonicalWriter.WriteInt32(stream, (int)rotation);
            CanonicalWriter.WriteInt32(stream, Footprint.CellCount);
            foreach (var cell in Footprint.OccupiedCells) // Normalized, row-major.
            {
                CanonicalWriter.WriteInt32(stream, cell.X);
                CanonicalWriter.WriteInt32(stream, cell.Y);
            }
        }
    }
}
