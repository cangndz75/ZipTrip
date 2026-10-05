using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZipTrip.Unity;

namespace ZipTrip.Editor
{
    public static class Art01AssetBuilder
    {
        private const string ModelFolder = "Assets/Art/Models/Items/Art01/";
        private const string MaterialFolder = "Assets/Art/Materials/Items/Art01/";
        private const string PrefabFolder = "Assets/Art/Prefabs/Items/Art01/";
        // One normalization rule (mirrors Tools/Art/prepare_art01.py): uniform scale until the visual bounds touch the
        // authored footprint inset by Margin cells per side; base centre on the footprint centre.
        public const float Margin = 0.05f;
        // Documented exception: the tray Shampoo must clear the Boarding Pass backdrop prop (BACKDROP-01).
        public const float ShampooMargin = 0.14f;
        private const float HeightLimit = 0.50f;

        private static readonly (string Name, int Width, int Depth, bool Sideways)[] Items =
        {
            ("SweaterOpen", 3, 3, false),
            ("Passport", 1, 2, false),
            ("Towel", 1, 4, true),
            ("Shampoo", 1, 3, false),
            ("Sunglasses", 2, 1, false),
            ("TravelPouch", 2, 3, false)
        };

        [MenuItem("ZipTrip/ART-01/Inspect Imported Models")]
        public static void InspectModels()
        {
            foreach (var item in Items)
            {
                var path = ModelFolder + "M_Item_" + item.Name + "_Art01.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model == null) { Debug.LogWarning("[ART-01] Missing " + path); continue; }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    var bounds = BoundsOf(instance);
                    Debug.Log("[ART-01] Imported " + item.Name + " bounds " + bounds.min + ".." + bounds.max
                        + " renderers=" + instance.GetComponentsInChildren<MeshRenderer>().Length);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
        }

        [MenuItem("ZipTrip/ART-01/Audit Item Performance")]
        public static void AuditPerformance()
        {
            long oldTriangles = 0, newTriangles = 0, oldBytes = 0, newBytes = 0;
            foreach (var item in Items)
            {
                var oldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/Prefabs/Items/PF_Item_" + item.Name + ".prefab");
                var newPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "PF_Item_" + item.Name + ".prefab");
                if (oldPrefab == null || newPrefab == null) throw new InvalidOperationException(item.Name + " prefab missing");
                var oldTris = Triangles(oldPrefab);
                var newTris = Triangles(newPrefab);
                var oldFill = Fill(oldPrefab, item.Width, item.Depth);
                var newFill = Fill(newPrefab, item.Width, item.Depth);
                Debug.Log("[ART-01 FILL] " + item.Name + " footprint=" + item.Width + "x" + item.Depth
                    + " xzFill=" + oldFill.x.ToString("P0") + "x" + oldFill.y.ToString("P0") + "->"
                    + newFill.x.ToString("P0") + "x" + newFill.y.ToString("P0"));
                var oldMemory = VisualMemory(oldPrefab);
                var newMemory = VisualMemory(newPrefab);
                oldTriangles += oldTris;
                newTriangles += newTris;
                oldBytes += oldMemory;
                newBytes += newMemory;
                var material = newPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                var textures = new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" }
                    .Select(property => (Texture2D)material.GetTexture(property)).ToArray();
                Debug.Log("[ART-01 PERF] " + item.Name + " triangles=" + oldTris + "->" + newTris
                    + " editorBytes=" + oldMemory + "->" + newMemory + " textures="
                    + string.Join(",", textures.Select(texture => texture.width + "x" + texture.height
                        + "/" + texture.format + "/" + UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture))));
            }
            Debug.Log("[ART-01 PERF] Lv1 six items triangles=" + oldTriangles + "->" + newTriangles
                + " editorBytes=" + oldBytes + "->" + newBytes);
        }

