using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZipTrip.Editor
{
    public static class ArtGate02BAssetBuilder
    {
        private const string ModelFolder = "Assets/Art/Models/Items/Final/";
        private const string MaterialFolder = "Assets/Art/Materials/Items/GoldenLv1Final/";
        private const string PrefabFolder = "Assets/Art/Prefabs/Items/";
        private static readonly string[] Names =
            { "Passport", "Towel", "Shampoo", "Sunglasses", "TravelPouch" };

        [MenuItem("ZipTrip/ART-GATE-02B/Build Five Final Item Prefabs")]
        public static void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP Lit shader missing");
            foreach (var name in Names)
            {
                var modelPath = ModelFolder + "M_Item_" + name + ".fbx";
                var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter
                    ?? throw new InvalidOperationException("Missing FBX importer: " + modelPath);
                if (Math.Abs(importer.globalScale - 100f) > .001f)
                {
                    importer.globalScale = 100f;
                    importer.SaveAndReimport();
                }
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath)
                    ?? throw new InvalidOperationException("Missing model: " + modelPath);
                var texturePath = MaterialFolder + "T_Item_" + name + "_BaseColor.png";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)
                    ?? throw new InvalidOperationException("Missing texture: " + texturePath);
                var materialPath = MaterialFolder + "M_Item_" + name + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = "M_Item_" + name };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.shader = shader;
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", texture);
                material.SetFloat("_Smoothness", name == "Sunglasses" ? .38f :
                    name == "Shampoo" ? .31f : name == "Passport" ? .24f : .12f);
                EditorUtility.SetDirty(material);

                var root = new GameObject("PF_Item_" + name);
                try
                {
                    var source = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    source.transform.SetParent(root.transform, false);
                    source.transform.localPosition = Vector3.zero;
                    source.transform.localRotation = Quaternion.identity;
                    source.transform.localScale = Vector3.one;
                    var renderers = source.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length != 1)
                        throw new InvalidOperationException(name + " expected one mesh renderer, got " + renderers.Length);
                    renderers[0].sharedMaterial = material;
                    var prefabPath = PrefabFolder + "PF_Item_" + name + ".prefab";
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    if (prefab == null || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                        prefab.GetComponentsInChildren<Collider>(true).Length != 0)
                        throw new InvalidOperationException("Invalid script-free prefab: " + prefabPath);
                    var triangles = prefab.GetComponentsInChildren<MeshFilter>()
                        .Sum(filter => filter.sharedMesh.triangles.Length / 3);
                    Debug.Log("[ART-GATE-02B] " + name + " triangles=" + triangles +
                        " texture=" + texture.width + "x" + texture.height);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            PuzzleGameplaySceneBuilder.WireGoldenLv1FinalItems();
        }
    }
}
