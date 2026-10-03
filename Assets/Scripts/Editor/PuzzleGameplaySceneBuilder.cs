using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZipTrip.Unity;

namespace ZipTrip.Editor
{
    // ZT-040: builds the ADR-0006 first-playable scene (Lv1-Lv2) wired to the v2 runtime only.
    public static class PuzzleGameplaySceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/PuzzleGameplay.unity";
        private const string PrefabFolder = "Assets/Art/Prefabs/Items/";
        private const string RuntimeMaterial = "Assets/Art/Materials/Containers/Cabin_GridOverlay.mat";

        [MenuItem("ZipTrip/ZT-040/Build Puzzle Gameplay Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camera = new GameObject("Main Camera", typeof(Camera));
            camera.tag = "MainCamera";

            var light = new GameObject("Directional Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var root = new GameObject("Puzzle Gameplay");
            root.AddComponent<PointerInteractor>();
            var catalog = root.AddComponent<GoldenItemPrefabCatalog>();
            catalog.Configure(Load("PF_Item_Laptop"), Load("PF_Item_SneakerPair"), Load("PF_Item_SweaterOpen"), Load("PF_Item_SweaterFolded"));
            var material = AssetDatabase.LoadAssetAtPath<Material>(RuntimeMaterial)
                ?? throw new InvalidOperationException("Runtime presentation material is missing.");
            var gameplay = root.AddComponent<PuzzleGameplayScene>();
            gameplay.Configure(material, catalog, "lv1-fit", "lv2-rotate");
            gameplay.ConfigureContainer(LoadContainer());
            gameplay.ConfigureFonts(LoadFont(UiFontPath), LoadFont(DisplayFontPath));
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(gameplay);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ZT-040] Saved " + ScenePath);
        }

        // ZT-040C: points the existing scene at the golden container without rebuilding it (no unrelated scene churn).
        [MenuItem("ZipTrip/ZT-040C/Wire Golden Container Into Puzzle Gameplay Scene")]
        public static void WireGoldenContainer()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var gameplay = UnityEngine.Object.FindFirstObjectByType<PuzzleGameplayScene>()
                ?? throw new InvalidOperationException("PuzzleGameplayScene missing in " + ScenePath);
            gameplay.ConfigureContainer(LoadContainer());
            EditorUtility.SetDirty(gameplay);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + ScenePath);
            Debug.Log("[ZT-040C] Wired golden container into " + ScenePath);
        }

        // ZT-040D.1: points the existing scene at the approved HUD typography without rebuilding it.
        [MenuItem("ZipTrip/ZT-040D/Wire HUD Fonts Into Puzzle Gameplay Scene")]
        public static void WireFonts()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var gameplay = UnityEngine.Object.FindFirstObjectByType<PuzzleGameplayScene>()
                ?? throw new InvalidOperationException("PuzzleGameplayScene missing in " + ScenePath);
            gameplay.ConfigureFonts(LoadFont(UiFontPath), LoadFont(DisplayFontPath));
            EditorUtility.SetDirty(gameplay);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Could not save " + ScenePath);
            Debug.Log("[ZT-040D.1] Wired HUD fonts into " + ScenePath);
        }

        public const string UiFontPath = "Assets/Art/Fonts/BricolageGrotesque/BricolageGrotesque-SemiBold.ttf";
        public const string DisplayFontPath = "Assets/Art/Fonts/BricolageGrotesque/BricolageGrotesque-ExtraBold.ttf";

        private static Font LoadFont(string path) =>
            AssetDatabase.LoadAssetAtPath<Font>(path) ?? throw new InvalidOperationException("Missing font " + path);

        private static GameObject LoadContainer() =>
            AssetDatabase.LoadAssetAtPath<GameObject>(CabinSuitcaseAssetBuilder.PrefabPath)
            ?? throw new InvalidOperationException("Missing golden container prefab; run the ZT-040C prefab build first.");

        private static GameObject Load(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab")
            ?? throw new InvalidOperationException("Missing golden prefab " + name);
    }
}
