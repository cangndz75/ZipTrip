#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Domain;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // ZT-041 limited staging, end to end in the shipped PuzzleGameplay scene (golden suitcase) with a test-only fixture.
    // Legality always comes from PuzzleTransitions; these tests check that presentation routes through it, reflects
    // state, and never moves anything on its own.
    public sealed class StagingInteractionTests
    {
        private static IEnumerator Gameplay()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static PuzzleGameplayScene Scene() => Object.FindFirstObjectByType<PuzzleGameplayScene>();

        // Board-plane pointer that lands on a staging pad (pads rest lower than the board plane).
        internal static Vector3 OverSlot(PuzzleGameplayScene scene, int slot) =>
            GridProjector.ScreenToWorld(scene.Camera, scene.Camera.WorldToScreenPoint(scene.Staging.SlotCenter(slot)));

        // Grab point on the first footprint cell of a board item (board plane) or an off-board item (its own plane).
        internal static Vector3 Grab(PuzzleItemView view)
        {
            var first = view.Footprint.OccupiedCells[0];
            return view.transform.position + view.transform.lossyScale.x * new Vector3(first.X + 0.5f, 0f, -(first.Y + 0.5f));
        }

        private static ItemLocation Where(PuzzleGameplayScene scene, string id) =>
            scene.Session.CurrentState.TryGetItem(id, out var item) ? item.Location : default;

        internal static PuzzleSessionStepResult Stage(PuzzleGameplayScene scene, string id, int slot)
        {
            var view = scene.Board.ItemViews[id];
            var begin = scene.Drag.BeginDrag(id, Grab(view));
            scene.Drag.UpdateDrag(OverSlot(scene, slot));
            var hover = (scene.Staging.HoverSlot, scene.Staging.HoverValid);
            var step = scene.Drag.Drop();
            return new PuzzleSessionStepResult(begin, hover.HoverSlot, hover.HoverValid, step);
        }

        internal readonly struct PuzzleSessionStepResult
        {
            public DragBeginResult Begin { get; }
            public int HoverSlot { get; }
            public bool HoverValid { get; }
            public ZipTrip.Application.PuzzleSessionStep Step { get; }

            public PuzzleSessionStepResult(DragBeginResult begin, int hoverSlot, bool hoverValid, ZipTrip.Application.PuzzleSessionStep step)
            {
                Begin = begin;
                HoverSlot = hoverSlot;
                HoverValid = hoverValid;
                Step = step;
            }
        }

        [UnityTest]
        public IEnumerator StagingAppears_OnlyWhenTheLevelHasCapacity()
        {
            yield return Gameplay();
            var scene = Scene();
            Assert.That(scene.Level.Spec.StagingCapacity, Is.Zero);
            Assert.That(scene.Staging.gameObject.activeSelf, Is.False, "Lv1: no staging UI");
            Assert.That(scene.Staging.Pads, Is.Empty);

            scene.LoadLevel(StagingFixture.Load(), "Staging");
            yield return null;
            Assert.That(scene.Staging.gameObject.activeSelf, Is.True);
            Assert.That(scene.Staging.Capacity, Is.EqualTo(2), "capacity sourced from PuzzleSpec");
            Assert.That(scene.Staging.Pads.Count, Is.EqualTo(2));
            Assert.That(scene.Staging.GetComponentsInChildren<Component>(true), Has.None.Null, "no missing scripts");
            Assert.That(scene.Staging.GetComponentsInChildren<Collider>(true), Is.Empty);

            scene.NextLevel();
            yield return null;
            Assert.That(scene.Staging.gameObject.activeSelf, Is.False, "back to shipped progression: no staging");
        }

        [UnityTest]
        public IEnumerator SuitcaseToStaging_IsOneMove_PresenterFollowsState_UndoRestoresPlacement()
        {
            yield return Gameplay();
            var scene = Scene();
            scene.LoadLevel(StagingFixture.Load(), "Staging");
            yield return null;
            var original = Where(scene, "laptop-1");
            var hash = scene.Session.CurrentState.Hash;

            var result = Stage(scene, "laptop-1", 1);
            Assert.That(result.Begin, Is.EqualTo(DragBeginResult.Started));
            Assert.That((result.HoverSlot, result.HoverValid), Is.EqualTo((1, true)), "valid pad hover");
            Assert.That(scene.Drag.GhostCellCount, Is.Zero, "no suitcase footprint over a pad");
            Assert.That(result.Step.Move.IsAccepted, Is.True);
            Assert.That(scene.Session.MoveCount, Is.EqualTo(1));
            Assert.That(Where(scene, "laptop-1"), Is.EqualTo(ItemLocation.InStaging(1)));
            Assert.That(scene.Staging.ItemViews.ContainsKey("laptop-1") && !scene.Board.ItemViews.ContainsKey("laptop-1"), Is.True);
            var parked = scene.Staging.ItemViews["laptop-1"];
            Assert.That(parked.transform.parent, Is.SameAs(scene.Staging.Pads[1]), "parked on its slot");
            Assert.That(scene.Staging.HoverSlot, Is.EqualTo(-1), "hover cleared after drop");

            Assert.That(scene.Undo(), Is.True);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            Assert.That(Where(scene, "laptop-1"), Is.EqualTo(original), "exact suitcase placement restored");
            Assert.That(scene.Session.MoveCount, Is.Zero);
            Assert.That(scene.Staging.ItemViews, Is.Empty);
            Assert.That(scene.Board.ItemViews.ContainsKey("laptop-1"), Is.True);
        }

        [UnityTest]
        public IEnumerator BlockedItem_SourceTrayItem_AndOccupiedOrFullSlots_AreRejectedWithoutChange()
        {
            yield return Gameplay();
            var scene = Scene();
            scene.LoadLevel(StagingFixture.Load(), "Staging");
            yield return null;

            Assert.That(scene.Drag.BeginDrag("book-1", Grab(scene.Board.ItemViews["book-1"])), Is.EqualTo(DragBeginResult.NotAccessible),
                "AccessQueries: the book is under the sneaker");

            var before = scene.Session.CurrentState.Hash;
            var sweater = scene.Tray.ItemViews["sweater-1"];
            Assert.That(scene.Drag.BeginDrag("sweater-1", Grab(sweater)), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(OverSlot(scene, 0));
            Assert.That((scene.Staging.HoverSlot, scene.Staging.HoverValid), Is.EqualTo((0, false)), "Source Tray -> staging is not offered as valid");
            Assert.That(scene.Drag.Preview.Rejection, Is.EqualTo(MoveRejection.UnsupportedRoute));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.False);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(before));
            Assert.That(scene.Session.MoveCount, Is.Zero);
            Assert.That(scene.Session.UndoDepth, Is.Zero, "rejections leave history untouched");

            Assert.That(Stage(scene, "laptop-1", 0).Step.Move.IsAccepted, Is.True);
            Assert.That(Stage(scene, "sneaker-1", 1).Step.Move.IsAccepted, Is.True);
            Assert.That(scene.Staging.ItemViews.Count, Is.EqualTo(2), "both pads occupied, no overlap");
            var full = scene.Session.CurrentState.Hash;
            var third = Stage(scene, "book-1", 0);
            Assert.That(third.Begin, Is.EqualTo(DragBeginResult.Started), "the book is free once the sneaker left");
            Assert.That(third.HoverValid, Is.False, "occupied pad is not a misleading valid target");
            Assert.That(third.Step.Move.Rejection, Is.EqualTo(MoveRejection.StagingSlotOccupied));
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(full));
            Assert.That(scene.Session.MoveCount, Is.EqualTo(2));
            Assert.That(scene.Board.ItemViews.ContainsKey("book-1"), Is.True, "rejected item returns cleanly");
        }

        [UnityTest]
        public IEnumerator StagedItem_BackIntoTheSuitcase_PreviewMatchesCommit_UndoWalksBack()
        {
            yield return Gameplay();
            var scene = Scene();
            scene.LoadLevel(StagingFixture.Load(), "Staging");
            yield return null;
            var original = Where(scene, "laptop-1");
            Stage(scene, "laptop-1", 0);
            var staged = scene.Session.CurrentState.Hash;

            var origin = scene.Board.Compartments["main"].transform.position;
            var view = scene.Staging.ItemViews["laptop-1"];
            Assert.That(scene.Drag.BeginDrag("laptop-1", Grab(view)), Is.EqualTo(DragBeginResult.Started));
            // First footprint cell under the pointer at board cell (2, 3): laptop 3x4 -> anchor (2, 3).
            scene.Drag.UpdateDrag(origin + new Vector3(2.5f, 0f, -3.5f));
            Assert.That(scene.Drag.CandidateStagingSlot, Is.EqualTo(-1));
            Assert.That(scene.Drag.CandidateAnchor, Is.EqualTo(new Cell(2, 3)));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            var glow = scene.Drag.FootprintPreview.bounds.center;
            var step = scene.Drag.Drop();
            Assert.That(step.Move.IsAccepted, Is.True);
            Assert.That(scene.Session.MoveCount, Is.EqualTo(2));
            var placed = scene.Board.ItemViews["laptop-1"];
            Assert.That(placed.Placement.Anchor, Is.EqualTo(new Cell(2, 3)));
            Assert.That(placed.transform.position.x + 1.5f, Is.EqualTo(glow.x).Within(0.01f), "committed where the glow was");
            Assert.That(placed.transform.position.z - 2f, Is.EqualTo(glow.z).Within(0.01f));

            // Dropping a staged item back on its own pad is a cancel, never a staging -> staging move.
            scene.Undo();
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(staged));
            Assert.That(Where(scene, "laptop-1"), Is.EqualTo(ItemLocation.InStaging(0)), "undo returns it to the same slot");
            scene.Drag.BeginDrag("laptop-1", Grab(scene.Staging.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(OverSlot(scene, 0));
            Assert.That(scene.Drag.CandidateStagingSlot, Is.EqualTo(-1));
            Assert.That(scene.Drag.Drop(), Is.Null, "own pad = cancel");
            Assert.That(scene.Session.MoveCount, Is.EqualTo(1));

            scene.Undo();
            Assert.That(Where(scene, "laptop-1"), Is.EqualTo(original));
            Assert.That(scene.Session.MoveCount, Is.Zero);
        }

        [UnityTest, Explicit("Writes ZT-041 screenshots")]
        public IEnumerator CaptureStagingScreenshots()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            try
            {
                var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "../Builds/zt041-screens"));
                System.IO.Directory.CreateDirectory(folder);
                yield return Gameplay();
                var scene = Scene();
                scene.Hud.RenderThrough(scene.Camera);
                yield return Shot(scene, folder, "00-lv1-no-staging");

                scene.LoadLevel(StagingFixture.Load(), "Staging");
                yield return null;
                var origin = scene.Board.Compartments["main"].transform.position;
                yield return Shot(scene, folder, "01-fixture-two-empty-slots");
                scene.Drag.BeginDrag("laptop-1", Grab(scene.Board.ItemViews["laptop-1"]));
                scene.Drag.UpdateDrag(origin + new Vector3(1.2f, 0f, -8.4f));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(scene, folder, "02-laptop-picked-up");
                scene.Drag.UpdateDrag(OverSlot(scene, 0));
                yield return Shot(scene, folder, "03-hover-staging-slot");
                scene.Drag.Drop();
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Shot(scene, folder, "04-staged-in-slot-1");
                Stage(scene, "sneaker-1", 1);
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Shot(scene, folder, "05-both-slots-occupied");
                scene.Drag.BeginDrag("book-1", Grab(scene.Board.ItemViews["book-1"]));
                scene.Drag.UpdateDrag(OverSlot(scene, 0));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(scene, folder, "06-third-item-full-rejected-hover");
                scene.Drag.Drop();
                scene.Drag.BeginDrag("laptop-1", Grab(scene.Staging.ItemViews["laptop-1"]));
                scene.Drag.UpdateDrag(origin + new Vector3(2.5f, 0f, -3.5f));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(scene, folder, "07-staged-laptop-back-over-suitcase");
                scene.Drag.Drop();
                yield return new WaitForSecondsRealtime(0.35f);
                yield return Shot(scene, folder, "08-laptop-re-placed");
                scene.Undo();
                yield return Shot(scene, folder, "09-undo-back-to-staged");
                scene.Undo();
                scene.Undo();
                yield return Shot(scene, folder, "10-undo-original-suitcase");
                Debug.Log("[zt041-screens] " + folder);
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static IEnumerator Shot(PuzzleGameplayScene scene, string folder, string name)
        {
            var camera = scene.Camera;
            var target = new RenderTexture(1080, 2340, 24);
            camera.targetTexture = target;
            // Gameplay never reframes during a drag; neither does the capture.
            if (!scene.Drag.IsDragging)
                scene.FrameCamera();
            yield return null;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1080, 2340, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1080, 2340), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(target);
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator Completion_WaitsForEmptyStaging()
        {
            yield return Gameplay();
            var scene = Scene();
            scene.LoadLevel(StagingFixture.Load(), "Staging");
            yield return null;
            var origin = scene.Board.Compartments["main"].transform.position;
            Stage(scene, "laptop-1", 0);
            // Sweater 3x3 into the free lower-left area.
            scene.Drag.BeginDrag("sweater-1", Grab(scene.Tray.ItemViews["sweater-1"]));
            scene.Drag.UpdateDrag(origin + new Vector3(0.5f, 0f, -4.5f));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.False, "laptop still staged");
            Assert.That(scene.Hud.CompletionVisible, Is.False);

            scene.Drag.BeginDrag("laptop-1", Grab(scene.Staging.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(origin + new Vector3(2.5f, 0f, -0.5f));
            var last = scene.Drag.Drop();
            Assert.That(last.Move.IsAccepted && last.CompletionReached, Is.True, "staging empty -> complete");
            Assert.That(scene.Staging.ItemViews, Is.Empty);
            Assert.That(scene.Staging.HoverSlot, Is.EqualTo(-1));
            scene.Completion.Advance(2f);
            Assert.That(scene.Hud.CompletionVisible, Is.True);
        }
    }
}
#endif
