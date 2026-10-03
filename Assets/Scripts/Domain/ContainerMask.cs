using System;
using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public sealed class ContainerMask
    {
        private readonly byte[] _bits;

        public int Width { get; }
        public int Height { get; }
        public int ValidCellCount { get; }

        public ContainerMask(int width, int height, IEnumerable<Cell> validCells)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Mask width must be positive.");
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height), height, "Mask height must be positive.");
            if (validCells == null)
                throw new ArgumentNullException(nameof(validCells));

            Width = width;
            Height = height;
            _bits = new byte[checked(width * height + 7) / 8];

            var count = 0;

            foreach (var cell in validCells)
            {
                if (!IsWithinBounds(cell))
                    throw new ArgumentOutOfRangeException(nameof(validCells), cell, "Cell is outside the mask bounds.");

                var index = ToRowMajorIndex(cell);
                var byteIndex = index / 8;
                var bitIndex = index % 8;

                if ((_bits[byteIndex] & (1 << bitIndex)) == 0)
                {
                    _bits[byteIndex] |= (byte)(1 << bitIndex);
                    count++;
                }
            }

            ValidCellCount = count;
        }

        public bool IsWithinBounds(Cell cell)
        {
            return cell.X >= 0
                && cell.X < Width
                && cell.Y >= 0
                && cell.Y < Height;
        }

        public bool IsValid(Cell cell)
        {
            if (!IsWithinBounds(cell))
                return false;

            var index = ToRowMajorIndex(cell);
            var byteIndex = index / 8;
            var bitIndex = index % 8;

            return (_bits[byteIndex] & (1 << bitIndex)) != 0;
        }

        public IEnumerable<Cell> GetValidCells()
        {
            for (var y = 0; y < Height; y++)
            {
                for (var x = 0; x < Width; x++)
                {
                    var cell = new Cell(x, y);

                    if (IsValid(cell))
                        yield return cell;
                }
            }
        }

        public byte[] ToStableBytes()
        {
            var copy = new byte[_bits.Length];
            Array.Copy(_bits, copy, _bits.Length);
            return copy;
        }

        private int ToRowMajorIndex(Cell cell)
        {
            return cell.Y * Width + cell.X;
        }
    }
}
