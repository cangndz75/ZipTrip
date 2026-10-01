using System;
using System.Collections.Generic;

namespace ZipTrip.Domain.Solver
{
    public sealed class PackSolverResult
    {
        public const int HeuristicVersion = 1;
        public const int SolutionCountCap = 100;

        public bool Solvable => CappedSolutionCount > 0;
        public int CappedSolutionCount { get; }
        public bool SolutionCountCapped => CappedSolutionCount == SolutionCountCap;
        public string SolutionDensity { get; }
        public long NodeExpansionCount { get; }
        public long NormalizedSearchNumerator => NodeExpansionCount;
        public int NormalizedSearchDenominator { get; }
        public long BacktrackCount { get; }
        public long? FirstSolutionBacktracks { get; }
        public int? ForcedPlacementCount { get; }
        public int? FirstSolutionPlacementCount { get; }
        public IReadOnlyList<PlacedItem> FirstSolution { get; }

        internal PackSolverResult(int solutionCount, long nodeExpansionCount, int initialInventoryCount,
            long backtrackCount, long? firstSolutionBacktracks, int? forcedPlacementCount,
            IReadOnlyList<PlacedItem> firstSolution)
        {
            CappedSolutionCount = solutionCount;
            SolutionDensity = solutionCount == 0 ? "0" : solutionCount == 1 ? "1" :
                solutionCount <= 10 ? "2_10" : solutionCount < SolutionCountCap ? "11_99" : "100_PLUS";
            NodeExpansionCount = nodeExpansionCount;
            NormalizedSearchDenominator = initialInventoryCount;
            BacktrackCount = backtrackCount;
            FirstSolutionBacktracks = firstSolutionBacktracks;
            ForcedPlacementCount = forcedPlacementCount;
            FirstSolutionPlacementCount = firstSolutionBacktracks.HasValue ? initialInventoryCount : (int?)null;
            FirstSolution = firstSolution;
        }
    }

    public static class PackSolver
    {
        public static PackSolverResult Solve(LevelDefinition level)
        {
            if (level == null)
                throw new ArgumentNullException(nameof(level));
            if (!StringComparer.Ordinal.Equals(level.Grammar, "pack"))
                throw new ArgumentException("PackSolverRequiresPackGrammar", nameof(level));
            if (level.Inventory.Count == 0)
                throw new ArgumentException("EmptyInventory", nameof(level));

            var search = new Search(level);
            search.Run();
            return search.Result();
        }

        private sealed class Search
        {
            private readonly LevelDefinition _level;
            private readonly List<TrayItem> _initialInventory;
            private readonly List<Cell> _initialOccupied;
            private readonly List<Decision> _path = new List<Decision>();
            private IReadOnlyList<PlacedItem> _firstSolution = Array.Empty<PlacedItem>();
            private int _solutionCount;
            private long _nodeExpansionCount;
            private long _backtrackCount;
            private long? _firstSolutionBacktracks;
            private int? _forcedPlacementCount;

            public Search(LevelDefinition level)
            {
                _level = level;
                _initialInventory = new List<TrayItem>(level.Inventory);
                _initialOccupied = new List<Cell>(level.InitialState.Occupancy);
            }

            public void Run() => Explore(_initialInventory, _initialOccupied);

            public PackSolverResult Result() => new PackSolverResult(_solutionCount,
                _nodeExpansionCount, _initialInventory.Count, _backtrackCount,
                _firstSolutionBacktracks, _forcedPlacementCount, _firstSolution);

