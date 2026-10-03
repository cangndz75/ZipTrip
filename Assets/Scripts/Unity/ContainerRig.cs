using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZipTrip.Unity
{
    // ZT-040C presentation contract for swappable container art (Cabin Suitcase now; Backpack / Large Suitcase later).
    // A container model only has to author these node names; the rig binds them on an instance. Presentation only:
    // gameplay never reads it, and nothing here knows which container family it is.
    public sealed class ContainerRig
    {
        public const string BaseNode = "Base";
        public const string LidNode = "Lid";
        public const string InteriorAnchorNode = "InteriorAnchor";
        public const string InteriorMinNode = "InteriorMin";
        public const string InteriorMaxNode = "InteriorMax";
        public const string HingeAnchorNode = "HingeAnchor";
        public static readonly string[] RequiredNodes =
            { BaseNode, LidNode, InteriorAnchorNode, InteriorMinNode, InteriorMaxNode, HingeAnchorNode };

        /// <summary>Lid contract: identity local rotation is the closed pose (consumed by the Zip It ritual, ZT-043).</summary>
        public static readonly Quaternion LidClosedLocalRotation = Quaternion.identity;

        public Transform Root { get; }
        public Transform Base { get; }
        public Transform Lid { get; }
        public Transform InteriorAnchor { get; }
        public Transform InteriorMin { get; }
        public Transform InteriorMax { get; }
        public Transform HingeAnchor { get; }
        /// <summary>The authored (open) lid pose, captured at bind time.</summary>
        public Quaternion LidOpenLocalRotation { get; }

        private ContainerRig(Transform root, IReadOnlyDictionary<string, Transform> nodes)
        {
            Root = root;
            Base = nodes[BaseNode];
            Lid = nodes[LidNode];
            InteriorAnchor = nodes[InteriorAnchorNode];
            InteriorMin = nodes[InteriorMinNode];
            InteriorMax = nodes[InteriorMaxNode];
            HingeAnchor = nodes[HingeAnchorNode];
            LidOpenLocalRotation = Lid.localRotation;
        }

        /// <summary>Binds the required nodes (direct children of <paramref name="root"/>); throws listing any missing.</summary>
        public static ContainerRig Bind(Transform root)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));
            var nodes = new Dictionary<string, Transform>();
            var missing = new List<string>();
            foreach (var name in RequiredNodes)
            {
                var node = root.Find(name);
                if (node == null)
                    missing.Add(name);
                else
                    nodes.Add(name, node);
            }
            if (missing.Count > 0)
                throw new InvalidOperationException($"Container '{root.name}' is missing nodes: {string.Join(", ", missing)}");
            return new ContainerRig(root, nodes);
        }

        public bool LidClosed => Lid.localRotation == LidClosedLocalRotation;

        /// <summary>Snaps the lid to the closed (identity) or authored open pose. No animation (ZT-043 owns Zip It).</summary>
        public void SetLidClosed(bool closed) => Lid.localRotation = closed ? LidClosedLocalRotation : LidOpenLocalRotation;

        /// <summary>Authored interior corners in the root's local space (unscaled model units).</summary>
        public Vector3 InteriorMinLocal => Root.InverseTransformPoint(InteriorMin.position);
        public Vector3 InteriorMaxLocal => Root.InverseTransformPoint(InteriorMax.position);
    }

    /// <summary>Uniform scale + pose that seats a container's authored interior around a board rectangle.</summary>
    public readonly struct ContainerFit
    {
        public float Scale { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }

        public ContainerFit(float scale, Vector3 position, Quaternion rotation)
        {
            Scale = scale;
            Position = position;
            Rotation = rotation;
        }

        /// <summary>
        /// The art adapts to the board: one uniform scale so the authored interior (InteriorMin/Max, root-local) covers
        /// <paramref name="board"/> (contour space x, z) plus <paramref name="padding"/> on every side, centred on the
        /// board, with the interior floor at <paramref name="floorY"/>. Deterministic; the board is never changed.
        /// </summary>
        public static ContainerFit Seat(Rect board, Vector3 interiorMin, Vector3 interiorMax, float padding, float floorY, float yawDegrees)
        {
            var rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            var a = rotation * interiorMin;
            var b = rotation * interiorMax;
            var width = Mathf.Abs(b.x - a.x);
            var depth = Mathf.Abs(b.z - a.z);
            if (width <= 0f || depth <= 0f)
                throw new ArgumentException("Container interior has no area.");
            var scale = Mathf.Max((board.width + 2f * padding) / width, (board.height + 2f * padding) / depth);
            var center = (a + b) * 0.5f * scale;
            var floor = Mathf.Min(a.y, b.y) * scale;
            var position = new Vector3(board.center.x - center.x, floorY - floor, board.center.y - center.z);
            return new ContainerFit(scale, position, rotation);
        }

        /// <summary>The seated interior rectangle (contour space x, z) for this fit.</summary>
        public Rect Interior(Vector3 interiorMin, Vector3 interiorMax)
        {
            var a = Position + Rotation * (interiorMin * Scale);
            var b = Position + Rotation * (interiorMax * Scale);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
        }
    }
}
