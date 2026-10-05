using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZipTrip.Editor
{
    // Offline-only import and icon bake. No runtime cameras, render textures or item-source edits.
    public static class TargetConvergenceAssets
    {
        public static void Prepare()
        {
            Import("mission_leather", 1024, false);
            BakeIcon("Passport", "passport");
            BakeIcon("Shampoo", "shampoo");
            AssetDatabase.SaveAssets();
        }

        private static void Import(string name, int size, bool mipmaps)
        {
            var path = "Assets/Resources/UiSlice011/" + name + ".png";
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = !mipmaps;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = size;
            importer.isReadable = false;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = size;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        private static void BakeIcon(string prefab, string id)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Prefabs/Items/Art01/PF_Item_" + prefab + ".prefab");
            var instance = Object.Instantiate(source);
            var cameraObject = new GameObject("Offline icon camera");
            var lightObject = new GameObject("Offline icon light");
            var target = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                instance.transform.position = Vector3.one * 1000f;
                foreach (var node in instance.GetComponentsInChildren<Transform>()) node.gameObject.layer = 31;
                var bounds = instance.GetComponentInChildren<Renderer>().bounds;
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * .6f;
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                camera.transform.position = bounds.center + Vector3.up * 10f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.8f;
                light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                pixels.Apply();
                File.WriteAllBytes("Assets/Resources/UiSlice011/item_" + id + ".png", pixels.EncodeToPNG());
                Import("item_" + id, 256, false);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}
