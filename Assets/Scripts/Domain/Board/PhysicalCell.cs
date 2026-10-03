using System;

namespace ZipTrip.Domain.Board
{
    /// <summary>One physical cell of a compartment: XY column plus layer z (ADR-0006 Decision 12).</summary>
    public readonly struct PhysicalCell : IEquatable<PhysicalCell>
    {
        public string Compartment { get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public PhysicalCell(string compartment, int x, int y, int z)
        {
            Compartment = compartment ?? throw new ArgumentNullException(nameof(compartment));
            X = x;
            Y = y;
            Z = z;
        }

        public PhysicalCell(string compartment, Cell column, int z)
            : this(compartment, column.X, column.Y, z)
        {
        }

        public Cell Column => new Cell(X, Y);

        public bool Equals(PhysicalCell other)
        {
            return string.Equals(Compartment, other.Compartment, StringComparison.Ordinal)
                && X == other.X
                && Y == other.Y
                && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is PhysicalCell other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Compartment == null ? 0 : StringComparer.Ordinal.GetHashCode(Compartment);
                hash = (hash * 397) ^ X;
                hash = (hash * 397) ^ Y;
                return (hash * 397) ^ Z;
            }
        }

        public static bool operator ==(PhysicalCell left, PhysicalCell right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PhysicalCell left, PhysicalCell right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"{Compartment}({X},{Y},{Z})";
        }
    }
}
