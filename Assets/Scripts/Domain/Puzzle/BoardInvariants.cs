using System;
using System.Collections.Generic;
using ZipTrip.Domain.Board;

namespace ZipTrip.Domain.Puzzle
{
    /// <summary>Board invariant kinds (ADR-0006 Decision 15, first layer). Enforced at every committed state.</summary>
    public enum InvariantKind
    {
        /// <summary>Physical cell outside the compartment's W x H x L range.</summary>
        OutOfBounds = 0,
        /// <summary>XY column inside the range but not a valid mask cell.</summary>
        MaskedCell = 1,
        /// <summary>Two items occupy the same physical (x, y, z) cell.</summary>
        Overlap = 2,
        /// <summary>A footprint cell above layer 0 has nothing directly below it.</summary>
        Unsupported = 3,
        StagingOverCapacity = 4,
        NestOverCapacity = 5,
        NestIncompatible = 6,
        DestinationOverCapacity = 7,
        DestinationNotAccepted = 8,
        /// <summary>ZT-041: a staged item sits in an out-of-range slot or shares its slot with another item.</summary>
        StagingSlotConflict = 9
    }

    public readonly struct InvariantViolation
    {
        public InvariantKind Kind { get; }
        /// <summary>The offending item; null for container-level violations (staging capacity).</summary>
        public string InstanceId { get; }
        /// <summary>The other item involved (overlap owner, nest parent); null otherwise.</summary>
        public string OtherInstanceId { get; }
        public bool HasCell { get; }
        public PhysicalCell Cell { get; }
        /// <summary>Destination id for destination violations; null otherwise.</summary>
        public string ContainerId { get; }

        internal InvariantViolation(InvariantKind kind, string instanceId, string otherInstanceId = null,
            PhysicalCell? cell = null, string containerId = null)
        {
            Kind = kind;
            InstanceId = instanceId;
            OtherInstanceId = otherInstanceId;
            HasCell = cell.HasValue;
            Cell = cell.GetValueOrDefault();
            ContainerId = containerId;
        }

        public bool Involves(string instanceId) =>
            string.Equals(InstanceId, instanceId, StringComparison.Ordinal)
            || string.Equals(OtherInstanceId, instanceId, StringComparison.Ordinal);

        public override string ToString() =>
            $"{Kind} {InstanceId}{(OtherInstanceId != null ? " vs " + OtherInstanceId : "")}{(HasCell ? " @" + Cell : "")}{(ContainerId != null ? " in " + ContainerId : "")}";
    }

    /// <summary>Structured invariant result; violations in deterministic order.</summary>
    public sealed class InvariantReport
    {
        public IReadOnlyList<InvariantViolation> Violations { get; }
        public bool IsValid => Violations.Count == 0;

        internal InvariantReport(List<InvariantViolation> violations)
        {
            Violations = violations.AsReadOnly();
        }

        /// <summary>Violations that involve one item (e.g. for a placement preview).</summary>
        public IReadOnlyList<InvariantViolation> Involving(string instanceId)
        {
            var result = new List<InvariantViolation>();
            foreach (var violation in Violations)
                if (violation.Involves(instanceId))
                    result.Add(violation);
            return result.AsReadOnly();
        }
    }

    /// <summary>
    /// The single authority for structural legality (ADR-0006 Decisions 11, 12, 15, 16): bounds and mask,
    /// physical volume overlap, full support, staging / nest / destination capacity and acceptance.
    /// Container exclusivity is guaranteed by PuzzleState construction. Puzzle rules are not checked here.
    /// </summary>
    public static class BoardInvariants
    {
        public static InvariantReport Evaluate(PuzzleState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));

            var violations = new List<InvariantViolation>();
            var owners = new Dictionary<PhysicalCell, string>();

            // Items are in ordinal id order, so the first owner of a cell and every violation order are deterministic.
            foreach (var item in state.Items)
            {
                if (item.Location.Kind != ItemLocationKind.Suitcase)
                    continue;
                state.Spec.Board.TryGetCompartment(item.Location.Placement.Compartment, out var compartment);
                foreach (var cell in PuzzleState.GetPhysicalCells(item))
                {
                    if (!compartment.Mask.IsWithinBounds(cell.Column) || !compartment.IsWithinLayers(cell.Z))
                        violations.Add(new InvariantViolation(InvariantKind.OutOfBounds, item.InstanceId, cell: cell));
                    else if (!compartment.IsValidColumn(cell.Column))
                        violations.Add(new InvariantViolation(InvariantKind.MaskedCell, item.InstanceId, cell: cell));

                    if (owners.TryGetValue(cell, out var owner))
                        violations.Add(new InvariantViolation(InvariantKind.Overlap, item.InstanceId, owner, cell));
                    else
                        owners.Add(cell, item.InstanceId);
                }
            }

            foreach (var item in state.Items)
            {
                if (item.Location.Kind != ItemLocationKind.Suitcase || item.Location.Placement.Layer <= 0)
                    continue;
                var placement = item.Location.Placement;
                foreach (var cell in PuzzleState.GetPhysicalCells(item))
                {
                    if (cell.Z != placement.Layer)
                        continue;
                    var below = new PhysicalCell(cell.Compartment, cell.X, cell.Y, cell.Z - 1);
                    if (!owners.ContainsKey(below))
                        violations.Add(new InvariantViolation(InvariantKind.Unsupported, item.InstanceId, cell: cell));
                }
            }

            var staged = state.GetItems(ItemLocationKind.Staging);
            if (staged.Count > state.Spec.StagingCapacity)
                violations.Add(new InvariantViolation(InvariantKind.StagingOverCapacity, null));
            else
            {
                // ZT-041: each staged item owns one distinct in-range slot (ADR-0006 D11).
                var used = new HashSet<int>();
                foreach (var item in staged)
                    if (item.Location.StagingSlot >= state.Spec.StagingCapacity || !used.Add(item.Location.StagingSlot))
                        violations.Add(new InvariantViolation(InvariantKind.StagingSlotConflict, item.InstanceId));
            }

            foreach (var parent in state.Items)
            {
                var children = state.GetChildren(parent.InstanceId);
                if (children.Count == 0)
                    continue;
                if (children.Count > parent.Definition.Nest.Capacity)
                    violations.Add(new InvariantViolation(InvariantKind.NestOverCapacity, parent.InstanceId));
                foreach (var child in children)
                    if (!parent.Definition.Nest.Accepts(child.Definition))
                        violations.Add(new InvariantViolation(InvariantKind.NestIncompatible, child.InstanceId, parent.InstanceId));
            }

            foreach (var destination in state.Spec.Destinations)
            {
                var inside = state.GetDestinationItems(destination.Id);
                if (inside.Count > destination.Capacity)
                    violations.Add(new InvariantViolation(InvariantKind.DestinationOverCapacity, null, containerId: destination.Id));
                foreach (var item in inside)
                    if (!destination.Accepts(item))
                        violations.Add(new InvariantViolation(InvariantKind.DestinationNotAccepted, item.InstanceId, containerId: destination.Id));
            }

            return new InvariantReport(violations);
        }
    }
}
