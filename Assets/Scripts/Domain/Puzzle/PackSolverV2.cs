using System;
using System.Collections.Generic;
using System.Text;
using ZipTrip.Domain.Items;

namespace ZipTrip.Domain.Puzzle
{
    public enum PackSolveStatus
    {
        /// <summary>At least one complete final state was found within the ZT-037 search scope.</summary>
        Solvable = 0,
        /// <summary>No solution was found while holding authored pre-placed suitcase items fixed (Source Tray items
        /// only, no Nest planning). Not proof that no player-reachable solution exists after rearranging those items.</summary>
        Unsolvable = 1,
        /// <summary>Only Pack is supported; Extract / Repack planning belongs to ZT-046.</summary>
        UnsupportedProfile = 2,
        /// <summary>The initial state is outside the ZT-037 search scope; see <see cref="PackSolveResult.UnsupportedReason"/>.</summary>
        UnsupportedInitialState = 3
    }

    public enum PackSolveUnsupportedReason
    {
        None = 0,
        /// <summary>The initial state has an item in staging. Only Source Tray items are decision variables, and
        /// completion needs staging empty, so staged items would require temporal planning.</summary>
        InitialStagingNotSupported = 1
    }

    public enum RuleCoupling
    {
        /// <summary>Removing the rule admits at least one final state that the rule rejects.</summary>
        Coupled = 0,
        /// <summary>The complete rule-free search found no final state that the rule rejects.</summary>
        Dead = 1,
        /// <summary>The rule-free search hit the cap before any rule-violating solution was seen.</summary>
        Inconclusive = 2
    }

    public sealed class RuleCouplingResult
    {
        public string RuleId { get; }
        public RuleCoupling Coupling { get; }
        public int SolutionsWithoutRule { get; }
        public bool SolutionsWithoutRuleCapped { get; }

        internal RuleCouplingResult(string ruleId, RuleCoupling coupling, int solutions, bool capped)
        {
            RuleId = ruleId;
            Coupling = coupling;
            SolutionsWithoutRule = solutions;
            SolutionsWithoutRuleCapped = capped;
        }
    }

    /// <summary>A representative solution: player moves replayable through PuzzleSession, plus the final state.</summary>
    public sealed class PackSolution
    {
        public IReadOnlyList<PuzzleMove> Moves { get; }
        public PuzzleState FinalState { get; }
        /// <summary>Solver-only layout identity (symmetric rotations merged, instances kept distinct).</summary>
        public string EquivalenceKey { get; }

        internal PackSolution(IReadOnlyList<PuzzleMove> moves, PuzzleState finalState, string key)
        {
            Moves = moves;
            FinalState = finalState;
            EquivalenceKey = key;
        }
    }

    public sealed class PackSolveMetrics
    {
        /// <summary>Search decisions visited (place or defer), all searches of this solve.</summary>
        public long NodesVisited { get; internal set; }
        /// <summary>PuzzleTransitions.Apply calls made to generate candidates.</summary>
        public long CandidatesTested { get; internal set; }
        /// <summary>Leaves or forced placements that did not lead to a solution.</summary>
        public long DeadEnds { get; internal set; }
    }

    /// <summary>
    /// Authoring result. Solvable / Unsolvable, the solution count, the greedy result and rule coupling are all
    /// conditional on the ZT-037 search scope: fixed authored suitcase layout (<see cref="PreplacedItemsHeldFixed"/>),
    /// Source Tray decision items, no Nest planning.
    /// </summary>
    public sealed class PackSolveResult
    {
        public const int SolutionCountCap = 50;

        public PackSolveStatus Status { get; }
        /// <summary>Why the level is outside the search scope (<see cref="PackSolveStatus.UnsupportedInitialState"/>); None otherwise.</summary>
        public PackSolveUnsupportedReason UnsupportedReason { get; }
        public PackSolution Solution { get; }
        /// <summary>Distinct solutions found, at most <see cref="SolutionCountCap"/>.</summary>
        public int SolutionCount { get; }
        /// <summary>True when more than the cap exist; false means <see cref="SolutionCount"/> is exact.</summary>
        public bool SolutionCountCapped { get; }
        public bool GreedySolvable { get; }
        public IReadOnlyList<RuleCouplingResult> RuleCouplings { get; }
        public bool PreplacedItemsHeldFixed { get; }
        public int PreplacedItemCount { get; }
        public PackSolveMetrics Metrics { get; }

