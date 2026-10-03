using System.Collections.Generic;
using System.Linq;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // ZT-045 only: authored modifier and containment contracts through the schema v2 loader.
    public static class ModifierFixture
    {
        private static readonly Rotation[] All =
            { Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270 };

        private static ItemShape Rect(int width, int height) => new ItemShape(
            from y in Enumerable.Range(0, height) from x in Enumerable.Range(0, width) select new Cell(x, y));

        private static IReadOnlyList<ItemSpec> Catalog() => new[]
        {
            new ItemSpec("sweater", "open", new[]
            {
                new ItemStateSpec("open", Rect(3, 3), 1, All),
                new ItemStateSpec("folded", Rect(2, 4), 1, All)
            }, new[] { "clothes" }, new[]
            {
                new StateTransition(ItemModifier.Fold, "open", "folded"),
                new StateTransition(ItemModifier.Fold, "folded", "open")
            }),
            new ItemSpec("jacket", "normal", new[]
            {
                new ItemStateSpec("normal", Rect(2, 2), 2, All),
                new ItemStateSpec("compressed", Rect(2, 2), 1, All)
            }, new[] { "clothes" }, new[] { new StateTransition(ItemModifier.Compress, "normal", "compressed") }),
            new ItemSpec("fabric", "open", new[]
            {
                new ItemStateSpec("open", new ItemShape(from y in Enumerable.Range(0, 3) from x in Enumerable.Range(0, 3)
                    where x != 2 || y != 2 select new Cell(x, y)), 1, All),
                new ItemStateSpec("folded", Rect(2, 4), 1, All)
            }, transitions: new[]
            {
                new StateTransition(ItemModifier.Fold, "open", "folded"),
                new StateTransition(ItemModifier.Fold, "folded", "open")
            }),
            new ItemSpec("shoe", "open", new[] { new ItemStateSpec("open", Rect(2, 2), 1, All) },
                nest: new NestSpec(1, new[] { "socks" }, null)),
            new ItemSpec("socks", "open", new[] { new ItemStateSpec("open", Rect(1, 1), 1, All) }),
            new ItemSpec("box", "open", new[] { new ItemStateSpec("open", Rect(1, 1), 1, All) },
                nest: new NestSpec(1, new[] { "book" }, null)),
            new ItemSpec("book", "open", new[] { new ItemStateSpec("open", Rect(1, 1), 1, All) })
        };

        private static string Board(int layers) => "\"board\":{\"compartments\":[{\"id\":\"main\",\"width\":5,\"height\":7,\"layers\":"
            + layers + ",\"rows\":[\"#####\",\"#####\",\"#####\",\"#####\",\"#####\",\"#####\",\"#####\"]}]},";

        private static string Level(string id, int layers, int staging, string items, string required) =>
            "{\"schemaVersion\":2,\"id\":\"" + id + "\"," + Board(layers) + "\"stagingCapacity\":" + staging
            + ",\"items\":[" + items + "],\"rules\":[],\"objective\":{\"profile\":\"pack\",\"requiredInstanceIds\":["
            + required + "]}}";

        private const string Tray = "{\"kind\":\"sourceTray\"}";
        private const string Stage = "{\"kind\":\"staging\",\"slot\":0}";
        private static string BoardAt(int x, int y) => "{\"kind\":\"suitcase\",\"compartmentId\":\"main\",\"x\":" + x
            + ",\"y\":" + y + ",\"layer\":0,\"rotation\":0}";
        private static string Item(string id, string definition, string location) => "{\"id\":\"" + id + "\",\"definitionId\":\""
            + definition + "\",\"role\":\"required\",\"location\":" + location + "}";

        public static PuzzleLevel Fold(bool completion = false) => LevelJsonLoaderV2.Load(Level("zt045-fold", 1, 0,
            Item("sweater-1", "sweater", Tray) + (completion ? "" : "," + Item("book-2", "book", Tray)),
            "\"sweater-1\"" + (completion ? "" : ",\"book-2\"")), Catalog());

        public static PuzzleLevel Compress() => LevelJsonLoaderV2.Load(Level("zt045-compress", 1, 0,
            Item("jacket-1", "jacket", Tray) + "," + Item("book-2", "book", Tray),
            "\"jacket-1\",\"book-2\""), Catalog());

        public static PuzzleLevel FoldRotation() => LevelJsonLoaderV2.Load(Level("zt045-fold-rotation", 1, 0,
            Item("fabric-1", "fabric", Tray) + "," + Item("book-2", "book", Tray),
            "\"fabric-1\",\"book-2\""), Catalog());

        public static PuzzleLevel FoldStaged() => LevelJsonLoaderV2.Load(Level("zt045-fold-staged", 1, 1,
            Item("sweater-1", "sweater", Stage) + "," + Item("book-2", "book", Tray),
            "\"sweater-1\",\"book-2\""), Catalog());

        public static PuzzleLevel Nest(bool full = false) => LevelJsonLoaderV2.Load(Level("zt045-nest", 1, 1,
            Item("shoe-1", "shoe", BoardAt(0, 0)) + "," + Item("socks-1", "socks", Stage) + ","
            + Item("box-1", "box", BoardAt(3, 0)) + "," + Item("book-2", "book", Tray)
            + (full ? "," + Item("socks-2", "socks", "{\"kind\":\"nested\",\"parentId\":\"shoe-1\"}") : ""),
            "\"shoe-1\",\"socks-1\",\"box-1\",\"book-2\"" + (full ? ",\"socks-2\"" : "")), Catalog());
    }
}
