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
    public sealed class Art04HeroReview
    {
        private static readonly string[] Names = { "TravelPouch", "SweaterOpen" };

        [UnityTest]
        public IEnumerator ThreeRebuiltPrefabs_KeepVisualContractAndRotationClearance()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            var initialHash = scene.Session.CurrentState.Hash;
            foreach (var (name, width, depth, height, canRotate) in new[]
            {
                ("TravelPouch", 1.9f, 2.58f, .5f, true),
                ("SweaterOpen", 2.9f, 2.64f, .5f, false),
                ("Passport", .9f, 1.72f, .2426f, true)
            })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Prefabs/Items/Art01/PF_Item_" + name + ".prefab");
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true).Length, Is.EqualTo(1));
                var filter = prefab.GetComponentInChildren<MeshFilter>(true);
                Assert.That(AssetDatabase.GetAssetPath(filter.sharedMesh), Does.Contain("/Art04/"));
                var original = BoundsOf(prefab);
                Assert.That(original.size.x, Is.EqualTo(width).Within(.003f), name);
                Assert.That(original.size.z, Is.EqualTo(depth).Within(.003f), name);
                Assert.That(original.size.y, Is.EqualTo(height).Within(.003f), name);
                if (canRotate)
                {
                    var rotated = Object.Instantiate(prefab);
                    rotated.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    var swapped = BoundsOf(rotated);
                    Assert.That(swapped.size.x, Is.EqualTo(depth).Within(.003f), name);
                    Assert.That(swapped.size.z, Is.EqualTo(width).Within(.003f), name);
                    Object.Destroy(rotated);
                }
            }
            Assert.That(scene.Session.CurrentState.Hash, Is.EqualTo(initialHash));
        }

        [UnityTest, Explicit("ART-04 actual Unity construction views")]
        public IEnumerator CaptureThreeAndThreeQuarterViews()
        {
            var folder = Path.GetFullPath(Environment.GetEnvironmentVariable("ZIPTRIP_ART04_CAPTURE")
                ?? "Builds/art04/inspection");
            Directory.CreateDirectory(folder);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scenes/PuzzleGameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = Object.FindFirstObjectByType<PuzzleGameplayScene>();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) renderer.enabled = false;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) light.enabled = false;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.5f, .48f, .45f);
            var key = new GameObject("ART04 inspection key").AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.15f;
            key.color = new Color(1f, .95f, .87f);
            key.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            var camera = scene.Camera;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.24f, .25f, .25f);
            camera.orthographic = true;
            camera.cullingMask = 1 << 30;
            var items = Names.Select((name, i) =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Prefabs/Items/Art01/PF_Item_" + name + ".prefab");
                var item = Object.Instantiate(prefab);
                item.transform.position += new Vector3((i - .5f) * 3.25f, 0f, 0f) - BoundsOf(item).center;
                SetLayer(item.transform, 30);
                return item;
            }).ToArray();
            yield return Shot(camera, folder, "both-neutral", Vector3.zero,
                new Vector3(65f, 0f, 0f), 3.3f, 1600, 900);
            yield return Shot(camera, folder, "both-three-quarter", Vector3.zero,
                new Vector3(48f, 28f, 0f), 3.8f, 1600, 900);
            for (var i = 0; i < items.Length; i++)
            {
                for (var j = 0; j < items.Length; j++) items[j].SetActive(i == j);
                yield return Shot(camera, folder, "side-" + Names[i], BoundsOf(items[i]).center,
                    new Vector3(42f, 38f, 0f), 1.65f, 800, 800);
            }
            camera.targetTexture = null;
        }

        private static IEnumerator Shot(Camera camera, string folder, string name, Vector3 center,
            Vector3 euler, float size, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            camera.transform.rotation = Quaternion.Euler(euler);
            camera.transform.position = center - camera.transform.forward * 20f;
            camera.orthographicSize = size;
            camera.ResetProjectionMatrix();
            var projection = camera.projectionMatrix;
            projection.m11 *= PuzzleCameraFraming.VerticalScale;
            camera.projectionMatrix = projection;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null;
            Object.Destroy(image);
            camera.targetTexture = null;
            Object.Destroy(target);
            yield break;
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
