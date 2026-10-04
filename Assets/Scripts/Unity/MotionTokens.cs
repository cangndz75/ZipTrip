using UnityEngine;

namespace ZipTrip.Unity
{
    /// <summary>Current shipped presentation timing and easing. Gameplay state never depends on these values.</summary>
    public static class MotionTokens
    {
        public const float ItemPressDuration = 0.04f;
        public const float ItemLiftDuration = 0.14f;
        public const float ItemLiftHoldDuration = 0.08f;
        public const float ItemLiftOutBackOvershoot = 1.3f;
        public const float ItemSettleFallDuration = 0.09f;
        public const float ItemSettleDuration = 0.26f;
        public const float ItemSettleOutBackOvershoot = 1.4f;
        public const float ItemRejectRecoilDuration = 0.06f;
        public const float ItemRejectDuration = 0.28f;
        public const float ItemModifierDuration = 0.22f;
        public const float CompletionSettleDuration = ItemSettleDuration;
        public const float CompletionAnticipationDuration = 0.14f;
        public const float CompletionLidDuration = 0.64f;
        public const float CompletionZipDuration = 0.36f;
        public const float UiPressDuration = 0.16f;
        public const float RulePulseDuration = 0.32f;
        public const float FabricSettleDuration = 0.34f;
        public const float LeatherSettleDuration = 0.29f;
        public const float PlasticSettleDuration = 0.23f;
        public const float PaperSettleDuration = 0.22f;
        public const float MetalSettleDuration = 0.18f;
        public const float DragTiltMaxDegrees = 8f;
        public const float DragTiltDegreesPerWorldUnit = 3f;

        public static float SettleResponse(MaterialFamily family, float t)
        {
            t = Mathf.Clamp01(t);
            switch (family)
            {
                case MaterialFamily.Fabric: return Mathf.Sin(t * Mathf.PI * 2f) * (1f - t);
                case MaterialFamily.Plastic: return Mathf.Sin(t * Mathf.PI * 4f) * (1f - t);
                case MaterialFamily.Leather: return 1f - EaseOutCubic(t);
                case MaterialFamily.Paper: return Mathf.Sin(t * Mathf.PI) * (1f - t);
                case MaterialFamily.MetalGlass: return 0f;
                default: return 1f - OutBack(t, ItemSettleOutBackOvershoot);
            }
        }

        public static float EaseInQuadratic(float t) => t * t;
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float OutBack(float t, float overshoot)
        {
            t -= 1f;
            return t * t * ((overshoot + 1f) * t + overshoot) + 1f;
        }

        public static float LidSmoothStep(float t) => t * t * (3f - 2f * t);
        public static float UiPressOutBack(float t)
        {
            var back = t - 1f;
            return back * back * (2.6f * back + 1.6f) + 1f;
        }

        public static float SinePulse(float t) => Mathf.Sin(t * Mathf.PI);
    }
}
