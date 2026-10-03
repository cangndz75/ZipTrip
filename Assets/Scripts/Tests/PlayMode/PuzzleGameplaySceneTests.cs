using System.Collections;
using System.IO;
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
    public sealed class PuzzleGameplaySceneTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.Destroy(_root);
        }

        private PuzzleGameplayScene Scene(int level = 0)
        {
            _root = new GameObject("Puzzle Gameplay");
            var scene = _root.AddComponent<PuzzleGameplayScene>();
            scene.LoadLevel(level);
            return scene;
        }

        // Places a Source Tray item at a board anchor through the UI path: select, Rotate button until the requested
        // orientation, drag with the first footprint cell under the pointer, drop.
        private static PuzzleSessionStepProbe Place(PuzzleGameplayScene scene, string id, Rotation rotation, Cell anchor)
        {
            var view = scene.Tray.ItemViews[id];
            scene.Drag.BeginDrag(id, TrayGrab(view));
            scene.Drag.Cancel();
            for (var i = 0; i < 4 && scene.Tray.ItemViews[id].Rotation != rotation; i++)
                scene.Perform(PuzzleHudAction.Rotate);
            view = scene.Tray.ItemViews[id];
            Assert.That(view.Rotation, Is.EqualTo(rotation), id + " reachable through the Rotate button");
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, TrayGrab(view)), Is.EqualTo(DragBeginResult.Started));
            var origin = scene.Board.Compartments["main"].transform.position;
            scene.Drag.UpdateDrag(origin + new Vector3(anchor.X + first.X + 0.5f, 0f, -(anchor.Y + first.Y + 0.5f)));
            var valid = scene.Drag.PreviewValid;
            var step = scene.Drag.Drop();
            return new PuzzleSessionStepProbe(valid, step);
        }

        private static Vector3 TrayGrab(PuzzleItemView view)
        {
            var first = view.Footprint.OccupiedCells[0];
            return view.transform.position + view.transform.lossyScale.x * new Vector3(first.X + 0.5f, 0f, -(first.Y + 0.5f));
        }

        private readonly struct PuzzleSessionStepProbe
        {
            public bool PreviewValid { get; }
            public ZipTrip.Application.PuzzleSessionStep Step { get; }

            public PuzzleSessionStepProbe(bool previewValid, ZipTrip.Application.PuzzleSessionStep step)
            {
                PreviewValid = previewValid;
                Step = step;
            }
        }

        [UnityTest]
        public IEnumerator Bootstrap_LoadsLv1ThroughSchemaV2IntoAFreshSession()
        {
            var scene = Scene();
            yield return null;
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"));
            Assert.That(scene.Session.CurrentState, Is.SameAs(scene.Level.InitialState));
            Assert.That(scene.Session.MoveCount, Is.Zero);
            Assert.That(scene.Tray.ItemViews.Keys, Is.EquivalentTo(new[] { "book-1", "laptop-1", "sneaker-1", "sweater-1" }));
            Assert.That(scene.Board.ItemViews, Is.Empty);
            Assert.That(scene.Hud.LevelLabel, Is.EqualTo("Level 1"));
            Assert.That(scene.Hud.CompletionVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator RotateButton_ChangesTheCandidateNotTheState()
        {
            var scene = Scene(1);
            yield return null;
            var state = scene.Session.CurrentState;
            scene.Drag.BeginDrag("sweater-1", TrayGrab(scene.Tray.ItemViews["sweater-1"]));
            scene.Drag.Cancel();
            yield return null;
            Assert.That(scene.Hud.RotateVisible, Is.False, "a square sweater has nothing to rotate");

            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.Cancel();
            yield return null;
            Assert.That(scene.Hud.RotateVisible, Is.True);
            scene.Perform(PuzzleHudAction.Rotate);
            Assert.That(scene.Tray.ItemViews["laptop-1"].Rotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(scene.Session.CurrentState, Is.SameAs(state));
            Assert.That(scene.Session.MoveCount, Is.Zero);
            Assert.That(scene.Session.UndoDepth, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Lv2_PlaysToCompletionThroughTheUi_ThenNextAndRestartAreClean()
        {
            var scene = Scene(1);
            yield return null;
            var upright = Place(scene, "laptop-1", Rotation.Degrees0, new Cell(0, 0));
            Assert.That(upright.Step.Move.IsAccepted, Is.True, "an upright laptop is placeable, just not solvable");
            Assert.That(scene.Undo(), Is.True);
            Assert.That(scene.Session.MoveCount, Is.Zero);

            Assert.That(Place(scene, "laptop-1", Rotation.Degrees90, new Cell(0, 0)).PreviewValid, Is.True);
            Assert.That(Place(scene, "sweater-1", Rotation.Degrees0, new Cell(0, 3)).Step.Move.IsAccepted, Is.True);
            var last = Place(scene, "sneaker-1", Rotation.Degrees270, new Cell(1, 5));
            Assert.That(last.Step.CompletionReached, Is.True);
            yield return null;
            Assert.That(scene.Session.MoveCount, Is.EqualTo(3));
            Assert.That(scene.Hud.CompletionVisible, Is.True);
            Assert.That(scene.Drag.InteractionEnabled, Is.False);
            Assert.That(scene.Drag.BeginDrag("laptop-1", Vector3.zero), Is.EqualTo(DragBeginResult.InteractionLocked));
            Assert.That(scene.Board.ItemViews.Count, Is.EqualTo(3));

            scene.Perform(PuzzleHudAction.Next);
            yield return null;
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"), "wraps to Lv1");
            Assert.That(scene.Board.ItemViews, Is.Empty, "no leaked board views");
            Assert.That(scene.Tray.ItemViews.Count, Is.EqualTo(4));
            Assert.That(scene.Hud.CompletionVisible, Is.False);
            Assert.That(scene.Drag.InteractionEnabled, Is.True);

            Place(scene, "book-1", Rotation.Degrees0, new Cell(3, 0));
            Assert.That(scene.Session.MoveCount, Is.EqualTo(1));
            scene.Perform(PuzzleHudAction.Restart);
            yield return null;
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(scene.Level.InitialState.Hash));
            Assert.That(scene.Session.MoveCount, Is.Zero);
            Assert.That(scene.Session.UndoDepth, Is.Zero);
            Assert.That(scene.Tray.ItemViews.Count, Is.EqualTo(4));
            Assert.That(scene.Board.ItemViews, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Lv1_PoorPlacementIsRecoverable_AndCompletionFollowsTheSession()
        {
            var scene = Scene(0);
            yield return null;
            // Laptop in the middle columns blocks both side lanes; relocating it is one normal move.
            Assert.That(Place(scene, "laptop-1", Rotation.Degrees0, new Cell(1, 0)).Step.Move.IsAccepted, Is.True);
            var laptop = scene.Board.ItemViews["laptop-1"];
            Assert.That(scene.Drag.BeginDrag("laptop-1", laptop.transform.position + new Vector3(0.5f, 0f, -0.5f)), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position + new Vector3(0.5f, 0f, -0.5f));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
            Assert.That(Place(scene, "sweater-1", Rotation.Degrees0, new Cell(0, 4)).Step.Move.IsAccepted, Is.True);
            Assert.That(Place(scene, "book-1", Rotation.Degrees0, new Cell(3, 4)).Step.Move.IsAccepted, Is.True);
            Assert.That(scene.Hud.CompletionVisible, Is.False);
            var last = Place(scene, "sneaker-1", Rotation.Degrees0, new Cell(3, 0));
            Assert.That(last.Step.CompletionReached, Is.True);
            Assert.That(scene.Hud.CompletionVisible, Is.EqualTo(scene.Session.CurrentCompletion.IsComplete));
            Assert.That(scene.CompletionCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SwitchingLevels_FramesOnlyTheNewLevel()
        {
            var scene = Scene(0);
            yield return null;
            scene.Perform(PuzzleHudAction.Next);
            var frame = scene.Board.CompartmentFrames().Single();
            var center = frame.Origin + new Vector3(frame.Width * 0.5f, 0f, 0f);
            Assert.That(scene.Camera.WorldToViewportPoint(center).x, Is.EqualTo(0.5f).Within(0.02f),
                "Lv2's 4-wide board is centred, not framed with Lv1's leftover 5-wide board");
            yield return null;
            Assert.That(scene.Camera.WorldToViewportPoint(center).x, Is.EqualTo(0.5f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator ScreenSpacePointer_PicksTrayItemsAndPlacesThroughTheRealCamera()
        {
            var scene = Scene(0);
            yield return null;
            var camera = scene.Camera;
            var view = scene.Tray.ItemViews["book-1"];
            var grab = camera.WorldToScreenPoint(TrayGrab(view));
            var target = camera.WorldToScreenPoint(scene.Board.Compartments["main"].transform.position + new Vector3(3.5f, 0f, -0.5f));
            scene.HandlePointer(new PointerSignal(PointerPhase.Down, grab));
            Assert.That(scene.Drag.DraggedInstanceId, Is.EqualTo("book-1"));
            scene.HandlePointer(new PointerSignal(PointerPhase.Move, target));
            Assert.That(scene.Drag.CandidateAnchor, Is.EqualTo(new Cell(3, 0)));
            scene.HandlePointer(new PointerSignal(PointerPhase.Up, target));
            Assert.That(scene.Session.CurrentState.TryGetItem("book-1", out var book) && book.Location.Kind == ItemLocationKind.Suitcase, Is.True);

            var undo = scene.Hud.ButtonCenter(PuzzleHudAction.Undo);
            scene.HandlePointer(new PointerSignal(PointerPhase.Down, undo));
            scene.HandlePointer(new PointerSignal(PointerPhase.Up, undo));
            Assert.That(scene.Session.MoveCount, Is.Zero, "HUD tap reached Undo, not the board");
        }

        // ZT-040B: the grid is a hidden snapping aid. Idle shows no cell guides; a drag reveals only the cells around the
        // snapped footprint; drop / cancel hide them again. The suitcase interior is exactly the playable board.
        [UnityTest]
        public IEnumerator CellGuides_AreHiddenWhenIdle_LocalDuringDrag_AndGoneAfterDropOrCancel()
        {
            var scene = Scene(0);
            yield return null;
            var main = scene.Board.Compartments["main"];
            Assert.That(scene.Board.Shell.Interior, Is.EqualTo(new Rect(0f, -7f, 5f, 7f)), "suitcase lining matches the 5x7 board");
            Assert.That(main.VisibleGuideCount, Is.Zero, "no grid while idle");

            var origin = main.transform.position;
            scene.Drag.BeginDrag("book-1", TrayGrab(scene.Tray.ItemViews["book-1"]));
            scene.Drag.UpdateDrag(origin + new Vector3(0.5f, 0f, -0.5f));
            Assert.That(scene.Drag.CandidateAnchor, Is.EqualTo(new Cell(0, 0)));
            // Book 2x3 at (0,0): guides on the 3x4 neighbourhood inside the board minus the 6 covered cells.
            Assert.That(main.VisibleGuideCount, Is.EqualTo(6));
            Assert.That(main.IsGuideVisible(new Cell(2, 0)) && main.IsGuideVisible(new Cell(0, 3)), Is.True);
            Assert.That(main.IsGuideVisible(new Cell(4, 6)), Is.False, "far cells stay hidden");
            scene.Drag.Drop();
            Assert.That(main.VisibleGuideCount, Is.Zero, "guides disappear after drop");
            Assert.That(scene.Board.ItemViews["book-1"].IsGhosted, Is.False);
            Assert.That(scene.Board.ItemViews["book-1"].ShadowVisible, Is.True, "packed item rests with its contact shadow");

            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(origin + new Vector3(2.5f, 0f, -0.5f));
            Assert.That(main.VisibleGuideCount, Is.GreaterThan(0));
            scene.Drag.Cancel();
            Assert.That(main.VisibleGuideCount, Is.Zero, "guides disappear after cancel");
        }

#if UNITY_EDITOR
        // ZT-040B: Lv1-Lv2 show item art only (golden prefabs or the procedural book), never footprint blocks.
        [UnityTest]
        public IEnumerator FirstPlayableItems_UseItemArt_NotFootprintBlocks()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            for (var level = 0; level < 2; level++)
            {
                foreach (var view in scene.Tray.ItemViews.Values)
                    Assert.That(view.UsesPrefab, Is.True, scene.LevelId + " " + view.InstanceId + " uses item art");
                scene.NextLevel();
            }
        }

        [UnityTest]
        public IEnumerator SceneAsset_IsWiredToTheV2RuntimeWithGoldenArt()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            Assert.That(scene, Is.Not.Null);
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"));
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                Assert.That(root.GetComponentsInChildren<Component>(true), Has.None.Null, root.name + " has no missing scripts");
            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position + new Vector3(0.5f, 0f, -0.5f));
            scene.Drag.Drop();
            Assert.That(scene.Board.ItemViews["laptop-1"].UsesPrefab, Is.True, "golden laptop");
        }

        // Visual smoke check: renders both levels at 1080x1920 and a 20:9 device aspect.
        [UnityTest, Explicit("Writes ZT-040 screenshots")]
        public IEnumerator CaptureFirstPlayableScreenshots()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.Hud.RenderThrough(scene.Camera);
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/zt040-screens"));
            Directory.CreateDirectory(folder);

            yield return Capture(scene, folder, "lv1-1-initial", 1080, 1920);
            yield return Capture(scene, folder, "lv1-1-initial-20x9", 1080, 2400);
            Place(scene, "laptop-1", Rotation.Degrees0, new Cell(0, 0));
            scene.Drag.BeginDrag("sweater-1", TrayGrab(scene.Tray.ItemViews["sweater-1"]));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position + new Vector3(2.5f, 0f, -0.5f));
            yield return Capture(scene, folder, "lv1-2-drag-invalid", 1080, 1920);
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position + new Vector3(0.5f, 0f, -4.5f));
            yield return Capture(scene, folder, "lv1-3-drag-valid", 1080, 1920);
            scene.Drag.Drop();
            Place(scene, "book-1", Rotation.Degrees0, new Cell(3, 4));
            Place(scene, "sneaker-1", Rotation.Degrees0, new Cell(3, 0));
            yield return Capture(scene, folder, "lv1-4-complete", 1080, 1920);

            scene.Perform(PuzzleHudAction.Next);
            yield return Capture(scene, folder, "lv2-1-initial", 1080, 1920);
            Place(scene, "laptop-1", Rotation.Degrees0, new Cell(0, 0));
            yield return Capture(scene, folder, "lv2-2-upright-dead-gap", 1080, 1920);
            scene.Undo();
            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.Cancel();
            scene.Perform(PuzzleHudAction.Rotate);
            yield return Capture(scene, folder, "lv2-3-rotated-in-tray", 1080, 1920);
            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position + new Vector3(0.5f, 0f, -0.5f));
            yield return Capture(scene, folder, "lv2-4-rotation-preview", 1080, 1920);
            scene.Drag.Drop();
            Place(scene, "sweater-1", Rotation.Degrees0, new Cell(0, 3));
            Place(scene, "sneaker-1", Rotation.Degrees270, new Cell(1, 5));
            yield return Capture(scene, folder, "lv2-5-complete", 1080, 1920);
            yield return Capture(scene, folder, "lv2-5-complete-20x9", 1080, 2400);
            Debug.Log("[zt040-screens] " + folder);
        }

        // ZT-040B visual proof: idle, drag over the suitcase, partially packed and complete for Lv1 and Lv2, at the
        // Huawei 1080x2340 portrait resolution (plus a 1080x1920 idle reference).
        [UnityTest, Explicit("Writes ZT-040B screenshots")]
        public IEnumerator CaptureSuitcasePresentationScreenshots()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.Hud.RenderThrough(scene.Camera);
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/zt040b-screens"));
            Directory.CreateDirectory(folder);
            var main = scene.Board.Compartments["main"].transform.position;

            yield return Capture(scene, folder, "lv1-A-idle", 1080, 2340);
            yield return Capture(scene, folder, "lv1-A-idle-1080x1920", 1080, 1920);
            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(main + new Vector3(0.5f, 0f, -0.5f));
            yield return Capture(scene, folder, "lv1-B-drag-valid", 1080, 2340);
            scene.Drag.UpdateDrag(main + new Vector3(3.5f, 0f, -4.5f));
            yield return Capture(scene, folder, "lv1-B-drag-invalid", 1080, 2340);
            scene.Drag.Cancel();
            Place(scene, "laptop-1", Rotation.Degrees0, new Cell(0, 0));
            Place(scene, "book-1", Rotation.Degrees0, new Cell(3, 4));
            yield return Capture(scene, folder, "lv1-C-partial", 1080, 2340);
            Place(scene, "sweater-1", Rotation.Degrees0, new Cell(0, 4));
            Place(scene, "sneaker-1", Rotation.Degrees0, new Cell(3, 0));
            yield return Capture(scene, folder, "lv1-D-complete", 1080, 2340);

            scene.Perform(PuzzleHudAction.Next);
            main = scene.Board.Compartments["main"].transform.position;
            yield return Capture(scene, folder, "lv2-A-idle", 1080, 2340);
            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.Cancel();
            scene.Perform(PuzzleHudAction.Rotate);
            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(main + new Vector3(0.5f, 0f, -0.5f));
            yield return Capture(scene, folder, "lv2-B-drag-rotated", 1080, 2340);
            scene.Drag.Drop();
            yield return Capture(scene, folder, "lv2-C-partial", 1080, 2340);
            Place(scene, "sweater-1", Rotation.Degrees0, new Cell(0, 3));
            Place(scene, "sneaker-1", Rotation.Degrees270, new Cell(1, 5));
            yield return Capture(scene, folder, "lv2-D-complete", 1080, 2340);
            Debug.Log("[zt040b-screens] " + folder);
        }

        private static IEnumerator Capture(PuzzleGameplayScene scene, string folder, string name, int width, int height)
        {
            var camera = scene.Camera;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            scene.FrameCamera();
            yield return null;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(target);
            Object.Destroy(image);
        }
#endif
    }
}
