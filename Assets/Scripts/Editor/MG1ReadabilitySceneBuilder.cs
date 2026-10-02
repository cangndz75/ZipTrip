using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZipTrip.Unity;

namespace ZipTrip.Editor
{
    public static class MG1ReadabilitySceneBuilder
    {
        private const string SourceScene = "Assets/Scenes/ReadabilityTest.unity";
        private const string TestScene = "Assets/Scenes/MG1ReadabilityTest.unity";

        [MenuItem("ZipTrip/ZA-005/Rebuild MG-1 ReadabilityTest")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            var previousItems = GameObject.Find("Accepted item prefabs — static scale review");
            if (previousItems == null)
                throw new InvalidOperationException("Accepted ZA-004 scene item group missing.");
            UnityEngine.Object.DestroyImmediate(previousItems);

            var presenterObject = new GameObject("MG-1 one-item presentation");
            var presenter = presenterObject.AddComponent<MG1ReadabilityPresenter>();
            var definitions = new[]
            {
                (Name: "Laptop", File: "PF_Item_Laptop", Column: 1, Row: 2),
                (Name: "Sweater Open", File: "PF_Item_SweaterOpen", Column: 1, Row: 2),
                (Name: "Sweater Folded", File: "PF_Item_SweaterFolded", Column: 2, Row: 2),
                (Name: "Sneaker Pair", File: "PF_Item_SneakerPair", Column: 2, Row: 2)
            };
            var instances = new GameObject[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                var entry = definitions[index];
                var path = "Assets/Art/Prefabs/Items/" + entry.File + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    throw new InvalidOperationException("Missing accepted prefab: " + path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = entry.Name;
                instance.transform.SetParent(presenterObject.transform, false);
                instance.transform.localPosition = new Vector3(entry.Column, 0f, -entry.Row);
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                if (instance.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                    instance.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("Item prefab has script or collider: " + path);
                if (!instance.GetComponentsInChildren<MeshRenderer>(true).Any())
                    throw new InvalidOperationException("Item prefab has no mesh: " + path);
                instance.SetActive(false);
                instances[index] = instance;
            }
            presenter.Configure(instances);
            var camera = GameObject.Find("Main Camera")?.GetComponent<Camera>();
            if (camera == null || !camera.orthographic ||
                Mathf.Abs(camera.transform.eulerAngles.x - 75f) > 0.001f)
                throw new InvalidOperationException("Canonical 75-degree camera missing.");
            if (camera.GetComponent<ReadabilityPortraitFraming>() == null)
                throw new InvalidOperationException("Portrait framing missing.");
            EditorSceneManager.SaveScene(scene, TestScene);
            Debug.Log("MG1 scene ready: " + TestScene + "; four inactive prefabs; " +
                "one item activated at runtime; no footprint or answer UI.");
        }
    }
}
