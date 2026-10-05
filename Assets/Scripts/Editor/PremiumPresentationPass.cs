using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZipTrip.Editor
{
    // Asset-only fitting: the root, footprint, collider and catalog contracts stay unchanged.
    public static class PremiumPresentationPass
    {
        private const string Container = "Assets/Art/Models/Containers/CabinSuitcase/";

        public static void Apply()
        {
            var report = new System.Text.StringBuilder();
            foreach (var item in new[] { ("SweaterOpen", 3, 3, .88f), ("Passport", 1, 2, .86f),
                ("Shampoo", 1, 3, .86f), ("Towel", 1, 4, .90f), ("TravelPouch", 2, 3, .86f),
                ("Sunglasses", 2, 1, .86f) })
            {
                var path = "Assets/Art/Prefabs/Items/Art01/PF_Item_" + item.Item1 + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var anchor = root.transform.Find("Base Centre Anchor");
                    var renderer = root.GetComponentInChildren<MeshRenderer>();
                    var before = renderer.bounds;
                    var factor = new Vector3(
                        Mathf.Max(1f, (item.Item2 - .12f) / before.size.x),
                        item.Item1 == "Towel" ? Mathf.Min(1.25f / anchor.localScale.y, .5f / before.size.y) : 1f,
                        Mathf.Max(1f, item.Item3 * item.Item4 / before.size.z));
                    // Keep the long rolled towel's circular end: compensate only the compressed vertical dimension.
                    anchor.localScale = Vector3.Scale(anchor.localScale, factor);
                    var after = renderer.bounds;
                    anchor.position += new Vector3(item.Item2 * .5f - after.center.x,
                        -after.min.y, -item.Item3 * .5f - after.center.z);
                    after = renderer.bounds;
                    if (after.min.x < .049f || after.max.x > item.Item2 - .049f
                        || after.min.z < -item.Item3 + .049f || after.max.z > -.049f)
                        throw new InvalidOperationException("Visual outside footprint: " + item.Item1);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    report.AppendLine($"{item.Item1}: bounds {before.size:F3} -> {after.size:F3}; occupancy {after.size.x / item.Item2:P1} x {after.size.z / item.Item3:P1}; scale {factor:F3}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var name in new[] { "M_CabinInterior_Review", "M_CabinSuitcase_Lining" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(Container + "Materials/" + name + ".mat");
                var color = name == "M_CabinInterior_Review" ? new Color(.48f, .53f, .57f) : new Color(.045f, .10f, .115f);
                material.SetColor("_BaseColor", color);
                material.SetColor("_Color", color);
                material.SetFloat("_Smoothness", .19f);
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Builds/premium-pass");
            File.WriteAllText("Builds/premium-pass/asset-measurements.txt", report.ToString());
            Debug.Log(report);
        }

        public static void BakeLid()
        {
            var path = Container + "Prefabs/CabinSuitcase_Golden.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var lid = root.transform.Find("Lid");
                var renderer = lid.GetComponent<MeshRenderer>();
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    if (!materials[i].name.Contains("Exterior")) continue;
                    var leather = new Material(materials[i]);
                    leather.SetColor("_BaseColor", new Color(.55f, .60f, .58f));
                    leather.SetFloat("_BumpScale", .5f);
                    materials[i] = SaveAsset(leather, "M_PremiumLid.mat");
                }
                renderer.sharedMaterials = materials;
                var mesh = lid.GetComponent<MeshFilter>().sharedMesh;
                var collider = lid.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                Physics.SyncTransforms();
                var b = mesh.bounds;
                var unit = b.size.x;
                var rect = new Rect(b.min.x + unit * .12f, b.min.z + b.size.z * .14f,
                    unit * .76f, b.size.z * .72f);
                var template = ZipTrip.Unity.PresentationKit.TemplateOrFallback(null);
                var binding = SaveAsset(ZipTrip.Unity.PresentationKit.Matte(template, new Color(.12f, .068f, .042f), null, .3f), "M_PremiumBinding.mat");
                var thread = SaveAsset(ZipTrip.Unity.PresentationKit.Matte(template, new Color(.49f, .34f, .18f), null, .22f), "M_PremiumThread.mat");
                var brass = ZipTrip.Unity.PresentationKit.Matte(template, new Color(.62f, .40f, .14f), null, .65f);
                brass.SetFloat("_Metallic", .75f);
                brass = SaveAsset(brass, "M_PremiumBrass.mat");
                var perimeter = ZipTrip.Unity.PresentationKit.RoundedRect(rect, unit * .055f, 16);
                BakeSurfaceMesh("LidWelt", ZipTrip.Unity.PresentationKit.Ribbon(perimeter, unit * .006f, 0f, true), binding, unit * .0015f);
                var inset = new Rect(rect.xMin + unit * .011f, rect.yMin + unit * .011f, rect.width - unit * .022f, rect.height - unit * .022f);
                BakeSurfaceMesh("LidStitch", ZipTrip.Unity.PresentationKit.DashedPath(
                    ZipTrip.Unity.PresentationKit.RoundedRect(inset, unit * .047f, 16), unit * .006f, unit * .004f,
                    unit * .0013f, 0f, true), thread, unit * .0018f);
                var vertices = new System.Collections.Generic.List<Vector3>();
                var triangles = new System.Collections.Generic.List<int>();
                var buckles = new System.Collections.Generic.List<CombineInstance>();
                foreach (var fraction in new[] { .22f, .78f })
                {
                    var x = Mathf.Lerp(b.min.x, b.max.x, fraction);
                    int start = vertices.Count;
                    const int across = 8, along = 64;
                    for (var z = 0; z <= along; z++)
                        for (var u = 0; u <= across; u++)
                        {
                            var arch = Mathf.Sin(u / (float)across * Mathf.PI) * unit * .003f;
                            vertices.Add(new Vector3(x + (u / (float)across - .5f) * unit * .047f,
                                arch, Mathf.Lerp(rect.yMin, rect.yMax, z / (float)along)));
                            if (z == along || u == across) continue;
                            var a = start + z * (across + 1) + u;
                            triangles.AddRange(new[] { a, a + across + 1, a + 1, a + 1, a + across + 1, a + across + 2 });
                        }
                    var buckle = new Rect(x - unit * .029f, rect.yMin + rect.height * .18f, unit * .058f, unit * .068f);
                    buckles.Add(new CombineInstance { mesh = ZipTrip.Unity.PresentationKit.Ribbon(
                        ZipTrip.Unity.PresentationKit.RoundedRect(buckle, unit * .008f, 12), unit * .006f, 0f, true), transform = Matrix4x4.identity });
                }
                var straps = new Mesh();
                straps.SetVertices(vertices); straps.SetTriangles(triangles, 0);
                BakeSurfaceMesh("LidStraps", straps, binding, unit * .002f);
                var buckleMesh = new Mesh();
                buckleMesh.CombineMeshes(buckles.ToArray(), true, false);
                foreach (var buckle in buckles) UnityEngine.Object.DestroyImmediate(buckle.mesh);
                BakeSurfaceMesh("LidBuckles", buckleMesh, brass, unit * .006f);
                UnityEngine.Object.DestroyImmediate(collider);
                PrefabUtility.SaveAsPrefabAsset(root, path);

                void BakeSurfaceMesh(string name, Mesh detail, Material material, float lift)
                {
                    var points = detail.vertices;
                    for (var i = 0; i < points.Length; i++)
                    {
                        var p = points[i];
                        var ray = new Ray(lid.TransformPoint(new Vector3(p.x, b.max.y + unit, p.z)), -lid.up);
                        if (!collider.Raycast(ray, out var hit, unit * 4f))
                            throw new InvalidOperationException("Lid detail misses surface: " + name + " " + p);
                        p.y += lid.InverseTransformPoint(hit.point).y + lift;
                        points[i] = p;
                    }
                    detail.vertices = points;
                    detail.RecalculateNormals(); detail.RecalculateBounds();
                    detail = SaveAsset(detail, name + ".asset");
                    var old = lid.Find(name);
                    if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    ZipTrip.Unity.PresentationKit.MeshObject(name, lid, detail, material);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        private static T SaveAsset<T>(T asset, string name) where T : UnityEngine.Object
        {
            asset.name = Path.GetFileNameWithoutExtension(name);
            var folder = Container + "Premium";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Container.TrimEnd('/'), "Premium");
            var path = folder + "/" + name;
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing);
            UnityEngine.Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
