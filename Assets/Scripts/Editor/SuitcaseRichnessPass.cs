using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZipTrip.Editor
{
    // SUITCASE-RICHNESS-01: deterministic material maps for the existing Golden Cabin UVs.
    // The source FBXs, prefab hierarchy, transforms, colliders and gameplay code are untouched.
    public static class SuitcaseRichnessPass
    {
        private const string Root = "Assets/Art/Models/Containers/CabinSuitcase/";
        private const string Textures = Root + "Textures/";
        private const string ExteriorSource = Textures + "T_CabinSuitcase_Exterior_BaseColor.jpg";
        private const string InteriorSource = Textures + "T_CabinInterior_Review_BaseColor.png";
        private const string ExteriorNormal = Textures + "T_CabinSuitcase_Exterior_Richness_Normal.png";
        private const string ExteriorMetallic = Textures + "T_CabinSuitcase_Exterior_Richness_MetallicSmoothness.png";
        private const string InteriorBase = Textures + "T_CabinInterior_Richness_BaseColor.png";
        private const string InteriorOcclusion = Textures + "T_CabinInterior_Richness_Occlusion.png";
        private const string ExteriorMaterial = Root + "Materials/M_CabinSuitcase_Exterior.mat";
        private const string InteriorMaterial = Root + "Materials/M_CabinInterior_Review.mat";
        private const string Prefab = Root + "Prefabs/CabinSuitcase_Golden.prefab";
        private const string Evidence = "Builds/suitcase-richness-01/";

        public static void AuditBefore() => Audit("budget-before.txt");
        public static void AuditAfter() => Audit("budget-after.txt");

        [MenuItem("ZipTrip/SUITCASE-RICHNESS-01/Generate Material Maps")]
        public static void Apply()
        {
            var exterior = LoadPixels(ExteriorSource);
            var interior = LoadPixels(InteriorSource);
            try
            {
                MakeExteriorMaps(exterior);
                MakeInteriorBase(interior);
                MakeInteriorOcclusion(interior);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(exterior);
                UnityEngine.Object.DestroyImmediate(interior);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(ExteriorNormal, TextureImporterType.NormalMap);
            ConfigureTexture(ExteriorMetallic, TextureImporterType.Default);
            ConfigureTexture(InteriorBase, TextureImporterType.Default, true);
            ConfigureTexture(InteriorOcclusion, TextureImporterType.Default);

            var shell = AssetDatabase.LoadAssetAtPath<Material>(ExteriorMaterial);
            var lining = AssetDatabase.LoadAssetAtPath<Material>(InteriorMaterial);
            if (shell == null || lining == null)
                throw new InvalidOperationException("Golden Cabin materials are missing.");
            shell.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ExteriorNormal));
            shell.SetFloat("_BumpScale", 0.6f);
            shell.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ExteriorMetallic));
            shell.SetFloat("_Metallic", 1f);
            shell.SetFloat("_Smoothness", 1f);
            shell.EnableKeyword("_NORMALMAP");
            shell.EnableKeyword("_METALLICSPECGLOSSMAP");
            lining.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(InteriorBase));
            lining.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(InteriorBase));
            lining.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(InteriorOcclusion));
            lining.SetFloat("_OcclusionStrength", 0.85f);
            lining.SetFloat("_BumpScale", 1.4f);
            lining.SetFloat("_Smoothness", 0.13f);
            lining.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(shell);
            EditorUtility.SetDirty(lining);
            AssetDatabase.SaveAssets();
            Debug.Log("[SUITCASE-RICHNESS-01] Material maps applied; container geometry and prefab unchanged.");
        }

        private static Texture2D LoadPixels(string assetPath)
        {
            var path = Path.GetFullPath(assetPath);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!texture.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidOperationException("Could not decode " + path);
            return texture;
        }

        private static void MakeExteriorMaps(Texture2D source)
        {
            const int size = 1024;
            var src = source.GetPixels32();
            var normals = new Color32[size * size];
            var packed = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var sx = x * source.width / size;
                var sy = y * source.height / size;
                var c = src[sy * source.width + sx];
                var r = c.r / 255f;
                var g = c.g / 255f;
                var b = c.b / 255f;
                var brass = r > 0.42f && g > 0.35f && b < g * 0.78f && g > r * 0.58f;
                var leather = !brass && r > b * 1.20f && r > g * 1.14f;
                var grain = (Hash(x, y) - 0.5f) * (leather ? 0.035f : 0.025f);
                var left = Brightness(src[sy * source.width + Mathf.Max(0, sx - 2)]);
                var right = Brightness(src[sy * source.width + Mathf.Min(source.width - 1, sx + 2)]);
                var down = Brightness(src[Mathf.Max(0, sy - 2) * source.width + sx]);
                var up = Brightness(src[Mathf.Min(source.height - 1, sy + 2) * source.width + sx]);
                var nx = Mathf.Clamp((left - right) * 0.34f + grain, -0.25f, 0.25f);
                var ny = Mathf.Clamp((down - up) * 0.34f + grain, -0.25f, 0.25f);
                var nz = Mathf.Sqrt(1f - nx * nx - ny * ny);
                normals[y * size + x] = new Color(nx * 0.5f + 0.5f, ny * 0.5f + 0.5f, nz * 0.5f + 0.5f, 1f);
                var smooth = brass ? 0.68f : leather ? 0.37f : 0.18f;
                smooth = Mathf.Clamp01(smooth + (Brightness(c) - 0.5f) * 0.055f + grain * 0.4f);
                packed[y * size + x] = new Color(brass ? 0.82f : 0f, 0f, 0f, smooth);
            }
            SavePng(ExteriorNormal, size, size, normals);
            SavePng(ExteriorMetallic, size, size, packed);
        }

        private static void MakeInteriorOcclusion(Texture2D source)
        {
            var pixels = source.GetPixels32();
            var result = new Color32[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                // Follow the approved cushion texture's broad light/dark forms; no cells or new seams.
                var shade = Mathf.Clamp(0.58f + Brightness(pixels[i]) * 1.6f, 0.66f, 0.98f);
                result[i] = new Color(1f, shade, 1f, 1f);
            }
            SavePng(InteriorOcclusion, source.width, source.height, result);
        }

        private static void MakeInteriorBase(Texture2D source)
        {
            var pixels = source.GetPixels32();
            var result = new Color32[pixels.Length];
            for (var i = 0; i < pixels.Length; i++)
            {
                // Strengthen the approved padded forms and weave without drawing cells or new construction lines.
                var depth = Mathf.Clamp(1.12f + (Brightness(pixels[i]) - 0.19f) * 5f, 0.75f, 1.35f);
                result[i] = new Color(
                    Mathf.Clamp01(pixels[i].r / 255f * depth * 1.06f),
                    Mathf.Clamp01(pixels[i].g / 255f * depth * 1.02f),
                    Mathf.Clamp01(pixels[i].b / 255f * depth), 1f);
            }
            SavePng(InteriorBase, source.width, source.height, result);
        }

        private static float Brightness(Color32 c) => (c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f) / 255f;

        private static float Hash(int x, int y)
        {
            unchecked
            {
                var h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        private static void SavePng(string path, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void ConfigureTexture(string path, TextureImporterType type, bool sRgb = false)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }

        private static void Audit(string file)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (prefab == null) throw new InvalidOperationException("Missing " + Prefab);
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().OrderBy(m => m.name).ToArray();
            long triangles = 0;
            var report = new StringBuilder();
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var count = Enumerable.Range(0, mesh.subMeshCount).Sum(i => (long)mesh.GetIndexCount(i) / 3);
                triangles += count;
                report.AppendLine($"mesh={renderer.name}; triangles={count}; materials={string.Join(",", renderer.sharedMaterials.Select(m => m.name))}");
            }
            report.AppendLine($"rendererCount={renderers.Length}");
            report.AppendLine($"materialCount={materials.Length}");
            report.AppendLine($"triangleCount={triangles}");
            report.AppendLine($"colliderCount={prefab.GetComponentsInChildren<Collider>(true).Length}");
            foreach (var material in materials)
            {
                report.AppendLine($"material={material.name}; shader={material.shader.name}; passes={material.passCount}; keywords={string.Join(",", material.shaderKeywords)}");
                foreach (var property in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(property);
                    if (texture != null)
                        report.AppendLine($"texture={material.name}.{property}; {texture.name}; {texture.width}x{texture.height}");
                }
            }
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Evidence + file, report.ToString());
            Debug.Log("[SUITCASE-RICHNESS-01] " + Evidence + file);
        }
    }
}
