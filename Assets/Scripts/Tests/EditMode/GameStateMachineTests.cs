using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ZipTrip.Application;
using ZipTrip.Domain;

namespace ZipTrip.Tests.EditMode
{
    public sealed class GameStateMachineTests
    {
        private static ContainerDefinition Cabin() => ContainerFixtures.CreateCabin(new Cell(0, 0));

        private static ItemDefinition CellItem(string id)
        {
            return new ItemDefinition(id, "base", new[]
            {
                new KeyValuePair<string, ItemShape>("base", new ItemShape(new[] { new Cell(0, 0) }))
            }, new[] { Rotation.Degrees0 }, Array.Empty<string>());
        }

        private static PlacedItem Place(string id, int x, int y)
        {
            return new PlacedItem(CellItem(id), new Cell(x, y), Rotation.Degrees0, "base");
        }

        private static GameState State(params PlacedItem[] placements)
        {
            return new GameState(Cabin(), placements, Array.Empty<string>(), Array.Empty<string>());
        }

        [Test]
        public void EmptyState_HasRepeatableCanonicalBytesAndHash()
        {
            var first = State();
            var second = State();

            Assert.That(CanonicalStateSerializer.Serialize(first),
                Is.EqualTo(CanonicalStateSerializer.Serialize(second)));
            Assert.That(StateHash.Compute(first), Is.EqualTo(StateHash.Compute(second)));
            Assert.That(StateHash.Compute(first), Is.EqualTo(0x84BE89F9A0EF9F1AUL));
        }

        [Test]
        public void SameLogicalState_FromIndependentInputs_HasSameBytesAndHash()
        {
            var first = new GameState(Cabin(), new[] { Place("b", 2, 3), Place("a", 1, 1) },
                new[] { "sweater", "shoe" }, new[] { "z", "a" });
            var second = new GameState(Cabin(), new[] { Place("a", 1, 1), Place("b", 2, 3) },
                new[] { "sweater", "shoe" }, new[] { "a", "z" });

            Assert.That(first.Placements.Select(p => p.ItemId), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(first.Occupancy, Is.EqualTo(new[] { new Cell(1, 1), new Cell(2, 3) }));
            Assert.That(first.Targets, Is.EqualTo(new[] { "a", "z" }));
            Assert.That(CanonicalStateSerializer.Serialize(first),
                Is.EqualTo(CanonicalStateSerializer.Serialize(second)));
            Assert.That(StateHash.Compute(first), Is.EqualTo(StateHash.Compute(second)));
        }

        [Test]
        public void Occupancy_IsFullyDerivedFromSerializedPlacementsRegardlessOfInsertionOrder()
        {
            var sneaker = new ItemDefinition("sneaker", "open", new[]
            {
                new KeyValuePair<string, ItemShape>("open", new ItemShape(new[]
                {
                    new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2)
                }))
            }, new[] { Rotation.Degrees0 }, Array.Empty<string>());
            var shoe = new PlacedItem(sneaker, new Cell(1, 1), Rotation.Degrees0, "open");
            var other = Place("other", 4, 4);
            var first = State(other, shoe);
            var second = State(shoe, other);
            var expected = new[]
            {
                new Cell(1, 1), new Cell(1, 2), new Cell(1, 3),
                new Cell(2, 3), new Cell(4, 4)
            };

            Assert.That(first.Occupancy, Is.EqualTo(expected));
            Assert.That(second.Occupancy, Is.EqualTo(expected));
            Assert.That(OccupancyFromSerializedPlacements(CanonicalStateSerializer.Serialize(first)),
                Is.EqualTo(expected));
            Assert.That(CanonicalStateSerializer.Serialize(first),
                Is.EqualTo(CanonicalStateSerializer.Serialize(second)));
            Assert.That(StateHash.Compute(first), Is.EqualTo(StateHash.Compute(second)));
        }

