using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // FIX-SLICE-00: safe-area HUD, real-art drag presentation and Zip It lid occlusion. Presentation only.
    public sealed class FixSlice00Tests
    {
        // GOLDEN-LV1-SHIP: the shipped Lv1 Source Tray items (sweater, passport and towel start prepacked).
        private static readonly string[] Lv1Items = { "shampoo-1", "sunglasses-1", "travel-pouch-1" };
        // Representative synthetic profiles (normalized, portrait). Not device-exact; Device Simulator is editor-UI only.
        public static readonly Rect AndroidPunchHole = Rect.MinMaxRect(0f, 0f, 1f, 1f - 110f / 2340f);
        public static readonly Rect IPhoneDynamicIsland = Rect.MinMaxRect(0f, 102f / 2556f, 1f, 1f - 177f / 2556f);
        public static readonly Rect FullScreen = new Rect(0f, 0f, 1f, 1f);

        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            PuzzleHud.SafeAreaOverride = null;
            if (_root != null)
                Object.Destroy(_root);
        }

        private PuzzleGameplayScene Scene()
        {
            _root = new GameObject("Puzzle Gameplay");
            var scene = _root.AddComponent<PuzzleGameplayScene>();
            scene.LoadLevel(0);
            return scene;
        }

        private static IEnumerator LoadGameplayScene()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static Vector3 TrayGrab(PuzzleItemView view)
        {
            var first = view.Footprint.OccupiedCells[0];
            return view.transform.position + view.transform.lossyScale.x * new Vector3(first.X + 0.5f, 0f, -(first.Y + 0.5f));
        }

        private static Vector3 At(PuzzleGameplayScene scene, float x, float y) =>
            scene.Board.Compartments["main"].transform.position + new Vector3(x + 0.5f, 0f, -(y + 0.5f));

        private static void Place(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Tray.ItemViews[id];
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, TrayGrab(view)), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(At(scene, x + first.X, y + first.Y));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id);
        }

        private static void PackLv1(PuzzleGameplayScene scene, bool includeLast = true)
        {
            Place(scene, "travel-pouch-1", 1, 3);
            Place(scene, "sunglasses-1", 3, 6);
            if (includeLast)
                Place(scene, "shampoo-1", 3, 2);
        }

        // ---------------------------------------------------------------- safe area

        [Test]
        public void NormalizeSafeArea_MapsPixelsToAnchors_AndFallsBackToFullScreen()
        {
            var safe = PuzzleHud.NormalizeSafeArea(new Rect(0f, 0f, 1080f, 2230f), 1080f, 2340f);
            Assert.That(safe.yMax, Is.EqualTo(2230f / 2340f).Within(1e-5f));
            Assert.That(safe.xMin, Is.Zero);
            Assert.That(PuzzleHud.NormalizeSafeArea(new Rect(0f, 0f, 0f, 0f), 1080f, 2340f), Is.EqualTo(FullScreen), "degenerate");
            Assert.That(PuzzleHud.NormalizeSafeArea(new Rect(0f, 0f, 1080f, 2340f), 0f, 0f), Is.EqualTo(FullScreen), "no screen");
        }

        [UnityTest]
        public IEnumerator SafeArea_FollowsInjectedRect_AndUpdatesWhenItChanges()
        {
            PuzzleHud.SafeAreaOverride = AndroidPunchHole;
            var scene = Scene();
            yield return null;
            var hud = scene.Hud;
            Assert.That(hud.AppliedSafeArea, Is.EqualTo(AndroidPunchHole));
            Assert.That(hud.SafeArea.anchorMax.y, Is.EqualTo(AndroidPunchHole.yMax).Within(1e-5f));
            var pill = hud.SafeArea.Find("Header") as RectTransform;
            Assert.That(pill, Is.Not.Null, "level header lives inside the safe area");
            var corners = new Vector3[4];
            pill.GetWorldCorners(corners);
            Assert.That(corners[1].y, Is.LessThanOrEqualTo(AndroidPunchHole.yMax * Screen.height + 0.5f), "pill below the cutout");

            PuzzleHud.SafeAreaOverride = IPhoneDynamicIsland;
            yield return null;
            Assert.That(hud.AppliedSafeArea, Is.EqualTo(IPhoneDynamicIsland), "re-evaluated at runtime");
            Assert.That(hud.SafeArea.anchorMin.y, Is.EqualTo(IPhoneDynamicIsland.yMin).Within(1e-5f), "home indicator inset");
            pill.GetWorldCorners(corners);
            Assert.That(corners[1].y, Is.LessThanOrEqualTo(IPhoneDynamicIsland.yMax * Screen.height + 0.5f));
            var undo = hud.ButtonCenter(PuzzleHudAction.Undo);
            Assert.That(hud.Hit(undo), Is.EqualTo(PuzzleHudAction.Undo), "hit testing follows the moved buttons");

            PuzzleHud.SafeAreaOverride = null;
            yield return null;
            Assert.That(hud.AppliedSafeArea,
                Is.EqualTo(PuzzleHud.NormalizeSafeArea(Screen.safeArea, Screen.width, Screen.height)), "device safe area");
        }

        [UnityTest]
        public IEnumerator SafeArea_DoesNotChangeCameraFraming()
        {
            var scene = Scene();
            yield return null;
            scene.FrameCamera();
            var position = scene.Camera.transform.position;
            var size = scene.Camera.orthographicSize;
            PuzzleHud.SafeAreaOverride = IPhoneDynamicIsland;
            yield return null;
            scene.FrameCamera();
            Assert.That(scene.Camera.transform.position, Is.EqualTo(position));
            Assert.That(scene.Camera.orthographicSize, Is.EqualTo(size));
        }

        [UnityTest]
        public IEnumerator SafeAreaDebugOverlay_IsNotPartOfTheNormalHud()
        {
            var scene = Scene();
            yield return null;
            Assert.That(scene.Hud.SafeAreaDebugVisible, Is.False);
            Assert.That(scene.Hud.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Safe Area Debug"), Is.False);
            scene.Hud.SetSafeAreaDebugVisible(true);
            Assert.That(scene.Hud.SafeAreaDebugVisible, Is.True, "available in the editor / development builds");
            scene.Hud.SetSafeAreaDebugVisible(false);
            Assert.That(scene.Hud.SafeAreaDebugVisible, Is.False);
        }

        // ---------------------------------------------------------------- drag

        private static List<(Renderer Renderer, Material[] Materials)> Snapshot(PuzzleItemView view) =>
            view.VisualRoot.GetComponentsInChildren<Renderer>(true).Select(r => (r, r.sharedMaterials)).ToList();

        private static void AssertRealArt(PuzzleItemView view, List<(Renderer Renderer, Material[] Materials)> before, string id)
        {
            Assert.That(view.UsesPrefab, Is.True, id + " uses item art, not footprint blocks");
            Assert.That(view.IsGhosted, Is.False, id + " is not the white ghost");
            Assert.That(before, Is.Not.Empty);
            foreach (var (renderer, materials) in before)
            {
                Assert.That(renderer.enabled, Is.True, id + " " + renderer.name + " visible");
                Assert.That(renderer.sharedMaterials, Is.EqualTo(materials), id + " " + renderer.name + " keeps its own materials");
            }
        }

        [UnityTest]
        public IEnumerator Drag_KeepsRealArtForEveryLv1Item_TintsOnlyWhenInvalid_AndNeverTouchesState()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var hash = scene.Session.CurrentState.Hash;
            var moves = scene.Session.MoveCount;
            var undo = scene.Session.UndoDepth;
            foreach (var id in Lv1Items)
            {
                var view = scene.Tray.ItemViews[id];
                var before = Snapshot(view);
                Assert.That(scene.Drag.BeginDrag(id, TrayGrab(view)), Is.EqualTo(DragBeginResult.Started));
                scene.Drag.UpdateDrag(At(scene, 3f, 2f));
                Assert.That(scene.Drag.PreviewValid, Is.True, id + " valid hover");
                AssertRealArt(view, before, id);
                Assert.That(view.RejectTinted, Is.False, id + " valid hover is untinted");
                Assert.That(view.ShadowVisible, Is.True, id + " keeps a lift shadow");
                var shadow = view.transform.Find("Contact Shadow");
                Assert.That(shadow.position.y, Is.LessThan(view.transform.position.y - PuzzleDragController.LiftHeight * 0.5f),
                    id + " shadow stays down on the surface");
                Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
                scene.Drag.Cancel();
                // Cancel leaves the item selected (lifted) in the tray; its shadow is back on the mat, not left dropped.
                Assert.That(view.transform.Find("Contact Shadow").position.y,
                    Is.EqualTo(scene.Tray.transform.position.y).Within(0.02f), id + " shadow back on the mat");
                foreach (var renderer in view.VisualRoot.GetComponentsInChildren<Renderer>())
                    Assert.That(renderer.HasPropertyBlock(), Is.False, id + " no tint left behind");
            }

            Place(scene, "travel-pouch-1", 1, 3);
            hash = scene.Session.CurrentState.Hash;
            moves = scene.Session.MoveCount;
            undo = scene.Session.UndoDepth;
            // In-bounds anchors that overlap the pouch (x 1-2, y 3-5).
            foreach (var (id, x, y) in new[] { ("shampoo-1", 1f, 3f), ("sunglasses-1", 1f, 5f) })
            {
                var view = scene.Tray.ItemViews[id];
                var before = Snapshot(view);
                scene.Drag.BeginDrag(id, TrayGrab(view));
                scene.Drag.UpdateDrag(At(scene, x, y));
                Assert.That(scene.Drag.HasCandidate && !scene.Drag.PreviewValid, Is.True, id + " overlaps the pouch");
                AssertRealArt(view, before, id);
                Assert.That(view.RejectTinted, Is.True, id + " invalid hover is tinted");
                scene.Drag.UpdateDrag(At(scene, 3f, 2f));
                Assert.That(view.RejectTinted, Is.False, id + " tint clears when it fits again");
                scene.Drag.UpdateDrag(At(scene, x, y));
                var step = scene.Drag.Drop();
                Assert.That(step.Move.IsAccepted, Is.False);
                Assert.That(scene.Tray.ItemViews[id].RejectTinted, Is.False);
                Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash), "rejected drop keeps state");
                Assert.That(scene.Session.MoveCount, Is.EqualTo(moves));
                Assert.That(scene.Session.UndoDepth, Is.EqualTo(undo));
            }
        }

        // A suitcase item dragged again also keeps its art; the commit lands where the glow was.
        [UnityTest]
        public IEnumerator Drag_FromSuitcase_KeepsArt_AndCommitsWhereTheGlowWas()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var view = scene.Board.ItemViews["sweater-1"];
            var before = Snapshot(view);
            Assert.That(scene.Drag.BeginDrag("sweater-1", At(scene, 0f, 0f)), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(At(scene, 2f, 4f));
            Assert.That(scene.Drag.PreviewValid, Is.True);
            AssertRealArt(view, before, "sweater-1 (board)");
            var glow = scene.Drag.FootprintPreview.bounds;
            scene.Drag.Drop();
            var placed = scene.Board.ItemViews["sweater-1"];
            Assert.That(placed.Placement.Anchor, Is.EqualTo(new Cell(2, 4)));
            Assert.That(placed.transform.position.x, Is.EqualTo(glow.center.x - 1.5f).Within(0.02f));
            Assert.That(placed.transform.position.z, Is.EqualTo(glow.center.z + 1.5f).Within(0.02f));
            Assert.That(placed.transform.Find("Contact Shadow").localPosition.y, Is.EqualTo(0f).Within(1e-4f));
        }

        // ---------------------------------------------------------------- zip it

        // Measures the closing lid against the packed Lv1 items along vertical lines (both faces), at several lid poses,
        // and lists item / lid render state. Writes Builds/fix-slice-00/zipit-clearance.txt.
        [UnityTest, Explicit("FIX-SLICE-00 Zip It diagnostics")]
        public IEnumerator ZipIt_LidClearanceDiagnostics()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.Completion.AutoAdvance = false;
            PackLv1(scene);
            var rig = scene.Board.Container;
            var log = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            log.AppendLine("== render state");
            foreach (var view in scene.Board.ItemViews.Values)
                foreach (var renderer in view.VisualRoot.GetComponentsInChildren<Renderer>())
                    LogRenderer(log, view.InstanceId, renderer);
            foreach (var renderer in rig.Lid.GetComponentsInChildren<Renderer>())
                LogRenderer(log, "lid", renderer);
            log.AppendLine(string.Format(inv, "interior floor y {0:F3}  interior max y {1:F3}  hinge y {2:F3}  lid pivot y {3:F3}",
                rig.InteriorMin.position.y, rig.InteriorMax.position.y, rig.HingeAnchor.position.y, rig.Lid.position.y));
            foreach (var view in scene.Board.ItemViews.Values)
            {
                var bounds = Bounds(view);
                log.AppendLine(string.Format(inv, "{0}: visual bottom {1:F3} top {2:F3} (layer floor {3:F3})", view.InstanceId,
                    bounds.min.y, bounds.max.y, view.transform.position.y));
            }

            var added = new List<Collider>();
            var lidColliders = new HashSet<Collider>();
            var itemColliders = new Dictionary<Collider, string>();
            foreach (var filter in rig.Lid.GetComponentsInChildren<MeshFilter>())
                lidColliders.Add(AddCollider(filter, added));
            foreach (var view in scene.Board.ItemViews.Values)
                foreach (var filter in view.VisualRoot.GetComponentsInChildren<MeshFilter>())
                    itemColliders[AddCollider(filter, added)] = view.InstanceId;
            var backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                foreach (var t in new[] { 0.3f, 0.5f, 0.7f, 0.85f, 0.95f, 1f })
                {
                    var ease = t * t * (3f - 2f * t);
                    rig.Lid.localRotation = Quaternion.Slerp(rig.LidOpenLocalRotation, ContainerRig.LidClosedLocalRotation, ease);
                    Physics.SyncTransforms();
                    log.AppendLine(string.Format(inv, "== lid time {0:P0} (rotation {1:P0})", t, ease));
                    foreach (var view in scene.Board.ItemViews.Values)
                    {
                        var bounds = Bounds(view);
                        var worst = float.MaxValue;
                        var at = Vector3.zero;
                        var covered = 0;
                        for (var x = bounds.min.x; x <= bounds.max.x; x += 0.05f)
                            for (var z = bounds.min.z; z <= bounds.max.z; z += 0.05f)
                            {
                                var hits = Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f);
                                var itemTop = float.MinValue;
                                var lidLow = float.MaxValue;
                                foreach (var hit in hits)
                                {
                                    if (itemColliders.TryGetValue(hit.collider, out var owner) && owner == view.InstanceId)
                                        itemTop = Mathf.Max(itemTop, hit.point.y);
                                    else if (lidColliders.Contains(hit.collider))
                                        lidLow = Mathf.Min(lidLow, hit.point.y);
                                }
                                if (itemTop == float.MinValue || lidLow == float.MaxValue)
                                    continue;
                                covered++;
                                if (lidLow - itemTop < worst)
                                {
                                    worst = lidLow - itemTop;
                                    at = new Vector3(x, itemTop, z);
                                }
                            }
                        log.AppendLine(covered == 0
                            ? string.Format(inv, "  {0}: lid not over item", view.InstanceId)
                            : string.Format(inv, "  {0}: min clearance {1:F3} at ({2:F2}, {3:F3}, {4:F2}) over {5} samples{6}",
                                view.InstanceId, worst, at.x, at.y, at.z, covered, worst < 0f ? "  ** PENETRATES **" : ""));
                    }
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = backfaces;
                foreach (var collider in added)
                    Object.Destroy(collider);
                rig.SetLidClosed(false);
            }
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/fix-slice-00"));
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "zipit-clearance.txt"), log.ToString());
            Debug.Log("[fix-slice-00] zip it diagnostics\n" + log);
        }

        private static void LogRenderer(StringBuilder log, string owner, Renderer renderer)
        {
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;
                var surface = material.HasProperty("_Surface") ? material.GetFloat("_Surface") : -1f;
                var zwrite = material.HasProperty("_ZWrite") ? material.GetFloat("_ZWrite") : -1f;
                log.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0} | {1} | {2} | shader {3} | queue {4} | surface {5} | zwrite {6} | sortingOrder {7}",
                    owner, renderer.name, material.name, material.shader.name, material.renderQueue, surface, zwrite,
                    renderer.sortingOrder));
            }
        }

        private static Collider AddCollider(MeshFilter filter, List<Collider> added)
        {
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            added.Add(collider);
            return collider;
        }

        private static Bounds Bounds(PuzzleItemView view)
        {
            var renderers = view.VisualRoot.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        // ---------------------------------------------------------------- visual proof

        [UnityTest, Explicit("Writes FIX-SLICE-00 review captures")]
        public IEnumerator CaptureFixSlice00()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            try
            {
                var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/fix-slice-00"));
                Directory.CreateDirectory(folder);
                yield return LoadGameplayScene();
                var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
                scene.Hud.RenderThrough(scene.Camera);
                scene.Completion.AutoAdvance = false;

                // Safe area: "before" = the pre-fix full-canvas layout (override = full screen) under the same cutout.
                PuzzleHud.SafeAreaOverride = AndroidPunchHole;
                scene.Hud.SetSafeAreaDebugVisible(true);
                using (Cutout(scene, AndroidPunchHole, false))
                    yield return Capture(scene, folder, "01-lv1-idle-safe-area-overlay", 1080, 2340);
                scene.Hud.SetSafeAreaDebugVisible(false);
                foreach (var (name, profile, island) in new[]
                         { ("02-android-punch-hole", AndroidPunchHole, false), ("03-iphone-notch", IPhoneDynamicIsland, true) })
                {
                    using (Cutout(scene, profile, island))
                    {
                        PuzzleHud.SafeAreaOverride = FullScreen;
                        yield return Capture(scene, folder, name + "-before", 1080, 2340);
                        PuzzleHud.SafeAreaOverride = profile;
                        yield return Capture(scene, folder, name + "-after", 1080, 2340);
                    }
                }
                PuzzleHud.SafeAreaOverride = FullScreen;
                yield return Capture(scene, folder, "04-9x16-no-notch", 1080, 1920);

                // Drag: every shipped Lv1 item over an empty valid spot, then valid / invalid frames.
                var number = 5;
                foreach (var id in Lv1Items)
                {
                    scene.Drag.BeginDrag(id, TrayGrab(scene.Tray.ItemViews[id]));
                    scene.Drag.UpdateDrag(At(scene, 1f, 2f));
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return Capture(scene, folder, $"{number++:00}-drag-{id.Replace("-1", "")}", 1080, 2340);
                    scene.Drag.Cancel();
                }
                Place(scene, "laptop-1", 0, 0);
                scene.Drag.BeginDrag("sweater-1", TrayGrab(scene.Tray.ItemViews["sweater-1"]));
                scene.Drag.UpdateDrag(At(scene, 0f, 4f));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.That(scene.Drag.PreviewValid, Is.True);
                yield return Capture(scene, folder, "09-drag-valid-drop", 1080, 2340);
                scene.Drag.UpdateDrag(At(scene, 1f, 1f));
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(scene.Drag.PreviewValid, Is.False);
                yield return Capture(scene, folder, "10-drag-invalid-drop", 1080, 2340);
                scene.Drag.Cancel();

                // Zip It (lid rotation fractions via the presenter's smoothstep: t 0.3735 -> 30 %, 0.6265 -> 70 %).
                Place(scene, "book-1", 3, 4);
                Place(scene, "sweater-1", 0, 4);
                Place(scene, "sneaker-1", 3, 0);
                yield return Capture(scene, folder, "11-complete-before-closure", 1080, 2340);
                scene.Completion.Advance(PuzzleCompletionPresenter.SettleDuration + PuzzleCompletionPresenter.AnticipationDuration
                    + PuzzleCompletionPresenter.LidDuration * 0.3735f);
                yield return Capture(scene, folder, "12-lid-30-percent", 1080, 2340);
                scene.Completion.Advance(PuzzleCompletionPresenter.LidDuration * (0.6265f - 0.3735f));
                yield return Capture(scene, folder, "13-lid-70-percent", 1080, 2340);
                scene.Completion.Advance(PuzzleCompletionPresenter.LidDuration * (1f - 0.6265f) + 0.0001f);
                yield return Capture(scene, folder, "14-lid-closed", 1080, 2340);
                scene.Completion.Advance(2f);
                yield return Capture(scene, folder, "15-packed", 1080, 2340);
                Debug.Log("[fix-slice-00] " + folder);
            }
            finally
            {
                PuzzleHud.SafeAreaOverride = null;
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        // Review-only black cutout drawn on the HUD canvas root (outside the safe area) where the profile reserves it.
        private sealed class CutoutMarker : System.IDisposable
        {
            public GameObject Marker;
            public void Dispose() => Object.Destroy(Marker);
        }

        private static CutoutMarker Cutout(PuzzleGameplayScene scene, Rect safe, bool island)
        {
            var marker = new GameObject("Review Cutout", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)marker.transform;
            rect.SetParent(scene.Hud.transform, false);
            var inset = 1f - safe.yMax;
            var center = 1f - inset * 0.5f;
            var halfHeight = inset * (island ? 0.3f : 0.32f);
            var halfWidth = island ? 0.16f : halfHeight * 2340f / 1080f;
            rect.anchorMin = new Vector2(0.5f - halfWidth, center - halfHeight);
            rect.anchorMax = new Vector2(0.5f + halfWidth, center + halfHeight);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            marker.GetComponent<Image>().color = Color.black;
            marker.GetComponent<Image>().raycastTarget = false;
            return new CutoutMarker { Marker = marker };
        }

        private static IEnumerator Capture(PuzzleGameplayScene scene, string folder, string name, int width, int height)
        {
            var camera = scene.Camera;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            scene.FrameCamera();
            yield return null;
            Canvas.ForceUpdateCanvases();
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
    }
}
