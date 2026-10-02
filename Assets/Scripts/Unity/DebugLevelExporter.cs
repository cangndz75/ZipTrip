#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public static class DebugLevelExporter
    {
        public static string Export(LevelDefinition source, GameState state)
        {
            if (source == null || state == null)
                throw new ArgumentNullException();

            var preplaced = new PreplacedDto[state.Placements.Count];
            for (var i = 0; i < state.Placements.Count; i++)
            {
                var placement = state.Placements[i];
                preplaced[i] = new PreplacedDto
                {
                    itemId = placement.ItemId,
                    anchorX = placement.Anchor.X,
                    anchorY = placement.Anchor.Y,
                    rotation = (int)placement.Rotation,
                    shapeState = ShapeStateOrdinal(source.Items[placement.ItemId], placement.ShapeState)
                };
            }

            var inventory = new InventoryDto[state.Tray.Count];
            for (var i = 0; i < state.Tray.Count; i++)
            {
                var item = state.Tray[i];
                inventory[i] = new InventoryDto
                {
                    itemId = item.ItemId,
                    rotation = (int)item.Rotation,
                    shapeState = ShapeStateOrdinal(source.Items[item.ItemId], item.ShapeState)
                };
            }

            var targets = new string[state.Targets.Count];
            for (var i = 0; i < targets.Length; i++)
                targets[i] = state.Targets[i];
            return JsonUtility.ToJson(new LevelDto
            {
                schemaVersion = source.SchemaVersion,
                metricVersion = source.MetricVersion,
                id = source.Id,
                grammar = source.Grammar,
                containerId = state.Container.Id,
                preplaced = preplaced,
                inventory = inventory,
                targets = targets,
                boosters = new BoostersDto { vacuumCount = source.Boosters.VacuumCount }
            });
        }

        private static int ShapeStateOrdinal(ItemDefinition item, string stateId)
        {
            for (var i = 0; i < item.AuthoredShapeStateIds.Count; i++)
                if (StringComparer.Ordinal.Equals(item.AuthoredShapeStateIds[i], stateId))
                    return i;
            throw new InvalidOperationException("Missing authored shape state: " + stateId);
        }

        [Serializable]
        private sealed class LevelDto
        {
            public int schemaVersion;
            public int metricVersion;
            public string id;
            public string grammar;
            public string containerId;
            public PreplacedDto[] preplaced;
            public InventoryDto[] inventory;
            public string[] targets;
            public BoostersDto boosters;
        }

        [Serializable]
        private sealed class PreplacedDto
        {
            public string itemId;
            public int anchorX;
            public int anchorY;
            public int rotation;
            public int shapeState;
        }

        [Serializable]
        private sealed class InventoryDto
        {
            public string itemId;
            public int rotation;
            public int shapeState;
        }

        [Serializable]
        private sealed class BoostersDto
        {
            public int vacuumCount;
        }
    }
}
#endif
