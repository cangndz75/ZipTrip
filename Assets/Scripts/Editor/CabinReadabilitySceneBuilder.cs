using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZipTrip.Editor
{
    // ZA-004 static review fixture. No Domain state, input, or gameplay scripts.
    public static class CabinReadabilitySceneBuilder
    {
        private const string ModelPath = "Assets/Art/Models/Containers/Final/M_Container_Cabin.fbx";
        private const string ScenePath = "Assets/Scenes/ReadabilityTest.unity";
        private const string MaterialFolder = "Assets/Art/Materials/Containers";
        private const string PrefabFolder = "Assets/Art/Prefabs/Items";
        private const int Width = 6;
        private const int Height = 8;
        private static readonly Vector2Int[] Blocked =
        {
            new Vector2Int(0, 0), new Vector2Int(5, 0),
            new Vector2Int(0, 7), new Vector2Int(5, 7)
        };

        [MenuItem("ZipTrip/ZA-004/Rebuild Cabin ReadabilityTest")]
        public static void BuildAndCapture()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (importer == null || model == null)
                throw new InvalidOperationException("Cabin FBX must import as ModelImporter/GameObject.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.73f, 0.73f, 0.72f);

            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name = "Cabin suitcase — static model";
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var filters = root.GetComponentsInChildren<MeshFilter>();
            if (renderers.Length != 11 || filters.Length != 11)
                throw new InvalidOperationException("Cabin importer mesh count changed.");
            var importedSlots = renderers.Sum(renderer => renderer.sharedMaterials.Length);
            var triangles = filters.Sum(filter => filter.sharedMesh.triangles.Length / 3);
            if (triangles != 5520)
                throw new InvalidOperationException("Cabin triangle count changed: " + triangles);

            var shell = GetMaterial("Cabin_DeepBlueGreen", "31515D");
            var lining = GetMaterial("Cabin_WarmCream", "E9E5DD");
            var accent = GetMaterial("Cabin_Terracotta", "C36F58");
            var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(
                MaterialFolder + "/Cabin_LiningGrain.png");
            if (grain == null)
                throw new InvalidOperationException("Cabin lining grain texture missing.");
            lining.SetColor("_BaseColor", Color.white);
            lining.SetTexture("_BaseMap", grain);
            EditorUtility.SetDirty(lining);
            foreach (var renderer in renderers)
            {
                var name = renderer.name;
                renderer.sharedMaterial = name.Contains("FlatLining") ? lining :
                    name.Contains("ZipperLine") ? accent : shell;
            }

            var gridMaterial = GetMaterial("Cabin_GridOverlay", "4E9FA2", true);
            var edgeMaterial = GetMaterial("Cabin_EdgeReview", "D7AA58", true);
            var cornerMaterial = GetMaterial("Cabin_CornerReview", "C36F58", true);
            var grid = new GameObject("Grid overlay — toggle in Hierarchy");
            var edges = new GameObject("Edge-cell review — toggle in Hierarchy");
            var corners = new GameObject("Blocked-corner review — toggle in Hierarchy");
            for (var row = 0; row < Height; row++)
            for (var column = 0; column < Width; column++)
            {
                if (IsBlocked(column, row))
                {
                    AddOutline(corners.transform, column, row, 0.205f,
                        0.025f, cornerMaterial);
                    continue;
                }
                AddOutline(grid.transform, column, row, 0.009f,
                    0.012f, gridMaterial);
                if (column == 0 || column == Width - 1 || row == 0 || row == Height - 1)
                    AddOutline(edges.transform, column, row, 0.014f,
                        0.022f, edgeMaterial);
            }

            var items = new GameObject("Accepted item prefabs — static scale review");
            AddItem(items.transform, "M_Item_Laptop.fbx", "Laptop", "PF_Item_Laptop", 0, 1,
                "Laptop/Textures/M_Item_Laptop_TripoRetopo_8k_basecolor.jpg", "D7AA58");
            AddItem(items.transform, "M_Item_SweaterOpen.fbx", "Sweater Open", "PF_Item_SweaterOpen", 3, 1,
                "SweaterOpen/Textures/M_Item_SweaterOpen_MeshyRetopo_8k_basecolor.jpg", "D7AA58");
            AddItem(items.transform, "M_Item_SweaterFolded.fbx", "Sweater Folded", "PF_Item_SweaterFolded", 3, 4,
                "SweaterFolded/Textures/SweaterFolded_LongFront.jpeg", "D7AA58");
            AddItem(items.transform, "M_Item_SneakerPair.fbx", "Sneaker Pair", "PF_Item_SneakerPair", 1, 5,
                "SneakerPair/Textures/M_Item_SneakerPair_TripoRetopo_10k_basecolor.jpg", "4E9FA2");

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            cameraObject.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
            var center = new Vector3(3f, 0f, -4f);
            cameraObject.transform.position = center - cameraObject.transform.forward * 12f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 30f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.914f, 0.898f, 0.867f);
            // Static ZA-004 review framing: exterior shell is wider, with a
            // narrow but visible portrait margin; gameplay camera pitch is unchanged.
            camera.orthographicSize = 6.74f;
            cameraObject.AddComponent<ZipTrip.Unity.ReadabilityPortraitFraming>();

            var lightObject = new GameObject("Soft upper-left key");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            var floorRenderer = renderers.FirstOrDefault(renderer =>
                renderer.name.Contains("FlatLining"));
            if (floorRenderer == null)
                throw new InvalidOperationException("Cabin floor renderer missing.");
            AssertBounds(floorRenderer.bounds, new Vector3(0f, -0.12f, -8f),
                new Vector3(6f, 0f, 0f), "floor");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            AssertBounds(bounds, new Vector3(-0.60f, -0.38f, -8.60f),
                new Vector3(6.60f, 0.254f, 0.93f), "exterior");

            grid.SetActive(false);
            edges.SetActive(false);
            corners.SetActive(false);
            items.SetActive(true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            var output = Environment.GetEnvironmentVariable("ZIPTRIP_ZA004_RESULTS");
            if (string.IsNullOrEmpty(output))
                output = @"D:\Games\ZipTrip-ZA004-TestResults";
            Directory.CreateDirectory(output);
            items.SetActive(false);
            Capture(camera, Path.Combine(output, "cabin-polished-empty-75deg.png"), 540, 960);
            grid.SetActive(true);
            Capture(camera, Path.Combine(output, "cabin-polished-grid-75deg.png"), 540, 960);
            grid.SetActive(false);
            cameraObject.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            cameraObject.transform.position = center - cameraObject.transform.forward * 12f;
            Capture(camera, Path.Combine(output, "cabin-polished-45deg.png"), 540, 960);
            cameraObject.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
            cameraObject.transform.position = center - cameraObject.transform.forward * 12f;
            items.SetActive(true);
            Capture(camera, Path.Combine(output, "cabin-polished-golden-items-75deg.png"), 540, 960);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log("ZA004 Cabin ModelImporter PASS; pivot=(0,0,0); bounds=" +
                bounds.min.ToString("F6") + ".." + bounds.max.ToString("F6") +
                "; triangles=" + triangles + "; imported material slots=" + importedSlots +
                "; static scene materials=3; screenshots=" + output);
        }

        private static bool IsBlocked(int column, int row)
        {
            foreach (var cell in Blocked)
                if (cell.x == column && cell.y == row) return true;
            return false;
        }

        private static void AssertBounds(Bounds actual, Vector3 min, Vector3 max, string label)
        {
            const float tolerance = 0.002f;
            if ((actual.min - min).sqrMagnitude > tolerance * tolerance ||
                (actual.max - max).sqrMagnitude > tolerance * tolerance)
                throw new InvalidOperationException(label + " bounds changed: " + actual);
        }

        private static Material GetMaterial(string name, string hex, bool unlit = false)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Art/Materials", "Containers");
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" :
                "Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP shader missing");
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddOutline(Transform parent, int column, int row,
            float elevation, float width, Material material)
        {
            var child = new GameObject("Cell " + column + "," + row);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.positionCount = 5;
            line.startWidth = width;
            line.endWidth = width;
            line.SetPositions(new[]
            {
                new Vector3(column, elevation, -row),
                new Vector3(column + 1, elevation, -row),
                new Vector3(column + 1, elevation, -row - 1),
                new Vector3(column, elevation, -row - 1),
                new Vector3(column, elevation, -row)
            });
        }

        private static void AddItem(Transform parent, string filename, string name,
            string prefabName,
            int column, int row, string texturePath, string fallbackColor)
        {
            var path = "Assets/Art/Models/Items/Final/" + filename;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Missing accepted FBX: " + path);
            if (!AssetDatabase.IsValidFolder("Assets/Art/Prefabs"))
                AssetDatabase.CreateFolder("Assets/Art", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/Art/Prefabs", "Items");
            var prefabRoot = new GameObject(prefabName);
            var source = (GameObject)PrefabUtility.InstantiatePrefab(model);
            source.transform.SetParent(prefabRoot.transform, false);
            source.transform.localPosition = Vector3.zero;
            source.transform.localRotation = Quaternion.identity;
            source.transform.localScale = Vector3.one;
            var material = GetMaterial("Cabin_Static_" + name.Replace(" ", ""),
                fallbackColor);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/Materials/Items/" + texturePath);
            if (texture == null)
                throw new InvalidOperationException("Missing accepted texture: " + texturePath);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials = Enumerable.Repeat(material,
                    renderer.sharedMaterials.Length).ToArray();
            var prefabPath = PrefabFolder + "/" + prefabName + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            if (prefab == null)
                throw new InvalidOperationException("Could not save prefab: " + prefabPath);
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Prefab has a script or collider: " + prefabPath);
            if (prefab.transform.localScale != Vector3.one)
                throw new InvalidOperationException("Prefab root scale changed: " + prefabPath);
            var placement = new GameObject(name + " — static scale reference");
            placement.transform.SetParent(parent, false);
            placement.transform.localPosition = new Vector3(column, 0f, -row);
            placement.transform.localRotation = Quaternion.identity;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(placement.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            var itemRenderers = instance.GetComponentsInChildren<MeshRenderer>();
            if (itemRenderers.Length == 0)
                throw new InvalidOperationException("Accepted FBX has no renderer: " + name);
            var itemBounds = itemRenderers[0].bounds;
            foreach (var renderer in itemRenderers.Skip(1))
                itemBounds.Encapsulate(renderer.bounds);
            var prefabTriangles = instance.GetComponentsInChildren<MeshFilter>()
                .Sum(filter => filter.sharedMesh.triangles.Length / 3);
            Debug.Log("ZA004 Prefab " + prefabPath + " rootComponents=" +
                string.Join(",", prefab.GetComponents<Component>().Select(component =>
                    component.GetType().Name)) + " scripts=0 colliders=0 renderers=" +
                itemRenderers.Length + " meshes=" +
                instance.GetComponentsInChildren<MeshFilter>().Length + " triangles=" +
                prefabTriangles + " scale=" + instance.transform.localScale +
                " materials=" + itemRenderers.Sum(renderer => renderer.sharedMaterials.Length));
            Debug.Log("ZA004 StaticItem " + name + " renderers=" + itemRenderers.Length +
                " bounds=" + itemBounds.min.ToString("F3") + ".." +
                itemBounds.max.ToString("F3") + " rootRotation=" +
                instance.transform.localEulerAngles.ToString("F1") +
                " meshRotation=" + itemRenderers[0].transform.localEulerAngles.ToString("F1") +
                " meshLocalBounds=" +
                itemRenderers[0].GetComponent<MeshFilter>().sharedMesh.bounds.ToString() +
                " worldRotation=" + itemRenderers[0].transform.eulerAngles.ToString("F1"));
        }

        private static void Capture(Camera camera, string path, int width, int height)
        {
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            var oldTarget = camera.targetTexture;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
