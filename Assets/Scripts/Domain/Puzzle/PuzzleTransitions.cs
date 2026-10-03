using System;
using System.Collections.Generic;

namespace ZipTrip.Domain.Puzzle
{
    public enum PuzzleMoveKind
    {
        /// <summary>Into the suitcase: from Source Tray, staging, another suitcase placement or a nest.</summary>
        PlaceInSuitcase = 0,
        /// <summary>From the suitcase or a nest into a staging slot.</summary>
        MoveToStaging = 1,
        /// <summary>Into a compatible accessible parent.</summary>
        NestInto = 2,
        /// <summary>Into an extraction destination.</summary>
        MoveToDestination = 3
    }

    /// <summary>
    /// One committed relocation of one item (ADR-0006 Decision 18). Rotation and the authored state chosen before
    /// commit are part of the relocation; the layer is never chosen by the caller (Decision 12).
    /// </summary>
    public sealed class PuzzleMove
    {
        public PuzzleMoveKind Kind { get; }
        public string InstanceId { get; }
        /// <summary>PlaceInSuitcase: requested authored state; null keeps the current state.</summary>
        public string StateId { get; }
        public string CompartmentId { get; }
        public Cell Anchor { get; }
        public Rotation Rotation { get; }
        /// <summary>NestInto: parent instance id. MoveToDestination: destination id.</summary>
        public string TargetId { get; }

        private PuzzleMove(PuzzleMoveKind kind, string instanceId, string stateId, string compartmentId, Cell anchor,
            Rotation rotation, string targetId)
        {
            Kind = kind;
            InstanceId = instanceId;
            StateId = stateId;
            CompartmentId = compartmentId;
            Anchor = anchor;
            Rotation = rotation;
            TargetId = targetId;
        }

        public static PuzzleMove PlaceInSuitcase(string instanceId, string compartmentId, Cell anchor, Rotation rotation,
            string stateId = null) =>
            new PuzzleMove(PuzzleMoveKind.PlaceInSuitcase, instanceId, stateId, compartmentId, anchor, rotation, null);

        public static PuzzleMove MoveToStaging(string instanceId) =>
            new PuzzleMove(PuzzleMoveKind.MoveToStaging, instanceId, null, null, default, default, null);

        public static PuzzleMove NestInto(string instanceId, string parentInstanceId) =>
            new PuzzleMove(PuzzleMoveKind.NestInto, instanceId, null, null, default, default, parentInstanceId);

        public static PuzzleMove MoveToDestination(string instanceId, string destinationId) =>
            new PuzzleMove(PuzzleMoveKind.MoveToDestination, instanceId, null, null, default, default, destinationId);

        public override string ToString() => $"{Kind} {InstanceId} {StateId} {CompartmentId}({Anchor.X},{Anchor.Y}) r{(int)Rotation} {TargetId}";
    }

    public enum MoveRejection
    {
        None = 0,
        UnknownItem = 1,
        /// <summary>The item is blocked or its parent is not accessible.</summary>
        NotAccessible = 2,
        /// <summary>The item is in an extraction destination; normal moves cannot take it back.</summary>
        InDestination = 3,
        /// <summary>The source/target pair is not one of the Decision 18 moves (e.g. Source Tray to staging).</summary>
        UnsupportedRoute = 4,
        /// <summary>Fold/Compress state may only be chosen for an item in the Source Tray or staging.</summary>
        StateChangeNotAllowed = 5,
        /// <summary>The requested state is not reachable through authored Fold/Compress transitions.</summary>
        StateNotReachable = 6,
        RotationNotAllowed = 7,
        UnknownCompartment = 8,
        /// <summary>No layer of the drop candidate satisfies the board invariants.</summary>
        NoLegalLayer = 9,
        /// <summary>The resulting state violates a board invariant (capacity, compatibility, acceptance).</summary>
        InvariantViolation = 10,
        UnknownTarget = 11,
        /// <summary>The nest parent is not accessible in the suitcase.</summary>
        ParentNotAccessible = 12,
        /// <summary>The item would be nested inside itself or one of its own children.</summary>
        NestCycle = 13,
        /// <summary>The relocation would not change the state.</summary>
        NoChange = 14
    }

