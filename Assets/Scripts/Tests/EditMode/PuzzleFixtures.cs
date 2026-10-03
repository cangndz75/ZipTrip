using System.Collections.Generic;
using System.Linq;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Tests.EditMode
{
    /// <summary>Small shared fixtures for the ADR-0006 puzzle model tests.</summary>
    internal static class PuzzleFixtures
    {
        public static readonly Rotation[] AllRotations =
            { Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270 };

        public static IEnumerable<Cell> RectCells(int width, int height) =>
            from y in Enumerable.Range(0, height) from x in Enumerable.Range(0, width) select new Cell(x, y);

        public static ItemShape Rect(int width, int height) => new ItemShape(RectCells(width, height));

        public static ItemSpec Block(string id, int width, int height, int thickness = 1, NestSpec nest = null,
            params string[] tags) =>
            new ItemSpec(id, "default", new[] { new ItemStateSpec("default", Rect(width, height), thickness, AllRotations) },
                tags, null, nest);

        /// <summary>Open 3x3 / folded 2x4 sweater, thickness 1.</summary>
        public static ItemSpec Sweater() => new ItemSpec("sweater", "open",
            new[]
            {
                new ItemStateSpec("open", Rect(3, 3), 1, AllRotations),
                new ItemStateSpec("folded", Rect(2, 4), 1, AllRotations)
            },
            new[] { "clothes" },
            new[]
            {
                new StateTransition(ItemModifier.Fold, "open", "folded"),
                new StateTransition(ItemModifier.Fold, "folded", "open")
            });

        /// <summary>2x2 jacket: thickness 2, compressed thickness 1.</summary>
        public static ItemSpec Jacket() => new ItemSpec("jacket", "normal",
            new[]
            {
                new ItemStateSpec("normal", Rect(2, 2), 2, AllRotations),
                new ItemStateSpec("compressed", Rect(2, 2), 1, AllRotations)
            },
            new[] { "clothes" },
            new[] { new StateTransition(ItemModifier.Compress, "normal", "compressed") });

        /// <summary>Main compartment 4x3, L = 2, plus a 2x1 pocket with L = 1.</summary>
        public static BoardSpec Board(int width = 4, int height = 3) => new BoardSpec(new[]
        {
            new Compartment("main", width, height, 2, RectCells(width, height)),
            new Compartment("pocket", 2, 1, 1, RectCells(2, 1))
        });

        public static ExtractionDestinationSpec Tray() =>
            new ExtractionDestinationSpec("tray", 1, acceptedRoles: new[] { ObjectiveRole.ExtractionTarget });

        public static PuzzleSpec Spec(int stagingCapacity = 2, BoardSpec board = null) =>
            new PuzzleSpec(board ?? Board(), stagingCapacity, new[] { Tray() });

        public static Placement At(int x, int y, int layer = 0, Rotation rotation = Rotation.Degrees0, string compartment = "main") =>
            new Placement(compartment, new Cell(x, y), layer, rotation);

        public static PuzzleItem Item(string id, ItemSpec definition, ItemLocation location,
            ObjectiveRole role = ObjectiveRole.Required, string stateId = null) =>
            new PuzzleItem(id, definition, stateId ?? definition.DefaultStateId, role, location);

        public static PuzzleItem Placed(string id, ItemSpec definition, Placement placement,
            ObjectiveRole role = ObjectiveRole.Required, string stateId = null) =>
            Item(id, definition, ItemLocation.InSuitcase(placement), role, stateId);

        public static PuzzleState State(PuzzleSpec spec, params PuzzleItem[] items) => new PuzzleState(spec, items);
    }
}
