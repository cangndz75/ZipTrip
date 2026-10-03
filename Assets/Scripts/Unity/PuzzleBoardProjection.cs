using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    // A compartment's presentation frame in world space: origin = top-left corner of cell (0, 0). Unrotated.
    public readonly struct CompartmentFrame
    {
        public string Id { get; }
        public Vector3 Origin { get; }
        public int Width { get; }
        public int Height { get; }

        public CompartmentFrame(string id, Vector3 origin, int width, int height)
        {
            Id = id;
            Origin = origin;
            Width = width;
            Height = height;
        }

        public bool ContainsXZ(Vector3 world) =>
            world.x >= Origin.x && world.x < Origin.x + Width && -(world.z - Origin.z) >= 0f && -(world.z - Origin.z) < Height;
    }

    // Pure world -> (compartment, local anchor) conversion for a dragged footprint. Coordinates only: the Domain decides
    // legality. Compartment frames come from presentation roots, so scenes may place compartments anywhere.
    public static class PuzzleBoardProjection
    {
        /// <summary>
        /// The compartment is the one containing the footprint's centre; the anchor is the raw nearest integer cell of
        /// <paramref name="anchorWorld"/> relative to that compartment (GridProjector rounding, never clamped).
        /// False when the footprint centre is over no compartment.
        /// </summary>
        public static bool TryProject(IReadOnlyList<CompartmentFrame> frames, Vector3 anchorWorld, ItemShape footprint,
            out string compartmentId, out Cell anchor)
        {
            var width = 0;
            var height = 0;
            foreach (var cell in footprint.OccupiedCells)
            {
                width = Mathf.Max(width, cell.X + 1);
                height = Mathf.Max(height, cell.Y + 1);
            }
            var center = anchorWorld + new Vector3(width * 0.5f, 0f, -height * 0.5f);
            foreach (var frame in frames)
            {
                if (!frame.ContainsXZ(center))
                    continue;
                compartmentId = frame.Id;
                anchor = GridProjector.WorldToAnchor(anchorWorld - frame.Origin);
                return true;
            }
            compartmentId = null;
            anchor = default;
            return false;
        }
    }
}
