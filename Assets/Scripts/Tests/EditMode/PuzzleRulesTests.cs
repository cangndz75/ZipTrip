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
    public sealed class PuzzleRulesTests
    {
        // Main 4x3, L = 2: row y = 0 is zone "top", row y = 2 is zone "bottom", row y = 1 has no zone.
        private static BoardSpec ZonedBoard() => new BoardSpec(new[]
        {
            new Compartment("main", 4, 3, 2, RectCells(4, 3),
                Enumerable.Range(0, 4).SelectMany(x => new[]
                {
                    new KeyValuePair<Cell, string>(new Cell(x, 0), "top"),
                    new KeyValuePair<Cell, string>(new Cell(x, 2), "bottom")
                })),
            new Compartment("pocket", 2, 1, 1, RectCells(2, 1))
        });

        private static PuzzleSpec ZonedSpec() => Spec(board: ZonedBoard());

        private static RuleResult Single(PuzzleState state, PuzzleRule rule) =>
            RuleEvaluator.Evaluate(state, new RuleSet(state.Spec.Board, new[] { rule })).Single();

        private static ItemSpec Shoe() => Block("shoe", 2, 1, nest: new NestSpec(1, new[] { "socks" }, null), tags: new[] { "shoes" });

        // ---- Zone ----

        [Test]
        public void Zone_FullFootprintInside_Passes()
        {
            var state = State(ZonedSpec(), Placed("shoe-1", Shoe(), At(1, 2)));
            var result = Single(state, new ZoneRule("z", ItemSelector.Instance("shoe-1"), "bottom"));
            Assert.That(result.IsSatisfied, Is.True);
            Assert.That(result.SubjectIds, Is.EqualTo(new[] { "shoe-1" }));
        }

        [Test]
        public void Zone_PartialFootprint_OrDifferentZone_Fails()
        {
            var partial = State(ZonedSpec(), Placed("book", Block("book", 1, 2), At(0, 1)));
            var result = Single(partial, new ZoneRule("z", ItemSelector.Instance("book"), "bottom"));
            Assert.That(result.IsSatisfied, Is.False, "touching the zone with part of the footprint is not enough");
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { "book" }));

            var other = State(ZonedSpec(), Placed("shoe-1", Shoe(), At(0, 0)));
            Assert.That(Single(other, new ZoneRule("z", ItemSelector.Instance("shoe-1"), "bottom")).IsSatisfied, Is.False);
        }

        [Test]
        public void Zone_AppliesToUpperLayerToo()
        {
            var state = State(ZonedSpec(), Placed("base", Block("book", 4, 1), At(0, 2)), Placed("top", Block("box", 1, 1), At(3, 2, layer: 1)));
            Assert.That(Single(state, new ZoneRule("z", ItemSelector.Instance("top"), "bottom")).IsSatisfied, Is.True);
        }

        [Test]
        public void Zone_NestedChildInheritsOutermostParent_ButNotFromStaging()
        {
            var placed = State(ZonedSpec(), Placed("shoe-1", Shoe(), At(0, 2)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            Assert.That(Single(placed, new ZoneRule("z", ItemSelector.Instance("socks-1"), "bottom")).IsSatisfied, Is.True);
            Assert.That(Single(placed, new ZoneRule("z", ItemSelector.Instance("socks-1"), "top")).IsSatisfied, Is.False);

            var staged = State(ZonedSpec(), Item("shoe-1", Shoe(), ItemLocation.InStaging(0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            var result = Single(staged, new ZoneRule("z", ItemSelector.Instance("socks-1"), "bottom"));
            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.SubjectIds, Is.EqualTo(new[] { "socks-1" }), "still in the active domain");
        }

        [Test]
        public void Zone_TagSelector_EverySubjectMustPass()
        {
            var state = State(ZonedSpec(), Placed("shoe-1", Shoe(), At(0, 2)), Placed("shoe-2", Shoe(), At(0, 1)));
            var result = Single(state, new ZoneRule("z", ItemSelector.Tag("shoes"), "bottom"));
            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.SubjectIds, Is.EqualTo(new[] { "shoe-1", "shoe-2" }));
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { "shoe-2" }));
        }

        [Test]
        public void Zone_IntermediateViolation_DoesNotAffectMoveLegality()
        {
            var state = State(ZonedSpec(), Item("shoe-1", Shoe(), ItemLocation.SourceTray));
            var moved = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("shoe-1", "main", new Cell(0, 0), Rotation.Degrees0));
            Assert.That(moved.IsAccepted, Is.True, "rules never reject a move");
            Assert.That(Single(moved.State, new ZoneRule("z", ItemSelector.Tag("shoes"), "bottom")).IsSatisfied, Is.False);
            Assert.That(BoardInvariants.Evaluate(moved.State).IsValid, Is.True);
        }

        // ---- Adjacency required ----

        [Test]
        public void AdjacencyRequired_AdjacentPasses_SeparatedOrDiagonalFails()
        {
            var rule = new AdjacencyRequiredRule("adj", ItemSelector.Instance("globe"), ItemSelector.Tag("soft"));
            var adjacent = State(Spec(), Placed("globe", Block("globe", 1, 1), At(0, 0)), Placed("towel", Block("towel", 1, 1, tags: new[] { "soft" }), At(1, 0)));
            Assert.That(Single(adjacent, rule).IsSatisfied, Is.True);

            var apart = State(Spec(), Placed("globe", Block("globe", 1, 1), At(0, 0)), Placed("towel", Block("towel", 1, 1, tags: new[] { "soft" }), At(3, 0)));
            Assert.That(Single(apart, rule).IsSatisfied, Is.False);

            var diagonal = State(Spec(), Placed("globe", Block("globe", 1, 1), At(0, 0)), Placed("towel", Block("towel", 1, 1, tags: new[] { "soft" }), At(1, 1)));
            Assert.That(Single(diagonal, rule).IsSatisfied, Is.False, "diagonal contact is not adjacency");
        }

        [Test]
        public void AdjacencyRequired_VerticalContactPasses()
        {
            var state = State(Spec(), Placed("sweater", Block("sweater", 3, 3, tags: new[] { "soft" }), At(0, 0)),
                Placed("globe", Block("globe", 1, 1), At(1, 1, layer: 1)));
            Assert.That(Single(state, new AdjacencyRequiredRule("adj", ItemSelector.Instance("globe"), ItemSelector.Tag("soft"))).IsSatisfied, Is.True);
        }

        [Test]
        public void AdjacencyRequired_SubjectCannotSatisfyItself()
        {
            var state = State(Spec(), Placed("t1", Block("towel", 1, 1, tags: new[] { "soft" }), At(0, 0)));
            var result = Single(state, new AdjacencyRequiredRule("adj", ItemSelector.Tag("soft"), ItemSelector.Tag("soft")));
            Assert.That(result.IsSatisfied, Is.False);

            var pair = State(Spec(), Placed("t1", Block("towel", 1, 1, tags: new[] { "soft" }), At(0, 0)),
                Placed("t2", Block("towel", 1, 1, tags: new[] { "soft" }), At(1, 0)));
            Assert.That(Single(pair, new AdjacencyRequiredRule("adj", ItemSelector.Tag("soft"), ItemSelector.Tag("soft"))).IsSatisfied, Is.True);
        }

        [Test]
        public void AdjacencyRequired_EverySubjectNeedsATarget()
        {
            var cable = Block("cable", 1, 1, tags: new[] { "tech" });
            var state = State(Spec(), Placed("bag", Block("bag", 1, 1), At(0, 0)), Placed("c1", cable, At(1, 0)), Placed("c2", cable, At(3, 2)));
            var result = Single(state, new AdjacencyRequiredRule("adj", ItemSelector.Definition("cable"), ItemSelector.Instance("bag")));
            Assert.That(result.SubjectIds, Is.EqualTo(new[] { "c1", "c2" }));
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { "c2" }));
        }

        // ---- Adjacency forbidden ----

        [Test]
        public void AdjacencyForbidden_SeparatedPasses_AdjacentPairFails()
        {
            var rule = new AdjacencyForbiddenRule("no", ItemSelector.Instance("shoes"), ItemSelector.Tag("clean"));
            var apart = State(Spec(), Placed("shoes", Block("shoes", 1, 1), At(0, 0)), Placed("shirt", Block("shirt", 1, 1, tags: new[] { "clean" }), At(2, 0)));
            Assert.That(Single(apart, rule).IsSatisfied, Is.True);

            var touching = State(Spec(), Placed("shoes", Block("shoes", 1, 1), At(0, 0)), Placed("shirt", Block("shirt", 1, 1, tags: new[] { "clean" }), At(0, 1)),
                Placed("towel", Block("towel", 1, 1, tags: new[] { "clean" }), At(3, 2)));
            var result = Single(touching, rule);
            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.OffendingIds, Is.EqualTo(new[] { "shoes" }));
            Assert.That(result.RelatedIds, Is.EqualTo(new[] { "shirt" }));
        }

        // ---- Access ----

        [Test]
        public void Access_AccessiblePasses_BlockedFails_NestedFollowsParent()
        {
            var rule = new AccessRule("acc", ItemSelector.Instance("passport"));
            var open = State(Spec(), Placed("passport", Block("passport", 1, 1), At(0, 0)));
            Assert.That(Single(open, rule).IsSatisfied, Is.True);

            var blocked = State(Spec(), Placed("passport", Block("passport", 1, 1), At(0, 0)), Placed("top", Block("box", 1, 1), At(0, 0, layer: 1)));
            Assert.That(Single(blocked, rule).IsSatisfied, Is.False);

            var pouch = Block("pouch", 1, 1, nest: new NestSpec(1, new[] { "passport" }, null));
            var nested = State(Spec(), Placed("pouch-1", pouch, At(0, 0)), Item("passport", Block("passport", 1, 1), ItemLocation.NestedIn("pouch-1")));
            Assert.That(Single(nested, rule).IsSatisfied, Is.True);
            var covered = State(Spec(), Placed("pouch-1", pouch, At(0, 0)), Item("passport", Block("passport", 1, 1), ItemLocation.NestedIn("pouch-1")),
                Placed("top", Block("box", 1, 1), At(0, 0, layer: 1)));
            Assert.That(Single(covered, rule).IsSatisfied, Is.False);
        }

        // ---- Active domain ----

        [Test]
        public void ExtractedExactTarget_LeavesDomain_TwinStaysActive()
        {
            var laptop = Block("laptop", 1, 1, tags: new[] { "tech" });
            var start = State(Spec(), Placed("laptop-a", laptop, At(0, 0), ObjectiveRole.ExtractionTarget),
                Placed("laptop-b", laptop, At(3, 2)), Placed("cable", Block("cable", 1, 1, tags: new[] { "tech" }), At(1, 0)));
            var rules = new RuleSet(start.Spec.Board, new PuzzleRule[]
            {
                new AdjacencyRequiredRule("cable-near-laptop", ItemSelector.Instance("cable"), ItemSelector.Definition("laptop")),
                new AdjacencyRequiredRule("laptop-near-cable", ItemSelector.Instance("laptop-a"), ItemSelector.Instance("cable")),
                new AccessRule("tech-access", ItemSelector.Tag("tech"))
            });

            var before = RuleEvaluator.Evaluate(start, rules);
            Assert.That(before.All(r => r.IsSatisfied), Is.True);

            var extracted = PuzzleTransitions.Apply(start, PuzzleMove.MoveToDestination("laptop-a", "tray")).State;
            Assert.That(RuleEvaluator.GetActiveDomain(extracted).Select(i => i.InstanceId), Is.EqualTo(new[] { "cable", "laptop-b" }));

            var after = RuleEvaluator.Evaluate(extracted, rules).ToDictionary(r => r.RuleId);
            Assert.That(after["laptop-near-cable"].IsSatisfied, Is.True, "subject left the domain");
            Assert.That(after["laptop-near-cable"].SubjectIds, Is.Empty);
            Assert.That(after["cable-near-laptop"].IsSatisfied, Is.False, "the twin is still active but not adjacent");
            Assert.That(after["tech-access"].SubjectIds, Is.EqualTo(new[] { "cable", "laptop-b" }));
        }

        [Test]
        public void AdjacencyRequired_TargetThatLeftTheDomain_NoLongerHasToBeMet()
        {
            var laptop = Block("laptop", 1, 1, tags: new[] { "tech" });
            var start = State(Spec(), Placed("laptop-a", laptop, At(0, 0), ObjectiveRole.ExtractionTarget),
                Placed("cable", Block("cable", 1, 1), At(1, 0)), Placed("charger", Block("charger", 1, 1), At(3, 2)));
            var rules = new RuleSet(start.Spec.Board, new PuzzleRule[]
            {
                new AdjacencyRequiredRule("cable-near-target", ItemSelector.Instance("cable"), ItemSelector.Instance("laptop-a")),
                new AdjacencyRequiredRule("charger-near-tech", ItemSelector.Instance("charger"), ItemSelector.Tag("tech")),
                new AdjacencyRequiredRule("charger-near-nothing", ItemSelector.Instance("charger"), ItemSelector.Tag("missing"))
            });
            var before = RuleEvaluator.Evaluate(start, rules).ToDictionary(r => r.RuleId);
            Assert.That(before["cable-near-target"].IsSatisfied, Is.True);
            Assert.That(before["charger-near-tech"].IsSatisfied, Is.False);

            var extracted = PuzzleTransitions.Apply(start, PuzzleMove.MoveToDestination("laptop-a", "tray")).State;
            var after = RuleEvaluator.Evaluate(extracted, rules).ToDictionary(r => r.RuleId);
            Assert.That(after["cable-near-target"].IsSatisfied, Is.True, "Decision 16: extraction must not make the rule impossible");
            Assert.That(after["charger-near-tech"].IsSatisfied, Is.True, "every tech target left the domain");
            Assert.That(after["charger-near-nothing"].IsSatisfied, Is.False, "a selector that never matched anything is still unmet");
        }

        // ---- Authoring validation / determinism ----

        [Test]
        public void MalformedRules_AreRejectedAtConstruction()
        {
            Assert.That(() => ItemSelector.Tag(" "), Throws.ArgumentException.With.Message.StartsWith("MalformedSelector"));
            Assert.That(() => new AccessRule("a", default), Throws.ArgumentException.With.Message.StartsWith("MalformedSelector"));
            Assert.That(() => new AdjacencyRequiredRule("a", ItemSelector.Tag("x"), default), Throws.ArgumentException.With.Message.StartsWith("MalformedSelector"));
            Assert.That(() => new AccessRule("", ItemSelector.Tag("x")), Throws.ArgumentException.With.Message.StartsWith("MissingRuleId"));
            Assert.That(() => new ZoneRule("z", ItemSelector.Tag("x"), null), Throws.ArgumentException.With.Message.StartsWith("MissingZoneId"));
            Assert.That(() => new RuleSet(ZonedBoard(), new[] { new ZoneRule("z", ItemSelector.Tag("x"), "lid") }),
                Throws.ArgumentException.With.Message.StartsWith("UnknownZone"));
            Assert.That(() => new RuleSet(ZonedBoard(), new PuzzleRule[] { new AccessRule("a", ItemSelector.Tag("x")), new AccessRule("a", ItemSelector.Tag("y")) }),
                Throws.ArgumentException.With.Message.StartsWith("DuplicateRuleId"));
        }

        [Test]
        public void ResultOrdering_IsIndependentOfAuthoringAndInputOrder()
        {
            var rules = new PuzzleRule[]
            {
                new ZoneRule("z-rule", ItemSelector.Tag("shoes"), "bottom"),
                new AccessRule("a-rule", ItemSelector.Tag("shoes")),
                new AdjacencyForbiddenRule("m-rule", ItemSelector.Tag("shoes"), ItemSelector.Tag("shoes"))
            };
            var items = new[] { Placed("shoe-2", Shoe(), At(2, 2)), Placed("shoe-1", Shoe(), At(0, 2)) };
            var a = RuleEvaluator.Evaluate(new PuzzleState(ZonedSpec(), items), new RuleSet(ZonedBoard(), rules));
            var b = RuleEvaluator.Evaluate(new PuzzleState(ZonedSpec(), items.Reverse()), new RuleSet(ZonedBoard(), rules.Reverse()));
            Assert.That(a.Select(r => r.RuleId), Is.EqualTo(new[] { "a-rule", "m-rule", "z-rule" }));
            Assert.That(b.Select(r => r.RuleId + string.Join(",", r.SubjectIds) + r.IsSatisfied + string.Join(",", r.OffendingIds)),
                Is.EqualTo(a.Select(r => r.RuleId + string.Join(",", r.SubjectIds) + r.IsSatisfied + string.Join(",", r.OffendingIds))));
            Assert.That(a.Single(r => r.RuleId == "m-rule").OffendingIds, Is.EqualTo(new[] { "shoe-1", "shoe-2" }));
        }
    }
}
