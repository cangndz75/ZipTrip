using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ZipTrip.Domain;
using ZipTrip.Domain.Solver;

namespace ZipTrip.Unity
{
    public sealed class PhaseALevelValidation
    {
        public LevelDefinition Level { get; }
        public PackSolverResult WithoutFold { get; }
        public PackSolverResult WithFold { get; }
        public PackSolverResult RotationZeroOnly { get; }
        public PackSolverResult SweaterLockedOpen { get; }
        public PackSolverResult SweaterOnlyFold { get; }

        internal PhaseALevelValidation(LevelDefinition level, PackSolverResult withoutFold,
            PackSolverResult withFold = null, PackSolverResult rotationZeroOnly = null,
            PackSolverResult sweaterLockedOpen = null, PackSolverResult sweaterOnlyFold = null)
        {
            Level = level;
            WithoutFold = withoutFold;
            WithFold = withFold;
            RotationZeroOnly = rotationZeroOnly;
            SweaterLockedOpen = sweaterLockedOpen;
            SweaterOnlyFold = sweaterOnlyFold;
        }
    }

    /// <summary>Shared Editor and EditMode validation of the approved authored levels.</summary>
    public static class PhaseALevelValidator
    {
        public static IReadOnlyList<PhaseALevelValidation> ValidateAll()
        {
            if (PackSolverResult.HeuristicVersion != 1)
                throw new InvalidOperationException("MetricSnapshotMismatch: heuristic version");
            var results = new List<PhaseALevelValidation>();
            foreach (var id in PhaseALevels.Ids)
                results.Add(ValidateLevel(PhaseALevels.Load(id)));
            return results.AsReadOnly();
        }

        public static PhaseALevelValidation ValidateLevel(LevelDefinition level)
        {
            if (level == null)
                throw new ArgumentNullException(nameof(level));
            var id = level.Id;
            if (id != "L1" && id != "L2" && id != "L3" && id != "L4")
                throw new ArgumentException("UnknownPhaseALevel: " + id, nameof(level));
            if (level.Id != id || level.Grammar != "pack" || level.Preplaced.Count != 0 ||
                level.Targets.Count != 0 || level.Boosters.VacuumCount != 0)
                throw new InvalidOperationException("AuthoredLevelMismatch: " + id);
            var expectedContainer = id == "L1" || id == "L2" ? "backpack_std" : "cabin_std";
            var expectedInventory = id == "L1" ? new[] { "book", "camera", "bottle", "sneaker" } :
                id == "L2" ? new[] { "book", "bottle", "camera", "laptop", "sneaker" } :
                id == "L3" ? new[] { "camera", "laptop", "pants", "sneaker", "sweater" } :
                new[] { "book", "bottle", "camera", "laptop", "scarf", "sneaker", "sweater" };
            if (level.ContainerId != expectedContainer ||
                !level.Inventory.Select(tray => tray.ItemId).SequenceEqual(expectedInventory) ||
                level.Inventory.Any(tray => tray.Rotation != Rotation.Degrees0 || tray.ShapeState != "open"))
                throw new InvalidOperationException("AuthoredLevelMismatch: " + id);
            var withoutFold = PackSolver.Solve(level);
            if (id == "L1")
            {
                RequireArea(level, 17, 31);
                Check(id, "noFold", withoutFold, new MetricSnapshot(100, "100_PLUS", 10, 0, 0, 0,
                    "camera@1,0:0:open;book@3,1:0:open;bottle@0,1:0:open;sneaker@1,2:0:open"));
                return new PhaseALevelValidation(level, withoutFold);
            }
            if (id == "L2")
            {
                RequireArea(level, 29, 31);
                Check(id, "noFold", withoutFold, new MetricSnapshot(12, "11_99", 171, 140, 22, 2,
                    "laptop@0,1:0:open;book@1,5:90:open;camera@3,1:0:open;" +
                    "sneaker@3,3:180:open;bottle@1,0:90:open"));
                var zeroOnly = PackSolver.Solve(WithRestrictions(level, zeroRotations: true));
                Check(id, "rotationZeroOnly", zeroOnly, new MetricSnapshot(0, "0", 31, 30, null, null, ""));
                return new PhaseALevelValidation(level, withoutFold, rotationZeroOnly: zeroOnly);
            }
            if (id == "L3")
            {
                RequireArea(level, 39, 44);
                if (level.Items["pants"].ShapeStates.Count != 1 ||
                    level.Items["sweater"].ShapeStates.Count != 1)
                    throw new InvalidOperationException("FoldMustBeDisabled: L3");
                Check(id, "noFold", withoutFold, new MetricSnapshot(4, "2_10", 303, 286, 4, 1,
                    "sweater@1,0:0:open;pants@4,1:0:open;laptop@0,3:90:open;" +
                    "camera@1,6:0:open;sneaker@3,6:90:open"));
                return new PhaseALevelValidation(level, withoutFold);
            }

            RequireArea(level, 44, 44);
            Check(id, "noFold", withoutFold, new MetricSnapshot(0, "0", 1213, 1212, null, null, ""));
            var withFold = PackSolver.Solve(level, true);
            Check(id, "withFold", withFold, new MetricSnapshot(100, "100_PLUS", 11769, 11417, 39, 2,
                "laptop@1,0:0:open;camera@4,1:0:open;sweater@4,3:0:folded;" +
                "book@1,5:0:open;scarf@0,1:0:open;sneaker@3,5:0:open;bottle@1,4:90:open"));
            var sweaterOpen = PackSolver.Solve(WithRestrictions(level, lockSweaterOpen: true), true);
            Check(id, "sweaterOpen", sweaterOpen, new MetricSnapshot(0, "0", 2859, 2858, null, null, ""));
            var sweaterOnly = PackSolver.Solve(WithRestrictions(level, sweaterOnlyFold: true), true);
            Check(id, "sweaterOnly", sweaterOnly, new MetricSnapshot(100, "100_PLUS", 4244, 3954, 345, 1,
                "scarf@0,1:0:open;laptop@1,0:0:open;camera@4,1:0:open;" +
                "sweater@4,3:0:folded;book@1,5:0:open;sneaker@3,5:0:open;bottle@1,4:90:open"));
            if (withFold.FirstSolution.Count(p => p.ShapeState == "folded") != 1 ||
                !withFold.FirstSolution.Any(p => p.ItemId == "sweater" && p.ShapeState == "folded"))
                throw new InvalidOperationException("SweaterFoldDependencyMismatch: L4");
            return new PhaseALevelValidation(level, withoutFold, withFold,
                sweaterLockedOpen: sweaterOpen, sweaterOnlyFold: sweaterOnly);
        }

