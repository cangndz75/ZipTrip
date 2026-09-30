using System;

namespace ZipTrip.Domain
{
    public interface IGameEvent { }

    public sealed class ItemPlacedEvent : IGameEvent
    {
        public string ItemId { get; }
        public Cell Anchor { get; }

        public ItemPlacedEvent(string itemId, Cell anchor)
        {
            ItemId = RequiredId(itemId);
            Anchor = anchor;
        }

        internal static string RequiredId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("Item id is required.", nameof(itemId));
            return itemId;
        }
    }

    public sealed class ItemRotatedEvent : IGameEvent
    {
        public string ItemId { get; }
        public Rotation Rotation { get; }

        public ItemRotatedEvent(string itemId, Rotation rotation)
        {
            ItemId = ItemPlacedEvent.RequiredId(itemId);
            if (!Enum.IsDefined(typeof(Rotation), rotation))
                throw new ArgumentOutOfRangeException(nameof(rotation));
            Rotation = rotation;
        }
    }

    public sealed class FoldChangedEvent : IGameEvent
    {
        public string ItemId { get; }
        public string ShapeState { get; }

        public FoldChangedEvent(string itemId, string shapeState)
        {
            ItemId = ItemPlacedEvent.RequiredId(itemId);
            if (string.IsNullOrWhiteSpace(shapeState))
                throw new ArgumentException("Shape state is required.", nameof(shapeState));
            ShapeState = shapeState;
        }
    }

    public sealed class ItemReturnedEvent : IGameEvent
    {
        public string ItemId { get; }

        public ItemReturnedEvent(string itemId)
        {
            ItemId = ItemPlacedEvent.RequiredId(itemId);
        }
    }

    public sealed class LevelCompletedEvent : IGameEvent { }
}
