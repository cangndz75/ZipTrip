using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ZipTrip.Domain
{
    public readonly struct LevelBoosters
    {
        public int VacuumCount { get; }

        public LevelBoosters(int vacuumCount)
        {
            if (vacuumCount < 0)
                throw new ArgumentException("InvalidBoosterValue: vacuumCount must be non-negative.",
                    nameof(vacuumCount));
            VacuumCount = vacuumCount;
        }
    }

    public sealed class LevelDefinition
    {
        public const int SupportedSchemaVersion = 1;
        public const int SupportedMetricVersion = 1;

        public int SchemaVersion { get; }
        public int MetricVersion { get; }
        public string Id { get; }
        public string Grammar { get; }
        public string ContainerId => InitialState.Container.Id;
        public IReadOnlyList<PlacedItem> Preplaced => InitialState.Placements;
        public IReadOnlyList<TrayItem> Inventory => InitialState.Tray;
        public IReadOnlyList<string> Targets => InitialState.Targets;
        public LevelBoosters Boosters { get; }
        public IReadOnlyDictionary<string, ItemDefinition> Items { get; }
        public GameState InitialState { get; }

        public LevelDefinition(int schemaVersion, int metricVersion, string id, string grammar,
            ContainerDefinition container, IEnumerable<ItemDefinition> items,
            IEnumerable<PlacedItem> preplaced, IEnumerable<TrayItem> inventory,
            IEnumerable<string> targets, LevelBoosters boosters)
        {
            if (schemaVersion != SupportedSchemaVersion)
                throw new ArgumentException("UnsupportedSchemaVersion", nameof(schemaVersion));
            if (metricVersion != SupportedMetricVersion)
                throw new ArgumentException("UnsupportedMetricVersion", nameof(metricVersion));
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("MissingLevelId", nameof(id));
            if (string.IsNullOrWhiteSpace(grammar))
                throw new ArgumentException("MissingGrammar", nameof(grammar));
            if (container == null)
                throw new ArgumentException("MissingContainerId", nameof(container));
            if (items == null || preplaced == null || inventory == null || targets == null)
                throw new ArgumentException("MalformedRequiredField");

            var definitions = new SortedDictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null || definitions.ContainsKey(item.Id))
                    throw new ArgumentException("DuplicateOrNullItemDefinition", nameof(items));
                definitions.Add(item.Id, item);
            }

            var orderedPreplaced = new List<PlacedItem>(preplaced);
            foreach (var placement in orderedPreplaced)
                if (placement == null || string.IsNullOrWhiteSpace(placement.ItemId))
                    throw new ArgumentException("MissingItemId", nameof(preplaced));
            orderedPreplaced.Sort((a, b) => StringComparer.Ordinal.Compare(a.ItemId, b.ItemId));

            var placed = new List<PlacedItem>();
            var occupied = new List<Cell>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var placement in orderedPreplaced)
            {
                if (!seenIds.Add(placement.ItemId))
                    throw new ArgumentException("DuplicateItemId", nameof(preplaced));
                if (!definitions.TryGetValue(placement.ItemId, out var item))
                    throw new ArgumentException("MissingItemDefinition: " + placement.ItemId, nameof(items));
                if (!item.ShapeStates.ContainsKey(placement.ShapeState))
                    throw new ArgumentException("InvalidShapeState: " + placement.ItemId, nameof(preplaced));
                if (!item.GetRotatedShape(placement.ShapeState, placement.Rotation).IsAccepted)
                    throw new ArgumentException("InvalidRotation: " + placement.ItemId, nameof(preplaced));

                var validation = PlacementValidator.Validate(new PlacementBoard(container, occupied),
                    item, placement.Anchor, placement.Rotation, placement.ShapeState);
                if (!validation.IsValid)
                    throw new ArgumentException("Preplaced" + validation.Reason + ": " + placement.ItemId,
                        nameof(preplaced));
                var canonical = new PlacedItem(item, placement.Anchor, placement.Rotation,
                    placement.ShapeState);
                if (!SameCells(canonical.OccupiedCells, placement.OccupiedCells))
                    throw new ArgumentException("ItemDefinitionMismatch: " + placement.ItemId,
                        nameof(preplaced));
                placed.Add(canonical);
                occupied.AddRange(canonical.OccupiedCells);
            }

            var tray = new List<TrayItem>();
            foreach (var trayItem in inventory)
            {
                if (string.IsNullOrWhiteSpace(trayItem.ItemId))
                    throw new ArgumentException("MissingItemId", nameof(inventory));
                if (!seenIds.Add(trayItem.ItemId))
                    throw new ArgumentException("DuplicateItemId", nameof(inventory));
                if (!definitions.TryGetValue(trayItem.ItemId, out var item))
                    throw new ArgumentException("MissingItemDefinition: " + trayItem.ItemId, nameof(items));
                if (!item.ShapeStates.ContainsKey(trayItem.ShapeState))
                    throw new ArgumentException("InvalidShapeState: " + trayItem.ItemId,
                        nameof(inventory));
                if (!item.GetRotatedShape(trayItem.ShapeState, trayItem.Rotation).IsAccepted)
                    throw new ArgumentException("InvalidRotation: " + trayItem.ItemId, nameof(inventory));
                tray.Add(trayItem);
            }

            SchemaVersion = schemaVersion;
            MetricVersion = metricVersion;
            Id = id;
            Grammar = grammar;
            Boosters = boosters;
            Items = new ReadOnlyDictionary<string, ItemDefinition>(definitions);
            InitialState = new GameState(container, placed, tray, targets);
        }

        private static bool SameCells(IReadOnlyList<Cell> first, IReadOnlyList<Cell> second)
        {
            if (first.Count != second.Count)
                return false;
            for (var i = 0; i < first.Count; i++)
                if (first[i] != second[i])
                    return false;
            return true;
        }
    }
}
