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
    /// <summary>Cross-ticket regressions for the dynamic puzzle kernel (ZT-029..ZT-033 audit).</summary>
    public sealed class PuzzleKernelTests
    {
        private static ItemSpec Shoe() => Block("shoe", 2, 1, nest: new NestSpec(2, new[] { "socks" }, new[] { "small" }), tags: new[] { "shoes" });
        private static ItemSpec Socks() => Block("socks", 1, 1, tags: new[] { "small" });

        private static PuzzleItem Get(PuzzleState state, string id)
        {
            state.TryGetItem(id, out var item);
            return item;
        }

        private static void AssertRejectedUnchanged(PuzzleState state, PuzzleMove move, MoveRejection reason)
        {
            var bytes = state.ToStableBytes();
            var before = state.Items.Select(i => (i.InstanceId, i.StateId, i.Location)).ToArray();
            MoveResult result = null;
            Assert.That(() => result = PuzzleTransitions.Apply(state, move), Throws.Nothing, move?.ToString());
            Assert.That(result.Rejection, Is.EqualTo(reason), move?.ToString());
            Assert.That(result.State, Is.SameAs(state));
            Assert.That(state.ToStableBytes(), Is.EqualTo(bytes));
            Assert.That(state.Items.Select(i => (i.InstanceId, i.StateId, i.Location)), Is.EqualTo(before));
        }

        // ---- C. Thickness + support ----

        [Test]
        public void ThickItemOnEmptyFloor_DoesNotNeedSupportForItsOwnUpperCells()
        {
            var state = State(Spec(), Placed("j", Jacket(), At(0, 0)));
            Assert.That(PuzzleState.GetPhysicalCells(Get(state, "j")).Count(c => c.Z == 1), Is.EqualTo(4));
            Assert.That(BoardInvariants.Evaluate(state).IsValid, Is.True);
        }

        [Test]
        public void NestedChildren_ProvideNoSupport()
        {
            // The shoe is staged; its nested socks occupy no cells, so nothing supports "top" at (0, 0, 1).
            var state = State(Spec(), Item("shoe-1", Shoe(), ItemLocation.Staging), Item("socks-1", Socks(), ItemLocation.NestedIn("shoe-1")),
                Placed("top", Block("box", 1, 1), At(0, 0, layer: 1)));
            var violation = BoardInvariants.Evaluate(state).Violations.Single();
            Assert.That(violation.Kind, Is.EqualTo(InvariantKind.Unsupported));
            Assert.That(violation.InstanceId, Is.EqualTo("top"));
        }

        // ---- D. Relocation self-collision ----

        [Test]
        public void Relocation_OverlappingItsOwnOldFootprint_IsLegal()
        {
            var state = State(Spec(), Placed("rod", Block("rod", 3, 1), At(0, 0)));
            var result = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("rod", "main", new Cell(1, 0), Rotation.Degrees0));
            Assert.That(result.IsAccepted, Is.True);
            Assert.That(Get(result.State, "rod").Location.Placement.Layer, Is.EqualTo(0), "no self-collision lift to layer 1");
        }

        [Test]
        public void ParentRelocation_OverlappingOldFootprint_CarriesDescendants()
        {
            var state = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Item("socks-1", Socks(), ItemLocation.NestedIn("shoe-1")),
                Item("socks-2", Socks(), ItemLocation.NestedIn("shoe-1")));
            var result = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("shoe-1", "main", new Cell(1, 0), Rotation.Degrees0));
            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.MovedInstanceIds, Is.EqualTo(new[] { "shoe-1", "socks-1", "socks-2" }));
            Assert.That(result.State.GetChildren("shoe-1").Select(i => i.InstanceId), Is.EqualTo(new[] { "socks-1", "socks-2" }));
            Assert.That(PuzzleState.GetPhysicalCells(Get(result.State, "socks-1")), Is.Empty);
        }

        // ---- E. Failed move atomicity ----

        [Test]
        public void StagedItem_ValidFoldTargetButIllegalPlacement_StaysStagedInOriginalState()
        {
            var state = State(Spec(), Item("sw", Sweater(), ItemLocation.Staging), Placed("base", Block("book", 1, 1), At(0, 0)));
            Assert.That(PuzzleTransitions.GetSelectableStates(Get(state, "sw")), Has.Member("folded"));
            AssertRejectedUnchanged(state, PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 0), Rotation.Degrees90, "folded"),
                MoveRejection.NoLegalLayer);
            Assert.That(Get(state, "sw").StateId, Is.EqualTo("open"));
            Assert.That(Get(state, "sw").Location.Kind, Is.EqualTo(ItemLocationKind.Staging));
        }

        [Test]
        public void ExpectedInvalidPlayerActions_ReturnRejectionsWithoutThrowing()
        {
            var state = State(Spec(stagingCapacity: 1),
                Placed("shoe-1", Shoe(), At(0, 0)), Item("socks-1", Socks(), ItemLocation.NestedIn("shoe-1")),
                Placed("base", Block("book", 2, 1), At(2, 0)), Placed("top", Block("box", 1, 1), At(2, 0, layer: 1)),
                Item("staged", Block("cup", 1, 1), ItemLocation.Staging),
                Item("tray-1", Block("shirt", 1, 1), ItemLocation.SourceTray),
                Placed("laptop", Block("laptop", 1, 1), At(0, 2), ObjectiveRole.ExtractionTarget),
                Item("cam", Block("camera", 1, 1), ItemLocation.InDestination("tray"), ObjectiveRole.ExtractionTarget));

            AssertRejectedUnchanged(state, PuzzleMove.MoveToStaging("base"), MoveRejection.NotAccessible);
            AssertRejectedUnchanged(state, PuzzleMove.PlaceInSuitcase("tray-1", "main", new Cell(4, 0), Rotation.Degrees0), MoveRejection.NoLegalLayer);
            AssertRejectedUnchanged(state, PuzzleMove.NestInto("tray-1", "shoe-1"), MoveRejection.UnsupportedRoute);
            AssertRejectedUnchanged(state, PuzzleMove.NestInto("top", "shoe-1"), MoveRejection.InvariantViolation);
            AssertRejectedUnchanged(state, PuzzleMove.NestInto("shoe-1", "socks-1"), MoveRejection.NestCycle);
            AssertRejectedUnchanged(state, PuzzleMove.MoveToStaging("top"), MoveRejection.InvariantViolation);
            AssertRejectedUnchanged(state, PuzzleMove.MoveToDestination("laptop", "tray"), MoveRejection.InvariantViolation);
            AssertRejectedUnchanged(state, PuzzleMove.MoveToStaging("cam"), MoveRejection.InDestination);
            AssertRejectedUnchanged(state, PuzzleMove.PlaceInSuitcase("staged", "main", new Cell(9, 9), Rotation.Degrees0), MoveRejection.NoLegalLayer);
            AssertRejectedUnchanged(state, PuzzleMove.PlaceInSuitcase("staged", null, new Cell(0, 0), Rotation.Degrees0), MoveRejection.UnknownCompartment);
            AssertRejectedUnchanged(state, PuzzleMove.PlaceInSuitcase("staged", "main", new Cell(3, 2), (Rotation)45), MoveRejection.RotationNotAllowed);
            AssertRejectedUnchanged(state, PuzzleMove.PlaceInSuitcase(null, "main", new Cell(0, 0), Rotation.Degrees0), MoveRejection.UnknownItem);
            AssertRejectedUnchanged(state, null, MoveRejection.UnknownItem);
            Assert.That(() => LayerResolver.ResolveLowestLegalLayer(state, null, null, null, new Cell(0, 0), Rotation.Degrees0), Throws.Nothing);
        }

        // ---- F. Nest transitions ----

        [Test]
        public void IndirectNestCycle_IsRejectedNotThrown()
        {
            var bag = Block("bag", 2, 2, nest: new NestSpec(1, new[] { "shoe" }, null));
            var state = State(Spec(), Placed("bag-1", bag, At(0, 0)), Item("shoe-1", Shoe(), ItemLocation.NestedIn("bag-1")),
                Item("socks-1", Block("socks", 1, 1, nest: new NestSpec(1, new[] { "bag" }, null), tags: new[] { "small" }), ItemLocation.NestedIn("shoe-1")));
            AssertRejectedUnchanged(state, PuzzleMove.NestInto("bag-1", "socks-1"), MoveRejection.NestCycle);
            AssertRejectedUnchanged(state, PuzzleMove.NestInto("bag-1", "shoe-1"), MoveRejection.NestCycle);
        }

        [Test]
        public void NestedChild_MovesToAnotherCompatibleParent_AndDetaches()
        {
            var state = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Placed("shoe-2", Shoe(), At(0, 2)),
                Item("socks-1", Socks(), ItemLocation.NestedIn("shoe-1")));
            var moved = PuzzleTransitions.Apply(state, PuzzleMove.NestInto("socks-1", "shoe-2"));
            Assert.That(moved.IsAccepted, Is.True);
            Assert.That(moved.State.GetChildren("shoe-1"), Is.Empty);
            Assert.That(moved.State.GetChildren("shoe-2").Single().InstanceId, Is.EqualTo("socks-1"));

            var detached = PuzzleTransitions.Apply(moved.State, PuzzleMove.PlaceInSuitcase("socks-1", "main", new Cell(3, 0), Rotation.Degrees0));
            Assert.That(detached.State.GetChildren("shoe-2"), Is.Empty);
            Assert.That(PuzzleState.GetPhysicalCells(Get(detached.State, "socks-1")).Count, Is.EqualTo(1));
        }

        [Test]
        public void NestedChild_AccessFollowsParent_AdjacencyDoesNot_ZoneUsesOutermostParent()
        {
            var board = new BoardSpec(new[]
            {
                new Compartment("main", 4, 3, 2, RectCells(4, 3),
                    Enumerable.Range(0, 4).Select(x => new KeyValuePair<Cell, string>(new Cell(x, 2), "bottom")))
            });
            var spec = new PuzzleSpec(board, 2, new[] { Tray() });
            var towel = Block("towel", 1, 1, tags: new[] { "soft" });
            var state = State(spec, Placed("shoe-1", Shoe(), At(0, 2)), Item("socks-1", Socks(), ItemLocation.NestedIn("shoe-1")),
                Placed("towel-1", towel, At(2, 2)));
            var rules = new RuleSet(board, new PuzzleRule[]
            {
                new AdjacencyRequiredRule("shoe-touches-towel", ItemSelector.Instance("shoe-1"), ItemSelector.Tag("soft")),
                new AdjacencyRequiredRule("socks-touch-towel", ItemSelector.Instance("socks-1"), ItemSelector.Tag("soft")),
                new ZoneRule("socks-bottom", ItemSelector.Instance("socks-1"), "bottom"),
                new AccessRule("socks-access", ItemSelector.Instance("socks-1"))
            });

            var results = RuleEvaluator.Evaluate(state, rules).ToDictionary(r => r.RuleId);
            Assert.That(results["shoe-touches-towel"].IsSatisfied, Is.True);
            Assert.That(results["socks-touch-towel"].IsSatisfied, Is.False, "containment does not inherit physical contact");
            Assert.That(AdjacencyQueries.GetNeighbours(state, "socks-1"), Is.Empty);
            Assert.That(results["socks-bottom"].IsSatisfied, Is.True, "zone uses the outermost placed parent");
            Assert.That(results["socks-access"].IsSatisfied, Is.True, "access follows the accessible parent");

            var staged = PuzzleTransitions.Apply(state, PuzzleMove.MoveToStaging("shoe-1")).State;
            var after = RuleEvaluator.Evaluate(staged, rules).ToDictionary(r => r.RuleId);
            Assert.That(AccessQueries.GetAccess(staged, "socks-1"), Is.EqualTo(ItemAccess.External));
            Assert.That(after["socks-access"].IsSatisfied, Is.False, "access rule is a suitcase notion; parent is staged");
            Assert.That(after["socks-bottom"].IsSatisfied, Is.False, "external parent: no zone");
        }

        // ---- H / I. Blockers and adjacency edges ----

        [Test]
        public void UpperNonOverlappingItem_DoesNotBlock_AndNoItemTouchesItself()
        {
            var state = State(Spec(), Placed("base", Block("book", 2, 1), At(0, 0)), Placed("other", Block("cup", 1, 1), At(2, 0)),
                Placed("top", Block("box", 1, 1), At(2, 0, layer: 1)));
            Assert.That(AccessQueries.GetBlockers(state, "base"), Is.Empty);
            Assert.That(AccessQueries.GetBlockers(state, "other"), Is.EqualTo(new[] { "top" }));
            Assert.That(AdjacencyQueries.GetContact(state, "base", "base"), Is.EqualTo(AdjacencyKind.None));
            Assert.That(AdjacencyQueries.GetNeighbours(state, "top"), Does.Not.Contain("top"));
            Assert.That(AdjacencyQueries.GetContact(state, "base", "top"), Is.EqualTo(AdjacencyKind.None), "diagonal across layers");
        }

        [Test]
        public void ThickItem_TouchesUpperLayerNeighbourLaterally()
        {
            var state = State(Spec(), Placed("j", Jacket(), At(0, 0)), Placed("base", Block("book", 1, 1), At(2, 0)),
                Placed("top", Block("box", 1, 1), At(2, 0, layer: 1)));
            Assert.That(AdjacencyQueries.GetContact(state, "j", "top"), Is.EqualTo(AdjacencyKind.Lateral), "z = 1 cells touch");
            Assert.That(AdjacencyQueries.GetContact(state, "base", "top"), Is.EqualTo(AdjacencyKind.Vertical));
        }

        // ---- A. Canonical staging ----

        [Test]
        public void Staging_IsAnUnorderedSet_InCanonicalBytes()
        {
            var a = State(Spec(), Placed("x", Block("x", 1, 1), At(0, 0)), Placed("y", Block("y", 1, 1), At(1, 0)));
            var xFirst = PuzzleTransitions.Apply(PuzzleTransitions.Apply(a, PuzzleMove.MoveToStaging("x")).State, PuzzleMove.MoveToStaging("y")).State;
            var yFirst = PuzzleTransitions.Apply(PuzzleTransitions.Apply(a, PuzzleMove.MoveToStaging("y")).State, PuzzleMove.MoveToStaging("x")).State;
            Assert.That(yFirst.ToStableBytes(), Is.EqualTo(xFirst.ToStableBytes()));
        }

        // ---- L. Cross-ticket integration ----

        [Test]
        public void Integration_PlaceStackRuleRejectRelocate()
        {
            // 1-3. Board with a "bottom" row, items in the Source Tray.
            var board = new BoardSpec(new[]
            {
                new Compartment("main", 4, 3, 2, RectCells(4, 3),
                    Enumerable.Range(0, 4).Select(x => new KeyValuePair<Cell, string>(new Cell(x, 2), "bottom")))
            });
            var spec = new PuzzleSpec(board, 1, new[] { Tray() });
            var sweater = Block("sweater", 2, 2, tags: new[] { "soft" });
            var globe = Block("globe", 1, 1, tags: new[] { "fragile" });
            var state = State(spec, Item("sweater-1", sweater, ItemLocation.SourceTray), Item("globe-1", globe, ItemLocation.SourceTray));
            var rules = new RuleSet(board, new PuzzleRule[]
            {
                new AdjacencyRequiredRule("globe-on-soft", ItemSelector.Tag("fragile"), ItemSelector.Tag("soft")),
                new ZoneRule("sweater-bottom", ItemSelector.Instance("sweater-1"), "bottom")
            });

            // 4. Lower item.
            state = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("sweater-1", "main", new Cell(0, 0), Rotation.Degrees0)).State;
            // 5. Upper item resolves to layer 1.
            var upper = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("globe-1", "main", new Cell(1, 1), Rotation.Degrees0));
            Assert.That(upper.IsAccepted, Is.True);
            state = upper.State;
            Assert.That(Get(state, "globe-1").Location.Placement.Layer, Is.EqualTo(1));
            // 6. Blocker / access.
            Assert.That(AccessQueries.GetBlockers(state, "sweater-1"), Is.EqualTo(new[] { "globe-1" }));
            Assert.That(AccessQueries.IsAccessible(state, "globe-1"), Is.True);
            // 7. Rules: adjacency satisfied, zone not (sweater rows 0..1).
            var results = RuleEvaluator.Evaluate(state, rules).ToDictionary(r => r.RuleId);
            Assert.That(results["globe-on-soft"].IsSatisfied, Is.True);
            Assert.That(results["sweater-bottom"].IsSatisfied, Is.False);
            Assert.That(BoardInvariants.Evaluate(state).IsValid, Is.True, "rule failure is not an invariant failure");
            // 8. Invalid move: blocked sweater cannot move; state untouched.
            AssertRejectedUnchanged(state, PuzzleMove.PlaceInSuitcase("sweater-1", "main", new Cell(0, 1), Rotation.Degrees0), MoveRejection.NotAccessible);
            // 9. Legal relocation: globe to staging, sweater down into the bottom row, globe back on top.
            state = PuzzleTransitions.Apply(state, PuzzleMove.MoveToStaging("globe-1")).State;
            var relocated = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("sweater-1", "main", new Cell(0, 1), Rotation.Degrees0));
            Assert.That(relocated.IsAccepted, Is.True, "overlaps its old footprint");
            state = PuzzleTransitions.Apply(relocated.State, PuzzleMove.PlaceInSuitcase("globe-1", "main", new Cell(0, 1), Rotation.Degrees0)).State;
            // 10. Rules again: sweater rows 1..2 is still not fully in bottom; adjacency holds.
            results = RuleEvaluator.Evaluate(state, rules).ToDictionary(r => r.RuleId);
            Assert.That(results["globe-on-soft"].IsSatisfied, Is.True);
            Assert.That(results["sweater-bottom"].IsSatisfied, Is.False, "partial footprint in zone");
            Assert.That(state.GetItems(ItemLocationKind.Staging), Is.Empty);
        }
    }
}
