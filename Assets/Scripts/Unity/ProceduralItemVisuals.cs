using System.Collections.Generic;
using UnityEngine;

namespace ZipTrip.Unity
{
    // ZT-040B: simple procedural visuals for first-playable items that have no golden art yet, so Lv1-Lv2 never show
    // grid-block placeholders. Each visual is built once per material template as an inactive, hidden template in the
    // item's authored footprint frame (x right, -z down, base at y = 0) and is then used exactly like a golden prefab
    // (instantiated and rotated by PuzzleItemView). Presentation only; footprints and gameplay are unchanged.
    public static class ProceduralItemVisuals
    {
        private static readonly Dictionary<string, GameObject> Templates = new Dictionary<string, GameObject>();

        /// <summary>Procedural visual for a definition without golden art, or null when there is none.</summary>
        public static GameObject Resolve(string definitionId, Material template)
        {
            if (definitionId != "book")
                return null;
            template = PresentationKit.TemplateOrFallback(template);
            if (template == null)
                return null;
            var key = definitionId + "|" + template.GetInstanceID();
            if (Templates.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var built = Book(template);
            Templates[key] = built;
            return built;
        }

        // Closed hardback lying flat inside the approved 2 x 3 footprint: coral covers, cream page block, darker spine
        // on the left, a cream title band and a mustard ribbon marker. 0.27 high, below one layer (0.36).
        private static GameObject Book(Material template)
        {
            var root = new GameObject("PF_Item_Book_Procedural") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            var cover = PresentationKit.Matte(template, PresentationKit.Hex(0xC9604F), null, 0.22f);
            var spine = PresentationKit.Matte(template, PresentationKit.Hex(0xA34A3D), null, 0.22f);
            var pages = PresentationKit.Matte(template, PresentationKit.Paper, null, 0.08f);
            var band = PresentationKit.Matte(template, PresentationKit.TrayCard, null, 0.1f);
            var ribbon = PresentationKit.Matte(template, PresentationKit.Mustard, null, 0.3f);

            Part(root, "Back Cover", new Rect(0.07f, -2.93f, 1.86f, 2.86f), 0.1f, 0f, 0.05f, cover);
            Part(root, "Pages", new Rect(0.2f, -2.86f, 1.68f, 2.72f), 0.04f, 0.05f, 0.215f, pages);
            Part(root, "Front Cover", new Rect(0.07f, -2.93f, 1.86f, 2.86f), 0.1f, 0.215f, 0.265f, cover);
            Part(root, "Spine", new Rect(0.05f, -2.95f, 0.3f, 2.9f), 0.09f, 0f, 0.27f, spine);
            Part(root, "Title Band", new Rect(0.62f, -1.15f, 1.0f, 0.3f), 0.06f, 0.265f, 0.272f, band);
            Part(root, "Ribbon", new Rect(1.5f, -0.5f, 0.12f, 0.46f), 0.03f, 0.265f, 0.275f, ribbon);
            return root;
        }

        private static void Part(GameObject root, string name, Rect rect, float radius, float bottom, float top, Material material)
        {
            var go = PresentationKit.MeshObject(name, root.transform, PresentationKit.Slab(rect, radius, bottom, top), material);
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
