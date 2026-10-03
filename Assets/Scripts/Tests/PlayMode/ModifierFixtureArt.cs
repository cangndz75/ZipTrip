using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // Test-only physical visuals for the authored modifier fixtures. No item art or content enters shipped levels.
    public sealed class ModifierFixtureArt : IDisposable
    {
        private readonly Material _template;
        private readonly Dictionary<string, GameObject> _visuals = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();

        public ModifierFixtureArt(Material template) => _template = PresentationKit.TemplateOrFallback(template);

        public GameObject Resolve(PuzzleItem item)
        {
            if (item.Definition.Id == "sweater")
            {
#if UNITY_EDITOR
                var path = item.StateId == "folded"
                    ? "Assets/Art/Prefabs/Items/PF_Item_SweaterFolded.prefab"
                    : "Assets/Art/Prefabs/Items/PF_Item_SweaterOpen.prefab";
                var golden = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (golden != null)
                    return golden;
#endif
            }

            var key = item.Definition.Id + "|" + item.StateId;
            if (_visuals.TryGetValue(key, out var visual))
                return visual;
            visual = new GameObject("Fixture " + key);
            switch (item.Definition.Id)
            {
                case "jacket": Jacket(visual, item.StateId == "compressed"); break;
                case "shoe": Shoe(visual); break;
                case "socks": Socks(visual); break;
                case "box": Case(visual); break;
                case "book": Passport(visual); break;
                case "sweater": Sweater(visual, item.StateId == "folded"); break;
                case "fabric": Fabric(visual, item.StateId == "folded"); break;
                default: UnityEngine.Object.Destroy(visual); return null;
            }
            visual.SetActive(false);
            _visuals.Add(key, visual);
            return visual;
        }

        private Material Ink(int rgb) => Own(PresentationKit.Matte(_template, PresentationKit.Hex(rgb), null, 0.12f));

        private void Part(GameObject root, string name, Rect rect, float radius, float bottom, float top, Material material)
        {
            var mesh = Own(PresentationKit.Slab(rect, radius, bottom, top));
            var part = PresentationKit.MeshObject(name, root.transform, mesh, material);
            part.GetComponent<MeshRenderer>().receiveShadows = true;
        }

        private void Jacket(GameObject root, bool compressed)
        {
            // Identical top silhouette and seams in both states. Only the side profile changes with authored thickness.
            var height = compressed ? 0.14f : 0.68f;
            var cloth = Ink(0xC96E52);
            var edge = Ink(0xA95440);
            var seam = Ink(0xE4AE86);
            var lining = Ink(0x31515D);
            Part(root, "Left sleeve", new Rect(0.07f, -1.60f, 0.52f, 1.18f), 0.22f, 0.02f, height * 0.83f, edge);
            Part(root, "Right sleeve", new Rect(1.41f, -1.60f, 0.52f, 1.18f), 0.22f, 0.02f, height * 0.83f, edge);
            Part(root, "Puffer body", new Rect(0.36f, -1.88f, 1.28f, 1.73f), 0.28f, 0.02f, height, cloth);
            Part(root, "Collar opening", new Rect(0.70f, -0.44f, 0.60f, 0.27f), 0.12f, height, height + 0.025f, lining);
            Part(root, "Zipper", new Rect(0.97f, -1.72f, 0.06f, 1.30f), 0.025f, height, height + 0.026f, seam);
            foreach (var z in new[] { -0.79f, -1.14f, -1.49f })
                Part(root, "Quilt seam", new Rect(0.46f, z, 1.08f, 0.045f), 0.02f, height, height + 0.018f, edge);
            Part(root, "Pocket", new Rect(0.53f, -1.49f, 0.31f, 0.23f), 0.08f, height, height + 0.025f, seam);
            if (compressed)
            {
                var strap = Ink(0xE7C49A);
                var buckle = Ink(0x31515D);
                foreach (var z in new[] { -0.76f, -1.42f })
                {
                    Part(root, "Compression band", new Rect(0.35f, z, 1.30f, 0.11f), 0.04f,
                        height + 0.027f, height + 0.045f, strap);
                    Part(root, "Band clasp", new Rect(1.30f, z - 0.025f, 0.18f, 0.16f), 0.04f,
                        height + 0.045f, height + 0.060f, buckle);
                }
            }
        }

        private void Shoe(GameObject root)
        {
            var sole = Ink(0xE8DDC8);
            var upper = Ink(0x5A9FA0);
            var toe = Ink(0x8BC3B9);
            var dark = Ink(0x284B55);
            var lace = Ink(0xF6EBD9);
            Part(root, "Rubber sole", new Rect(0.12f, -1.87f, 1.76f, 1.76f), 0.68f, 0f, 0.12f, sole);
            Part(root, "Shoe upper", new Rect(0.22f, -1.78f, 1.56f, 1.58f), 0.55f, 0.12f, 0.34f, upper);
            Part(root, "Rounded toe", new Rect(0.38f, -1.70f, 1.24f, 0.49f), 0.23f, 0.34f, 0.355f, toe);
            Part(root, "Open heel", new Rect(0.49f, -0.88f, 1.02f, 0.57f), 0.25f, 0.34f, 0.365f, dark);
            Part(root, "Heel lining", new Rect(0.59f, -0.78f, 0.82f, 0.40f), 0.18f, 0.365f, 0.38f, sole);
            foreach (var z in new[] { -1.08f, -1.22f })
                Part(root, "Lace", new Rect(0.68f, z, 0.64f, 0.055f), 0.02f, 0.355f, 0.375f, lace);
        }

        private void Socks(GameObject root)
        {
            var knit = Ink(0xF4E8D3);
            var cuff = Ink(0xD7AA58);
            var heel = Ink(0x73969C);
            Part(root, "Rolled socks", new Rect(0.09f, -0.91f, 0.82f, 0.82f), 0.34f, 0.01f, 0.25f, knit);
            Part(root, "Rolled cuff", new Rect(0.18f, -0.73f, 0.64f, 0.17f), 0.08f, 0.25f, 0.29f, cuff);
            Part(root, "Sock heel", new Rect(0.22f, -0.48f, 0.23f, 0.20f), 0.08f, 0.25f, 0.28f, heel);
        }

        private void Case(GameObject root)
        {
            var shell = Ink(0x9E738A);
            var edge = Ink(0x73556C);
            var clasp = Ink(0xD7AA58);
            Part(root, "Small travel case", new Rect(0.08f, -0.92f, 0.84f, 0.84f), 0.18f, 0f, 0.30f, shell);
            Part(root, "Closed lid seam", new Rect(0.13f, -0.42f, 0.74f, 0.06f), 0.025f, 0.30f, 0.32f, edge);
            Part(root, "Clasp", new Rect(0.43f, -0.48f, 0.14f, 0.12f), 0.04f, 0.32f, 0.35f, clasp);
        }

        private void Passport(GameObject root)
        {
            var cover = Ink(0xB85F4E);
            var pages = Ink(0xF4E8D3);
            var mark = Ink(0xD7AA58);
            Part(root, "Passport pages", new Rect(0.12f, -0.92f, 0.76f, 0.84f), 0.09f, 0f, 0.17f, pages);
            Part(root, "Passport cover", new Rect(0.08f, -0.94f, 0.82f, 0.88f), 0.09f, 0.17f, 0.22f, cover);
            Part(root, "Cover mark", new Rect(0.39f, -0.59f, 0.22f, 0.22f), 0.10f, 0.22f, 0.235f, mark);
        }

        private void Sweater(GameObject root, bool folded)
        {
            var cloth = Ink(0xE4A584);
            var trim = Ink(0xF3E6D0);
            var width = folded ? 2f : 3f;
            var depth = folded ? 4f : 3f;
            Part(root, "Knitted sweater", new Rect(0.12f, -depth + 0.12f, width - 0.24f, depth - 0.24f), 0.36f,
                0f, 0.30f, cloth);
            Part(root, "Neck rib", new Rect(width * 0.35f, -0.47f, width * 0.30f, 0.25f), 0.11f,
                0.30f, 0.33f, trim);
        }

        private void Fabric(GameObject root, bool folded)
        {
            var cloth = Ink(0xA7B5A6);
            Part(root, "Folded fabric", new Rect(0.10f, folded ? -3.9f : -2.9f, folded ? 1.8f : 2.8f,
                folded ? 3.8f : 2.8f), 0.28f, 0f, 0.23f, cloth);
        }

        private T Own<T>(T asset) where T : UnityEngine.Object
        {
            _owned.Add(asset);
            return asset;
        }

        public void Dispose()
        {
            foreach (var visual in _visuals.Values)
                if (visual != null)
                    UnityEngine.Object.Destroy(visual);
            _visuals.Clear();
            foreach (var asset in _owned)
                if (asset != null)
                    UnityEngine.Object.Destroy(asset);
            _owned.Clear();
        }
    }
}
