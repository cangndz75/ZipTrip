using System;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PuzzleSessionTests
    {
        private static ItemSpec Shoe() => Block("shoe", 2, 1, nest: new NestSpec(1, new[] { "socks" }, null));

        private static PuzzleLevel Level(PuzzleObjective objective, PuzzleRule[] rules, params PuzzleItem[] items) =>
            new PuzzleLevel("test", State(Spec(), items), new RuleSet(Board(), rules), objective);

        // Pack: a (2x1) and b (1x1) start in the Source Tray.
        private static PuzzleSession PackSession(params PuzzleRule[] rules) => new PuzzleSession(Level(
            PuzzleObjective.Pack(new[] { "a", "b" }), rules,
            Item("a", Block("a", 2, 1), ItemLocation.SourceTray), Item("b", Block("b", 1, 1), ItemLocation.SourceTray)));

        private static readonly PuzzleMove PlaceA = PuzzleMove.PlaceInSuitcase("a", "main", new Cell(0, 0), Rotation.Degrees0);
        private static readonly PuzzleMove PlaceB = PuzzleMove.PlaceInSuitcase("b", "main", new Cell(3, 0), Rotation.Degrees0);
        private static readonly PuzzleMove Illegal = PuzzleMove.PlaceInSuitcase("b", "main", new Cell(9, 9), Rotation.Degrees0);

        private static PuzzleItem Get(PuzzleState state, string id)
        {
            state.TryGetItem(id, out var item);
            return item;
        }

        private static void AssertUnchanged(PuzzleSession session, PuzzleSessionStep step, PuzzleState state, int moves, int depth)
        {
            Assert.That(step.StateChanged, Is.False);
            Assert.That(step.CompletionReached, Is.False);
            Assert.That(session.CurrentState, Is.SameAs(state));
            Assert.That(session.CurrentState.Hash, Is.EqualTo(state.Hash));
            Assert.That(session.MoveCount, Is.EqualTo(moves));
            Assert.That(session.UndoDepth, Is.EqualTo(depth));
        }

        // ---- Basic session ----

        [Test]
        public void NewSession_StartsFromTheExactInitialState()
        {
            var session = PackSession();
            Assert.That(session.CurrentState, Is.SameAs(session.Level.InitialState));
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.UndoDepth, Is.Zero);
            Assert.That(session.CanUndo, Is.False);
            Assert.That(session.CurrentCompletion.IsComplete, Is.False);
        }

        [Test]
        public void AcceptedMove_ChangesStateAndCountsOnce_RejectedMoveChangesNothing()
        {
            var session = PackSession();
            var accepted = session.Apply(PlaceA);
            Assert.That(accepted.StateChanged, Is.True);
            Assert.That(accepted.Move.IsAccepted, Is.True);
            Assert.That(session.CurrentState, Is.SameAs(accepted.Move.State));
            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(session.UndoDepth, Is.EqualTo(1));

            var before = session.CurrentState;
            var rejected = session.Apply(Illegal);
            Assert.That(rejected.Move.Rejection, Is.EqualTo(MoveRejection.NoLegalLayer));
            AssertUnchanged(session, rejected, before, 1, 1);
            AssertUnchanged(session, session.Apply(null), before, 1, 1);
        }

        // ---- Undo ----

        [Test]
        public void Undo_RestoresExactSnapshotsAndMoveCounts()
        {
            var session = PackSession();
            var initial = session.CurrentState;
            session.Apply(PlaceA);
            var afterA = session.CurrentState;
            session.Apply(Illegal);
            session.Apply(PlaceB);
            Assert.That(session.MoveCount, Is.EqualTo(2));
            Assert.That(session.UndoDepth, Is.EqualTo(2), "the rejected move created no history frame");

            var undo = session.Undo();
            Assert.That(undo.StateChanged, Is.True);
            Assert.That(undo.Move, Is.Null);
            Assert.That(session.CurrentState.ToStableBytes(), Is.EqualTo(afterA.ToStableBytes()));
            Assert.That(session.MoveCount, Is.EqualTo(1));

            session.Undo();
            Assert.That(session.CurrentState.Hash, Is.EqualTo(initial.Hash));
            Assert.That(session.MoveCount, Is.Zero);

            AssertUnchanged(session, session.Undo(), session.CurrentState, 0, 0);
        }

        [Test]
        public void Undo_OfParentMove_RestoresWholeContainment()
        {
            var session = new PuzzleSession(Level(PuzzleObjective.Pack(new[] { "shoe-1" }), new PuzzleRule[0],
                Placed("shoe-1", Shoe(), At(0, 0)), Item("socks-1", Block("socks", 1, 1), ItemLocation.NestedIn("shoe-1"))));
            var initial = session.CurrentState;
            var step = session.Apply(PuzzleMove.MoveToStaging("shoe-1"));
            Assert.That(step.Move.MovedInstanceIds, Is.EqualTo(new[] { "shoe-1", "socks-1" }));
            Assert.That(session.MoveCount, Is.EqualTo(1), "parent carrying children is one move");

            session.Undo();
            Assert.That(session.CurrentState.Hash, Is.EqualTo(initial.Hash));
            Assert.That(Get(session.CurrentState, "socks-1").Location, Is.EqualTo(ItemLocation.NestedIn("shoe-1")));
            Assert.That(Get(session.CurrentState, "shoe-1").Location.Kind, Is.EqualTo(ItemLocationKind.Suitcase));
        }

        // ---- Rotation / Fold / Compress ----

        [Test]
        public void RotationFoldAndCompressSelections_AreOneMoveEach_AndUndoRestoresThem()
        {
            var session = new PuzzleSession(new PuzzleLevel("test", State(Spec(board: Board(6, 4)),
                    Item("rod", Block("rod", 3, 1), ItemLocation.SourceTray),
                    Item("sw", Sweater(), ItemLocation.SourceTray),
                    Item("j", Jacket(), ItemLocation.Staging)),
                new RuleSet(Board(6, 4), null), PuzzleObjective.Pack(new[] { "rod", "sw", "j" })));

            session.Apply(PuzzleMove.PlaceInSuitcase("rod", "main", new Cell(5, 0), Rotation.Degrees90));
            Assert.That(session.MoveCount, Is.EqualTo(1));
            session.Apply(PuzzleMove.PlaceInSuitcase("sw", "main", new Cell(0, 0), Rotation.Degrees0, "folded"));
            Assert.That(session.MoveCount, Is.EqualTo(2));
            var beforeCompress = session.CurrentState;
            session.Apply(PuzzleMove.PlaceInSuitcase("j", "main", new Cell(2, 0), Rotation.Degrees0, "compressed"));
            Assert.That(session.MoveCount, Is.EqualTo(3));
            Assert.That(Get(session.CurrentState, "j").StateId, Is.EqualTo("compressed"));

            session.Undo();
            Assert.That(session.CurrentState, Is.SameAs(beforeCompress));
            Assert.That(Get(session.CurrentState, "j").StateId, Is.EqualTo("normal"));
            Assert.That(Get(session.CurrentState, "j").Location.Kind, Is.EqualTo(ItemLocationKind.Staging));
            session.Undo();
            Assert.That(Get(session.CurrentState, "sw").StateId, Is.EqualTo("open"));
            session.Undo();
            Assert.That(Get(session.CurrentState, "rod").Location.Kind, Is.EqualTo(ItemLocationKind.SourceTray));
            Assert.That(session.MoveCount, Is.Zero);
        }

        // ---- Completion edges ----

        [Test]
        public void Pack_CompletionFiresOnlyOnIncompleteToCompleteEdges()
        {
            var session = PackSession();
            var edges = 0;
            Action<PuzzleSessionStep> count = s => edges += s.CompletionReached ? 1 : 0;

            count(session.Apply(PlaceA));
            Assert.That(edges, Is.Zero);
            var final = session.Apply(PlaceB);
            count(final);
            Assert.That(final.CompletionReached, Is.True);
            Assert.That(session.CurrentCompletion.IsComplete, Is.True);
            Assert.That(edges, Is.EqualTo(1));

            Assert.That(session.CurrentCompletion.IsComplete, Is.True, "querying again fires nothing");
            count(session.Apply(Illegal));
            Assert.That(edges, Is.EqualTo(1), "rejected move while complete");

            var undo = session.Undo();
            count(undo);
            Assert.That(undo.Completion.IsComplete, Is.False);
            Assert.That(edges, Is.EqualTo(1), "undo to incomplete is not a completion");

            count(session.Apply(PlaceB));
            Assert.That(edges, Is.EqualTo(2), "a new incomplete -> complete edge fires again");
        }

        [Test]
        public void UndoBackIntoACompleteState_IsAlsoAnEdge()
        {
            var session = PackSession();
            session.Apply(PlaceA);
            session.Apply(PlaceB);
            var out_ = session.Apply(PuzzleMove.MoveToStaging("b"));
            Assert.That(out_.Completion.IsComplete, Is.False);
            var undo = session.Undo();
            Assert.That(undo.CompletionReached, Is.True, "generic edge rule, not only after Apply");
        }

        [Test]
        public void InitiallyCompleteLevel_IsCompleteWithoutAnEdge()
        {
            var session = new PuzzleSession(Level(PuzzleObjective.Pack(new[] { "a" }), new PuzzleRule[0],
                Placed("a", Block("a", 1, 1), At(0, 0))));
            Assert.That(session.CurrentCompletion.IsComplete, Is.True);
            var step = session.Apply(PuzzleMove.PlaceInSuitcase("a", "main", new Cell(1, 0), Rotation.Degrees0));
            Assert.That(step.StateChanged, Is.True);
            Assert.That(step.CompletionReached, Is.False, "complete -> complete is not an edge");
        }

        // ---- Extract ----

        [Test]
        public void Extract_TargetToDestination_CompletesOnce_AndUndoReturnsIt()
        {
            var session = new PuzzleSession(Level(PuzzleObjective.Extract("laptop-a", "tray"), new PuzzleRule[0],
                Placed("laptop-a", Block("laptop", 2, 2), At(0, 0), ObjectiveRole.ExtractionTarget),
                Placed("laptop-b", Block("laptop", 2, 2), At(2, 0))));
            var initial = session.CurrentState;
            Assert.That(session.CurrentCompletion.IsComplete, Is.False);

            var step = session.Apply(PuzzleMove.MoveToDestination("laptop-a", "tray"));
            Assert.That(session.MoveCount, Is.EqualTo(1));
            Assert.That(CompletionEvaluator.IsInDestination(session.CurrentState, "laptop-a", "tray"), Is.True);
            Assert.That(step.CompletionReached, Is.True);
            Assert.That(session.CurrentCompletion.IsComplete, Is.True);

            var undo = session.Undo();
            Assert.That(undo.CompletionReached, Is.False);
            Assert.That(Get(session.CurrentState, "laptop-a").Location.Kind, Is.EqualTo(ItemLocationKind.Suitcase));
            Assert.That(session.CurrentState.Hash, Is.EqualTo(initial.Hash));
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(session.CurrentCompletion.IsComplete, Is.False);
        }

        [Test]
        public void LoadedExtractFixture_PlaysThroughTheSession()
        {
            var session = new PuzzleSession(LevelSchemaV2Tests.Fixture("extract.json"));
            Assert.That(session.Apply(PuzzleMove.MoveToDestination("laptop-a", "security-tray")).Move.Rejection,
                Is.EqualTo(MoveRejection.NotAccessible), "the book covers the target");
            session.Apply(PuzzleMove.MoveToStaging("book-1"));
            session.Apply(PuzzleMove.MoveToDestination("laptop-a", "security-tray"));
            var last = session.Apply(PuzzleMove.PlaceInSuitcase("book-1", "main", new Cell(0, 0), Rotation.Degrees0));
            Assert.That(last.CompletionReached, Is.True);
            Assert.That(session.MoveCount, Is.EqualTo(3));
        }

        // ---- Rule violation vs legality ----

        [Test]
        public void RuleBreakingButLegalMove_IsAcceptedAndCounted()
        {
            var session = PackSession(new AdjacencyForbiddenRule("apart", ItemSelector.Instance("a"), ItemSelector.Instance("b")));
            session.Apply(PlaceA);
            var step = session.Apply(PuzzleMove.PlaceInSuitcase("b", "main", new Cell(2, 0), Rotation.Degrees0));
            Assert.That(step.Move.IsAccepted, Is.True);
            Assert.That(step.StateChanged, Is.True);
            Assert.That(session.MoveCount, Is.EqualTo(2));
            Assert.That(step.CompletionReached, Is.False);
            Assert.That(session.CurrentCompletion.Failures.Single().Kind, Is.EqualTo(CompletionFailureKind.RuleViolation));
        }

        [Test]
        public void ExpectedGameplayFailures_DoNotThrow()
        {
            var session = new PuzzleSession(Level(PuzzleObjective.Pack(new[] { "shoe-1" }), new PuzzleRule[0],
                Placed("shoe-1", Shoe(), At(0, 0)), Placed("top", Block("top", 1, 1), At(0, 0, layer: 1)),
                Item("cam", Block("camera", 1, 1), ItemLocation.Staging), Item("cup", Block("cup", 1, 1), ItemLocation.Staging)));
            Assert.That(() => session.Apply(PuzzleMove.MoveToStaging("shoe-1")), Throws.Nothing);
            Assert.That(session.Apply(PuzzleMove.MoveToStaging("shoe-1")).Move.Rejection, Is.EqualTo(MoveRejection.NotAccessible));
            Assert.That(session.Apply(PuzzleMove.NestInto("cam", "shoe-1")).Move.Rejection, Is.EqualTo(MoveRejection.ParentNotAccessible));
            Assert.That(session.Apply(PuzzleMove.MoveToStaging("top")).Move.Rejection, Is.EqualTo(MoveRejection.InvariantViolation));
            Assert.That(session.MoveCount, Is.Zero);
            Assert.That(() => new PuzzleSession(null), Throws.ArgumentNullException);
        }
    }
}