        private static LevelDefinition WithRestrictions(LevelDefinition source, bool zeroRotations = false,
            bool lockSweaterOpen = false, bool sweaterOnlyFold = false)
        {
            var items = new List<ItemDefinition>();
            foreach (var item in source.Items.Values)
            {
                var openOnly = (lockSweaterOpen && item.Id == "sweater") ||
                    (sweaterOnlyFold && item.Id != "sweater");
                var stateIds = openOnly ? new[] { item.BaseStateId } : item.AuthoredShapeStateIds;
                items.Add(new ItemDefinition(item.Id, item.BaseStateId,
                    stateIds.Select(state => new KeyValuePair<string, ItemShape>(state, item.ShapeStates[state])),
                    zeroRotations ? new[] { Rotation.Degrees0 } : item.AllowedRotations,
                    item.Tags, item.VacuumShape, authoredShapeStateIds: stateIds,
                    visualPrefabId: item.VisualPrefabId));
            }
            return new LevelDefinition(source.SchemaVersion, source.MetricVersion, source.Id,
                source.Grammar, source.InitialState.Container, items, source.Preplaced,
                source.Inventory, source.Targets, source.Boosters);
        }

        private static void RequireArea(LevelDefinition level, int expectedArea, int expectedCapacity)
        {
            var area = level.Inventory.Sum(tray => level.Items[tray.ItemId].ShapeStates[tray.ShapeState].CellCount);
            if (area != expectedArea || level.InitialState.Container.Mask.ValidCellCount != expectedCapacity)
                throw new InvalidOperationException("AuthoredAreaMismatch: " + level.Id);
        }

        private static void Check(string levelId, string mode, PackSolverResult actual,
            MetricSnapshot expected)
        {
            if (actual.CappedSolutionCount != expected.Count ||
                actual.SolutionDensity != expected.Bucket ||
                actual.NodeExpansionCount != expected.Nodes ||
                actual.BacktrackCount != expected.Backtracks ||
                actual.FirstSolutionBacktracks != expected.FirstBacktracks ||
                actual.ForcedPlacementCount != expected.Forced ||
                FirstSolution(actual) != expected.First)
                throw new InvalidOperationException("MetricSnapshotMismatch: " + levelId + "/" + mode);
        }

        private static string FirstSolution(PackSolverResult result) =>
            string.Join(";", result.FirstSolution.Select(p =>
                p.ItemId + "@" + p.Anchor.X.ToString(CultureInfo.InvariantCulture) + "," +
                p.Anchor.Y.ToString(CultureInfo.InvariantCulture) + ":" +
                ((int)p.Rotation).ToString(CultureInfo.InvariantCulture) + ":" + p.ShapeState));

        private sealed class MetricSnapshot
        {
            public int Count { get; }
            public string Bucket { get; }
            public long Nodes { get; }
            public long Backtracks { get; }
            public long? FirstBacktracks { get; }
            public int? Forced { get; }
            public string First { get; }

            public MetricSnapshot(int count, string bucket, long nodes, long backtracks,
                long? firstBacktracks, int? forced, string first)
            {
                Count = count;
                Bucket = bucket;
                Nodes = nodes;
                Backtracks = backtracks;
                FirstBacktracks = firstBacktracks;
                Forced = forced;
                First = first;
            }
        }
    }
}
