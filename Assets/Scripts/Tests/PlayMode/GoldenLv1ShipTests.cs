using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // GOLDEN-LV1-SHIP: the PuzzleGameplay scene asset exactly as it boots (no LoadLevel override, no fixture, no proxy art)
    // must present the Golden Lv1 as Level 1.
    public sealed class GoldenLv1ShipTests
    {
        private const string ScenePath = "Assets/Scenes/PuzzleGameplay.unity";
        private static readonly string Folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
            "../Builds/golden-lv1-ship/captures"));
        private static readonly (string Id, string Prefab)[] Roster =
        {
            ("passport-1", "PF_Item_Passport"), ("sweater-1", "PF_Item_SweaterOpen"), ("towel-1", "PF_Item_Towel"),
            ("shampoo-1", "PF_Item_Shampoo"), ("sunglasses-1", "PF_Item_Sunglasses"), ("travel-pouch-1", "PF_Item_TravelPouch")
        };

        private static IEnumerator Boot()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static PuzzleGameplayScene Scene => UnityEngine.Object.FindFirstObjectByType<PuzzleGameplayScene>();

        private static Vector3 CellCenter(PuzzleGameplayScene scene, int x, int y) =>
            scene.Board.Compartments["main"].transform.position + new Vector3(x + .5f, 0f, -(y + .5f));

        private static PuzzleSessionStep Place(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Tray.ItemViews[id];
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(view)), Is.EqualTo(DragBeginResult.Started), id);
            scene.Drag.UpdateDrag(CellCenter(scene, x + first.X, y + first.Y));
            var step = scene.Drag.Drop();
            Assert.That(step.Move.IsAccepted, Is.True, id);
            return step;
        }

        private static void AssertAuthoredInitialState(PuzzleGameplayScene scene)
        {
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(scene.Level.InitialState.Hash));
            Assert.That(scene.Session.MoveCount, Is.Zero);
            Assert.That(scene.Session.UndoDepth, Is.Zero);
            Assert.That(scene.Tray.ItemViews.Keys, Is.EquivalentTo(new[] { "shampoo-1", "sunglasses-1", "travel-pouch-1" }));
            Assert.That(scene.Board.ItemViews.Keys, Is.EquivalentTo(new[] { "passport-1", "sweater-1", "towel-1" }));
            Assert.That(scene.Board.ItemViews["sweater-1"].Placement.Anchor, Is.EqualTo(new Cell(0, 0)));
            Assert.That(scene.Board.ItemViews["passport-1"].Placement.Anchor, Is.EqualTo(new Cell(3, 0)));
            Assert.That(scene.Board.ItemViews["towel-1"].Placement.Anchor, Is.EqualTo(new Cell(0, 3)));
            Assert.That(scene.Rules.StatusOf("passport-upper"), Is.EqualTo(RuleTagStatus.Satisfied), "passport ✓");
            Assert.That(scene.Rules.IsPending("shampoo-right"), Is.True, "shampoo ○ while it waits in the tray");
            Assert.That(scene.Hud.CompletionVisible, Is.False);
            Assert.That(scene.Drag.InteractionEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator SceneBoots_IntoGoldenLv1_WithProductionPrefabs_ThreeTrayItems_AndBothRules()
        {
            yield return Boot();
            var scene = Scene;
            Assert.That(scene.LevelIndex, Is.Zero);
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"));
            Assert.That(scene.Hud.LevelLabel, Is.EqualTo("Seviye 1"));
            Assert.That(scene.Hud.LevelSubtitle, Is.EqualTo("İlk Yolculuk"));
            Assert.That(scene.Hud.StatusText, Is.EqualTo("3 eşya kaldı"));
            AssertAuthoredInitialState(scene);
            Assert.That(scene.Tray.ItemViews.Keys.Select(id => scene.Tray.CardPosition(id).z).Distinct().Count(), Is.EqualTo(1),
                "three loose items in one row");

            foreach (var (id, prefab) in Roster)
            {
                var view = scene.Board.ItemViews.TryGetValue(id, out var packed) ? packed : scene.Tray.ItemViews[id];
                Assert.That(view.UsesPrefab, Is.True, id + " uses item art");
                Assert.That(view.VisualRoot.childCount, Is.EqualTo(1), id);
                Assert.That(view.VisualRoot.GetChild(0).name, Is.EqualTo(prefab), id + " production prefab, not a proxy");
                Assert.That(view.VisualRoot.GetComponentsInChildren<MeshRenderer>().Single().sharedMaterial.GetTexture("_BaseMap"),
                    Is.Not.Null, id + " textured production material");
            }

            Assert.That(scene.Rules.ObjectiveVisible, Is.True);
            Assert.That(scene.Rules.RuleIds, Is.EqualTo(new[] { "passport-upper", "shampoo-right" }));
            Assert.That(scene.Rules.LabelOf("passport-upper"), Is.EqualTo("Pasaport üst bölgede olmalı"));
            Assert.That(scene.Rules.LabelOf("shampoo-right"), Is.EqualTo("Şampuan sağ bölgede olmalı"));
        }

        [UnityTest]
        public IEnumerator Rules_Undo_AutomaticCompletion_ZipIt_AndRestart_OnTheShippedLevel()
        {
            yield return Boot();
            var scene = Scene;
            scene.Completion.AutoAdvance = false;
            var initial = scene.Session.CurrentState.Hash;

            // Negative: the shampoo outside the right zone is a legal move that breaks its rule (no longer pending).
            Place(scene, "shampoo-1", 1, 3);
            Assert.That(scene.Rules.StatusOf("shampoo-right"), Is.EqualTo(RuleTagStatus.Violated));
            Assert.That(scene.Rules.IsPending("shampoo-right"), Is.False);
            Assert.That(scene.Session.MoveCount, Is.EqualTo(1));
            Assert.That(scene.Undo(), Is.True);
            Assert.That(scene.Session.MoveCount, Is.Zero, "Undo restores MoveCount");
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(initial));
            Assert.That(scene.Rules.IsPending("shampoo-right"), Is.True);

            Assert.That(Place(scene, "travel-pouch-1", 1, 3).CompletionReached, Is.False);
            Assert.That(Place(scene, "sunglasses-1", 3, 6).CompletionReached, Is.False);
            Assert.That(scene.Completion.PlayCount, Is.Zero);
            var last = Place(scene, "shampoo-1", 3, 2);
            Assert.That(last.CompletionReached, Is.True, "automatic completion on the last accepted move");
            Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.True);
            Assert.That(scene.Session.CurrentCompletion.Rules.All(r => r.IsSatisfied), Is.True);
            Assert.That(scene.Rules.StatusOf("shampoo-right"), Is.EqualTo(RuleTagStatus.Satisfied), "shampoo ✓");
            Assert.That(scene.CompletionCount, Is.EqualTo(1), "one completion edge");
            Assert.That(scene.Completion.PlayCount, Is.EqualTo(1), "Zip It starts from the normal completion flow");
            Assert.That(scene.Drag.InteractionEnabled, Is.False);
            Assert.That(scene.Session.MoveCount, Is.EqualTo(3));

            scene.Completion.Advance(5f);
            Assert.That(scene.Board.Container.Lid.localRotation, Is.EqualTo(Quaternion.identity), "suitcase closed");
            Assert.That(scene.Hud.CompletionVisible, Is.True);
            Assert.That(scene.Completion.PlayCount, Is.EqualTo(1), "edge-triggered: no replay");

            scene.Perform(PuzzleHudAction.Restart);
            yield return null;
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"));
            AssertAuthoredInitialState(scene);
            Assert.That(Quaternion.Angle(scene.Board.Container.Lid.localRotation, scene.Board.Container.LidOpenLocalRotation),
                Is.LessThan(.01f), "lid open again");
        }

        // GOLDEN-LV1-SHIP review captures from the booted scene (no fixture): Builds/golden-lv1-ship/captures.
        [UnityTest, Explicit("Writes GOLDEN-LV1-SHIP review captures")]
        public IEnumerator CaptureShippedGoldenLv1()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "Mobile"), true);
            Directory.CreateDirectory(Folder);
            RenderTexture target = null;
            try
            {
                yield return Boot();
                var scene = Scene;
                Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"), "the shipped level, as booted");
                scene.Completion.AutoAdvance = false;
                target = new RenderTexture(1080, 2340, 24);
                scene.Camera.targetTexture = target;
                scene.Hud.RenderThrough(scene.Camera);
                scene.FrameCamera();
                yield return Shot(scene, target, "A-initial");
                scene.Drag.CancelCandidate();
                yield return new WaitForSecondsRealtime(.3f);
                yield return Shot(scene, target, "B-initial-clean");

                foreach (var (id, name) in new[] { ("shampoo-1", "C-shampoo-selected"), ("sunglasses-1", "D-sunglasses-selected"),
                                                   ("travel-pouch-1", "E-travel-pouch-selected") })
                {
                    Assert.That(scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews[id])),
                        Is.EqualTo(DragBeginResult.Started));
                    scene.Drag.Cancel();
                    yield return new WaitForSecondsRealtime(.2f);
                    yield return Shot(scene, target, name);
                    scene.Drag.CancelCandidate();
                }

                Assert.That(scene.Drag.BeginDrag("shampoo-1", GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews["shampoo-1"])),
                    Is.EqualTo(DragBeginResult.Started));
                scene.Drag.UpdateDrag(CellCenter(scene, 3, 2));
                Assert.That(scene.Drag.PreviewValid, Is.True);
                yield return Shot(scene, target, "F-valid-placement-cue");
                scene.Drag.UpdateDrag(CellCenter(scene, 1, 1));
                Assert.That(scene.Drag.HasCandidate && !scene.Drag.PreviewValid, Is.True, "over the prepacked sweater");
                yield return Shot(scene, target, "G-invalid-placement-cue");
                scene.Drag.Cancel();
                scene.Drag.CancelCandidate();

                Place(scene, "travel-pouch-1", 1, 3);
                yield return new WaitForSecondsRealtime(.4f);
                yield return Shot(scene, target, "H-partially-packed");
                Place(scene, "sunglasses-1", 3, 6);
                Assert.That(Place(scene, "shampoo-1", 3, 2).CompletionReached, Is.True);
                yield return Shot(scene, target, "I-final-pre-zip");
                scene.Completion.Advance(5f);
                yield return Shot(scene, target, "J-completion-closed");

                scene.Perform(PuzzleHudAction.Restart);
                yield return Shot(scene, target, "K-9x16", 1080, 1920);
                PuzzleHud.SafeAreaOverride = FixSlice00Tests.AndroidPunchHole;
                yield return Shot(scene, target, "L-android-cutout");
                PuzzleHud.SafeAreaOverride = FixSlice00Tests.IPhoneDynamicIsland;
                yield return Shot(scene, target, "M-iphone-notch");
                Debug.Log("[GOLDEN-LV1-SHIP] Captures: " + Folder);
            }
            finally
            {
                PuzzleHud.SafeAreaOverride = null;
                var scene = Scene;
                if (scene != null) scene.Camera.targetTexture = null;
                if (target != null) UnityEngine.Object.Destroy(target);
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static IEnumerator Shot(PuzzleGameplayScene scene, RenderTexture target, string name,
            int width = 1080, int height = 2340)
        {
            var camera = scene.Camera;
            RenderTexture custom = null;
            if (width != target.width || height != target.height)
            {
                custom = new RenderTexture(width, height, 24);
                camera.targetTexture = custom;
                scene.FrameCamera();
            }
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(image);
            if (custom != null)
            {
                camera.targetTexture = target;
                UnityEngine.Object.Destroy(custom);
                scene.FrameCamera();
            }
        }
    }
}
