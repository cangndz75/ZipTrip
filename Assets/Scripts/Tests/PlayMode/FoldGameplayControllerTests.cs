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
    public sealed class FoldGameplayControllerTests
    {
        [UnityTest]
        public IEnumerator FoldControlAppearsOnlyForFoldableTrayItemsAndDoesNotOverlapRotate()
        {
            yield return LoadL4();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;

            Assert.That(gameplay.HasFoldAffordance("sweater"), Is.True);
            Assert.That(gameplay.HasFoldAffordance("laptop"), Is.False);
            Assert.That(gameplay.FoldAffordanceCount, Is.EqualTo(2));
            var fold = GameObject.Find("Fold sweater");
            var rotate = GameObject.Find("Rotate sweater");
            Assert.That(fold, Is.Not.Null);
            Assert.That(rotate, Is.Not.Null);
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(x => x.name.StartsWith("Fold ") || x.name.StartsWith("Open ") ||
                            x.name.StartsWith("Rotate ")).ToArray();
            for (var i = 0; i < transforms.Length; i++)
            {
                var firstCenter = (Vector2)Camera.main.WorldToScreenPoint(transforms[i].position);
                var firstEdge = (Vector2)Camera.main.WorldToScreenPoint(
                    transforms[i].position + Vector3.right * 0.3f);
                var firstRadius = Mathf.Max(32f, Vector2.Distance(firstCenter, firstEdge) * 1.6f);
                for (var j = i + 1; j < transforms.Length; j++)
                {
                    if (transforms[i].name.StartsWith("Rotate ") &&
                        transforms[j].name.StartsWith("Rotate "))
                        continue;
                    var secondCenter = (Vector2)Camera.main.WorldToScreenPoint(transforms[j].position);
                    var secondEdge = (Vector2)Camera.main.WorldToScreenPoint(
                        transforms[j].position + Vector3.right * 0.3f);
                    var secondRadius = Mathf.Max(32f,
                        Vector2.Distance(secondCenter, secondEdge) * 1.6f);
                    Assert.That(Vector2.Distance(firstCenter, secondCenter),
                        Is.GreaterThan(firstRadius + secondRadius),
                        transforms[i].name + " overlaps " + transforms[j].name);
                }
            }
        }

        [UnityTest]
        public IEnumerator AcceptedFoldChangesStateFirstLocksInputAndEndsWithFoldedGoldenPrefab()
        {
            yield return LoadL4();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();

            Assert.That(gameplay.TryFoldTrayItem("sweater"), Is.True);
            Assert.That(gameplay.Session.State.Tray.Single(x => x.ItemId == "sweater").ShapeState,
                Is.EqualTo("folded"));
            Assert.That(gameplay.IsAnimating, Is.True);
            Assert.That(preview.InteractionEnabled, Is.False);
            Assert.That(board.FindItemView("sweater").ShapeState, Is.EqualTo("open"));
            Assert.That(gameplay.TryFoldTrayItem("sweater"), Is.False);
            Assert.That(gameplay.TryRotateTrayItem("sweater"), Is.False);

            TryBeginDrag(board, preview, "sweater");
            Assert.That(preview.ActiveItem, Is.Null);
            yield return WaitForAnimation(gameplay);

            var folded = board.FindItemView("sweater");
            Assert.That(folded.ShapeState, Is.EqualTo("folded"));
            Assert.That(folded.VisualPrefabInstance.name, Is.EqualTo("PF_Item_SweaterFolded"));
            Assert.That(folded.VisualRoot.localScale, Is.EqualTo(Vector3.one));
            Assert.That(preview.InteractionEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator RejectedFoldPreservesStateForRigidAndPlacedItems()
        {
            yield return LoadL4();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();

            var rigidHash = StateHash.Compute(gameplay.Session.State);
            Assert.That(gameplay.TryFoldTrayItem("laptop"), Is.False);
            Assert.That(StateHash.Compute(gameplay.Session.State), Is.EqualTo(rigidHash));

            DragTo(board, preview, "sweater", new Cell(1, 1));
            yield return WaitForAnimation(gameplay);
            var placedHash = StateHash.Compute(gameplay.Session.State);
            Assert.That(gameplay.TryFoldTrayItem("sweater"), Is.False);
            Assert.That(StateHash.Compute(gameplay.Session.State), Is.EqualTo(placedHash));
            Assert.That(gameplay.Session.State.Placements.Single(x => x.ItemId == "sweater")
                .ShapeState, Is.EqualTo("open"));
            Assert.That(gameplay.HasFoldAffordance("sweater"), Is.False);
        }

        [UnityTest]
        public IEnumerator FoldedGhostUsesCanonicalFootprintAndOpenReversesState()
        {
            yield return LoadL4();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();

            Assert.That(gameplay.TryFoldTrayItem("sweater"), Is.True);
            yield return WaitForAnimation(gameplay);
            var folded = board.FindItemView("sweater");
            Assert.That(folded.Footprint.OccupiedCells, Is.EqualTo(new[]
            {
                new Cell(0, 0), new Cell(1, 0), new Cell(0, 1), new Cell(1, 1),
                new Cell(0, 2), new Cell(1, 2), new Cell(0, 3), new Cell(1, 3)
            }));

            BeginDrag(board, preview, "sweater", out var grabOffset);
            var desired = new Vector3(1f, 0f, -1f);
            preview.HandlePointer(new PointerSignal(PointerPhase.Move,
                (Vector2)Camera.main.WorldToScreenPoint(desired - grabOffset)));
            Assert.That(preview.CandidateAnchor, Is.EqualTo(new Cell(1, 1)));
            Assert.That(preview.GhostCellCount, Is.EqualTo(8));
            preview.CancelActiveImmediate();

            Assert.That(gameplay.HasFoldAffordance("sweater"), Is.True);
            Assert.That(gameplay.TryFoldTrayItem("sweater"), Is.True);
            Assert.That(gameplay.Session.State.Tray.Single(x => x.ItemId == "sweater").ShapeState,
                Is.EqualTo("open"));
            yield return WaitForAnimation(gameplay);
            Assert.That(board.FindItemView("sweater").VisualPrefabInstance.name,
                Is.EqualTo("PF_Item_SweaterOpen"));
        }

        [UnityTest]
        public IEnumerator L4RequiresFoldAndCompletesThroughGameplayControls()
        {
            yield return LoadL4();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var dependency = PackSolver.AnalyzeFoldDependency(root.Level);
            Assert.That(dependency.WithoutFold.Solvable, Is.False);
            Assert.That(dependency.WithFold.Solvable, Is.True);
            Assert.That(dependency.WithFold.FirstSolution.Any(x =>
                x.ItemId == "sweater" && x.ShapeState == "folded"), Is.True);

            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            foreach (var placement in dependency.WithFold.FirstSolution)
            {
                var tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                while (tray.ShapeState != placement.ShapeState)
                {
                    Assert.That(root.Gameplay.TryFoldTrayItem(placement.ItemId), Is.True);
                    yield return WaitForAnimation(root.Gameplay);
                    tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                }
                while (tray.Rotation != placement.Rotation)
                {
                    Assert.That(root.Gameplay.TryRotateTrayItem(placement.ItemId), Is.True);
                    tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                }
                DragTo(board, preview, placement.ItemId, placement.Anchor);
                yield return WaitForAnimation(root.Gameplay);
            }

            Assert.That(root.Gameplay.Session.State.Tray, Is.Empty);
            Assert.That(root.Gameplay.LevelCompletedCount, Is.EqualTo(1));
            Assert.That(root.Gameplay.PackedVisible, Is.True);
        }

        private static IEnumerator LoadL4()
        {
            yield return SceneManager.LoadSceneAsync("GameplaySandbox");
            yield return null;
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            root.LoadLevel("L4");
            yield return null;
        }

        private static void DragTo(BoardPresenter board, DragPreviewPresenter preview,
            string itemId, Cell anchor)
        {
            BeginDrag(board, preview, itemId, out var grabOffset);
            var desired = new Vector3(anchor.X, 0f, -anchor.Y);
            var screen = (Vector2)Camera.main.WorldToScreenPoint(desired - grabOffset);
            preview.HandlePointer(new PointerSignal(PointerPhase.Move, screen));
            Assert.That(preview.CandidateAnchor, Is.EqualTo(anchor));
            preview.HandlePointer(new PointerSignal(PointerPhase.Up, screen));
        }

        private static void TryBeginDrag(BoardPresenter board, DragPreviewPresenter preview,
            string itemId)
        {
            var view = board.FindItemView(itemId);
            var cell = view.Footprint.OccupiedCells[0];
            var point = view.transform.TransformPoint(new Vector3(cell.X + 0.5f, 0f,
                -cell.Y - 0.5f));
            preview.HandlePointer(new PointerSignal(PointerPhase.Down,
                (Vector2)Camera.main.WorldToScreenPoint(point)));
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

        private static IEnumerator WaitForAnimation(PackGameplayController gameplay)
        {
            var timeout = 3f;
            while (gameplay.IsAnimating && timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(gameplay.IsAnimating, Is.False);
        }
    }
}
#endif
