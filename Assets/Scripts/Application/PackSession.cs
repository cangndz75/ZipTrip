using System;
using System.Collections.Generic;
using ZipTrip.Domain;

namespace ZipTrip.Application
{
    /// <summary>Application entry point for Pack actions. GameSession owns undo history.</summary>
    public sealed class PackSession
    {
        private GameSession _session;
        private readonly GameState _initialState;
        private readonly Dictionary<string, ItemDefinition> _items;

        public GameState State => _session.State;
        public int UndoDepth => _session.UndoDepth;

        public PackSession(GameState initialState, IEnumerable<ItemDefinition> items)
        {
            _initialState = initialState ?? throw new ArgumentNullException(nameof(initialState));
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            _items = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null || !_items.TryAdd(item.Id, item))
                    throw new ArgumentException("Definitions require unique non-null item ids.", nameof(items));
            }
            foreach (var trayItem in initialState.Tray)
            {
                if (!_items.TryGetValue(trayItem.ItemId, out var item) ||
                    !item.ShapeStates.ContainsKey(trayItem.ShapeState) ||
                    !item.GetRotatedShape(trayItem.ShapeState, trayItem.Rotation).IsAccepted)
                    throw new ArgumentException("Tray item requires a matching definition and valid state.", nameof(initialState));
            }
            foreach (var placement in initialState.Placements)
            {
                if (!_items.TryGetValue(placement.ItemId, out var item) ||
                    !item.ShapeStates.ContainsKey(placement.ShapeState) ||
                    !item.GetRotatedShape(placement.ShapeState, placement.Rotation).IsAccepted)
                    throw new ArgumentException("Placement requires a matching definition and valid state.", nameof(initialState));
            }
            _session = new GameSession(initialState);
        }

        public CommandResult PlaceItem(string itemId, Cell anchor, Rotation rotation, string shapeState) =>
            Execute(state =>
            {
                var slot = FindTray(state, itemId);
                if (slot < 0)
                    return CommandResult.Rejected("ItemNotInTray");
                var current = state.Tray[slot];
                if (!StringComparer.Ordinal.Equals(shapeState, current.ShapeState))
                    return CommandResult.Rejected("ShapeStateMismatch");
                var item = _items[itemId];
                var rejection = ValidateShape(item, shapeState, rotation);
                if (rejection != null)
                    return CommandResult.Rejected(rejection);
                var validation = PlacementValidator.Validate(Board(state, null), item, anchor, rotation, shapeState);
                if (!validation.IsValid)
                    return CommandResult.Rejected(validation.Reason.ToString());

                var placements = new List<PlacedItem>(state.Placements)
                {
                    new PlacedItem(item, anchor, rotation, shapeState)
                };
                var tray = new List<TrayItem>(state.Tray);
                tray.RemoveAt(slot);
                var next = Next(state, placements, tray);
                return Accepted(state, next, new ItemPlacedEvent(itemId, anchor));
            });

        public CommandResult MoveItem(string itemId, Cell anchor) => Execute(state =>
        {
            var current = FindPlaced(state, itemId);
            if (current == null)
                return CommandResult.Rejected("ItemNotPlaced");
            var item = _items[itemId];
            var validation = PlacementValidator.Validate(Board(state, itemId), item,
                anchor, current.Rotation, current.ShapeState);
            if (!validation.IsValid)
                return CommandResult.Rejected(validation.Reason.ToString());

            var placements = WithoutPlacement(state, itemId);
            placements.Add(new PlacedItem(item, anchor, current.Rotation, current.ShapeState));
            return Accepted(state, Next(state, placements, state.Tray));
        });

        public CommandResult RotateItem(string itemId, Rotation rotation) => Execute(state =>
        {
            var slot = FindTray(state, itemId);
            var current = FindPlaced(state, itemId);
            if (slot < 0 && current == null)
                return CommandResult.Rejected("ItemNotFound");
            var item = _items[itemId];
            var shapeState = slot >= 0 ? state.Tray[slot].ShapeState : current.ShapeState;
            var rejection = ValidateShape(item, shapeState, rotation);
            if (rejection != null)
                return CommandResult.Rejected(rejection);

            GameState next;
            if (slot >= 0)
            {
                var tray = new List<TrayItem>(state.Tray);
                tray[slot] = new TrayItem(itemId, rotation, shapeState);
                next = Next(state, state.Placements, tray);
            }
            else
            {
                var validation = PlacementValidator.Validate(Board(state, itemId), item,
                    current.Anchor, rotation, shapeState);
                if (!validation.IsValid)
                    return CommandResult.Rejected(validation.Reason.ToString());
                var placements = WithoutPlacement(state, itemId);
                placements.Add(new PlacedItem(item, current.Anchor, rotation, shapeState));
                next = Next(state, placements, state.Tray);
            }
            return Accepted(state, next, new ItemRotatedEvent(itemId, rotation));
        });

        public CommandResult FoldItem(string itemId, string shapeState) => Execute(state =>
        {
            if (FindPlaced(state, itemId) != null)
                return CommandResult.Rejected("FoldOnlyInTray");
            var slot = FindTray(state, itemId);
            if (slot < 0)
                return CommandResult.Rejected("ItemNotInTray");
            var item = _items[itemId];
            if (item.ShapeStates.Count < 2)
                return CommandResult.Rejected("FoldNotAvailable");
            if (string.IsNullOrWhiteSpace(shapeState) || !item.ShapeStates.ContainsKey(shapeState))
                return CommandResult.Rejected("ShapeStateNotFound");
            var current = state.Tray[slot];
            var tray = new List<TrayItem>(state.Tray);
            tray[slot] = new TrayItem(itemId, current.Rotation, shapeState);
            return Accepted(state, Next(state, state.Placements, tray),
                new FoldChangedEvent(itemId, shapeState));
        });

        public CommandResult ReturnToTray(string itemId) => Execute(state =>
        {
            var current = FindPlaced(state, itemId);
            if (current == null)
                return CommandResult.Rejected("ItemNotPlaced");
            // Pack tray is an ordered sequence: returned items append after current items.
            var tray = new List<TrayItem>(state.Tray)
            {
                new TrayItem(itemId, current.Rotation, current.ShapeState)
            };
            return Accepted(state, Next(state, WithoutPlacement(state, itemId), tray),
                new ItemReturnedEvent(itemId));
        });

        public CommandResult Undo() => _session.Undo()
            ? CommandResult.Accepted(_session.State, Array.Empty<IGameEvent>())
            : CommandResult.Rejected("NoUndoAvailable");

        public CommandResult ResetLevel()
        {
            // A new session restores the initial immutable state and discards all prior history.
            _session = new GameSession(_initialState);
            return CommandResult.Accepted(_session.State, Array.Empty<IGameEvent>());
        }

        private CommandResult Execute(Func<GameState, CommandResult> operation) =>
            _session.Execute(new PackCommand(operation));

        private static string ValidateShape(ItemDefinition item, string shapeState, Rotation rotation)
        {
            if (string.IsNullOrWhiteSpace(shapeState) || !item.ShapeStates.ContainsKey(shapeState))
                return "ShapeStateNotFound";
            return item.GetRotatedShape(shapeState, rotation).IsAccepted ? null : "RotationNotAllowed";
        }

        private static PlacementBoard Board(GameState state, string excludedId)
        {
            var cells = new List<Cell>();
            foreach (var placement in state.Placements)
                if (!StringComparer.Ordinal.Equals(placement.ItemId, excludedId))
                    cells.AddRange(placement.OccupiedCells);
            return new PlacementBoard(state.Container, cells);
        }

        private static GameState Next(GameState state, IEnumerable<PlacedItem> placements,
            IEnumerable<TrayItem> tray) => new GameState(state.Container, placements, tray, state.Targets);

        private static List<PlacedItem> WithoutPlacement(GameState state, string itemId)
        {
            var remaining = new List<PlacedItem>();
            foreach (var placement in state.Placements)
                if (!StringComparer.Ordinal.Equals(placement.ItemId, itemId))
                    remaining.Add(placement);
            return remaining;
        }

        private static PlacedItem FindPlaced(GameState state, string itemId)
        {
            foreach (var placement in state.Placements)
                if (StringComparer.Ordinal.Equals(placement.ItemId, itemId))
                    return placement;
            return null;
        }

        private static int FindTray(GameState state, string itemId)
        {
            for (var i = 0; i < state.Tray.Count; i++)
                if (StringComparer.Ordinal.Equals(state.Tray[i].ItemId, itemId))
                    return i;
            return -1;
        }

        private static CommandResult Accepted(GameState previous, GameState next,
            IGameEvent gameEvent = null)
        {
            var events = new List<IGameEvent>();
            if (gameEvent != null)
                events.Add(gameEvent);
            if (!IsComplete(previous) && IsComplete(next))
                events.Add(new LevelCompletedEvent());
            return CommandResult.Accepted(next, events);
        }

        private static bool IsComplete(GameState state)
        {
            if (state.Tray.Count != 0)
                return false;
            var placed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in state.Placements)
                placed.Add(placement.ItemId);
            foreach (var target in state.Targets)
                if (!placed.Contains(target))
                    return false;
            return true;
        }

        private sealed class PackCommand : ICommand
        {
            private readonly Func<GameState, CommandResult> _operation;
            public PackCommand(Func<GameState, CommandResult> operation) { _operation = operation; }
            public CommandResult Execute(GameState state) => _operation(state);
        }
    }
}
