using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // Pure presentation math for the ADR-0006 board: Domain coordinates in, local positions out. Never gameplay values.
    // Same grid convention as GridProjector: cell (x, y) spans [x, x + 1] on X and [-y - 1, -y] on Z.
    public static class PuzzleBoardLayout
    {
        public const float CellSize = 1f;
        // Visual elevation of one Domain layer; a thickness-1 item is slightly shorter so stacks stay readable.
        public const float LayerHeight = 0.36f;
        public const float ItemHeightRatio = 0.86f;
        // Empty cells between compartments laid out side by side.
        public const float CompartmentGap = 1f;

        // Compartments in BoardSpec order (ordinal id), left to right, top edges aligned at z = 0.
        public static Dictionary<string, Vector3> CompartmentOrigins(BoardSpec board)
        {
            var origins = new Dictionary<string, Vector3>();
            var x = 0f;
            foreach (var compartment in board.Compartments)
            {
                origins.Add(compartment.Id, new Vector3(x, 0f, 0f));
                x += compartment.Width * CellSize + CompartmentGap;
            }
            return origins;
        }

        // Item root inside its compartment: the normalized footprint anchor (ADR-0003) at the base layer's elevation.
        public static Vector3 ItemLocalPosition(Placement placement) =>
            new Vector3(placement.Anchor.X * CellSize, placement.Layer * LayerHeight, -placement.Anchor.Y * CellSize);

        // Centre of a footprint cell relative to the item root.
        public static Vector3 FootprintCellCenter(Cell cell) =>
            new Vector3((cell.X + 0.5f) * CellSize, 0f, -(cell.Y + 0.5f) * CellSize);

        // Centre of a compartment column relative to the compartment origin.
        public static Vector3 ColumnCenter(Cell column) => FootprintCellCenter(column);

        public static float ItemHeight(int thickness) => thickness * LayerHeight * ItemHeightRatio;
    }
}