        [Test]
        public void EqualCanonicalBytes_RepresentEqualLogicalState()
        {
            var first = new GameState(Cabin(), new[] { Place("b", 3, 3), Place("a", 1, 1) },
                new[] { "first", "second" }, new[] { "b", "a" });
            var second = new GameState(Cabin(), new[] { Place("a", 1, 1), Place("b", 3, 3) },
                new[] { "first", "second" }, new[] { "a", "b" });
            var changedOwner = new GameState(Cabin(), new[] { Place("b", 1, 1), Place("a", 3, 3) },
                new[] { "first", "second" }, new[] { "a", "b" });

            Assert.That(CanonicalStateSerializer.Serialize(first),
                Is.EqualTo(CanonicalStateSerializer.Serialize(second)));
            AssertLogicalStateEqual(first, second);
            Assert.That(first.Occupancy, Is.EqualTo(changedOwner.Occupancy));
            Assert.That(CanonicalStateSerializer.Serialize(first),
                Is.Not.EqualTo(CanonicalStateSerializer.Serialize(changedOwner)));
        }

        [Test]
        public void DifferentLogicalState_ChangesCanonicalBytesAndHash()
        {
            var first = State();
            var second = State(Place("a", 1, 1));

            Assert.That(CanonicalStateSerializer.Serialize(first),
                Is.Not.EqualTo(CanonicalStateSerializer.Serialize(second)));
            Assert.That(StateHash.Compute(first), Is.Not.EqualTo(StateHash.Compute(second)));
        }

        [Test]
        public void ContainerIdMaskAndZipperEdge_AffectHash()
        {
            var cabin = Cabin();
            var baseline = State();
            var changedId = new ContainerDefinition("other", cabin.Mask, cabin.ZipperEdge);
            var changedMask = new ContainerDefinition(cabin.Id,
                new ContainerMask(new[] { new Cell(1, 1) }), cabin.ZipperEdge);
            var changedEdge = new ContainerDefinition(cabin.Id, cabin.Mask, ZipperEdge.Bottom);

            foreach (var container in new[] { changedId, changedMask, changedEdge })
            {
                var other = new GameState(container, Array.Empty<PlacedItem>(),
                    Array.Empty<string>(), Array.Empty<string>());
                Assert.That(StateHash.Compute(other), Is.Not.EqualTo(StateHash.Compute(baseline)));
            }
        }

        [Test]
        public void ItemIdentityRotationAndShapeState_AreAuthoritativeHashInputs()
        {
            var shape = new ItemShape(new[] { new Cell(0, 0) });
            ItemDefinition Definition(string id) => new ItemDefinition(id, "open", new[]
            {
                new KeyValuePair<string, ItemShape>("open", shape),
                new KeyValuePair<string, ItemShape>("folded", shape)
            }, new[] { Rotation.Degrees0, Rotation.Degrees90 }, Array.Empty<string>());

            GameState With(string id, Rotation rotation, string shapeState) => State(
                new PlacedItem(Definition(id), new Cell(1, 1), rotation, shapeState));

            var baseline = With("a", Rotation.Degrees0, "open");
            var identity = With("b", Rotation.Degrees0, "open");
            var rotated = With("a", Rotation.Degrees90, "open");
            var folded = With("a", Rotation.Degrees0, "folded");

            Assert.That(baseline.Occupancy, Is.EqualTo(identity.Occupancy));
            Assert.That(baseline.Occupancy, Is.EqualTo(rotated.Occupancy));
            Assert.That(baseline.Occupancy, Is.EqualTo(folded.Occupancy));
            Assert.That(StateHash.Compute(identity), Is.Not.EqualTo(StateHash.Compute(baseline)));
            Assert.That(StateHash.Compute(rotated), Is.Not.EqualTo(StateHash.Compute(baseline)));
            Assert.That(StateHash.Compute(folded), Is.Not.EqualTo(StateHash.Compute(baseline)));
        }

