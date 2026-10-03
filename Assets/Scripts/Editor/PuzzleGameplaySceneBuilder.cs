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
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(gameplay);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save " + ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ZT-040] Saved " + ScenePath);
        }

        private static GameObject Load(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab")
            ?? throw new InvalidOperationException("Missing golden prefab " + name);
    }
}