        internal PackSolveResult(PackSolveStatus status, PackSolution solution, int count, bool capped, bool greedy,
            IReadOnlyList<RuleCouplingResult> couplings, int preplaced, PackSolveMetrics metrics,
            PackSolveUnsupportedReason unsupportedReason = PackSolveUnsupportedReason.None)
        {
            Status = status;
            UnsupportedReason = unsupportedReason;
            Solution = solution;
            SolutionCount = count;
            SolutionCountCapped = capped;
            GreedySolvable = greedy;
            RuleCouplings = couplings;
            PreplacedItemCount = preplaced;
            PreplacedItemsHeldFixed = preplaced > 0;
            Metrics = metrics;
        }
    }

    /// <summary>
    /// Offline Pack solver (ADR-0006 solver table, ZT-037). Deterministic backtracking over Source Tray items only;
    /// items in the suitcase at the start are fixed context (Option A). Nest is not searched. An initial state with
    /// staged items is reported as UnsupportedInitialState without searching.
    ///
    /// Every candidate is a real player move run through <see cref="PuzzleTransitions.Apply"/> (layer from
    /// LayerResolver, legality from BoardInvariants), and a solution is a state where CompletionEvaluator reports
    /// complete. Because players cannot choose layers, the search is phased bottom-up: in phase L each variable either
    /// takes a placement that resolves to layer L or defers to a later phase (optional Source Tray items may also stay
    /// in the tray). The search therefore enumerates final layouts within the ZT-037 Pack search scope (fixed authored
    /// suitcase layout, Source Tray decision items, no Nest planning) exactly once each, in an order a player could replay.
    ///
    /// Variable order (static, computed on the initial state): fewest legal placements first, then larger default
    /// footprint, then instance id. Candidate order: selectable state id, unique rotation, compartment id, y, x.
    /// Greedy heuristic: the same orders, always the first available option (first candidate, otherwise defer), never
    /// backtracking.
    /// </summary>
    public static class PackSolverV2
    {
        public static PackSolveResult Solve(PuzzleLevel level)
        {
            if (level == null)
                throw new ArgumentNullException(nameof(level));
            var metrics = new PackSolveMetrics();
            var preplaced = 0;
            foreach (var item in level.InitialState.Items)
                if (CompletionEvaluator.IsInSuitcase(level.InitialState, item.InstanceId))
                    preplaced++;

            if (level.Objective.Profile != ObjectiveProfile.Pack)
                return new PackSolveResult(PackSolveStatus.UnsupportedProfile, null, 0, false, false,
                    Array.Empty<RuleCouplingResult>(), preplaced, metrics);
            if (level.InitialState.GetItems(ItemLocationKind.Staging).Count > 0)
                return new PackSolveResult(PackSolveStatus.UnsupportedInitialState, null, 0, false, false,
                    Array.Empty<RuleCouplingResult>(), preplaced, metrics, PackSolveUnsupportedReason.InitialStagingNotSupported);

            var variables = OrderVariables(level, metrics);

            var baseline = new Search(level, variables, level.Rules, null, false, metrics);
            baseline.Run();
            var greedy = new Search(level, variables, level.Rules, null, true, metrics);
            greedy.Run();

            var couplings = new List<RuleCouplingResult>();
            foreach (var rule in level.Rules.Rules)
            {
                var others = new List<PuzzleRule>();
                foreach (var candidate in level.Rules.Rules)
                    if (!ReferenceEquals(candidate, rule))
                        others.Add(candidate);
                var without = new Search(level, variables, new RuleSet(level.Spec.Board, others), level.Rules, false, metrics);
                without.Run();
                var coupling = without.FoundRuleViolation ? RuleCoupling.Coupled
                    : without.Capped ? RuleCoupling.Inconclusive : RuleCoupling.Dead;
                couplings.Add(new RuleCouplingResult(rule.Id, coupling, without.Count, without.Capped));
            }

            return new PackSolveResult(baseline.Count > 0 ? PackSolveStatus.Solvable : PackSolveStatus.Unsolvable,
                baseline.First, baseline.Count, baseline.Capped, greedy.Count > 0, couplings.AsReadOnly(), preplaced, metrics);
        }

