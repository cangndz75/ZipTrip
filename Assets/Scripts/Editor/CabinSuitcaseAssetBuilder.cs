using System;
using UnityEditor;
using UnityEngine;
using ZipTrip.Unity;

namespace ZipTrip.Editor
{
    // ZT-040C: deterministic Unity side of the golden Cabin Suitcase. The FBX generates no materials; two explicit URP
    // Lit materials are assigned per authored slot on a prefab variant of the model. Presentation only, no colliders.
    public static class CabinSuitcaseAssetBuilder
    {
        public const string Folder = "Assets/Art/Models/Containers/CabinSuitcase/";
        public const string ModelPath = Folder + "Models/CabinSuitcase_Golden.fbx";
        public const string TexturePath = Folder + "Textures/T_CabinSuitcase_Exterior_BaseColor.jpg";
        public const string ExteriorMaterialPath = Folder + "Materials/M_CabinSuitcase_Exterior.mat";
        public const string LiningMaterialPath = Folder + "Materials/M_CabinSuitcase_Lining.mat";
        public const string PrefabPath = Folder + "Prefabs/CabinSuitcase_Golden.prefab";
        public const float ExteriorSmoothness = 0.3f;
        public const float LiningSmoothness = 0.08f;

        [MenuItem("ZipTrip/ZT-040C/Build Golden Cabin Suitcase Prefab")]
        public static void Build()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath)
                ?? throw new InvalidOperationException("Missing " + TexturePath);
            var exterior = SaveMaterial(ExteriorMaterialPath, Color.white, texture, ExteriorSmoothness);
            var lining = SaveMaterial(LiningMaterialPath, PuzzleBoardPresenter.ContainerLining, null, LiningSmoothness);
            AssetDatabase.SaveAssets();

            // Remap by authored FBX material name: submesh order differs per node (Unity orders the Lid's slots
            // Lining, Exterior), so positional assignment would be wrong. No FBX-generated materials are used.
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath)
                ?? throw new InvalidOperationException("Missing " + ModelPath);
            importer.addCollider = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), exterior.name), exterior);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), lining.name), lining);
            importer.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                ContainerRig.Bind(instance.transform); // throws if the contract nodes are missing
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (var material in renderer.sharedMaterials)
                        if (material != exterior && material != lining)
                            throw new InvalidOperationException($"{renderer.name} uses unexpected material {material}.");
                if (instance.GetComponentsInChildren<Collider>(true).Length > 0)
                    throw new InvalidOperationException("Container prefab must not carry colliders.");
                if (!AssetDatabase.IsValidFolder(Folder + "Prefabs"))
                    AssetDatabase.CreateFolder(Folder.TrimEnd('/'), "Prefabs");
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[ZT-040C] Saved " + PrefabPath);
        }

        private static Material SaveMaterial(string path, Color color, Texture texture, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var folder = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")
                    ?? throw new InvalidOperationException("URP Lit shader not found."));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
