using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZipTrip.Unity;

namespace ZipTrip.Editor
{
    public static class ZT016GameplaySceneBuilder
    {
        private const string GameplayScene = "Assets/Scenes/GameplaySandbox.unity";
        private const string DeviceReviewScene = "Assets/Scenes/ZT016GoldenGameplayTest.unity";
        private const string PrefabFolder = "Assets/Art/Prefabs/Items/";
        private const string RuntimeMaterial =
            "Assets/Art/Materials/Containers/Cabin_GridOverlay.mat";

        [MenuItem("ZipTrip/ZT-016/Bind Golden Prefabs And Build Review Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            var presentation = UnityEngine.Object.FindFirstObjectByType<PhaseAL1Presentation>();
            if (presentation == null)
                throw new InvalidOperationException("Gameplay presentation root is missing.");

            var catalog = presentation.GetComponent<GoldenItemPrefabCatalog>();
            if (catalog == null)
                catalog = presentation.gameObject.AddComponent<GoldenItemPrefabCatalog>();
            catalog.Configure(Load("PF_Item_Laptop"), Load("PF_Item_SneakerPair"),
                Load("PF_Item_SweaterOpen"), Load("PF_Item_SweaterFolded"));
            var board = presentation.GetComponent<BoardPresenter>();
            board.ConfigureGoldenItemPrefabs(catalog);
            var material = AssetDatabase.LoadAssetAtPath<Material>(RuntimeMaterial);
            if (material == null)
                throw new InvalidOperationException("Runtime presentation material is missing.");
            board.ConfigureRuntimeMaterial(material);
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(board);
            EditorSceneManager.SaveScene(scene, GameplayScene);

            var serialized = new SerializedObject(presentation);
            serialized.FindProperty("levelId").stringValue = "L3";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, DeviceReviewScene);
            Debug.Log("ZT-016 golden bindings saved; L1 GameplaySandbox and L3 device review scene ready.");
        }

        private static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + name + ".prefab");
            if (prefab == null)
                throw new InvalidOperationException("Missing accepted prefab: " + name);
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Golden prefab must remain script-free: " + name);
            return prefab;
        }
    }
}
