using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PackSolverV2Tests
    {
        private static BoardSpec Board(int width, int height, int layers, IEnumerable<KeyValuePair<Cell, string>> zones = null) =>
            new BoardSpec(new[] { new Compartment("main", width, height, layers, RectCells(width, height), zones) });

        private static PuzzleLevel Level(BoardSpec board, PuzzleRule[] rules, string[] required, params PuzzleItem[] items) =>
            new PuzzleLevel("solver", new PuzzleState(new PuzzleSpec(board, 2, new[] { Tray() }), items),
                new RuleSet(board, rules), PuzzleObjective.Pack(required ?? items.Select(i => i.InstanceId).ToArray()));

        private static PuzzleItem T(string id, ItemSpec spec) => Item(id, spec, ItemLocation.SourceTray);

        private static PackSolveResult Solve(PuzzleLevel level)
        {
            var timer = Stopwatch.StartNew();
            var result = PackSolverV2.Solve(level);
            TestContext.WriteLine($"[solver-metrics] {TestContext.CurrentContext.Test.Name}: status={result.Status} count={result.SolutionCount}" +
                $"{(result.SolutionCountCapped ? "+" : "")} nodes={result.Metrics.NodesVisited} candidates={result.Metrics.CandidatesTested}" +
                $" deadEnds={result.Metrics.DeadEnds} ms={timer.ElapsedMilliseconds}");
            return result;
        }

        private static PuzzleItem Get(PuzzleState state, string id)
        {
            state.TryGetItem(id, out var item);
            return item;
        }

        /// <summary>Replays the representative solution through a fresh PuzzleSession: every move legal, ends complete.</summary>
        private static void AssertReplays(PuzzleLevel level, PackSolveResult result)
        {
            var session = new PuzzleSession(level);
            foreach (var move in result.Solution.Moves)
                Assert.That(session.Apply(move).Move.IsAccepted, Is.True, move.ToString());
            Assert.That(session.CurrentCompletion.IsComplete, Is.True);
            Assert.That(session.MoveCount, Is.EqualTo(result.Solution.Moves.Count));
            Assert.That(session.CurrentState.Hash, Is.EqualTo(result.Solution.FinalState.Hash));
        }

        // ---- Solvability ----

        [Test]
        public void SimpleLevel_IsSolvableWithExactCountAndReplays()
        {
            var level = Level(Board(2, 1, 1), new PuzzleRule[0], null, T("a", Block("a", 1, 1)), T("b", Block("b", 1, 1)));
            var result = Solve(level);
            Assert.That(result.Status, Is.EqualTo(PackSolveStatus.Solvable));
            Assert.That(result.SolutionCount, Is.EqualTo(2));
            Assert.That(result.SolutionCountCapped, Is.False);
            Assert.That(result.GreedySolvable, Is.True);
            Assert.That(result.PreplacedItemsHeldFixed, Is.False);
            AssertReplays(level, result);
        }

        [Test]
        public void ImpossibleLevel_IsUnsolvableWithoutThrowing()
        {
            var result = Solve(Level(Board(2, 1, 1), new PuzzleRule[0], null, T("rod", Block("rod", 3, 1))));
            Assert.That(result.Status, Is.EqualTo(PackSolveStatus.Unsolvable));
            Assert.That(result.SolutionCount, Is.Zero);
            Assert.That(result.Solution, Is.Null);
            Assert.That(result.GreedySolvable, Is.False);
        }

        // ---- Rotation ----

        [Test]
        public void UniqueRotations_RemoveGeometricDuplicatesOnly()
        {
            var sneaker = new ItemStateSpec("s", new ItemShape(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2) }), 1, AllRotations);
            Assert.That(PackSolverV2.UniqueRotations(sneaker).Count, Is.EqualTo(4));
            Assert.That(PackSolverV2.UniqueRotations(new ItemStateSpec("r", Rect(2, 1), 1, AllRotations)),
                Is.EqualTo(new[] { Rotation.Degrees0, Rotation.Degrees90 }));
            Assert.That(PackSolverV2.UniqueRotations(new ItemStateSpec("q", Rect(2, 2), 1, AllRotations)), Is.EqualTo(new[] { Rotation.Degrees0 }));
        }

        [Test]
        public void SymmetricShapes_AreNotMultipliedByEquivalentRotations()
        {
            var rod = Solve(Level(Board(2, 1, 1), new PuzzleRule[0], null, T("rod", Block("rod", 2, 1))));
            Assert.That(rod.SolutionCount, Is.EqualTo(1), "0 and 180 degrees are the same geometry");
            Assert.That(Get(rod.Solution.FinalState, "rod").Location.Placement.Rotation, Is.EqualTo(Rotation.Degrees0), "runtime keeps the real rotation");

            var square = Solve(Level(Board(2, 2, 1), new PuzzleRule[0], null, T("sq", Block("sq", 2, 2))));
            Assert.That(square.SolutionCount, Is.EqualTo(1));
        }

        [Test]
        public void RotationRequiredLevel_PlacesTheRotatedFootprint()
        {
            var level = Level(Board(1, 3, 1), new PuzzleRule[0], null, T("rod", Block("rod", 3, 1)));
            var result = Solve(level);
            Assert.That(result.SolutionCount, Is.EqualTo(1));
            Assert.That(Get(result.Solution.FinalState, "rod").Location.Placement.Rotation, Is.EqualTo(Rotation.Degrees90));
            AssertReplays(level, result);
        }

        // ---- Layers / modifiers ----

        [Test]
        public void LayeredLevel_NeedsSupportAndReplaysWithResolvedLayer()
        {
            var level = Level(Board(2, 1, 2), new PuzzleRule[0], null, T("top", Block("top", 1, 1)), T("base", Block("base", 2, 1)));
            var result = Solve(level);
            Assert.That(result.SolutionCount, Is.EqualTo(2), "top on either half of the base");
            Assert.That(Get(result.Solution.FinalState, "top").Location.Placement.Layer, Is.EqualTo(1));
            Assert.That(Get(result.Solution.FinalState, "base").Location.Placement.Layer, Is.EqualTo(0));
            AssertReplays(level, result);
        }

        [Test]
        public void FoldRequiredLevel_UsesTheAuthoredFoldStateInOneMove()
        {
            var level = Level(Board(2, 4, 1), new PuzzleRule[0], null, T("sw", Sweater()));
            var result = Solve(level);
            Assert.That(result.SolutionCount, Is.EqualTo(1));
            Assert.That(Get(result.Solution.FinalState, "sw").StateId, Is.EqualTo("folded"));
            Assert.That(result.Solution.Moves.Single().StateId, Is.EqualTo("folded"));
            AssertReplays(level, result);
        }

        [Test]
        public void CompressRequiredLevel_ChoosesTheCompressedState()
        {
            var level = Level(Board(2, 2, 2), new PuzzleRule[0], null, T("j", Jacket()), T("base", Block("base", 2, 2)));
            var result = Solve(level);
            Assert.That(result.SolutionCount, Is.EqualTo(2), "compressed jacket under or over the base");
            Assert.That(Get(result.Solution.FinalState, "j").StateId, Is.EqualTo("compressed"));
            AssertReplays(level, result);
        }

        // ---- Counting ----

        [Test]
        public void ManySolutions_StopAtTheCapAndReportIt()
        {
            var level = Level(Board(6, 3, 1), new PuzzleRule[0], null,
                T("a", Block("a", 1, 1)), T("b", Block("b", 1, 1)), T("c", Block("c", 1, 1)));
            var result = Solve(level);
            Assert.That(result.SolutionCount, Is.EqualTo(PackSolveResult.SolutionCountCap));
            Assert.That(result.SolutionCountCapped, Is.True);
        }

        [Test]
        public void InstancesOfOneDefinition_StayDistinctSolutions()
        {
            var dot = Block("dot", 1, 1);
            var result = Solve(Level(Board(2, 1, 1), new PuzzleRule[0], null, T("d1", dot), T("d2", dot)));
            Assert.That(result.SolutionCount, Is.EqualTo(2), "swapping two instances is a different solution");
        }

        [Test]
        public void SolveIsDeterministicAndPure()
        {
            var level = Level(Board(3, 2, 2), new PuzzleRule[0], null, T("a", Block("a", 2, 1)), T("b", Block("b", 1, 1)), T("c", Block("c", 1, 2)));
            var hash = level.InitialState.Hash;
            var first = Solve(level);
            var second = Solve(level);
            Assert.That(second.SolutionCount, Is.EqualTo(first.SolutionCount));
            Assert.That(second.Solution.EquivalenceKey, Is.EqualTo(first.Solution.EquivalenceKey));
            Assert.That(second.Solution.Moves.Select(m => m.ToString()), Is.EqualTo(first.Solution.Moves.Select(m => m.ToString())));
            Assert.That(level.InitialState.Hash, Is.EqualTo(hash));
        }

        // ---- Greedy ----

        [Test]
        public void Greedy_FailsWhereBacktrackingSucceeds_AndZoneRuleIsCoupled()
        {
            // Rod (2 candidates) is ordered before the dot (3); greedy puts the rod at x = 0 and the dot cannot reach the zone.
            var board = Board(3, 1, 1, new[] { new KeyValuePair<Cell, string>(new Cell(0, 0), "left") });
            var level = Level(board, new PuzzleRule[] { new ZoneRule("dot-left", ItemSelector.Instance("dot"), "left") }, null,
                T("rod", Block("rod", 2, 1)), T("dot", Block("dot", 1, 1)));
            var result = Solve(level);
            Assert.That(result.Status, Is.EqualTo(PackSolveStatus.Solvable));
            Assert.That(result.SolutionCount, Is.EqualTo(1));
            Assert.That(result.GreedySolvable, Is.False);
            var coupling = result.RuleCouplings.Single();
            Assert.That(coupling.Coupling, Is.EqualTo(RuleCoupling.Coupled));
            Assert.That(coupling.SolutionsWithoutRule, Is.EqualTo(2));
            AssertReplays(level, result);
        }

        // ---- Rule coupling ----

        [Test]
        public void ZoneCoveringEverything_IsDead()
        {
            var board = Board(2, 1, 1, RectCells(2, 1).Select(c => new KeyValuePair<Cell, string>(c, "all")));
            var result = Solve(Level(board, new PuzzleRule[] { new ZoneRule("anywhere", ItemSelector.Instance("a"), "all") }, null,
                T("a", Block("a", 1, 1)), T("b", Block("b", 1, 1))));
            Assert.That(result.RuleCouplings.Single().Coupling, Is.EqualTo(RuleCoupling.Dead));
            Assert.That(result.RuleCouplings.Single().SolutionsWithoutRule, Is.EqualTo(result.SolutionCount));
        }

        [Test]
        public void CappedSearchWithoutAViolation_IsInconclusive()
        {
            var board = Board(6, 3, 1, RectCells(6, 3).Select(c => new KeyValuePair<Cell, string>(c, "all")));
            var result = Solve(Level(board, new PuzzleRule[] { new ZoneRule("anywhere", ItemSelector.Instance("a"), "all") }, null,
                T("a", Block("a", 1, 1)), T("b", Block("b", 1, 1)), T("c", Block("c", 1, 1))));
            Assert.That(result.SolutionCountCapped, Is.True);
            Assert.That(result.RuleCouplings.Single().Coupling, Is.EqualTo(RuleCoupling.Inconclusive));
        }

        [Test]
        public void AdjacencyAndAccessRules_ConstrainTheSolutionSet()
        {
            var a = Block("a", 1, 1);
            var b = Block("b", 1, 1);
            var near = Solve(Level(Board(3, 1, 1), new PuzzleRule[] { new AdjacencyRequiredRule("near", ItemSelector.Instance("a"), ItemSelector.Instance("b")) },
                null, T("a", a), T("b", b)));
            Assert.That(near.SolutionCount, Is.EqualTo(4));
            Assert.That(near.RuleCouplings.Single().SolutionsWithoutRule, Is.EqualTo(6));
            Assert.That(near.RuleCouplings.Single().Coupling, Is.EqualTo(RuleCoupling.Coupled));

            var apart = Solve(Level(Board(3, 1, 1), new PuzzleRule[] { new AdjacencyForbiddenRule("apart", ItemSelector.Instance("a"), ItemSelector.Instance("b")) },
                null, T("a", a), T("b", b)));
            Assert.That(apart.SolutionCount, Is.EqualTo(2));

            var accessLevel = Level(Board(1, 1, 2), new PuzzleRule[] { new AccessRule("a-on-top", ItemSelector.Instance("a")) }, null, T("a", a), T("b", b));
            var access = Solve(accessLevel);
            Assert.That(access.SolutionCount, Is.EqualTo(1));
            Assert.That(Get(access.Solution.FinalState, "a").Location.Placement.Layer, Is.EqualTo(1));
            AssertReplays(accessLevel, access);
        }

        // ---- Option A scope ----

        [Test]
        public void PreplacedItems_AreHeldFixedAndReported()
        {
            var fixedBlock = Placed("fixed", Block("fixed", 1, 1), new Placement("main", new Cell(0, 0), 0, Rotation.Degrees0));
            var level = Level(Board(3, 1, 1), new PuzzleRule[0], new[] { "fixed", "dot" }, fixedBlock, T("dot", Block("dot", 1, 1)));
            var result = Solve(level);
            Assert.That(result.PreplacedItemsHeldFixed, Is.True);
            Assert.That(result.PreplacedItemCount, Is.EqualTo(1));
            Assert.That(result.SolutionCount, Is.EqualTo(2));
            Assert.That(Get(result.Solution.FinalState, "fixed").Location, Is.EqualTo(fixedBlock.Location));
            AssertReplays(level, result);

            // Only rearranging the pre-placed item would help: reported Unsolvable relative to the fixed layout.
            var blocked = Solve(Level(Board(3, 1, 1), new PuzzleRule[0], new[] { "fixed", "rod" },
                Placed("fixed", Block("fixed", 1, 1), new Placement("main", new Cell(1, 0), 0, Rotation.Degrees0)), T("rod", Block("rod", 2, 1))));
            Assert.That(blocked.Status, Is.EqualTo(PackSolveStatus.Unsolvable));
            Assert.That(blocked.PreplacedItemsHeldFixed, Is.True);
        }

        [Test]
        public void OptionalSourceTrayItem_MayStayInTheTray()
        {
            var result = Solve(Level(Board(1, 1, 1), new PuzzleRule[0], new[] { "a" }, T("a", Block("a", 1, 1)), T("spare", Block("spare", 1, 1))));
            Assert.That(result.SolutionCount, Is.EqualTo(1));
            Assert.That(Get(result.Solution.FinalState, "spare").Location.Kind, Is.EqualTo(ItemLocationKind.SourceTray));
        }

        // ---- Profiles ----

        [Test]
        public void ExtractAndRepack_AreUnsupported()
        {
            var board = Board(2, 1, 1);
            var state = new PuzzleState(new PuzzleSpec(board, 2, new[] { Tray() }),
                new[] { Placed("x", Block("x", 1, 1), new Placement("main", new Cell(0, 0), 0, Rotation.Degrees0), ObjectiveRole.ExtractionTarget),
                        T("y", Block("y", 1, 1)) });
            var extract = new PuzzleLevel("e", state, new RuleSet(board, null), PuzzleObjective.Extract("x", "tray"));
            var repack = new PuzzleLevel("r", state, new RuleSet(board, null), PuzzleObjective.Repack(new[] { "x" }, new[] { "y" }));
            Assert.That(PackSolverV2.Solve(extract).Status, Is.EqualTo(PackSolveStatus.UnsupportedProfile));
            Assert.That(PackSolverV2.Solve(repack).Status, Is.EqualTo(PackSolveStatus.UnsupportedProfile));
        }

        [Test]
        public void LoadedFixtures_SolveThroughTheSchemaV2Boundary()
        {
            var simple = LevelSchemaV2Tests.Fixture("pack-simple.json");
            var solved = Solve(simple);
            Assert.That(solved.Status, Is.EqualTo(PackSolveStatus.Solvable));
            Assert.That(solved.PreplacedItemsHeldFixed, Is.False);
            AssertReplays(simple, solved);

            // The 2x2 jacket cannot fit around the fixed sweater and globe: unsolvable relative to the authored layout.
            var layered = Solve(LevelSchemaV2Tests.Fixture("pack-layered.json"));
            Assert.That(layered.Status, Is.EqualTo(PackSolveStatus.Unsolvable));
            Assert.That(layered.PreplacedItemsHeldFixed, Is.True);
            Assert.That(layered.PreplacedItemCount, Is.EqualTo(2));
        }
    }
}
