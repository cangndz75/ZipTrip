using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Solver;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PhaseALevelTests
    {
        [Test]
        public void Catalog_UsesApprovedAreasStatesAndRotations()
        {
            var items = PhaseAItemCatalog.Create(true).ToDictionary(item => item.Id);
            Assert.That(items.Count, Is.EqualTo(10));
            Assert.That(items.ToDictionary(x => x.Key, x => x.Value.ShapeStates["open"].CellCount),
                Is.EquivalentTo(new Dictionary<string, int>
                {
                    { "sweater", 9 }, { "pants", 10 }, { "scarf", 6 }, { "towel", 4 },
                    { "laptop", 12 }, { "book", 6 }, { "sneaker", 4 }, { "camera", 4 },
                    { "bottle", 3 }, { "passport", 2 }
                }));
            Assert.That(items["sweater"].ShapeStates["folded"].CellCount, Is.EqualTo(8));
            Assert.That(items["pants"].ShapeStates["folded"].CellCount, Is.EqualTo(9));
            Assert.That(items["scarf"].ShapeStates["folded"].CellCount, Is.EqualTo(6));
            Assert.That(items["towel"].ShapeStates["folded"].CellCount, Is.EqualTo(4));
            foreach (var id in new[] { "sweater", "pants", "scarf", "towel" })
                Assert.That(items[id].AuthoredShapeStateIds, Is.EqualTo(new[] { "open", "folded" }));
            Assert.That(items["camera"].AllowedRotations, Is.EqualTo(new[] { Rotation.Degrees0 }));
            Assert.That(items["sneaker"].AllowedRotations, Is.EqualTo(new[]
            {
                Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270
            }));
            Assert.That(items["sneaker"].ShapeStates["open"].OccupiedCells,
                Is.EqualTo(new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2), new Cell(1, 2) }));
        }

        [Test]
        public void RealJson_LoadsCanonicalInventoryOrderAndLevelPolicy()
        {
            var expected = new[]
            {
                new[] { "book", "camera", "bottle", "sneaker" },
                new[] { "book", "bottle", "camera", "laptop", "sneaker" },
                new[] { "camera", "laptop", "pants", "sneaker", "sweater" },
                new[] { "book", "bottle", "camera", "laptop", "scarf", "sneaker", "sweater" }
            };
            for (var i = 0; i < PhaseALevels.Ids.Count; i++)
            {
                var id = PhaseALevels.Ids[i];
                Assert.That(Resources.Load<TextAsset>("Levels/" + id), Is.Not.Null);
                var level = PhaseALevels.Load(id);
                Assert.That(level.Id, Is.EqualTo(id));
                Assert.That(level.Grammar, Is.EqualTo("pack"));
                Assert.That(level.ContainerId, Is.EqualTo(i < 2 ? "backpack_std" : "cabin_std"));
                Assert.That(level.Inventory.Select(x => x.ItemId), Is.EqualTo(expected[i]));
                Assert.That(level.Inventory.All(x => x.Rotation == Rotation.Degrees0 && x.ShapeState == "open"), Is.True);
                Assert.That(level.Preplaced, Is.Empty);
                Assert.That(level.Targets, Is.Empty);
                Assert.That(level.Boosters.VacuumCount, Is.Zero);
                Assert.That(level.Items["sweater"].ShapeStates.Count, Is.EqualTo(i == 3 ? 2 : 1));
            }
        }

        [Test]
        public void L1_RealJsonMeetsCappedSnapshot()
        {
            var result = PhaseALevelValidator.ValidateLevel(PhaseALevels.Load("L1"));
            Assert.That(result.WithoutFold.CappedSolutionCount, Is.EqualTo(100));
            Assert.That(result.WithoutFold.SolutionDensity, Is.EqualTo("100_PLUS"));
        }

        [Test]
        public void L2_RequiresRotationAndHasTwelveSolutions()
        {
            var result = PhaseALevelValidator.ValidateLevel(PhaseALevels.Load("L2"));
            Assert.That(result.WithoutFold.CappedSolutionCount, Is.EqualTo(12));
            Assert.That(result.WithoutFold.SolutionDensity, Is.EqualTo("11_99"));
            Assert.That(result.RotationZeroOnly.CappedSolutionCount, Is.Zero);
        }

        [Test]
        public void L3_HasExactOccupancyAndFoldIsUnavailable()
        {
            var result = PhaseALevelValidator.ValidateLevel(PhaseALevels.Load("L3"));
            Assert.That(result.Level.Inventory.Sum(x => result.Level.Items[x.ItemId]
                .ShapeStates[x.ShapeState].CellCount), Is.EqualTo(39));
            Assert.That(result.Level.InitialState.Container.Mask.ValidCellCount, Is.EqualTo(44));
            Assert.That(result.WithoutFold.CappedSolutionCount, Is.EqualTo(4));
            Assert.That(result.WithoutFold.SolutionDensity, Is.EqualTo("2_10"));
            var session = new PackSession(result.Level.InitialState, result.Level.Items.Values);
            var fold = session.FoldItem("sweater", "folded");
            Assert.That(fold.IsAccepted, Is.False);
            Assert.That(fold.Reason, Is.EqualTo("FoldNotAvailable"));
        }

        [Test]
        public void L4_RequiresSweaterFoldUnderAllFourChecks()
        {
            var result = PhaseALevelValidator.ValidateLevel(PhaseALevels.Load("L4"));
            Assert.That(result.Level.Preplaced, Is.Empty);
            Assert.That(result.WithoutFold.CappedSolutionCount, Is.Zero);
            Assert.That(result.WithFold.Solvable, Is.True);
            Assert.That(result.SweaterLockedOpen.CappedSolutionCount, Is.Zero);
            Assert.That(result.SweaterOnlyFold.Solvable, Is.True);
            Assert.That(result.WithFold.FirstSolution.Where(p => p.ShapeState == "folded")
                .Select(p => p.ItemId), Is.EqualTo(new[] { "sweater" }));
        }

        [TestCase("L1")]
        [TestCase("L2")]
        [TestCase("L3")]
        [TestCase("L4")]
        public void FirstCanonicalSolution_ReplaysThroughPackSessionAndCompletes(string id)
        {
            var level = PhaseALevels.Load(id);
            var result = PackSolver.Solve(level, id == "L4");
            var session = new PackSession(level.InitialState, level.Items.Values);
            var completions = 0;
            foreach (var placement in result.FirstSolution)
            {
                var current = session.State.Tray.Single(x => x.ItemId == placement.ItemId);
                if (current.ShapeState != placement.ShapeState)
                    Assert.That(session.FoldItem(placement.ItemId, placement.ShapeState).IsAccepted, Is.True);
                var placed = session.PlaceItem(placement.ItemId, placement.Anchor,
                    placement.Rotation, placement.ShapeState);
                Assert.That(placed.IsAccepted, Is.True, placement.ItemId);
                completions += placed.Events.OfType<LevelCompletedEvent>().Count();
            }
            Assert.That(session.State.Tray, Is.Empty);
            Assert.That(completions, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedRealJsonValidation_HasIdenticalMetricsAndFirstSolutions()
        {
            var first = PhaseALevelValidator.ValidateAll();
            var second = PhaseALevelValidator.ValidateAll();
            for (var i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].WithoutFold.CappedSolutionCount,
                    Is.EqualTo(first[i].WithoutFold.CappedSolutionCount));
                Assert.That(second[i].WithoutFold.NodeExpansionCount,
                    Is.EqualTo(first[i].WithoutFold.NodeExpansionCount));
                Assert.That(second[i].WithoutFold.BacktrackCount,
                    Is.EqualTo(first[i].WithoutFold.BacktrackCount));
                Assert.That(second[i].WithoutFold.FirstSolution.Select(p =>
                    (p.ItemId, p.Anchor, p.Rotation, p.ShapeState)),
                    Is.EqualTo(first[i].WithoutFold.FirstSolution.Select(p =>
                        (p.ItemId, p.Anchor, p.Rotation, p.ShapeState))));
            }
            Assert.That(second[3].WithFold.NodeExpansionCount,
                Is.EqualTo(first[3].WithFold.NodeExpansionCount));
            Assert.That(second[3].WithFold.BacktrackCount,
                Is.EqualTo(first[3].WithFold.BacktrackCount));
        }

        [Test]
        public void AlteredItemRotation_SnapshotMismatchFailsValidation()
        {
            var level = PhaseALevels.Load("L2");
            var items = level.Items.Values.Select(item => new ItemDefinition(item.Id,
                item.BaseStateId, item.AuthoredShapeStateIds.Select(state =>
                    new KeyValuePair<string, ItemShape>(state, item.ShapeStates[state])),
                new[] { Rotation.Degrees0 }, item.Tags, item.VacuumShape,
                authoredShapeStateIds: item.AuthoredShapeStateIds));
            var altered = new LevelDefinition(level.SchemaVersion, level.MetricVersion,
                level.Id, level.Grammar, level.InitialState.Container, items,
                level.Preplaced, level.Inventory, level.Targets, level.Boosters);
            Assert.That(() => PhaseALevelValidator.ValidateLevel(altered),
                Throws.InvalidOperationException.With.Message.Contains("MetricSnapshotMismatch"));
        }

        [Test]
        public void AlteredInventoryOrder_IsRejectedEvenWhenSolverMetricWouldMatch()
        {
            var level = PhaseALevels.Load("L2");
            var reversed = new LevelDefinition(level.SchemaVersion, level.MetricVersion,
                level.Id, level.Grammar, level.InitialState.Container, level.Items.Values,
                level.Preplaced, level.Inventory.Reverse(), level.Targets, level.Boosters);
            Assert.That(() => PhaseALevelValidator.ValidateLevel(reversed),
                Throws.InvalidOperationException.With.Message.Contains("AuthoredLevelMismatch"));
        }
    }
}
