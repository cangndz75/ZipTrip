using System.IO;
using NUnit.Framework;
using UnityEngine;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public sealed class MaterialFeelTests
    {
        [TestCase("sweater", MaterialFamily.Fabric)]
        [TestCase("towel", MaterialFamily.Fabric)]
        [TestCase("passport", MaterialFamily.Paper)]
        [TestCase("shampoo", MaterialFamily.Plastic)]
        [TestCase("sunglasses", MaterialFamily.Plastic)]
        [TestCase("travel-pouch", MaterialFamily.Leather)]
        public void GoldenRoster_ResolvesPresentationMaterial(string id, MaterialFamily expected)
        {
            var profile = PuzzleItemCatalog.ResolveMotion(id);
            Assert.That(profile.Family, Is.EqualTo(expected));
            Assert.That(profile.SettleDuration, Is.GreaterThan(MotionTokens.ItemSettleFallDuration));
        }

        [Test]
        public void Profiles_UseCentralDurations_AndHaveDistinctResponse()
        {
            Assert.That(PuzzleItemCatalog.ResolveMotion("towel").SettleDuration, Is.EqualTo(MotionTokens.FabricSettleDuration));
            Assert.That(PuzzleItemCatalog.ResolveMotion("travel-pouch").SettleDuration, Is.EqualTo(MotionTokens.LeatherSettleDuration));
            Assert.That(PuzzleItemCatalog.ResolveMotion("shampoo").SettleDuration, Is.EqualTo(MotionTokens.PlasticSettleDuration));
            Assert.That(PuzzleItemCatalog.ResolveMotion("passport").SettleDuration, Is.EqualTo(MotionTokens.PaperSettleDuration));
            Assert.That(MotionTokens.SettleResponse(MaterialFamily.Fabric, 0.3f), Is.GreaterThan(0f));
            Assert.That(MotionTokens.SettleResponse(MaterialFamily.Fabric, 0.7f), Is.LessThan(0f));
            Assert.That(MotionTokens.SettleResponse(MaterialFamily.MetalGlass, 0.3f), Is.Zero);
            Assert.That(PuzzleItemCatalog.MetalGlassMotion.SettleDuration, Is.EqualTo(MotionTokens.MetalSettleDuration));
            Assert.That(PuzzleItemCatalog.MetalGlassMotion.SettleBounce, Is.Zero);
        }

        [Test]
        public void CueServices_DisabledAndMissingSourceAreSilent()
        {
            var root = new GameObject("feel services test");
            try
            {
                var haptics = root.AddComponent<HapticsService>();
                haptics.CuesEnabled = false;
                Assert.That(haptics.Play(FeelCue.ItemLift), Is.False);
                var audio = root.AddComponent<AudioCueService>();
                Assert.That(audio.Play(FeelCue.ItemSettle, MaterialFamily.Fabric), Is.False);
                audio.CuesEnabled = false;
                Assert.That(audio.Play(FeelCue.RuleSatisfied), Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void DomainAndApplicationAssemblies_KeepUnityOut()
        {
            foreach (var assembly in new[] { "Domain", "Application" })
            {
                var path = Path.Combine(UnityEngine.Application.dataPath, "Scripts", assembly, "ZipTrip." + assembly + ".asmdef");
                var text = File.ReadAllText(path);
                Assert.That(text, Does.Contain("\"noEngineReferences\": true"), assembly);
                Assert.That(text, Does.Not.Contain("ZipTrip.Unity"), assembly);
            }
        }
    }
}
