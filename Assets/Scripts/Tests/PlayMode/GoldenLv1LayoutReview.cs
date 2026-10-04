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
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // LAYOUT-SLICE-01 review harness (test-only, nothing ships): the Golden Lv1 candidate in the real PuzzleGameplay scene,
    // with labelled proxy items for the not-yet-produced roster and a greybox shell (header, objective note variants,
    // tray variants, bottom dock with a contextual action slot) drawn over the real camera framing.
    public sealed class GoldenLv1LayoutReview
    {
        private static readonly Rotation[] Upright = { Rotation.Degrees0, Rotation.Degrees90 };
        private static readonly Color Ink = PresentationKit.Hex(0x2F3A3C);
        private static readonly Color Muted = PresentationKit.Hex(0x7A7468);
        private static readonly Color Cream = PresentationKit.Hex(0xF7F1E6);

        public static PuzzleLevel LoadCandidate()
        {
            var catalog = PuzzleItemCatalog.Create().Where(s => s.Id == "sweater").Concat(new[]
            {
                Single("passport", 1, 2), Single("towel", 1, 4), Single("shampoo", 1, 3),
                Single("sunglasses", 2, 1), Single("travel-pouch", 2, 3)
            });
            return LevelJsonLoaderV2.Load(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath,
                "Scripts/Tests/EditMode/Fixtures/LevelsV2/golden-lv1-candidate.json")), catalog);
        }

        private static ItemSpec Single(string id, int width, int height) => new ItemSpec(id, "open", new[]
        {
            new ItemStateSpec("open", new ItemShape(from y in Enumerable.Range(0, height) from x in Enumerable.Range(0, width)
                select new Cell(x, y)), 1, Upright)
        });

        internal static Vector3 TrayGrab(PuzzleItemView view)
        {
            var first = view.Footprint.OccupiedCells[0];
            return view.transform.position + view.transform.lossyScale.x * new Vector3(first.X + 0.5f, 0f, -(first.Y + 0.5f));
        }

        internal static void Place(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Tray.ItemViews[id];
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, TrayGrab(view)), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                + new Vector3(x + first.X + 0.5f, 0f, -(y + first.Y + 0.5f)));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id);
        }

        [UnityTest, Explicit("Writes LAYOUT-SLICE-01 greybox captures")]
        public IEnumerator CaptureGoldenLv1Layout()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(System.Array.IndexOf(QualitySettings.names, "Mobile"), true);
            var art = new GoldenProxyArt();
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/golden-lv1/layout"));
            Directory.CreateDirectory(folder);
            var metrics = new StringBuilder();
            try
            {
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
                scene.Hud.RenderThrough(scene.Camera);
                scene.Completion.AutoAdvance = false;
                yield return CaptureRaw(scene, folder, "00-current-shipped-lv1", 1080, 2340);
                var level = LoadCandidate();
                scene.LoadLevel(level, "Seviye 1", art.Resolve);
                yield return null;

                foreach (var variant in new[] { 'A', 'B' })
                {
                    using (var shell = new LayoutGreybox(scene, variant))
                    {
                        yield return Capture(scene, shell, folder, $"{variant}1-idle", 1080, 2340, metrics);
                        scene.Drag.BeginDrag("travel-pouch-1", TrayGrab(scene.Tray.ItemViews["travel-pouch-1"]));
                        scene.Drag.Cancel();
                        yield return new WaitForSecondsRealtime(0.2f);
                        yield return Capture(scene, shell, folder, $"{variant}2-source-selected", 1080, 2340, metrics);
                        Place(scene, "travel-pouch-1", 1, 3);
                        Place(scene, "sunglasses-1", 3, 6);
                        yield return new WaitForSecondsRealtime(0.4f);
                        yield return Capture(scene, shell, folder, $"{variant}3-partially-packed", 1080, 2340, metrics);
                        scene.Perform(PuzzleHudAction.Restart);
                        yield return null;
                    }
                }

                // Selected: H = Variant A's lid note + Variant B's card tray (see report).
                const char best = 'H';
                using (var shell = new LayoutGreybox(scene, best))
                {
                    yield return Capture(scene, shell, folder, $"{best}1-idle", 1080, 2340, metrics);
                    scene.Drag.BeginDrag("travel-pouch-1", TrayGrab(scene.Tray.ItemViews["travel-pouch-1"]));
                    scene.Drag.Cancel();
                    yield return new WaitForSecondsRealtime(0.2f);
                    yield return Capture(scene, shell, folder, $"{best}2-source-selected", 1080, 2340, metrics);
                    scene.Drag.CancelCandidate();
                    yield return Capture(scene, shell, folder, $"{best}4-9x16", 1080, 1920, metrics);
                    PuzzleHud.SafeAreaOverride = FixSlice00Tests.AndroidPunchHole;
                    shell.Cutout(FixSlice00Tests.AndroidPunchHole);
                    yield return Capture(scene, shell, folder, $"{best}5-notched-android", 1080, 2340, metrics);
                    PuzzleHud.SafeAreaOverride = FixSlice00Tests.IPhoneDynamicIsland;
                    shell.Cutout(FixSlice00Tests.IPhoneDynamicIsland);
                    yield return Capture(scene, shell, folder, $"{best}6-notched-iphone", 1080, 2340, metrics);
                }
                // STYLE-FRAME-01 base plates: the selected layout under the Android cutout profile, pouch selected.
                PuzzleHud.SafeAreaOverride = FixSlice00Tests.AndroidPunchHole;
                using (var shell = new LayoutGreybox(scene, best))
                {
                    Place(scene, "sunglasses-1", 3, 6);
                    scene.Drag.BeginDrag("travel-pouch-1", TrayGrab(scene.Tray.ItemViews["travel-pouch-1"]));
                    scene.Drag.Cancel();
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return StylePlates(scene, shell, Path.GetFullPath(Path.Combine(folder, "../style")));
                }
                File.WriteAllText(Path.Combine(folder, "layout-metrics.txt"), metrics.ToString());
                Debug.Log("[golden-lv1-layout] " + folder + "\n" + metrics);
            }
            finally
            {
                PuzzleHud.SafeAreaOverride = null;
                art.Dispose();
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        // Writes style-full (as rendered), style-fg-black / style-fg-white (no table, no post: a difference matte) and
        // style-layout.json (UI and item screen rects, pixels from the top-left).
        private static IEnumerator StylePlates(PuzzleGameplayScene scene, LayoutGreybox shell, string folder)
        {
            Directory.CreateDirectory(folder);
            const int width = 1080, height = 2340;
            var camera = scene.Camera;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            scene.FrameCamera();
            shell.Refresh();
            yield return null;
            shell.Refresh();
            File.WriteAllText(Path.Combine(folder, "style-layout.json"), shell.LayoutJson(width, height));
            shell.SetCanvasVisible(false);
            foreach (var label in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                label.gameObject.SetActive(false);
            Plate(camera, target, folder, "style-full");
            var clearFlags = camera.clearFlags;
            var background = camera.backgroundColor;
            scene.Table.Surface.SetActive(false);
            scene.Table.Vignette.SetActive(false);
            var post = GameObject.Find("Presentation Post").GetComponents<Behaviour>().Where(b => b.GetType().Name == "Volume").ToList();
            post.ForEach(b => b.enabled = false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            Plate(camera, target, folder, "style-fg-black");
            camera.backgroundColor = Color.white;
            Plate(camera, target, folder, "style-fg-white");
            camera.clearFlags = clearFlags;
            camera.backgroundColor = background;
            post.ForEach(b => b.enabled = true);
            scene.Table.Surface.SetActive(true);
            scene.Table.Vignette.SetActive(true);
            shell.SetCanvasVisible(true);
            camera.targetTexture = null;
            Object.Destroy(target);
        }

        private static void Plate(Camera camera, RenderTexture target, string folder, string name)
        {
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(image);
        }

        private static IEnumerator CaptureRaw(PuzzleGameplayScene scene, string folder, string name, int width, int height)
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

        private static IEnumerator Capture(PuzzleGameplayScene scene, LayoutGreybox shell, string folder, string name,
            int width, int height, StringBuilder metrics)
        {
            var camera = scene.Camera;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            scene.FrameCamera();
            shell.Refresh();
            yield return null;
            shell.Refresh();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            metrics.AppendLine(name + ": " + shell.Describe(width, height));
            camera.targetTexture = null;
            RenderTexture.active = null;
            Object.Destroy(target);
            Object.Destroy(image);
        }

        // ------------------------------------------------------------------ greybox shell

        internal sealed class LayoutGreybox : System.IDisposable
        {
            private const float Ref = 1080f;
            private readonly PuzzleGameplayScene _scene;
            private readonly char _variant;
            private readonly GameObject _canvasRoot;
            private readonly RectTransform _canvas;
            private readonly RectTransform _safe;
            private readonly RectTransform _note;
            private readonly RectTransform _dock;
            private readonly RectTransform _action;
            private readonly Text _actionLabel;
            private readonly Image _actionImage;
            private readonly Text _remaining;
            private readonly Text[] _ruleMarks = new Text[2];
            private readonly Image[] _ruleDots = new Image[2];
            private readonly List<Object> _owned = new List<Object>();
            private readonly Transform _cards;
            private readonly Material _cardMaterial;
            private readonly Material _shellMaterial;
            private readonly Sprite _rounded;
            private readonly Font _font;
            private GameObject _cutout;

            public LayoutGreybox(PuzzleGameplayScene scene, char variant)
            {
                _scene = scene;
                _variant = variant;
                _font = scene.Hud.Font;
                _rounded = Own(PresentationKit.RoundedSprite(96, 40));
                Own(_rounded.texture);
                scene.Hud.SafeArea.gameObject.SetActive(false);

                _canvasRoot = new GameObject("Layout Greybox " + variant, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                var canvas = _canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = scene.Camera;
                canvas.planeDistance = 0.9f;
                var scaler = _canvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(Ref, 1920f);
                scaler.matchWidthOrHeight = 0f;
                _canvas = (RectTransform)_canvasRoot.transform;
                _safe = Stretch(_canvas, "Safe", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

                // Header: travel icon + "Seviye 1" / "İlk Yolculuk". Identity only; no economy.
                var header = Panel(_safe, "Header", new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(560f, 128f), Cream);
                var icon = Panel(header, "Travel Icon", new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(92f, 92f), PresentationKit.Teal);
                Panel(icon, "Case", new Vector2(0.5f, 0.5f), new Vector2(0f, -6f), new Vector2(50f, 36f), Cream);
                Panel(icon, "Handle", new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(22f, 12f), Cream);
                Label(header, "Seviye 1", 46, Ink, new Vector2(0f, 0.5f), new Vector2(340f, 20f), new Vector2(380f, 56f), TextAnchor.MiddleLeft);
                Label(header, "İlk Yolculuk", 32, Muted, new Vector2(0f, 0.5f), new Vector2(340f, -26f), new Vector2(380f, 44f), TextAnchor.MiddleLeft);

                // Objective: A = paper note clipped onto the open lid (placed per frame from the lid's screen rect);
                // B = card directly under the header, overlapping the top of the suitcase composition.
                _note = Panel(_canvas, "Objective Note", new Vector2(0f, 0f), Vector2.zero,
                    variant != 'B' ? new Vector2(610f, 210f) : new Vector2(980f, 150f), Cream);
                if (variant != 'B')
                {
                    _note.localRotation = Quaternion.Euler(0f, 0f, 2.5f);
                    Panel(_note, "Clip", new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(90f, 30f), PresentationKit.Mustard);
                    Label(_note, "Yolculuk Hazırlıkları", 34, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(560f, 44f), TextAnchor.MiddleCenter);
                    Rule(0, "Pasaport üst bölgede olmalı", new Vector2(36f, -108f), 28);
                    Rule(1, "Şampuan sağ bölgede olmalı", new Vector2(36f, -160f), 28);
                }
                else
                {
                    Label(_note, "Yolculuk Hazırlıkları", 32, Ink, new Vector2(0f, 1f), new Vector2(250f, -40f), new Vector2(440f, 44f), TextAnchor.MiddleLeft);
                    Rule(0, "Pasaport üst bölgede", new Vector2(36f, -102f), 30);
                    Rule(1, "Şampuan sağ bölgede", new Vector2(500f, -102f), 30);
                }

                // Bottom dock: secondary Undo / Restart at the thumb, contextual hero slot in the centre.
                _dock = Panel(_safe, "Dock", new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1016f, 196f), Cream);
                IconButton(_dock, "Geri Al", PresentationKit.UndoIcon(128), new Vector2(0f, 0.5f), new Vector2(98f, 0f));
                IconButton(_dock, "Baştan", PresentationKit.RestartIcon(128), new Vector2(1f, 0.5f), new Vector2(-98f, 0f));
                _action = Panel(_dock, "Hero Action", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 140f), PresentationKit.Mustard);
                _actionImage = _action.GetComponent<Image>();
                _actionLabel = Label(_action, "Döndür", 48, Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 140f), TextAnchor.MiddleCenter);
                _remaining = Label(_dock, "", 36, Muted, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 140f), TextAnchor.MiddleCenter);

                // Tray: A = the shared felt panel as shipped; B = one card per remaining item on a shell that shrinks with them.
                _cards = new GameObject("Tray Cards " + variant).transform;
                var template = PresentationKit.TemplateOrFallback(null);
                _cardMaterial = Own(PresentationKit.Matte(template, Cream, null, 0.1f));
                _shellMaterial = Own(PresentationKit.Matte(template, PresentationKit.Hex(0xBFA987), null, 0.1f));
            }

            private void Rule(int index, string text, Vector2 position, int size)
            {
                var dot = Panel(_note, "Rule Dot", new Vector2(0f, 1f), position + new Vector2(20f, 0f), new Vector2(40f, 40f), PresentationKit.Teal);
                _ruleDots[index] = dot.GetComponent<Image>();
                _ruleMarks[index] = Label(dot, "", 28, Cream, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f), TextAnchor.MiddleCenter);
                Label(_note, (index + 1) + ". " + text, size, Ink, new Vector2(0f, 1f), position + new Vector2(56f + 210f, 0f),
                    new Vector2(420f, 44f), TextAnchor.MiddleLeft);
            }

            public void Cutout(Rect safe)
            {
                if (_cutout != null)
                    Object.Destroy(_cutout);
                _cutout = new GameObject("Review Cutout", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)_cutout.transform;
                rect.SetParent(_canvas, false);
                var inset = 1f - safe.yMax;
                var island = safe.yMin > 0f;
                var halfHeight = inset * 0.3f;
                var halfWidth = island ? 0.16f : halfHeight * 2340f / 1080f;
                rect.anchorMin = new Vector2(0.5f - halfWidth, 1f - inset * 0.5f - halfHeight);
                rect.anchorMax = new Vector2(0.5f + halfWidth, 1f - inset * 0.5f + halfHeight);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                _cutout.GetComponent<Image>().color = Color.black;
            }

            public void Refresh()
            {
                var camera = _scene.Camera;
                var safe = PuzzleHud.SafeAreaOverride ?? PuzzleHud.NormalizeSafeArea(Screen.safeArea, Screen.width, Screen.height);
                _safe.anchorMin = safe.min;
                _safe.anchorMax = safe.max;
                var toRef = Ref / camera.pixelWidth;

                // Objective note position.
                var lid = LidScreenRect(camera);
                var headerBottom = (1f - safe.yMax) * camera.pixelHeight * toRef + 150f;
                var canvasHeight = camera.pixelHeight * toRef;
                if (_variant != 'B')
                {
                    // Pinned on the lid's lining, left of centre, between the header and the playable bed's back edge;
                    // it scales down (never below 60 %) rather than covering the bed on shorter aspects.
                    var top = Mathf.Max(canvasHeight - headerBottom - 10f, 0f);
                    var bed = camera.WorldToScreenPoint(_scene.Board.CompartmentFrames()[0].Origin).y * toRef + 16f;
                    var scale = Mathf.Clamp((top - bed) / 230f, 0.6f, 1f);
                    _note.localScale = Vector3.one * scale;
                    _note.anchoredPosition = new Vector2(lid.center.x * toRef - 140f * scale, Mathf.Max(bed + 105f * scale, (top + bed) * 0.5f));
                }
                else
                {
                    _note.anchoredPosition = new Vector2(Ref * 0.5f, canvasHeight - headerBottom - 75f - 8f);
                }

                // Rule status from the live session (presentation of Domain results only).
                var results = _scene.Session.CurrentCompletion.Rules;
                foreach (var (index, id) in new[] { (0, "passport-upper"), (1, "shampoo-right") })
                {
                    var ok = results.First(r => r.RuleId == id).IsSatisfied;
                    var shampooTray = id == "shampoo-right" && _scene.Session.CurrentState.TryGetItem("shampoo-1", out var shampoo)
                        && shampoo.Location.Kind == ItemLocationKind.SourceTray;
                    _ruleDots[index].color = shampooTray ? PresentationKit.Hex(0xC9C1B2) : ok ? PresentationKit.Teal : PresentationKit.Terracotta;
                    _ruleMarks[index].text = shampooTray ? "" : ok ? "" : "!";
                }

                // Contextual hero slot: Rotate only while a rotatable item is selected; otherwise a quiet progress line.
                var rotate = _scene.Drag.CanRotateSelection;
                _action.gameObject.SetActive(rotate);
                var left = _scene.Session.CurrentState.GetItems(ItemLocationKind.SourceTray).Count;
                _remaining.gameObject.SetActive(!rotate);
                _remaining.text = left > 0 ? $"{left} eşya kaldı" : "Hazır!";

                // Tray treatment.
                foreach (Transform child in _cards)
                    Object.Destroy(child.gameObject);
                _scene.Table.SetMatVisible(_variant == 'A' && _scene.Tray.ItemViews.Count > 0);
                if (_variant != 'A' && _scene.Tray.ItemViews.Count > 0)
                {
                    var y = _scene.Tray.transform.position.y;
                    var all = new List<Rect>();
                    foreach (var view in _scene.Tray.ItemViews.Values)
                    {
                        int w = 0, d = 0;
                        foreach (var c in view.Footprint.OccupiedCells)
                        {
                            w = Mathf.Max(w, c.X + 1);
                            d = Mathf.Max(d, c.Y + 1);
                        }
                        var s = _scene.Tray.Scale;
                        var p = view.transform.position;
                        var card = new Rect(p.x - 0.18f, p.z - d * s - 0.18f, w * s + 0.36f, d * s + 0.36f);
                        all.Add(card);
                        Slab("Card " + view.InstanceId, card, 0.16f, y - 0.05f, y - 0.004f, _cardMaterial);
                    }
                    var xMin = all.Min(r => r.xMin) - 0.16f;
                    var xMax = all.Max(r => r.xMax) + 0.16f;
                    var zMin = all.Min(r => r.yMin) - 0.16f;
                    var zMax = all.Max(r => r.yMax) + 0.16f;
                    Slab("Tray Shell", Rect.MinMaxRect(xMin, zMin, xMax, zMax), 0.3f, y - 0.07f, y - 0.03f, _shellMaterial);
                }
            }

            private void Slab(string name, Rect worldXZ, float radius, float bottom, float top, Material material)
            {
                // PresentationKit.Slab takes (x, z) with z as given; build it at the origin and keep the world rect.
                var mesh = PresentationKit.Slab(worldXZ, radius, bottom, top);
                var go = PresentationKit.MeshObject(name, _cards, mesh, material);
                go.GetComponent<MeshRenderer>().receiveShadows = true;
                _owned.Add(mesh);
            }

            private Rect LidScreenRect(Camera camera)
            {
                var bounds = new Bounds();
                var first = true;
                foreach (var renderer in _scene.Board.Lid.GetComponentsInChildren<Renderer>())
                {
                    if (first) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                    first = false;
                }
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                for (var i = 0; i < 8; i++)
                {
                    var p = camera.WorldToScreenPoint(new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y, (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            public void SetCanvasVisible(bool visible) => _canvasRoot.SetActive(visible);

            // Screen rects (pixels, origin top-left) for the paintover: UI panels and every item view.
            public string LayoutJson(int width, int height)
            {
                var camera = _scene.Camera;
                var json = new StringBuilder("{\n");
                string R(UnityEngine.Rect r) => string.Format(CultureInfo.InvariantCulture, "[{0:F0},{1:F0},{2:F0},{3:F0}]",
                    r.xMin, height - r.yMax, r.xMax, height - r.yMin);
                UnityEngine.Rect Ui(RectTransform rect)
                {
                    var c = new Vector3[4];
                    rect.GetWorldCorners(c);
                    var a = camera.WorldToScreenPoint(c[0]);
                    var b = camera.WorldToScreenPoint(c[2]);
                    return UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                }
                json.AppendLine("  \"header\": " + R(Ui((RectTransform)_safe.Find("Header"))) + ",");
                json.AppendLine("  \"note\": " + R(Ui(_note)) + ",");
                json.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"noteScale\": {0:F3},", _note.localScale.x));
                json.AppendLine("  \"dock\": " + R(Ui(_dock)) + ",");
                json.AppendLine("  \"action\": " + R(Ui(_action)) + ",");
                json.AppendLine("  \"safe\": " + R(UnityEngine.Rect.MinMaxRect(_safe.anchorMin.x * width, _safe.anchorMin.y * height,
                    _safe.anchorMax.x * width, _safe.anchorMax.y * height)) + ",");
                json.AppendLine("  \"lid\": " + R(LidScreenRect(camera)) + ",");
                var frame = _scene.Board.CompartmentFrames()[0];
                var o = camera.WorldToScreenPoint(frame.Origin);
                var e = camera.WorldToScreenPoint(frame.Origin + new Vector3(frame.Width, 0f, -frame.Height));
                json.AppendLine("  \"bed\": " + R(UnityEngine.Rect.MinMaxRect(o.x, e.y, e.x, o.y)) + ",");
                json.AppendLine("  \"items\": {");
                var views = _scene.Board.ItemViews.Values.Concat(_scene.Tray.ItemViews.Values).ToList();
                for (var i = 0; i < views.Count; i++)
                {
                    var r = ScreenRect(views[i], camera);
                    json.AppendLine(string.Format(CultureInfo.InvariantCulture, "    \"{0}\": {{\"rect\": {1}, \"tray\": {2}, \"rotation\": {3}}}{4}",
                        views[i].InstanceId, R(r), views[i].IsOnBoard ? "false" : "true", (int)views[i].Rotation,
                        i < views.Count - 1 ? "," : ""));
                }
                json.AppendLine("  }\n}");
                return json.ToString();
            }

            private static UnityEngine.Rect ScreenRect(PuzzleItemView view, Camera camera)
            {
                var min = new Vector2(float.MaxValue, float.MaxValue);
                var max = new Vector2(float.MinValue, float.MinValue);
                foreach (var renderer in view.VisualRoot.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.GetComponent<TextMesh>() != null)
                        continue;
                    var b = renderer.bounds;
                    for (var i = 0; i < 8; i++)
                    {
                        var p = camera.WorldToScreenPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                            (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                }
                return UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            /// <summary>Screen fractions of the main regions and the largest band with no shell, suitcase or tray.</summary>
            public string Describe(int width, int height)
            {
                var camera = _scene.Camera;
                var body = _scene.Board.ContainerFootprint;
                var y = _scene.Board.ContainerBottomY;
                var front = camera.WorldToViewportPoint(new Vector3(body.center.x, y, body.yMin)).y;
                var lid = LidScreenRect(camera);
                var trayBottom = 1f;
                foreach (var view in _scene.Tray.ItemViews.Values)
                {
                    var depth = view.Footprint.OccupiedCells.Max(c => c.Y) + 1;
                    trayBottom = Mathf.Min(trayBottom, camera.WorldToViewportPoint(view.transform.position
                        + new Vector3(0f, 0f, -depth * _scene.Tray.Scale - 0.4f)).y);
                }
                if (_scene.Tray.ItemViews.Count == 0)
                    trayBottom = front;
                var corners = new Vector3[4];
                _dock.GetWorldCorners(corners);
                var dockTop = camera.WorldToViewportPoint(corners[1]).y;
                var left = camera.WorldToViewportPoint(new Vector3(body.xMin, y, body.center.y)).x;
                var right = camera.WorldToViewportPoint(new Vector3(body.xMax, y, body.center.y)).x;
                return string.Format(CultureInfo.InvariantCulture,
                    "suitcase width {0:P0}, suitcase body {1:P0} of height, lid top {2:F3}, front {3:F3}, tray bottom {4:F3}, dock top {5:F3}, gap tray->dock {6:P1}",
                    right - left, camera.WorldToViewportPoint(new Vector3(body.center.x, y, body.yMax)).y - front,
                    lid.yMax / camera.pixelHeight, front, trayBottom, dockTop, Mathf.Max(0f, trayBottom - dockTop));
            }

            private RectTransform Stretch(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
            {
                var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
                return rect;
            }

            private RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
            {
                var shadow = new GameObject(name + " Shadow", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                shadow.SetParent(parent, false);
                shadow.anchorMin = shadow.anchorMax = anchor;
                shadow.anchoredPosition = position + new Vector2(0f, -8f);
                shadow.sizeDelta = size + new Vector2(8f, 8f);
                var shadowImage = shadow.GetComponent<Image>();
                shadowImage.sprite = _rounded;
                shadowImage.type = Image.Type.Sliced;
                shadowImage.color = new Color(0.12f, 0.16f, 0.17f, 0.2f);
                var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                rect.anchorMin = rect.anchorMax = anchor;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                var image = rect.GetComponent<Image>();
                image.sprite = _rounded;
                image.type = Image.Type.Sliced;
                image.color = color;
                shadow.SetParent(rect, true);
                shadow.SetAsFirstSibling();
                return rect;
            }

            private void IconButton(Transform parent, string caption, Sprite icon, Vector2 anchor, Vector2 position)
            {
                Own(icon);
                Own(icon.texture);
                var button = Panel(parent, "Button " + caption, anchor, position, new Vector2(140f, 140f), PresentationKit.Hex(0xEFE6D6));
                var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(button, false);
                image.rectTransform.anchoredPosition = new Vector2(0f, 14f);
                image.rectTransform.sizeDelta = new Vector2(64f, 64f);
                image.sprite = icon;
                image.color = Ink;
                Label(button, caption, 24, Ink, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(140f, 30f), TextAnchor.MiddleCenter);
            }

            private Text Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 position, Vector2 box,
                TextAnchor alignment)
            {
                var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
                label.rectTransform.SetParent(parent, false);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor;
                label.rectTransform.anchoredPosition = position;
                label.rectTransform.sizeDelta = box;
                label.font = _font;
                label.fontSize = size;
                label.alignment = alignment;
                label.color = color;
                label.text = text;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                return label;
            }

            private T Own<T>(T asset) where T : Object
            {
                _owned.Add(asset);
                return asset;
            }

            public void Dispose()
            {
                _scene.Hud.SafeArea.gameObject.SetActive(true);
                _scene.Table.SetMatVisible(_scene.Tray.ItemViews.Count > 0);
                Object.Destroy(_canvasRoot);
                Object.Destroy(_cards.gameObject);
                foreach (var asset in _owned)
                    if (asset != null)
                        Object.Destroy(asset);
            }
        }

        // ------------------------------------------------------------------ proxy item art

        // Labelled flat proxies for the Golden Lv1 items without production art (ART-GATE-02B owns the real assets).
        // Sweater resolves to the approved golden prefab.
        internal sealed class GoldenProxyArt : System.IDisposable
        {
            private readonly Material _template = PresentationKit.TemplateOrFallback(null);
            private readonly Dictionary<string, GameObject> _visuals = new Dictionary<string, GameObject>();
            private readonly List<Object> _owned = new List<Object>();

            public GameObject Resolve(PuzzleItem item)
            {
                if (item.Definition.Id == "sweater")
                    return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Items/PF_Item_SweaterOpen.prefab");
                if (_visuals.TryGetValue(item.Definition.Id, out var cached))
                    return cached;
                var root = new GameObject("Proxy " + item.Definition.Id);
                switch (item.Definition.Id)
                {
                    case "passport":
                        Part(root, new Rect(0.1f, -1.9f, 0.8f, 1.8f), 0.1f, 0f, 0.14f, 0x2E4A6B);
                        Part(root, new Rect(0.32f, -0.95f, 0.36f, 0.36f), 0.17f, 0.14f, 0.15f, 0xD7AA58);
                        LabelOn(root, "PASAPORT", new Vector3(0.5f, 0.16f, -1.5f), 0.045f, 90f, Color.white);
                        break;
                    case "towel":
                        Part(root, new Rect(0.06f, -3.94f, 0.88f, 3.88f), 0.42f, 0f, 0.46f, 0xF4EFE6);
                        foreach (var z in new[] { -0.6f, -3.6f })
                            Part(root, new Rect(0.06f, z, 0.88f, 0.22f), 0.08f, 0.46f, 0.47f, 0x4E9FA2);
                        LabelOn(root, "HAVLU", new Vector3(0.5f, 0.48f, -2f), 0.08f, 90f, Ink);
                        break;
                    case "shampoo":
                        Part(root, new Rect(0.14f, -2.86f, 0.72f, 2.1f), 0.3f, 0f, 0.5f, 0xE07A5F);
                        Part(root, new Rect(0.3f, -0.76f, 0.4f, 0.62f), 0.16f, 0f, 0.38f, 0x31515D);
                        Part(root, new Rect(0.2f, -2.3f, 0.6f, 0.9f), 0.1f, 0.5f, 0.51f, 0xF7F1E6);
                        LabelOn(root, "ŞAMPUAN", new Vector3(0.5f, 0.53f, -1.85f), 0.045f, 90f, Ink);
                        break;
                    case "sunglasses":
                        Part(root, new Rect(0.1f, -0.88f, 1.8f, 0.76f), 0.3f, 0f, 0.06f, 0x8A5A3C);
                        Part(root, new Rect(0.16f, -0.82f, 0.78f, 0.64f), 0.3f, 0.06f, 0.14f, 0x22262B);
                        Part(root, new Rect(1.06f, -0.82f, 0.78f, 0.64f), 0.3f, 0.06f, 0.14f, 0x22262B);
                        LabelOn(root, "GÖZLÜK", new Vector3(1f, 0.16f, -0.5f), 0.06f, 0f, Color.white);
                        break;
                    case "travel-pouch":
                        Part(root, new Rect(0.08f, -2.92f, 1.84f, 2.84f), 0.4f, 0f, 0.44f, 0xD7AA58);
                        Part(root, new Rect(0.24f, -0.5f, 1.52f, 0.1f), 0.04f, 0.44f, 0.46f, 0x31515D);
                        Part(root, new Rect(1.52f, -0.62f, 0.16f, 0.3f), 0.06f, 0.44f, 0.48f, 0xC36F58);
                        LabelOn(root, "ÇANTA", new Vector3(1f, 0.47f, -1.7f), 0.08f, 0f, Ink);
                        break;
                    default:
                        Object.Destroy(root);
                        return null;
                }
                root.hideFlags = HideFlags.HideAndDontSave;
                root.SetActive(false);
                _visuals.Add(item.Definition.Id, root);
                return root;
            }

            private void Part(GameObject root, Rect rect, float radius, float bottom, float top, int rgb)
            {
                var material = Own(PresentationKit.Matte(_template, PresentationKit.Hex(rgb), null, 0.15f));
                var mesh = Own(PresentationKit.Slab(rect, radius, bottom, top));
                var part = PresentationKit.MeshObject("Part", root.transform, mesh, material);
                part.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            private static void LabelOn(GameObject root, string text, Vector3 position, float size, float yaw, Color color)
            {
                var label = new GameObject("Proxy Label", typeof(TextMesh));
                label.transform.SetParent(root.transform, false);
                label.transform.localPosition = position;
                label.transform.localRotation = Quaternion.Euler(90f, yaw, 0f);
                var mesh = label.GetComponent<TextMesh>();
                mesh.text = text;
                mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
                mesh.fontSize = 48;
                mesh.characterSize = size;
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.color = color;
            }

            private T Own<T>(T asset) where T : Object
            {
                _owned.Add(asset);
                return asset;
            }

            public void Dispose()
            {
                foreach (var visual in _visuals.Values)
                    if (visual != null)
                        Object.Destroy(visual);
                foreach (var asset in _owned)
                    if (asset != null)
                        Object.Destroy(asset);
            }
        }
    }
}
