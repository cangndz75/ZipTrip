using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ZipTrip.Unity;
using Object = UnityEngine.Object;

namespace ZipTrip.Tests.PlayMode
{
    public sealed class Art03FamilyReview
    {
        private static readonly string[] Names = { "SweaterOpen", "Passport", "Shampoo", "Towel", "Sunglasses", "TravelPouch" };
        private RenderTexture _target;
        private string _folder;

        [UnityTest, Explicit("ZT-ART-03 actual Unity family inspection and gameplay captures")]
        public IEnumerator Capture()
        {
            _folder = Path.GetFullPath(Environment.GetEnvironmentVariable("ZIPTRIP_ART03_CAPTURE") ?? "Builds/art03/new");
            Directory.CreateDirectory(_folder);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            scene.Completion.AutoAdvance = false;
            _target = new RenderTexture(1080, 2340, 24);
            scene.Camera.targetTexture = _target;
            scene.Hud.RenderThrough(scene.Camera);
            scene.FrameCamera();
            yield return null;
            Shot(scene.Camera, "level-start");
            Place(scene, "travel-pouch-1", 1, 3);
            Place(scene, "sunglasses-1", 3, 6);
            Place(scene, "shampoo-1", 3, 2);
            foreach (var view in scene.Board.ItemViews.Values) view.Feedback.CompleteAll();
            yield return null;
            Shot(scene.Camera, "all-placed");
            scene.Hud.RenderThrough(null);
            scene.Camera.targetTexture = null;
            Object.Destroy(_target);

            // Inspection-only layout: no saved scene, catalog or gameplay state edits.
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) r.enabled = false;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.50f, .48f, .45f);
            var key = new GameObject("Art03 inspection key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, .95f, .87f);
            key.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var camera = scene.Camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.24f, .25f, .25f);
            camera.orthographic = true;
            camera.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
            var items = new GameObject[Names.Length];
            var audit = new System.Text.StringBuilder();
            for (var i = 0; i < Names.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Items/Art01/PF_Item_" + Names[i] + ".prefab");
                items[i] = Object.Instantiate(prefab);
                var bounds = BoundsOf(items[i]);
                items[i].transform.position += new Vector3((i % 3 - 1) * 4.3f, 0f, i < 3 ? 2.7f : -2.7f) - bounds.center;
                var materials = items[i].GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Distinct().ToArray();
                var textures = materials.SelectMany(m => new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" }.Select(m.GetTexture)).Where(t => t != null).Distinct().ToArray();
                long bytes = textures.Sum(t => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t));
                long triangles = items[i].GetComponentsInChildren<MeshFilter>().Sum(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(s => (long)f.sharedMesh.GetIndexCount(s) / 3));
                audit.AppendLine($"{Names[i]}: bounds={bounds.size:F4}; materials={materials.Length}; textures={textures.Length}; textureBytes={bytes}; triangles={triangles}");
            }
            File.WriteAllText(Path.Combine(_folder, "asset-audit.txt"), audit.ToString());
            Frame(camera, Vector3.zero, 5.3f, 1800, 1200);
            yield return null;
            Shot(camera, "neutral-family");
            for (var i = 0; i < Names.Length; i++)
            {
                for (var j = 0; j < items.Length; j++) items[j].SetActive(i == j);
                Frame(camera, BoundsOf(items[i]).center, 2.45f, 800, 800);
                yield return null;
                Shot(camera, "closeup-" + Names[i]);
            }
            camera.targetTexture = null;
            Object.Destroy(_target);
        }

        private void Frame(Camera camera, Vector3 center, float size, int width, int height)
        {
            camera.targetTexture = null;
            if (_target != null) Object.Destroy(_target);
            _target = new RenderTexture(width, height, 24);
            camera.targetTexture = _target;
            camera.transform.position = center - camera.transform.forward * 20f;
            camera.orthographicSize = size;
            camera.ResetProjectionMatrix();
            var projection = camera.projectionMatrix;
            projection.m11 *= PuzzleCameraFraming.VerticalScale;
            camera.projectionMatrix = projection;
        }

        private void Shot(Camera camera, string name)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = _target;
            var image = new Texture2D(_target.width, _target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(image);
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void Place(PuzzleGameplayScene scene, string id, int x, int y)
        {
            var view = scene.Tray.ItemViews[id];
            Assert.That(scene.Drag.BeginDrag(id, view.transform.position + view.transform.lossyScale.x * new Vector3(.5f, 0f, -.5f)), Is.EqualTo(DragBeginResult.Started));
            scene.Drag.UpdateDrag(scene.Board.CompartmentFrames().Single().Origin + new Vector3(x + .5f, 0f, -(y + .5f)));
            Assert.That(scene.Drag.Drop().Move.IsAccepted, Is.True);
        }
    }
}
