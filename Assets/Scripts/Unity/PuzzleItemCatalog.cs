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
    // GOLDEN-LV1-SHIP: the Golden Lv1 roster (golden-lv1-content-lock.md). Passport 1x2 and towel 1x4 are Blueprint Ek A
    // canonical; the towel is open only (its folded state is deferred so Lv1 needs no Fold).
    public static class PuzzleItemCatalog
    {
        public static MaterialMotionProfile MetalGlassMotion =>
            new MaterialMotionProfile(MaterialFamily.MetalGlass, 1.03f, 1.02f, 0.005f, 0f, MotionTokens.MetalSettleDuration);

        public static MaterialMotionProfile ResolveMotion(string definitionId)
        {
            switch (definitionId)
            {
                case "sweater":
                case "towel": return new MaterialMotionProfile(MaterialFamily.Fabric, 1.055f, 1.035f, 0.05f, 0.045f, MotionTokens.FabricSettleDuration);
                case "travel-pouch": return new MaterialMotionProfile(MaterialFamily.Leather, 1.04f, 1.025f, 0.02f, 0.012f, MotionTokens.LeatherSettleDuration);
                case "shampoo":
                case "sunglasses": return new MaterialMotionProfile(MaterialFamily.Plastic, 1.06f, 1.04f, 0.025f, 0.035f, MotionTokens.PlasticSettleDuration);
                case "passport": return new MaterialMotionProfile(MaterialFamily.Paper, 1.035f, 1.025f, 0.01f, 0.006f, MotionTokens.PaperSettleDuration);
                default: return new MaterialMotionProfile(MaterialFamily.Neutral, ItemFeedback.LiftScale, ItemFeedback.HeldScale, 0.08f, 0.025f, MotionTokens.ItemSettleDuration);
            }
        }
        private static readonly Rotation[] Upright = { Rotation.Degrees0, Rotation.Degrees90 };
        private static readonly Rotation[] All = { Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270 };

        public static IReadOnlyList<ItemSpec> Create() => new[]
        {
            Single("laptop", "open", Rect(3, 4), Upright, "tech"),
            Single("sweater", "open", Rect(3, 3), new[] { Rotation.Degrees0 }, "clothes", "soft"),
            Single("sneaker", "open", new ItemShape(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2) }), All, "shoes"),
            Single("book", "open", Rect(2, 3), Upright),
            Single("passport", "open", Rect(1, 2), Upright, "documents"),
            Single("towel", "open", Rect(1, 4), Upright, "soft"),
            Single("shampoo", "open", Rect(1, 3), Upright, "liquid", "toiletries"),
            Single("sunglasses", "open", Rect(2, 1), Upright, "fragile"),
            Single("travel-pouch", "open", Rect(2, 3), Upright, "toiletries")
        };

        /// <summary>Golden prefab lookup for a v2 item; null when the item has no golden art (fallback blocks).</summary>
        public static GameObject ResolveGolden(GoldenItemPrefabCatalog catalog, string definitionId, string stateId)
        {
            if (catalog == null)
                return null;
            switch (definitionId)
            {
                case "laptop": return catalog.Resolve("PF_Item_Laptop", stateId);
                case "passport": return catalog.Resolve("PF_Item_Passport", stateId);
                case "towel": return catalog.Resolve("PF_Item_Towel", stateId);
                case "shampoo": return catalog.Resolve("PF_Item_Shampoo", stateId);
                case "sunglasses": return catalog.Resolve("PF_Item_Sunglasses", stateId);
                case "travel-pouch": return catalog.Resolve("PF_Item_TravelPouch", stateId);
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
