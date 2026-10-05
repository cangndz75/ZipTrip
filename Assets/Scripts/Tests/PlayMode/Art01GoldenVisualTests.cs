using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Profiling;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class Art01GoldenVisualTests
    {
        private const string ScenePath = "Assets/Scenes/PuzzleGameplay.unity";
        private const string PrefabFolder = "Assets/Art/Prefabs/Items/Art01/";
        private const float Margin = 0.05f; // ART-01 shared normalization margin (Art01AssetBuilder.Margin)
        // Screen-space cover remains possible because raised art projects beyond its floor footprint at 65 degrees.
        // The accepted ART-01 frame measures 0.9%; allow 1% with orthographic camera rays (ADR-0010).
        private const float ApprovedParallaxCover = .01f;

        [UnityTest]
        public IEnumerator SixShippedItems_ResolveToNormalizedArt01Prefabs_WithoutChangingState()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var catalog = Object.FindFirstObjectByType<GoldenItemPrefabCatalog>();
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"));
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(scene.Level.InitialState.Hash));
            foreach (var item in new[]
            {
                ("sweater", "SweaterOpen", 3, 3, 10000), ("passport", "Passport", 1, 2, 5000),
                ("towel", "Towel", 1, 4, 5000), ("shampoo", "Shampoo", 1, 3, 5000),
                ("sunglasses", "Sunglasses", 2, 1, 10000), ("travel-pouch", "TravelPouch", 2, 3, 10000)
            })
            {
                var prefab = PuzzleItemCatalog.ResolveGolden(catalog, item.Item1, "open");
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(PrefabFolder + "PF_Item_" + item.Item2 + ".prefab"));
                Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                var renderer = prefab.GetComponentsInChildren<MeshRenderer>(true).Single();
                var mesh = prefab.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                var triangles = Enumerable.Range(0, mesh.subMeshCount).Sum(i => (long)mesh.GetIndexCount(i)) / 3;
                Debug.Log("[ART-01] " + item.Item2 + " runtime triangles=" + triangles);
                Assert.That(triangles, Is.LessThanOrEqualTo(item.Item5), item.Item2 + " ADR-0009 mesh budget");
                var bounds = renderer.bounds;
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(Margin - .002f), item.Item2);
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(item.Item3 - Margin + .002f), item.Item2);
                Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(-item.Item4 + Margin - .002f), item.Item2);
                Assert.That(bounds.max.z, Is.LessThanOrEqualTo(-Margin + .002f), item.Item2);
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(-.002f), item.Item2);
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(.502f), item.Item2);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                foreach (var textureName in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" })
                {
                    var texture = renderer.sharedMaterial.GetTexture(textureName) as Texture2D;
                    Assert.That(texture, Is.Not.Null, item.Item2 + " " + textureName);
                    Assert.That(texture.width, Is.EqualTo(1024), item.Item2 + " " + textureName);
                }
            }
            Assert.That(AssetDatabase.GetAssetPath(catalog.Resolve("PF_Item_Sweater", "folded")),
                Is.EqualTo("Assets/Art/Prefabs/Items/PF_Item_SweaterFolded.prefab"));
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(scene.Level.InitialState.Hash));
        }

        [UnityTest]
        public IEnumerator ShippedTrayItems_ClearBackdropPropsAtBothPhoneAspects()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var overlaps = new System.Collections.Generic.List<string>();
            foreach (var (width, height) in new[] { (1080, 2340), (1080, 1920) })
            {
                var target = new RenderTexture(width, height, 24);
                scene.Camera.targetTexture = target;
                scene.FrameCamera();
                yield return null;
                foreach (var prop in scene.Table.PropRenderers)
                    foreach (var view in scene.Tray.ItemViews.Values)
                    {
                        var item = view.VisualRoot.GetComponentInChildren<MeshRenderer>();
                        var propRect = ScreenRect(scene.Camera, prop.bounds);
                        var itemRect = ScreenRect(scene.Camera, item.bounds);
                        if (itemRect.Overlaps(propRect))
                            overlaps.Add(prop.name + " / " + view.InstanceId + " at " + width + "x" + height);
                    }
                scene.Camera.targetTexture = null;
                Object.Destroy(target);
            }
            Assert.That(overlaps, Is.Empty, string.Join(", ", overlaps));
        }

        [UnityTest]
        public IEnumerator SolvedLv1Items_DoNotReadOverNeighbouringFootprints_FromGameplayCamera()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.Completion.AutoAdvance = false;
            var initial = scene.Session.CurrentState.Hash;
            var overlaps = new System.Collections.Generic.List<string>();
            CheckNeighbours(scene, overlaps, "start");
            Place(scene, "travel-pouch-1", 1, 3);
            Place(scene, "sunglasses-1", 3, 6);
            Place(scene, "shampoo-1", 3, 2);
            Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.True);
            Assert.That(scene.Session.CurrentState.Hash, Is.Not.EqualTo(initial));
            yield return new WaitForSeconds(1f); // let FEEL-01 settle motion finish before sampling rest poses
            foreach (var (width, height) in new[] { (1080, 2340), (1080, 1920) })
            {
                var target = new RenderTexture(width, height, 24);
                scene.Camera.targetTexture = target;
                scene.FrameCamera();
                yield return null;
                CheckNeighbours(scene, overlaps, width + "x" + height);
                scene.Camera.targetTexture = null;
                Object.Destroy(target);
            }
            Assert.That(overlaps, Is.Empty, string.Join(", ", overlaps));
        }

        // Samples the floor of every placed footprint and casts the camera ray to it; the first item mesh it meets
        // should be that footprint's own item (or none). Cover by a neighbour is bounded by ApprovedParallaxCover.
        private static void CheckNeighbours(PuzzleGameplayScene scene, System.Collections.Generic.List<string> overlaps,
            string label)
        {
            var owners = new System.Collections.Generic.Dictionary<Collider, string>();
            foreach (var view in scene.Board.ItemViews.Values)
                foreach (var filter in view.VisualRoot.GetComponentsInChildren<MeshFilter>())
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = filter.sharedMesh;
                    owners.Add(collider, view.InstanceId);
                }
            Physics.SyncTransforms();
            var backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
            foreach (var view in scene.Board.ItemViews.Values)
            {
                    var covered = new System.Collections.Generic.Dictionary<string, int>();
                    var samples = 0;
                    foreach (var cell in view.Footprint.OccupiedCells)
                        for (var u = .05f; u < 1f; u += .1f)
                            for (var v = .05f; v < 1f; v += .1f)
                            {
                                var floor = view.transform.TransformPoint(new Vector3(cell.X + u, 0f, -(cell.Y + v)));
                                samples++;
                                var screen = scene.Camera.WorldToScreenPoint(floor);
                                var ray = scene.Camera.ScreenPointToRay(screen);
                                var floorDistance = Vector3.Dot(floor - ray.origin, ray.direction);
                                var nearest = float.PositiveInfinity;
                                string owner = null;
                                foreach (var hit in Physics.RaycastAll(ray, floorDistance))
                                    if (owners.TryGetValue(hit.collider, out var id) && hit.distance < nearest)
                                    {
                                        nearest = hit.distance;
                                        owner = id;
                                    }
                                if (owner != null && owner != view.InstanceId)
                                    covered[owner] = covered.TryGetValue(owner, out var n) ? n + 1 : 1;
                            }
                    foreach (var pair in covered)
                    {
                        var cover = pair.Value / (float)samples;
                        var line = pair.Key + " over " + view.InstanceId + " " + cover.ToString("P1") + " (" + label + ")";
                        Debug.Log("[ART-01 PARALLAX] " + line);
                        if (cover > ApprovedParallaxCover)
                            overlaps.Add(line);
                    }
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = backfaces;
                foreach (var collider in owners.Keys) Object.DestroyImmediate(collider);
            }
        }

        private static Rect ScreenRect(Camera camera, Bounds bounds)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (var i = 0; i < 8; i++)
            {
                var point = camera.WorldToScreenPoint(new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest, Explicit("Writes ART-01 shipped Lv1 review captures")]
        public IEnumerator CaptureShippedLv1WithArt01()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            Assert.That(scene.Level.Id, Is.EqualTo("lv1-fit"));
            scene.Hud.RenderThrough(scene.Camera);
            scene.Completion.AutoAdvance = false;
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Builds/art01-captures"));
            Directory.CreateDirectory(folder);
            var catalog = Object.FindFirstObjectByType<GoldenItemPrefabCatalog>();
            var laptop = catalog.Resolve("PF_Item_Laptop", "open");
            var sneaker = catalog.Resolve("PF_Item_SneakerPair", "open");
            var folded = catalog.Resolve("PF_Item_Sweater", "folded");
            var newPrefabs = new[] { "SweaterOpen", "Passport", "Towel", "Shampoo", "Sunglasses", "TravelPouch" }
                .Select(name => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "PF_Item_" + name + ".prefab"))
                .ToArray();
            GameObject Old(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Prefabs/Items/PF_Item_" + name + ".prefab");
            var initialHash = scene.Session.CurrentState.Hash;
            catalog.Configure(laptop, sneaker, Old("SweaterOpen"), folded);
            catalog.ConfigureGoldenLv1Final(Old("Passport"), Old("Towel"), Old("Shampoo"),
                Old("Sunglasses"), Old("TravelPouch"));
            scene.LoadLevel(0);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(initialHash));
            var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 4);
            var beforeCounter = drawCalls.LastValue;
            yield return Capture(scene, folder, "comparison-shipped-old-items", 1080, 2340);
            var oldCounter = drawCalls.LastValue;
            catalog.Configure(laptop, sneaker, newPrefabs[0], folded);
            catalog.ConfigureGoldenLv1Final(newPrefabs[1], newPrefabs[2], newPrefabs[3], newPrefabs[4], newPrefabs[5]);
            scene.LoadLevel(0);
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(initialHash));
            yield return Capture(scene, folder, "01-level-start", 1080, 2340);
            var newCounter = drawCalls.LastValue;
            Debug.Log("[ART-01] Lv1 draw calls per manual render before=" + (oldCounter - beforeCounter)
                + " after=" + (newCounter - oldCounter) + " recorderValid=" + drawCalls.Valid);
            drawCalls.Dispose();
            Select(scene, "travel-pouch-1");
            yield return new WaitForSecondsRealtime(.25f);
            yield return Capture(scene, folder, "02-selected-staging-item", 1080, 2340);
            var pouch = scene.Tray.ItemViews["travel-pouch-1"];
            Assert.That(scene.Drag.BeginDrag("travel-pouch-1", GoldenLv1LayoutReview.TrayGrab(pouch)),
                Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                + new Vector3(2.5f, 0f, -4.5f));
            yield return Capture(scene, folder, "03-mid-drag", 1080, 2340);
            scene.Drag.Cancel();
            Place(scene, "sunglasses-1", 3, 5);
            Place(scene, "travel-pouch-1", 3, 2);
            yield return Capture(scene, folder, "04-one-item-remaining", 1080, 2340);
            Place(scene, "shampoo-1", 1, 3);
            Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.False,
                "pre-completion capture keeps the live rule invalid");
            yield return Capture(scene, folder, "05-pre-completion", 1080, 2340);
            yield return Capture(scene, folder, "closeup-source", 1080, 2340);
            WriteRegions(scene, Path.Combine(folder, "closeup-regions.txt"), 1080, 2340);
            scene.Undo();
            Move(scene, "travel-pouch-1", 1, 3);
            Move(scene, "sunglasses-1", 3, 6);
            Place(scene, "shampoo-1", 3, 2);
            Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.True);
            scene.Completion.Advance(PuzzleCompletionPresenter.FullDuration);
            yield return Capture(scene, folder, "06-zip02-final", 1080, 2340);
            Debug.Log("[ART-01] Captures " + folder);
        }

        private static void Select(PuzzleGameplayScene scene, string id)
        {
            Assert.That(scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(scene.Tray.ItemViews[id])),
                Is.EqualTo(DragBeginResult.Started), id);
            scene.Drag.Cancel();
        }

        private static void Move(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Board.ItemViews[id];
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, view.transform.TransformPoint(
                new Vector3(first.X + .5f, 0f, -(first.Y + .5f)))), Is.EqualTo(DragBeginResult.Started), id);
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                + new Vector3(x + first.X + .5f, 0f, -(y + first.Y + .5f)));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id);
        }

        private static void Place(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Tray.ItemViews[id];
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(view)),
                Is.EqualTo(DragBeginResult.Started), id);
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                + new Vector3(x + first.X + .5f, 0f, -(y + first.Y + .5f)));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id);
        }

        // Screen rects (top-left origin) of the six ART-01 items for the close-up sheet.
        private static void WriteRegions(PuzzleGameplayScene scene, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            scene.Camera.targetTexture = target;
            scene.FrameCamera();
            var lines = new System.Collections.Generic.List<string>();
            foreach (var (id, label) in new[] { ("sweater-1", "Sweater"), ("passport-1", "Passport"), ("towel-1", "Towel"),
                         ("shampoo-1", "Shampoo"), ("sunglasses-1", "Sunglasses"), ("travel-pouch-1", "Travel Pouch") })
            {
                var view = scene.Board.ItemViews.TryGetValue(id, out var board) ? board : scene.Tray.ItemViews[id];
                var rect = ScreenRect(scene.Camera, view.VisualRoot.GetComponentInChildren<MeshRenderer>().bounds);
                lines.Add(string.Join("\t", label, Mathf.FloorToInt(rect.xMin), Mathf.FloorToInt(height - rect.yMax),
                    Mathf.CeilToInt(rect.xMax), Mathf.CeilToInt(height - rect.yMin)));
            }
            File.WriteAllLines(path, lines);
            scene.Camera.targetTexture = null;
            Object.Destroy(target);
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
    }
}
