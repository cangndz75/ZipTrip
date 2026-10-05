using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class ArtGate02BTests
    {
        private const string ScenePath = "Assets/Scenes/PuzzleGameplay.unity";
        private static readonly string Folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
            "../Builds/art-gate-02b/gameplay"));

        [UnityTest]
        public IEnumerator FiveProductionPrefabs_MapAndStayWithinCanonicalBounds()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var catalog = UnityEngine.Object.FindFirstObjectByType<GoldenItemPrefabCatalog>();
            Assert.That(catalog, Is.Not.Null);
            foreach (var item in new[]
            {
                ("passport", "Passport", 1, 2), ("towel", "Towel", 1, 4),
                ("shampoo", "Shampoo", 1, 3), ("sunglasses", "Sunglasses", 2, 1),
                ("travel-pouch", "TravelPouch", 2, 3)
            })
            {
                var prefab = PuzzleItemCatalog.ResolveGolden(catalog, item.Item1, "open");
                Assert.That(prefab.name, Is.EqualTo("PF_Item_" + item.Item2));
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.EqualTo(1));
                Assert.That(renderers[0].sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                var mesh = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
                var texture = (Texture2D)renderers[0].sharedMaterial.GetTexture("_BaseMap");
                var triangles = Enumerable.Range(0, mesh.subMeshCount).Sum(i => (long)mesh.GetIndexCount(i)) / 3;
                Debug.Log("[ART-GATE-02B] " + item.Item2 + " mesh tris=" + triangles +
                    " verts=" + mesh.vertexCount + " materials=1 textures=1 " + texture.width + "x" +
                    texture.height + " shader=" + renderers[0].sharedMaterial.shader.name + " renderers=1");
                var bounds = renderers[0].bounds;
                Debug.Log("[ART-GATE-02B] " + item.Item2 + " prefab bounds " + bounds.min + ".." + bounds.max);
                Assert.That(bounds.size.x, Is.LessThanOrEqualTo(item.Item3 + .03f), item.Item2);
                Assert.That(bounds.size.z, Is.LessThanOrEqualTo(item.Item4 + .03f), item.Item2);
                Assert.That(bounds.size.y, Is.LessThan(.55f), item.Item2 + " lid clearance");
            }
        }

        [UnityTest]
        public IEnumerator SourceTray_DragKeepsRealArtAndCanonicalState()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = UnityEngine.Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.LoadLevel(GoldenLv1LayoutReview.LoadShipped(), PuzzleRuleText.LevelTitle(1));
            var hash = scene.Session.CurrentState.Hash;
            foreach (var id in new[] { "shampoo-1", "sunglasses-1", "travel-pouch-1" })
            {
                var view = scene.Tray.ItemViews[id];
                var renderer = view.VisualRoot.GetComponentInChildren<MeshRenderer>();
                var material = renderer.sharedMaterial;
                var texture = material.GetTexture("_BaseMap");
                Assert.That(scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(view)),
                    Is.EqualTo(DragBeginResult.Started));
                scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                    + new Vector3(3.5f, 0f, -3.5f));
                Assert.That(renderer.sharedMaterial, Is.SameAs(material), id);
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.SameAs(texture), id);
                scene.Drag.Cancel();
                Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(hash), id);
            }
        }

        [UnityTest, Explicit("Writes ART-GATE-02B real gameplay captures")]
        public IEnumerator CaptureFinalGoldenLv1()
        {
            var quality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(Array.IndexOf(QualitySettings.names, "Mobile"), true);
            Directory.CreateDirectory(Folder);
            RenderTexture target = null;
            try
            {
                yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var scene = UnityEngine.Object.FindFirstObjectByType<PuzzleGameplayScene>();
                scene.LoadLevel(GoldenLv1LayoutReview.LoadShipped(), PuzzleRuleText.LevelTitle(1));
                scene.Completion.AutoAdvance = false;
                target = new RenderTexture(1080, 2340, 24);
                scene.Camera.targetTexture = target;
                scene.Hud.RenderThrough(scene.Camera);
                scene.FrameCamera();
                yield return Shot(scene, target, "01-initial");
                yield return Shot(scene, target, "02-initial-clean");

                foreach (var selected in new[] { ("shampoo-1", "03-shampoo-selected"),
                                                  ("sunglasses-1", "04-sunglasses-selected"),
                                                  ("travel-pouch-1", "05-pouch-selected") })
                {
                    Select(scene, selected.Item1);
                    yield return Shot(scene, target, selected.Item2);
                    scene.Drag.CancelCandidate();
                }
                var shampoo = scene.Tray.ItemViews["shampoo-1"];
                Assert.That(scene.Drag.BeginDrag("shampoo-1", GoldenLv1LayoutReview.TrayGrab(shampoo)),
                    Is.EqualTo(DragBeginResult.Started));
                scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                    + new Vector3(3.5f, 0f, -3.5f));
                yield return Shot(scene, target, "06-shampoo-dragging");
                yield return Shot(scene, target, "07-valid-placement-cue");
                scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                    + new Vector3(-.5f, 0f, -.5f));
                yield return Shot(scene, target, "08-invalid-placement-cue");
                scene.Drag.Cancel();

                Place(scene, "travel-pouch-1", 1, 3);
                yield return Shot(scene, target, "09-partially-packed");
                Place(scene, "sunglasses-1", 3, 6);
                Place(scene, "shampoo-1", 3, 2);
                Assert.That(scene.Session.CurrentCompletion.IsComplete, Is.True);
                yield return Shot(scene, target, "10-final-pre-zip");
                CheckLidClearance(scene);
                scene.Completion.Advance(PuzzleCompletionPresenter.SettleDuration +
                    PuzzleCompletionPresenter.AnticipationDuration + PuzzleCompletionPresenter.RuleCascadeDuration
                    + PuzzleCompletionPresenter.StrapsDuration + PuzzleCompletionPresenter.LidDuration * .30f);
                yield return Shot(scene, target, "11-lid-30");
                scene.Completion.Advance(PuzzleCompletionPresenter.LidDuration * .40f);
                yield return Shot(scene, target, "12-lid-70");
                scene.Completion.Advance(5f);
                yield return Shot(scene, target, "13-lid-closed");
                yield return Shot(scene, target, "14-completion-ui");

                scene.Perform(PuzzleHudAction.Restart);
                yield return Shot(scene, target, "15-9x16", 1080, 1920);
                PuzzleHud.SafeAreaOverride = FixSlice00Tests.AndroidPunchHole;
                yield return Shot(scene, target, "16-android-cutout");
                PuzzleHud.SafeAreaOverride = FixSlice00Tests.IPhoneDynamicIsland;
                yield return Shot(scene, target, "17-iphone-notch");
                Debug.Log("[ART-GATE-02B] Captures: " + Folder);
            }
            finally
            {
                PuzzleHud.SafeAreaOverride = null;
                var scene = UnityEngine.Object.FindFirstObjectByType<PuzzleGameplayScene>();
                if (scene != null) scene.Camera.targetTexture = null;
                if (target != null) UnityEngine.Object.Destroy(target);
                QualitySettings.SetQualityLevel(quality, true);
            }
        }

        private static void Select(PuzzleGameplayScene scene, string id)
        {
            var view = scene.Tray.ItemViews[id];
            Assert.That(scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(view)),
                Is.EqualTo(DragBeginResult.Started));
            scene.Drag.Cancel();
        }

        private static void Place(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Tray.ItemViews[id];
            var first = view.Footprint.OccupiedCells[0];
            Assert.That(scene.Drag.BeginDrag(id, GoldenLv1LayoutReview.TrayGrab(view)),
                Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(scene.Board.Compartments["main"].transform.position
                + new Vector3(x + first.X + .5f, 0f, -(y + first.Y + .5f)));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True, id);
        }

        private static IEnumerator Shot(PuzzleGameplayScene scene, RenderTexture target, string name,
            int width = 1080, int height = 2340)
        {
            var camera = scene.Camera;
            RenderTexture custom = null;
            if (width != 1080 || height != 2340)
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

        private static void CheckLidClearance(PuzzleGameplayScene scene)
        {
            var rig = scene.Board.Container;
            var added = new List<MeshCollider>();
            var lidColliders = new HashSet<Collider>();
            var itemColliders = new Dictionary<Collider, string>();
            var report = new StringBuilder();
            foreach (var filter in rig.Lid.GetComponentsInChildren<MeshFilter>())
                lidColliders.Add(Add(filter));
            foreach (var view in scene.Board.ItemViews.Values)
                foreach (var filter in view.VisualRoot.GetComponentsInChildren<MeshFilter>())
                    itemColliders.Add(Add(filter), view.InstanceId);
            var backfaces = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            try
            {
                foreach (var fraction in new[] { .3f, .7f, 1f })
                {
                    var ease = fraction * fraction * (3f - 2f * fraction);
                    rig.Lid.localRotation = Quaternion.Slerp(rig.LidOpenLocalRotation,
                        ContainerRig.LidClosedLocalRotation, ease);
                    Physics.SyncTransforms();
                    foreach (var view in scene.Board.ItemViews.Values)
                    {
                        var renderers = view.VisualRoot.GetComponentsInChildren<Renderer>();
                        var bounds = renderers[0].bounds;
                        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                        var worst = float.PositiveInfinity;
                        var samples = 0;
                        for (var x = bounds.min.x; x <= bounds.max.x; x += .10f)
                            for (var z = bounds.min.z; z <= bounds.max.z; z += .10f)
                            {
                                var itemTop = float.NegativeInfinity;
                                var lidLow = float.PositiveInfinity;
                                foreach (var hit in Physics.RaycastAll(new Vector3(x, 60f, z), Vector3.down, 120f))
                                {
                                    if (itemColliders.TryGetValue(hit.collider, out var owner) && owner == view.InstanceId)
                                        itemTop = Mathf.Max(itemTop, hit.point.y);
                                    if (lidColliders.Contains(hit.collider))
                                        lidLow = Mathf.Min(lidLow, hit.point.y);
                                }
                                if (float.IsInfinity(itemTop) || float.IsInfinity(lidLow)) continue;
                                worst = Mathf.Min(worst, lidLow - itemTop);
                                samples++;
                            }
                        report.AppendLine(view.InstanceId + " lid=" + fraction + " samples=" + samples +
                            " clearance=" + (samples == 0 ? "uncovered" : worst.ToString("F3")));
                        if (samples > 0)
                            Assert.That(worst, Is.GreaterThanOrEqualTo(-.01f), view.InstanceId + " lid penetration at " + fraction);
                    }
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = backfaces;
                foreach (var collider in added) UnityEngine.Object.Destroy(collider);
                rig.SetLidClosed(false);
            }
            File.WriteAllText(Path.Combine(Folder, "lid-clearance.txt"), report.ToString());

            MeshCollider Add(MeshFilter filter)
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                added.Add(collider);
                return collider;
            }
        }
    }
}
