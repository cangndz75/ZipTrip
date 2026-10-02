#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public sealed class DebugLevelExporterTests
    {
        [Test]
        public void CurrentStateExport_IsDeterministicAndReloadsSameCanonicalState()
        {
            var level = PhaseALevels.Load("L1");
            var session = new PackSession(level.InitialState, level.Items.Values);
            Assert.That(session.RotateItem("book", Rotation.Degrees90).IsAccepted, Is.True);
            Assert.That(session.PlaceItem("sneaker", new Cell(1, 1), Rotation.Degrees0,
                "open").IsAccepted, Is.True);

            var first = DebugLevelExporter.Export(level, session.State);
            var second = DebugLevelExporter.Export(level, session.State);
            Assert.That(second, Is.EqualTo(first));

            var reloaded = LevelJsonLoader.Load(first, new[]
            {
                ContainerFixtures.CreateBackpack(new Cell(0, 0)),
                ContainerFixtures.CreateCabin(new Cell(0, 0))
            }, level.Items.Values);
            Assert.That(CanonicalStateSerializer.Serialize(reloaded.InitialState),
                Is.EqualTo(CanonicalStateSerializer.Serialize(session.State)));
            Assert.That(StateHash.Compute(reloaded.InitialState),
                Is.EqualTo(StateHash.Compute(session.State)));
        }

        [Test]
        public void DebugToolingSource_IsGuardedFromNonDevelopmentPlayerBuilds()
        {
            var unityRoot = Path.Combine(UnityEngine.Application.dataPath, "Scripts", "Unity");
            foreach (var name in new[] { "DebugGameplayOverlay.cs", "DebugLevelExporter.cs" })
            {
                var source = File.ReadAllText(Path.Combine(unityRoot, name));
                Assert.That(source.StartsWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD"), Is.True, name);
                Assert.That(source.TrimEnd().EndsWith("#endif"), Is.True, name);
            }
            var presentation = File.ReadAllText(Path.Combine(unityRoot, "PhaseAL1Presentation.cs"));
            StringAssert.Contains("#if UNITY_EDITOR || DEVELOPMENT_BUILD\n" +
                "            gameObject.AddComponent<DebugGameplayOverlay>()", presentation.Replace("\r\n", "\n"));
        }
    }
}
#endif
