using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public static class PhaseALevels
    {
        public static readonly IReadOnlyList<string> Ids =
            Array.AsReadOnly(new[] { "L1", "L2", "L3", "L4" });

        public static LevelDefinition Load(string id)
        {
            if (id != "L1" && id != "L2" && id != "L3" && id != "L4")
                throw new ArgumentException("UnknownPhaseALevel: " + id, nameof(id));
            var asset = Resources.Load<TextAsset>("Levels/" + id);
            if (asset == null)
                throw new InvalidOperationException("MissingPhaseALevelJson: " + id);
            return LevelJsonLoader.Load(asset.text, new[]
            {
                ContainerFixtures.CreateBackpack(new Cell(0, 0)),
                ContainerFixtures.CreateCabin(new Cell(0, 0))
            }, PhaseAItemCatalog.Create(id == "L4"));
        }
    }
}