        /// <summary>Allowed rotations of a state with geometrically duplicate footprints removed (first rotation kept).</summary>
        public static IReadOnlyList<Rotation> UniqueRotations(ItemStateSpec state)
        {
            var result = new List<Rotation>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rotation in state.AllowedRotations)
            {
                state.TryGetFootprint(rotation, out var footprint);
                if (seen.Add(CellsKey(footprint)))
                    result.Add(rotation);
            }
            return result.AsReadOnly();
        }

        private static List<string> OrderVariables(PuzzleLevel level, PackSolveMetrics metrics)
        {
            var entries = new List<(string Id, int Candidates, int Area)>();
            foreach (var item in level.InitialState.Items)
            {
                var kind = item.Location.Kind;
                if (kind != ItemLocationKind.SourceTray)
                    continue;
                var count = 0;
                for (var layer = 0; layer < MaxLayers(level); layer++)
                    count += Candidates(level.InitialState, item.InstanceId, layer, metrics).Count;
                entries.Add((item.InstanceId, count, item.Definition.DefaultState.Footprint.CellCount));
            }
            entries.Sort((a, b) =>
                a.Candidates != b.Candidates ? a.Candidates.CompareTo(b.Candidates)
                : a.Area != b.Area ? b.Area.CompareTo(a.Area)
                : string.CompareOrdinal(a.Id, b.Id));
            var ids = new List<string>();
            foreach (var entry in entries)
                ids.Add(entry.Id);
            return ids;
        }

        private static int MaxLayers(PuzzleLevel level)
        {
            var max = 0;
            foreach (var compartment in level.Spec.Board.Compartments)
                max = Math.Max(max, compartment.Layers);
            return max;
        }

        private static List<(PuzzleMove Move, PuzzleState State)> Candidates(PuzzleState state, string instanceId, int layer,
            PackSolveMetrics metrics)
        {
            var result = new List<(PuzzleMove, PuzzleState)>();
            state.TryGetItem(instanceId, out var item);
            foreach (var stateId in PuzzleTransitions.GetSelectableStates(item))
            {
                item.Definition.TryGetState(stateId, out var itemState);
                foreach (var rotation in UniqueRotations(itemState))
                {
                    itemState.TryGetFootprint(rotation, out var footprint);
                    var width = 0;
                    var height = 0;
                    foreach (var cell in footprint.OccupiedCells)
                    {
                        width = Math.Max(width, cell.X + 1);
                        height = Math.Max(height, cell.Y + 1);
                    }
                    foreach (var compartment in state.Spec.Board.Compartments)
                    {
                        if (layer >= compartment.Layers)
                            continue;
                        for (var y = 0; y + height <= compartment.Height; y++)
                            for (var x = 0; x + width <= compartment.Width; x++)
                            {
                                var move = PuzzleMove.PlaceInSuitcase(instanceId, compartment.Id, new Cell(x, y), rotation, stateId);
                                metrics.CandidatesTested++;
                                var applied = PuzzleTransitions.Apply(state, move);
                                if (!applied.IsAccepted)
                                    continue;
                                applied.State.TryGetItem(instanceId, out var placed);
                                if (placed.Location.Placement.Layer == layer)
                                    result.Add((move, applied.State));
                            }
                    }
                }
            }
            return result;
        }

        private static string CellsKey(ItemShape shape)
        {
            var key = new StringBuilder();
            foreach (var cell in shape.OccupiedCells)
                key.Append(cell.X).Append(',').Append(cell.Y).Append(';');
            return key.ToString();
        }

        private sealed class Search
        {
            private readonly PuzzleLevel _level;
            private readonly List<string> _variables;
            private readonly RuleSet _rules;
            private readonly RuleSet _fullRules;
            private readonly bool _greedy;
            private readonly PackSolveMetrics _metrics;
            private readonly int _phases;
            private readonly HashSet<string> _required;
            private readonly HashSet<string> _found = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<PuzzleMove> _path = new List<PuzzleMove>();
            private bool _stop;

