using System.IO;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    // GOLDEN-LV1-CONTENT-LOCK: a non-shipping candidate level proving the Golden Lv1 roster exists under the current
    // Domain (schema v2, zones, Source Tray, PackSolverV2) with no new rule family, staging or modifier. Not lv1-fit.
    public sealed class GoldenLv1CandidateTests
    {
        private static readonly Rotation[] Upright = { Rotation.Degrees0, Rotation.Degrees90 };

        // Blueprint Ek A canonical footprints for passport (1x2) and towel (1x4, open only: Fold is not part of this level);
        // sweater is the shipped catalog spec. Shampoo, sunglasses and travel-pouch are new Golden Lv1 definitions.
        public static ItemSpec[] Catalog() => PuzzleItemCatalog.Create().Where(s => s.Id == "sweater").Concat(new[]
        {
            Single("passport", Rect(1, 2), "documents"),
            Single("towel", Rect(1, 4), "soft"),
            Single("shampoo", Rect(1, 3), "liquid", "toiletries"),
            Single("sunglasses", Rect(2, 1), "fragile"),
            Single("travel-pouch", Rect(2, 3), "toiletries")
        }).ToArray();

        private static ItemShape Rect(int width, int height) =>
            new ItemShape(from y in Enumerable.Range(0, height) from x in Enumerable.Range(0, width) select new Cell(x, y));

        private static ItemSpec Single(string id, ItemShape footprint, params string[] tags) =>
            new ItemSpec(id, "open", new[] { new ItemStateSpec("open", footprint, 1, Upright) }, tags);

        public static PuzzleLevel Load() => LevelJsonLoaderV2.Load(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
            "Scripts/Tests/EditMode/Fixtures/LevelsV2/golden-lv1-candidate.json")), Catalog());

        private static RuleResult Rule(PuzzleState state, PuzzleLevel level, string id) =>
            CompletionEvaluator.Evaluate(state, level.Objective, level.Rules).Rules.Single(r => r.RuleId == id);

        private static PuzzleMove At(string id, int x, int y, Rotation rotation = Rotation.Degrees0) =>
            PuzzleMove.PlaceInSuitcase(id, "main", new Cell(x, y), rotation, "open");

        [Test]
        public void Candidate_LoadsUnderSchemaV2_WithoutLaterMechanics()
        {
            var level = Load();
            var board = level.Spec.Board.Compartments.Single();
            Assert.That((board.Width, board.Height, board.Layers), Is.EqualTo((5, 7, 1)));
            Assert.That(board.ColumnZoneIds, Is.EqualTo(new[] { "right", "upper" }));
            Assert.That(level.Spec.StagingCapacity, Is.Zero, "no staging");
            Assert.That(level.Objective.Profile, Is.EqualTo(ObjectiveProfile.Pack));
            Assert.That(level.InitialState.Items.All(i => i.Definition.Transitions.Count == 0), Is.True, "no Fold / Compress");
            Assert.That(level.InitialState.Items.All(i => i.Definition.Nest.Capacity == 0), Is.True, "no Nest");
            Assert.That(level.Rules.Rules.All(r => r is ZoneRule), Is.True, "Zone rules only");
            Assert.That(level.InitialState.GetItems(ItemLocationKind.Suitcase).Select(i => i.InstanceId),
                Is.EquivalentTo(new[] { "sweater-1", "passport-1", "towel-1" }));
            Assert.That(level.InitialState.GetItems(ItemLocationKind.SourceTray).Select(i => i.InstanceId),
                Is.EquivalentTo(new[] { "shampoo-1", "sunglasses-1", "travel-pouch-1" }));
            var area = level.InitialState.Items.Sum(i => i.Definition.DefaultState.Footprint.CellCount);
            Assert.That(area, Is.EqualTo(26), "26 / 35 solved occupancy");
            Assert.That(board.GetColumnZone(new Cell(4, 1)), Is.EqualTo("upper"));
            Assert.That(board.GetColumnZone(new Cell(3, 2)), Is.EqualTo("right"));
            Assert.That(board.GetColumnZone(new Cell(2, 2)), Is.Null);
        }

        [Test]
        public void Candidate_InitialPassportSatisfiesUpper_ShampooRuleOpen_AndNotComplete()
        {
            var level = Load();
            var session = new PuzzleSession(level);
            Assert.That(Rule(level.InitialState, level, "passport-upper").IsSatisfied, Is.True);
            Assert.That(session.CurrentCompletion.IsComplete, Is.False, "three items still in the Source Tray");
            // Negative: the prepacked passport is still movable, and leaving the upper zone breaks its rule.
            var moved = session.Apply(At("passport-1", 4, 5));
            Assert.That(moved.Move.IsAccepted, Is.True);
            Assert.That(Rule(session.CurrentState, level, "passport-upper").OffendingIds, Is.EqualTo(new[] { "passport-1" }));
        }

        [Test]
        public void Candidate_IsSolvable_AndTheSolutionCompletesAutomatically()
        {
            var level = Load();
            var result = PackSolverV2.Solve(level);
            TestContext.WriteLine($"[golden-lv1] status={result.Status} count={result.SolutionCount} capped={result.SolutionCountCapped}" +
                $" greedy={result.GreedySolvable} preplacedFixed={result.PreplacedItemCount}" +
                $" couplings={string.Join(",", result.RuleCouplings.Select(c => c.RuleId + ":" + c.Coupling))}" +
                $" nodes={result.Metrics.NodesVisited} moves={string.Join(" ", result.Solution.Moves)}");
            Assert.That(result.Status, Is.EqualTo(PackSolveStatus.Solvable));
            // Greedy is false by design: the pouch dropped first into the right strip leaves no room for the shampoo
            // (one recoverable "read the rule" moment). Many solutions exist (search cap reached).
            Assert.That(result.SolutionCountCapped, Is.True);
            Assert.That(result.RuleCouplings.Single(c => c.RuleId == "shampoo-right").Coupling, Is.EqualTo(RuleCoupling.Coupled),
                "the shampoo rule changes which layouts complete");
            var session = new PuzzleSession(level);
            PuzzleSessionStep last = null;
            foreach (var move in result.Solution.Moves)
                Assert.That((last = session.Apply(move)).Move.IsAccepted, Is.True, move.ToString());
            Assert.That(last.CompletionReached, Is.True, "completion on the last accepted move, no extra action");
            Assert.That(session.MoveCount, Is.EqualTo(3));
        }

        [Test]
        public void Candidate_AllPackedWithShampooOutsideTheRightZone_IsNotComplete()
        {
            var level = Load();
            var session = new PuzzleSession(level);
            Assert.That(session.Apply(At("shampoo-1", 1, 3)).Move.IsAccepted, Is.True);
            Assert.That(session.Apply(At("travel-pouch-1", 3, 2)).Move.IsAccepted, Is.True);
            Assert.That(session.Apply(At("sunglasses-1", 3, 5)).Move.IsAccepted, Is.True);
            Assert.That(session.CurrentState.GetItems(ItemLocationKind.SourceTray), Is.Empty);
            Assert.That(session.CurrentCompletion.IsComplete, Is.False);
            Assert.That(Rule(session.CurrentState, level, "shampoo-right").OffendingIds, Is.EqualTo(new[] { "shampoo-1" }));
            // Positive: the same three items with the shampoo in the right zone complete.
            var fixedSession = new PuzzleSession(level);
            fixedSession.Apply(At("shampoo-1", 4, 2));
            fixedSession.Apply(At("travel-pouch-1", 1, 3));
            var last = fixedSession.Apply(At("sunglasses-1", 3, 6));
            Assert.That(last.CompletionReached, Is.True);
        }
    }
}