        [Test]
        public void StateCollections_AreDefensiveReadOnlySnapshots()
        {
            var placed = Place("a", 1, 1);
            var placements = new List<PlacedItem> { placed };
            var tray = new List<string> { "shoe", "sweater" };
            var targets = new List<string> { "shoe" };
            var container = Cabin();
            var state = new GameState(container, placements, tray, targets);
            var maskCopy = container.Mask.ToStableBytes();

            placements.Add(Place("b", 2, 2));
            tray[0] = "changed";
            targets.Clear();
            maskCopy[0] = 0;

            Assert.That(state.Placements.Select(p => p.ItemId), Is.EqualTo(new[] { "a" }));
            Assert.That(state.Occupancy, Is.EqualTo(new[] { new Cell(1, 1) }));
            Assert.That(state.Tray, Is.EqualTo(new[] { "shoe", "sweater" }));
            Assert.That(state.Targets, Is.EqualTo(new[] { "shoe" }));
            Assert.That(state.Container.Mask.IsValid(new Cell(1, 1)), Is.True);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<PlacedItem>)state.Placements)[0] = Place("c", 3, 3));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<Cell>)state.Occupancy)[0] = new Cell(3, 3));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)state.Tray)[0] = "changed");
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)state.Targets)[0] = "changed");
            Assert.Throws<NotSupportedException>(() =>
                ((IList<Cell>)placed.OccupiedCells)[0] = new Cell(3, 3));
        }

        [Test]
        public void State_RejectsDuplicateItemOverlapAndOutsideMask()
        {
            Assert.Throws<ArgumentException>(() => State(Place("a", 1, 1), Place("a", 2, 2)));
            Assert.Throws<ArgumentException>(() => State(Place("a", 1, 1), Place("b", 1, 1)));
            Assert.Throws<ArgumentException>(() => State(Place("a", 0, 0)));
        }

        [Test]
        public void TraySlotOrder_ChangesBytesAndHash()
        {
            var first = new GameState(Cabin(), Array.Empty<PlacedItem>(),
                new[] { "shoe", "sweater" }, Array.Empty<string>());
            var second = new GameState(Cabin(), Array.Empty<PlacedItem>(),
                new[] { "sweater", "shoe" }, Array.Empty<string>());

            Assert.That(CanonicalStateSerializer.Serialize(first),
                Is.Not.EqualTo(CanonicalStateSerializer.Serialize(second)));
            Assert.That(StateHash.Compute(first), Is.Not.EqualTo(StateHash.Compute(second)));
        }

        [Test]
        public void Fnv1a64_KnownVectorHello()
        {
            Assert.That(StateHash.Compute(Encoding.ASCII.GetBytes("hello")),
                Is.EqualTo(0xA430D84680AABD0BUL));
        }

        [Test]
        public void Serializer_UsesExplicitLittleEndianFieldOrderAndUtf8()
        {
            var container = new ContainerDefinition("x",
                new ContainerMask(new[] { new Cell(0, 0) }), ZipperEdge.Top);
            var state = new GameState(container, new[] { Place("i", 0, 0) },
                new[] { "é" }, new[] { "b", "a" });

            var expected = new List<byte>();
            expected.AddRange(new byte[] { 1, 0, 0, 0, 8, 0, 0, 0, 10, 0, 0, 0 });
            expected.AddRange(new byte[] { 1, 0, 0, 0, (byte)'x' });
            expected.AddRange(new byte[] { 0, 0, 0, 0, 10, 0, 0, 0 });
            expected.AddRange(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); // Mask.
            expected.AddRange(new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, (byte)'i' }); // Placement count and id.
            expected.AddRange(new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); // Anchor and rotation.
            expected.AddRange(new byte[] { 4, 0, 0, 0, (byte)'b', (byte)'a', (byte)'s', (byte)'e' });
            expected.AddRange(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }); // Cell count and cell.
            expected.AddRange(new byte[] { 1, 0, 0, 0, 2, 0, 0, 0, 0xC3, 0xA9 }); // Tray.
            expected.AddRange(new byte[] { 2, 0, 0, 0, 1, 0, 0, 0, (byte)'a', 1, 0, 0, 0, (byte)'b' });

            Assert.That(CanonicalStateSerializer.Serialize(state), Is.EqualTo(expected.ToArray()));
        }

        [Test]
        public void AcceptedCommand_ReturnsNewStateAndTypedEvents()
        {
            var initial = State();
            var next = State(Place("shoe", 1, 1));
            var events = new List<IGameEvent>
            {
                new ItemPlacedEvent("shoe", new Cell(1, 1)),
                new ItemRotatedEvent("shoe", Rotation.Degrees90),
                new FoldChangedEvent("sweater", "folded"),
                new ItemReturnedEvent("shoe"),
                new LevelCompletedEvent()
            };
            var session = new GameSession(initial);

            var result = session.Execute(new AcceptSnapshotCommand(next, events));
            events.Clear();

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(result.NewState, Is.SameAs(next));
            Assert.That(session.State, Is.SameAs(next));
            Assert.That(result.Reason, Is.Null);
            Assert.That(result.Events.Count, Is.EqualTo(5));
            Assert.That(((ItemPlacedEvent)result.Events[0]).Anchor, Is.EqualTo(new Cell(1, 1)));
            Assert.That(((ItemRotatedEvent)result.Events[1]).Rotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(((FoldChangedEvent)result.Events[2]).ShapeState, Is.EqualTo("folded"));
            Assert.That(((ItemReturnedEvent)result.Events[3]).ItemId, Is.EqualTo("shoe"));
            Assert.That(result.Events[4], Is.TypeOf<LevelCompletedEvent>());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<IGameEvent>)result.Events)[0] = new LevelCompletedEvent());
        }

        [Test]
        public void RejectedCommand_DoesNotChangeStateHashHistoryOrEvents()
        {
            var session = new GameSession(State());
            var original = session.State;
            var bytes = CanonicalStateSerializer.Serialize(original);
            var hash = StateHash.Compute(original);

            var result = session.Execute(new RejectFixtureCommand("FixtureRejected"));

            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Reason, Is.EqualTo("FixtureRejected"));
            Assert.That(result.NewState, Is.Null);
            Assert.That(result.Events, Is.Empty);
            Assert.That(session.State, Is.SameAs(original));
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(bytes));
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(hash));
            Assert.That(session.UndoDepth, Is.Zero);
            Assert.That(session.Undo(), Is.False);
        }

        [Test]
        public void RejectedCommand_PreservesExistingUndoSnapshot()
        {
            var initial = State();
            var session = new GameSession(initial);
            session.Execute(new AcceptSnapshotCommand(State(Place("a", 1, 1)), Array.Empty<IGameEvent>()));
            var before = CanonicalStateSerializer.Serialize(session.State);

            var result = session.Execute(new RejectFixtureCommand("FixtureRejected"));

            Assert.That(result.Events, Is.Empty);
            Assert.That(session.UndoDepth, Is.EqualTo(1));
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(before));
            Assert.That(session.Undo(), Is.True);
            Assert.That(CanonicalStateSerializer.Serialize(session.State),
                Is.EqualTo(CanonicalStateSerializer.Serialize(initial)));
        }

        [Test]
        public void Undo_RestoresByteEquivalentInitialState()
        {
            var initial = new GameState(Cabin(), new[] { Place("b", 4, 4), Place("a", 1, 1) },
                new[] { "shoe", "sweater" }, new[] { "b", "a" });
            var session = new GameSession(initial);
            var originalBytes = CanonicalStateSerializer.Serialize(initial);
            var next = new GameState(Cabin(), new[] { Place("a", 2, 2) },
                new[] { "sweater" }, new[] { "a" });

            session.Execute(new AcceptSnapshotCommand(next, Array.Empty<IGameEvent>()));
            Assert.That(session.UndoDepth, Is.EqualTo(1));
            Assert.That(session.Undo(), Is.True);

            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(originalBytes));
            AssertLogicalStateEqual(initial, session.State);
            Assert.That(session.UndoDepth, Is.Zero);
        }

        [Test]
        public void ThreeAcceptedMovesAndThreeUndos_RestoreInitialHash()
        {
            var initial = State(Place("a", 1, 1), Place("b", 5, 5));
            var session = new GameSession(initial);
            var initialHash = StateHash.Compute(initial);
            var initialBytes = CanonicalStateSerializer.Serialize(initial);

            Assert.That(session.Execute(new MoveCellCommand("a", new Cell(2, 1))).IsAccepted, Is.True);
            Assert.That(session.Execute(new MoveCellCommand("a", new Cell(3, 1))).IsAccepted, Is.True);
            Assert.That(session.Execute(new MoveCellCommand("a", new Cell(4, 1))).IsAccepted, Is.True);
            Assert.That(session.UndoDepth, Is.EqualTo(3));

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.Undo(), Is.False);
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(initialHash));
            Assert.That(CanonicalStateSerializer.Serialize(session.State), Is.EqualTo(initialBytes));
        }

        [Test]
        public void AcceptedTestMove_CannotOverlapExistingOccupancy()
        {
            var session = new GameSession(State(Place("a", 1, 1), Place("b", 3, 3)));
            var before = StateHash.Compute(session.State);

            var rejected = session.Execute(new MoveCellCommand("a", new Cell(3, 3)));
            var accepted = session.Execute(new MoveCellCommand("a", new Cell(2, 2)));

            Assert.That(rejected.IsAccepted, Is.False);
            Assert.That(rejected.Reason, Is.EqualTo("Overlap"));
            Assert.That(accepted.IsAccepted, Is.True);
            Assert.That(session.State.Occupancy, Is.EqualTo(new[] { new Cell(2, 2), new Cell(3, 3) }));
            Assert.That(session.State.Occupancy.Distinct().Count(), Is.EqualTo(2));
            Assert.That(StateHash.Compute(session.State), Is.Not.EqualTo(before));
            Assert.That(session.UndoDepth, Is.EqualTo(1));
        }

        [Test]
        public void EventPayloads_AreDeterministicAcrossIndependentCommands()
        {
            var firstState = State();
            var secondState = State();
            var first = new GameSession(firstState).Execute(new AcceptSnapshotCommand(firstState,
                new IGameEvent[] { new ItemPlacedEvent("shoe", new Cell(2, 3)), new LevelCompletedEvent() }));
            var second = new GameSession(secondState).Execute(new AcceptSnapshotCommand(secondState,
                new IGameEvent[] { new ItemPlacedEvent("shoe", new Cell(2, 3)), new LevelCompletedEvent() }));

            Assert.That(first.Events.Select(e => e.GetType()), Is.EqualTo(second.Events.Select(e => e.GetType())));
            Assert.That(((ItemPlacedEvent)first.Events[0]).ItemId,
                Is.EqualTo(((ItemPlacedEvent)second.Events[0]).ItemId));
            Assert.That(((ItemPlacedEvent)first.Events[0]).Anchor,
                Is.EqualTo(((ItemPlacedEvent)second.Events[0]).Anchor));
        }

        [Test]
        public void History_IsOutsideCanonicalStateHash()
        {
            var session = new GameSession(State());
            var before = StateHash.Compute(session.State);
            session.Execute(new AcceptSnapshotCommand(State(), Array.Empty<IGameEvent>()));

            Assert.That(session.UndoDepth, Is.EqualTo(1));
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(before));
        }

        [Test]
        public void SeededTwoHundredCommandSequences_ProduceSameFinalBytesAndHash()
        {
            var first = RunSequence(0x5A17u);
            var second = RunSequence(0x5A17u);

            Assert.That(first.Executed, Is.EqualTo(200));
            Assert.That(first.Accepted, Is.GreaterThan(0));
            Assert.That(first.Rejected, Is.GreaterThan(0));
            Assert.That(first.Accepted, Is.EqualTo(second.Accepted));
            Assert.That(first.Rejected, Is.EqualTo(second.Rejected));
            Assert.That(first.Bytes, Is.EqualTo(second.Bytes));
            Assert.That(first.Hash, Is.EqualTo(second.Hash));
        }

        private static (int Executed, int Accepted, int Rejected, byte[] Bytes, ulong Hash) RunSequence(uint seed)
        {
            var session = new GameSession(State(Place("a", 1, 1), Place("b", 5, 5)));
            var accepted = 0;
            var rejected = 0;

            for (var i = 0; i < 200; i++)
            {
                seed = unchecked(seed * 1664525u + 1013904223u);
                var x = (int)(seed % 8u);
                seed = unchecked(seed * 1664525u + 1013904223u);
                var y = (int)(seed % 10u);
                var result = session.Execute(new MoveCellCommand("a", new Cell(x, y)));
                if (result.IsAccepted)
                {
                    accepted++;
                    Assert.That(session.State.Occupancy.Distinct().Count(),
                        Is.EqualTo(session.State.Occupancy.Count));
                }
                else
                {
                    rejected++;
                    Assert.That(result.Events, Is.Empty);
                }
            }

            return (200, accepted, rejected,
                CanonicalStateSerializer.Serialize(session.State), StateHash.Compute(session.State));
        }

        private static Cell[] OccupancyFromSerializedPlacements(byte[] bytes)
        {
            using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
            {
                Assert.That(reader.ReadInt32(), Is.EqualTo(1));
                Assert.That(reader.ReadInt32(), Is.EqualTo(GridSize.Width));
                Assert.That(reader.ReadInt32(), Is.EqualTo(GridSize.Height));
                ReadString(reader); // Container id.
                reader.ReadInt32(); // Zipper edge.
                reader.ReadBytes(reader.ReadInt32()); // Mask.

                var cells = new List<Cell>();
                var placementCount = reader.ReadInt32();
                for (var i = 0; i < placementCount; i++)
                {
                    ReadString(reader); // Item id.
                    reader.ReadInt32(); // Anchor X.
                    reader.ReadInt32(); // Anchor Y.
                    reader.ReadInt32(); // Rotation.
                    ReadString(reader); // Shape state.
                    var cellCount = reader.ReadInt32();
                    for (var j = 0; j < cellCount; j++)
                        cells.Add(new Cell(reader.ReadInt32(), reader.ReadInt32()));
                }

                cells.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
                return cells.ToArray();
            }
        }

        private static string ReadString(BinaryReader reader)
        {
            return Encoding.UTF8.GetString(reader.ReadBytes(reader.ReadInt32()));
        }

        private static void AssertLogicalStateEqual(GameState expected, GameState actual)
        {
            Assert.That(actual.Container.Id, Is.EqualTo(expected.Container.Id));
            Assert.That(actual.Container.ZipperEdge, Is.EqualTo(expected.Container.ZipperEdge));
            Assert.That(actual.Container.Mask.ToStableBytes(),
                Is.EqualTo(expected.Container.Mask.ToStableBytes()));
            Assert.That(actual.Placements.Select(p => p.ItemId),
                Is.EqualTo(expected.Placements.Select(p => p.ItemId)));
            for (var i = 0; i < expected.Placements.Count; i++)
            {
                Assert.That(actual.Placements[i].Anchor, Is.EqualTo(expected.Placements[i].Anchor));
                Assert.That(actual.Placements[i].Rotation, Is.EqualTo(expected.Placements[i].Rotation));
                Assert.That(actual.Placements[i].ShapeState, Is.EqualTo(expected.Placements[i].ShapeState));
                Assert.That(actual.Placements[i].OccupiedCells,
                    Is.EqualTo(expected.Placements[i].OccupiedCells));
            }
            Assert.That(actual.Occupancy, Is.EqualTo(expected.Occupancy));
            Assert.That(actual.Tray, Is.EqualTo(expected.Tray));
            Assert.That(actual.Targets, Is.EqualTo(expected.Targets));
        }

        private sealed class AcceptSnapshotCommand : ICommand
        {
            private readonly GameState _next;
            private readonly IEnumerable<IGameEvent> _events;

            public AcceptSnapshotCommand(GameState next, IEnumerable<IGameEvent> events)
            {
                _next = next;
                _events = events;
            }

            public CommandResult Execute(GameState state) => CommandResult.Accepted(_next, _events);
        }

        private sealed class RejectFixtureCommand : ICommand
        {
            private readonly string _reason;

            public RejectFixtureCommand(string reason) { _reason = reason; }

            public CommandResult Execute(GameState state) => CommandResult.Rejected(_reason);
        }

        // Test-only movement fixture; Pack use cases belong to ZT-005.
        private sealed class MoveCellCommand : ICommand
        {
            private readonly string _itemId;
            private readonly Cell _to;

            public MoveCellCommand(string itemId, Cell to)
            {
                _itemId = itemId;
                _to = to;
            }

            public CommandResult Execute(GameState state)
            {
                var current = state.Placements.FirstOrDefault(p => p.ItemId == _itemId);
                if (current == null)
                    return CommandResult.Rejected("MissingItem");

                var remaining = state.Placements.Where(p => p.ItemId != _itemId).ToArray();
                var board = new PlacementBoard(state.Container,
                    remaining.SelectMany(p => p.OccupiedCells));
                var item = CellItem(_itemId);
                var validation = PlacementValidator.Validate(board, item, _to,
                    Rotation.Degrees0, "base");
                if (!validation.IsValid)
                    return CommandResult.Rejected(validation.Reason.ToString());

                var moved = new PlacedItem(item, _to, Rotation.Degrees0, "base");
                var next = new GameState(state.Container, remaining.Concat(new[] { moved }),
                    state.Tray, state.Targets);
                return CommandResult.Accepted(next, Array.Empty<IGameEvent>());
            }
        }
    }
}
