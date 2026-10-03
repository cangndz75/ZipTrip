using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;

namespace ZipTrip.Unity
{
    // First-playable ADR-0006 item catalog (ZT-040). Footprints and rotations match the approved golden references
    // (golden-item-footprints.md); the sweater has only its open state here because Fold is not part of Lv1-Lv2
    // (ZT-045 adds the folded state and must re-run the Lv1-Lv2 solver gates). Book has no golden art yet.
    public static class PuzzleItemCatalog
    {
        private static readonly Rotation[] Upright = { Rotation.Degrees0, Rotation.Degrees90 };
        private static readonly Rotation[] All = { Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270 };

        public static IReadOnlyList<ItemSpec> Create() => new[]
        {
            Single("laptop", "open", Rect(3, 4), Upright, "tech"),
            Single("sweater", "open", Rect(3, 3), new[] { Rotation.Degrees0 }, "clothes", "soft"),
            Single("sneaker", "open", new ItemShape(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2) }), All, "shoes"),
            Single("book", "open", Rect(2, 3), Upright)
        };

        /// <summary>Golden prefab lookup for a v2 item; null when the item has no golden art (fallback blocks).</summary>
        public static GameObject ResolveGolden(GoldenItemPrefabCatalog catalog, string definitionId, string stateId)
        {
            if (catalog == null)
                return null;
            switch (definitionId)
            {
                case "laptop": return catalog.Resolve("PF_Item_Laptop", stateId);
                case "sneaker": return catalog.Resolve("PF_Item_SneakerPair", stateId);
                case "sweater": return catalog.Resolve("PF_Item_Sweater", stateId);
                default: return null;
            }
        }

        private static ItemShape Rect(int width, int height) =>
            new ItemShape(from y in Enumerable.Range(0, height) from x in Enumerable.Range(0, width) select new Cell(x, y));

        private static ItemSpec Single(string id, string stateId, ItemShape footprint, Rotation[] rotations, params string[] tags) =>
            new ItemSpec(id, stateId, new[] { new ItemStateSpec(stateId, footprint, 1, rotations) }, tags);
    }
}
