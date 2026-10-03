using System;
using System.Collections.Generic;
using ZipTrip.Domain.Items;

namespace ZipTrip.Domain.Puzzle
{
    /// <summary>
    /// Per-instance objective role assigned by the level (ADR-0006 Decision 16 and canonical equivalence key).
    /// Two otherwise identical instances with different roles are never interchangeable.
    /// </summary>
    public enum ObjectiveRole
    {
        /// <summary>Not required by the objective.</summary>
        None = 0,
        /// <summary>Must end inside the suitcase (Pack / Repack).</summary>
        Required = 1,
        /// <summary>The Extract target.</summary>
        ExtractionTarget = 2
    }

    /// <summary>The single container an instance is in. Exactly one per instance (container exclusivity).</summary>
    public enum ItemLocationKind
    {
        SourceTray = 0,
        Suitcase = 1,
        Staging = 2,
        Nested = 3,
        Destination = 4
    }

    /// <summary>
    /// Suitcase placement of the item's current state: compartment, anchor of the normalized rotated footprint
    /// (ADR-0003) and base layer. Footprint and thickness are derived from the item state, never stored here.
    /// </summary>
    public readonly struct Placement : IEquatable<Placement>
    {
        public string Compartment { get; }
        public Cell Anchor { get; }
        public int Layer { get; }
        public Rotation Rotation { get; }

        public Placement(string compartment, Cell anchor, int layer, Rotation rotation)
        {
            Compartment = compartment ?? throw new ArgumentNullException(nameof(compartment));
            Anchor = anchor;
            Layer = layer;
            Rotation = rotation;
        }

        public bool Equals(Placement other) =>
            string.Equals(Compartment, other.Compartment, StringComparison.Ordinal)
            && Anchor == other.Anchor && Layer == other.Layer && Rotation == other.Rotation;

        public override bool Equals(object obj) => obj is Placement other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((((Compartment?.GetHashCode() ?? 0) * 397) ^ Anchor.GetHashCode()) * 397 ^ Layer) * 397 ^ (int)Rotation);

        public override string ToString() => $"{Compartment}({Anchor.X},{Anchor.Y}) z{Layer} r{(int)Rotation}";
    }

    /// <summary>Where an instance currently is. Payload depends on <see cref="Kind"/>.</summary>
    public readonly struct ItemLocation : IEquatable<ItemLocation>
    {
        public ItemLocationKind Kind { get; }
        /// <summary>Only meaningful for <see cref="ItemLocationKind.Suitcase"/>.</summary>
        public Placement Placement { get; }
        /// <summary>Parent instance id (Nested) or destination id (Destination); null otherwise.</summary>
        public string TargetId { get; }

        private ItemLocation(ItemLocationKind kind, Placement placement, string targetId)
        {
            Kind = kind;
            Placement = placement;
            TargetId = targetId;
        }

        public static ItemLocation SourceTray => new ItemLocation(ItemLocationKind.SourceTray, default, null);
        public static ItemLocation Staging => new ItemLocation(ItemLocationKind.Staging, default, null);
        public static ItemLocation InSuitcase(Placement placement) => new ItemLocation(ItemLocationKind.Suitcase, placement, null);
        public static ItemLocation NestedIn(string parentInstanceId) =>
            new ItemLocation(ItemLocationKind.Nested, default, parentInstanceId ?? throw new ArgumentNullException(nameof(parentInstanceId)));
        public static ItemLocation InDestination(string destinationId) =>
            new ItemLocation(ItemLocationKind.Destination, default, destinationId ?? throw new ArgumentNullException(nameof(destinationId)));

        public bool Equals(ItemLocation other) =>
            Kind == other.Kind && Placement.Equals(other.Placement) && string.Equals(TargetId, other.TargetId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is ItemLocation other && Equals(other);

        public override int GetHashCode() => unchecked(((int)Kind * 397 ^ Placement.GetHashCode()) * 397 ^ (TargetId?.GetHashCode() ?? 0));

        public override string ToString() =>
            Kind == ItemLocationKind.Suitcase ? "Suitcase " + Placement : TargetId == null ? Kind.ToString() : $"{Kind} {TargetId}";
    }

    /// <summary>
    /// Solver equivalence descriptor (ADR-0006: definition + state + tags + objective role). Instance identity is
    /// deliberately excluded; location is not part of it. No mutable per-instance properties exist in the contract
    /// beyond the current state, so none are represented.
    /// </summary>
    public readonly struct ItemEquivalenceKey : IEquatable<ItemEquivalenceKey>
    {
        public ItemSpec Definition { get; }
        public string StateId { get; }
        public ObjectiveRole Role { get; }
        public IReadOnlyList<string> Tags => Definition.Tags;

        public ItemEquivalenceKey(ItemSpec definition, string stateId, ObjectiveRole role)
        {
            Definition = definition;
            StateId = stateId;
            Role = role;
        }

        public bool Equals(ItemEquivalenceKey other) =>
            Equals(Definition, other.Definition) && string.Equals(StateId, other.StateId, StringComparison.Ordinal) && Role == other.Role;

        public override bool Equals(object obj) => obj is ItemEquivalenceKey other && Equals(other);

        public override int GetHashCode() =>
            unchecked(((Definition?.GetHashCode() ?? 0) * 397 ^ (StateId?.GetHashCode() ?? 0)) * 397 ^ (int)Role);
    }

    /// <summary>One item instance in a puzzle state: identity, authored definition, current state, role and location.</summary>
    public sealed class PuzzleItem
    {
        public string InstanceId { get; }
        public ItemSpec Definition { get; }
        public string StateId { get; }
        public ObjectiveRole Role { get; }
        public ItemLocation Location { get; }

        public ItemStateSpec State { get; }

        public PuzzleItem(string instanceId, ItemSpec definition, string stateId, ObjectiveRole role, ItemLocation location)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("MissingInstanceId", nameof(instanceId));
            if (definition == null)
                throw new ArgumentException("MissingDefinition: " + instanceId, nameof(definition));
            if (!definition.TryGetState(stateId, out var state))
                throw new ArgumentException($"UnknownState: {instanceId}.{stateId}", nameof(stateId));
            if (!Enum.IsDefined(typeof(ObjectiveRole), role))
                throw new ArgumentException("InvalidObjectiveRole: " + instanceId, nameof(role));
            if (!Enum.IsDefined(typeof(ItemLocationKind), location.Kind))
                throw new ArgumentException("InvalidLocation: " + instanceId, nameof(location));
            if (location.Kind == ItemLocationKind.Suitcase)
            {
                if (location.Placement.Compartment == null)
                    throw new ArgumentException("InvalidLocation: " + instanceId, nameof(location));
                if (!state.AllowsRotation(location.Placement.Rotation))
                    throw new ArgumentException($"RotationNotAllowed: {instanceId} {location.Placement.Rotation}", nameof(location));
            }

            InstanceId = instanceId;
            Definition = definition;
            StateId = stateId;
            Role = role;
            Location = location;
            State = state;
        }

        public ItemEquivalenceKey EquivalenceKey => new ItemEquivalenceKey(Definition, StateId, Role);

        public PuzzleItem With(ItemLocation location) => new PuzzleItem(InstanceId, Definition, StateId, Role, location);

        public PuzzleItem With(string stateId, ItemLocation location) => new PuzzleItem(InstanceId, Definition, stateId, Role, location);
    }
}
