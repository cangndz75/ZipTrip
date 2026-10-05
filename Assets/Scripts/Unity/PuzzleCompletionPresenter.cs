using System;
using UnityEngine;

namespace ZipTrip.Unity
{
    /// <summary>Presentation-only Zip It sequence. The scene starts it only for a PuzzleSession completion edge.</summary>
    public sealed class PuzzleCompletionPresenter : MonoBehaviour
    {
        public const float SettleDuration = MotionTokens.CompletionSettleDuration;
        public const float AnticipationDuration = MotionTokens.CompletionAnticipationDuration;
        public const float RuleCascadeDuration = MotionTokens.CompletionRuleCascadeDuration;
        public const float StrapsDuration = MotionTokens.CompletionStrapsDuration;
        public const float LidDuration = MotionTokens.CompletionLidDuration;
        public const float ZipDuration = MotionTokens.CompletionZipDuration;
        public const float CelebrationDuration = MotionTokens.CompletionCelebrationDuration;
        public const float StampDuration = MotionTokens.CompletionStampDuration;
        public const float FullDuration = SettleDuration + AnticipationDuration + RuleCascadeDuration + StrapsDuration
            + LidDuration + ZipDuration + CelebrationDuration + StampDuration;

        public enum Phase { Idle, Settle, Anticipation, RuleCascade, Straps, Lid, Zip, Celebration, Stamp, Confirmed }
        public Phase CurrentPhase { get; private set; }
        public int PlayCount { get; private set; }
        public bool Replay { get; private set; }
        public bool Accelerated { get; private set; }
        public float TimelineTime { get; private set; }
        public bool ZipperComplete { get; private set; }
        public bool SignatureEnabled { get; private set; }
        /// <summary>Disable only for deterministic frame capture; gameplay advances from unscaled time.</summary>
        public bool AutoAdvance { get; set; } = true;
        public event Action<string> FeedbackPoint;

        private ContainerRig _rig;
        private SuitcaseRig _suitcase;
        private PuzzleHud _hud;
        private PuzzleRulesPresenter _rules;
        private HapticsService _haptics;
        private AudioCueService _audio;
        private Rect _rim;
        private float _rimY;
        private float _elapsed;
        private LineRenderer _sweep;
        private Material _sweepMaterial;
        private Vector3 _rootScale;
        private ParticleSystem _celebration;
        private Material _particleMaterial;
        private int _strapCues;
        private int _zipTicks;

        public void Configure(PuzzleRulesPresenter rules, HapticsService haptics, AudioCueService audio)
        {
            _rules = rules;
            _haptics = haptics;
            _audio = audio;
        }

        public void ResetForLevel(ContainerRig rig, Rect rim, float rimY, PuzzleHud hud, bool signature = false)
        {
            if (_rig != null && _rig.Root != null)
            {
                _rig.SetLidClosed(false);
                _rig.Root.localScale = _rootScale;
                SetLidVisible(true);
            }
            _rig = rig;
            SignatureEnabled = signature && rig != null;
            _suitcase = SignatureEnabled ? rig.Root.GetComponent<SuitcaseRig>() : null;
            if (SignatureEnabled && _suitcase == null)
                _suitcase = rig.Root.gameObject.AddComponent<SuitcaseRig>();
            if (SignatureEnabled)
                _suitcase.Bind(rig);
            _suitcase?.RequireLid();
            _rootScale = rig != null ? rig.Root.localScale : Vector3.one;
            _rim = rim;
            _rimY = rimY;
            _hud = hud;
            _elapsed = 0f;
            TimelineTime = 0f;
            Accelerated = false;
            ZipperComplete = false;
            _strapCues = _zipTicks = 0;
            CurrentPhase = Phase.Idle;
            if (_rig != null)
            {
                _rig.SetLidClosed(false);
                // ART-CC02: while packing the lid stays in the hierarchy but outside the composition; Zip It shows it.
                SetLidVisible(false);
            }
            _suitcase?.SetStraps(0f, 0f);
            if (_suitcase != null && _suitcase.HasZipperPath)
                _suitcase.ZipperPull.position = _suitcase.ZipperStart.position;
            if (_sweep != null)
                _sweep.enabled = false;
            if (SignatureEnabled)
                EnsureCelebration();
            if (_celebration != null)
                _celebration.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _hud?.SetCompletionVisible(false);
        }

