using System;
using System.Collections.Generic;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    /// <summary>Approved Phase A footprints; level-specific Fold availability is projected from these definitions.</summary>
    public static class PhaseAItemCatalog
    {
        public static IReadOnlyList<ItemDefinition> Create(bool allowFold)
        {
            var items = new List<ItemDefinition>
            {
                Rigid("book", Rectangle(2, 3), Rotation.Degrees0, Rotation.Degrees90),
                Rigid("bottle", Rectangle(1, 3), Rotation.Degrees0, Rotation.Degrees90),
                Rigid("camera", Rectangle(2, 2), Rotation.Degrees0),
                Rigid("laptop", Rectangle(3, 4), Rotation.Degrees0, Rotation.Degrees90),
                Soft("pants", Rectangle(2, 5), Rectangle(3, 3), allowFold),
                Rigid("passport", Rectangle(1, 2), Rotation.Degrees0, Rotation.Degrees90),
                Soft("scarf", Rectangle(1, 6), Rectangle(2, 3), allowFold),
                Rigid("sneaker", new ItemShape(new[]
                {
                    new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2)
                }), Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270),
                Soft("sweater", Rectangle(3, 3), Rectangle(2, 4), allowFold),
                Soft("towel", Rectangle(1, 4), Rectangle(2, 2), allowFold)
            };
            return items.AsReadOnly();
        }

        private static ItemDefinition Rigid(string id, ItemShape shape, params Rotation[] rotations) =>
            new ItemDefinition(id, "open", new[]
            {
                new KeyValuePair<string, ItemShape>("open", shape)
            }, rotations, Array.Empty<string>(), authoredShapeStateIds: new[] { "open" });

        private static ItemDefinition Soft(string id, ItemShape open, ItemShape folded, bool allowFold)
        {
            var states = new List<KeyValuePair<string, ItemShape>>
            {
                new KeyValuePair<string, ItemShape>("open", open)
            };
            var authoredOrder = new List<string> { "open" };
            if (allowFold)
            {
                states.Add(new KeyValuePair<string, ItemShape>("folded", folded));
                authoredOrder.Add("folded");
            }
            return new ItemDefinition(id, "open", states,
                new[] { Rotation.Degrees0, Rotation.Degrees90 }, Array.Empty<string>(),
                authoredShapeStateIds: authoredOrder);
        }

        private static ItemShape Rectangle(int width, int height)
        {
            var cells = new List<Cell>();
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    cells.Add(new Cell(x, y));
            return new ItemShape(cells);
        }
    }
}