        [MenuItem("ZipTrip/ART-01/Build And Wire Golden Lv1 Items")]
        public static void BuildAndWire()
        {
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);
            foreach (var item in Items)
                Build(item);
            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(PuzzleGameplaySceneBuilder.ScenePath, OpenSceneMode.Single);
            var catalog = UnityEngine.Object.FindFirstObjectByType<GoldenItemPrefabCatalog>()
                ?? throw new InvalidOperationException("GoldenItemPrefabCatalog missing in shipped scene");
            var serialized = new SerializedObject(catalog);
            foreach (var item in Items)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "PF_Item_" + item.Name + ".prefab");
                if (prefab == null) continue; // Reported fallback: retain the existing catalog reference.
                var field = item.Name == "SweaterOpen" ? "sweaterOpen"
                    : item.Name == "TravelPouch" ? "travelPouch" : char.ToLowerInvariant(item.Name[0]) + item.Name.Substring(1);
                serialized.FindProperty(field).objectReferenceValue = prefab;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + PuzzleGameplaySceneBuilder.ScenePath);
            Debug.Log("[ART-01] Wired six item visuals through the scene catalog");
        }

        private static void Build((string Name, int Width, int Depth, bool Sideways) item)
        {
            var modelPath = ModelFolder + "M_Item_" + item.Name + "_Art01.fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[ART-01] Missing normalized FBX; retaining previous visual: " + item.Name);
                return;
            }
            importer.globalScale = 1f;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var material = CreateMaterial(item.Name);
            var root = new GameObject("PF_Item_" + item.Name);
            try
            {
                var anchor = new GameObject("Base Centre Anchor").transform;
                anchor.SetParent(root.transform, false);
                anchor.localPosition = new Vector3(item.Width * .5f, 0f, -item.Depth * .5f);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(anchor, false);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localScale = Vector3.one;
                visual.transform.localRotation = item.Sideways
                    ? Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-90f, 0f, 0f)
                    : Quaternion.Euler(-90f, 0f, 0f);
                var renderers = visual.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length != 1)
                    throw new InvalidOperationException(item.Name + " expected one mesh renderer, got " + renderers.Length);
                renderers[0].sharedMaterial = material;
                var original = BoundsOf(visual);
                var margin = item.Name == "Shampoo" ? ShampooMargin : Margin;
                var scale = Mathf.Min((item.Width - 2f * margin) / original.size.x,
                    (item.Depth - 2f * margin) / original.size.z);
                visual.transform.localScale = Vector3.one * scale;
                var fitted = BoundsOf(visual);
                visual.transform.localPosition = new Vector3(anchor.position.x - fitted.center.x,
                    -fitted.min.y, anchor.position.z - fitted.center.z);
                fitted = BoundsOf(visual);
                if (fitted.min.x < margin - .001f || fitted.max.x > item.Width - margin + .001f
                    || fitted.max.z > -margin + .001f || fitted.min.z < -item.Depth + margin - .001f
                    || fitted.min.y < -.001f || fitted.max.y > HeightLimit + .001f)
                    throw new InvalidOperationException(item.Name + " outside visual bounds: " + fitted);
                var path = PrefabFolder + root.name + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (prefab == null || prefab.GetComponentsInChildren<Collider>(true).Length != 0
                    || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Invalid script-free ART-01 prefab " + path);
                var triangles = prefab.GetComponentsInChildren<MeshFilter>(true)
                    .Sum(filter => filter.sharedMesh.triangles.Length / 3);
                Debug.Log("[ART-01] " + item.Name + " scale=" + scale.ToString("F5")
                    + " rotation=" + visual.transform.localEulerAngles + " bounds=" + fitted.min + ".." + fitted.max
                    + " triangles=" + triangles + " margin=" + margin);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Material CreateMaterial(string name)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP Lit shader missing");
            var baseMap = ImportTexture(name, "BaseColor", false, false);
            var normal = ImportTexture(name, "Normal", true, false);
            var metallic = ImportTexture(name, "MetallicSmoothness", false, true);
            var path = MaterialFolder + "M_Item_" + name + "_Art01.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "M_Item_" + name + "_Art01" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", baseMap);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_METALLICGLOSSMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D ImportTexture(string name, string suffix, bool normal, bool alpha)
        {
            var path = MaterialFolder + "T_Item_" + name + "_" + suffix + (suffix == "BaseColor" ? ".jpg" : ".png");
            var importer = AssetImporter.GetAtPath(path) as TextureImporter
                ?? throw new InvalidOperationException("Missing texture importer: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !alpha;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException(root.name + " has no renderer");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static Vector2 Fill(GameObject prefab, int width, int depth)
        {
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return new Vector2(bounds.size.x / width, bounds.size.z / depth);
        }

        private static long Triangles(GameObject root) => root.GetComponentsInChildren<MeshFilter>(true)
            .Sum(filter => filter.sharedMesh.triangles.Length / 3L);

        private static long VisualMemory(GameObject root)
        {
            var meshes = root.GetComponentsInChildren<MeshFilter>(true).Select(filter => filter.sharedMesh).Distinct();
            var textures = root.GetComponentsInChildren<MeshRenderer>(true)
                .SelectMany(renderer => renderer.sharedMaterials)
                .SelectMany(material => new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap" }
                    .Select(property => material.HasProperty(property) ? material.GetTexture(property) : null))
                .Where(texture => texture != null).Distinct();
            return meshes.Sum(mesh => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh))
                + textures.Sum(texture => UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture));
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.TrimEnd('/').Split('/');
            var parent = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var child = parent + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(child)) AssetDatabase.CreateFolder(parent, parts[i]);
                parent = child;
            }
        }
    }
}