            private void Explore(List<TrayItem> remaining, List<Cell> occupied)
            {
                if (_solutionCount == PackSolverResult.SolutionCountCap)
                    return;
                if (remaining.Count == 0)
                {
                    if (!TargetsPlaced())
                        return;
                    _solutionCount++;
                    if (_solutionCount == 1)
                    {
                        var first = new List<PlacedItem>();
                        var forced = 0;
                        foreach (var decision in _path)
                        {
                            first.Add(decision.Placement);
                            if (decision.CandidateCount == 1)
                                forced++;
                        }
                        _firstSolution = first.AsReadOnly();
                        _firstSolutionBacktracks = _backtrackCount;
                        _forcedPlacementCount = forced;
                    }
                    return;
                }

                _nodeExpansionCount++;
                var board = new PlacementBoard(_level.InitialState.Container, occupied);
                TrayItem selected = default;
                List<PlacedItem> selectedCandidates = null;
                var selectedArea = -1;
                foreach (var trayItem in remaining)
                {
                    var item = _level.Items[trayItem.ItemId];
                    var candidates = Candidates(board, item, trayItem.ShapeState);
                    var area = item.ShapeStates[trayItem.ShapeState].CellCount;
                    if (selectedCandidates == null || candidates.Count < selectedCandidates.Count ||
                        (candidates.Count == selectedCandidates.Count &&
                         (area > selectedArea || (area == selectedArea &&
                          StringComparer.Ordinal.Compare(trayItem.ItemId, selected.ItemId) < 0))))
                    {
                        selected = trayItem;
                        selectedCandidates = candidates;
                        selectedArea = area;
                    }
                }

                var next = new List<TrayItem>();
                foreach (var trayItem in remaining)
                    if (!StringComparer.Ordinal.Equals(trayItem.ItemId, selected.ItemId))
                        next.Add(trayItem);

                foreach (var placement in selectedCandidates)
                {
                    var nextOccupied = new List<Cell>(occupied);
                    nextOccupied.AddRange(placement.OccupiedCells);
                    _path.Add(new Decision(placement, selectedCandidates.Count));
                    var countBefore = _solutionCount;
                    Explore(next, nextOccupied);
                    _path.RemoveAt(_path.Count - 1);
                    if (_solutionCount == PackSolverResult.SolutionCountCap)
                        return;
                    if (_solutionCount == countBefore)
                        _backtrackCount++;
                }
            }

            private bool TargetsPlaced()
            {
                var placed = new HashSet<string>(StringComparer.Ordinal);
                foreach (var placement in _level.Preplaced)
                    placed.Add(placement.ItemId);
                foreach (var decision in _path)
                    placed.Add(decision.Placement.ItemId);
                foreach (var target in _level.Targets)
                    if (!placed.Contains(target))
                        return false;
                return true;
            }

            private static List<PlacedItem> Candidates(PlacementBoard board, ItemDefinition item,
                string shapeState)
            {
                var candidates = new List<PlacedItem>();
                var rotations = new List<Rotation>(item.AllowedRotations);
                rotations.Sort();
                var uniqueShapes = new List<ItemShape>();
                foreach (var rotation in rotations)
                {
                    var shape = item.GetRotatedShape(shapeState, rotation).Shape;
                    var duplicate = false;
                    foreach (var earlier in uniqueShapes)
                    {
                        if (SameShape(earlier, shape))
                        {
                            duplicate = true;
                            break;
                        }
                    }
                    if (duplicate)
                        continue;
                    uniqueShapes.Add(shape);

                    for (var y = 0; y < GridSize.Height; y++)
                    {
                        for (var x = 0; x < GridSize.Width; x++)
                        {
                            var anchor = new Cell(x, y);
                            if (PlacementValidator.Validate(board, item, anchor, rotation,
                                shapeState).IsValid)
                                candidates.Add(new PlacedItem(item, anchor, rotation, shapeState));
                        }
                    }
                }
                return candidates;
            }

            private static bool SameShape(ItemShape first, ItemShape second)
            {
                if (first.CellCount != second.CellCount)
                    return false;
                for (var i = 0; i < first.CellCount; i++)
                    if (first.OccupiedCells[i] != second.OccupiedCells[i])
                        return false;
                return true;
            }

            private readonly struct Decision
            {
                public PlacedItem Placement { get; }
                public int CandidateCount { get; }

                public Decision(PlacedItem placement, int candidateCount)
                {
                    Placement = placement;
                    CandidateCount = candidateCount;
                }
            }
        }
    }
}