        public void Begin(bool replay = false)
        {
            if (CurrentPhase != Phase.Idle)
                return;
            PlayCount++;
            Replay = SignatureEnabled && replay;
            Accelerated = false;
            TimelineTime = 0f;
            _elapsed = 0f;
            CurrentPhase = Phase.Settle;
            SetLidVisible(true);
            FeedbackPoint?.Invoke("final_item_settle");
        }

        /// <summary>The lid's renderers are drawn (Zip It) or hidden (packing).</summary>
        public bool LidVisible { get; private set; } = true;

        private void SetLidVisible(bool visible)
        {
            LidVisible = visible;
            if (_rig != null && _rig.Lid != null)
                foreach (var renderer in _rig.Lid.GetComponentsInChildren<Renderer>(true))
                    renderer.enabled = visible;
        }

        public bool Accelerate()
        {
            if (CurrentPhase == Phase.Idle || CurrentPhase == Phase.Confirmed
                || !SignatureEnabled
                || TimelineTime + 0.00001f < MotionTokens.CompletionAccelerateAfter)
                return false;
            Accelerated = true;
            return true;
        }

        private void Update()
        {
            if (AutoAdvance)
                Advance(Time.unscaledDeltaTime);
        }

        /// <summary>Deterministic timing hook for tests and capture; never writes canonical state.</summary>
        public void Advance(float delta)
        {
            if (delta < 0f)
                throw new ArgumentOutOfRangeException(nameof(delta));
            var speed = (SignatureEnabled && Replay ? FullDuration / MotionTokens.CompletionReplayDuration : 1f)
                * (Accelerated ? MotionTokens.CompletionAcceleration : 1f);
            delta *= speed;
            while (delta > 0f && CurrentPhase != Phase.Idle && CurrentPhase != Phase.Confirmed)
            {
                var duration = CurrentPhase == Phase.Settle ? SettleDuration
                    : CurrentPhase == Phase.Anticipation ? AnticipationDuration
                    : CurrentPhase == Phase.RuleCascade ? RuleCascadeDuration
                    : CurrentPhase == Phase.Straps ? StrapsDuration
                    : CurrentPhase == Phase.Lid ? LidDuration
                    : CurrentPhase == Phase.Zip ? ZipDuration
                    : CurrentPhase == Phase.Celebration ? CelebrationDuration : StampDuration;
                var consumed = Mathf.Min(delta, duration - _elapsed);
                _elapsed += consumed;
                TimelineTime += consumed;
                delta -= consumed;
                RenderPhase();
                if (_elapsed < duration - 0.00001f)
                    break;
                _elapsed = 0f;
                switch (CurrentPhase)
                {
                    case Phase.Settle: CurrentPhase = Phase.Anticipation; break;
                    case Phase.Anticipation:
                        CurrentPhase = SignatureEnabled ? Phase.RuleCascade : _rig != null ? Phase.Lid : Phase.Zip;
                        if (SignatureEnabled) _rules?.BeginCompletionCascade();
                        break;
                    case Phase.RuleCascade:
                        CurrentPhase = Phase.Straps;
                        _rules?.ClearForCompletion();
                        break;
                    case Phase.Straps:
                        _suitcase?.SetStraps(1f, 1f);
                        CurrentPhase = Phase.Lid;
                        FeedbackPoint?.Invoke("lid_close");
                        break;
                    case Phase.Lid:
                        if (_rig != null) (_suitcase != null ? _suitcase.LidPivot : _rig.Lid).localRotation
                            = ContainerRig.LidClosedLocalRotation;
                        CurrentPhase = Phase.Zip;
                        Cue(FeelCue.LidContact, "lid_contact");
                        break;
                    case Phase.Zip:
                        if (_sweep != null) _sweep.enabled = false;
                        ZipperComplete = true;
                        CurrentPhase = SignatureEnabled ? Phase.Celebration : Phase.Confirmed;
                        Cue(FeelCue.ZipComplete, "zip_complete");
                        if (SignatureEnabled) PlayCelebration();
                        else
                        {
                            _hud?.SetCompletionVisible(true);
                            _hud?.SetStampProgress(1f);
                            _hud?.SetCompletionActionsVisible(true);
                            FeedbackPoint?.Invoke("packed_confirm");
                        }
                        break;
                    case Phase.Celebration:
                        if (_rig != null) _rig.Root.localScale = _rootScale;
                        CurrentPhase = Phase.Stamp;
                        _hud?.SetCompletionVisible(true);
                        _hud?.SetStampProgress(0f);
                        Cue(FeelCue.Celebration, "celebration_success");
                        break;
                    case Phase.Stamp:
                        _hud?.SetStampProgress(1f);
                        _hud?.SetCompletionActionsVisible(true);
                        CurrentPhase = Phase.Confirmed;
                        Cue(FeelCue.Stamp, "stamp");
                        FeedbackPoint?.Invoke("packed_confirm");
                        break;
                }
            }
        }

