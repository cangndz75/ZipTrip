using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ZipTrip.Domain;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // UI-SLICE-01: production Golden Lv1 UI (header, live objective note / compact chip, compact Source Tray, bottom dock).
    // Presentation observes gameplay state; nothing here may change it.
    public sealed class UiSlice01Tests
    {
        private GoldenLv1LayoutReview.GoldenProxyArt _art;
        private RenderTexture _target;

        [TearDown]
        public void TearDown()
        {
            PuzzleHud.SafeAreaOverride = null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            if (scene != null && scene.Camera != null)
                scene.Camera.targetTexture = null;
            _art?.Dispose();
            _art = null;
            if (_target != null)
                Object.Destroy(_target);
        }

        // The real scene with the Golden Lv1 candidate, rendered through the camera at a phone resolution so UI and world
        // share one screen space.
        private IEnumerator Golden(int width = 1080, int height = 2340)
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            _art = new GoldenLv1LayoutReview.GoldenProxyArt();
            scene.LoadLevel(GoldenLv1LayoutReview.LoadCandidate(), PuzzleRuleText.LevelTitle(1), _art.Resolve);
            scene.Completion.AutoAdvance = false;
            _target = new RenderTexture(width, height, 24);
            scene.Camera.targetTexture = _target;
            scene.Hud.RenderThrough(scene.Camera);
            scene.FrameCamera();
            yield return null;
            yield return null;
        }

        private static PuzzleGameplayScene Scene => Object.FindFirstObjectByType<PuzzleGameplayScene>();

        private static Rect ScreenRect(Camera camera, RectTransform rect)
        {
            var c = new Vector3[4];
            rect.GetWorldCorners(c);
            var a = camera.WorldToScreenPoint(c[0]);
            var b = camera.WorldToScreenPoint(c[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static Vector2 Center(Camera camera, RectTransform rect) => ScreenRect(camera, rect).center;

        private static void Tap(PuzzleGameplayScene scene, Vector2 screen)
        {
            scene.HandlePointer(new PointerSignal(PointerPhase.Down, screen));
            scene.HandlePointer(new PointerSignal(PointerPhase.Up, screen));
        }

        private static void Place(PuzzleGameplayScene scene, string id, int x, int y, Rotation rotation = Rotation.Degrees0)
        {
            var view = scene.Tray.ItemViews[id];
            scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(view));
            scene.Drag.Cancel();
            for (var i = 0; i < 4 && scene.Tray.ItemViews[id].Rotation != rotation; i++)
                scene.Perform(PuzzleHudAction.Rotate);
            view = scene.Tray.ItemViews[id];
            Assert.That(view.Rotation, Is.EqualTo(rotation), id + " reachable through Döndür");
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(view)), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                + new Vector3(x + first.X + 0.5f, 0f, -(y + first.Y + 0.5f)));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id);
        }

        // Moves a suitcase item (e.g. the pre-packed passport) by dragging it from its first cell.
        private static void Move(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Board.ItemViews[id];
            var origin = scene.Board.Compartments["main"].transform.position;
            var a = view.Placement.Anchor;
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, origin + new Vector3(a.X + first.X + 0.5f, 0f, -(a.Y + first.Y + 0.5f))),
                Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(origin + new Vector3(x + first.X + 0.5f, 0f, -(y + first.Y + 0.5f)));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id);
        }

        private static bool Shows(RectTransform note, string rule, string part) =>
            note.Find("Status " + rule + "/" + part).gameObject.activeSelf;

        // ------------------------------------------------------------------ header / safe area

        [UnityTest]
        public IEnumerator Header_ShowsLevelAndSubtitle_StaysInsideTheSafeArea_AndNeverMovesTheCamera()
        {
            yield return Golden();
            var scene = Scene;
            var camera = scene.Camera;
            Assert.That(scene.Hud.LevelLabel, Is.EqualTo("Seviye 1"));
            Assert.That(scene.Hud.LevelSubtitle, Is.EqualTo("İlk Yolculuk"));
            var position = camera.transform.position;
            var size = camera.orthographicSize;
            foreach (var profile in new[] { FixSlice00Tests.AndroidPunchHole, FixSlice00Tests.IPhoneDynamicIsland, FixSlice00Tests.FullScreen })
            {
                PuzzleHud.SafeAreaOverride = profile;
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                var header = ScreenRect(camera, scene.Hud.Header);
                Assert.That(header.yMax, Is.LessThanOrEqualTo(profile.yMax * camera.pixelHeight + 0.5f), "header below the cutout");
                var dock = ScreenRect(camera, scene.Hud.Dock);
                Assert.That(dock.yMin, Is.GreaterThanOrEqualTo(profile.yMin * camera.pixelHeight - 0.5f), "dock above the home indicator");
                scene.FrameCamera();
                Assert.That(camera.transform.position, Is.EqualTo(position), "UI adapts, framing does not");
                Assert.That(camera.orthographicSize, Is.EqualTo(size));
            }
        }

        // ------------------------------------------------------------------ objective note

        [UnityTest]
        public IEnumerator ObjectiveNote_FollowsTheDomain_PassportValidInvalidValid_ShampooInactiveInvalidValid()
        {
            yield return Golden();
            var scene = Scene;
            var note = scene.Rules.Note;
            Assert.That(scene.Rules.LabelOf("passport-upper"), Is.EqualTo("Pasaport üst bölgede olmalı"));
            Assert.That(scene.Rules.LabelOf("shampoo-right"), Is.EqualTo("Şampuan sağ bölgede olmalı"));
            Assert.That(scene.Rules.StatusOf("passport-upper"), Is.EqualTo(RuleTagStatus.Satisfied));
            Assert.That(Shows(note, "passport-upper", "Check"), Is.True);
            Assert.That(scene.Rules.StatusOf("shampoo-right"), Is.EqualTo(RuleTagStatus.Violated), "Domain: not in the zone yet");
            Assert.That(scene.Rules.IsPending("shampoo-right"), Is.True, "only because it is still in the Source Tray");
            Assert.That(Shows(note, "shampoo-right", "Ring") && !Shows(note, "shampoo-right", "Bang"), Is.True, "open to-do ring");

            Move(scene, "passport-1", 4, 5);
            Assert.That(scene.Rules.StatusOf("passport-upper"), Is.EqualTo(RuleTagStatus.Violated), "moved out of the upper zone");
            Assert.That(Shows(note, "passport-upper", "Bang") && !Shows(note, "passport-upper", "Check"), Is.True);
            Assert.That(scene.Undo(), Is.True);
            Assert.That(scene.Rules.StatusOf("passport-upper"), Is.EqualTo(RuleTagStatus.Satisfied), "undo restores it");

            Place(scene, "shampoo-1", 1, 3);
            Assert.That(scene.Rules.StatusOf("shampoo-right"), Is.EqualTo(RuleTagStatus.Violated));
            Assert.That(Shows(note, "shampoo-right", "Bang"), Is.True);
            Move(scene, "shampoo-1", 4, 2);
            Assert.That(scene.Rules.StatusOf("shampoo-right"), Is.EqualTo(RuleTagStatus.Satisfied));
            Assert.That(Shows(note, "shampoo-right", "Check"), Is.True);
            foreach (var result in scene.Session.CurrentCompletion.Rules)
                Assert.That(scene.Rules.StatusOf(result.RuleId) == RuleTagStatus.Satisfied, Is.EqualTo(result.IsSatisfied), result.RuleId);
        }

        [UnityTest]
        public IEnumerator Objective_IsTheFullNoteWhenItFits_TheChipOnANotchedPhone_AndTapsNeverTouchState()
        {
            yield return Golden();
            var scene = Scene;
            var camera = scene.Camera;
            Assert.That(scene.Rules.Compact, Is.False, "default 1080x2340: the full note fits");
            Assert.That(scene.Rules.Note.gameObject.activeSelf && !scene.Rules.Chip.gameObject.activeSelf, Is.True);
            Canvas.ForceUpdateCanvases();
            var bed = camera.pixelHeight - camera.WorldToScreenPoint(scene.Board.CompartmentFrames()[0].Origin).y;
            Assert.That(camera.pixelHeight - ScreenRect(camera, scene.Rules.Note).yMin, Is.LessThan(bed), "note clear of the bed");

            PuzzleHud.SafeAreaOverride = FixSlice00Tests.IPhoneDynamicIsland;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(scene.Rules.Compact, Is.True, "not enough room under the notch: chip, not a shrunken note");
            Assert.That(scene.Rules.Chip.gameObject.activeSelf && !scene.Rules.Note.gameObject.activeSelf, Is.True);
            Assert.That(camera.pixelHeight - ScreenRect(camera, scene.Rules.Chip).yMin, Is.LessThan(bed), "chip clear of the bed");
            Assert.That(scene.Rules.Chip.GetComponentsInChildren<Text>().All(t => t.fontSize >= PuzzleRulesPresenter.RuleFontSize), Is.True);

            var hash = scene.Session.CurrentState.Hash;
            var moves = scene.Session.MoveCount;
            var undo = scene.Session.UndoDepth;
            Tap(scene, Center(camera, scene.Rules.Chip));
            Assert.That(scene.Rules.Expanded && scene.Rules.Note.gameObject.activeSelf, Is.True, "tap expands");
            Assert.That(scene.Drag.IsDragging, Is.False);
            Tap(scene, Center(camera, scene.Rules.Note));
            Assert.That(scene.Rules.Expanded, Is.False, "second tap collapses");
            Tap(scene, Center(camera, scene.Rules.Chip));
            var grab = camera.WorldToScreenPoint(GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews["shampoo-1"]));
            scene.HandlePointer(new PointerSignal(PointerPhase.Down, grab));
            Assert.That(scene.Rules.Expanded, Is.False, "pressing elsewhere collapses");
            Assert.That(scene.Drag.DraggedInstanceId, Is.EqualTo("shampoo-1"), "and the press still reaches gameplay");
            scene.Drag.Cancel();
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            Assert.That(scene.Session.MoveCount, Is.EqualTo(moves));
            Assert.That(scene.Session.UndoDepth, Is.EqualTo(undo));

            PuzzleHud.SafeAreaOverride = FixSlice00Tests.FullScreen;
            yield return null;
            yield return null;
            Assert.That(scene.Rules.Compact, Is.False, "follows safe-area changes back");
        }

        // ------------------------------------------------------------------ source tray

        [UnityTest]
        public IEnumerator SourceTray_ReflowsThreeTwoOneZero_UndoAndRestartRestoreIt_AndItemsStayDraggable()
        {
            yield return Golden();
            var scene = Scene;
            var table = scene.Table;
            Assert.That(table.TrayCardCount, Is.EqualTo(3));
            var full = table.TrayShellRect;
            var initialHash = scene.Level.InitialState.Hash;

            Place(scene, "sunglasses-1", 4, 0, Rotation.Degrees90);
            yield return null;
            Assert.That(table.TrayCardCount, Is.EqualTo(2));
            Assert.That(table.TrayShellRect.width, Is.LessThan(full.width), "tray contracts");
            Assert.That(table.TrayShellRect.center.x, Is.EqualTo(full.center.x).Within(0.05f), "and stays centred");
            var two = table.TrayShellRect;
            Place(scene, "travel-pouch-1", 1, 3);
            yield return null;
            Assert.That(table.TrayCardCount, Is.EqualTo(1));
            Assert.That(table.TrayShellRect.width, Is.LessThan(two.width));
            var one = table.TrayShellRect;
            Place(scene, "shampoo-1", 1, 6, Rotation.Degrees90);
            yield return null;
            Assert.That(table.TrayShellVisible, Is.False, "no empty tray panel");
            Assert.That(table.TrayCardCount, Is.Zero);
            Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.False, "shampoo outside the right zone");
            Assert.That(scene.Hud.StatusText, Is.Null, "nothing left: no status");

            Assert.That(scene.Undo(), Is.True);
            yield return null;
            Assert.That(table.TrayCardCount, Is.EqualTo(1), "undo brings the shampoo card back");
            Assert.That(table.TrayShellVisible, Is.True);
            Assert.That(table.TrayShellRect.center.x, Is.EqualTo(one.center.x).Within(0.05f));
            scene.Perform(PuzzleHudAction.Restart);
            yield return null;
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(initialHash));
            Assert.That(table.TrayCardCount, Is.EqualTo(3));
            Assert.That(table.TrayShellRect.center.x, Is.EqualTo(full.center.x).Within(0.01f));
            Assert.That(table.TrayShellRect.width, Is.EqualTo(full.width).Within(0.01f), "restart restores the tray");

            // The tray shell is world decoration with no input surface; a press on a loose item still picks it.
            Assert.That(table.GetComponentsInChildren<Collider>(true), Is.Empty);
            var camera = scene.Camera;
            scene.HandlePointer(new PointerSignal(PointerPhase.Down,
                camera.WorldToScreenPoint(GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews["travel-pouch-1"]))));
            Assert.That(scene.Drag.DraggedInstanceId, Is.EqualTo("travel-pouch-1"));
            scene.Drag.Cancel();
        }

        [UnityTest]
        public IEnumerator ShippedLevels_TrayShellStaysWithinPhoneWidth()
        {
            yield return Golden();
            var scene = Scene;
            foreach (var level in new[] { 0, 1 })
            {
                scene.LoadLevel(level);
                scene.FrameCamera();
                yield return null;
                var shell = scene.Table.TrayShellRect;
                Assert.That(scene.Table.TrayShellVisible, Is.True, "shipped level " + (level + 1));
                var left = scene.Camera.WorldToScreenPoint(new Vector3(shell.xMin, 0f, shell.center.y)).x;
                var right = scene.Camera.WorldToScreenPoint(new Vector3(shell.xMax, 0f, shell.center.y)).x;
                Assert.That(left, Is.GreaterThanOrEqualTo(0f), "left shell edge, shipped level " + (level + 1));
                Assert.That(right, Is.LessThanOrEqualTo(scene.Camera.pixelWidth), "right shell edge, shipped level " + (level + 1));
            }
        }

        // ------------------------------------------------------------------ dock / actions

        [UnityTest]
        public IEnumerator Dock_UndoRestartRotateUseTheExistingCommands_StatusFollowsTheTray_AndNoManualCompletionExists()
        {
            yield return Golden();
            var scene = Scene;
            var camera = scene.Camera;
            Assert.That(scene.Hud.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget), Is.Empty,
                "no HUD graphic can intercept a raycast");
            Assert.That(scene.Hud.GetComponentsInChildren<Transform>(true).Select(t => t.name)
                .Where(n => n.StartsWith("Button ")).Distinct().OrderBy(n => n),
                Is.EqualTo(new[] { "Button Baştan", "Button Döndür", "Button Geri Al", "Button Katla", "Button Sonraki", "Button Sıkıştır", "Button Tekrar" }
                    .OrderBy(n => n)), "no Kontrol Et / check / submit action");
            Assert.That(scene.Hud.StatusText, Is.EqualTo("3 eşya kaldı"));
            Assert.That(scene.Hud.RotateVisible, Is.False, "rotate is contextual");
            Assert.That(scene.Hud.UndoEnabled, Is.False);

            scene.Drag.BeginDrag("travel-pouch-1", GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews["travel-pouch-1"]));
            scene.Drag.Cancel();
            yield return null;
            Assert.That(scene.Hud.RotateVisible, Is.True);
            Assert.That(scene.Hud.StatusText, Is.Null, "the slot shows Döndür instead");
            var hash = scene.Session.CurrentState.Hash;
            var before = scene.Tray.DisplayRotation(scene.Session.CurrentState.Items.Single(i => i.InstanceId == "travel-pouch-1"));
            scene.Perform(PuzzleHudAction.Rotate);
            Assert.That(scene.Tray.DisplayRotation(scene.Session.CurrentState.Items.Single(i => i.InstanceId == "travel-pouch-1")),
                Is.Not.EqualTo(before), "existing Rotate: display orientation only");
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            scene.Drag.CancelCandidate();

            Place(scene, "sunglasses-1", 3, 6);
            yield return null;
            Assert.That(scene.Hud.StatusText, Is.EqualTo("2 eşya kaldı"));
            Assert.That(scene.Hud.UndoEnabled, Is.True);
            var moves = scene.Session.MoveCount;
            scene.Perform(PuzzleHudAction.Undo);
            Assert.That(scene.Session.MoveCount, Is.EqualTo(moves - 1), "Geri Al is the existing undo");
            yield return null;
            Assert.That(scene.Hud.StatusText, Is.EqualTo("3 eşya kaldı"));

            Place(scene, "travel-pouch-1", 1, 3);
            Place(scene, "sunglasses-1", 3, 6);
            Place(scene, "shampoo-1", 3, 2);
            Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.True, "completion is automatic on the last drop");
            Assert.That(scene.Hud.CompletionVisible, Is.False, "Sonraki waits for Zip It");
            var level = scene.Level;
            scene.Perform(PuzzleHudAction.Next);
            Assert.That(scene.Level, Is.SameAs(level), "Next is gated by the Zip It ritual");
            scene.Completion.Advance(5f);
            Assert.That(scene.Hud.CompletionVisible, Is.True);
            scene.Perform(PuzzleHudAction.Restart);
            Assert.That(scene.Session.MoveCount, Is.Zero, "Tekrar replays");
            yield return null;
            Assert.That(scene.Table.TrayCardCount, Is.EqualTo(3));
        }

        // ------------------------------------------------------------------ visual proof

        [UnityTest, Explicit("Writes UI-SLICE-01 captures")]
        public IEnumerator CaptureUiSlice01()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/ui-slice-01"));
            Directory.CreateDirectory(folder);
            try
            {
                yield return Golden();
                var scene = Scene;
                Debug.Log($"[ui-slice-01.1] canvases={Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length} "
                    + $"sceneObjects={scene.GetComponentsInChildren<Transform>(true).Length} "
                    + $"sceneMaterials={scene.GetComponentsInChildren<Renderer>(true).Select(r => r.sharedMaterial).Where(m => m != null).Distinct().Count()} "
                    + $"uiImages={scene.GetComponentsInChildren<Image>(true).Length}");
                yield return Shot(scene, folder, "01-initial");
                Place(scene, "shampoo-1", 1, 3);
                yield return Shot(scene, folder, "02-rule1-valid-rule2-invalid");
                Move(scene, "passport-1", 4, 5);
                yield return Shot(scene, folder, "03-passport-invalid");
                scene.Perform(PuzzleHudAction.Restart);
                Select(scene, "shampoo-1");
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(scene, folder, "04-one-item-selected");
                scene.Drag.CancelCandidate();
                Select(scene, "travel-pouch-1");
                scene.Perform(PuzzleHudAction.Rotate);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot(scene, folder, "05-rotate-visible");
                scene.Perform(PuzzleHudAction.Rotate);
                scene.Drag.CancelCandidate();
                Place(scene, "sunglasses-1", 4, 0, Rotation.Degrees90);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "06-two-items-in-tray");
                Place(scene, "travel-pouch-1", 1, 3);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "07-one-item-in-tray");
                Place(scene, "shampoo-1", 1, 6, Rotation.Degrees90);
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "08-tray-empty-pre-completion");
                scene.Undo();
                Place(scene, "shampoo-1", 3, 2);
                Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.True);
                scene.Completion.Advance(5f);
                yield return Shot(scene, folder, "09-zip-it-completed");
                scene.Perform(PuzzleHudAction.Restart);
                yield return null;
                yield return Shot(scene, folder, "10-9x16", 1080, 1920);
                PuzzleHud.SafeAreaOverride = FixSlice00Tests.AndroidPunchHole;
                yield return Shot(scene, folder, "11-android-cutout");
                PuzzleHud.SafeAreaOverride = FixSlice00Tests.IPhoneDynamicIsland;
                yield return Shot(scene, folder, "12-iphone-notch-chip");
                Tap(scene, Center(scene.Camera, scene.Rules.Chip));
                yield return Shot(scene, folder, "12b-iphone-chip-expanded");
                Tap(scene, Center(scene.Camera, scene.Rules.Note));
                // Style-frame state (Android profile): sunglasses packed at the right, pouch selected.
                PuzzleHud.SafeAreaOverride = FixSlice00Tests.AndroidPunchHole;
                Place(scene, "sunglasses-1", 3, 6);
                Select(scene, "travel-pouch-1");
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot(scene, folder, "13-style-frame-state");
                // The same production UI on the shipped Lv1 (no rules, no subtitle, larger loose items).
                PuzzleHud.SafeAreaOverride = null;
                scene.LoadLevel(0);
                scene.FrameCamera();
                yield return Shot(scene, folder, "15-shipped-lv1");
                scene.LoadLevel(1);
                scene.FrameCamera();
                yield return Shot(scene, folder, "16-shipped-lv2");
                Debug.Log("[ui-slice-01] " + folder);
            }
            finally
            {
                PuzzleHud.SafeAreaOverride = null;
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static void Select(PuzzleGameplayScene scene, string id)
        {
            scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews[id]));
            scene.Drag.Cancel();
        }

        private IEnumerator Shot(PuzzleGameplayScene scene, string folder, string name, int width = 1080, int height = 2340)
        {
            var camera = scene.Camera;
            // Like the game, the camera is framed only when the resolution changes (never per state).
            if (_target.width != width || _target.height != height)
            {
                camera.targetTexture = null;
                Object.Destroy(_target);
                _target = new RenderTexture(width, height, 24);
                camera.targetTexture = _target;
                scene.FrameCamera();
            }
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = _target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(image);
            if (width != 1080 || height != 2340)
            {
                camera.targetTexture = null;
                Object.Destroy(_target);
                _target = new RenderTexture(1080, 2340, 24);
                camera.targetTexture = _target;
                scene.FrameCamera();
            }
        }
    }
}
