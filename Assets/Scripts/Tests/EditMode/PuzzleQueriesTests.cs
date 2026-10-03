using NUnit.Framework;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PuzzleQueriesTests
    {
        // ---- Blockers / access ----

        [Test]
        public void ItemWithNothingAbove_HasNoBlockersAndIsAccessible()
        {
            var state = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Placed("side", Block("box", 1, 1), At(3, 0)));
            Assert.That(AccessQueries.GetBlockers(state, "base"), Is.Empty);
            Assert.That(AccessQueries.GetAccess(state, "base"), Is.EqualTo(ItemAccess.Accessible));
            Assert.That(AccessQueries.IsAccessible(state, "base"), Is.True);
        }

        [Test]
        public void OneAndMultipleBlockers_AreReportedInIdOrder()
        {
            var one = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Placed("top", Block("box", 1, 1), At(1, 1, layer: 1)));
            Assert.That(AccessQueries.GetBlockers(one, "base"), Is.EqualTo(new[] { "top" }));
            Assert.That(AccessQueries.GetAccess(one, "base"), Is.EqualTo(ItemAccess.Blocked));
            Assert.That(AccessQueries.GetBlockers(one, "top"), Is.Empty, "lower items never block");

            var many = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)),
                Placed("z2", Block("box", 1, 1), At(1, 1, layer: 1)), Placed("a1", Block("cup", 1, 1), At(0, 0, layer: 1)));
            Assert.That(AccessQueries.GetBlockers(many, "base"), Is.EqualTo(new[] { "a1", "z2" }));
        }

        [Test]
        public void RemovingTheBlocker_ChangesTheDerivedResult()
        {
            var blocked = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Placed("top", Block("box", 1, 1), At(1, 1, layer: 1)));
            blocked.TryGetItem("top", out var top);
            var cleared = blocked.With(top.With(ItemLocation.Staging));
            Assert.That(AccessQueries.IsAccessible(blocked, "base"), Is.False);
            Assert.That(AccessQueries.IsAccessible(cleared, "base"), Is.True);
        }

        [Test]
        public void XYOverlapInAnotherCompartment_DoesNotBlock()
        {
            var state = State(Spec(), Placed("main", Block("book", 1, 1), At(0, 0)), Placed("pocket", Block("box", 1, 1), At(0, 0, compartment: "pocket")));
            Assert.That(AccessQueries.GetBlockers(state, "main"), Is.Empty);
        }

        [Test]
        public void NestedChild_FollowsParentAccess()
        {
            var shoe = Block("shoe", 2, 2, nest: new NestSpec(1, new[] { "socks" }, null));
            var open = State(Spec(), Placed("shoe-1", shoe, At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            Assert.That(AccessQueries.GetAccess(open, "socks-1"), Is.EqualTo(ItemAccess.Accessible));

            var covered = State(Spec(), Placed("shoe-1", shoe, At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")),
                Placed("top", Block("box", 1, 1), At(0, 0, layer: 1)));
            Assert.That(AccessQueries.GetAccess(covered, "socks-1"), Is.EqualTo(ItemAccess.ParentBlocked));
            Assert.That(AccessQueries.CanTake(covered, "socks-1"), Is.False);

            var staged = State(Spec(), Item("shoe-1", shoe, ItemLocation.Staging), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            Assert.That(AccessQueries.GetAccess(staged, "socks-1"), Is.EqualTo(ItemAccess.Accessible));
        }

        [Test]
        public void ExternalAndTerminalLocations_AreDistinctFromAccessible()
        {
            var state = State(Spec(), Item("t", Block("shirt", 1, 1), ItemLocation.SourceTray), Item("s", Block("book", 1, 1), ItemLocation.Staging),
                Item("d", Block("laptop", 1, 1), ItemLocation.InDestination("tray"), ObjectiveRole.ExtractionTarget));
            Assert.That(AccessQueries.GetAccess(state, "t"), Is.EqualTo(ItemAccess.External));
            Assert.That(AccessQueries.GetAccess(state, "s"), Is.EqualTo(ItemAccess.External));
            Assert.That(AccessQueries.IsAccessible(state, "t"), Is.False, "not in the suitcase");
            Assert.That(AccessQueries.CanTake(state, "t"), Is.True);
            Assert.That(AccessQueries.GetAccess(state, "d"), Is.EqualTo(ItemAccess.Terminal));
            Assert.That(AccessQueries.CanTake(state, "d"), Is.False);
            Assert.That(AccessQueries.GetAccess(state, "ghost"), Is.EqualTo(ItemAccess.Unknown));
        }

        [Test]
        public void Thickness2Item_SpansBothLayers_HasNoBlockersAndTouchesLaterally()
        {
            var state = State(Spec(), Placed("j", Jacket(), At(0, 0)), Placed("side", Block("box", 1, 1), At(2, 0)));
            Assert.That(AccessQueries.GetBlockers(state, "j"), Is.Empty);
            Assert.That(AdjacencyQueries.GetContact(state, "j", "side"), Is.EqualTo(AdjacencyKind.Lateral));
        }

        // ---- Adjacency ----

        [Test]
        public void LateralAdjacency_IsOrthogonalOnly()
        {
            var state = State(Spec(), Placed("a", Block("a", 1, 1), At(0, 0)), Placed("b", Block("b", 1, 1), At(1, 0)),
                Placed("diag", Block("d", 1, 1), At(1, 1)), Placed("far", Block("f", 1, 1), At(3, 2)));
            Assert.That(AdjacencyQueries.GetContact(state, "a", "b"), Is.EqualTo(AdjacencyKind.Lateral));
            Assert.That(AdjacencyQueries.AreAdjacent(state, "a", "diag"), Is.False, "no diagonal adjacency");
            Assert.That(AdjacencyQueries.AreAdjacent(state, "a", "far"), Is.False);
            Assert.That(AdjacencyQueries.GetNeighbours(state, "diag"), Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void LateralAdjacency_RequiresTheSameZ()
        {
            // "high" sits on "base" at z = 1 next to "low" at z = 0: vertical with base, not lateral with low.
            var state = State(Spec(), Placed("base", Block("book", 1, 1), At(0, 0)), Placed("high", Block("box", 1, 1), At(0, 0, layer: 1)),
                Placed("low", Block("cup", 1, 1), At(1, 0)));
            Assert.That(AdjacencyQueries.GetContact(state, "high", "low"), Is.EqualTo(AdjacencyKind.None));
            Assert.That(AdjacencyQueries.GetContact(state, "base", "low"), Is.EqualTo(AdjacencyKind.Lateral));
        }

        [Test]
        public void VerticalAdjacency_SnowGlobeOnSweater()
        {
            var state = State(Spec(), Placed("sweater", Sweater(), At(0, 0)), Placed("globe", Block("globe", 1, 1), At(1, 1, layer: 1)));
            Assert.That(AdjacencyQueries.GetContact(state, "globe", "sweater"), Is.EqualTo(AdjacencyKind.Vertical));
            Assert.That(AdjacencyQueries.GetContact(state, "sweater", "globe"), Is.EqualTo(AdjacencyKind.Vertical), "symmetric");
        }

        [Test]
        public void DifferentCompartments_AndNestedItems_AreNotAdjacent()
        {
            var shoe = Block("shoe", 1, 1, nest: new NestSpec(1, new[] { "socks" }, null));
            var state = State(Spec(), Placed("a", Block("a", 2, 1), At(0, 0)), Placed("p", Block("p", 1, 1), At(0, 0, compartment: "pocket")),
                Placed("shoe-1", shoe, At(2, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            Assert.That(AdjacencyQueries.AreAdjacent(state, "a", "p"), Is.False);
            Assert.That(AdjacencyQueries.AreAdjacent(state, "shoe-1", "socks-1"), Is.False, "containment is not adjacency");
            Assert.That(AdjacencyQueries.AreAdjacent(state, "a", "socks-1"), Is.False);
            Assert.That(AdjacencyQueries.GetNeighbours(state, "a"), Is.EqualTo(new[] { "shoe-1" }));
        }

        [Test]
        public void QueryOrdering_IsDeterministic()
        {
            var items = new[]
            {
                Placed("m", Block("m", 1, 1), At(1, 1)), Placed("c", Block("c", 1, 1), At(0, 1)),
                Placed("x", Block("x", 1, 1), At(2, 1)), Placed("a", Block("a", 1, 1), At(1, 0))
            };
            var forward = new PuzzleState(Spec(), items);
            var reversed = new PuzzleState(Spec(), new[] { items[3], items[2], items[1], items[0] });
            Assert.That(AdjacencyQueries.GetNeighbours(forward, "m"), Is.EqualTo(new[] { "a", "c", "x" }));
            Assert.That(AdjacencyQueries.GetNeighbours(reversed, "m"), Is.EqualTo(AdjacencyQueries.GetNeighbours(forward, "m")));
        }
    }
}
