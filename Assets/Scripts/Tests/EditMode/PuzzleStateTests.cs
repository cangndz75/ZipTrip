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
    public sealed class PuzzleStateTests
    {
        private static void AssertRejected(TestDelegate construct, string reason) =>
            Assert.That(construct, Throws.InstanceOf<ArgumentException>().With.Message.StartsWith(reason));

        [Test]
        public void EmptyState_IsValidAndHashable()
        {
            var state = State(Spec());
            Assert.That(state.Items, Is.Empty);
            Assert.That(state.Hash, Is.EqualTo(State(Spec()).Hash));
        }

        [Test]
        public void EveryContainerKind_IsRepresented()
        {
            var bag = Block("bag", 2, 2, nest: new NestSpec(1, new[] { "socks" }, null));
            var state = State(Spec(),
                Item("tray-1", Block("shirt", 1, 2), ItemLocation.SourceTray),
                Item("staged-1", Block("book", 1, 1), ItemLocation.InStaging(0)),
                Placed("bag-1", bag, At(0, 0)),
                Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("bag-1")),
                Item("laptop-1", Block("laptop", 2, 2), ItemLocation.InDestination("tray"), ObjectiveRole.ExtractionTarget));

            Assert.That(state.GetItems(ItemLocationKind.SourceTray).Single().InstanceId, Is.EqualTo("tray-1"));
            Assert.That(state.GetItems(ItemLocationKind.Staging).Single().InstanceId, Is.EqualTo("staged-1"));
            Assert.That(state.GetItems(ItemLocationKind.Suitcase).Single().InstanceId, Is.EqualTo("bag-1"));
            Assert.That(state.GetChildren("bag-1").Single().InstanceId, Is.EqualTo("socks-1"));
            Assert.That(state.GetDestinationItems("tray").Single().InstanceId, Is.EqualTo("laptop-1"));
        }

        [Test]
        public void SameInstance_CannotBeInTwoContainers()
        {
            var shirt = Block("shirt", 1, 2);
            AssertRejected(() => State(Spec(), Item("s", shirt, ItemLocation.SourceTray), Item("s", shirt, ItemLocation.InStaging(0))),
                "DuplicateInstanceId");
        }

        [Test]
        public void StructuralReferences_AreValidated()
        {
            var shirt = Block("shirt", 1, 2);
            AssertRejected(() => State(Spec(), Placed("s", shirt, At(0, 0, compartment: "lid"))), "UnknownCompartment");
            AssertRejected(() => State(Spec(), Item("s", shirt, ItemLocation.InDestination("bin"))), "UnknownDestination");
            AssertRejected(() => State(Spec(), Item("s", shirt, ItemLocation.NestedIn("ghost"))), "UnknownNestParent");
            AssertRejected(() => State(Spec(), Item("s", shirt, ItemLocation.NestedIn("s"))), "NestCycle");
            AssertRejected(() => State(Spec(), Item("a", shirt, ItemLocation.NestedIn("b")), Item("b", shirt, ItemLocation.NestedIn("a"))),
                "NestCycle");
            AssertRejected(() => Item("s", shirt, ItemLocation.SourceTray, stateId: "folded"), "UnknownState");
            AssertRejected(() => State(Spec(), Item("a", shirt, ItemLocation.SourceTray), Item("b", Block("shirt", 2, 2), ItemLocation.SourceTray)),
                "ConflictingDefinition");
            var fixedRotation = new ItemSpec("rod", "default",
                new[] { new ItemStateSpec("default", Rect(1, 3), 1, new[] { Rotation.Degrees0 }) });
            AssertRejected(() => Placed("r", fixedRotation, At(0, 0, rotation: Rotation.Degrees90)), "RotationNotAllowed");
        }

        [Test]
        public void Occupancy_Thickness1_Thickness2_AndRotatedFootprint()
        {
            var thin = Placed("thin", Block("book", 2, 1), At(1, 1, layer: 1));
            Assert.That(PuzzleState.GetPhysicalCells(thin), Is.EqualTo(new[]
            {
                new PhysicalCell("main", 1, 1, 1), new PhysicalCell("main", 2, 1, 1)
            }));

            var thick = Placed("thick", Jacket(), At(0, 0));
            var cells = PuzzleState.GetPhysicalCells(thick);
            Assert.That(cells.Count, Is.EqualTo(8));
            Assert.That(cells.Select(c => c.Z).Distinct(), Is.EquivalentTo(new[] { 0, 1 }));

            var rotated = Placed("rot", Block("rod", 3, 1), At(2, 0, rotation: Rotation.Degrees90));
            Assert.That(PuzzleState.GetPhysicalCells(rotated), Is.EqualTo(new[]
            {
                new PhysicalCell("main", 2, 0, 0), new PhysicalCell("main", 2, 1, 0), new PhysicalCell("main", 2, 2, 0)
            }));

            var folded = Placed("sw", Sweater(), At(0, 0), stateId: "folded");
            Assert.That(PuzzleState.GetPhysicalCells(folded).Count, Is.EqualTo(8), "occupancy uses the current state");
        }

        [Test]
        public void NonSuitcaseItems_HaveNoPhysicalCells()
        {
            var bag = Block("bag", 2, 2, nest: new NestSpec(1, new[] { "socks" }, null));
            var state = State(Spec(),
                Placed("bag-1", bag, At(0, 0)),
                Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("bag-1")),
                Item("tray-1", Block("shirt", 1, 1), ItemLocation.SourceTray),
                Item("staged-1", Block("book", 1, 1), ItemLocation.InStaging(0)),
                Item("out-1", Block("laptop", 1, 1), ItemLocation.InDestination("tray"), ObjectiveRole.ExtractionTarget));
            foreach (var id in new[] { "socks-1", "tray-1", "staged-1", "out-1" })
            {
                state.TryGetItem(id, out var item);
                Assert.That(PuzzleState.GetPhysicalCells(item), Is.Empty, id);
            }
            state.TryGetItem("bag-1", out var parent);
            Assert.That(PuzzleState.GetPhysicalCells(parent).Count, Is.EqualTo(4));
        }

        [Test]
        public void CanonicalBytes_AreDeterministicAndIndependentOfInputOrder()
        {
            var a = new[] { Placed("b-1", Block("book", 1, 1), At(0, 0)), Item("s-1", Block("shirt", 1, 1), ItemLocation.InStaging(0)) };
            var first = new PuzzleState(Spec(), a);
            var second = new PuzzleState(Spec(), a.Reverse());
            Assert.That(second, Is.EqualTo(first));
            Assert.That(second.Hash, Is.EqualTo(first.Hash));
            Assert.That(second.ToStableBytes(), Is.EqualTo(first.ToStableBytes()));
            Assert.That(first.Items.Select(i => i.InstanceId), Is.EqualTo(new[] { "b-1", "s-1" }));
            Assert.That(first.Hash, Is.EqualTo(Fnv1a64.Compute(first.ToStableBytes())));
        }

        [Test]
        public void SemanticChanges_ChangeBytesAndHash()
        {
            var book = Block("book", 2, 1);
            var baseline = State(Spec(), Placed("b", book, At(0, 0)));
            var variants = new[]
            {
                State(Spec(), Placed("b", book, At(1, 0))),
                State(Spec(), Placed("b", book, At(0, 0, layer: 1))),
                State(Spec(), Placed("b", book, At(0, 0, rotation: Rotation.Degrees90))),
                State(Spec(), Placed("b", book, At(0, 0, compartment: "pocket"))),
                State(Spec(), Placed("b", book, At(0, 0), ObjectiveRole.None)),
                State(Spec(), Item("b", book, ItemLocation.InStaging(0))),
                State(Spec(), Item("b", book, ItemLocation.SourceTray)),
                State(Spec(), Placed("c", book, At(0, 0)))
            };
            foreach (var variant in variants)
            {
                Assert.That(variant, Is.Not.EqualTo(baseline));
                Assert.That(variant.Hash, Is.Not.EqualTo(baseline.Hash));
            }

            var sweater = State(Spec(), Item("w", Sweater(), ItemLocation.InStaging(0)));
            Assert.That(State(Spec(), Item("w", Sweater(), ItemLocation.InStaging(0), stateId: "folded")).Hash, Is.Not.EqualTo(sweater.Hash));
        }

        [Test]
        public void EquivalenceKey_PreservesRoleAndStateButIgnoresIdentityAndLocation()
        {
            var shirt = Block("shirt", 1, 2, tags: new[] { "clothes" });
            var required = Item("t-1", shirt, ItemLocation.SourceTray, ObjectiveRole.Required);
            var twin = Placed("t-2", shirt, At(0, 0), ObjectiveRole.Required);
            var optional = Item("t-3", shirt, ItemLocation.SourceTray, ObjectiveRole.None);

            Assert.That(twin.EquivalenceKey, Is.EqualTo(required.EquivalenceKey));
            Assert.That(optional.EquivalenceKey, Is.Not.EqualTo(required.EquivalenceKey), "objective role differs");
            Assert.That(required.EquivalenceKey.Tags, Is.EqualTo(new[] { "clothes" }));
            Assert.That(Item("w-1", Sweater(), ItemLocation.InStaging(0)).EquivalenceKey,
                Is.Not.EqualTo(Item("w-2", Sweater(), ItemLocation.InStaging(1), stateId: "folded").EquivalenceKey), "state differs");

            var state = State(Spec(), required, twin);
            Assert.That(state.Items.Select(i => i.InstanceId), Is.EqualTo(new[] { "t-1", "t-2" }), "instances stay distinct");
        }

        [Test]
        public void With_ReturnsNewStateAndLeavesOriginalUnchanged()
        {
            var book = Placed("b", Block("book", 1, 1), At(0, 0));
            var before = State(Spec(), book);
            var after = before.With(book.With(ItemLocation.InStaging(0)));
            Assert.That(after.GetItems(ItemLocationKind.Staging).Count, Is.EqualTo(1));
            Assert.That(before.GetItems(ItemLocationKind.Suitcase).Count, Is.EqualTo(1));
            Assert.That(() => ((IList<PuzzleItem>)before.Items).Clear(), Throws.InstanceOf<NotSupportedException>());
            AssertRejected(() => before.With(Placed("x", Block("book", 1, 1), At(0, 0))), "UnknownInstance");
        }

        [Test]
        public void Destination_AcceptanceIsAuthoredAndValidated()
        {
            var tray = Tray();
            Assert.That(tray.Accepts(Item("l", Block("laptop", 2, 2), ItemLocation.SourceTray, ObjectiveRole.ExtractionTarget)), Is.True);
            Assert.That(tray.Accepts(Item("s", Block("shirt", 1, 1), ItemLocation.SourceTray)), Is.False);
            AssertRejected(() => new ExtractionDestinationSpec("x", 0, new[] { "laptop" }), "InvalidDestinationCapacity");
            AssertRejected(() => new ExtractionDestinationSpec("x", 1), "MalformedDestinationAcceptance");
            AssertRejected(() => new PuzzleSpec(Board(), -1), "InvalidStagingCapacity");
        }

        [Test]
        public void Destination_DistinguishesInstanceFromDefinitionIdentity()
        {
            var laptop = Block("laptop", 2, 2);
            var target = Placed("laptop-a", laptop, At(0, 0), ObjectiveRole.ExtractionTarget);
            var twin = Placed("laptop-b", laptop, At(2, 0), ObjectiveRole.Required);

            var byInstance = new ExtractionDestinationSpec("tray", 1, acceptedInstanceIds: new[] { "laptop-a" });
            Assert.That(byInstance.Accepts(target), Is.True);
            Assert.That(byInstance.Accepts(twin), Is.False, "same definition, different instance");
            Assert.That(byInstance.Accepts(Placed("laptop", laptop, At(0, 0))), Is.False, "definition id is not an instance id");

            var byDefinition = new ExtractionDestinationSpec("tray", 1, acceptedDefinitionIds: new[] { "laptop" });
            Assert.That(byDefinition.Accepts(twin), Is.True, "definition acceptance covers every instance");

            foreach (var destination in new[] { byInstance, Tray() })
            {
                var state = State(new PuzzleSpec(Board(), 2, new[] { destination }), target, twin);
                var wrong = PuzzleTransitions.Apply(state, PuzzleMove.MoveToDestination("laptop-b", "tray"));
                Assert.That(wrong.Rejection, Is.EqualTo(MoveRejection.InvariantViolation));
                Assert.That(wrong.Report.Violations.Single().Kind, Is.EqualTo(InvariantKind.DestinationNotAccepted));
                Assert.That(PuzzleTransitions.Apply(state, PuzzleMove.MoveToDestination("laptop-a", "tray")).IsAccepted, Is.True);
            }
        }
    }
}
