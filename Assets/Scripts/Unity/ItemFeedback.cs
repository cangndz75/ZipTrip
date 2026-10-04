using UnityEngine;

namespace ZipTrip.Unity
{
    // ZT-040D presentation feedback for one item view. Writes ONLY its own FeedbackRoot transform (between the item
    // root, which presentation places from canonical state, and the visual); never the item root, never state. Every
    // channel ends at the identity pose and CompleteAll() jumps there, so animation can never own placement.
    // Unscaled time; no randomness.
    public sealed class ItemFeedback : MonoBehaviour
    {
        public const float PressScale = 0.97f;
        public const float LiftScale = 1.06f;
        public const float HeldScale = 1.04f;
        public const float LiftDuration = MotionTokens.ItemLiftDuration;
        public const float SettleDuration = MotionTokens.ItemSettleDuration;
        public const float RejectDuration = MotionTokens.ItemRejectDuration;
        public const float ModifierDuration = MotionTokens.ItemModifierDuration;

        private enum Channel { None, Lift, Settle, Reject, Fold, Compress }

        private Channel _channel;
        private float _time;
        private Vector3 _rejectDirection;

        /// <summary>The pivot transform this component animates (footprint centre).</summary>
        public Transform Root { get; private set; }
        /// <summary>A settle or reject is playing (lift is held while dragging, not "playing").</summary>
        public bool IsAnimating => _channel == Channel.Settle || _channel == Channel.Reject
            || _channel == Channel.Fold || _channel == Channel.Compress;
        public bool IsHeld => _channel == Channel.Lift;

        internal void Attach(Transform root) => Root = root;

        /// <summary>Pick: quick squash, then grows past the held size and eases back (out-back). Held until released.</summary>
        public void PlayLift() => Begin(Channel.Lift);

        /// <summary>Valid drop: drops the last bit onto the lining with a short squash and rebound.</summary>
        public void PlaySettle() => Begin(Channel.Settle);

        public void PlayFold() => Begin(Channel.Fold);

        public void PlayCompress() => Begin(Channel.Compress);

        /// <summary>Rejected drop: small recoil along the drag direction and a decaying wobble at home. No state change.</summary>
        public void PlayReject(Vector3 dragDirection)
        {
            _rejectDirection = new Vector3(dragDirection.x, 0f, dragDirection.z).normalized;
            Begin(Channel.Reject);
        }

        /// <summary>Jumps every channel to its end pose (identity).</summary>
        public void CompleteAll()
        {
            _channel = Channel.None;
            _time = 0f;
            Apply(Vector3.zero, Vector3.one, 0f);
        }

        private void Begin(Channel channel)
        {
            CompleteAll();
            _channel = channel;
            Step(0f);
        }

        private void Update()
        {
            if (_channel != Channel.None)
                Step(Time.unscaledDeltaTime);
        }

        private void Step(float delta)
        {
            _time += delta;
            switch (_channel)
            {
                case Channel.Lift:
                {
                    // 0..40 ms press squash, then out-back to LiftScale, easing down to HeldScale.
                    var press = Mathf.Clamp01(_time / MotionTokens.ItemPressDuration);
                    var grow = Mathf.Clamp01((_time - MotionTokens.ItemPressDuration) / LiftDuration);
                    var hold = Mathf.Clamp01((_time - MotionTokens.ItemPressDuration - LiftDuration) / MotionTokens.ItemLiftHoldDuration);
                    var s = _time < MotionTokens.ItemPressDuration
                        ? Mathf.Lerp(1f, PressScale, press)
                        : Mathf.LerpUnclamped(PressScale, LiftScale, MotionTokens.OutBack(grow, MotionTokens.ItemLiftOutBackOvershoot));
                    s = Mathf.Lerp(s, HeldScale, MotionTokens.EaseOutCubic(hold));
                    Apply(Vector3.zero, Vector3.one * s, 0f);
                    break;
                }
                case Channel.Settle:
                {
                    // 0..90 ms: falls the last 0.12 onto the lining; then squash 1.04 x .92 rebounds to rest.
                    var fall = Mathf.Clamp01(_time / MotionTokens.ItemSettleFallDuration);
                    var y = Mathf.Lerp(0.12f, 0f, MotionTokens.EaseInQuadratic(fall));
                    var t = Mathf.Clamp01((_time - MotionTokens.ItemSettleFallDuration) / (SettleDuration - MotionTokens.ItemSettleFallDuration));
                    var squash = _time < MotionTokens.ItemSettleFallDuration ? 0f : 1f - MotionTokens.OutBack(t, MotionTokens.ItemSettleOutBackOvershoot);
                    var scale = new Vector3(1f + 0.04f * squash, 1f - 0.08f * squash, 1f + 0.04f * squash);
                    Apply(new Vector3(0f, y, 0f), scale, 0f);
                    if (_time >= SettleDuration)
                        CompleteAll();
                    break;
                }
                case Channel.Reject:
                {
                    // 60 ms recoil 0.12 back along the drag, then three decaying wobbles home.
                    var recoil = Mathf.Clamp01(_time / MotionTokens.ItemRejectRecoilDuration);
                    var t = Mathf.Clamp01((_time - MotionTokens.ItemRejectRecoilDuration) / (RejectDuration - MotionTokens.ItemRejectRecoilDuration));
                    var offset = _time < MotionTokens.ItemRejectRecoilDuration
                        ? -_rejectDirection * (0.12f * MotionTokens.EaseOutCubic(recoil))
                        : -_rejectDirection * (0.12f * (1f - MotionTokens.EaseOutCubic(t)));
                    var wobble = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 5f;
                    Apply(offset + Vector3.up * (0.2f * MotionTokens.SinePulse(t) * (1f - t)), Vector3.one, wobble);
                    if (_time >= RejectDuration)
                        CompleteAll();
                    break;
                }
                case Channel.Fold:
                {
                    var t = Mathf.Clamp01(_time / ModifierDuration);
                    var pulse = MotionTokens.SinePulse(t);
                    Apply(Vector3.up * (0.06f * pulse), new Vector3(1f - 0.06f * pulse, 1f - 0.1f * pulse, 1f - 0.06f * pulse),
                        5f * pulse);
                    if (_time >= ModifierDuration)
                        CompleteAll();
                    break;
                }
                case Channel.Compress:
                {
                    var t = Mathf.Clamp01(_time / ModifierDuration);
                    var press = MotionTokens.SinePulse(t);
                    Apply(Vector3.zero, new Vector3(1f, 1f - 0.24f * press, 1f), 0f);
                    if (_time >= ModifierDuration)
                        CompleteAll();
                    break;
                }
            }
        }

        private Vector3 _pivot;

        /// <summary>Pivot (item-local) the scale/tilt happen around; the visual is offset by -pivot under Root.</summary>
        internal void SetPivot(Vector3 pivot) => _pivot = pivot;

        private void Apply(Vector3 offset, Vector3 scale, float tiltDegrees)
        {
            if (Root == null)
                return;
            Root.localPosition = _pivot + offset;
            Root.localScale = scale;
            Root.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);
        }

    }
}
