using System.Collections.Generic;
using ZipTrip.Domain.Board;

namespace ZipTrip.Domain.Puzzle
{
    public enum LayerResolutionFailure
    {
        None = 0,
        UnknownItem = 1,
        UnknownState = 2,
        RotationNotAllowed = 3,
        UnknownCompartment = 4,
        /// <summary>Every layer of the compartment violates a board invariant.</summary>
        NoLegalLayer = 5
    }

    /// <summary>Outcome of resolving a 2D drop candidate to a layer. Never mutates the source state.</summary>
    public sealed class LayerResolution
    {
        public LayerResolutionFailure Failure { get; }
        public bool IsValid => Failure == LayerResolutionFailure.None;
        /// <summary>Resolved base layer; -1 when invalid.</summary>
        public int Layer { get; }
        /// <summary>The resulting state with the item placed at <see cref="Layer"/>; null when invalid.</summary>
        public PuzzleState Candidate { get; }
        /// <summary>Invariant report per attempted layer, index = layer (empty when no layer was attempted).</summary>
        public IReadOnlyList<InvariantReport> Attempts { get; }

        internal LayerResolution(LayerResolutionFailure failure, int layer, PuzzleState candidate, List<InvariantReport> attempts)
        {
            Failure = failure;
            Layer = layer;
            Candidate = candidate;
            Attempts = attempts.AsReadOnly();
        }
    }

    /// <summary>
    /// Automatic lowest-legal-layer resolution (ADR-0006 Decision 12): the player never picks a layer. Layers are
    /// tried bottom-up and the first whose resulting state passes <see cref="BoardInvariants"/> wins, so the
    /// resolver and committed moves share one legality authority. With L = 2 a thickness-2 item can only start at
    /// layer 0 (layer 1 is out of bounds) and layer 1 needs full support.
    /// </summary>
    public static class LayerResolver
    {
        public static LayerResolution ResolveLowestLegalLayer(PuzzleState state, string instanceId, string stateId,
            string compartmentId, Cell anchor, Rotation rotation)
        {
            var attempts = new List<InvariantReport>();
            if (state == null || !state.TryGetItem(instanceId, out var item))
                return new LayerResolution(LayerResolutionFailure.UnknownItem, -1, null, attempts);
            if (!item.Definition.TryGetState(stateId, out var itemState))
                return new LayerResolution(LayerResolutionFailure.UnknownState, -1, null, attempts);
            if (!itemState.AllowsRotation(rotation))
                return new LayerResolution(LayerResolutionFailure.RotationNotAllowed, -1, null, attempts);
            if (compartmentId == null || !state.Spec.Board.TryGetCompartment(compartmentId, out Compartment compartment))
                return new LayerResolution(LayerResolutionFailure.UnknownCompartment, -1, null, attempts);

            for (var layer = 0; layer < compartment.Layers; layer++)
            {
                var placement = new Placement(compartmentId, anchor, layer, rotation);
                var candidate = state.With(item.With(stateId, ItemLocation.InSuitcase(placement)));
                var report = BoardInvariants.Evaluate(candidate);
                attempts.Add(report);
                if (report.IsValid)
                    return new LayerResolution(LayerResolutionFailure.None, layer, candidate, attempts);
            }
            return new LayerResolution(LayerResolutionFailure.NoLegalLayer, -1, null, attempts);
        }
    }
}
