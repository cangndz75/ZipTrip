using System;
using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public sealed class ContainerMask
    {
        private const int ByteCount = (GridSize.CellCount + 7) / 8;
        private readonly byte[] _bits;

        public int ValidCellCount { get; }

        public ContainerMask(IEnumerable<Cell> validCells)
        {
            if (validCells == null)
                throw new ArgumentNullException(nameof(validCells));

            _bits = new byte[ByteCount];

            var count = 0;

            foreach (var cell in validCells)
            {
                if (!GridSize.IsWithinBounds(cell))
                    throw new ArgumentOutOfRangeException(nameof(validCells), cell, "Cell is outside the canonical grid.");

                var index = GridSize.ToRowMajorIndex(cell);
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

        public bool IsValid(Cell cell)
        {
            if (!GridSize.IsWithinBounds(cell))
                return false;

            var index = GridSize.ToRowMajorIndex(cell);
            var byteIndex = index / 8;
            var bitIndex = index % 8;

            return (_bits[byteIndex] & (1 << bitIndex)) != 0;
        }

        public IEnumerable<Cell> GetValidCells()
        {
            for (var y = 0; y < GridSize.Height; y++)
            {
                for (var x = 0; x < GridSize.Width; x++)
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
    }
}
