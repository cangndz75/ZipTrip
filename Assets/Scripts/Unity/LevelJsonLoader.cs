using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public static class LevelJsonLoader
    {
        private const int MissingInteger = int.MinValue;

        public static LevelDefinition Load(string json, IEnumerable<ContainerDefinition> containers,
            IEnumerable<ItemDefinition> items)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("MalformedJson");
            if (containers == null || items == null)
                throw new ArgumentException("MissingContentCatalog");

            LevelDto dto;
            try
            {
                dto = JsonUtility.FromJson<LevelDto>(json);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("MalformedJson", exception);
            }
            if (dto == null)
                throw new FormatException("MalformedJson");
            if (dto.preplaced == null || dto.inventory == null || dto.targets == null ||
                dto.boosters == null || dto.boosters.vacuumCount == MissingInteger)
                throw new FormatException("MalformedRequiredField");
            if (string.IsNullOrWhiteSpace(dto.containerId))
                throw new ArgumentException("MissingContainerId");

            var containerById = new Dictionary<string, ContainerDefinition>(StringComparer.Ordinal);
            foreach (var container in containers)
            {
                if (container == null || containerById.ContainsKey(container.Id))
                    throw new ArgumentException("DuplicateOrNullContainerDefinition", nameof(containers));
                containerById.Add(container.Id, container);
            }
            if (!containerById.TryGetValue(dto.containerId, out var selectedContainer))
                throw new ArgumentException("MissingContainerDefinition: " + dto.containerId);

            var itemList = new List<ItemDefinition>();
            var itemById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                if (item == null || itemById.ContainsKey(item.Id))
                    throw new ArgumentException("DuplicateOrNullItemDefinition", nameof(items));
                itemById.Add(item.Id, item);
                itemList.Add(item);
            }

            var preplaced = new List<PlacedItem>();
            foreach (var entry in dto.preplaced)
            {
                if (entry == null || entry.anchorX == MissingInteger || entry.anchorY == MissingInteger)
                    throw new FormatException("MalformedRequiredField: preplaced anchor");
                var item = Resolve(itemById, entry.itemId);
                var rotation = MapRotation(entry.rotation);
                var shapeState = MapShapeState(item, entry.shapeState);
                if (!item.GetRotatedShape(shapeState, rotation).IsAccepted)
                    throw new ArgumentException("InvalidRotation: " + item.Id);
                preplaced.Add(new PlacedItem(item, new Cell(entry.anchorX, entry.anchorY),
                    rotation, shapeState));
            }

            var inventory = new List<TrayItem>();
            foreach (var entry in dto.inventory)
            {
                if (entry == null)
                    throw new FormatException("MalformedRequiredField: inventory item");
                var item = Resolve(itemById, entry.itemId);
                inventory.Add(new TrayItem(item.Id, MapRotation(entry.rotation),
                    MapShapeState(item, entry.shapeState)));
            }

            return new LevelDefinition(dto.schemaVersion, dto.metricVersion, dto.id, dto.grammar,
                selectedContainer, itemList, preplaced, inventory, dto.targets,
                new LevelBoosters(dto.boosters.vacuumCount));
        }

        private static ItemDefinition Resolve(Dictionary<string, ItemDefinition> items, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("MissingItemId");
            if (!items.TryGetValue(itemId, out var item))
                throw new ArgumentException("MissingItemDefinition: " + itemId);
            return item;
        }

        private static Rotation MapRotation(int value)
        {
            if (value == MissingInteger)
                throw new FormatException("MissingRotation");
            if (!Enum.IsDefined(typeof(Rotation), value))
                throw new ArgumentException("InvalidRotation: " + value);
            return (Rotation)value;
        }

        private static string MapShapeState(ItemDefinition item, int ordinal)
        {
            if (ordinal == MissingInteger)
                throw new FormatException("MissingShapeState");
            var order = item.AuthoredShapeStateIds;
            if (order.Count == 0)
                throw new ArgumentException("MissingAuthoredShapeStateOrder: " + item.Id);
            if (ordinal < 0 || ordinal >= order.Count)
                throw new ArgumentException("InvalidShapeState: " + ordinal);
            return order[ordinal];
        }

        [Serializable]
        private sealed class LevelDto
        {
            public int schemaVersion = MissingInteger;
            public int metricVersion = MissingInteger;
            public string id = null;
            public string grammar = null;
            public string containerId = null;
            public PreplacedDto[] preplaced = null;
            public InventoryDto[] inventory = null;
            public string[] targets = null;
            public BoostersDto boosters = null;
        }

        [Serializable]
        private sealed class PreplacedDto
        {
            public string itemId = null;
            public int anchorX = MissingInteger;
            public int anchorY = MissingInteger;
            public int rotation = MissingInteger;
            public int shapeState = MissingInteger;
        }

        [Serializable]
        private sealed class InventoryDto
        {
            public string itemId = null;
            public int rotation = MissingInteger;
            public int shapeState = MissingInteger;
        }

        [Serializable]
        private sealed class BoostersDto
        {
            public int vacuumCount = MissingInteger;
        }
    }
}