        private void RenderPhase()
        {
            if (CurrentPhase == Phase.Anticipation && _rig != null)
                _rig.Root.localScale = _rootScale * Mathf.Lerp(1f, MotionTokens.CompletionBreathScale,
                    MotionTokens.SinePulse(_elapsed / AnticipationDuration));
            else if (CurrentPhase == Phase.RuleCascade)
            {
                if (_rig != null) _rig.Root.localScale = _rootScale;
                while (_rules != null && _rules.ConfirmedRuleCount < _rules.CompletionRuleCount
                    && _elapsed >= _rules.ConfirmedRuleCount * MotionTokens.CompletionRuleStagger)
                {
                    _rules.ConfirmCompletionRule(_rules.ConfirmedRuleCount);
                    Cue(FeelCue.RuleSatisfied, "rule_confirm");
                }
            }
            else if (CurrentPhase == Phase.Straps && _suitcase != null)
            {
                var a = Mathf.Clamp01(_elapsed / (StrapsDuration - MotionTokens.CompletionSecondStrapDelay));
                var b = Mathf.Clamp01((_elapsed - MotionTokens.CompletionSecondStrapDelay)
                    / (StrapsDuration - MotionTokens.CompletionSecondStrapDelay));
                _suitcase.SetStraps(MotionTokens.LidSmoothStep(a), MotionTokens.LidSmoothStep(b));
                while (_strapCues < 2 && _elapsed >= (_strapCues + 1)
                    * (StrapsDuration - MotionTokens.CompletionSecondStrapDelay))
                {
                    _strapCues++;
                    Cue(FeelCue.StrapBuckle, "strap_buckle");
                }
            }
            if (CurrentPhase == Phase.Lid && _rig != null)
            {
                var t = Mathf.Clamp01(_elapsed / LidDuration);
                var ease = MotionTokens.LidSmoothStep(t);
                (_suitcase != null ? _suitcase.LidPivot : _rig.Lid).localRotation = Quaternion.Slerp(_rig.LidOpenLocalRotation,
                    ContainerRig.LidClosedLocalRotation, ease);
            }
            else if (CurrentPhase == Phase.Zip && _rig != null)
            {
                var t = Mathf.Clamp01(_elapsed / ZipDuration);
                while (_zipTicks < 2 && t >= (_zipTicks + 1f) / 3f)
                {
                    _zipTicks++;
                    Cue(FeelCue.ZipTick, "zip_tick");
                }
                if (_suitcase != null && _suitcase.HasZipperPath)
                {
                    _suitcase.ZipperPull.position = Vector3.Lerp(_suitcase.ZipperStart.position,
                        _suitcase.ZipperEnd.position, MotionTokens.LidSmoothStep(t));
                    return;
                }
                EnsureSweep();
                if (_sweep == null) return;
                var lidRenderer = _rig.Lid.GetComponent<Renderer>();
                if (lidRenderer != null)
                    _rimY = lidRenderer.bounds.max.y - 0.15f;
                _sweep.enabled = true;
                _sweep.SetPosition(0, RimPoint(Mathf.Max(0f, t - 0.035f)));
                _sweep.SetPosition(1, RimPoint(t));
                var color = PresentationKit.Mustard;
                color.a = Mathf.Sin(t * Mathf.PI) * 0.65f;
                _sweep.startColor = _sweep.endColor = color;
            }
            else if (CurrentPhase == Phase.Celebration && _rig != null)
            {
                _rig.Root.localScale = _rootScale * (1f + (MotionTokens.CompletionCelebrationScale - 1f)
                    * MotionTokens.SinePulse(_elapsed / CelebrationDuration));
                if (!AutoAdvance && _celebration != null)
                    _celebration.Simulate(_elapsed, true, true);
            }
            else if (CurrentPhase == Phase.Stamp)
                _hud?.SetStampProgress(_elapsed / StampDuration);
        }

