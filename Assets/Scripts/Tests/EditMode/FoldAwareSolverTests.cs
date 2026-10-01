using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Solver;

namespace ZipTrip.Tests.EditMode
{
    public sealed class FoldAwareSolverTests
    {
        private static ItemDefinition Sweater(string[] authoredOrder = null) =>
            new ItemDefinition("sweater", "Open", new[]
            {
                new KeyValuePair<string, ItemShape>("Folded", new ItemShape(new[]
                {
                    new Cell(0, 0), new Cell(0, 1)
                })),
                new KeyValuePair<string, ItemShape>("Open", new ItemShape(new[]
                {
                    new Cell(0, 0), new Cell(1, 0)
                }))
            }, new[] { Rotation.Degrees0 }, Array.Empty<string>(),
                authoredShapeStateIds: authoredOrder ?? new[] { "Open", "Folded" });

        private static LevelDefinition Level(Cell[] mask, string initialState = "Open",
            ItemDefinition item = null)
        {
            item = item ?? Sweater();
            return new LevelDefinition(1, 1, "fold_fixture", "pack",
                new ContainerDefinition("fold_container", new ContainerMask(mask), ZipperEdge.Top),
                new[] { item }, Array.Empty<PlacedItem>(),
                new[] { new TrayItem(item.Id, Rotation.Degrees0, initialState) },
                new[] { item.Id }, new LevelBoosters(0));
        }

        private static readonly Cell[] Vertical = { new Cell(1, 1), new Cell(1, 2) };
        private static readonly Cell[] Both =
        {
            new Cell(1, 1), new Cell(2, 1), new Cell(1, 2), new Cell(2, 2)
        };

        [Test]
        public void FoldRequired_NoFoldFailsAndFoldAwareSolutionReplaysThroughPackSession()
        {
            var level = Level(Vertical);
            var before = CanonicalStateSerializer.Serialize(level.InitialState);
            var analysis = PackSolver.AnalyzeFoldDependency(level);

            Assert.That(analysis.WithoutFold.Solvable, Is.False);
            Assert.That(analysis.WithFold.Solvable, Is.True);
            Assert.That(analysis.FoldRequired, Is.True);
            Assert.That(analysis.FoldDesignedLevelValid, Is.True);
            Assert.That(analysis.WithFold.FirstSolution.Single().ShapeState, Is.EqualTo("Folded"));

            var session = new PackSession(level.InitialState, level.Items.Values);
            var chosen = analysis.WithFold.FirstSolution.Single();
            var folded = session.FoldItem(chosen.ItemId, chosen.ShapeState);
            Assert.That(folded.IsAccepted, Is.True);
            var placed = session.PlaceItem(chosen.ItemId, chosen.Anchor, chosen.Rotation,
                chosen.ShapeState);
            Assert.That(placed.IsAccepted, Is.True);
            Assert.That(placed.Events.OfType<LevelCompletedEvent>().Count(), Is.EqualTo(1));
            Assert.That(CanonicalStateSerializer.Serialize(level.InitialState), Is.EqualTo(before));
        }

        [Test]
        public void FoldNotRequired_IsRejectedForFoldDesignedLevel()
        {
            var analysis = PackSolver.AnalyzeFoldDependency(Level(Both));

            Assert.That(analysis.WithoutFold.Solvable, Is.True);
            Assert.That(analysis.WithFold.Solvable, Is.True);
            Assert.That(analysis.FoldRequired, Is.False);
            Assert.That(analysis.FoldDesignedLevelValid, Is.False);
        }

        [Test]
        public void NeitherModeSolvable_IsNotAValidFoldDesignedLevel()
        {
            var analysis = PackSolver.AnalyzeFoldDependency(Level(new[] { new Cell(1, 1) }));
            Assert.That(analysis.WithoutFold.Solvable, Is.False);
            Assert.That(analysis.WithFold.Solvable, Is.False);
            Assert.That(analysis.FoldRequired, Is.False);
            Assert.That(analysis.FoldDesignedLevelValid, Is.False);
        }

