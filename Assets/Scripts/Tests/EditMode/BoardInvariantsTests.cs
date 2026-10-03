using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class BoardInvariantsTests
    {
        private static InvariantKind[] Kinds(PuzzleState state) =>
            BoardInvariants.Evaluate(state).Violations.Select(v => v.Kind).ToArray();

        // ---- Bounds / mask ----

        [Test]
        public void Layer0Placement_IsValid()
        {
            Assert.That(BoardInvariants.Evaluate(State(Spec(), Placed("b", Block("book", 2, 2), At(0, 0)))).IsValid, Is.True);
        }

        [Test]
        public void OutOfBoundsXY_AndLayer_AreRejected()
        {
            Assert.That(Kinds(State(Spec(), Placed("b", Block("book", 2, 1), At(3, 0)))), Is.EqualTo(new[] { InvariantKind.OutOfBounds }));
            Assert.That(Kinds(State(Spec(), Placed("b", Block("book", 1, 1), At(-1, 0)))), Is.EqualTo(new[] { InvariantKind.OutOfBounds }));
            Assert.That(Kinds(State(Spec(), Placed("b", Block("book", 1, 1), At(0, 0, layer: 2)))),
                Has.Member(InvariantKind.OutOfBounds));
            Assert.That(Kinds(State(Spec(), Placed("b", Block("book", 1, 1), At(0, 0, layer: 1, compartment: "pocket")))),
                Has.Member(InvariantKind.OutOfBounds), "pocket has L = 1");
        }

        [Test]
        public void MaskedCell_IsRejected()
        {
            var board = new BoardSpec(new[] { new Compartment("main", 3, 3, 2, RectCells(3, 3).Where(c => c != new Cell(1, 1))) });
            var report = BoardInvariants.Evaluate(State(Spec(board: board), Placed("b", Block("book", 2, 2), At(0, 0))));
            Assert.That(report.Violations.Single().Kind, Is.EqualTo(InvariantKind.MaskedCell));
            Assert.That(report.Violations.Single().Cell, Is.EqualTo(new PhysicalCell("main", 1, 1, 0)));
            Assert.That(BoardInvariants.Evaluate(State(Spec(board: board), Placed("b", Block("book", 1, 1), At(0, 0)))).IsValid, Is.True);
        }

        // ---- Overlap ----

        [Test]
        public void SameZOverlap_IsRejectedWithBothItemsAndCell()
        {
            var report = BoardInvariants.Evaluate(State(Spec(),
                Placed("a", Block("book", 2, 1), At(0, 0)), Placed("b", Block("box", 1, 1), At(1, 0))));
            var violation = report.Violations.Single();
            Assert.That(violation.Kind, Is.EqualTo(InvariantKind.Overlap));
            Assert.That((violation.InstanceId, violation.OtherInstanceId), Is.EqualTo(("b", "a")));
            Assert.That(violation.Cell, Is.EqualTo(new PhysicalCell("main", 1, 0, 0)));
            Assert.That(report.Involving("a").Count, Is.EqualTo(1));
        }

        [Test]
        public void CrossZXYOverlap_WithSupport_IsValid()
        {
            var state = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Placed("top", Block("box", 1, 1), At(1, 1, layer: 1)));
            Assert.That(BoardInvariants.Evaluate(state).IsValid, Is.True);
        }

        [Test]
        public void SameCellsInDifferentCompartments_DoNotOverlap()
        {
            var state = State(Spec(), Placed("a", Block("book", 1, 1), At(0, 0)), Placed("b", Block("box", 1, 1), At(0, 0, compartment: "pocket")));
            Assert.That(BoardInvariants.Evaluate(state).IsValid, Is.True);
        }

        // ---- Support ----

        [Test]
        public void Layer1_WithoutSupport_IsRejected()
        {
            Assert.That(Kinds(State(Spec(), Placed("top", Block("box", 1, 1), At(0, 0, layer: 1)))),
                Is.EqualTo(new[] { InvariantKind.Unsupported }));
        }

        [Test]
        public void PartialSupport_IsRejectedPerUnsupportedCell()
        {
            var report = BoardInvariants.Evaluate(State(Spec(),
                Placed("base", Block("book", 1, 1), At(0, 0)), Placed("top", Block("rod", 2, 1), At(0, 0, layer: 1))));
            var violation = report.Violations.Single();
            Assert.That(violation.Kind, Is.EqualTo(InvariantKind.Unsupported));
            Assert.That(violation.Cell, Is.EqualTo(new PhysicalCell("main", 1, 0, 1)));
        }

        [Test]
        public void MultipleLowerItems_CanJointlySupport()
        {
            var state = State(Spec(),
                Placed("left", Block("book", 1, 1), At(0, 0)), Placed("right", Block("box", 1, 1), At(1, 0)),
                Placed("top", Block("rod", 2, 1), At(0, 0, layer: 1)));
            Assert.That(BoardInvariants.Evaluate(state).IsValid, Is.True);
        }

        [Test]
        public void Thickness2_Layer0Valid_Layer1OutOfBounds()
        {
            Assert.That(BoardInvariants.Evaluate(State(Spec(), Placed("j", Jacket(), At(0, 0)))).IsValid, Is.True);
            var report = BoardInvariants.Evaluate(State(Spec(),
                Placed("base", Block("book", 2, 2), At(0, 0)), Placed("j", Jacket(), At(0, 0, layer: 1))));
            Assert.That(report.Violations.Select(v => v.Kind).Distinct(), Is.EqualTo(new[] { InvariantKind.OutOfBounds }));
        }

        [Test]
        public void Thickness2Item_SupportsALayer1Item()
        {
            // A thickness-2 item fills z = 0 and z = 1, so nothing can sit on it at layer 1 (overlap).
            var report = BoardInvariants.Evaluate(State(Spec(), Placed("j", Jacket(), At(0, 0)), Placed("top", Block("box", 1, 1), At(0, 0, layer: 1))));
            Assert.That(report.Violations.Single().Kind, Is.EqualTo(InvariantKind.Overlap));
        }

        // ---- Containers ----

        [Test]
        public void StagingCapacity_IsEnforced()
        {
            var two = State(Spec(stagingCapacity: 2), Item("a", Block("a", 1, 1), ItemLocation.InStaging(0)), Item("b", Block("b", 1, 1), ItemLocation.InStaging(1)));
            Assert.That(BoardInvariants.Evaluate(two).IsValid, Is.True);
            var three = State(Spec(stagingCapacity: 2), Item("a", Block("a", 1, 1), ItemLocation.InStaging(0)),
                Item("b", Block("b", 1, 1), ItemLocation.InStaging(1)), Item("c", Block("c", 1, 1), ItemLocation.InStaging(2)));
            Assert.That(Kinds(three), Is.EqualTo(new[] { InvariantKind.StagingOverCapacity }));
        }

        [Test]
        public void NestCapacityAndCompatibility_AreEnforced()
        {
            var shoe = Block("shoe", 1, 2, nest: new NestSpec(1, new[] { "socks" }, null));
            var ok = State(Spec(), Placed("shoe-1", shoe, At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            Assert.That(BoardInvariants.Evaluate(ok).IsValid, Is.True);

            var over = State(Spec(), Placed("shoe-1", shoe, At(0, 0)),
                Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")),
                Item("socks-2", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1")));
            Assert.That(Kinds(over), Is.EqualTo(new[] { InvariantKind.NestOverCapacity }));

            var wrong = State(Spec(), Placed("shoe-1", shoe, At(0, 0)), Item("cam-1", Block("camera", 1, 1), ItemLocation.NestedIn("shoe-1")));
            Assert.That(Kinds(wrong), Is.EqualTo(new[] { InvariantKind.NestIncompatible }));

            var noNest = State(Spec(), Placed("book-1", Block("book", 1, 1), At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("book-1")));
            Assert.That(Kinds(noNest), Is.EqualTo(new[] { InvariantKind.NestOverCapacity, InvariantKind.NestIncompatible }));
        }

        [Test]
        public void DestinationCapacityAndAcceptance_AreEnforced()
        {
            var laptop = Block("laptop", 2, 2);
            var ok = State(Spec(), Item("l", laptop, ItemLocation.InDestination("tray"), ObjectiveRole.ExtractionTarget));
            Assert.That(BoardInvariants.Evaluate(ok).IsValid, Is.True);

            var two = State(Spec(), Item("l", laptop, ItemLocation.InDestination("tray"), ObjectiveRole.ExtractionTarget),
                Item("m", laptop, ItemLocation.InDestination("tray"), ObjectiveRole.ExtractionTarget));
            Assert.That(Kinds(two), Is.EqualTo(new[] { InvariantKind.DestinationOverCapacity }));

            var wrong = State(Spec(), Item("s", Block("shirt", 1, 1), ItemLocation.InDestination("tray")));
            Assert.That(Kinds(wrong), Is.EqualTo(new[] { InvariantKind.DestinationNotAccepted }));
        }

        // ---- Lowest legal layer ----

        [Test]
        public void Resolver_PrefersLayer0()
        {
            var state = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Item("box", Block("box", 1, 1), ItemLocation.SourceTray));
            var result = LayerResolver.ResolveLowestLegalLayer(state, "box", "default", "main", new Cell(2, 0), Rotation.Degrees0);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Layer, Is.EqualTo(0));
            Assert.That(result.Candidate.TryGetItem("box", out var placed) && placed.Location.Placement.Layer == 0, Is.True);
            Assert.That(state.GetItems(ItemLocationKind.SourceTray).Count, Is.EqualTo(1), "source state unchanged");
        }

        [Test]
        public void Resolver_FallsBackToSupportedLayer1()
        {
            var state = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Item("box", Block("box", 1, 1), ItemLocation.SourceTray));
            var result = LayerResolver.ResolveLowestLegalLayer(state, "box", "default", "main", new Cell(1, 1), Rotation.Degrees0);
            Assert.That(result.Layer, Is.EqualTo(1));
            Assert.That(result.Attempts[0].Violations.Single().Kind, Is.EqualTo(InvariantKind.Overlap));
            Assert.That(result.Attempts[1].IsValid, Is.True);
        }

        [Test]
        public void Resolver_ReturnsInvalidWhenNoLayerIsLegal()
        {
            var state = State(Spec(), Placed("base", Block("book", 1, 1), At(0, 0)), Item("rod", Block("rod", 2, 1), ItemLocation.SourceTray),
                Item("j", Jacket(), ItemLocation.SourceTray));

            var partial = LayerResolver.ResolveLowestLegalLayer(state, "rod", "default", "main", new Cell(0, 0), Rotation.Degrees0);
            Assert.That(partial.Failure, Is.EqualTo(LayerResolutionFailure.NoLegalLayer));
            Assert.That(partial.Attempts[1].Violations.Single().Kind, Is.EqualTo(InvariantKind.Unsupported));

            var thick = LayerResolver.ResolveLowestLegalLayer(state, "j", "normal", "main", new Cell(0, 0), Rotation.Degrees0);
            Assert.That(thick.Failure, Is.EqualTo(LayerResolutionFailure.NoLegalLayer), "thickness 2 cannot start at layer 1");
            Assert.That(thick.Candidate, Is.Null);
        }

        [Test]
        public void Resolver_UsesRequestedStateAndRejectsBadInputWithoutThrowing()
        {
            var state = State(Spec(), Placed("base", Block("book", 2, 2), At(0, 0)), Item("j", Jacket(), ItemLocation.InStaging(0)));
            var compressed = LayerResolver.ResolveLowestLegalLayer(state, "j", "compressed", "main", new Cell(0, 0), Rotation.Degrees0);
            Assert.That(compressed.Layer, Is.EqualTo(1), "compressed thickness 1 fits on top");

            Assert.That(LayerResolver.ResolveLowestLegalLayer(state, "ghost", "normal", "main", new Cell(0, 0), Rotation.Degrees0).Failure,
                Is.EqualTo(LayerResolutionFailure.UnknownItem));
            Assert.That(LayerResolver.ResolveLowestLegalLayer(state, "j", "flat", "main", new Cell(0, 0), Rotation.Degrees0).Failure,
                Is.EqualTo(LayerResolutionFailure.UnknownState));
            Assert.That(LayerResolver.ResolveLowestLegalLayer(state, "j", "normal", "lid", new Cell(0, 0), Rotation.Degrees0).Failure,
                Is.EqualTo(LayerResolutionFailure.UnknownCompartment));
            Assert.That(LayerResolver.ResolveLowestLegalLayer(state, "j", "normal", "main", new Cell(0, 0), (Rotation)45).Failure,
                Is.EqualTo(LayerResolutionFailure.RotationNotAllowed));
        }
    }
}
