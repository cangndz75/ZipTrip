using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ZipTrip.Editor
{
    public static class Art03FamilyAssets
    {
        private const string Folder = "Assets/Art/Materials/Items/Art03/";
        private const string Generated = "Builds/art03/generated/";
        private static readonly string[] Names = { "SweaterOpen", "Passport", "Shampoo", "Towel", "Sunglasses", "TravelPouch" };

        public static void Apply()
        {
            Directory.CreateDirectory(Folder);
            foreach (var name in Names)
                File.Copy(Generated + "T_Item_" + name + "_BaseColor.png", Folder + "T_Item_" + name + "_BaseColor.png", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var name in Names)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Items/Art01/M_Item_" + name + "_Art01.mat");
                var albedo = ImportTexture("T_Item_" + name + "_BaseColor.png", false, true);
                material.SetTexture("_BaseMap", albedo);
                material.SetFloat("_BumpScale", name == "SweaterOpen" ? .28f : name == "Towel" ? .25f : name == "TravelPouch" ? .65f : .4f);
                material.SetFloat("_Smoothness", name == "SweaterOpen" ? .25f : name == "Towel" ? .22f : name == "Sunglasses" ? .55f : .48f);
                // URP's packed-metallic keyword; keep one material and existing URP Lit per object.
                material.DisableKeyword("_METALLICGLOSSMAP");
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (name != "Passport" && name != "TravelPouch")
                {
                    material.DisableKeyword("_METALLICSPECGLOSSMAP");
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", name == "SweaterOpen" ? .18f : name == "Towel" ? .15f : .30f);
                }
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }

        private static Texture2D ImportTexture(string name, bool normal, bool sRgb)
        {
            var path = Folder + name;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = sRgb;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
