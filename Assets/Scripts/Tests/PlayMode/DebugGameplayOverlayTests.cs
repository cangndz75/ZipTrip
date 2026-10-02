#if UNITY_EDITOR
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
    public sealed class DebugGameplayOverlayTests
    {
        [UnityTest]
        public IEnumerator SnapshotTracksAuthoritativeOccupancyAndDraggedItemData()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var overlay = root.GetComponent<DebugGameplayOverlay>();
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            Assert.That(overlay, Is.Not.Null);
            StringAssert.Contains("stateHash=0x" + StateHash.Compute(root.Gameplay.Session.State).ToString("X16"),
                overlay.SnapshotText);

            BeginDrag(board, preview, "sneaker", out var grabOffset);
            var desired = new Vector3(1f, 0f, -1f);
            var screen = (Vector2)Camera.main.WorldToScreenPoint(desired - grabOffset);
            preview.HandlePointer(new PointerSignal(PointerPhase.Move, screen));
            overlay.RefreshSnapshot();
            StringAssert.Contains("item=sneaker anchor=(1,1) rotation=0 shapeState=open", overlay.SnapshotText);
            StringAssert.Contains("footprint=(0,0),(0,1),(0,2),(1,2)", overlay.SnapshotText);
            preview.HandlePointer(new PointerSignal(PointerPhase.Up, screen));
            yield return WaitForTween(root.Gameplay);

            StringAssert.Contains("occupied[4]=(1,1),(1,2),(1,3),(2,3)", overlay.SnapshotText);
            StringAssert.Contains("[ACCEPTED] PlaceItem events=ItemPlaced", overlay.LogEntries.Last());
            StringAssert.Contains("stateHash=0x" + StateHash.Compute(root.Gameplay.Session.State).ToString("X16"),
                overlay.SnapshotText);
        }

        [UnityTest]
        public IEnumerator RejectedCommandLogsReasonAndPreservesAuthoritativeHash()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var overlay = root.GetComponent<DebugGameplayOverlay>();
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            var before = StateHash.Compute(root.Gameplay.Session.State);

            DragTo(board, preview, "sneaker", new Cell(-2, 0));
            Assert.That(StateHash.Compute(root.Gameplay.Session.State), Is.EqualTo(before));
            StringAssert.Contains("[REJECTED] PlaceItem reason=OutOfBounds", overlay.LogEntries.Last());
            yield return WaitForTween(root.Gameplay);
        }

        [UnityTest]
        public IEnumerator CompletionEventAndCopiedJsonAreReported()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var overlay = root.GetComponent<DebugGameplayOverlay>();
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            var solved = PackSolver.Solve(root.Level);
            Assert.That(solved.Solvable, Is.True);
            foreach (var placement in solved.FirstSolution)
            {
                var tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                while (tray.Rotation != placement.Rotation)
                {
                    Assert.That(root.Gameplay.TryRotateTrayItem(placement.ItemId), Is.True);
                    tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                }
                DragTo(board, preview, placement.ItemId, placement.Anchor);
                yield return WaitForTween(root.Gameplay);
            }

            Assert.That(overlay.LogEntries.Any(x => x.Contains("LevelCompleted")), Is.True);
            var json = overlay.CopyCurrentState();
            Assert.That(json, Is.EqualTo(DebugLevelExporter.Export(root.Level,
                root.Gameplay.Session.State)));
            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo(json));
        }

        private static void DragTo(BoardPresenter board, DragPreviewPresenter preview,
            string itemId, Cell anchor)
        {
            BeginDrag(board, preview, itemId, out var grabOffset);
            var desired = new Vector3(anchor.X, 0f, -anchor.Y);
            var screen = (Vector2)Camera.main.WorldToScreenPoint(desired - grabOffset);
            preview.HandlePointer(new PointerSignal(PointerPhase.Move, screen));
            preview.HandlePointer(new PointerSignal(PointerPhase.Up, screen));
        }

        private static void BeginDrag(BoardPresenter board, DragPreviewPresenter preview,
            string itemId, out Vector3 grabOffset)
        {
            var view = board.FindItemView(itemId);
            var cell = view.Footprint.OccupiedCells[0];
            var point = view.transform.TransformPoint(new Vector3(cell.X + 0.5f, 0f,
                -cell.Y - 0.5f));
            grabOffset = view.transform.position - point;
            preview.HandlePointer(new PointerSignal(PointerPhase.Down,
                (Vector2)Camera.main.WorldToScreenPoint(point)));
            Assert.That(preview.ActiveItem, Is.SameAs(view));
        }

        private static IEnumerator WaitForTween(PackGameplayController gameplay)
        {
            var timeout = 2f;
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
        }
    }
}
#endif
