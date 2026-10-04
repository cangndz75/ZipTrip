using NUnit.Framework;
using ZipTrip.Unity;

namespace ZipTrip.Tests.EditMode
{
    public sealed class MotionTokensTests
    {
        [Test]
        public void ShippedTimingValuesAndExistingPresenterAliases_ArePreserved()
        {
            Assert.That(MotionTokens.ItemPressDuration, Is.EqualTo(0.04f));
            Assert.That(MotionTokens.ItemLiftDuration, Is.EqualTo(0.14f));
            Assert.That(MotionTokens.ItemLiftHoldDuration, Is.EqualTo(0.08f));
            Assert.That(MotionTokens.ItemLiftOutBackOvershoot, Is.EqualTo(1.3f));
            Assert.That(MotionTokens.ItemSettleFallDuration, Is.EqualTo(0.09f));
            Assert.That(MotionTokens.ItemSettleDuration, Is.EqualTo(0.26f));
            Assert.That(MotionTokens.ItemSettleOutBackOvershoot, Is.EqualTo(1.4f));
            Assert.That(MotionTokens.ItemRejectRecoilDuration, Is.EqualTo(0.06f));
            Assert.That(MotionTokens.ItemRejectDuration, Is.EqualTo(0.28f));
            Assert.That(MotionTokens.ItemModifierDuration, Is.EqualTo(0.22f));
            Assert.That(MotionTokens.CompletionAnticipationDuration, Is.EqualTo(0.14f));
            Assert.That(MotionTokens.CompletionLidDuration, Is.EqualTo(0.64f));
            Assert.That(MotionTokens.CompletionZipDuration, Is.EqualTo(0.36f));
            Assert.That(MotionTokens.UiPressDuration, Is.EqualTo(0.16f));
            Assert.That(MotionTokens.RulePulseDuration, Is.EqualTo(0.32f));
            Assert.That(ItemFeedback.LiftDuration, Is.EqualTo(MotionTokens.ItemLiftDuration));
            Assert.That(ItemFeedback.SettleDuration, Is.EqualTo(MotionTokens.ItemSettleDuration));
            Assert.That(ItemFeedback.RejectDuration, Is.EqualTo(MotionTokens.ItemRejectDuration));
            Assert.That(ItemFeedback.ModifierDuration, Is.EqualTo(MotionTokens.ItemModifierDuration));
            Assert.That(PuzzleCompletionPresenter.SettleDuration, Is.EqualTo(MotionTokens.ItemSettleDuration));
            Assert.That(PuzzleCompletionPresenter.AnticipationDuration, Is.EqualTo(MotionTokens.CompletionAnticipationDuration));
            Assert.That(PuzzleCompletionPresenter.LidDuration, Is.EqualTo(MotionTokens.CompletionLidDuration));
            Assert.That(PuzzleCompletionPresenter.ZipDuration, Is.EqualTo(MotionTokens.CompletionZipDuration));
            Assert.That(PuzzleHud.PressDuration, Is.EqualTo(MotionTokens.UiPressDuration));
            Assert.That(PuzzleRulesPresenter.PulseDuration, Is.EqualTo(MotionTokens.RulePulseDuration));
        }

        [Test]
        public void ShippedEaseCurves_KeepTheirMidpointsAndEndpoints()
        {
            Assert.That(MotionTokens.EaseInQuadratic(0.5f), Is.EqualTo(0.25f));
            Assert.That(MotionTokens.EaseOutCubic(0.5f), Is.EqualTo(0.875f));
            Assert.That(MotionTokens.OutBack(0.5f, 1.3f), Is.EqualTo(1.0375f).Within(1e-6f));
            Assert.That(MotionTokens.LidSmoothStep(0.5f), Is.EqualTo(0.5f));
            Assert.That(MotionTokens.UiPressOutBack(0.5f), Is.EqualTo(1.075f).Within(1e-6f));
            Assert.That(MotionTokens.SinePulse(0.5f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(MotionTokens.LidSmoothStep(0f), Is.Zero);
            Assert.That(MotionTokens.LidSmoothStep(1f), Is.EqualTo(1f));
        }
    }
}