        private void Cue(FeelCue cue, string point)
        {
            _haptics?.Play(cue);
            _audio?.Play(cue);
            FeedbackPoint?.Invoke(point);
        }

        private void PlayCelebration()
        {
            if (_rig == null) return;
            EnsureCelebration();
            var lidRenderer = _suitcase.LidPivot.GetComponent<Renderer>();
            _celebration.transform.position = _suitcase.CelebrationOrigin != _suitcase.transform || lidRenderer == null
                ? _suitcase.CelebrationOrigin.position
                : new Vector3(lidRenderer.bounds.center.x, lidRenderer.bounds.max.y + 0.08f, lidRenderer.bounds.center.z);
            _celebration.Play(true);
        }

        private void EnsureCelebration()
        {
            if (_celebration == null)
            {
                var particles = new GameObject("Completion Celebration");
                particles.transform.SetParent(transform, false);
                _celebration = particles.AddComponent<ParticleSystem>();
                _celebration.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _celebration.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = CelebrationDuration;
                main.startLifetime = 0.45f;
                main.startSpeed = 1.2f;
                main.startSize = 0.085f;
                main.maxParticles = 40;
                main.startColor = PresentationKit.Mustard;
                main.useUnscaledTime = true;
                var emission = _celebration.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _particleMaterial = new Material(shader);
                    _celebration.GetComponent<ParticleSystemRenderer>().sharedMaterial = _particleMaterial;
                }
            }
        }

        private Vector3 RimPoint(float t)
        {
            var distance = t * 4f;
            const float seamInset = 0.42f;
            var left = _rim.xMin + seamInset;
            var right = _rim.xMax - seamInset;
            var front = _rim.yMin + seamInset;
            var back = _rim.yMax - seamInset;
            var x = left;
            var z = front;
            if (distance < 1f) x = Mathf.Lerp(left, right, distance);
            else if (distance < 2f) { x = right; z = Mathf.Lerp(front, back, distance - 1f); }
            else if (distance < 3f) { x = Mathf.Lerp(right, left, distance - 2f); z = back; }
            else z = Mathf.Lerp(back, front, distance - 3f);
            return new Vector3(x, _rimY, z);
        }

        private void EnsureSweep()
        {
            if (_sweep != null) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            _sweepMaterial = new Material(shader);
            _sweep = new GameObject("Zip Rim Sweep").AddComponent<LineRenderer>();
            _sweep.transform.SetParent(transform, false);
            _sweep.material = _sweepMaterial;
            _sweep.useWorldSpace = true;
            _sweep.positionCount = 2;
            _sweep.widthMultiplier = 0.045f;
            _sweep.numCapVertices = 2;
            _sweep.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _sweep.receiveShadows = false;
        }

        private void OnDestroy()
        {
            if (_sweepMaterial != null) Destroy(_sweepMaterial);
            if (_particleMaterial != null) Destroy(_particleMaterial);
        }
    }
}
