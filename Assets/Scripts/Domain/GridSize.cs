namespace ZipTrip.Domain
{
    public static class GridSize
    {
        public const int Width = 8;
        public const int Height = 10;
        public const int CellCount = Width * Height;

        public static bool IsWithinBounds(Cell cell)
        {
            return cell.X >= 0
                && cell.X < Width
                && cell.Y >= 0
                && cell.Y < Height;
        }

        public static int ToRowMajorIndex(Cell cell)
        {
            return cell.Y * Width + cell.X;
        }
    }
}
