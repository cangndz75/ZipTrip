using System;
using UnityEngine;

namespace ZipTrip.Unity
{
    /// <summary>Presentation-only Zip It sequence. The scene starts it only for a PuzzleSession completion edge.</summary>
    public sealed class PuzzleCompletionPresenter : MonoBehaviour
    {
        public const float SettleDuration = ItemFeedback.SettleDuration;
        public const float AnticipationDuration = 0.14f;
        public const float LidDuration = 0.64f;
        public const float ZipDuration = 0.36f;

        public enum Phase { Idle, Settle, Anticipation, Lid, Zip, Confirmed }
        public Phase CurrentPhase { get; private set; }
        public int PlayCount { get; private set; }
        /// <summary>Disable only for deterministic frame capture; gameplay advances from unscaled time.</summary>
        public bool AutoAdvance { get; set; } = true;
        public event Action<string> FeedbackPoint;

        private ContainerRig _rig;
        private PuzzleHud _hud;
        private Rect _rim;
        private float _rimY;
        private float _elapsed;
        private LineRenderer _sweep;
        private Material _sweepMaterial;

        public void ResetForLevel(ContainerRig rig, Rect rim, float rimY, PuzzleHud hud)
        {
            if (_rig != null && _rig.Root != null)
                _rig.SetLidClosed(false);
            _rig = rig;
            _rim = rim;
            _rimY = rimY;
            _hud = hud;
            _elapsed = 0f;
            CurrentPhase = Phase.Idle;
            if (_rig != null)
                _rig.SetLidClosed(false);
            if (_sweep != null)
                _sweep.enabled = false;
            _hud?.SetCompletionVisible(false);
        }

        public void Begin()
        {
            if (CurrentPhase != Phase.Idle)
                return;
            PlayCount++;
            _elapsed = 0f;
            CurrentPhase = Phase.Settle;
            FeedbackPoint?.Invoke("final_item_settle");
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
            while (delta > 0f && CurrentPhase != Phase.Idle && CurrentPhase != Phase.Confirmed)
            {
                var duration = CurrentPhase == Phase.Settle ? SettleDuration
                    : CurrentPhase == Phase.Anticipation ? AnticipationDuration
                    : CurrentPhase == Phase.Lid ? LidDuration : ZipDuration;
                var consumed = Mathf.Min(delta, duration - _elapsed);
                _elapsed += consumed;
                delta -= consumed;
                RenderPhase();
                if (_elapsed < duration - 0.00001f)
                    break;
                _elapsed = 0f;
                switch (CurrentPhase)
                {
                    case Phase.Settle: CurrentPhase = Phase.Anticipation; break;
                    case Phase.Anticipation:
                        CurrentPhase = _rig != null ? Phase.Lid : Phase.Zip;
                        FeedbackPoint?.Invoke("lid_close");
                        break;
                    case Phase.Lid:
                        _rig.Lid.localRotation = ContainerRig.LidClosedLocalRotation;
                        CurrentPhase = Phase.Zip;
                        break;
                    case Phase.Zip:
                        if (_sweep != null) _sweep.enabled = false;
                        CurrentPhase = Phase.Confirmed;
                        FeedbackPoint?.Invoke("zip_complete");
                        _hud?.SetCompletionVisible(true);
                        FeedbackPoint?.Invoke("packed_confirm");
                        break;
                }
            }
        }

        private void RenderPhase()
        {
            if (CurrentPhase == Phase.Lid && _rig != null)
            {
                var t = Mathf.Clamp01(_elapsed / LidDuration);
                var ease = t * t * (3f - 2f * t);
                _rig.Lid.localRotation = Quaternion.Slerp(_rig.LidOpenLocalRotation,
                    ContainerRig.LidClosedLocalRotation, ease);
            }
            else if (CurrentPhase == Phase.Zip && _rig != null)
            {
                EnsureSweep();
                if (_sweep == null) return;
                var lidRenderer = _rig.Lid.GetComponent<Renderer>();
                if (lidRenderer != null)
                    _rimY = lidRenderer.bounds.max.y - 0.15f;
                var t = Mathf.Clamp01(_elapsed / ZipDuration);
                _sweep.enabled = true;
                _sweep.SetPosition(0, RimPoint(Mathf.Max(0f, t - 0.035f)));
                _sweep.SetPosition(1, RimPoint(t));
                var color = PresentationKit.Mustard;
                color.a = Mathf.Sin(t * Mathf.PI) * 0.65f;
                _sweep.startColor = _sweep.endColor = color;
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
        }
    }
}
