using System.Collections;
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
    // BACKDROP-SLICE-01: the travel-world surface and edge props are presentation-only and stay out of every input,
    // framing and gameplay path.
    public sealed class BackdropSlice01Tests
    {
        [TearDown]
        public void TearDown() => PuzzleHud.SafeAreaOverride = null;

        private static IEnumerator LoadGameplayScene()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
        }

        private static Rect ScreenRect(Camera camera, Bounds b)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 8; i++)
            {
                var p = camera.WorldToScreenPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest]
        public IEnumerator SceneAsset_WiresTheBackdrop_AsOneSurfaceAndFivePropsOnOneMaterial_WithNoInputSurface()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            Assert.That(scene.BackdropSurface, Is.Not.Null);
            Assert.That(scene.BackdropProps, Is.Not.Null);
            var table = scene.Table;
            Assert.That(table.Backdrop, Is.Not.Null);
            Assert.That(table.PropRenderers.Count, Is.EqualTo(5));
            Assert.That(table.PropRenderers.Select(r => r.sharedMaterial).Distinct().Count(), Is.EqualTo(1), "one prop material");
            Assert.That(table.GetComponentsInChildren<Collider>(true), Is.Empty, "no colliders");
            Assert.That(table.GetComponentsInChildren<Graphic>(true), Is.Empty, "no UI graphics (nothing to raycast)");
            Assert.That(table.GetComponentsInChildren<Canvas>(true), Is.Empty);
            Assert.That(table.transform.IsChildOf(scene.Board.transform) || table.transform.IsChildOf(scene.Tray.transform), Is.False,
                "outside the bounds camera framing measures");
        }

        [UnityTest]
        public IEnumerator Backdrop_DoesNotChangeCameraFraming()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.FrameCamera();
            var position = scene.Camera.transform.position;
            var size = scene.Camera.orthographicSize;
            scene.Table.gameObject.SetActive(false);
            scene.FrameCamera();
            Assert.That(scene.Camera.transform.position, Is.EqualTo(position));
            Assert.That(scene.Camera.orthographicSize, Is.EqualTo(size));
            scene.Table.gameObject.SetActive(true);
        }

        // Golden Lv1 composition (compact card tray) at 1080x2340 and 9:16: props sit cropped at the edges, outside the
        // suitcase bed and the loose items (they may tuck under the dock, as in the locked frame); a press on a prop starts nothing and leaves the state
        // untouched; a press on a loose item still picks it.
        [UnityTest]
        public IEnumerator Props_StayOutOfGameplayAndControls_AndNeverInterceptInput()
        {
            yield return LoadGameplayScene();
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var camera = scene.Camera;
            var art = new GoldenLv1LayoutReview.GoldenProxyArt();
            scene.LoadLevel(GoldenLv1LayoutReview.LoadCandidate(), "Seviye 1", art.Resolve);
            yield return null;
            var shell = new GoldenLv1LayoutReview.LayoutGreybox(scene, 'H');
            foreach (var (w, h) in new[] { (1080, 2340), (1080, 1920) })
            {
                var target = new RenderTexture(w, h, 24);
                camera.targetTexture = target;
                scene.FrameCamera();
                shell.Refresh();
                yield return null;
                shell.Refresh();
                var frame = scene.Board.CompartmentFrames().Single();
                var bed = ScreenRect(camera, new Bounds(frame.Origin + new Vector3(frame.Width * 0.5f, 0f, -frame.Height * 0.5f),
                    new Vector3(frame.Width, 0f, frame.Height)));
                foreach (var prop in scene.Table.PropRenderers)
                {
                    var r = ScreenRect(camera, prop.bounds);
                    Assert.That(r.Overlaps(bed), Is.False, $"{prop.name} off the bed at {w}x{h}");
                    foreach (var view in scene.Tray.ItemViews.Values)
                        foreach (var item in view.VisualRoot.GetComponentsInChildren<Renderer>())
                            Assert.That(ScreenRect(camera, item.bounds).Overlaps(r), Is.False, $"{prop.name} clear of {view.InstanceId} at {w}x{h}");
                    var visible = Rect.MinMaxRect(Mathf.Max(r.xMin, 0f), Mathf.Max(r.yMin, 0f), Mathf.Min(r.xMax, w), Mathf.Min(r.yMax, h));
                    Assert.That(visible.width < r.width || visible.height < r.height, Is.True, $"{prop.name} is cropped by the screen edge");
                }
                camera.targetTexture = null;
                Object.Destroy(target);
            }

            scene.FrameCamera();
            shell.Refresh();
            var hash = scene.Session.CurrentState.Hash;
            var postcard = scene.Table.PropRenderers.Single(r => r.name == "Postcard");
            var press = (Vector2)camera.WorldToScreenPoint(postcard.bounds.center);
            scene.HandlePointer(new PointerSignal(PointerPhase.Down, press));
            Assert.That(scene.Drag.IsDragging, Is.False, "a prop is not an item");
            scene.HandlePointer(new PointerSignal(PointerPhase.Up, press));
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash));
            var grab = GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews["shampoo-1"]);
            scene.HandlePointer(new PointerSignal(PointerPhase.Down, camera.WorldToScreenPoint(grab)));
            Assert.That(scene.Drag.DraggedInstanceId, Is.EqualTo("shampoo-1"), "loose items are still picked");
            scene.Drag.Cancel();
            shell.Dispose();
            art.Dispose();
        }

        // ------------------------------------------------------------------ visual proof

        [UnityTest, Explicit("Writes BACKDROP-SLICE-01 captures")]
        public IEnumerator CaptureBackdropSlice01()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            var art = new GoldenLv1LayoutReview.GoldenProxyArt();
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/backdrop-slice-01"));
            Directory.CreateDirectory(folder);
            var log = new StringBuilder();
            try
            {
                yield return LoadGameplayScene();
                var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
                scene.Hud.RenderThrough(scene.Camera);
                scene.Completion.AutoAdvance = false;
                yield return Capture(scene, folder, "00-shipped-lv1-runtime-hud", 1080, 2340, log);
                scene.LoadLevel(GoldenLv1LayoutReview.LoadCandidate(), "Seviye 1", art.Resolve);
                yield return null;
                using (var shell = new GoldenLv1LayoutReview.LayoutGreybox(scene, 'H'))
                {
                    // Items hidden by renderer only, so the framing (which measures them) stays the gameplay framing.
                    var items = scene.Board.ItemViews.Values.Concat(scene.Tray.ItemViews.Values)
                        .SelectMany(v => v.GetComponentsInChildren<Renderer>()).ToList();
                    items.ForEach(r => r.enabled = false);
                    yield return Capture(scene, folder, "01-golden-lv1-empty", 1080, 2340, log, shell);
                    items.ForEach(r => r.enabled = true);
                    yield return Capture(scene, folder, "02-initial", 1080, 2340, log, shell);
                    Select(scene, "travel-pouch-1");
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return Capture(scene, folder, "03-one-item-selected", 1080, 2340, log, shell);
                    scene.Drag.CancelCandidate();
                    GoldenLv1LayoutReview.Place(scene, "sunglasses-1", 3, 6);
                    yield return new WaitForSecondsRealtime(0.4f);
                    yield return Capture(scene, folder, "05-source-tray-two-items", 1080, 2340, log, shell);
                    Select(scene, "travel-pouch-1");
                    yield return new WaitForSecondsRealtime(0.3f);
                    yield return Capture(scene, folder, "04-partially-packed", 1080, 2340, log, shell);
                    scene.Drag.CancelCandidate();
                    GoldenLv1LayoutReview.Place(scene, "travel-pouch-1", 1, 3);
                    yield return new WaitForSecondsRealtime(0.4f);
                    yield return Capture(scene, folder, "06-source-tray-one-item", 1080, 2340, log, shell);
                    GoldenLv1LayoutReview.Place(scene, "shampoo-1", 3, 2);
                    Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.True);
                    yield return new WaitForSecondsRealtime(0.4f);
                    yield return Capture(scene, folder, "07-completed-pre-zip", 1080, 2340, log, shell);
                    scene.Completion.Advance(5f);
                    scene.Perform(PuzzleHudAction.Restart);
                    Assert.That(scene.Session.MoveCount, Is.Zero, "replay restores the initial state");
                    yield return null;
                    yield return Capture(scene, folder, "08-9x16", 1080, 1920, log, shell);
                    PuzzleHud.SafeAreaOverride = FixSlice00Tests.AndroidPunchHole;
                    shell.Cutout(FixSlice00Tests.AndroidPunchHole);
                    yield return Capture(scene, folder, "09-android-cutout", 1080, 2340, log, shell);
                    PuzzleHud.SafeAreaOverride = FixSlice00Tests.IPhoneDynamicIsland;
                    shell.Cutout(FixSlice00Tests.IPhoneDynamicIsland);
                    yield return Capture(scene, folder, "10-iphone-notch", 1080, 2340, log, shell);
                }
                File.WriteAllText(Path.Combine(folder, "capture-log.txt"), log.ToString());
                Debug.Log("[backdrop-slice-01] " + folder + "\n" + log);
            }
            finally
            {
                PuzzleHud.SafeAreaOverride = null;
                art.Dispose();
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static void Select(PuzzleGameplayScene scene, string id)
        {
            scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews[id]));
            scene.Drag.Cancel();
        }

        private static IEnumerator Capture(PuzzleGameplayScene scene, string folder, string name, int width, int height,
            StringBuilder log, GoldenLv1LayoutReview.LayoutGreybox shell = null)
        {
            var camera = scene.Camera;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            scene.FrameCamera();
            shell?.Refresh();
            yield return null;
            shell?.Refresh();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            log.Append(name).Append(':');
            foreach (var prop in scene.Table.PropRenderers)
            {
                var r = ScreenRect(camera, prop.bounds);
                log.Append(string.Format(CultureInfo.InvariantCulture, " {0}=[{1:F0},{2:F0},{3:F0},{4:F0}]", prop.name.Replace(" ", ""),
                    r.xMin, height - r.yMax, r.xMax, height - r.yMin));
            }
            log.AppendLine();
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(target);
            Object.Destroy(image);
        }
    }
}
