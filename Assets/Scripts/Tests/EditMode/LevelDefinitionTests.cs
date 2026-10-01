using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public sealed class LevelDefinitionTests
    {
        private const string ValidJson =
            "{\"schemaVersion\":1,\"metricVersion\":1,\"id\":\"T1\",\"grammar\":\"pack\"," +
            "\"containerId\":\"cabin_std\",\"preplaced\":[{\"itemId\":\"laptop\"," +
            "\"anchorX\":1,\"anchorY\":1,\"rotation\":0,\"shapeState\":0}]," +
            "\"inventory\":[{\"itemId\":\"sweater\",\"rotation\":90,\"shapeState\":1}]," +
            "\"targets\":[\"laptop\",\"sweater\"],\"boosters\":{\"vacuumCount\":0}}";

        private static ItemDefinition Laptop() => new ItemDefinition("laptop", "open", new[]
        {
            new KeyValuePair<string, ItemShape>("open", new ItemShape(new[] { new Cell(0, 0) }))
        }, new[] { Rotation.Degrees0 }, Array.Empty<string>(),
            authoredShapeStateIds: new[] { "open" });

        private static ItemDefinition Sweater(bool reverseDictionaryInput = false)
        {
            var open = new ItemShape(new[] { new Cell(0, 0), new Cell(1, 0) });
            var folded = new ItemShape(new[] { new Cell(0, 0), new Cell(0, 1) });
            var states = new[]
            {
                new KeyValuePair<string, ItemShape>("folded", folded),
                new KeyValuePair<string, ItemShape>("open", open)
            };
            return new ItemDefinition("sweater", "open",
                reverseDictionaryInput ? states.Reverse() : states,
                new[] { Rotation.Degrees0, Rotation.Degrees90 }, Array.Empty<string>(),
                authoredShapeStateIds: new[] { "open", "folded" });
        }

        private static LevelDefinition Load(string json, ItemDefinition sweater = null) =>
            LevelJsonLoader.Load(json,
                new[] { ContainerFixtures.CreateCabin(new Cell(0, 0)) },
                new[] { Laptop(), sweater ?? Sweater() });

        [Test]
        public void ValidFixture_MapsAllApprovedFieldsAndCanonicalState()
        {
            var level = Load(ValidJson);

            Assert.That(level.SchemaVersion, Is.EqualTo(1));
            Assert.That(level.MetricVersion, Is.EqualTo(1));
            Assert.That(level.Id, Is.EqualTo("T1"));
            Assert.That(level.Grammar, Is.EqualTo("pack"));
            Assert.That(level.ContainerId, Is.EqualTo("cabin_std"));
            Assert.That(level.Preplaced.Single().ItemId, Is.EqualTo("laptop"));
            Assert.That(level.Preplaced.Single().ShapeState, Is.EqualTo("open"));
            Assert.That(level.Inventory.Single().ItemId, Is.EqualTo("sweater"));
            Assert.That(level.Inventory.Single().Rotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(level.Inventory.Single().ShapeState, Is.EqualTo("folded"));
            Assert.That(level.Targets, Is.EqualTo(new[] { "laptop", "sweater" }));
            Assert.That(level.Boosters.VacuumCount, Is.Zero);
            Assert.That(level.InitialState.Occupancy, Is.EqualTo(new[] { new Cell(1, 1) }));
        }

        [Test]
        public void ShapeStateZeroAndOne_MapExplicitAuthoredOrder()
        {
            var first = Load(ValidJson.Replace("\"shapeState\":1", "\"shapeState\":0"));
            var second = Load(ValidJson);
            Assert.That(first.Inventory.Single().ShapeState, Is.EqualTo("open"));
            Assert.That(second.Inventory.Single().ShapeState, Is.EqualTo("folded"));
            Assert.That(StateHash.Compute(first.InitialState),
                Is.Not.EqualTo(StateHash.Compute(second.InitialState)));
        }

        [Test]
        public void LastValidShapeStateIndex_MapsThirdAuthoredState()
        {
            var third = new ItemShape(new[] { new Cell(0, 0) });
            var item = new ItemDefinition("sweater", "open", new[]
            {
                new KeyValuePair<string, ItemShape>("third", third),
                new KeyValuePair<string, ItemShape>("folded", new ItemShape(new[]
                {
                    new Cell(0, 0), new Cell(0, 1)
                })),
                new KeyValuePair<string, ItemShape>("open", new ItemShape(new[]
                {
                    new Cell(0, 0), new Cell(1, 0)
                }))
            }, new[] { Rotation.Degrees0, Rotation.Degrees90 }, Array.Empty<string>(),
                authoredShapeStateIds: new[] { "open", "folded", "third" });

            var level = Load(ValidJson.Replace("\"shapeState\":1", "\"shapeState\":2"), item);
            Assert.That(level.Inventory.Single().ShapeState, Is.EqualTo("third"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void InvalidShapeStateOrdinal_IsRejected(int ordinal)
        {
            var json = ValidJson.Replace("\"shapeState\":1", "\"shapeState\":" + ordinal);
            Assert.That(() => Load(json), Throws.Exception.With.Message.Contains("InvalidShapeState"));
        }

        [Test]
        public void MissingShapeState_IsRejectedWithoutDefault()
        {
            var json = ValidJson.Replace(",\"shapeState\":1", string.Empty);
            Assert.That(() => Load(json), Throws.Exception.With.Message.Contains("MissingShapeState"));
        }

        [Test]
        public void MissingRotation_IsRejectedWithoutDefault()
        {
            var json = ValidJson.Replace("\"rotation\":90,", string.Empty);
            Assert.That(() => Load(json), Throws.Exception.With.Message.Contains("MissingRotation"));
        }

        [Test]
        public void MissingPreplacedPoseFields_AreRejectedWithoutDefaults()
        {
            Assert.That(() => Load(ValidJson.Replace("\"rotation\":0,", string.Empty)),
                Throws.Exception.With.Message.Contains("MissingRotation"));
            Assert.That(() => Load(ValidJson.Replace(",\"shapeState\":0", string.Empty)),
                Throws.Exception.With.Message.Contains("MissingShapeState"));
        }

        [Test]
        public void ExplicitStateOrder_IgnoresShapeDictionaryInputOrder()
        {
            var first = Load(ValidJson, Sweater());
            var second = Load(ValidJson, Sweater(true));
            Assert.That(first.Inventory.Single().ShapeState, Is.EqualTo("folded"));
            Assert.That(second.Inventory.Single().ShapeState, Is.EqualTo("folded"));
            Assert.That(CanonicalStateSerializer.Serialize(first.InitialState),
                Is.EqualTo(CanonicalStateSerializer.Serialize(second.InitialState)));
            Assert.That(StateHash.Compute(first.InitialState),
                Is.EqualTo(StateHash.Compute(second.InitialState)));
        }

        [Test]
        public void RepeatedParseAndMap_ProducesSameDomainStateAndHash()
        {
            var first = Load(ValidJson);
            var second = Load(ValidJson);
            Assert.That(CanonicalStateSerializer.Serialize(first.InitialState),
                Is.EqualTo(CanonicalStateSerializer.Serialize(second.InitialState)));
            Assert.That(StateHash.Compute(first.InitialState),
                Is.EqualTo(StateHash.Compute(second.InitialState)));
            Assert.That(first.Inventory, Is.EqualTo(second.Inventory));
        }

        [Test]
        public void InventoryJsonOrder_IsCanonicalTrayOrder()
        {
            var json = ValidJson.Replace(
                "\"preplaced\":[{\"itemId\":\"laptop\",\"anchorX\":1,\"anchorY\":1," +
                "\"rotation\":0,\"shapeState\":0}]",
                "\"preplaced\":[]").Replace(
                "\"inventory\":[{\"itemId\":\"sweater\"",
                "\"inventory\":[{\"itemId\":\"laptop\",\"rotation\":0,\"shapeState\":0}," +
                "{\"itemId\":\"sweater\"");
            var level = Load(json);
            Assert.That(level.Inventory.Select(item => item.ItemId),
                Is.EqualTo(new[] { "laptop", "sweater" }));
            Assert.That(level.InitialState.Tray, Is.EqualTo(level.Inventory));
        }

        [Test]
        public void MissingItemId_IsRejected()
        {
            var json = ValidJson.Replace("\"itemId\":\"sweater\",", string.Empty);
            Assert.That(() => Load(json), Throws.Exception.With.Message.Contains("MissingItemId"));
        }

        [Test]
        public void MissingContainerId_IsRejected()
        {
            var json = ValidJson.Replace("\"containerId\":\"cabin_std\"",
                "\"containerId\":\"\"");
            Assert.That(() => Load(json), Throws.Exception.With.Message.Contains("MissingContainerId"));
        }

        [Test]
        public void MissingLevelIdAndGrammar_AreRejected()
        {
            Assert.That(() => Load(ValidJson.Replace("\"id\":\"T1\"", "\"id\":\"\"")),
                Throws.Exception.With.Message.Contains("MissingLevelId"));
            Assert.That(() => Load(ValidJson.Replace("\"grammar\":\"pack\"", "\"grammar\":\"\"")),
                Throws.Exception.With.Message.Contains("MissingGrammar"));
        }

        [Test]
        public void UnsupportedSchemaAndMetricVersions_AreRejected()
        {
            Assert.That(() => Load(ValidJson.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")),
                Throws.Exception.With.Message.Contains("UnsupportedSchemaVersion"));
            Assert.That(() => Load(ValidJson.Replace("\"metricVersion\":1", "\"metricVersion\":2")),
                Throws.Exception.With.Message.Contains("UnsupportedMetricVersion"));
        }

        [Test]
        public void PreplacedOutsideMask_IsRejected()
        {
            var json = ValidJson.Replace("\"anchorX\":1,\"anchorY\":1",
                "\"anchorX\":0,\"anchorY\":0");
            Assert.That(() => Load(json), Throws.Exception.With.Message.Contains("PreplacedOutsideMask"));
        }

        [Test]
        public void DuplicateItemId_AcrossPreplacedAndInventory_IsRejected()
        {
            var json = ValidJson.Replace("\"itemId\":\"sweater\",\"rotation\":90,\"shapeState\":1",
                "\"itemId\":\"laptop\",\"rotation\":0,\"shapeState\":0");
            Assert.That(() => Load(json), Throws.Exception.With.Message.Contains("DuplicateItemId"));
        }

        [Test]
        public void InvalidRotationAndUnknownItemDefinition_AreRejected()
        {
            Assert.That(() => Load(ValidJson.Replace("\"rotation\":90", "\"rotation\":45")),
                Throws.Exception.With.Message.Contains("InvalidRotation"));
            Assert.That(() => Load(ValidJson.Replace("\"rotation\":0,\"shapeState\":0",
                    "\"rotation\":90,\"shapeState\":0")),
                Throws.Exception.With.Message.Contains("InvalidRotation"));
            Assert.That(() => Load(ValidJson.Replace("\"itemId\":\"sweater\"",
                    "\"itemId\":\"missing\"")),
                Throws.Exception.With.Message.Contains("MissingItemDefinition"));
        }

        [Test]
        public void MalformedJsonAndRequiredFields_AreRejected()
        {
            Assert.That(() => Load("{broken"), Throws.Exception.With.Message.Contains("MalformedJson"));
            Assert.That(() => Load(ValidJson.Replace("\"inventory\":[{\"itemId\":\"sweater\"," +
                "\"rotation\":90,\"shapeState\":1}],", string.Empty)),
                Throws.Exception.With.Message.Contains("MalformedRequiredField"));
        }

        [Test]
        public void NegativeVacuumCount_IsRejected()
        {
            var json = ValidJson.Replace("\"vacuumCount\":0", "\"vacuumCount\":-1");
            Assert.That(() => Load(json), Throws.Exception.With.Message.Contains("InvalidBoosterValue"));
        }

        [Test]
        public void MissingBoosterCountAndPreplacedAnchor_AreRejected()
        {
            Assert.That(() => Load(ValidJson.Replace("\"vacuumCount\":0", string.Empty)),
                Throws.Exception.With.Message.Contains("MalformedRequiredField"));
            Assert.That(() => Load(ValidJson.Replace("\"anchorX\":1,", string.Empty)),
                Throws.Exception.With.Message.Contains("MalformedRequiredField"));
        }

        [Test]
        public void MissingExplicitAuthoredOrder_IsRejectedForOrdinalMapping()
        {
            var item = new ItemDefinition("sweater", "open", new[]
            {
                new KeyValuePair<string, ItemShape>("open", new ItemShape(new[]
                {
                    new Cell(0, 0), new Cell(1, 0)
                }))
            }, new[] { Rotation.Degrees0, Rotation.Degrees90 }, Array.Empty<string>());
            Assert.That(() => Load(ValidJson, item),
                Throws.Exception.With.Message.Contains("MissingAuthoredShapeStateOrder"));
        }

        [Test]
        public void ExplicitAuthoredOrder_MustContainEveryUniqueState()
        {
            var states = new[]
            {
                new KeyValuePair<string, ItemShape>("open", new ItemShape(new[] { new Cell(0, 0) })),
                new KeyValuePair<string, ItemShape>("folded", new ItemShape(new[] { new Cell(0, 0) }))
            };
            Assert.That(() => new ItemDefinition("sweater", "open", states,
                    new[] { Rotation.Degrees0 }, Array.Empty<string>(),
                    authoredShapeStateIds: new[] { "open" }),
                Throws.ArgumentException.With.Message.Contains("Authored state order"));
            Assert.That(() => new ItemDefinition("sweater", "open", states,
                    new[] { Rotation.Degrees0 }, Array.Empty<string>(),
                    authoredShapeStateIds: new[] { "open", "open" }),
                Throws.ArgumentException.With.Message.Contains("Authored state order"));
        }

        [Test]
        public void DomainAssembly_DoesNotReferenceUnityEngine()
        {
            Assert.That(typeof(LevelDefinition).Assembly.GetReferencedAssemblies()
                .Select(assembly => assembly.Name), Has.None.StartsWith("UnityEngine"));
        }
    }
}
