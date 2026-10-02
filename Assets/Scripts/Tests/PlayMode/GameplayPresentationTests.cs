#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
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
    public sealed class GameplayPresentationTests
    {
        [UnityTest]
        public IEnumerator DefaultViewHidesDevelopmentControlsAndRawGrid()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var hud = root.Gameplay.Hud;

            foreach (var name in new[] { "Level L1", "Level L2", "Level L3", "Level L4", "Reset", "Packed Result" })
                Assert.That(GameObject.Find(name), Is.Null, name);
            Assert.That(hud.DrawerOpen, Is.False);
            Assert.That(root.GetComponent<DebugGameplayOverlay>().IsVisible, Is.False);
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Any(x => x.name.StartsWith("Valid ") || x.name.StartsWith("Blocked ")), Is.False,
                "raw cell tiles must not be the container presentation");
            var board = root.GetComponent<BoardPresenter>();
            Assert.That(board.SeamAlpha, Is.EqualTo(BoardPresenter.SeamRestAlpha).Within(0.001f));
            Assert.That(board.ValidCellCount, Is.EqualTo(31));
            Assert.That(board.BlockedCellCount, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator DevelopmentDrawerKeepsLevelResetAndOverlayControls()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            Assert.That(gameplay.Hud.HasDevDrawer, Is.True);

            Assert.That(gameplay.TryActivateControl(UiCenter("Dev drawer handle")), Is.True);
            Assert.That(gameplay.Hud.DrawerOpen, Is.True);
            Assert.That(gameplay.TryActivateControl(UiCenter("Button L3")), Is.True);
            Assert.That(root.Level.Id, Is.EqualTo("L3"));
            Assert.That(gameplay.Hud.DrawerOpen, Is.False);

            Assert.That(gameplay.TryActivateControl(UiCenter("Dev drawer handle")), Is.True);
            Assert.That(gameplay.TryActivateControl(UiCenter("Button Debug")), Is.True);
            Assert.That(root.GetComponent<DebugGameplayOverlay>().IsVisible, Is.True);

            // Tapping outside an open drawer only closes it.
            var before = StateHash.Compute(gameplay.Session.State);
            Assert.That(gameplay.TryActivateControl(UiCenter("Dev drawer handle")), Is.True);
            Assert.That(gameplay.TryActivateControl(new Vector2(5f, 5f)), Is.True);
            Assert.That(gameplay.Hud.DrawerOpen, Is.False);
            Assert.That(StateHash.Compute(gameplay.Session.State), Is.EqualTo(before));
            yield return null;
        }

        [UnityTest]
        public IEnumerator TrayRegionsAndChipsNeverOverlapOnL1ToL4()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            foreach (var id in PhaseALevels.Ids)
            {
                root.LoadLevel(id);
                yield return null;
                var board = root.GetComponent<BoardPresenter>();
                var hud = root.Gameplay.Hud;
                var regions = new List<(string Owner, Rect Rect)>();
                foreach (var view in board.ItemViews.Where(x => x.IsInTray))
                {
                    foreach (var cell in view.Footprint.OccupiedCells)
                        regions.Add((view.ItemId, CellHitRect(view, cell)));
                    if (TrayAffordances.HasRotate(view.Item))
                        regions.Add((view.ItemId + " rotate", hud.ChipHitRect(view.ItemId, false)));
                    if (TrayAffordances.HasFold(view.Item))
                        regions.Add((view.ItemId + " fold", hud.ChipHitRect(view.ItemId, true)));
                }
                var screen = new Rect(0f, 0f, Camera.main.pixelWidth, Camera.main.pixelHeight);
                for (var i = 0; i < regions.Count; i++)
                {
                    Assert.That(screen.Contains(regions[i].Rect.min) && screen.Contains(regions[i].Rect.max),
                        Is.True, id + " " + regions[i].Owner + " leaves the screen");
                    for (var j = i + 1; j < regions.Count; j++)
                        if (regions[i].Owner != regions[j].Owner)
                            Assert.That(regions[i].Rect.Overlaps(regions[j].Rect), Is.False,
                                id + ": " + regions[i].Owner + " overlaps " + regions[j].Owner);
                }
            }
        }

        [UnityTest]
        public IEnumerator ChipsBelongToTheirItemAndRigidItemsGetNone()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            root.LoadLevel("L4");
            yield return null;
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();

            Assert.That(gameplay.HasRotateAffordance("camera"), Is.False);
            Assert.That(gameplay.HasFoldAffordance("camera"), Is.False);
            Assert.That(gameplay.HasFoldAffordance("laptop"), Is.False);
            Assert.That(gameplay.HasFoldAffordance("book"), Is.False);
            Assert.That(gameplay.Hud.ChipCount, Is.EqualTo(gameplay.RotateAffordanceCount + gameplay.FoldAffordanceCount));
            foreach (var view in board.ItemViews.Where(x => x.IsInTray))
                foreach (var fold in new[] { false, true })
                {
                    var control = gameplay.FindControl(view.ItemId, fold);
                    if (control == null)
                        continue;
                    var at = new Vector2(control.position.x, control.position.z);
                    Assert.That(view.TraySlot.Value.Card.Contains(at), Is.True, control.name);
                    foreach (var other in board.ItemViews.Where(x => x.IsInTray && x != view))
                        Assert.That(other.TraySlot.Value.Card.Contains(at), Is.False,
                            control.name + " sits in " + other.ItemId + " slot");
                }

            // Pressing the sweater Fold chip folds the sweater, not a neighbour.
            var foldScreen = (Vector2)Camera.main.WorldToScreenPoint(gameplay.FindControl("sweater", true).position);
            Assert.That(gameplay.TryActivateControl(foldScreen), Is.True);
            Assert.That(gameplay.Session.State.Tray.Single(x => x.ItemId == "sweater").ShapeState,
                Is.EqualTo("folded"));
            Assert.That(gameplay.Session.State.Tray.Single(x => x.ItemId == "scarf").ShapeState,
                Is.EqualTo("open"));
            yield return WaitIdle(gameplay);
            Assert.That(gameplay.FindControl("sweater", true).name, Is.EqualTo("Open sweater"));
        }

        [UnityTest]
        public IEnumerator FeedbackAnimationNeverChangesAuthoritativeState()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();

            Assert.That(gameplay.TryRotateTrayItem("book"), Is.True);
            var rotated = CanonicalStateSerializer.Serialize(gameplay.Session.State);
            var book = board.FindItemView("book");
            Assert.That(book.Rotation, Is.EqualTo(Rotation.Degrees90), "state-first rotation");
            Assert.That(book.FeedbackActive, Is.True);
            var root0 = book.transform.position;
            for (var t = 0f; t < ItemView.RotateSeconds + 0.1f; t += Time.unscaledDeltaTime)
            {
                Assert.That(CanonicalStateSerializer.Serialize(gameplay.Session.State), Is.EqualTo(rotated));
                Assert.That(book.transform.position, Is.EqualTo(root0), "feedback never moves ItemRoot");
                yield return null;
            }
            Assert.That(book.FeedbackActive, Is.False);
            Assert.That(book.FeedbackRoot.localRotation, Is.EqualTo(Quaternion.identity));

            DragTo(board, preview, "sneaker", new Cell(1, 1));
            var placed = CanonicalStateSerializer.Serialize(gameplay.Session.State);
            for (var t = 0f; t < PackGameplayController.SnapDurationSeconds + ItemView.SettleSeconds + 0.1f;
                 t += Time.unscaledDeltaTime)
            {
                Assert.That(CanonicalStateSerializer.Serialize(gameplay.Session.State), Is.EqualTo(placed));
                yield return null;
            }
            var sneaker = board.FindItemView("sneaker");
            Assert.That((sneaker.transform.position - new Vector3(1f, 0f, -1f)).sqrMagnitude, Is.LessThan(1e-8f));
            Assert.That(sneaker.FeedbackRoot.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator CompletionFiresOnceLocksInputAndCannotDuplicate()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var gameplay = root.Gameplay;
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            var initial = CanonicalStateSerializer.Serialize(root.Level.InitialState);
            yield return Solve(root);

            Assert.That(gameplay.LevelCompletedCount, Is.EqualTo(1));
            Assert.That(gameplay.PackedVisible, Is.True);
            Assert.That(gameplay.IsCompleted, Is.True);
            Assert.That(preview.InteractionEnabled, Is.False);
            var completed = StateHash.Compute(gameplay.Session.State);

            // A drag attempt on a packed item is ignored while the result is shown.
            var placed = board.ItemViews.First(x => !x.IsInTray);
            var cell = placed.Footprint.OccupiedCells[0];
            preview.HandlePointer(new PointerSignal(PointerPhase.Down, Camera.main.WorldToScreenPoint(
                placed.transform.TransformPoint(new Vector3(cell.X + 0.5f, 0f, -cell.Y - 0.5f)))));
            Assert.That(preview.ActiveItem, Is.Null);
            for (var t = 0f; t < 1f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(gameplay.LevelCompletedCount, Is.EqualTo(1));
            Assert.That(StateHash.Compute(gameplay.Session.State), Is.EqualTo(completed));
            Assert.That(Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .Count(x => x.name == "Packed card"), Is.EqualTo(1));
            Assert.That(gameplay.Hud.CompletionCard.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(0.001f));

            // Replay on the card restores the exact initial state and unlocks play.
            Assert.That(gameplay.TryActivateControl(UiCenter("Button Replay")), Is.True);
            Assert.That(CanonicalStateSerializer.Serialize(gameplay.Session.State), Is.EqualTo(initial));
            Assert.That(gameplay.PackedVisible, Is.False);
            Assert.That(gameplay.LevelCompletedCount, Is.Zero);
            gameplay.Hud.ReleasePress();
            yield return null;
            Assert.That(preview.InteractionEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator GhostShowsOneFootprintOutlineAndSeamsOnlyDuringDrag()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();

            BeginDrag(board, preview, "book", out var grabOffset);
            preview.HandlePointer(new PointerSignal(PointerPhase.Move,
                Camera.main.WorldToScreenPoint(new Vector3(1f, 0f, -1f) - grabOffset)));
            Assert.That(preview.Validation.IsValid, Is.True);
            Assert.That(board.DragEmphasis, Is.True);
            var bars = preview.GetComponentsInChildren<Transform>()
                .Count(x => x.name == "Cream outline bar");
            Assert.That(bars, Is.EqualTo(2 * (2 + 3)), "2x3 book outline is its perimeter only");
            for (var t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
                yield return null;
            Assert.That(board.SeamAlpha, Is.EqualTo(BoardPresenter.SeamDragAlpha).Within(0.001f));

            preview.CancelActiveImmediate();
            Assert.That(board.DragEmphasis, Is.False);
            Assert.That(board.FindItemView("book").FeedbackRoot.localScale, Is.EqualTo(Vector3.one));
        }

        [UnityTest]
        public IEnumerator InvalidMarkersStayAboveTheContainerRim()
        {
            yield return LoadSandbox();
            var root = Object.FindFirstObjectByType<PhaseAL1Presentation>();
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();

            BeginDrag(board, preview, "bottle", out var grabOffset);
            preview.HandlePointer(new PointerSignal(PointerPhase.Move,
                Camera.main.WorldToScreenPoint(new Vector3(0f, 0f, 0f) - grabOffset)));
            Assert.That(preview.Validation.Reason, Is.EqualTo(PlacementFailureReason.OutsideMask));
            Assert.That(preview.IsMarked(new Cell(0, 0)), Is.True);
            var markers = preview.GetComponentsInChildren<Transform>().Where(x => x.name.StartsWith("Invalid X")).ToArray();
            Assert.That(markers, Is.Not.Empty);
            foreach (var marker in markers)
                Assert.That(marker.position.y, Is.GreaterThan(board.ContainerVisualBounds.max.y),
                    "padded rim must not hide the offending-cell X");
            preview.CancelActiveImmediate();
            yield return null;
        }

        private static Rect CellHitRect(ItemView view, Cell cell)
        {
            var pad = view.HitPadding;
            var a = Camera.main.WorldToScreenPoint(view.transform.TransformPoint(
                new Vector3(cell.X - pad, 0f, -cell.Y + pad)));
            var b = Camera.main.WorldToScreenPoint(view.transform.TransformPoint(
                new Vector3(cell.X + 1f + pad, 0f, -cell.Y - 1f - pad)));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x),
                Mathf.Max(a.y, b.y));
        }

        private static Vector2 UiCenter(string name)
        {
            var rect = Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .Single(x => x.name == name);
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        private static IEnumerator Solve(PhaseAL1Presentation root)
        {
            var result = PackSolver.Solve(root.Level);
            var board = root.GetComponent<BoardPresenter>();
            var preview = root.GetComponent<DragPreviewPresenter>();
            foreach (var placement in result.FirstSolution)
            {
                var tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                while (tray.Rotation != placement.Rotation)
                {
                    Assert.That(root.Gameplay.TryRotateTrayItem(placement.ItemId), Is.True);
                    tray = root.Gameplay.Session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                }
                DragTo(board, preview, placement.ItemId, placement.Anchor);
                yield return WaitIdle(root.Gameplay);
            }
        }

        private static void DragTo(BoardPresenter board, DragPreviewPresenter preview, string itemId, Cell anchor)
        {
            BeginDrag(board, preview, itemId, out var grabOffset);
            var screen = (Vector2)Camera.main.WorldToScreenPoint(new Vector3(anchor.X, 0f, -anchor.Y) - grabOffset);
            preview.HandlePointer(new PointerSignal(PointerPhase.Move, screen));
            Assert.That(preview.CandidateAnchor, Is.EqualTo(anchor));
            preview.HandlePointer(new PointerSignal(PointerPhase.Up, screen));
        }

        private static void BeginDrag(BoardPresenter board, DragPreviewPresenter preview, string itemId,
            out Vector3 grabOffset)
        {
            var view = board.FindItemView(itemId);
            var cell = view.Footprint.OccupiedCells[0];
            var point = view.transform.TransformPoint(new Vector3(cell.X + 0.5f, 0f, -cell.Y - 0.5f));
            grabOffset = view.transform.position - point;
            preview.HandlePointer(new PointerSignal(PointerPhase.Down, Camera.main.WorldToScreenPoint(point)));
            Assert.That(preview.ActiveItem, Is.SameAs(view));
        }

        private static IEnumerator WaitIdle(PackGameplayController gameplay)
        {
            var timeout = 3f;
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
