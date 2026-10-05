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
    // GOLDEN-LV1-SHIP: the shipped Level 1 (Resources/LevelsV2/lv1-fit.json) loaded through the runtime catalog, exactly
    // as PuzzleGameplayScene loads it. Content lock: golden-lv1-content-lock.md.
    public sealed class GoldenLv1ShippedTests
    {
        public const string LevelId = "lv1-fit";
        private static readonly Rotation[] Upright = { Rotation.Degrees0, Rotation.Degrees90 };

        public static PuzzleLevel Load() => LevelJsonLoaderV2.Load(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
            "Resources/LevelsV2", LevelId + ".json")), PuzzleItemCatalog.Create());

        private static RuleResult Rule(PuzzleState state, PuzzleLevel level, string id) =>
            CompletionEvaluator.Evaluate(state, level.Objective, level.Rules).Rules.Single(r => r.RuleId == id);

        private static PuzzleMove At(string id, int x, int y, Rotation rotation = Rotation.Degrees0) =>
            PuzzleMove.PlaceInSuitcase(id, "main", new Cell(x, y), rotation, "open");

        [Test]
        public void Shipped_LoadsUnderSchemaV2_WithoutLaterMechanics()
        {
            var level = Load();
            Assert.That(level.Id, Is.EqualTo(LevelId));
            var board = level.Spec.Board.Compartments.Single();
            Assert.That((board.Width, board.Height, board.Layers), Is.EqualTo((5, 7, 1)));
            Assert.That(level.Spec.StagingCapacity, Is.Zero, "no staging");
            Assert.That(level.Objective.Profile, Is.EqualTo(ObjectiveProfile.Pack));
            Assert.That(level.Objective.RequiredInstanceIds.Count, Is.EqualTo(6));
            Assert.That(level.InitialState.Items.All(i => i.Definition.Transitions.Count == 0), Is.True, "no Fold / Compress");
            Assert.That(level.InitialState.Items.All(i => i.Definition.Nest.Capacity == 0), Is.True, "no Nest");
            Assert.That(BoardInvariants.Evaluate(level.InitialState).IsValid, Is.True, "no initial overlap / support violation");
            var area = level.InitialState.Items.Sum(i => i.Definition.DefaultState.Footprint.CellCount);
            Assert.That(area, Is.EqualTo(26), "26 / 35 solved occupancy");
        }

        [TestCase("passport", 1, 2, true)]
        [TestCase("sweater", 3, 3, false)]
        [TestCase("towel", 1, 4, true)]
        [TestCase("shampoo", 1, 3, true)]
        [TestCase("sunglasses", 2, 1, true)]
        [TestCase("travel-pouch", 2, 3, true)]
        public void Shipped_RosterIsCanonical(string definitionId, int width, int height, bool rotates)
        {
            var item = Load().InitialState.Items.Single(i => i.Definition.Id == definitionId);
            Assert.That(item.Definition.States.Select(s => s.Id), Is.EqualTo(new[] { "open" }), "open only");
            var state = item.Definition.DefaultState;
            Assert.That(state.Thickness, Is.EqualTo(1));
            Assert.That(state.AllowedRotations, Is.EqualTo(rotates ? Upright : new[] { Rotation.Degrees0 }));
            var cells = state.Footprint.OccupiedCells;
            Assert.That(cells.Count, Is.EqualTo(width * height), "solid rectangle");
            Assert.That((cells.Max(c => c.X) + 1, cells.Max(c => c.Y) + 1), Is.EqualTo((width, height)));
        }

        [Test]
        public void Va03_PresentationCopyCoversEveryRuleAndAllSixItems_WithoutChangingCanonicalIds()
        {
            var level = Load();
            Assert.That(level.InitialState.Items.Select(i => i.Definition.Id).OrderBy(id => id), Is.EqualTo(new[]
            {
                "passport", "shampoo", "sunglasses", "sweater", "towel", "travel-pouch"
            }));
            foreach (var rule in level.Rules.Rules)
            {
                var keyword = PuzzleRuleText.Keyword(rule, level.InitialState);
                Assert.That(keyword, Is.Not.Null.And.Not.Empty, rule.Id);
                Assert.That(PuzzleRuleText.Label(rule, level.InitialState), Does.Contain(keyword), rule.Id);
                Assert.That(PuzzleRuleText.RichLabel(rule, level.InitialState), Does.Contain("<b><color=#168C8C>" + keyword), rule.Id);
            }
            // ART-CC02: short mission copy and a directional chip are presentation only; the full sentence stays the label.
            var byId = level.Rules.Rules.ToDictionary(r => r.Id);
            Assert.That(PuzzleRuleText.Copy(byId["passport-upper"], level.InitialState), Is.EqualTo("Pasaport üstte"));
            Assert.That(PuzzleRuleText.Copy(byId["shampoo-right"], level.InitialState), Is.EqualTo("Şampuan sağda"));
            Assert.That(PuzzleRuleText.Chip(byId["passport-upper"], level.InitialState), Is.EqualTo("Pasaport ↑"));
            Assert.That(PuzzleRuleText.Chip(byId["shampoo-right"], level.InitialState), Is.EqualTo("Şampuan →"));
            Assert.That(PuzzleRuleText.Label(byId["passport-upper"], level.InitialState), Is.EqualTo("Pasaport üst bölgede olmalı"),
                "full rule semantics kept");
            Assert.That(PuzzleRuleText.Chip(byId["passport-upper"], level.InitialState),
                Is.Not.EqualTo(PuzzleRuleText.Subject(byId["passport-upper"], level.InitialState)), "chip never drops the direction");
            Assert.That(new[] { "sweater", "passport", "towel", "shampoo", "sunglasses", "travel-pouch" }
                .Select(PuzzleRuleText.StagingName), Is.EqualTo(new[]
            {
                "Kazak", "Pasaport", "Havlu", "Şampuan", "Güneş Gözlüğü", "Seyahat Çantası"
            }));
        }

        [Test]
        public void Shipped_SplitIsThreePrepackedAndThreeSourceTray()
        {
            var state = Load().InitialState;
            Assert.That(state.Items.Count, Is.EqualTo(6));
            Assert.That(state.GetItems(ItemLocationKind.Suitcase).Select(i => (i.InstanceId, i.Location.Placement.ToString())),
                Is.EquivalentTo(new[]
                {
                    ("sweater-1", "main(0,0) z0 r0"), ("passport-1", "main(3,0) z0 r0"), ("towel-1", "main(0,3) z0 r0")
                }));
            Assert.That(state.GetItems(ItemLocationKind.SourceTray).Select(i => i.InstanceId),
                Is.EquivalentTo(new[] { "shampoo-1", "sunglasses-1", "travel-pouch-1" }));
        }

        [Test]
        public void Shipped_ZonesAndRulesAreAuthoredExactly()
        {
            var level = Load();
            var board = level.Spec.Board.Compartments.Single();
            Assert.That(board.ColumnZoneIds, Is.EqualTo(new[] { "right", "upper" }));
            for (var y = 0; y < 7; y++)
                for (var x = 0; x < 5; x++)
                {
                    var expected = y <= 1 ? "upper" : x >= 3 ? "right" : null;
                    Assert.That(board.GetColumnZone(new Cell(x, y)), Is.EqualTo(expected), $"({x},{y})");
                }
            Assert.That(level.Rules.Rules.Select(r => r.Id), Is.EqualTo(new[] { "passport-upper", "shampoo-right" }));
            var passport = (ZoneRule)level.Rules.Rules[0];
            var shampoo = (ZoneRule)level.Rules.Rules[1];
            Assert.That((passport.Subjects.Kind, passport.Subjects.Value, passport.ZoneId),
                Is.EqualTo((ItemSelectorKind.Instance, "passport-1", "upper")));
            Assert.That((shampoo.Subjects.Kind, shampoo.Subjects.Value, shampoo.ZoneId),
                Is.EqualTo((ItemSelectorKind.Instance, "shampoo-1", "right")));
        }

        [Test]
        public void Shipped_InitialPassportSatisfiesUpper_ShampooRuleOpen_AndNotComplete()
        {
            var level = Load();
            var session = new PuzzleSession(level);
            Assert.That(Rule(level.InitialState, level, "passport-upper").IsSatisfied, Is.True);
            Assert.That(Rule(level.InitialState, level, "shampoo-right").IsSatisfied, Is.False, "shampoo still in the tray");
            Assert.That(session.CurrentCompletion.IsComplete, Is.False, "three items still in the Source Tray");
            // Negative: the prepacked passport is still movable, and leaving the upper zone breaks its rule.
            var moved = session.Apply(At("passport-1", 4, 5));
            Assert.That(moved.Move.IsAccepted, Is.True);
            Assert.That(Rule(session.CurrentState, level, "passport-upper").OffendingIds, Is.EqualTo(new[] { "passport-1" }));
        }

        [Test]
        public void Shipped_IsSolvable_AndTheSolutionCompletesAutomatically()
        {
            var level = Load();
            var result = PackSolverV2.Solve(level);
            TestContext.WriteLine($"[golden-lv1-ship] status={result.Status} count={result.SolutionCount} capped={result.SolutionCountCapped}" +
                $" greedy={result.GreedySolvable} preplacedFixed={result.PreplacedItemCount}" +
                $" couplings={string.Join(",", result.RuleCouplings.Select(c => c.RuleId + ":" + c.Coupling))}" +
                $" nodes={result.Metrics.NodesVisited} moves={string.Join(" ", result.Solution.Moves)}");
            Assert.That(result.Status, Is.EqualTo(PackSolveStatus.Solvable));
            // Greedy is false by design: the pouch dropped first into the right strip leaves no room for the shampoo
            // (one recoverable "read the rule" moment). Many solutions exist (search cap reached).
            Assert.That(result.SolutionCountCapped, Is.True);
            Assert.That(result.GreedySolvable, Is.False);
            Assert.That(result.PreplacedItemCount, Is.EqualTo(3));
            Assert.That(result.RuleCouplings.Single(c => c.RuleId == "shampoo-right").Coupling, Is.EqualTo(RuleCoupling.Coupled),
                "the shampoo rule changes which layouts complete");
            var session = new PuzzleSession(level);
            PuzzleSessionStep last = null;
            foreach (var move in result.Solution.Moves)
                Assert.That((last = session.Apply(move)).Move.IsAccepted, Is.True, move.ToString());
            Assert.That(last.CompletionReached, Is.True, "completion on the last accepted move, no extra action");
            Assert.That(session.CurrentCompletion.Rules.All(r => r.IsSatisfied), Is.True, "both Zone rules satisfied");
            Assert.That(session.CurrentState.GetItems(ItemLocationKind.SourceTray), Is.Empty, "every required item placed");
            Assert.That(session.MoveCount, Is.EqualTo(3));
        }

        [Test]
        public void Shipped_AllPackedWithShampooOutsideTheRightZone_IsNotComplete()
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

        [Test]
        public void Shipped_UndoRestoresMoveCount_AndOverlapIsRejectedWithoutStateChange()
        {
            var level = Load();
            var session = new PuzzleSession(level);
            var initial = session.CurrentState;
            var overlap = session.Apply(At("travel-pouch-1", 0, 0));
            Assert.That(overlap.Move.IsAccepted, Is.False, "onto the prepacked sweater");
            Assert.That(session.CurrentState, Is.SameAs(initial));
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.Apply(At("travel-pouch-1", 1, 3)).Move.IsAccepted, Is.True);
            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(session.Undo().StateChanged, Is.True);
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.CurrentState.Hash, Is.EqualTo(initial.Hash));
        }
    }
}
