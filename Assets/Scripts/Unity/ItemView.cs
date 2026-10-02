using System;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    // ItemRoot (this transform) = placement; FeedbackRoot = transient tactile motion;
    // VisualRoot = persistent visual state. Feedback never reads or writes gameplay state.
    public sealed class ItemView : MonoBehaviour
    {
        public const float LiftScale = 1.025f;
        public const float LiftSeconds = 0.09f;
        public const float SettleSeconds = 0.16f;
        public const float RejectSeconds = 0.24f;
        public const float RotateSeconds = 0.18f;

        private Vector3 _pivot;
        private bool _lifted;
        private float _lift;
        // Elapsed seconds per channel; inactive channels are null.
        private float? _settleTime;
        private float? _rejectTime;
        private float? _rotateTime;
        private float _rotateFrom;
        private bool _feedbackActive;

        public ItemDefinition Item { get; private set; }
        public string ItemId { get; private set; }
        public string ShapeState { get; private set; }
        public Rotation Rotation { get; private set; }
        public ItemShape Footprint { get; private set; }
        public bool IsInTray { get; private set; }
        public Transform FeedbackRoot { get; private set; }
        public Transform VisualRoot { get; private set; }
        public GameObject VisualPrefabInstance { get; private set; }
        public bool FeedbackActive => _feedbackActive;
        // Tray-only hit padding in item-local cells; placed items keep the exact footprint.
        public float HitPadding { get; set; }
        public TraySlot? TraySlot { get; set; }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (Footprint == null)
                return false;
            var local = transform.InverseTransformPoint(worldPoint);
            var pad = IsInTray ? HitPadding : 0f;
            for (var i = 0; i < Footprint.OccupiedCells.Count; i++)
            {
                var cell = Footprint.OccupiedCells[i];
                if (local.x >= cell.X - pad && local.x < cell.X + 1f + pad &&
                    -local.z >= cell.Y - pad && -local.z < cell.Y + 1f + pad)
                    return true;
            }
            return false;
        }

        public void Present(ItemDefinition item, string shapeState, Rotation rotation,
            bool isInTray, Vector3 position, float scale, Color color,
            GameObject visualPrefab = null, Material runtimeMaterialTemplate = null)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            var result = item.GetRotatedShape(shapeState, rotation);
            if (!result.IsAccepted)
                throw new ArgumentException("Invalid item rotation.", nameof(rotation));

            Item = item;
            ItemId = item.Id;
            ShapeState = shapeState;
            Rotation = rotation;
            Footprint = result.Shape;
            IsInTray = isInTray;
            transform.position = position;
            transform.localScale = Vector3.one * scale;

            var width = 0;
            var depth = 0;
            for (var i = 0; i < Footprint.OccupiedCells.Count; i++)
            {
                width = Math.Max(width, Footprint.OccupiedCells[i].X + 1);
                depth = Math.Max(depth, Footprint.OccupiedCells[i].Y + 1);
            }
            _pivot = new Vector3(width * 0.5f, 0f, -depth * 0.5f);
            FeedbackRoot = new GameObject("Feedback Visual").transform;
            FeedbackRoot.SetParent(transform, false);
            FeedbackRoot.localPosition = _pivot;
            VisualRoot = new GameObject("Visual Root").transform;
            VisualRoot.SetParent(FeedbackRoot, false);
            VisualRoot.localPosition = -_pivot;

            if (visualPrefab != null)
            {
                VisualPrefabInstance = Instantiate(visualPrefab, VisualRoot, false);
                VisualPrefabInstance.name = visualPrefab.name;
                ApplyVisualRotation(item.ShapeStates[shapeState], rotation,
                    VisualPrefabInstance.transform);
                return;
            }

            foreach (var cell in Footprint.OccupiedCells)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Footprint " + cell.X + "," + cell.Y;
                block.transform.SetParent(VisualRoot, false);
                block.transform.localPosition = new Vector3(cell.X + 0.5f, 0.2f, -cell.Y - 0.5f);
                block.transform.localScale = new Vector3(0.86f, 0.28f, 0.86f);
                var renderer = block.GetComponent<Renderer>();
                if (runtimeMaterialTemplate != null)
                    renderer.sharedMaterial = runtimeMaterialTemplate;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(properties);
                Destroy(block.GetComponent<Collider>());
            }
        }

        // Footprint bbox united with rendered visual bounds, in item-local cells at the current scale.
        public TrayEntry MeasureTrayEntry()
        {
            var entry = new TrayEntry
            {
                MinX = 0f, MaxX = _pivot.x * 2f, MinZ = _pivot.z * 2f, MaxZ = 0f,
                Rotate = TrayAffordances.HasRotate(Item), Fold = TrayAffordances.HasFold(Item)
            };
            foreach (var renderer in VisualRoot.GetComponentsInChildren<Renderer>())
            {
                var b = renderer.bounds;
                for (var i = 0; i < 8; i++)
                {
                    var local = transform.InverseTransformPoint(new Vector3(
                        (i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y,
                        (i & 4) == 0 ? b.min.z : b.max.z));
                    entry.MinX = Mathf.Min(entry.MinX, local.x);
                    entry.MaxX = Mathf.Max(entry.MaxX, local.x);
                    entry.MinZ = Mathf.Min(entry.MinZ, local.z);
                    entry.MaxZ = Mathf.Max(entry.MaxZ, local.z);
                    entry.Height = Mathf.Max(entry.Height, local.y);
                }
            }
            return entry;
        }

        public void SetLifted(bool lifted)
        {
            if (lifted)
                ResetFeedback();
            _lifted = lifted;
            _feedbackActive = true;
        }

        public void PlaySettle(float delay = 0f)
        {
            _settleTime = -delay;
            _feedbackActive = true;
        }

        public void PlayReject()
        {
            _lifted = false;
            _lift = 0f;
            _rejectTime = 0f;
            _feedbackActive = true;
        }

        // Starts from the previous orientation and turns into the already-authoritative rotation.
        public void PlayRotateFrom(float degrees)
        {
            _rotateFrom = degrees;
            _rotateTime = 0f;
            _feedbackActive = true;
            ApplyFeedback();
        }

        public void ResetFeedback()
        {
            _lifted = false;
            _lift = 0f;
            _settleTime = _rejectTime = _rotateTime = null;
            _feedbackActive = false;
            if (FeedbackRoot == null)
                return;
            FeedbackRoot.localPosition = _pivot;
            FeedbackRoot.localRotation = Quaternion.identity;
            FeedbackRoot.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!_feedbackActive)
                return;
            var dt = Time.unscaledDeltaTime;
            _lift = Mathf.MoveTowards(_lift, _lifted ? 1f : 0f, dt / LiftSeconds);
            _settleTime = Advance(_settleTime, dt, SettleSeconds);
            _rejectTime = Advance(_rejectTime, dt, RejectSeconds);
            _rotateTime = Advance(_rotateTime, dt, RotateSeconds);
            ApplyFeedback();
            if (!_lifted && _lift <= 0f && _settleTime == null && _rejectTime == null && _rotateTime == null)
                ResetFeedback();
        }

        private void ApplyFeedback()
        {
            var lift = Mathf.Lerp(1f, LiftScale, EaseOut(_lift));
            var scale = new Vector3(lift, lift, lift);
            if (_settleTime >= 0f)
            {
                // One soft squash toward the floor after arrival.
                var s = Mathf.Sin(Mathf.Clamp01(_settleTime.Value / SettleSeconds) * Mathf.PI);
                scale = Vector3.Scale(scale, new Vector3(1f + 0.025f * s, 1f - 0.07f * s, 1f + 0.025f * s));
            }
            var offset = Vector3.zero;
            if (_rejectTime >= 0f)
            {
                var t = Mathf.Clamp01(_rejectTime.Value / RejectSeconds);
                offset.x = 0.09f * Mathf.Sin(t * Mathf.PI * 6f) * (1f - t);
            }
            var yaw = 0f;
            if (_rotateTime >= 0f)
                yaw = _rotateFrom * (1f - EaseOut(Mathf.Clamp01(_rotateTime.Value / RotateSeconds)));
            FeedbackRoot.localScale = scale;
            FeedbackRoot.localPosition = _pivot + offset;
            FeedbackRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static float? Advance(float? time, float dt, float duration)
        {
            if (time == null)
                return null;
            var next = time.Value + dt;
            return next > duration ? (float?)null : next;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        private static void ApplyVisualRotation(ItemShape authoredShape, Rotation rotation,
            Transform visual)
        {
            var width = 0;
            var height = 0;
            for (var i = 0; i < authoredShape.OccupiedCells.Count; i++)
            {
                width = Math.Max(width, authoredShape.OccupiedCells[i].X + 1);
                height = Math.Max(height, authoredShape.OccupiedCells[i].Y + 1);
            }

            visual.localRotation = Quaternion.Euler(0f, (int)rotation, 0f);
            visual.localScale = Vector3.one;
            switch (rotation)
            {
                case Rotation.Degrees0:
                    visual.localPosition = Vector3.zero;
                    break;
                case Rotation.Degrees90:
                    visual.localPosition = new Vector3(height, 0f, 0f);
                    break;
                case Rotation.Degrees180:
                    visual.localPosition = new Vector3(width, 0f, -height);
                    break;
                case Rotation.Degrees270:
                    visual.localPosition = new Vector3(0f, 0f, -width);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }
    }
}