    /// <summary>Outcome of applying a move. A rejected move returns the original state unchanged.</summary>
    public sealed class MoveResult
    {
        public bool IsAccepted => Rejection == MoveRejection.None;
        public MoveRejection Rejection { get; }
        /// <summary>The new state when accepted; the unchanged original when rejected.</summary>
        public PuzzleState State { get; }
        /// <summary>Invariant diagnostics for NoLegalLayer / InvariantViolation; null otherwise.</summary>
        public InvariantReport Report { get; }
        /// <summary>The moved item followed by the nested descendants it carried, in id order.</summary>
        public IReadOnlyList<string> MovedInstanceIds { get; }

        private MoveResult(MoveRejection rejection, PuzzleState state, InvariantReport report, IReadOnlyList<string> moved)
        {
            Rejection = rejection;
            State = state;
            Report = report;
            MovedInstanceIds = moved;
        }

        internal static MoveResult Accepted(PuzzleState state, IReadOnlyList<string> moved) =>
            new MoveResult(MoveRejection.None, state, null, moved);

        internal static MoveResult Rejected(PuzzleState original, MoveRejection reason, InvariantReport report = null) =>
            new MoveResult(reason, original, report, Array.Empty<string>());
    }

    /// <summary>
    /// Pure transition engine (ADR-0006 Decisions 8, 12, 15, 17, 18). One accepted move is one committed
    /// relocation; legality comes only from <see cref="AccessQueries"/>, <see cref="LayerResolver"/> and
    /// <see cref="BoardInvariants"/>. No move counter, history or standalone rotate / fold commands.
    /// </summary>
    public static class PuzzleTransitions
    {
        public static MoveResult Apply(PuzzleState state, PuzzleMove move)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (move == null || !state.TryGetItem(move.InstanceId, out var item))
                return MoveResult.Rejected(state, MoveRejection.UnknownItem);

            var access = AccessQueries.GetAccess(state, item.InstanceId);
            if (access == ItemAccess.Terminal)
                return MoveResult.Rejected(state, MoveRejection.InDestination);
            if (access != ItemAccess.Accessible && access != ItemAccess.External)
                return MoveResult.Rejected(state, MoveRejection.NotAccessible);

            var from = item.Location.Kind;
            switch (move.Kind)
            {
                case PuzzleMoveKind.PlaceInSuitcase:
                    return Place(state, item, move);

                case PuzzleMoveKind.MoveToStaging:
                    if (from != ItemLocationKind.Suitcase && from != ItemLocationKind.Nested)
                        return MoveResult.Rejected(state, MoveRejection.UnsupportedRoute);
                    return Commit(state, item, item.With(ItemLocation.Staging));

                case PuzzleMoveKind.NestInto:
                    if (from == ItemLocationKind.SourceTray)
                        return MoveResult.Rejected(state, MoveRejection.UnsupportedRoute);
                    if (!state.TryGetItem(move.TargetId, out var parent))
                        return MoveResult.Rejected(state, MoveRejection.UnknownTarget);
                    if (IsSelfOrDescendant(state, parent, item.InstanceId))
                        return MoveResult.Rejected(state, MoveRejection.NestCycle);
                    if (!AccessQueries.IsAccessible(state, parent.InstanceId))
                        return MoveResult.Rejected(state, MoveRejection.ParentNotAccessible);
                    return Commit(state, item, item.With(ItemLocation.NestedIn(parent.InstanceId)));

                case PuzzleMoveKind.MoveToDestination:
                    if (from == ItemLocationKind.SourceTray)
                        return MoveResult.Rejected(state, MoveRejection.UnsupportedRoute);
                    if (!state.Spec.TryGetDestination(move.TargetId, out _))
                        return MoveResult.Rejected(state, MoveRejection.UnknownTarget);
                    return Commit(state, item, item.With(ItemLocation.InDestination(move.TargetId)));

                default:
                    return MoveResult.Rejected(state, MoveRejection.UnsupportedRoute);
            }
        }

