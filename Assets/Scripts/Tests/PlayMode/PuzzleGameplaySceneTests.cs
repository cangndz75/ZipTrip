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

        // ZT-040B/C: the grid is a hidden snapping aid. Idle shows nothing; a drag shows one soft footprint-shaped glow
        // (no cell tiles, no neighbour guides) exactly over the snapped cells; drop / cancel remove it.
        [UnityTest]
        public IEnumerator DragPreview_IsOneFootprintGlow_WithNoCellGuides_AndGoneAfterDropOrCancel()
        {
            var scene = Scene(0);
            yield return null;
            var main = scene.Board.Compartments["main"];
            Assert.That(scene.Board.Shell.Interior, Is.EqualTo(new Rect(0f, -7f, 5f, 7f)), "fallback lining matches the 5x7 board");
            Assert.That(main.VisibleGuideCount, Is.Zero, "no grid while idle");
            Assert.That(scene.Drag.FootprintPreview == null || !scene.Drag.FootprintPreview.gameObject.activeSelf, Is.True);

            var origin = main.transform.position;
            scene.Drag.BeginDrag("book-1", TrayGrab(scene.Tray.ItemViews["book-1"]));
            scene.Drag.UpdateDrag(origin + new Vector3(0.5f, 0f, -0.5f));
            Assert.That(scene.Drag.CandidateAnchor, Is.EqualTo(new Cell(0, 0)));
            Assert.That(main.VisibleGuideCount, Is.Zero, "no cell guides during drag");
            Assert.That(scene.Drag.GhostCellCount, Is.EqualTo(6), "book 2x3 footprint");
            var glow = scene.Drag.FootprintPreview;
            Assert.That(glow.gameObject.activeSelf, Is.True);
            Assert.That(glow.transform.parent.GetComponentsInChildren<Renderer>().Count(r => r.gameObject.activeSelf), Is.EqualTo(1),
                "one merged shape, not a quad per cell");
            AssertCovers(glow.bounds, origin, new Rect(0f, -3f, 2f, 3f));
            scene.Drag.Drop();
            Assert.That(glow.gameObject.activeSelf, Is.False, "preview disappears after drop");
            Assert.That(main.VisibleGuideCount, Is.Zero);
            Assert.That(scene.Board.ItemViews["book-1"].IsGhosted, Is.False);
            Assert.That(scene.Board.ItemViews["book-1"].ShadowVisible, Is.True, "packed item rests with its contact shadow");

            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(origin + new Vector3(0.5f, 0f, -0.5f));
            Assert.That(scene.Drag.PreviewValid, Is.False, "overlaps the book");
            Assert.That(scene.Drag.OffendingPreview.gameObject.activeSelf, Is.True, "offending region emphasised");
            scene.Drag.Cancel();
            Assert.That(glow.gameObject.activeSelf || scene.Drag.OffendingPreview.gameObject.activeSelf, Is.False,
                "preview disappears after cancel");
        }

        // ZT-040D: drop feedback animates only the Feedback Root; the item root sits at its canonical placement on every
        // frame, the state is untouched, and every channel ends exactly at rest.
        [UnityTest]
        public IEnumerator DropFeedback_NeverMovesTheItemRoot_AndEndsAtRest()
        {
            var scene = Scene(0);
            yield return null;
            Place(scene, "book-1", Rotation.Degrees0, new Cell(3, 4));
            var view = scene.Board.ItemViews["book-1"];
            var canonical = PuzzleBoardLayout.ItemLocalPosition(view.Placement);
            var hash = scene.Session.CurrentState.Hash;
            Assert.That(view.Feedback.IsAnimating, Is.True, "valid drop settles");
            var start = Time.realtimeSinceStartup;
            while (view.Feedback.IsAnimating && Time.realtimeSinceStartup - start < 2f)
            {
                Assert.That(view.transform.localPosition, Is.EqualTo(canonical), "item root stays canonical");
                Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
                yield return null;
            }
            Assert.That(view.Feedback.IsAnimating, Is.False);
            Assert.That(view.Feedback.Root.localScale, Is.EqualTo(Vector3.one));
            Assert.That(view.Feedback.Root.localRotation, Is.EqualTo(Quaternion.identity));

            // Rejected drop (laptop onto the book): the returned tray view wobbles home; state and move count unchanged.
            var moves = scene.Session.MoveCount;
            var tray = scene.Tray.ItemViews["laptop-1"];
            scene.Drag.BeginDrag("laptop-1", TrayGrab(tray));
            Assert.That(scene.Drag.DraggedInstanceId, Is.EqualTo("laptop-1"));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position + new Vector3(3.5f, 0f, -4.5f));
            Assert.That(scene.Drag.PreviewValid, Is.False);
            scene.Drag.Drop();
            Assert.That(scene.Session.MoveCount, Is.EqualTo(moves), "a rejected drop is not a move");
            var returned = scene.Tray.ItemViews["laptop-1"];
            Assert.That(returned.Feedback.IsAnimating, Is.True, "rejection feedback plays at home");
            returned.Feedback.CompleteAll();
            Assert.That(returned.Feedback.Root.localScale, Is.EqualTo(Vector3.one));
        }

        // The glow covers exactly the snapped cells (plus its soft pad): centre and extent match the footprint rectangle.
        private static void AssertCovers(Bounds glow, Vector3 origin, Rect cells)
        {
            Assert.That(glow.center.x - origin.x, Is.EqualTo(cells.center.x).Within(0.01f));
            Assert.That(glow.center.z - origin.z, Is.EqualTo(cells.center.y).Within(0.01f));
            Assert.That(glow.size.x, Is.EqualTo(cells.width).Within(0.4f));
            Assert.That(glow.size.z, Is.EqualTo(cells.height).Within(0.4f));
        }

        // ZT-040B composition guard at the Huawei portrait resolution: the suitcase fills most of the width, loose items
        // lie in one row at a readable size, and nothing is pushed under the HUD bands.
        [UnityTest]
        public IEnumerator Portrait1080x2340_SuitcaseFillsTheWidth_LooseItemsAreOneReadableRow()
        {
            var scene = Scene(0);
            yield return null;
            var camera = scene.Camera;
            var target = new RenderTexture(1080, 2340, 24);
            camera.targetTexture = target;
            try
            {
                scene.FrameCamera();
                var shell = scene.Board.Shell;
                var origin = shell.transform.position;
                var body = shell.Body;
                var left = camera.WorldToViewportPoint(origin + new Vector3(body.xMin, 0f, body.center.y)).x;
                var right = camera.WorldToViewportPoint(origin + new Vector3(body.xMax, 0f, body.center.y)).x;
                Assert.That(right - left, Is.GreaterThanOrEqualTo(0.75f), "suitcase body width on screen");
                Assert.That(left, Is.GreaterThanOrEqualTo(0f));
                Assert.That(right, Is.LessThanOrEqualTo(1f));

                var rows = scene.Tray.ItemViews.Values.Select(v => v.transform.localPosition.z).Distinct().Count();
                Assert.That(rows, Is.EqualTo(1), "Lv1 loose items lie in one row");
                var pixelsPerCell = scene.Tray.Scale * camera.pixelHeight / (2f * camera.orthographicSize);
                Assert.That(pixelsPerCell, Is.GreaterThanOrEqualTo(75f), "loose items are not tiny icons");

                var top = PuzzleHud.TopFraction(camera.pixelWidth, camera.pixelHeight);
                var bottom = PuzzleHud.BottomFraction(camera.pixelWidth, camera.pixelHeight);
                Assert.That(camera.WorldToViewportPoint(origin + new Vector3(body.center.x, 0f, body.yMax)).y,
                    Is.LessThanOrEqualTo(1f - top + 0.001f), "suitcase body stays below the top HUD band");
                foreach (var view in scene.Tray.ItemViews.Values)
                {
                    var depth = view.Footprint.OccupiedCells.Max(c => c.Y) + 1;
                    var lowest = view.transform.position + new Vector3(0f, 0f, -depth * scene.Tray.Scale);
                    Assert.That(camera.WorldToViewportPoint(lowest).y, Is.GreaterThanOrEqualTo(bottom - 0.001f),
                        view.InstanceId + " stays above the controls");
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.Destroy(target);
            }
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

        private static IEnumerator LoadGameplayScene()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        // ZT-040C: normal PuzzleGameplay uses the golden container (not the procedural fallback), seated around both
        // unchanged boards, lid open as authored, loose items resting on the suitcase's surface.
        [UnityTest]
        public IEnumerator SceneAsset_UsesTheGoldenContainer_AroundBothBoards()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            Assert.That(scene.ContainerPrefab, Is.Not.Null);
            Assert.That(scene.Hud.Font.name, Does.StartWith("BricolageGrotesque"), "ZT-040D.1 HUD typography");
            for (var level = 0; level < 2; level++)
            {
                var board = scene.Board;
                Assert.That(board.Container, Is.Not.Null, scene.LevelId + " uses the golden container");
                Assert.That(board.Shell, Is.Null, "procedural suitcase is only a fallback");
                Assert.That(board.Container.LidClosed, Is.False, "lid stands open during play");
                Assert.That(Mathf.DeltaAngle(board.Container.Lid.localEulerAngles.x, PuzzleBoardPresenter.GameplayLidOpenDegrees),
                    Is.EqualTo(0f).Within(0.01f), "ZT-040C.1 gameplay open pose faces the lining to the camera");
                Assert.That(Mathf.DeltaAngle(board.Container.AuthoredLidOpenLocalRotation.eulerAngles.x, -77.08f),
                    Is.EqualTo(0f).Within(0.1f), "authored pose untouched");
                Assert.That(board.Container.Root.GetComponentsInChildren<Collider>(true), Is.Empty);
                var frame = board.CompartmentFrames().Single();
                var interior = board.ContainerInterior;
                Assert.That(interior.xMin <= frame.Origin.x && interior.xMax >= frame.Origin.x + frame.Width
                    && interior.yMin <= frame.Origin.z - frame.Height && interior.yMax >= frame.Origin.z, Is.True,
                    scene.LevelId + " board inside the authored interior");
                Assert.That(scene.Tray.transform.position.y, Is.EqualTo(board.ContainerBottomY + PackingTable.MatThickness).Within(1e-4f),
                    "loose items rest on the felt mat on the suitcase's table");
                Assert.That(scene.Tray.transform.position.z, Is.LessThan(board.ContainerFootprint.yMin), "in front of the suitcase");
                scene.NextLevel();
                yield return null;
            }
        }

        // Preview and commit stay in the same place through the real camera; loose items are picked where drawn.
        [UnityTest]
        public IEnumerator GoldenScene_PreviewAndCommitStayAligned_ThroughScreenSpace()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var camera = scene.Camera;
            var origin = scene.Board.Compartments["main"].transform.position;
            var view = scene.Tray.ItemViews["book-1"];
            scene.HandlePointer(new PointerSignal(PointerPhase.Down, camera.WorldToScreenPoint(TrayGrab(view))));
            Assert.That(scene.Drag.DraggedInstanceId, Is.EqualTo("book-1"), "picked on the lowered surface");
            var target = camera.WorldToScreenPoint(origin + new Vector3(3.5f, 0f, -2.5f));
            scene.HandlePointer(new PointerSignal(PointerPhase.Move, target));
            Assert.That(scene.Drag.CandidateAnchor, Is.EqualTo(new Cell(3, 2)));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            var glow = scene.Drag.FootprintPreview.bounds;
            AssertCovers(glow, origin, new Rect(3f, -5f, 2f, 3f));
            scene.HandlePointer(new PointerSignal(PointerPhase.Up, target));
            var placed = scene.Board.ItemViews["book-1"];
            Assert.That(placed.Placement.Anchor, Is.EqualTo(new Cell(3, 2)));
            Assert.That(placed.transform.position.x - origin.x, Is.EqualTo(glow.center.x - origin.x - 1f).Within(0.01f),
                "committed item where the glow was");
            Assert.That(placed.transform.position.z - origin.z, Is.EqualTo(glow.center.z - origin.z + 1.5f).Within(0.01f));
        }

        // ZT-040C composition guard at the Huawei portrait resolution with the golden suitcase.
        [UnityTest]
        public IEnumerator GoldenPortrait1080x2340_SuitcaseDominates_ItemsReadable_NothingUnderTheHud()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var camera = scene.Camera;
            var target = new RenderTexture(1080, 2340, 24);
            camera.targetTexture = target;
            try
            {
                for (var level = 0; level < 2; level++)
                {
                    scene.FrameCamera();
                    var board = scene.Board;
                    var body = board.ContainerFootprint;
                    var y = board.ContainerBottomY;
                    var left = camera.WorldToViewportPoint(new Vector3(body.xMin, y, body.center.y)).x;
                    var right = camera.WorldToViewportPoint(new Vector3(body.xMax, y, body.center.y)).x;
                    Assert.That(right - left, Is.GreaterThanOrEqualTo(0.7f), scene.LevelId + " suitcase width on screen");
                    Assert.That(left >= 0f && right <= 1f, Is.True, "no horizontal clipping");
                    var top = PuzzleHud.TopFraction(camera.pixelWidth, camera.pixelHeight);
                    var bottom = PuzzleHud.BottomFraction(camera.pixelWidth, camera.pixelHeight);
                    var frame = board.CompartmentFrames().Single();
                    Assert.That(camera.WorldToViewportPoint(frame.Origin + new Vector3(0f, 0f, 0f)).y,
                        Is.LessThanOrEqualTo(1f - top), "back row below the top HUD");
                    Assert.That(camera.WorldToViewportPoint(board.Lid.position).y, Is.LessThan(1f - top),
                        "hinge and the lower lid visible");
                    var pixelsPerCell = scene.Tray.Scale * camera.pixelHeight / (2f * camera.orthographicSize);
                    Assert.That(pixelsPerCell, Is.GreaterThanOrEqualTo(70f), "loose items are not tiny icons");
                    foreach (var view in scene.Tray.ItemViews.Values)
                    {
                        var depth = view.Footprint.OccupiedCells.Max(c => c.Y) + 1;
                        var lowest = view.transform.position + new Vector3(0f, 0f, -depth * scene.Tray.Scale);
                        Assert.That(camera.WorldToViewportPoint(lowest).y, Is.GreaterThanOrEqualTo(bottom - 0.001f),
                            view.InstanceId + " stays above the controls");
                    }
                    scene.NextLevel();
                    yield return null;
                }
            }
            finally
            {
                camera.targetTexture = null;
                Object.Destroy(target);
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
            scene.Drag.BeginDrag("sneaker-1", TrayGrab(scene.Tray.ItemViews["sneaker-1"]));
            scene.Drag.UpdateDrag(main + new Vector3(3.5f, 0f, -0.5f));
            yield return Capture(scene, folder, "lv1-B-drag-sneaker", 1080, 2340);
            scene.Drag.Cancel();
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

        // ZT-040C visual proof at 1080x2340 with the golden suitcase, plus the procedural ZT-040B fallback for comparison.
        [UnityTest, Explicit("Writes ZT-040C screenshots")]
        public IEnumerator CaptureGoldenSuitcaseScreenshots()
        {
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/zt040c-screens"));
            Directory.CreateDirectory(folder);
            var fallback = Scene(0);
            yield return null;
            fallback.Hud.RenderThrough(fallback.Camera);
            yield return Capture(fallback, folder, "00-zt040b-procedural-lv1-idle", 1080, 2340);
            Object.Destroy(_root);
            _root = null;
            yield return null;

            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.Hud.RenderThrough(scene.Camera);
            var main = scene.Board.Compartments["main"].transform.position;

            yield return Capture(scene, folder, "01-lv1-idle", 1080, 2340);
            scene.Drag.BeginDrag("sneaker-1", TrayGrab(scene.Tray.ItemViews["sneaker-1"]));
            scene.Drag.UpdateDrag(main + new Vector3(3.5f, 0f, -0.5f));
            yield return Capture(scene, folder, "02-lv1-drag-sneaker-valid", 1080, 2340);
            scene.Drag.Cancel();
            Place(scene, "laptop-1", Rotation.Degrees0, new Cell(0, 0));
            scene.Drag.BeginDrag("sneaker-1", TrayGrab(scene.Tray.ItemViews["sneaker-1"]));
            scene.Drag.UpdateDrag(main + new Vector3(1.5f, 0f, -0.5f));
            yield return Capture(scene, folder, "02b-lv1-drag-sneaker-invalid", 1080, 2340);
            scene.Drag.Cancel();
            Place(scene, "book-1", Rotation.Degrees0, new Cell(3, 4));
            yield return Capture(scene, folder, "03-lv1-partial", 1080, 2340);
            Place(scene, "sweater-1", Rotation.Degrees0, new Cell(0, 4));
            Place(scene, "sneaker-1", Rotation.Degrees0, new Cell(3, 0));
            yield return Capture(scene, folder, "04-lv1-complete", 1080, 2340);

            scene.Perform(PuzzleHudAction.Next);
            main = scene.Board.Compartments["main"].transform.position;
            yield return Capture(scene, folder, "05-lv2-idle", 1080, 2340);
            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.Cancel();
            scene.Perform(PuzzleHudAction.Rotate);
            scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
            scene.Drag.UpdateDrag(main + new Vector3(0.5f, 0f, -0.5f));
            yield return Capture(scene, folder, "06-lv2-drag-rotated-laptop", 1080, 2340);
            scene.Drag.Drop();
            Place(scene, "sweater-1", Rotation.Degrees0, new Cell(0, 3));
            yield return Capture(scene, folder, "06b-lv2-partial", 1080, 2340);

            // Close view of the rim with a packed item: same fixed pitch, narrower orthographic window on the front-left.
            var camera = scene.Camera;
            yield return Capture(scene, folder, "07-rim-closeup", 1080, 2340, () =>
            {
                camera.orthographicSize *= 0.38f;
                camera.transform.position += new Vector3(-1.2f, 0f, 0f) + camera.transform.up * -2.2f;
            });
            Place(scene, "sneaker-1", Rotation.Degrees270, new Cell(1, 5));
            yield return Capture(scene, folder, "07b-lv2-complete", 1080, 2340);

            // Lid contract sanity: identity closes the suitcase, base unmoved; then reopen.
            scene.Board.Container.SetLidClosed(true);
            yield return Capture(scene, folder, "08-lid-identity-closed", 1080, 2340);
            scene.Board.Container.SetLidClosed(false);
            Debug.Log("[zt040c-screens] " + folder);
        }

        // ZT-040D visual proof at 1080x2340, rendered with the Mobile quality level (the Android pipeline asset).
        [UnityTest, Explicit("Writes ZT-040D screenshots")]
        public IEnumerator CaptureFirstPlayablePolishScreenshots()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            try
            {
                var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/zt040d-screens"));
                Directory.CreateDirectory(folder);
                yield return LoadGameplayScene();
                var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
                scene.Hud.RenderThrough(scene.Camera);
                var main = scene.Board.Compartments["main"].transform.position;

                yield return Capture(scene, folder, "01-lv1-idle", 1080, 2340);
                scene.Drag.BeginDrag("sneaker-1", TrayGrab(scene.Tray.ItemViews["sneaker-1"]));
                scene.Drag.UpdateDrag(main + new Vector3(2.2f, 0f, -9.2f));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(scene, folder, "02-lv1-picked-up", 1080, 2340);
                scene.Drag.UpdateDrag(main + new Vector3(3.5f, 0f, -0.5f));
                yield return Capture(scene, folder, "03-lv1-valid-hover", 1080, 2340);
                scene.Drag.Cancel();
                Place(scene, "laptop-1", Rotation.Degrees0, new Cell(0, 0));
                scene.Drag.BeginDrag("sneaker-1", TrayGrab(scene.Tray.ItemViews["sneaker-1"]));
                scene.Drag.UpdateDrag(main + new Vector3(1.5f, 0f, -0.5f));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(scene, folder, "04-lv1-invalid-hover", 1080, 2340);
                scene.Drag.Cancel();
                Place(scene, "book-1", Rotation.Degrees0, new Cell(3, 4));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture(scene, folder, "05-lv1-partial", 1080, 2340);
                Place(scene, "sweater-1", Rotation.Degrees0, new Cell(0, 4));
                Place(scene, "sneaker-1", Rotation.Degrees0, new Cell(3, 0));
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Capture(scene, folder, "06-lv1-complete", 1080, 2340);

                scene.Perform(PuzzleHudAction.Next);
                main = scene.Board.Compartments["main"].transform.position;
                yield return Capture(scene, folder, "07-lv2-idle", 1080, 2340);
                scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
                scene.Drag.Cancel();
                scene.Perform(PuzzleHudAction.Rotate);
                scene.Drag.BeginDrag("laptop-1", TrayGrab(scene.Tray.ItemViews["laptop-1"]));
                scene.Drag.UpdateDrag(main + new Vector3(0.5f, 0f, -0.5f));
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Capture(scene, folder, "08-lv2-rotated-laptop-drag", 1080, 2340);
                scene.Drag.Cancel();
                Debug.Log("[zt040d-screens] " + folder);
            }
            finally
            {
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static IEnumerator Capture(PuzzleGameplayScene scene, string folder, string name, int width, int height,
            System.Action adjustCamera = null)
        {
            var camera = scene.Camera;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            scene.FrameCamera();
            adjustCamera?.Invoke();
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