            public int Count => Math.Min(_found.Count, PackSolveResult.SolutionCountCap);
            public bool Capped => _found.Count > PackSolveResult.SolutionCountCap;
            public PackSolution First { get; private set; }
            /// <summary>Rule-coupling search only: a found solution is incomplete under the full rule set.</summary>
            public bool FoundRuleViolation { get; private set; }

            public Search(PuzzleLevel level, List<string> variables, RuleSet rules, RuleSet fullRules, bool greedy, PackSolveMetrics metrics)
            {
                _level = level;
                _variables = variables;
                _rules = rules;
                _fullRules = fullRules;
                _greedy = greedy;
                _metrics = metrics;
                _phases = MaxLayers(level);
                _required = new HashSet<string>(level.Objective.RequiredInstanceIds, StringComparer.Ordinal);
            }

            public void Run() => Explore(_level.InitialState, 0, 0);

            private void Explore(PuzzleState state, int phase, int index)
            {
                if (_stop)
                    return;
                if (index == _variables.Count)
                {
                    if (phase + 1 < _phases)
                        Explore(state, phase + 1, 0);
                    else
                        Leaf(state);
                    return;
                }

                var id = _variables[index];
                state.TryGetItem(id, out var item);
                if (item.Location.Kind == ItemLocationKind.Suitcase)
                {
                    Explore(state, phase, index + 1);
                    return;
                }

                var lastPhase = phase + 1 >= _phases;
                var canDefer = !lastPhase || !_required.Contains(id);
                var candidates = Candidates(state, id, phase, _metrics);
                if (candidates.Count == 0 && !canDefer)
                {
                    _metrics.DeadEnds++;
                    return;
                }

                foreach (var candidate in candidates)
                {
                    _metrics.NodesVisited++;
                    _path.Add(candidate.Move);
                    Explore(candidate.State, phase, index + 1);
                    _path.RemoveAt(_path.Count - 1);
                    if (_stop || _greedy)
                        return;
                }
                if (canDefer)
                {
                    _metrics.NodesVisited++;
                    Explore(state, phase, index + 1);
                }
            }

            private void Leaf(PuzzleState state)
            {
                if (!CompletionEvaluator.Evaluate(state, _level.Objective, _rules).IsComplete)
                {
                    _metrics.DeadEnds++;
                    if (_greedy)
                        _stop = true;
                    return;
                }
                var key = Key(state);
                if (!_found.Add(key))
                    return;
                if (First == null)
                    First = new PackSolution(new List<PuzzleMove>(_path).AsReadOnly(), state, key);
                if (_fullRules != null && !CompletionEvaluator.Evaluate(state, _level.Objective, _fullRules).IsComplete)
                    FoundRuleViolation = true;
                if (_greedy || _found.Count > PackSolveResult.SolutionCountCap)
                    _stop = true;
            }

            // Final-layout identity for the decision variables: instance, state, location, layer and occupied cells
            // (so symmetric rotations with identical geometry collapse; distinct instances never do).
            private string Key(PuzzleState state)
            {
                var ids = new List<string>(_variables);
                ids.Sort(StringComparer.Ordinal);
                var key = new StringBuilder();
                foreach (var id in ids)
                {
                    state.TryGetItem(id, out var item);
                    key.Append(id).Append('|').Append(item.StateId).Append('|');
                    if (item.Location.Kind == ItemLocationKind.Suitcase)
                    {
                        var placement = item.Location.Placement;
                        item.State.TryGetFootprint(placement.Rotation, out var footprint);
                        key.Append(placement.Compartment).Append('@').Append(placement.Anchor.X).Append(',')
                            .Append(placement.Anchor.Y).Append(",z").Append(placement.Layer).Append(':').Append(CellsKey(footprint));
                    }
                    else
                    {
                        key.Append(item.Location.Kind);
                    }
                    key.Append('\n');
                }
                return key.ToString();
            }
        }
    }
}
