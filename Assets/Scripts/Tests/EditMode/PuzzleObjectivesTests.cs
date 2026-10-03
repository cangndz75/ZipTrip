using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PuzzleObjectivesTests
    {
        private static readonly RuleSet NoRules = new RuleSet(Board(), null);

        private static ItemSpec Shoe() => Block("shoe", 2, 1, nest: new NestSpec(1, new[] { "socks" }, null));

        private static CompletionFailureKind[] Kinds(CompletionResult result) => result.Failures.Select(f => f.Kind).ToArray();

        private static CompletionResult Evaluate(PuzzleState state, PuzzleObjective objective, RuleSet rules = null) =>
            CompletionEvaluator.Evaluate(state, objective, rules ?? NoRules);

        private static RuleSet AccessRule(string instanceId) =>
            new RuleSet(Board(), new PuzzleRule[] { new AccessRule("access-" + instanceId, ItemSelector.Instance(instanceId)) });

        // ---- Pack ----

        [Test]
        public void Pack_AllRequiredInSuitcase_IsComplete()
        {
            var state = State(Spec(), Placed("a", Block("a", 2, 1), At(0, 0)), Placed("b", Block("b", 1, 1), At(2, 0)));
            var result = Evaluate(state, PuzzleObjective.Pack(new[] { "a", "b" }));
            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.Invariants.IsValid, Is.True);
        }

        [Test]
        public void Pack_RequiredInSourceTrayOrStaging_IsIncomplete()
        {
            var tray = State(Spec(), Placed("a", Block("a", 1, 1), At(0, 0)), Item("b", Block("b", 1, 1), ItemLocation.SourceTray));
            var result = Evaluate(tray, PuzzleObjective.Pack(new[] { "a", "b" }));
            Assert.That(result.Failures.Single().Kind, Is.EqualTo(CompletionFailureKind.RequiredItemNotInSuitcase));
            Assert.That(result.Failures.Single().InstanceId, Is.EqualTo("b"));

            var staged = State(Spec(), Placed("a", Block("a", 1, 1), At(0, 0)), Item("b", Block("b", 1, 1), ItemLocation.Staging));
            Assert.That(Kinds(Evaluate(staged, PuzzleObjective.Pack(new[] { "a", "b" }))),
                Is.EqualTo(new[] { CompletionFailureKind.RequiredItemNotInSuitcase, CompletionFailureKind.StagingNotEmpty }));
        }

        [Test]
        public void Pack_OptionalSourceTrayItem_DoesNotBlock()
        {
            var state = State(Spec(), Placed("a", Block("a", 1, 1), At(0, 0)), Item("spare", Block("spare", 1, 1), ItemLocation.SourceTray, ObjectiveRole.None),
                Item("other-required", Block("x", 1, 1), ItemLocation.SourceTray, ObjectiveRole.Required));
            Assert.That(Evaluate(state, PuzzleObjective.Pack(new[] { "a" })).IsComplete, Is.True,
                "only the objective's explicit required set counts, not every Required role in the state");
        }

        [Test]
        public void Pack_NestedRequiredChild_CountsOnlyUnderASuitcaseParent()
        {
            var inSuitcase = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            Assert.That(Evaluate(inSuitcase, PuzzleObjective.Pack(new[] { "shoe-1", "socks-1" })).IsComplete, Is.True);
            Assert.That(CompletionEvaluator.IsInSuitcase(inSuitcase, "socks-1"), Is.True);

            var staged = State(Spec(), Item("shoe-1", Shoe(), ItemLocation.Staging), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            var result = Evaluate(staged, PuzzleObjective.Pack(new[] { "socks-1" }));
            Assert.That(result.Failures.Select(f => (f.Kind, f.InstanceId)), Is.EqualTo(new[]
            {
                (CompletionFailureKind.RequiredItemNotInSuitcase, "socks-1"),
                (CompletionFailureKind.StagingNotEmpty, "shoe-1")
            }));
        }

        [Test]
        public void Pack_NonEmptyStaging_BlocksEvenWithOptionalItem()
        {
            var state = State(Spec(), Placed("a", Block("a", 1, 1), At(0, 0)), Item("spare", Block("spare", 1, 1), ItemLocation.Staging, ObjectiveRole.None));
            Assert.That(Kinds(Evaluate(state, PuzzleObjective.Pack(new[] { "a" }))), Is.EqualTo(new[] { CompletionFailureKind.StagingNotEmpty }));
        }

        [Test]
        public void Pack_RuleFailure_Blocks()
        {
            var state = State(Spec(), Placed("a", Block("a", 1, 1), At(0, 0)), Placed("top", Block("top", 1, 1), At(0, 0, layer: 1)));
            var result = Evaluate(state, PuzzleObjective.Pack(new[] { "a", "top" }), AccessRule("a"));
            Assert.That(result.Failures.Single().Kind, Is.EqualTo(CompletionFailureKind.RuleViolation));
            Assert.That(result.Failures.Single().RuleId, Is.EqualTo("access-a"));
            Assert.That(result.Rules.Single().IsSatisfied, Is.False);
        }

        [Test]
        public void Pack_InvariantFailure_Blocks()
        {
            var state = State(Spec(), Placed("a", Block("a", 1, 1), At(0, 0)), Placed("b", Block("b", 1, 1), At(0, 0)));
            var result = Evaluate(state, PuzzleObjective.Pack(new[] { "a", "b" }));
            Assert.That(Kinds(result), Is.EqualTo(new[] { CompletionFailureKind.InvariantViolation }));
            Assert.That(result.Invariants.Violations.Single().Kind, Is.EqualTo(InvariantKind.Overlap));
        }

        // ---- Repack ----

        private static PuzzleState RepackStart() => State(Spec(),
            Placed("old-a", Block("a", 2, 1), At(0, 0)), Placed("old-b", Block("b", 2, 1), At(2, 0)),
            Item("souvenir", Block("mug", 1, 1), ItemLocation.SourceTray));

        private static PuzzleObjective RepackGoal() => PuzzleObjective.Repack(new[] { "old-a", "old-b" }, new[] { "souvenir" });

        [Test]
        public void Repack_IncomingStillOutside_IsIncomplete_ThenCompletesWhenPacked()
        {
            var start = RepackStart();
            var result = Evaluate(start, RepackGoal());
            Assert.That(result.Failures.Single().InstanceId, Is.EqualTo("souvenir"));

            var packed = PuzzleTransitions.Apply(start, PuzzleMove.PlaceInSuitcase("souvenir", "main", new Cell(0, 1), Rotation.Degrees0)).State;
            Assert.That(Evaluate(packed, RepackGoal()).IsComplete, Is.True);
        }

        [Test]
        public void Repack_ChangedLayoutCompletes_OriginalPlacementNotRequired()
        {
            var state = RepackStart();
            state = PuzzleTransitions.Apply(state, PuzzleMove.MoveToStaging("old-a")).State;
            Assert.That(Kinds(Evaluate(state, RepackGoal())), Is.EqualTo(new[]
            {
                CompletionFailureKind.RequiredItemNotInSuitcase, CompletionFailureKind.RequiredItemNotInSuitcase, CompletionFailureKind.StagingNotEmpty
            }), "original item staged and souvenir outside");

            state = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("souvenir", "main", new Cell(0, 0), Rotation.Degrees0)).State;
            state = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("old-a", "main", new Cell(0, 2), Rotation.Degrees0)).State;
            Assert.That(Evaluate(state, RepackGoal()).IsComplete, Is.True);
            state.TryGetItem("old-a", out var moved);
            Assert.That(moved.Location.Placement.Anchor, Is.Not.EqualTo(new Cell(0, 0)));
        }

        [Test]
        public void Repack_NestedIncomingUnderSuitcaseParent_Counts_StagingAndRulesBlock()
        {
            var start = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Placed("old-b", Block("b", 1, 1), At(3, 0)),
                Item("socks-1", Block("socks", 1, 1), ItemLocation.SourceTray));
            var goal = PuzzleObjective.Repack(new[] { "shoe-1", "old-b" }, new[] { "socks-1" });
            var placed = PuzzleTransitions.Apply(start, PuzzleMove.PlaceInSuitcase("socks-1", "main", new Cell(0, 1), Rotation.Degrees0)).State;
            var nested = PuzzleTransitions.Apply(placed, PuzzleMove.NestInto("socks-1", "shoe-1")).State;
            Assert.That(Evaluate(nested, goal).IsComplete, Is.True);

            var staged = PuzzleTransitions.Apply(nested, PuzzleMove.MoveToStaging("old-b")).State;
            Assert.That(Kinds(Evaluate(staged, goal)), Is.EqualTo(new[] { CompletionFailureKind.RequiredItemNotInSuitcase, CompletionFailureKind.StagingNotEmpty }));

            var covered = PuzzleTransitions.Apply(nested, PuzzleMove.PlaceInSuitcase("old-b", "main", new Cell(0, 0), Rotation.Degrees0)).State;
            Assert.That(Kinds(Evaluate(covered, goal, AccessRule("shoe-1"))), Is.EqualTo(new[] { CompletionFailureKind.RuleViolation }));
        }

        // ---- Extract ----

        private static PuzzleState ExtractStart(ExtractionDestinationSpec destination = null) => State(
            new PuzzleSpec(Board(), 2, new[] { destination ?? new ExtractionDestinationSpec("tray", 1, acceptedInstanceIds: new[] { "laptop-a" }) }),
            Placed("laptop-a", Block("laptop", 1, 1), At(0, 0), ObjectiveRole.ExtractionTarget),
            Placed("laptop-b", Block("laptop", 1, 1), At(3, 2)),
            Placed("top", Block("top", 1, 1), At(0, 0, layer: 1)),
            Placed("cable", Block("cable", 1, 1), At(1, 0)));

        private static PuzzleObjective ExtractGoal() => PuzzleObjective.Extract("laptop-a", "tray");

        [Test]
        public void Extract_TargetInSuitcaseOrStaging_IsIncomplete()
        {
            var start = ExtractStart();
            Assert.That(Kinds(Evaluate(start, ExtractGoal())), Is.EqualTo(new[] { CompletionFailureKind.ExtractionTargetNotInDestination }));

            var cleared = PuzzleTransitions.Apply(start, PuzzleMove.MoveToStaging("top")).State;
            var staged = PuzzleTransitions.Apply(cleared, PuzzleMove.MoveToStaging("laptop-a")).State;
            Assert.That(Kinds(Evaluate(staged, ExtractGoal())), Is.EqualTo(new[]
            {
                CompletionFailureKind.ExtractionTargetNotInDestination, CompletionFailureKind.StagingNotEmpty, CompletionFailureKind.StagingNotEmpty
            }), "target in staging is not extraction");
        }

        [Test]
        public void Extract_TwinInDestination_DoesNotCount()
        {
            var start = ExtractStart(new ExtractionDestinationSpec("tray", 1, acceptedDefinitionIds: new[] { "laptop" }));
            var twin = PuzzleTransitions.Apply(start, PuzzleMove.MoveToDestination("laptop-b", "tray"));
            Assert.That(twin.IsAccepted, Is.True, "destination accepts the definition");
            Assert.That(Kinds(Evaluate(twin.State, ExtractGoal())), Is.EqualTo(new[] { CompletionFailureKind.ExtractionTargetNotInDestination }));
        }

        [Test]
        public void Extract_ExactTargetExtracted_CompletesWhileTwinStaysActive()
        {
            var start = ExtractStart();
            var rules = new RuleSet(Board(), new PuzzleRule[]
            {
                new AdjacencyRequiredRule("cable-near-target", ItemSelector.Instance("cable"), ItemSelector.Instance("laptop-a")),
                new AccessRule("laptops-accessible", ItemSelector.Definition("laptop"))
            });
            var state = PuzzleTransitions.Apply(start, PuzzleMove.MoveToStaging("top")).State;
            state = PuzzleTransitions.Apply(state, PuzzleMove.MoveToDestination("laptop-a", "tray")).State;
            state = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("top", "main", new Cell(2, 2), Rotation.Degrees0)).State;

            var result = Evaluate(state, ExtractGoal(), rules);
            Assert.That(result.IsComplete, Is.True, string.Join(", ", result.Failures.Select(f => f.ToString())));
            Assert.That(result.Rules.Single(r => r.RuleId == "laptops-accessible").SubjectIds, Is.EqualTo(new[] { "laptop-b" }),
                "the twin stays in the active domain and is still evaluated");
            Assert.That(CompletionEvaluator.IsInDestination(state, "laptop-a", "tray"), Is.True);
        }

        [Test]
        public void Extract_RemainingRuleOrInvariantViolation_Blocks()
        {
            var start = ExtractStart();
            var state = PuzzleTransitions.Apply(start, PuzzleMove.MoveToStaging("top")).State;
            state = PuzzleTransitions.Apply(state, PuzzleMove.MoveToDestination("laptop-a", "tray")).State;
            state = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("top", "main", new Cell(3, 2), Rotation.Degrees0)).State;
            Assert.That(Evaluate(state, ExtractGoal()).IsComplete, Is.True, "top stacked on the twin");

            var rules = new RuleSet(Board(), new PuzzleRule[] { new AccessRule("twin-accessible", ItemSelector.Instance("laptop-b")) });
            Assert.That(Evaluate(state, ExtractGoal(), rules).Failures.Single().RuleId, Is.EqualTo("twin-accessible"));

            state.TryGetItem("cable", out var cable);
            var broken = state.With(cable.With(ItemLocation.InSuitcase(At(3, 2))));
            Assert.That(Kinds(Evaluate(broken, ExtractGoal())), Is.EqualTo(new[] { CompletionFailureKind.InvariantViolation }));
        }

        // ---- Cross-cutting ----

        [Test]
        public void Evaluation_IsPureDeterministicAndOrdered()
        {
            var state = State(Spec(), Item("z", Block("z", 1, 1), ItemLocation.Staging), Item("b", Block("b", 1, 1), ItemLocation.SourceTray),
                Item("a", Block("a", 1, 1), ItemLocation.Staging), Placed("p", Block("p", 1, 1), At(0, 0)), Placed("q", Block("q", 1, 1), At(0, 0)));
            var bytes = state.ToStableBytes();
            var objective = PuzzleObjective.Pack(new[] { "z", "b", "a" });
            var first = Evaluate(state, objective);
            var second = Evaluate(state, PuzzleObjective.Pack(new[] { "a", "b", "z" }));

            Assert.That(first.Failures.Select(f => f.ToString()), Is.EqualTo(new[]
            {
                "RequiredItemNotInSuitcase a", "RequiredItemNotInSuitcase b", "RequiredItemNotInSuitcase z",
                "StagingNotEmpty a", "StagingNotEmpty z", "InvariantViolation "
            }));
            Assert.That(second.Failures.Select(f => f.ToString()), Is.EqualTo(first.Failures.Select(f => f.ToString())));
            Assert.That(state.ToStableBytes(), Is.EqualTo(bytes), "evaluation does not mutate the state");
        }

        [Test]
        public void MalformedObjectives_AreRejectedAtConstruction()
        {
            Assert.That(() => PuzzleObjective.Pack(new string[0]), Throws.ArgumentException.With.Message.StartsWith("MalformedObjective"));
            Assert.That(() => PuzzleObjective.Pack(new[] { "a", "a" }), Throws.ArgumentException.With.Message.StartsWith("MalformedObjective"));
            Assert.That(() => PuzzleObjective.Pack(new[] { " " }), Throws.ArgumentException.With.Message.StartsWith("MalformedObjective"));
            Assert.That(() => PuzzleObjective.Repack(new[] { "a" }, new string[0]), Throws.ArgumentException.With.Message.StartsWith("MalformedObjective"));
            Assert.That(() => PuzzleObjective.Repack(new[] { "a" }, new[] { "a" }), Throws.ArgumentException.With.Message.StartsWith("MalformedObjective"));
            Assert.That(() => PuzzleObjective.Extract(null, "tray"), Throws.ArgumentException.With.Message.StartsWith("MalformedObjective"));
            Assert.That(() => PuzzleObjective.Extract("laptop-a", ""), Throws.ArgumentException.With.Message.StartsWith("MalformedObjective"));

            var unknown = Evaluate(State(Spec()), PuzzleObjective.Pack(new[] { "ghost" }));
            Assert.That(unknown.Failures.Single().Kind, Is.EqualTo(CompletionFailureKind.RequiredItemNotInSuitcase), "evaluation never throws");
        }
    }
}
