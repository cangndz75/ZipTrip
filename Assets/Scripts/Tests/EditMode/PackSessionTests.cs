using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Application;
using ZipTrip.Domain;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PackSessionTests
    {
        private static ItemDefinition Sneaker() => Item("sneaker", new[]
        {
            new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2)
        }, null, new[] { Rotation.Degrees0, Rotation.Degrees90,
            Rotation.Degrees180, Rotation.Degrees270 });

        private static ItemDefinition Sweater() => Item("sweater", Rectangle(3, 3),
            Rectangle(2, 4), new[] { Rotation.Degrees0, Rotation.Degrees90 });

        private static ItemDefinition Laptop() => Item("laptop", Rectangle(2, 2),
            null, new[] { Rotation.Degrees0 });

        private static ItemDefinition Item(string id, IEnumerable<Cell> open,
            IEnumerable<Cell> folded, IEnumerable<Rotation> rotations)
        {
            var states = new List<KeyValuePair<string, ItemShape>>
            {
                new KeyValuePair<string, ItemShape>("open", new ItemShape(open))
            };
            if (folded != null)
                states.Add(new KeyValuePair<string, ItemShape>("folded", new ItemShape(folded)));
            return new ItemDefinition(id, "open", states, rotations, Array.Empty<string>());
        }

        private static Cell[] Rectangle(int width, int height) =>
            (from y in Enumerable.Range(0, height)
             from x in Enumerable.Range(0, width)
             select new Cell(x, y)).ToArray();

        private static PackSession Session()
        {
            var tray = new[]
            {
                new TrayItem("sneaker", Rotation.Degrees0, "open"),
                new TrayItem("sweater", Rotation.Degrees0, "open"),
                new TrayItem("laptop", Rotation.Degrees0, "open")
            };
            var state = new GameState(ContainerFixtures.CreateCabin(new Cell(0, 0)),
                Array.Empty<PlacedItem>(), tray, new[] { "sneaker", "sweater", "laptop" });
            return new PackSession(state, new[] { Sneaker(), Sweater(), Laptop() });
        }

        private static void AssertRejectedUnchanged(PackSession session, CommandResult result, byte[] before,
            ulong hash, int undoDepth)
        {
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.NewState, Is.Null);
            Assert.That(result.Events, Is.Empty);
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(before));
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(hash));
            Assert.That(session.UndoDepth, Is.EqualTo(undoDepth));
        }

        [Test]
        public void PlaceItem_TransfersTrayToPlacementAndEmitsEvent()
        {
            var session = Session();
            var result = session.PlaceItem("sneaker", new Cell(1, 0), Rotation.Degrees0, "open");

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(session.State.Tray.Select(x => x.ItemId), Is.EqualTo(new[] { "sweater", "laptop" }));
            Assert.That(session.State.Placements.Single().Anchor, Is.EqualTo(new Cell(1, 0)));
            Assert.That(session.State.Occupancy, Is.EqualTo(new[]
            {
                new Cell(1, 0), new Cell(1, 1), new Cell(1, 2), new Cell(2, 2)
            }));
            Assert.That(result.Events.Single(), Is.TypeOf<ItemPlacedEvent>());
            Assert.That(((ItemPlacedEvent)result.Events[0]).ItemId, Is.EqualTo("sneaker"));
        }

        [Test]
        public void InvalidPlace_RejectionPreservesStateHashHistoryAndEvents()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);
            var result = session.PlaceItem("sneaker", new Cell(-1, 0), Rotation.Degrees0, "open");

            Assert.That(result.Reason, Is.EqualTo("OutOfBounds"));
            AssertRejectedUnchanged(session, result, bytes, hash, 0);
        }

        [Test]
        public void PlaceOutsideMask_RejectsWithoutMutation()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var result = session.PlaceItem("laptop", new Cell(0, 0), Rotation.Degrees0, "open");

            Assert.That(result.Reason, Is.EqualTo("OutsideMask"));
            AssertRejectedUnchanged(session, result, bytes, StateHash.Compute(session.State), 0);
        }

        [Test]
        public void PlaceOverlap_RejectsWithoutMutation()
        {
            var session = Session();
            Assert.That(session.PlaceItem("sneaker", new Cell(1, 1), Rotation.Degrees0, "open").IsAccepted,
                Is.True);
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);
            var result = session.PlaceItem("laptop", new Cell(1, 2), Rotation.Degrees0, "open");

            Assert.That(result.Reason, Is.EqualTo("Overlap"));
            AssertRejectedUnchanged(session, result, bytes, hash, 1);
        }

        [Test]
        public void PlaceRequiresTrayAndCurrentAuthoredState()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);

            var missing = session.PlaceItem("unknown", new Cell(1, 1), Rotation.Degrees0, "open");
            Assert.That(missing.Reason, Is.EqualTo("ItemNotInTray"));
            AssertRejectedUnchanged(session, missing, bytes, hash, 0);

            var wrongState = session.PlaceItem("sweater", new Cell(1, 1), Rotation.Degrees0, "folded");
            Assert.That(wrongState.Reason, Is.EqualTo("ShapeStateMismatch"));
            AssertRejectedUnchanged(session, wrongState, bytes, hash, 0);
        }

        [Test]
        public void MoveExcludesOwnCellsButRejectsOtherItemOverlap()
        {
            var session = Session();
            Assert.That(session.PlaceItem("sneaker", new Cell(1, 1), Rotation.Degrees0, "open").IsAccepted, Is.True);
            Assert.That(session.PlaceItem("laptop", new Cell(4, 2), Rotation.Degrees0, "open").IsAccepted, Is.True);

            Assert.That(session.MoveItem("sneaker", new Cell(1, 2)).IsAccepted, Is.True);
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);
            var depth = session.UndoDepth;
            var rejected = session.MoveItem("sneaker", new Cell(4, 1));

            Assert.That(rejected.Reason, Is.EqualTo("Overlap"));
            AssertRejectedUnchanged(session, rejected, bytes, hash, depth);
            Assert.That(session.State.Placements.Single(p => p.ItemId == "sneaker").Anchor,
                Is.EqualTo(new Cell(1, 2)));
        }

        [Test]
        public void MoveTrayItem_Rejects()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var rejected = session.MoveItem("sneaker", new Cell(1, 1));
            Assert.That(rejected.Reason, Is.EqualTo("ItemNotPlaced"));
            AssertRejectedUnchanged(session, rejected, bytes, StateHash.Compute(session.State), 0);
        }

        [Test]
        public void RotateTrayItem_IsAuthoritativeAndUndoable()
        {
            var session = Session();
            var initial = CanonicalStateSerializer.Serialize(session.State);
            var changed = session.RotateItem("sneaker", Rotation.Degrees90);

            Assert.That(changed.IsAccepted, Is.True);
            Assert.That(changed.Events.Single(), Is.TypeOf<ItemRotatedEvent>());
            Assert.That(session.State.Tray[0].Rotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.Not.EqualTo(initial));
            Assert.That(session.Undo().IsAccepted, Is.True);
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(initial));
        }

        [Test]
        public void RotatePlacedItem_RevalidatesFootprintAndKeepsAnchor()
        {
            var session = Session();
            Assert.That(session.PlaceItem("sneaker", new Cell(1, 1), Rotation.Degrees0, "open").IsAccepted, Is.True);
            var rotated = session.RotateItem("sneaker", Rotation.Degrees90);

            Assert.That(rotated.IsAccepted, Is.True);
            Assert.That(rotated.Events.Single(), Is.TypeOf<ItemRotatedEvent>());
            var placement = session.State.Placements.Single();
            Assert.That(placement.Anchor, Is.EqualTo(new Cell(1, 1)));
            Assert.That(placement.Rotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(placement.OccupiedCells, Is.EqualTo(new[]
            {
                new Cell(1, 1), new Cell(2, 1), new Cell(3, 1), new Cell(1, 2)
            }));
        }

        [Test]
        public void RotatePlacedItem_RejectsMaskViolationWithoutMutation()
        {
            var session = Session();
            Assert.That(session.PlaceItem("sneaker", new Cell(3, 0), Rotation.Degrees0, "open").IsAccepted, Is.True);
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);
            var depth = session.UndoDepth;
            var rejected = session.RotateItem("sneaker", Rotation.Degrees90);

            Assert.That(rejected.Reason, Is.EqualTo("OutsideMask"));
            AssertRejectedUnchanged(session, rejected, bytes, hash, depth);
        }

        [Test]
        public void DisallowedRotation_RejectsForTrayAndBag()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var rejected = session.RotateItem("laptop", Rotation.Degrees90);
            Assert.That(rejected.Reason, Is.EqualTo("RotationNotAllowed"));
            AssertRejectedUnchanged(session, rejected, bytes, StateHash.Compute(session.State), 0);

            Assert.That(session.PlaceItem("laptop", new Cell(3, 4), Rotation.Degrees0, "open").IsAccepted, Is.True);
            bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);
            rejected = session.RotateItem("laptop", Rotation.Degrees90);
            Assert.That(rejected.Reason, Is.EqualTo("RotationNotAllowed"));
            AssertRejectedUnchanged(session, rejected, bytes, hash, 1);
        }

        [Test]
        public void FoldOnlyInTray_UsesAuthoredShapeAndCanBeReversed()
        {
            var session = Session();
            var folded = session.FoldItem("sweater", "folded");
            Assert.That(folded.IsAccepted, Is.True);
            Assert.That(folded.Events.Single(), Is.TypeOf<FoldChangedEvent>());
            Assert.That(session.State.Tray.Single(x => x.ItemId == "sweater").ShapeState,
                Is.EqualTo("folded"));
            Assert.That(session.PlaceItem("sweater", new Cell(2, 1), Rotation.Degrees0, "folded").IsAccepted,
                Is.True);
            Assert.That(session.State.Placements.Single().OccupiedCells.Count, Is.EqualTo(8));
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);
            var rejected = session.FoldItem("sweater", "open");
            Assert.That(rejected.Reason, Is.EqualTo("FoldOnlyInTray"));
            AssertRejectedUnchanged(session, rejected, bytes, hash, 2);

            Assert.That(session.ReturnToTray("sweater").IsAccepted, Is.True);
            Assert.That(session.FoldItem("sweater", "open").IsAccepted, Is.True);
            Assert.That(session.State.Tray.Single(x => x.ItemId == "sweater").ShapeState,
                Is.EqualTo("open"));
        }

        [Test]
        public void UnknownFoldState_RejectsWithoutMutation()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var rejected = session.FoldItem("sweater", "missing");
            Assert.That(rejected.Reason, Is.EqualTo("ShapeStateNotFound"));
            AssertRejectedUnchanged(session, rejected, bytes, StateHash.Compute(session.State), 0);
        }

        [Test]
        public void ItemWithoutAuthoredFoldState_Rejects()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);
            var rejected = session.FoldItem("laptop", "open");
            Assert.That(rejected.Reason, Is.EqualTo("FoldNotAvailable"));
            AssertRejectedUnchanged(session, rejected, bytes, hash, 0);
        }

        [Test]
        public void PlaceWithDisallowedRotation_Rejects()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var hash = StateHash.Compute(session.State);
            var rejected = session.PlaceItem("laptop", new Cell(1, 1), Rotation.Degrees90, "open");
            Assert.That(rejected.Reason, Is.EqualTo("RotationNotAllowed"));
            AssertRejectedUnchanged(session, rejected, bytes, hash, 0);
        }

        [Test]
        public void EmptyTrayWithoutTargetPlacement_DoesNotComplete()
        {
            var item = Laptop();
            var initial = new GameState(ContainerFixtures.CreateCabin(new Cell(0, 0)),
                Array.Empty<PlacedItem>(), new[] { new TrayItem(item.Id, Rotation.Degrees0, "open") },
                new[] { "missing" });
            var session = new PackSession(initial, new[] { item });

            var result = session.PlaceItem("laptop", new Cell(1, 1), Rotation.Degrees0, "open");
            Assert.That(result.IsAccepted, Is.True);
            Assert.That(session.State.Tray, Is.Empty);
            Assert.That(result.Events.OfType<LevelCompletedEvent>(), Is.Empty);
        }

        [Test]
        public void ReturnToTray_RemovesOccupancyAndEmitsEvent()
        {
            var session = Session();
            Assert.That(session.PlaceItem("sneaker", new Cell(1, 1), Rotation.Degrees90, "open").IsAccepted, Is.True);
            var result = session.ReturnToTray("sneaker");

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.Events.Single(), Is.TypeOf<ItemReturnedEvent>());
            Assert.That(session.State.Placements, Is.Empty);
            Assert.That(session.State.Occupancy, Is.Empty);
            Assert.That(session.State.Tray.Single(x => x.ItemId == "sneaker").Rotation,
                Is.EqualTo(Rotation.Degrees90));
        }

        [Test]
        public void ReturnMiddleTrayItem_AppendsAndUndoRestoresExactOrder()
        {
            var session = Session();
            var initial = CanonicalStateSerializer.Serialize(session.State);
            Assert.That(session.PlaceItem("sweater", new Cell(1, 1), Rotation.Degrees0, "open").IsAccepted,
                Is.True);
            Assert.That(session.State.Tray.Select(x => x.ItemId),
                Is.EqualTo(new[] { "sneaker", "laptop" }));
            var beforeReturn = CanonicalStateSerializer.Serialize(session.State);
            var beforeHash = StateHash.Compute(session.State);

            Assert.That(session.ReturnToTray("sweater").IsAccepted, Is.True);
            Assert.That(session.State.Tray.Select(x => x.ItemId),
                Is.EqualTo(new[] { "sneaker", "laptop", "sweater" }));
            var returned = CanonicalStateSerializer.Serialize(session.State);
            var returnedHash = StateHash.Compute(session.State);
            Assert.That(returned, Is.Not.EqualTo(initial)); // Slot order is canonical state.
            Assert.That(returnedHash, Is.Not.EqualTo(StateHash.Compute(initial)));

            Assert.That(session.Undo().IsAccepted, Is.True);
            Assert.That(session.State.Tray.Select(x => x.ItemId),
                Is.EqualTo(new[] { "sneaker", "laptop" }));
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(beforeReturn));
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(beforeHash));
        }

        [Test]
        public void MultipleReturns_AppendInReturnOrderWithDeterministicBytesAndHash()
        {
            PackSession Run()
            {
                var session = Session();
                Assert.That(session.PlaceItem("sweater", new Cell(1, 1), Rotation.Degrees0, "open").IsAccepted,
                    Is.True);
                Assert.That(session.PlaceItem("laptop", new Cell(4, 4), Rotation.Degrees0, "open").IsAccepted,
                    Is.True);
                Assert.That(session.State.Tray.Select(x => x.ItemId), Is.EqualTo(new[] { "sneaker" }));
                Assert.That(session.ReturnToTray("laptop").IsAccepted, Is.True);
                Assert.That(session.State.Tray.Select(x => x.ItemId),
                    Is.EqualTo(new[] { "sneaker", "laptop" }));
                Assert.That(session.ReturnToTray("sweater").IsAccepted, Is.True);
                Assert.That(session.State.Tray.Select(x => x.ItemId),
                    Is.EqualTo(new[] { "sneaker", "laptop", "sweater" }));
                return session;
            }

            var first = Run();
            var second = Run();
            Assert.That(CanonicalStateSerializer.Serialize(first.State),
                Is.EqualTo(CanonicalStateSerializer.Serialize(second.State)));
            Assert.That(StateHash.Compute(first.State), Is.EqualTo(StateHash.Compute(second.State)));
        }

        [Test]
        public void ReturnTrayItem_RejectsWithoutMutation()
        {
            var session = Session();
            var bytes = CanonicalStateSerializer.Serialize(session.State);
            var rejected = session.ReturnToTray("sneaker");
            Assert.That(rejected.Reason, Is.EqualTo("ItemNotPlaced"));
            AssertRejectedUnchanged(session, rejected, bytes, StateHash.Compute(session.State), 0);
        }

        [Test]
        public void Completion_RequiresEveryTargetAndEmptyTray_AndCanRepeatAfterUndo()
        {
            var session = Session();
            Assert.That(session.PlaceItem("sneaker", new Cell(1, 0), Rotation.Degrees0, "open")
                .Events.OfType<LevelCompletedEvent>(), Is.Empty);
            Assert.That(session.FoldItem("sweater", "folded").Events.OfType<LevelCompletedEvent>(), Is.Empty);
            Assert.That(session.PlaceItem("sweater", new Cell(3, 1), Rotation.Degrees0, "folded")
                .Events.OfType<LevelCompletedEvent>(), Is.Empty);

            var complete = session.PlaceItem("laptop", new Cell(1, 5), Rotation.Degrees0, "open");
            Assert.That(complete.IsAccepted, Is.True);
            Assert.That(complete.Events.Select(e => e.GetType()), Is.EqualTo(new[]
            {
                typeof(ItemPlacedEvent), typeof(LevelCompletedEvent)
            }));
            Assert.That(session.State.Tray, Is.Empty);
            Assert.That(session.State.Placements.Count, Is.EqualTo(3));

            var moved = session.MoveItem("laptop", new Cell(2, 5));
            Assert.That(moved.IsAccepted, Is.True);
            Assert.That(moved.Events.OfType<LevelCompletedEvent>(), Is.Empty);
            Assert.That(session.Undo().IsAccepted, Is.True); // Completed state restored.
            Assert.That(session.Undo().IsAccepted, Is.True); // Incomplete state restored.
            var again = session.PlaceItem("laptop", new Cell(1, 5), Rotation.Degrees0, "open");
            Assert.That(again.Events.OfType<LevelCompletedEvent>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void UndoEveryAction_RestoresInitialCanonicalState()
        {
            var session = Session();
            var initial = CanonicalStateSerializer.Serialize(session.State);
            var initialHash = StateHash.Compute(session.State);
            Assert.That(session.RotateItem("sneaker", Rotation.Degrees90).IsAccepted, Is.True);
            Assert.That(session.PlaceItem("sneaker", new Cell(1, 1), Rotation.Degrees90, "open").IsAccepted, Is.True);
            Assert.That(session.MoveItem("sneaker", new Cell(2, 2)).IsAccepted, Is.True);
            Assert.That(session.ReturnToTray("sneaker").IsAccepted, Is.True);
            Assert.That(session.FoldItem("sweater", "folded").IsAccepted, Is.True);

            for (var i = 0; i < 5; i++)
                Assert.That(session.Undo().IsAccepted, Is.True);
            Assert.That(session.Undo().Reason, Is.EqualTo("NoUndoAvailable"));
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(initial));
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(initialHash));
            Assert.That(session.State.Placements, Is.Empty);
            Assert.That(session.State.Occupancy, Is.Empty);
            Assert.That(session.State.Tray.Select(x => x.ItemId),
                Is.EqualTo(new[] { "sneaker", "sweater", "laptop" }));
            Assert.That(session.State.Targets, Is.EqualTo(new[] { "laptop", "sneaker", "sweater" }));
        }

        [Test]
        public void ResetLevel_RestoresInitialStateAndClearsUndoHistory()
        {
            var session = Session();
            var initial = CanonicalStateSerializer.Serialize(session.State);
            var initialHash = StateHash.Compute(session.State);
            Assert.That(session.PlaceItem("sneaker", new Cell(1, 1), Rotation.Degrees0, "open").IsAccepted,
                Is.True);
            Assert.That(session.MoveItem("sneaker", new Cell(2, 2)).IsAccepted, Is.True);
            Assert.That(session.RotateItem("sneaker", Rotation.Degrees90).IsAccepted, Is.True);
            Assert.That(session.UndoDepth, Is.EqualTo(3));

            var reset = session.ResetLevel();
            Assert.That(reset.IsAccepted, Is.True);
            Assert.That(reset.NewState, Is.SameAs(session.State));
            Assert.That(reset.Events, Is.Empty);
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(initial));
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(initialHash));
            Assert.That(session.State.Placements, Is.Empty);
            Assert.That(session.State.Occupancy, Is.Empty);
            Assert.That(session.State.Tray.Select(x => x.ItemId),
                Is.EqualTo(new[] { "sneaker", "sweater", "laptop" }));
            Assert.That(session.State.Targets, Is.EqualTo(new[] { "laptop", "sneaker", "sweater" }));
            Assert.That(session.UndoDepth, Is.Zero);

            var stateAfterReset = session.State;
            var undo = session.Undo();
            Assert.That(undo.IsAccepted, Is.False);
            Assert.That(undo.Reason, Is.EqualTo("NoUndoAvailable"));
            Assert.That(undo.NewState, Is.Null);
            Assert.That(undo.Events, Is.Empty);
            Assert.That(session.State, Is.SameAs(stateAfterReset));
            Assert.That(session.UndoDepth, Is.Zero);
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(initial));
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(initialHash));
        }

        [Test]
        public void RepeatedActionSequence_HasSameCanonicalBytesAndHash()
        {
            PackSession Run()
            {
                var session = Session();
                session.RotateItem("sneaker", Rotation.Degrees90);
                session.PlaceItem("sneaker", new Cell(1, 1), Rotation.Degrees90, "open");
                session.FoldItem("sweater", "folded");
                session.PlaceItem("sweater", new Cell(3, 1), Rotation.Degrees0, "folded");
                return session;
            }

            var first = Run();
            var second = Run();
            Assert.That(CanonicalStateSerializer.Serialize(first.State),
                Is.EqualTo(CanonicalStateSerializer.Serialize(second.State)));
            Assert.That(StateHash.Compute(first.State), Is.EqualTo(StateHash.Compute(second.State)));
        }
    }
}
