using System;
using UnityEditor;
using UnityEngine;

namespace ZipTrip.Editor
{
    public static class Art04HeroAssets
    {
        private const string Models = "Assets/Art/Models/Items/Art04/";
        private const string Prefabs = "Assets/Art/Prefabs/Items/Art01/";
        private const string Materials = "Assets/Art/Materials/Items/Art01/";
        private static readonly string[] Names = { "TravelPouch", "SweaterOpen" };

        public static void Apply()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in Names)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Item_" + name + "_Art01.mat");
                if (material == null) throw new InvalidOperationException("ART03 material missing: " + name);

                var path = Prefabs + "PF_Item_" + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var oldRenderers = root.GetComponentsInChildren<Renderer>();
                    if (oldRenderers.Length != 1) throw new InvalidOperationException("ART04 expected one old renderer: " + name);
                    var oldBounds = oldRenderers[0].bounds;
                    var anchor = root.transform.Find("Base Centre Anchor");
                    if (anchor == null || anchor.childCount != 1) throw new InvalidOperationException("ART04 prefab anchor contract: " + name);
                    var oldVisual = anchor.GetChild(0);
                    var oldMesh = oldVisual.GetComponentInChildren<MeshFilter>();
                    if (oldMesh == null || !AssetDatabase.GetAssetPath(oldMesh.sharedMesh).Contains("/Art01/"))
                        throw new InvalidOperationException("ART04 apply requires the ART01 baseline prefab: " + name);
                    UnityEngine.Object.DestroyImmediate(oldVisual.gameObject);

                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "M_Item_" + name + "_Art04.fbx");
                    if (model == null) throw new InvalidOperationException("ART04 mesh missing: " + name);
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                    visual.transform.SetParent(anchor, false);
                    visual.transform.localPosition = new Vector3(0f,
                        name == "Passport" ? .11989926f : .2500003f,
                        name == "TravelPouch" ? 1.0423789f : name == "Passport" ? .5929549f : 1.1250246f);
                    visual.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                    visual.transform.localScale = Vector3.one * (name == "TravelPouch" ? 2.0847576f
                        : name == "Passport" ? 1.1859096f : 2.9f);
                    var renderers = visual.GetComponentsInChildren<MeshRenderer>();
                    if (renderers.Length != 1) throw new InvalidOperationException("ART04 must remain one renderer: " + name);
                    renderers[0].sharedMaterial = material;
                    var newBounds = renderers[0].bounds;
                    var baseScale = visual.transform.localScale.x;
                    visual.transform.localScale = new Vector3(
                        baseScale * oldBounds.size.x / newBounds.size.x,
                        baseScale * oldBounds.size.z / newBounds.size.z,
                        baseScale * oldBounds.size.y / newBounds.size.y);
                    newBounds = renderers[0].bounds;
                    var localDelta = anchor.InverseTransformVector(oldBounds.center - newBounds.center);
                    visual.transform.localPosition += localDelta;
                    newBounds = renderers[0].bounds;
                    if (Vector3.Distance(oldBounds.min, newBounds.min) > .002f ||
                        Vector3.Distance(oldBounds.max, newBounds.max) > .002f)
                        throw new InvalidOperationException("ART04 bounds drift: " + name + " " + oldBounds + " -> " + newBounds);
                    if (root.GetComponentsInChildren<Collider>(true).Length != 0 ||
                        root.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                        throw new InvalidOperationException("ART04 gameplay components introduced: " + name);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    Debug.Log("[ART04] " + name + " renderer bounds " + oldBounds + " -> " + newBounds
                        + "; triangles " + visual.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length / 3);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
        }
    }
}
