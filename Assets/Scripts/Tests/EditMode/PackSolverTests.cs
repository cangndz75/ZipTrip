using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Solver;

namespace ZipTrip.Tests.EditMode
{
    public sealed class PackSolverTests
    {
        private static ItemDefinition Item(string id, Rotation[] rotations, params Cell[] shape) =>
            new ItemDefinition(id, "open", new[]
            {
                new KeyValuePair<string, ItemShape>("open", new ItemShape(shape))
            }, rotations, Array.Empty<string>(), authoredShapeStateIds: new[] { "open" });

        private static readonly Cell[] Single = { new Cell(0, 0) };
        private static readonly Cell[] Horizontal = { new Cell(0, 0), new Cell(1, 0) };
        private static readonly Cell[] Vertical = { new Cell(0, 0), new Cell(0, 1) };

        private static LevelDefinition Level(Cell[] mask, params ItemDefinition[] items)
        {
            var tray = items.Select(item => new TrayItem(item.Id, item.AllowedRotations[0], "open"));
            return new LevelDefinition(1, 1, "fixture", "pack",
                new ContainerDefinition("fixture_container", new ContainerMask(mask), ZipperEdge.Top),
                items, Array.Empty<PlacedItem>(), tray, items.Select(item => item.Id),
                new LevelBoosters(0));
        }

        private static Cell[] Rectangle(int width, int height) =>
            (from y in Enumerable.Range(0, height)
             from x in Enumerable.Range(0, width)
             select new Cell(x, y)).ToArray();

        [Test]
        public void SingleSolution_HasExactMetricsAndReplayCompletion()
        {
            var level = Level(new[] { new Cell(1, 1) },
                Item("a", new[] { Rotation.Degrees0 }, Single));
            var result = PackSolver.Solve(level);

            Assert.That(PackSolverResult.HeuristicVersion, Is.EqualTo(1));
            Assert.That(result.Solvable, Is.True);
            Assert.That(result.CappedSolutionCount, Is.EqualTo(1));
            Assert.That(result.SolutionCountCapped, Is.False);
            Assert.That(result.SolutionDensity, Is.EqualTo("1"));
            Assert.That(result.NodeExpansionCount, Is.EqualTo(1));
            Assert.That(result.NormalizedSearchNumerator, Is.EqualTo(1));
            Assert.That(result.NormalizedSearchDenominator, Is.EqualTo(1));
            Assert.That(result.BacktrackCount, Is.Zero);
            Assert.That(result.FirstSolutionBacktracks, Is.EqualTo(0));
            Assert.That(result.ForcedPlacementCount, Is.EqualTo(1));
            Assert.That(result.FirstSolutionPlacementCount, Is.EqualTo(1));
            Assert.That(result.FirstSolution.Single().Anchor, Is.EqualTo(new Cell(1, 1)));

            var session = new PackSession(level.InitialState, level.Items.Values);
            var placed = session.PlaceItem("a", result.FirstSolution[0].Anchor,
                result.FirstSolution[0].Rotation, result.FirstSolution[0].ShapeState);
            Assert.That(placed.IsAccepted, Is.True);
            Assert.That(placed.Events.OfType<LevelCompletedEvent>().Count(), Is.EqualTo(1));
            var expected = new GameState(level.InitialState.Container, result.FirstSolution,
                Array.Empty<TrayItem>(), level.Targets);
            Assert.That(CanonicalStateSerializer.Serialize(session.State),
                Is.EqualTo(CanonicalStateSerializer.Serialize(expected)));
            Assert.That(StateHash.Compute(session.State), Is.EqualTo(StateHash.Compute(expected)));
        }

        [Test]
        public void Unsolvable_HasNoFirstSolutionMetrics()
        {
            var level = Level(new[] { new Cell(1, 1) },
                Item("a", new[] { Rotation.Degrees0 }, Horizontal));
            var result = PackSolver.Solve(level);

            Assert.That(result.Solvable, Is.False);
            Assert.That(result.CappedSolutionCount, Is.Zero);
            Assert.That(result.SolutionDensity, Is.EqualTo("0"));
            Assert.That(result.NodeExpansionCount, Is.EqualTo(1));
            Assert.That(result.BacktrackCount, Is.Zero);
            Assert.That(result.FirstSolutionBacktracks, Is.Null);
            Assert.That(result.ForcedPlacementCount, Is.Null);
            Assert.That(result.FirstSolutionPlacementCount, Is.Null);
            Assert.That(result.FirstSolution, Is.Empty);
        }

