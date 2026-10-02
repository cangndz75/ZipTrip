using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Domain;
using ZipTrip.Domain.Solver;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class PackGameplayControllerTests
    {
        [UnityTest]
        public IEnumerator AcceptedReleaseUpdatesStateBeforeSnapAndEndsCanonical()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();

            DragTo(board, preview, "sneaker", new Cell(1, 1));

            Assert.That(gameplay.IsAnimating, Is.True);
            Assert.That(gameplay.Session.State.Placements.Single().Anchor,
                Is.EqualTo(new Cell(1, 1)));
            Assert.That(board.PresentedState, Is.SameAs(gameplay.Session.State));
            Assert.That(board.FindItemView("sneaker").transform.position.y,
                Is.GreaterThan(0f));
            Assert.That(PackGameplayController.SnapDurationSeconds, Is.LessThanOrEqualTo(0.12f));

            yield return WaitForTween(gameplay);
            AssertVector(board.FindItemView("sneaker").transform.position,
                new Vector3(1f, 0f, -1f));
        }

        [UnityTest]
        public IEnumerator RejectedTrayReleaseReturnsWithoutStateMutation()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            var before = CanonicalStateSerializer.Serialize(gameplay.Session.State);
            var view = board.FindItemView("sneaker");
            var restPosition = view.transform.position;
            var restScale = view.transform.localScale;

            DragTo(board, preview, "sneaker", new Cell(-2, 0));

            Assert.That(gameplay.IsAnimating, Is.True);
            Assert.That(CanonicalStateSerializer.Serialize(gameplay.Session.State), Is.EqualTo(before));
            Assert.That(board.PresentedState, Is.SameAs(gameplay.Session.State));
            yield return WaitForTween(gameplay);
            AssertVector(view.transform.position, restPosition);
            AssertVector(view.transform.localScale, restScale);
        }

        [UnityTest]
        public IEnumerator RejectedBoardMoveReturnsToPreviousAcceptedPosition()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            DragTo(board, preview, "sneaker", new Cell(1, 1));
            yield return WaitForTween(gameplay);

            var accepted = CanonicalStateSerializer.Serialize(gameplay.Session.State);
            var view = board.FindItemView("sneaker");
            var restPosition = view.transform.position;
            DragTo(board, preview, "sneaker", new Cell(-2, 0));

            Assert.That(CanonicalStateSerializer.Serialize(gameplay.Session.State), Is.EqualTo(accepted));
            Assert.That(gameplay.Session.State.Placements.Single(x => x.ItemId == "sneaker").Anchor,
                Is.EqualTo(new Cell(1, 1)));
            yield return WaitForTween(gameplay);
            AssertVector(view.transform.position, restPosition);
        }

        [UnityTest]
        public IEnumerator RotationAffordanceCyclesAllowedRotationsAndExcludesSingleRotation()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            Assert.That(gameplay.HasRotateAffordance("book"), Is.True);
            Assert.That(gameplay.HasRotateAffordance("camera"), Is.False);
            Assert.That(gameplay.TryRotateTrayItem("camera"), Is.False);

            var rotate = GameObject.Find("Rotate book");
            Assert.That(rotate, Is.Not.Null);
            var rotateScreen = (Vector2)Camera.main.WorldToScreenPoint(rotate.transform.position);
            Assert.That(gameplay.TryActivateControl(rotateScreen), Is.True);
            Assert.That(gameplay.Session.State.Tray.Single(x => x.ItemId == "book").Rotation,
                Is.EqualTo(Rotation.Degrees90));
            rotate = GameObject.Find("Rotate book");
            rotateScreen = (Vector2)Camera.main.WorldToScreenPoint(rotate.transform.position);
            Assert.That(gameplay.TryActivateControl(rotateScreen), Is.True);
            Assert.That(gameplay.Session.State.Tray.Single(x => x.ItemId == "book").Rotation,
                Is.EqualTo(Rotation.Degrees0));

            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            BeginDrag(board, preview, "book", out _);
            Assert.That(gameplay.TryRotateTrayItem("book"), Is.False);
            preview.HandlePointer(new PointerSignal(PointerPhase.Cancel, Vector2.zero));
            yield return null;
        }

        [UnityTest]
        public IEnumerator L1SimulatedInputCompletesExactlyOnceAndShowsPacked()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            yield return ReplayFirstSolution(root, false, new ReplayEvidence());

            Assert.That(root.Gameplay.Session.State.Tray, Is.Empty);
            Assert.That(root.Gameplay.LevelCompletedCount, Is.EqualTo(1));
            Assert.That(root.Gameplay.PackedVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator L2FirstSolutionUsesRotateAffordanceAndCompletes()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            root.LoadLevel("L2");
            yield return null;
            var evidence = new ReplayEvidence();
            yield return ReplayFirstSolution(root, true, evidence);

            Assert.That(evidence.UsedRotation, Is.True);
            Assert.That(root.Gameplay.Session.State.Tray, Is.Empty);
            Assert.That(root.Gameplay.LevelCompletedCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ResetRestoresExactInitialStateAndVisuals()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            var initial = CanonicalStateSerializer.Serialize(root.Level.InitialState);

            Assert.That(gameplay.TryRotateTrayItem("book"), Is.True);
            DragTo(board, preview, "sneaker", new Cell(1, 1));
            yield return WaitForTween(gameplay);
            gameplay.ResetLevel();
            yield return null;

            Assert.That(CanonicalStateSerializer.Serialize(gameplay.Session.State), Is.EqualTo(initial));
            Assert.That(CanonicalStateSerializer.Serialize(board.PresentedState), Is.EqualTo(initial));
            Assert.That(gameplay.Session.UndoDepth, Is.Zero);
            Assert.That(gameplay.PackedVisible, Is.False);
            Assert.That(board.ItemViews.Count, Is.EqualTo(root.Level.InitialState.Tray.Count));
            Assert.That(board.ItemViews.All(x => x.IsInTray), Is.True);
        }

        [UnityTest]
        public IEnumerator DebugLevelSelectLoadsImplementedDeviceLevels()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            foreach (var id in new[] { "L1", "L2", "L3", "L4" })
            {
                root.LoadLevel(id);
                yield return null;
                Assert.That(root.Level.Id, Is.EqualTo(id));
                Assert.That(root.Gameplay.Level.Id, Is.EqualTo(id));
                Assert.That(root.GetComponent<BoardPresenter>().PresentedState,
                    Is.SameAs(root.Gameplay.Session.State));
            }
            Assert.That(() => root.LoadLevel("L5"), Throws.ArgumentException);
        }

        private sealed class ReplayEvidence
        {
            public bool UsedRotation;
        }

        private static IEnumerator ReplayFirstSolution(PhaseAL1Presentation root,
            bool requireRotation, ReplayEvidence evidence)
        {
            var result = PackSolver.Solve(root.Level);
            Assert.That(result.Solvable, Is.True);
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            foreach (var placement in result.FirstSolution)
            {
                var tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                while (tray.Rotation != placement.Rotation)
                {
                    Assert.That(root.Gameplay.HasRotateAffordance(placement.ItemId), Is.True);
                    Assert.That(root.Gameplay.TryRotateTrayItem(placement.ItemId), Is.True);
                    evidence.UsedRotation = true;
                    tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                }
                DragTo(board, preview, placement.ItemId, placement.Anchor);
                yield return WaitForTween(root.Gameplay);
            }
            if (requireRotation)
                Assert.That(evidence.UsedRotation, Is.True);
        }

        private static void DragTo(BoardPresenter board, DragPreviewPresenter preview,
            string itemId, Cell anchor)
        {
            BeginDrag(board, preview, itemId, out var grabOffset);
            var desired = new Vector3(anchor.X, 0f, -anchor.Y);
            var pointerWorld = desired - grabOffset;
            var screen = (Vector2)Camera.main.WorldToScreenPoint(pointerWorld);
            preview.HandlePointer(new PointerSignal(PointerPhase.Move, screen));
            Assert.That(preview.CandidateAnchor, Is.EqualTo(anchor));
            preview.HandlePointer(new PointerSignal(PointerPhase.Up, screen));
        }

        private static void BeginDrag(BoardPresenter board, DragPreviewPresenter preview,
            string itemId, out Vector3 grabOffset)
        {
            var view = board.FindItemView(itemId);
            Assert.That(view, Is.Not.Null, itemId);
            var cell = view.Footprint.OccupiedCells[0];
            var point = view.transform.TransformPoint(new Vector3(cell.X + 0.5f, 0f,
                -cell.Y - 0.5f));
            grabOffset = view.transform.position - point;
            var screen = (Vector2)Camera.main.WorldToScreenPoint(point);
            preview.HandlePointer(new PointerSignal(PointerPhase.Down, screen));
            Assert.That(preview.ActiveItem, Is.SameAs(view));
        }

        private static IEnumerator WaitForTween(PackGameplayController gameplay)
        {
            var timeout = 1f;
            while (gameplay.IsAnimating && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(gameplay.IsAnimating, Is.False);
        }

        private static IEnumerator LoadSandbox()
        {
            yield return SceneManager.LoadSceneAsync("GameplaySandbox");
            yield return null;
            Assert.That(Object.FindFirstObjectByType<PackGameplayController>(), Is.Not.Null);
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That((actual - expected).sqrMagnitude, Is.LessThan(0.000001f));
        }
    }
}