        /// <summary>
        /// Authored states the item may be committed in from its current state: the current state plus everything
        /// reachable through authored Fold/Compress transitions. Choosing among them is not a move (Decision 18).
        /// </summary>
        public static IReadOnlyList<string> GetSelectableStates(PuzzleItem item)
        {
            var reached = new SortedSet<string>(StringComparer.Ordinal) { item.StateId };
            var pending = new Stack<string>();
            pending.Push(item.StateId);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var transition in item.Definition.Transitions)
                    if (transition.FromStateId == current && reached.Add(transition.ToStateId))
                        pending.Push(transition.ToStateId);
            }
            return new List<string>(reached).AsReadOnly();
        }

        private static MoveResult Place(PuzzleState state, PuzzleItem item, PuzzleMove move)
        {
            var stateId = move.StateId ?? item.StateId;
            if (stateId != item.StateId)
            {
                // Decision 8: Fold / Compress only outside the suitcase (Source Tray or staging), never in place or nested.
                var from = item.Location.Kind;
                if (from != ItemLocationKind.SourceTray && from != ItemLocationKind.Staging)
                    return MoveResult.Rejected(state, MoveRejection.StateChangeNotAllowed);
                var selectable = GetSelectableStates(item);
                var found = false;
                foreach (var candidate in selectable)
                    found |= candidate == stateId;
                if (!found)
                    return MoveResult.Rejected(state, MoveRejection.StateNotReachable);
            }

            var resolution = LayerResolver.ResolveLowestLegalLayer(state, item.InstanceId, stateId, move.CompartmentId, move.Anchor, move.Rotation);
            switch (resolution.Failure)
            {
                case LayerResolutionFailure.None:
                    break;
                case LayerResolutionFailure.RotationNotAllowed:
                    return MoveResult.Rejected(state, MoveRejection.RotationNotAllowed);
                case LayerResolutionFailure.UnknownCompartment:
                    return MoveResult.Rejected(state, MoveRejection.UnknownCompartment);
                case LayerResolutionFailure.NoLegalLayer:
                    return MoveResult.Rejected(state, MoveRejection.NoLegalLayer, resolution.Attempts[0]);
                default:
                    return MoveResult.Rejected(state, MoveRejection.StateNotReachable);
            }

            if (resolution.Candidate.Equals(state))
                return MoveResult.Rejected(state, MoveRejection.NoChange);
            return MoveResult.Accepted(resolution.Candidate, Carried(resolution.Candidate, item.InstanceId));
        }

        private static MoveResult Commit(PuzzleState state, PuzzleItem item, PuzzleItem moved)
        {
            if (moved.Location.Equals(item.Location))
                return MoveResult.Rejected(state, MoveRejection.NoChange);
            var next = state.With(moved);
            var report = BoardInvariants.Evaluate(next);
            if (!report.IsValid)
                return MoveResult.Rejected(state, MoveRejection.InvariantViolation, report);
            return MoveResult.Accepted(next, Carried(next, item.InstanceId));
        }

        private static bool IsSelfOrDescendant(PuzzleState state, PuzzleItem candidate, string ancestorId)
        {
            var current = candidate;
            while (true)
            {
                if (current.InstanceId == ancestorId)
                    return true;
                if (current.Location.Kind != ItemLocationKind.Nested || !state.TryGetItem(current.Location.TargetId, out current))
                    return false;
            }
        }

        private static IReadOnlyList<string> Carried(PuzzleState state, string instanceId)
        {
            var result = new List<string> { instanceId };
            var descendants = new SortedSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            pending.Push(instanceId);
            while (pending.Count > 0)
                foreach (var child in state.GetChildren(pending.Pop()))
                    if (descendants.Add(child.InstanceId))
                        pending.Push(child.InstanceId);
            result.AddRange(descendants);
            return result.AsReadOnly();
        }
    }
}
