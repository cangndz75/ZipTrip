namespace ZipTrip.Unity
{
    public enum MaterialFamily { Neutral, Fabric, Leather, Plastic, Paper, MetalGlass }

    // Unity presentation data only. Durations and easing are owned by MotionTokens.
    public readonly struct MaterialMotionProfile
    {
        public MaterialFamily Family { get; }
        public float LiftScale { get; }
        public float HeldScale { get; }
        public float SettleSquash { get; }
        public float SettleBounce { get; }
        public float SettleDuration { get; }

        public MaterialMotionProfile(MaterialFamily family, float liftScale, float heldScale,
            float settleSquash, float settleBounce, float settleDuration)
        {
            Family = family;
            LiftScale = liftScale;
            HeldScale = heldScale;
            SettleSquash = settleSquash;
            SettleBounce = settleBounce;
            SettleDuration = settleDuration;
        }
    }
}
