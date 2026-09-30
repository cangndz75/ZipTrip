using System;

namespace ZipTrip.Domain
{
    public readonly struct TrayItem : IEquatable<TrayItem>
    {
        public string ItemId { get; }
        public Rotation Rotation { get; }
        public string ShapeState { get; }

        public TrayItem(string itemId, Rotation rotation, string shapeState)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("Item id is required.", nameof(itemId));
            if (!Enum.IsDefined(typeof(Rotation), rotation))
                throw new ArgumentOutOfRangeException(nameof(rotation));
            if (string.IsNullOrWhiteSpace(shapeState))
                throw new ArgumentException("Shape state is required.", nameof(shapeState));

            ItemId = itemId;
            Rotation = rotation;
            ShapeState = shapeState;
        }

        public bool Equals(TrayItem other) =>
            StringComparer.Ordinal.Equals(ItemId, other.ItemId) &&
            Rotation == other.Rotation &&
            StringComparer.Ordinal.Equals(ShapeState, other.ShapeState);

        public override bool Equals(object obj) => obj is TrayItem other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in ItemId ?? string.Empty)
                    hash = (hash ^ c) * 16777619u;
                hash = (hash ^ (uint)Rotation) * 16777619u;
                foreach (var c in ShapeState ?? string.Empty)
                    hash = (hash ^ c) * 16777619u;
                return (int)hash;
            }
        }
    }
}