        [Test]
        public void HundredthSolution_CapsCountAndUsesHundredPlusBucket()
        {
            var level = Level(Rectangle(8, 10),
                Item("b", new[] { Rotation.Degrees0 }, Single),
                Item("a", new[] { Rotation.Degrees0 }, Single));
            var result = PackSolver.Solve(level);

            Assert.That(result.CappedSolutionCount, Is.EqualTo(100));
            Assert.That(result.SolutionCountCapped, Is.True);
            Assert.That(result.SolutionDensity, Is.EqualTo("100_PLUS"));
            Assert.That(result.FirstSolution.Select(item => item.ItemId), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(result.FirstSolution[0].Anchor, Is.EqualTo(new Cell(0, 0)));
            Assert.That(result.FirstSolution[1].Anchor, Is.EqualTo(new Cell(1, 0)));
        }

        [Test]
        public void DensityBuckets_DistinguishTwoToTenAndElevenToNinetyNine()
        {
            var two = PackSolver.Solve(Level(new[] { new Cell(0, 0), new Cell(1, 0) },
                Item("a", new[] { Rotation.Degrees0 }, Single)));
            var twenty = PackSolver.Solve(Level(Rectangle(5, 4),
                Item("a", new[] { Rotation.Degrees0 }, Single)));

            Assert.That(two.CappedSolutionCount, Is.EqualTo(2));
            Assert.That(two.SolutionDensity, Is.EqualTo("2_10"));
            Assert.That(twenty.CappedSolutionCount, Is.EqualTo(20));
            Assert.That(twenty.SolutionDensity, Is.EqualTo("11_99"));
        }

        [Test]
        public void SymmetricRotations_CountOneGeometricCandidatePerAnchor()
        {
            var item = Item("square", new[] { Rotation.Degrees270, Rotation.Degrees0,
                Rotation.Degrees180, Rotation.Degrees90 },
                new Cell(0, 0), new Cell(1, 0), new Cell(0, 1), new Cell(1, 1));
            var result = PackSolver.Solve(Level(Rectangle(2, 3), item));

            Assert.That(result.CappedSolutionCount, Is.EqualTo(2));
            Assert.That(result.FirstSolution.Single().Rotation, Is.EqualTo(Rotation.Degrees0));
            Assert.That(result.FirstSolution.Single().Anchor, Is.EqualTo(new Cell(0, 0)));
        }

        [Test]
        public void RotationOrder_PrecedesRowMajorAnchorOrder()
        {
            var mask = new[]
            {
                new Cell(1, 1), new Cell(1, 2), new Cell(2, 2), new Cell(3, 2)
            };
            var item = Item("domino", new[] { Rotation.Degrees90, Rotation.Degrees0 }, Horizontal);
            var result = PackSolver.Solve(Level(mask, item));

            Assert.That(result.Solvable, Is.True);
            Assert.That(result.FirstSolution.Single().Rotation, Is.EqualTo(Rotation.Degrees0));
            Assert.That(result.FirstSolution.Single().Anchor, Is.EqualTo(new Cell(1, 2)));
        }

        [Test]
        public void CandidateAnchors_AreRowMajorRegardlessOfMaskInputOrder()
        {
            var level = Level(new[] { new Cell(2, 1), new Cell(0, 0), new Cell(1, 1) },
                Item("a", new[] { Rotation.Degrees0 }, Single));
            var result = PackSolver.Solve(level);
            Assert.That(result.FirstSolution.Single().Anchor, Is.EqualTo(new Cell(0, 0)));
        }

        [Test]
        public void AllowedRotations_AreRespected()
        {
            var mask = new[] { new Cell(1, 1), new Cell(1, 2) };
            var rotated = PackSolver.Solve(Level(mask,
                Item("a", new[] { Rotation.Degrees90 }, Horizontal)));
            var forbidden = PackSolver.Solve(Level(mask,
                Item("a", new[] { Rotation.Degrees0 }, Horizontal)));

            Assert.That(rotated.CappedSolutionCount, Is.EqualTo(1));
            Assert.That(rotated.FirstSolution.Single().Rotation, Is.EqualTo(Rotation.Degrees90));
            Assert.That(forbidden.CappedSolutionCount, Is.Zero);
        }

        [Test]
        public void Mrv_SelectsItemWithFewerCandidates()
        {
            var level = Level(Rectangle(2, 2),
                Item("a", new[] { Rotation.Degrees0 }, Single),
                Item("b", new[] { Rotation.Degrees0 }, Horizontal));
            var result = PackSolver.Solve(level);

            Assert.That(result.FirstSolution[0].ItemId, Is.EqualTo("b"));
        }

        [Test]
        public void MrvTie_SelectsLargerCurrentShapeArea()
        {
            var level = Level(Rectangle(2, 2),
                Item("a", new[] { Rotation.Degrees0 }, Single),
                Item("b", new[] { Rotation.Degrees0, Rotation.Degrees90 }, Horizontal));
            var result = PackSolver.Solve(level);

            Assert.That(result.FirstSolution[0].ItemId, Is.EqualTo("b"));
        }

        [Test]
        public void MrvAndAreaTie_SelectOrdinalItemId()
        {
            var level = Level(new[] { new Cell(1, 1), new Cell(2, 1) },
                Item("z", new[] { Rotation.Degrees0 }, Single),
                Item("a", new[] { Rotation.Degrees0 }, Single));
            var result = PackSolver.Solve(level);

            Assert.That(result.FirstSolution.Select(item => item.ItemId), Is.EqualTo(new[] { "a", "z" }));
        }

        [Test]
        public void FailedBranches_CountBacktracksAndFirstSolutionPathForcedChoices()
        {
            var mask = new[]
            {
                new Cell(0, 0), new Cell(1, 0), new Cell(2, 0),
                new Cell(1, 1), new Cell(2, 1)
            };
            var level = Level(mask,
                Item("a", new[] { Rotation.Degrees0 }, Vertical),
                Item("b", new[] { Rotation.Degrees0 }, Horizontal));
            var result = PackSolver.Solve(level);

            Assert.That(result.Solvable, Is.True);
            Assert.That(result.NodeExpansionCount, Is.EqualTo(3));
            Assert.That(result.NormalizedSearchNumerator, Is.EqualTo(3));
            Assert.That(result.NormalizedSearchDenominator, Is.EqualTo(2));
            Assert.That(result.BacktrackCount, Is.EqualTo(1));
            Assert.That(result.FirstSolutionBacktracks, Is.EqualTo(1));
            Assert.That(result.ForcedPlacementCount, Is.EqualTo(1));
            Assert.That(result.FirstSolutionPlacementCount, Is.EqualTo(2));
            Assert.That(result.FirstSolution[0].Anchor, Is.EqualTo(new Cell(2, 0)));
            Assert.That(result.FirstSolution[1].Anchor, Is.EqualTo(new Cell(0, 0)));
        }

        [Test]
        public void EveryFailedCandidateBranch_IncrementsBacktrackCountOnce()
        {
            var level = Level(Rectangle(2, 2),
                Item("a", new[] { Rotation.Degrees0 }, Horizontal),
                Item("b", new[] { Rotation.Degrees0 }, Vertical));
            var result = PackSolver.Solve(level);

            Assert.That(result.CappedSolutionCount, Is.Zero);
            Assert.That(result.NodeExpansionCount, Is.EqualTo(3));
            Assert.That(result.BacktrackCount, Is.EqualTo(2));
            Assert.That(result.FirstSolutionBacktracks, Is.Null);
        }

        [Test]
        public void RepeatedSolve_ProducesIdenticalMetricsPathAndDoesNotMutateSource()
        {
            var level = Level(Rectangle(2, 2),
                Item("b", new[] { Rotation.Degrees0 }, Single),
                Item("a", new[] { Rotation.Degrees0 }, Single));
            var before = CanonicalStateSerializer.Serialize(level.InitialState);
            var beforeHash = StateHash.Compute(level.InitialState);

            var first = PackSolver.Solve(level);
            var second = PackSolver.Solve(level);

            Assert.That(first.CappedSolutionCount, Is.EqualTo(second.CappedSolutionCount));
            Assert.That(first.SolutionCountCapped, Is.EqualTo(second.SolutionCountCapped));
            Assert.That(first.SolutionDensity, Is.EqualTo(second.SolutionDensity));
            Assert.That(first.NodeExpansionCount, Is.EqualTo(second.NodeExpansionCount));
            Assert.That(first.NormalizedSearchNumerator, Is.EqualTo(second.NormalizedSearchNumerator));
            Assert.That(first.NormalizedSearchDenominator, Is.EqualTo(second.NormalizedSearchDenominator));
            Assert.That(first.BacktrackCount, Is.EqualTo(second.BacktrackCount));
            Assert.That(first.FirstSolutionBacktracks, Is.EqualTo(second.FirstSolutionBacktracks));
            Assert.That(first.ForcedPlacementCount, Is.EqualTo(second.ForcedPlacementCount));
            Assert.That(first.FirstSolutionPlacementCount, Is.EqualTo(second.FirstSolutionPlacementCount));
            Assert.That(first.FirstSolution.Select(p => (p.ItemId, p.Anchor, p.Rotation, p.ShapeState)),
                Is.EqualTo(second.FirstSolution.Select(p => (p.ItemId, p.Anchor, p.Rotation, p.ShapeState))));
            Assert.That(CanonicalStateSerializer.Serialize(level.InitialState), Is.EqualTo(before));
            Assert.That(StateHash.Compute(level.InitialState), Is.EqualTo(beforeHash));
        }

        [Test]
        public void EmptyInventory_IsRejectedBeforeNormalization()
        {
            var level = Level(new[] { new Cell(1, 1) });
            Assert.That(() => PackSolver.Solve(level),
                Throws.ArgumentException.With.Message.Contains("EmptyInventory"));
        }
    }
}
