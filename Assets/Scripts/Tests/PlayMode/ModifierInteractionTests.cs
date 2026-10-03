using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class ModifierInteractionTests
    {
        private GameObject _root;
        private ModifierFixtureArt _art;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
            _art?.Dispose();
        }

        private PuzzleGameplayScene Scene(PuzzleLevel level)
        {
            _root = new GameObject("Modifier Fixture");
            var scene = _root.AddComponent<PuzzleGameplayScene>();
            _art = new ModifierFixtureArt(scene.MaterialTemplate);
            scene.LoadLevel(level, "Modifiers", _art.Resolve);
            return scene;
        }

        private static PuzzleItem Item(PuzzleGameplayScene scene, string id)
        {
            scene.Session.CurrentState.TryGetItem(id, out var item);
            return item;
        }

        private static Vector3 Grab(PuzzleItemView view) => view.transform.position
            + view.transform.lossyScale.x * new Vector3(0.5f, 0f, -0.5f);

        private static Vector3 At(PuzzleGameplayScene scene, int x, int y) =>
            scene.Board.Compartments["main"].transform.position + new Vector3(x + 0.5f, 0f, -y - 0.5f);

        private static float VisualHeight(PuzzleItemView view)
        {
            var renderers = view.VisualRoot.GetComponentsInChildren<Renderer>();
            var min = float.MaxValue;
            var max = float.MinValue;
            foreach (var renderer in renderers)
            {
                min = Mathf.Min(min, renderer.bounds.min.y);
                max = Mathf.Max(max, renderer.bounds.max.y);
            }
            return max - min;
        }

        private static void Select(PuzzleGameplayScene scene, string id, PuzzleItemView view)
        {
            Assert.That(scene.Drag.BeginDrag(id, Grab(view)), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.Cancel();
        }

        [UnityTest]
        public IEnumerator Fold_CandidatePreviewCommitUndoAndCancel_AreAtomic()
        {
            var scene = Scene(ModifierFixture.Fold());
            yield return null;
            Select(scene, "sweater-1", scene.Tray.ItemViews["sweater-1"]);
            var before = scene.Session.CurrentState;
            Assert.That(scene.Drag.CanSelectModifier(ItemModifier.Fold), Is.True);
            Assert.That(scene.Drag.CanSelectModifier(ItemModifier.Compress), Is.False);
            scene.Perform(PuzzleHudAction.Fold);
            Assert.That(scene.Drag.SelectedCandidateStateId, Is.EqualTo("folded"));
            Assert.That(scene.Tray.ItemViews["sweater-1"].Footprint.CellCount, Is.EqualTo(8));
            Assert.That(scene.Session.CurrentState, Is.SameAs(before));
            Assert.That(scene.Session.MoveCount, Is.Zero);
            Assert.That(scene.Session.UndoDepth, Is.Zero);
            scene.Perform(PuzzleHudAction.Fold);
            Assert.That(scene.Drag.SelectedCandidateStateId, Is.EqualTo("open"), "authored reverse edge cycles home");
            scene.Perform(PuzzleHudAction.Fold);
            Assert.That(scene.Drag.BeginDrag("sweater-1", Grab(scene.Tray.ItemViews["sweater-1"])), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(At(scene, 3, 0));
            Assert.That(scene.Tray.ItemViews["sweater-1"].UsesPrefab, Is.True);
            Assert.That(scene.Tray.ItemViews["sweater-1"].IsGhosted, Is.False, "folded art stays visible over the glow");
            Assert.That(scene.Drag.GhostCellCount, Is.EqualTo(8));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            var previewHash = scene.Drag.Preview.State.Hash;
            Assert.That(scene.Session.CurrentState, Is.SameAs(before));
            var step = scene.Drag.Drop();
            Assert.That(step.Move.IsAccepted, Is.True);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(previewHash));
            Assert.That(Item(scene, "sweater-1").StateId, Is.EqualTo("folded"));
            Assert.That(scene.Session.MoveCount, Is.EqualTo(1));
            Assert.That(scene.Undo(), Is.True);
            Assert.That(Item(scene, "sweater-1").StateId, Is.EqualTo("open"));
            Assert.That(Item(scene, "sweater-1").Location.Kind, Is.EqualTo(ItemLocationKind.SourceTray));
            scene.Perform(PuzzleHudAction.Fold);
            scene.Drag.CancelCandidate();
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(before.Hash));
            Assert.That(scene.Session.UndoDepth, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Compress_KeepsXY_ChangesThickness_AndUndoRestores()
        {
            var scene = Scene(ModifierFixture.Compress());
            yield return null;
            Select(scene, "jacket-1", scene.Tray.ItemViews["jacket-1"]);
            var original = scene.Session.CurrentState;
            Assert.That(scene.Tray.ItemViews["jacket-1"].UsesPrefab, Is.True);
            var normalHeight = VisualHeight(scene.Tray.ItemViews["jacket-1"]);
            Assert.That(scene.Drag.CanSelectModifier(ItemModifier.Fold), Is.False);
            Assert.That(scene.Drag.CanSelectModifier(ItemModifier.Compress), Is.True);
            scene.Drag.BeginDrag("jacket-1", Grab(scene.Tray.ItemViews["jacket-1"]));
            scene.Drag.UpdateDrag(At(scene, 0, 0));
            Assert.That(scene.Drag.PreviewValid, Is.False, "thickness 2 cannot fit a one-layer board");
            scene.Drag.Cancel();
            scene.Perform(PuzzleHudAction.Compress);
            yield return null;
            Assert.That(scene.Hud.FoldVisible, Is.False);
            Assert.That(scene.Hud.CompressVisible, Is.True, "current Compress intent can be cancelled");
            scene.Perform(PuzzleHudAction.Compress);
            Assert.That(scene.Tray.ItemViews["jacket-1"].Thickness, Is.EqualTo(2), "second tap cancels the intent");
            Assert.That(scene.Session.MoveCount, Is.Zero);
            scene.Perform(PuzzleHudAction.Compress);
            Assert.That(scene.Tray.ItemViews["jacket-1"].Footprint.CellCount, Is.EqualTo(4));
            Assert.That(scene.Tray.ItemViews["jacket-1"].Thickness, Is.EqualTo(1));
            Assert.That(VisualHeight(scene.Tray.ItemViews["jacket-1"]), Is.LessThan(normalHeight * 0.5f));
            Assert.That(scene.Session.CurrentState, Is.SameAs(original));
            scene.Drag.BeginDrag("jacket-1", Grab(scene.Tray.ItemViews["jacket-1"]));
            scene.Drag.UpdateDrag(At(scene, 0, 0));
            Assert.That(scene.Tray.ItemViews["jacket-1"].IsGhosted, Is.False, "jacket stays visible over the glow");
            Assert.That(scene.Drag.GhostCellCount, Is.EqualTo(4));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            var hash = scene.Drag.Preview.State.Hash;
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            Assert.That(Item(scene, "jacket-1").State.Thickness, Is.EqualTo(1));
            Assert.That(scene.Session.MoveCount, Is.EqualTo(1));
            Assert.That(scene.Undo(), Is.True);
            Assert.That(Item(scene, "jacket-1").State.Thickness, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator CandidateRotation_ComposesInEitherOrder_AndRapidModifierResetsFeedback()
        {
            var scene = Scene(ModifierFixture.FoldRotation());
            yield return null;
            Select(scene, "fabric-1", scene.Tray.ItemViews["fabric-1"]);
            scene.Perform(PuzzleHudAction.Rotate);
            scene.Perform(PuzzleHudAction.Fold);
            var first = scene.Tray.ItemViews["fabric-1"].Rotation;
            var footprint = scene.Tray.ItemViews["fabric-1"].Footprint;
            scene.Drag.CancelCandidate();
            scene.Tray.SetDisplayRotation("fabric-1", Rotation.Degrees0);
            scene.Drag.SyncPresenters();
            Select(scene, "fabric-1", scene.Tray.ItemViews["fabric-1"]);
            scene.Perform(PuzzleHudAction.Fold);
            scene.Perform(PuzzleHudAction.Rotate);
            Assert.That(scene.Tray.ItemViews["fabric-1"].Rotation, Is.EqualTo(first));
            Assert.That(scene.Tray.ItemViews["fabric-1"].Footprint.OccupiedCells, Is.EquivalentTo(footprint.OccupiedCells));
            for (var i = 0; i < 8; i++)
                scene.Perform(PuzzleHudAction.Fold);
            Assert.That(scene.Tray.ItemViews["fabric-1"].Feedback.IsAnimating, Is.True);
            scene.Drag.BeginDrag("fabric-1", Grab(scene.Tray.ItemViews["fabric-1"]));
            Assert.That(scene.Tray.ItemViews["fabric-1"].Feedback.IsHeld, Is.True);
            scene.Drag.Cancel();
            Assert.That(scene.Tray.ItemViews["fabric-1"].Feedback.Root.localScale, Is.EqualTo(Vector3.one));
            Assert.That(scene.Session.MoveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator StagedFold_KeepsSlotUntilAtomicPlacement_ThenUndoRestoresIt()
        {
            var scene = Scene(ModifierFixture.FoldStaged());
            yield return null;
            Select(scene, "sweater-1", scene.Staging.ItemViews["sweater-1"]);
            var before = scene.Session.CurrentState;
            Assert.That(scene.Drag.CanSelectModifier(ItemModifier.Fold), Is.True);
            scene.Perform(PuzzleHudAction.Fold);
            Assert.That(scene.Staging.ItemViews["sweater-1"].StateId, Is.EqualTo("folded"));
            Assert.That(Item(scene, "sweater-1").Location.StagingSlot, Is.Zero);
            Assert.That(scene.Session.CurrentState, Is.SameAs(before));
            scene.Drag.BeginDrag("sweater-1", Grab(scene.Staging.ItemViews["sweater-1"]));
            scene.Drag.UpdateDrag(At(scene, 3, 0));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            Assert.That(Item(scene, "sweater-1").StateId, Is.EqualTo("folded"));
            Assert.That(scene.Session.MoveCount, Is.EqualTo(1));
            Assert.That(scene.Undo(), Is.True);
            Assert.That(Item(scene, "sweater-1").Location.StagingSlot, Is.Zero);
            Assert.That(Item(scene, "sweater-1").StateId, Is.EqualTo("open"));
        }

        [UnityTest]
        public IEnumerator Nest_CuesOnlyLegalParent_RejectsOthers_AndUndoRestores()
        {
            var scene = Scene(ModifierFixture.Nest());
            yield return null;
            var before = scene.Session.CurrentState;
            var child = scene.Staging.ItemViews["socks-1"];
            Assert.That(scene.Drag.BeginDrag("socks-1", Grab(child)), Is.EqualTo(DragBeginResult.Started));
            Assert.That(child.UsesPrefab, Is.True);
            Assert.That(child.IsGhosted, Is.False, "child art stays visible during Nest");
            Assert.That(scene.Board.ItemViews["shoe-1"].NestTargetCue, Is.True);
            Assert.That(scene.Board.ItemViews["box-1"].NestTargetCue, Is.False);
            scene.Drag.UpdateDrag(At(scene, 3, 0));
            Assert.That(scene.Drag.CandidateNestParent, Is.EqualTo("box-1"));
            Assert.That(scene.Drag.PreviewValid, Is.False);
            Assert.That(scene.Board.ItemViews["box-1"].NestInvalidCue, Is.True);
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.False);
            Assert.That(scene.Session.CurrentState, Is.SameAs(before));
            Assert.That(scene.Session.MoveCount, Is.Zero);
            Assert.That(scene.Session.UndoDepth, Is.Zero);
            scene.Drag.BeginDrag("socks-1", Grab(scene.Staging.ItemViews["socks-1"]));
            scene.Drag.UpdateDrag(At(scene, 0, 0));
            Assert.That(scene.Drag.CandidateNestParent, Is.EqualTo("shoe-1"));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            var hash = scene.Drag.Preview.State.Hash;
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            Assert.That(Item(scene, "socks-1").Location.Kind, Is.EqualTo(ItemLocationKind.Nested));
            Assert.That(scene.Board.ItemViews.ContainsKey("socks-1"), Is.False);
            Assert.That(scene.Board.ItemViews["shoe-1"].ContainsItemsCue, Is.True);
            Assert.That(scene.Board.ItemViews["shoe-1"].ContainedInsetVisible, Is.True);
            Assert.That(scene.Session.MoveCount, Is.EqualTo(1));
            var cue = scene.Board.ItemViews["shoe-1"].ContainedCueWorld;
            Assert.That(scene.Drag.TryPickItem(cue, out var picked), Is.True);
            Assert.That(picked, Is.EqualTo("socks-1"));
            Assert.That(scene.Drag.BeginDrag(picked, cue), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(At(scene, 3, 3));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            Assert.That(Item(scene, "socks-1").Location.Kind, Is.EqualTo(ItemLocationKind.Suitcase));
            Assert.That(scene.Board.ItemViews.ContainsKey("socks-1"), Is.True);
            Assert.That(scene.Board.ItemViews["shoe-1"].ContainsItemsCue, Is.False);
            Assert.That(scene.Board.ItemViews["shoe-1"].ContainedInsetVisible, Is.False);
            Assert.That(scene.Undo(), Is.True, "undo the unnest relocation");
            Assert.That(Item(scene, "socks-1").Location.Kind, Is.EqualTo(ItemLocationKind.Nested));
            Assert.That(scene.Undo(), Is.True);
            Assert.That(Item(scene, "socks-1").Location.Kind, Is.EqualTo(ItemLocationKind.Staging));
            Assert.That(scene.Board.ItemViews["shoe-1"].ContainsItemsCue, Is.False);
        }

        [UnityTest]
        public IEnumerator Nest_FullParentHasNoCueAndRejectsWithoutHistory()
        {
            var scene = Scene(ModifierFixture.Nest(true));
            yield return null;
            var before = scene.Session.CurrentState;
            scene.Drag.BeginDrag("socks-1", Grab(scene.Staging.ItemViews["socks-1"]));
            Assert.That(scene.Board.ItemViews["shoe-1"].NestTargetCue, Is.False);
            scene.Drag.UpdateDrag(At(scene, 0, 0));
            Assert.That(scene.Drag.PreviewValid, Is.False);
            Assert.That(scene.Board.ItemViews["shoe-1"].NestInvalidCue, Is.True);
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.False);
            Assert.That(scene.Session.CurrentState, Is.SameAs(before));
            Assert.That(scene.Session.UndoDepth, Is.Zero);
        }

        [UnityTest]
        public IEnumerator FoldCompletion_BeginsOnlyAfterCommittedPlacement_ReplayResetsCandidate()
        {
            var scene = Scene(ModifierFixture.Fold(true));
            yield return null;
            Select(scene, "sweater-1", scene.Tray.ItemViews["sweater-1"]);
            scene.Perform(PuzzleHudAction.Fold);
            Assert.That(scene.CompletionCount, Is.Zero);
            scene.Drag.BeginDrag("sweater-1", Grab(scene.Tray.ItemViews["sweater-1"]));
            scene.Drag.UpdateDrag(At(scene, 3, 0));
            Assert.That(scene.CompletionCount, Is.Zero);
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            Assert.That(scene.CompletionCount, Is.EqualTo(1));
            Assert.That(scene.Hud.FoldVisible, Is.False);
            scene.Restart();
            Assert.That(scene.Drag.SelectedCandidateStateId, Is.Null);
            Assert.That(scene.Tray.ItemViews["sweater-1"].StateId, Is.EqualTo("open"));
            Select(scene, "sweater-1", scene.Tray.ItemViews["sweater-1"]);
            scene.Perform(PuzzleHudAction.Fold);
            scene.NextLevel();
            Assert.That(scene.Drag.SelectedCandidateStateId, Is.Null);
            Assert.That(scene.Tray.ItemViews["sweater-1"].StateId, Is.EqualTo("open"));
        }

#if UNITY_EDITOR
        [UnityTest, Explicit("Writes ZT-045.1 1080x2340 review frames")]
        public IEnumerator CaptureModifierReadabilityFrames()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.Hud.RenderThrough(scene.Camera);
            var art = new ModifierFixtureArt(scene.MaterialTemplate);
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/zt0451-screens"));
            Directory.CreateDirectory(folder);

            scene.LoadLevel(ModifierFixture.Fold(), "Fold", art.Resolve);
            Select(scene, "sweater-1", scene.Tray.ItemViews["sweater-1"]);
            scene.Perform(PuzzleHudAction.Fold);
            scene.Drag.BeginDrag("sweater-1", Grab(scene.Tray.ItemViews["sweater-1"]));
            scene.Drag.UpdateDrag(At(scene, 3, 0));
            yield return Capture(scene, folder, "01-fold-hover");
            scene.Drag.Drop();
            yield return Capture(scene, folder, "02-fold-placed");

            scene.LoadLevel(ModifierFixture.Compress(), "Compress", art.Resolve);
            Select(scene, "jacket-1", scene.Tray.ItemViews["jacket-1"]);
            yield return Capture(scene, folder, "03-uncompressed");
            scene.Perform(PuzzleHudAction.Compress);
            yield return Capture(scene, folder, "04-compressed-candidate");
            scene.Drag.BeginDrag("jacket-1", Grab(scene.Tray.ItemViews["jacket-1"]));
            scene.Drag.UpdateDrag(At(scene, 0, 0));
            yield return Capture(scene, folder, "05-compressed-hover");
            scene.Drag.Drop();
            yield return Capture(scene, folder, "06-compressed-placed");

            scene.LoadLevel(ModifierFixture.Nest(), "Nest", art.Resolve);
            scene.Drag.BeginDrag("socks-1", Grab(scene.Staging.ItemViews["socks-1"]));
            yield return Capture(scene, folder, "07-compatible-parent-cue");
            var opening = scene.Board.ItemViews["shoe-1"].ContainedCueWorld;
            scene.Drag.UpdateDrag(opening);
            yield return Capture(scene, folder, "08-child-hover-parent");
            scene.Drag.UpdateDrag(At(scene, 3, 0));
            yield return Capture(scene, folder, "09-incompatible-target");
            scene.Drag.UpdateDrag(opening);
            scene.Drag.Drop();
            yield return Capture(scene, folder, "10-nested-result");
            scene.Undo();
            yield return Capture(scene, folder, "11-nest-undo");

            scene.LoadLevel(0);
            yield return Capture(scene, folder, "12-lv1-idle");
            scene.LoadLevel(1);
            Select(scene, "laptop-1", scene.Tray.ItemViews["laptop-1"]);
            scene.Perform(PuzzleHudAction.Rotate);
            yield return Capture(scene, folder, "13-lv2-rotate");
            art.Dispose();
            Debug.Log("[zt0451-screens] " + folder);
        }

        private static IEnumerator Capture(PuzzleGameplayScene scene, string folder, string name)
        {
            const int width = 1080, height = 2340;
            var target = new RenderTexture(width, height, 24);
            scene.Camera.targetTexture = target;
            scene.FrameCamera();
            yield return null;
            scene.Camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            scene.Camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(target);
            Object.Destroy(image);
        }
#endif
    }
}
