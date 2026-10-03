using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PuzzleTransitionsTests
    {
        private static ItemSpec Shoe() => Block("shoe", 1, 2, nest: new NestSpec(1, new[] { "socks" }, null));

        private static MoveResult AssertRejected(PuzzleState state, PuzzleMove move, MoveRejection reason)
        {
            var result = PuzzleTransitions.Apply(state, move);
            Assert.That(result.Rejection, Is.EqualTo(reason), move.ToString());
            Assert.That(result.State, Is.SameAs(state));
            Assert.That(result.State.Hash, Is.EqualTo(state.Hash), "rejected move keeps the canonical hash");
            return result;
        }

        private static PuzzleState AssertAccepted(PuzzleState state, PuzzleMove move)
        {
            var result = PuzzleTransitions.Apply(state, move);
            Assert.That(result.Rejection, Is.EqualTo(MoveRejection.None), move + " " + string.Join(", ", result.Report?.Violations.Select(v => v.ToString()) ?? new string[0]));
            Assert.That(result.State.Hash, Is.Not.EqualTo(state.Hash));
            return result.State;
        }

        private static PuzzleItem Get(PuzzleState state, string id)
        {
            state.TryGetItem(id, out var item);
            return item;
        }

        // ---- 1. Source Tray -> suitcase ----

        [Test]
        public void SourceTrayToSuitcase_AcceptedWithResolvedLayerAndRotation()
        {
            var state = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Item("rod", Block("rod", 2, 1), ItemLocation.SourceTray));
            var next = AssertAccepted(state, PuzzleMove.PlaceInSuitcase("rod", "main", new Cell(1, 0), Rotation.Degrees90));
            var placement = Get(next, "rod").Location.Placement;
            Assert.That(placement.Layer, Is.EqualTo(1), "resolver chose the supported upper layer");
            Assert.That(placement.Rotation, Is.EqualTo(Rotation.Degrees90), "rotation is part of the one move");
            Assert.That(Get(state, "rod").Location.Kind, Is.EqualTo(ItemLocationKind.SourceTray), "original state is immutable");
        }

        [Test]
        public void SourceTrayToSuitcase_RejectedWhenNoLayerIsLegal()
        {
            var state = State(Spec(), Placed("base", Block("book", 1, 1), At(0, 0)), Item("rod", Block("rod", 2, 1), ItemLocation.SourceTray));
            var result = AssertRejected(state, PuzzleMove.PlaceInSuitcase("rod", "main", new Cell(0, 0), Rotation.Degrees0), MoveRejection.NoLegalLayer);
            Assert.That(result.Report.Violations.Single().Kind, Is.EqualTo(InvariantKind.Overlap), "layer-0 diagnostics for preview");
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("rod", "main", new Cell(3, 0), Rotation.Degrees0), MoveRejection.NoLegalLayer);
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("rod", "lid", new Cell(0, 0), Rotation.Degrees0), MoveRejection.UnknownCompartment);
        }

        [Test]
        public void SourceTrayToStagingDestinationOrNest_AreUnsupported()
        {
            var state = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.SourceTray),
                Item("laptop", Block("laptop", 1, 1), ItemLocation.SourceTray, ObjectiveRole.ExtractionTarget));
            AssertRejected(state, PuzzleMove.MoveToStaging("socks-1"), MoveRejection.UnsupportedRoute);
            AssertRejected(state, PuzzleMove.NestInto("socks-1", "shoe-1"), MoveRejection.UnsupportedRoute);
            AssertRejected(state, PuzzleMove.MoveToDestination("laptop", "tray"), MoveRejection.UnsupportedRoute);
        }

        // ---- 2. suitcase -> suitcase ----

        [Test]
        public void SuitcaseToSuitcase_AcceptedKeepsStateAndMayRotate()
        {
            var state = State(Spec(), Placed("sw", Sweater(), At(0, 0, rotation: Rotation.Degrees90), stateId: "folded"));
            var next = AssertAccepted(state, PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 1), Rotation.Degrees270));
            Assert.That(Get(next, "sw").StateId, Is.EqualTo("folded"));
            Assert.That(Get(next, "sw").Location.Placement.Rotation, Is.EqualTo(Rotation.Degrees270));
        }

        [Test]
        public void SuitcaseItem_CannotFoldOrCompressInPlaceOrDuringRelocation()
        {
            var state = State(Spec(board: Board(6, 3)), Placed("sw", Sweater(), At(0, 0)), Placed("j", Jacket(), At(3, 0)));
            Assert.That(BoardInvariants.Evaluate(state).IsValid, Is.True);
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 0), Rotation.Degrees0, "folded"), MoveRejection.StateChangeNotAllowed);
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("j", "main", new Cell(3, 0), Rotation.Degrees0, "compressed"), MoveRejection.StateChangeNotAllowed);
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 0), Rotation.Degrees0), MoveRejection.NoChange);
        }

        [Test]
        public void InaccessibleSuitcaseItem_CannotMove()
        {
            var state = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Placed("top", Block("box", 1, 1), At(0, 0, layer: 1)));
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("base", "main", new Cell(2, 0), Rotation.Degrees0), MoveRejection.NotAccessible);
            AssertRejected(state, PuzzleMove.MoveToStaging("base"), MoveRejection.NotAccessible);
            AssertRejected(state, PuzzleMove.MoveToStaging("ghost"), MoveRejection.UnknownItem);
        }

        // ---- 3. suitcase -> staging, 4. staging -> suitcase ----

        [Test]
        public void SuitcaseToStaging_AcceptedUntilCapacity()
        {
            var state = State(Spec(stagingCapacity: 1), Placed("a", Block("a", 1, 1), At(0, 0)), Placed("b", Block("b", 1, 1), At(1, 0)));
            var next = AssertAccepted(state, PuzzleMove.MoveToStaging("a"));
            Assert.That(Get(next, "a").Location.Kind, Is.EqualTo(ItemLocationKind.Staging));
            var full = AssertRejected(next, PuzzleMove.MoveToStaging("b"), MoveRejection.InvariantViolation);
            Assert.That(full.Report.Violations.Single().Kind, Is.EqualTo(InvariantKind.StagingOverCapacity));
            AssertRejected(next, PuzzleMove.MoveToStaging("a"), MoveRejection.UnsupportedRoute);
        }

        // ZT-041: slots have canonical identity; one item per slot (ADR-0006 D11).
        [Test]
        public void SuitcaseToStaging_ExplicitSlot_HonouredAndOccupiedOrInvalidSlotsRejected()
        {
            var state = State(Spec(stagingCapacity: 2), Placed("a", Block("a", 1, 1), At(0, 0)), Placed("b", Block("b", 1, 1), At(1, 0)),
                Item("t", Block("t", 1, 1), ItemLocation.SourceTray));
            var second = AssertAccepted(state, PuzzleMove.MoveToStaging("a", 1));
            Assert.That(Get(second, "a").Location, Is.EqualTo(ItemLocation.InStaging(1)));
            AssertRejected(second, PuzzleMove.MoveToStaging("b", 1), MoveRejection.StagingSlotOccupied);
            AssertRejected(second, PuzzleMove.MoveToStaging("b", 2), MoveRejection.InvalidStagingSlot);
            AssertRejected(second, PuzzleMove.MoveToStaging("b", -3), MoveRejection.InvalidStagingSlot);
            AssertRejected(state, PuzzleMove.MoveToStaging("t", 0), MoveRejection.UnsupportedRoute);
            Assert.That(Get(AssertAccepted(second, PuzzleMove.MoveToStaging("b")), "b").Location, Is.EqualTo(ItemLocation.InStaging(0)),
                "default fills the lowest free slot");
            AssertRejected(second, PuzzleMove.MoveToStaging("a", 0), MoveRejection.UnsupportedRoute);
        }

        [Test]
        public void StagingSlots_SharedOrOutOfRange_AreInvariantViolations()
        {
            var shared = State(Spec(stagingCapacity: 2), Item("a", Block("a", 1, 1), ItemLocation.InStaging(0)),
                Item("b", Block("b", 1, 1), ItemLocation.InStaging(0)));
            Assert.That(BoardInvariants.Evaluate(shared).Violations.Select(v => v.Kind), Does.Contain(InvariantKind.StagingSlotConflict));
            var outOfRange = State(Spec(stagingCapacity: 2), Item("a", Block("a", 1, 1), ItemLocation.InStaging(2)));
            Assert.That(BoardInvariants.Evaluate(outOfRange).Violations.Select(v => v.Kind), Does.Contain(InvariantKind.StagingSlotConflict));
            var distinct = State(Spec(stagingCapacity: 2), Item("a", Block("a", 1, 1), ItemLocation.InStaging(0)),
                Item("b", Block("b", 1, 1), ItemLocation.InStaging(1)));
            Assert.That(BoardInvariants.Evaluate(distinct).Violations, Is.Empty);
        }

        [Test]
        public void StagingToSuitcase_CanSelectFoldOrCompressState()
        {
            var state = State(Spec(board: Board(6, 4)), Item("sw", Sweater(), ItemLocation.InStaging(0)), Item("j", Jacket(), ItemLocation.InStaging(1)),
                Placed("base", Block("book", 2, 2), At(4, 0)));
            var folded = AssertAccepted(state, PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 0), Rotation.Degrees0, "folded"));
            Assert.That(Get(folded, "sw").StateId, Is.EqualTo("folded"));

            var compressed = AssertAccepted(state, PuzzleMove.PlaceInSuitcase("j", "main", new Cell(4, 0), Rotation.Degrees0, "compressed"));
            Assert.That(Get(compressed, "j").StateId, Is.EqualTo("compressed"));
            Assert.That(Get(compressed, "j").Location.Placement.Layer, Is.EqualTo(1), "compressed jacket fits on top");

            AssertRejected(state, PuzzleMove.PlaceInSuitcase("j", "main", new Cell(4, 0), Rotation.Degrees0, "normal"), MoveRejection.NoLegalLayer);
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 0), Rotation.Degrees0, "rolled"), MoveRejection.StateNotReachable);
        }

        [Test]
        public void SourceTrayItem_CanSelectState_AndOnlyAuthoredStatesAreSelectable()
        {
            var state = State(Spec(), Item("j", Jacket(), ItemLocation.SourceTray, stateId: "compressed"));
            Assert.That(PuzzleTransitions.GetSelectableStates(Get(state, "j")), Is.EqualTo(new[] { "compressed" }), "no authored decompress edge");
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("j", "main", new Cell(0, 0), Rotation.Degrees0, "normal"), MoveRejection.StateNotReachable);

            var open = State(Spec(), Item("sw", Sweater(), ItemLocation.SourceTray));
            Assert.That(PuzzleTransitions.GetSelectableStates(Get(open, "sw")), Is.EqualTo(new[] { "folded", "open" }));
            AssertAccepted(open, PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 0), Rotation.Degrees90, "folded"));
        }

        // ---- 5./6. nested child out, 7. nest ----

        [Test]
        public void ParentMovesWithChildren_AsOneMove()
        {
            var state = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            var result = PuzzleTransitions.Apply(state, PuzzleMove.MoveToStaging("shoe-1"));
            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.MovedInstanceIds, Is.EqualTo(new[] { "shoe-1", "socks-1" }));
            Assert.That(Get(result.State, "socks-1").Location, Is.EqualTo(ItemLocation.NestedIn("shoe-1")));
            Assert.That(result.State.GetItems(ItemLocationKind.Staging).Count, Is.EqualTo(1), "parent with children uses one slot");
        }

        [Test]
        public void AccessibleNestedChild_ToStagingOrBoard()
        {
            var state = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            var staged = AssertAccepted(state, PuzzleMove.MoveToStaging("socks-1"));
            Assert.That(Get(staged, "shoe-1").Location.Kind, Is.EqualTo(ItemLocationKind.Suitcase), "parent stays in the suitcase");
            var board = AssertAccepted(state, PuzzleMove.PlaceInSuitcase("socks-1", "main", new Cell(3, 0), Rotation.Degrees0));
            Assert.That(PuzzleState.GetPhysicalCells(Get(board, "socks-1")).Count, Is.EqualTo(1));
        }

        [Test]
        public void BlockedParent_KeepsItsChildInaccessible()
        {
            var state = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")),
                Placed("top", Block("box", 1, 1), At(0, 0, layer: 1)));
            AssertRejected(state, PuzzleMove.MoveToStaging("socks-1"), MoveRejection.NotAccessible);
        }

        [Test]
        public void NestedChild_CannotChangeStateWhileNested()
        {
            var shoe = Block("shoe", 2, 2, nest: new NestSpec(1, null, new[] { "clothes" }));
            var state = State(Spec(), Placed("shoe-1", shoe, At(0, 0)), Item("sw", Sweater(), ItemLocation.NestedIn("shoe-1")));
            AssertRejected(state, PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 0), Rotation.Degrees0, "folded"), MoveRejection.StateChangeNotAllowed);
        }

        [Test]
        public void NestInto_CompatibleAccessibleParentAccepted_OthersRejected()
        {
            var state = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Placed("socks-1", Block("socks", 1, 1), At(3, 0)),
                Item("cam", Block("camera", 1, 1), ItemLocation.InStaging(0)), Placed("top", Block("box", 1, 1), At(1, 0)));
            var nested = AssertAccepted(state, PuzzleMove.NestInto("socks-1", "shoe-1"));
            Assert.That(PuzzleState.GetPhysicalCells(Get(nested, "socks-1")), Is.Empty, "nested child occupies no cells");

            var wrong = AssertRejected(state, PuzzleMove.NestInto("cam", "shoe-1"), MoveRejection.InvariantViolation);
            Assert.That(wrong.Report.Violations.Single().Kind, Is.EqualTo(InvariantKind.NestIncompatible));
            AssertRejected(state, PuzzleMove.NestInto("socks-1", "ghost"), MoveRejection.UnknownTarget);
            AssertRejected(state, PuzzleMove.NestInto("shoe-1", "shoe-1"), MoveRejection.NestCycle);

            var full = AssertRejected(nested, PuzzleMove.NestInto("top", "shoe-1"), MoveRejection.InvariantViolation);
            Assert.That(full.Report.Violations.Select(v => v.Kind), Has.Member(InvariantKind.NestOverCapacity));
        }

        [Test]
        public void NestInto_StagedOrBlockedParent_IsRejected()
        {
            var staged = State(Spec(), Item("shoe-1", Shoe(), ItemLocation.InStaging(0)), Placed("socks-1", Block("socks", 1, 1), At(3, 0)));
            AssertRejected(staged, PuzzleMove.NestInto("socks-1", "shoe-1"), MoveRejection.ParentNotAccessible);

            var blocked = State(Spec(), Placed("shoe-1", Shoe(), At(0, 0)), Placed("top", Block("box", 1, 1), At(0, 0, layer: 1)),
                Placed("socks-1", Block("socks", 1, 1), At(3, 0)));
            AssertRejected(blocked, PuzzleMove.NestInto("socks-1", "shoe-1"), MoveRejection.ParentNotAccessible);
        }

        // ---- 8. destination ----

        [Test]
        public void TargetToDestination_AcceptedOnce_AndCannotComeBack()
        {
            var state = State(Spec(), Placed("laptop", Block("laptop", 2, 2), At(0, 0), ObjectiveRole.ExtractionTarget),
                Placed("shirt", Block("shirt", 1, 1), At(3, 0)));
            var extracted = AssertAccepted(state, PuzzleMove.MoveToDestination("laptop", "tray"));
            Assert.That(AccessQueries.GetAccess(extracted, "laptop"), Is.EqualTo(ItemAccess.Terminal));

            AssertRejected(extracted, PuzzleMove.PlaceInSuitcase("laptop", "main", new Cell(0, 0), Rotation.Degrees0), MoveRejection.InDestination);
            AssertRejected(extracted, PuzzleMove.MoveToStaging("laptop"), MoveRejection.InDestination);
            var notTarget = AssertRejected(state, PuzzleMove.MoveToDestination("shirt", "tray"), MoveRejection.InvariantViolation);
            Assert.That(notTarget.Report.Violations.Single().Kind, Is.EqualTo(InvariantKind.DestinationNotAccepted));
            AssertRejected(state, PuzzleMove.MoveToDestination("laptop", "bin"), MoveRejection.UnknownTarget);
        }

        [Test]
        public void BlockedTarget_CannotBeExtracted()
        {
            var state = State(Spec(), Placed("laptop", Block("laptop", 2, 2), At(0, 0), ObjectiveRole.ExtractionTarget),
                Placed("top", Block("box", 1, 1), At(1, 1, layer: 1)));
            AssertRejected(state, PuzzleMove.MoveToDestination("laptop", "tray"), MoveRejection.NotAccessible);
        }

        [Test]
        public void ApplyingTheSameMoveTwice_IsDeterministic()
        {
            var state = State(Spec(), Item("a", Block("a", 2, 1), ItemLocation.SourceTray));
            var move = PuzzleMove.PlaceInSuitcase("a", "main", new Cell(1, 1), Rotation.Degrees90);
            Assert.That(PuzzleTransitions.Apply(state, move).State.Hash, Is.EqualTo(PuzzleTransitions.Apply(state, move).State.Hash));
        }
    }
}