        [Test]
        public void CurrentFoldedState_IsUsedWithoutAnotherFoldAction()
        {
            var level = Level(Vertical, "Folded");
            var result = PackSolver.Solve(level);

            Assert.That(result.CappedSolutionCount, Is.EqualTo(1));
            Assert.That(result.FirstSolution.Single().ShapeState, Is.EqualTo("Folded"));
            var session = new PackSession(level.InitialState, level.Items.Values);
            var placement = result.FirstSolution.Single();
            Assert.That(session.PlaceItem(placement.ItemId, placement.Anchor,
                placement.Rotation, placement.ShapeState).IsAccepted, Is.True);
        }

        [Test]
        public void AuthoredOrder_DeterminesFirstFoldAwareSolution()
        {
            var openFirst = PackSolver.Solve(Level(Both), true);
            var foldedFirst = PackSolver.Solve(Level(Both, item: Sweater(new[] { "Folded", "Open" })), true);

            Assert.That(openFirst.FirstSolution.Single().ShapeState, Is.EqualTo("Open"));
            Assert.That(foldedFirst.FirstSolution.Single().ShapeState, Is.EqualTo("Folded"));
            Assert.That(openFirst.CappedSolutionCount, Is.EqualTo(foldedFirst.CappedSolutionCount));
        }

        [Test]
        public void IdenticalAuthoredFootprints_CountAsOneGeometricSolution()
        {
            var item = new ItemDefinition("sweater", "Open", new[]
            {
                new KeyValuePair<string, ItemShape>("Open", new ItemShape(new[]
                    { new Cell(0, 0), new Cell(1, 0) })),
                new KeyValuePair<string, ItemShape>("Folded", new ItemShape(new[]
                    { new Cell(0, 0), new Cell(1, 0) }))
            }, new[] { Rotation.Degrees0 }, Array.Empty<string>(),
                authoredShapeStateIds: new[] { "Open", "Folded" });
            var level = Level(new[] { new Cell(1, 1), new Cell(2, 1) }, item: item);

            var result = PackSolver.Solve(level, true);
            Assert.That(result.CappedSolutionCount, Is.EqualTo(1));
            Assert.That(result.FirstSolution.Single().ShapeState, Is.EqualTo("Open"));
            Assert.That(PackSolver.AnalyzeFoldDependency(level).FoldRequired, Is.False);
        }

        [Test]
        public void RepeatedFoldAnalysis_IsDeterministic()
        {
            var level = Level(Vertical);
            var first = PackSolver.AnalyzeFoldDependency(level);
            var second = PackSolver.AnalyzeFoldDependency(level);

            Assert.That(first.FoldRequired, Is.EqualTo(second.FoldRequired));
            Assert.That(first.WithoutFold.CappedSolutionCount, Is.EqualTo(second.WithoutFold.CappedSolutionCount));
            Assert.That(first.WithFold.CappedSolutionCount, Is.EqualTo(second.WithFold.CappedSolutionCount));
            Assert.That(first.WithFold.NodeExpansionCount, Is.EqualTo(second.WithFold.NodeExpansionCount));
            Assert.That(first.WithFold.BacktrackCount, Is.EqualTo(second.WithFold.BacktrackCount));
            Assert.That(first.WithFold.FirstSolution.Select(p => (p.ItemId, p.Anchor, p.Rotation, p.ShapeState)),
                Is.EqualTo(second.WithFold.FirstSolution.Select(p => (p.ItemId, p.Anchor, p.Rotation, p.ShapeState))));
        }

        [Test]
        public void MissingAuthoredOrder_IsRejectedInFoldAwareMode()
        {
            var item = new ItemDefinition("sweater", "Open", new[]
            {
                new KeyValuePair<string, ItemShape>("Open", new ItemShape(new[]
                    { new Cell(0, 0), new Cell(1, 0) })),
                new KeyValuePair<string, ItemShape>("Folded", new ItemShape(new[]
                    { new Cell(0, 0), new Cell(0, 1) }))
            }, new[] { Rotation.Degrees0 }, Array.Empty<string>());
            var level = Level(Vertical, item: item);

            Assert.That(() => PackSolver.Solve(level, true),
                Throws.ArgumentException.With.Message.Contains("MissingAuthoredShapeStateOrder"));
            Assert.That(PackSolver.Solve(level).Solvable, Is.False);
        }
    }
}
