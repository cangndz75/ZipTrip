using System.IO;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    // ZT-040 authoring gates for the shipped Lv2 content (Resources/LevelsV2).
    public sealed class FirstPlayableLevelsTests
    {
        private static string Json(string id) =>
            File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Resources/LevelsV2", id + ".json"));

        private static PuzzleLevel Load(string id, params string[] uprightOnly)
        {
            var catalog = PuzzleItemCatalog.Create()
                .Select(spec => uprightOnly.Contains(spec.Id) ? HoldDefaultOrientation(spec) : spec);
            return LevelJsonLoaderV2.Load(Json(id), catalog);
        }

        // Same item restricted to its authored orientation: used to prove that rotation matters.
        private static ItemSpec HoldDefaultOrientation(ItemSpec spec) =>
            new ItemSpec(spec.Id, spec.DefaultStateId,
                spec.States.Select(s => new ItemStateSpec(s.Id, s.Footprint, s.Thickness, new[] { s.AllowedRotations[0] })),
                spec.Tags, spec.Transitions, spec.Nest);

        private static PackSolveResult SolveAndReport(PuzzleLevel level)
        {
            var result = PackSolverV2.Solve(level);
            TestContext.WriteLine($"[zt040-solver] {level.Id}: status={result.Status} count={result.SolutionCount}" +
                $" capped={result.SolutionCountCapped} greedy={result.GreedySolvable} nodes={result.Metrics.NodesVisited}" +
                $" candidates={result.Metrics.CandidatesTested} replay={result.Solution?.Moves.Count}");
            return result;
        }

        private static void AssertReplayCompletes(PuzzleLevel level, PackSolution solution)
        {
            var session = new PuzzleSession(level);
            foreach (var move in solution.Moves)
                Assert.That(session.Apply(move).Move.IsAccepted, Is.True, move.ToString());
            Assert.That(session.CurrentCompletion.IsComplete, Is.True);
            Assert.That(session.MoveCount, Is.EqualTo(solution.Moves.Count));
        }

        // Lv1 is the Golden Lv1 (prepacked items, Zone rules): GoldenLv1ShippedTests.
        [Test]
        public void Lv2_IsAnEmptySuitcasePackLevelWithoutLaterMechanics()
        {
            const string id = "lv2-rotate";
            var level = Load(id);
            Assert.That(level.Objective.Profile, Is.EqualTo(ObjectiveProfile.Pack), id);
            Assert.That(level.InitialState.GetItems(ItemLocationKind.Suitcase), Is.Empty, id + " starts empty");
            Assert.That(level.InitialState.Items.All(i => i.Location.Kind == ItemLocationKind.SourceTray), Is.True, id);
            Assert.That(level.Spec.Board.Compartments.Single().Layers, Is.EqualTo(1), id + " is L = 1");
            Assert.That(level.Spec.StagingCapacity, Is.Zero, id);
            Assert.That(level.Rules.Rules, Is.Empty, id);
            Assert.That(level.InitialState.Items.All(i => i.Definition.Transitions.Count == 0 && i.Definition.Nest.Capacity == 0), Is.True,
                id + " has no Fold / Compress / Nest");
        }

        [Test]
        public void Lv2_Rotate_RequiresTurningTheLaptop()
        {
            var level = Load("lv2-rotate");
            var result = SolveAndReport(level);
            Assert.That(result.Status, Is.EqualTo(PackSolveStatus.Solvable));
            AssertReplayCompletes(level, result.Solution);
            var laptopMove = result.Solution.Moves.Single(m => m.InstanceId == "laptop-1");
            Assert.That(laptopMove.Rotation, Is.EqualTo(Rotation.Degrees90), "the laptop lies flat in every solution");

            Assert.That(SolveAndReport(Load("lv2-rotate", "laptop")).Status, Is.EqualTo(PackSolveStatus.Unsolvable),
                "an upright laptop leaves a dead one-column gap");
            Assert.That(SolveAndReport(Load("lv2-rotate", "sneaker", "sweater")).Status, Is.EqualTo(PackSolveStatus.Solvable),
                "the laptop is the one necessary rotation");
        }
    }
}
