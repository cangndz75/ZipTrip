using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Application;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public sealed class PackGameplayController : MonoBehaviour
    {
        public const float SnapDurationSeconds = 0.1f;
        public const float FoldDurationSeconds = 0.55f;

        private enum ControlKind { Rotate, Fold, Reset, Level }

        private sealed class GameplayControl
        {
            public ControlKind Kind;
            public string Value;
            public Transform Transform;
        }

        private readonly List<GameplayControl> _controls = new List<GameplayControl>();
        private BoardPresenter _board;
        private Camera _camera;
        private PointerInteractor _pointer;
        private DragPreviewPresenter _preview;
        private Action<string> _levelLoader;
        private LevelDefinition _level;
        private PackSession _session;
        private Transform _controlsRoot;
        private GameObject _packedResult;
        private bool _controlPointerHeld;

        public PackSession Session => _session;
        public LevelDefinition Level => _level;
        public bool IsAnimating { get; private set; }
        public int LevelCompletedCount { get; private set; }
        public bool PackedVisible => _packedResult != null && _packedResult.activeSelf;
        public int RotateAffordanceCount { get; private set; }
        public int FoldAffordanceCount { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public event Action<string, CommandResult> CommandResolved;
        public event Action AuthoritativeStateRefreshed;
#endif

        public void Initialize(LevelDefinition level, BoardPresenter board, Camera camera,
            PointerInteractor pointer, DragPreviewPresenter preview, Action<string> levelLoader)
        {
            if (_board != null)
                throw new InvalidOperationException("Pack gameplay already initialized.");
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _levelLoader = levelLoader ?? throw new ArgumentNullException(nameof(levelLoader));
            _pointer.PointerEvent += HandleControlPointer;
            _preview.ReleaseRequested += HandleRelease;

            LoadLevel(level);
        }

        public void LoadLevel(LevelDefinition level)
        {
            _level = level ?? throw new ArgumentNullException(nameof(level));
            StopAllCoroutines();
            IsAnimating = false;
            _preview.InteractionEnabled = false;
            _preview.CancelActiveImmediate();
            RemoveControls();
            HidePacked();
            _session = new PackSession(level.InitialState, level.Items.Values);
            _board.Rebuild(level, _session.State);
            _camera.GetComponent<FixedGameplayCamera>().Configure(level.InitialState.Container,
                _camera.aspect, _board.PresentationBounds);
            _preview.RefreshPresentedState();
            LevelCompletedCount = 0;
            RebuildControls();
            _preview.InteractionEnabled = !_controlPointerHeld;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AuthoritativeStateRefreshed?.Invoke();
#endif
        }

        public bool HasRotateAffordance(string itemId)
        {
            for (var i = 0; i < _controls.Count; i++)
                if (_controls[i].Kind == ControlKind.Rotate &&
                    StringComparer.Ordinal.Equals(_controls[i].Value, itemId))
                    return true;
            return false;
        }

        public bool HasFoldAffordance(string itemId)
        {
            for (var i = 0; i < _controls.Count; i++)
                if (_controls[i].Kind == ControlKind.Fold &&
                    StringComparer.Ordinal.Equals(_controls[i].Value, itemId))
                    return true;
            return false;
        }

        public bool TryRotateTrayItem(string itemId)
        {
            if (IsAnimating || _preview.ActiveItem != null)
                return false;
            var item = _level.Items[itemId];
            if (item.AllowedRotations.Count <= 1)
                return false;
            var trayIndex = FindTray(itemId);
            if (trayIndex < 0)
                return false;
            var current = _session.State.Tray[trayIndex].Rotation;
            var rotationIndex = 0;
            for (var i = 0; i < item.AllowedRotations.Count; i++)
                if (item.AllowedRotations[i] == current)
                {
                    rotationIndex = i;
                    break;
                }
            var next = item.AllowedRotations[(rotationIndex + 1) % item.AllowedRotations.Count];
            var result = _session.RotateItem(itemId, next);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CommandResolved?.Invoke("RotateItem", result);
#endif
            if (!result.IsAccepted)
                throw new InvalidOperationException("Authored tray rotation was rejected: " + result.Reason);
            RefreshAuthoritativePresentation();
            return true;
        }

        public bool TryFoldTrayItem(string itemId)
        {
            if (IsAnimating || _preview.ActiveItem != null)
                return false;
            if (!_level.Items.TryGetValue(itemId, out var item))
                return false;

            string currentState = null;
            var trayIndex = FindTray(itemId);
            if (trayIndex >= 0)
                currentState = _session.State.Tray[trayIndex].ShapeState;
            else
            {
                for (var i = 0; i < _session.State.Placements.Count; i++)
                    if (StringComparer.Ordinal.Equals(_session.State.Placements[i].ItemId, itemId))
                    {
                        currentState = _session.State.Placements[i].ShapeState;
                        break;
                    }
            }
            if (currentState == null)
                return false;

            var nextState = currentState;
            if (item.AuthoredShapeStateIds.Count > 1)
            {
                var currentIndex = 0;
                for (var i = 0; i < item.AuthoredShapeStateIds.Count; i++)
                    if (StringComparer.Ordinal.Equals(item.AuthoredShapeStateIds[i], currentState))
                    {
                        currentIndex = i;
                        break;
                    }
                nextState = item.AuthoredShapeStateIds[
                    (currentIndex + 1) % item.AuthoredShapeStateIds.Count];
            }

            var oldView = _board.FindItemView(itemId);
            var result = _session.FoldItem(itemId, nextState);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CommandResolved?.Invoke("FoldItem", result);
#endif
            if (!result.IsAccepted)
                return false;

            EmitEvents(result);
            IsAnimating = true;
            _preview.InteractionEnabled = false;
            if (_controlsRoot != null)
                _controlsRoot.gameObject.SetActive(false);
            StartCoroutine(AnimateFold(itemId, oldView));
            return true;
        }

        public void ResetLevel()
        {
            StopAllCoroutines();
            IsAnimating = false;
            var result = _session.ResetLevel();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CommandResolved?.Invoke("ResetLevel", result);
#endif
            if (!result.IsAccepted)
                throw new InvalidOperationException("ResetLevel was rejected.");
            RefreshAuthoritativePresentation();
            HidePacked();
            LevelCompletedCount = 0;
        }

        public bool TryActivateControl(Vector2 screenPosition)
        {
            if (IsAnimating || _preview.ActiveItem != null)
                return false;
            for (var i = 0; i < _controls.Count; i++)
            {
                var control = _controls[i];
                var center = (Vector2)_camera.WorldToScreenPoint(control.Transform.position);
                var edge = (Vector2)_camera.WorldToScreenPoint(
                    control.Transform.position + Vector3.right * 0.3f);
                var radius = Mathf.Max(32f, Vector2.Distance(center, edge) * 1.6f);
                if (Vector2.Distance(screenPosition, center) > radius)
                    continue;
                if (control.Kind == ControlKind.Rotate)
                    return TryRotateTrayItem(control.Value);
                if (control.Kind == ControlKind.Fold)
                    return TryFoldTrayItem(control.Value);
                if (control.Kind == ControlKind.Reset)
                {
                    ResetLevel();
                    return true;
                }
                _levelLoader(control.Value);
                return true;
            }
            return false;
        }

        private void HandleControlPointer(PointerSignal signal)
        {
            if (signal.Phase == PointerPhase.Down && TryActivateControl(signal.ScreenPosition))
            {
                _controlPointerHeld = true;
                _preview.InteractionEnabled = false;
            }
            else if (_controlPointerHeld &&
                (signal.Phase == PointerPhase.Up || signal.Phase == PointerPhase.Cancel))
            {
                _controlPointerHeld = false;
                _preview.InteractionEnabled = !IsAnimating;
            }
        }

        private void HandleRelease(DragReleaseRequest request)
        {
            if (IsAnimating)
                return;
            CommandResult result = request.Item.IsInTray
                ? _session.PlaceItem(request.Item.ItemId, request.CandidateAnchor,
                    request.Item.Rotation, request.Item.ShapeState)
                : _session.MoveItem(request.Item.ItemId, request.CandidateAnchor);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CommandResolved?.Invoke(request.Item.IsInTray ? "PlaceItem" : "MoveItem", result);
#endif

            if (!result.IsAccepted)
            {
                StartCoroutine(Tween(request.Item.transform, request.VisualPosition,
                    request.RestPosition, Vector3.one, request.RestScale));
                return;
            }

            var itemId = request.Item.ItemId;
            RefreshAuthoritativePresentation();
            EmitEvents(result);
            var snapped = _board.FindItemView(itemId)
                ?? throw new InvalidOperationException("Accepted item view is missing: " + itemId);
            var targetPosition = snapped.transform.position;
            var targetScale = snapped.transform.localScale;
            snapped.transform.position = request.VisualPosition;
            snapped.transform.localScale = Vector3.one;
            StartCoroutine(Tween(snapped.transform, request.VisualPosition, targetPosition,
                Vector3.one, targetScale));
        }

        private void RefreshAuthoritativePresentation()
        {
            _board.RefreshItems(_level, _session.State);
            _preview.RefreshPresentedState();
            RebuildControls();
        }

        private IEnumerator Tween(Transform target, Vector3 startPosition, Vector3 endPosition,
            Vector3 startScale, Vector3 endScale)
        {
            IsAnimating = true;
            _preview.InteractionEnabled = false;
            var elapsed = 0f;
            if (target != null)
            {
                target.position = startPosition;
                target.localScale = startScale;
            }
            yield return null;
            while (elapsed < SnapDurationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / SnapDurationSeconds);
                if (target != null)
                {
                    target.position = Vector3.Lerp(startPosition, endPosition, t);
                    target.localScale = Vector3.Lerp(startScale, endScale, t);
                }
                yield return null;
            }
            if (target != null)
            {
                target.position = endPosition;
                target.localScale = endScale;
            }
            IsAnimating = false;
            _preview.InteractionEnabled = !_controlPointerHeld;
        }

        private IEnumerator AnimateFold(string itemId, ItemView oldView)
        {
            var halfDuration = FoldDurationSeconds * 0.5f;
            var compressed = new Vector3(0.82f, 0.55f, 0.82f);
            var oldVisual = oldView == null ? null : oldView.VisualRoot;
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (oldVisual != null)
                    oldVisual.localScale = Vector3.Lerp(Vector3.one, compressed,
                        Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }

            RefreshAuthoritativePresentation();
            if (_controlsRoot != null)
                _controlsRoot.gameObject.SetActive(false);
            var acceptedView = _board.FindItemView(itemId)
                ?? throw new InvalidOperationException("Folded item view is missing: " + itemId);
            var acceptedVisual = acceptedView.VisualRoot;
            acceptedVisual.localScale = compressed;
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                acceptedVisual.localScale = t < 0.75f
                    ? Vector3.Lerp(compressed, Vector3.one * 1.06f, t / 0.75f)
                    : Vector3.Lerp(Vector3.one * 1.06f, Vector3.one, (t - 0.75f) / 0.25f);
                yield return null;
            }
            acceptedVisual.localScale = Vector3.one;
            IsAnimating = false;
            _preview.InteractionEnabled = !_controlPointerHeld;
            if (_controlsRoot != null)
                _controlsRoot.gameObject.SetActive(true);
        }

        private void EmitEvents(CommandResult result)
        {
            for (var i = 0; i < result.Events.Count; i++)
                if (result.Events[i] is LevelCompletedEvent)
                {
                    LevelCompletedCount++;
                    ShowPacked();
                }
        }

        private int FindTray(string itemId)
        {
            for (var i = 0; i < _session.State.Tray.Count; i++)
                if (StringComparer.Ordinal.Equals(_session.State.Tray[i].ItemId, itemId))
                    return i;
            return -1;
        }

        private void RebuildControls()
        {
            RemoveControls();
            _controlsRoot = new GameObject("ZT-013 Controls").transform;
            _controlsRoot.SetParent(transform, false);
            RotateAffordanceCount = 0;
            FoldAffordanceCount = 0;
            var outer = FixedGameplayCamera.OuterBounds(_session.State.Container.Mask);
            for (var i = 0; i < _board.ItemViews.Count; i++)
            {
                var view = _board.ItemViews[i];
                if (!view.IsInTray)
                    continue;
                var renderers = view.VisualRoot.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds;
                for (var rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++)
                    bounds.Encapsulate(renderers[rendererIndex].bounds);
                if (view.Item.AllowedRotations.Count > 1)
                {
                    var position = new Vector3(bounds.max.x + 0.18f, 0.16f, bounds.center.z);
                    AddControl("Rotate " + view.ItemId, "R", position, ControlKind.Rotate,
                        view.ItemId, new Color(0.76f, 0.44f, 0.35f));
                    RotateAffordanceCount++;
                }
                if (view.Item.ShapeStates.Count > 1)
                {
                    var label = StringComparer.Ordinal.Equals(view.ShapeState, view.Item.BaseStateId)
                        ? "Fold" : "Open";
                    var screen = (Vector2)_camera.WorldToScreenPoint(bounds.center) +
                        Vector2.down * 140f;
                    var foldWorld = GridProjector.ScreenToWorld(_camera, screen);
                    var position = new Vector3(foldWorld.x, 0.16f, foldWorld.z);
                    AddControl(label + " " + view.ItemId, label, position, ControlKind.Fold,
                        view.ItemId, new Color(0.84f, 0.67f, 0.35f));
                    FoldAffordanceCount++;
                }
            }

            var topZ = -outer.yMin + 0.7f;
            AddControl("Reset", "Reset", new Vector3(outer.xMin + 0.6f, 0.12f, topZ),
                ControlKind.Reset, "reset", new Color(0.76f, 0.44f, 0.35f));
            AddControl("Level L1", "L1", new Vector3(outer.xMin + 2.0f, 0.12f, topZ),
                ControlKind.Level, "L1", new Color(0.28f, 0.59f, 0.6f));
            AddControl("Level L2", "L2", new Vector3(outer.xMin + 3.0f, 0.12f, topZ),
                ControlKind.Level, "L2", new Color(0.28f, 0.59f, 0.6f));
            AddControl("Level L3", "L3", new Vector3(outer.xMin + 4.0f, 0.12f, topZ),
                ControlKind.Level, "L3", new Color(0.28f, 0.59f, 0.6f));
            AddControl("Level L4", "L4", new Vector3(outer.xMin + 5.0f, 0.12f, topZ),
                ControlKind.Level, "L4", new Color(0.28f, 0.59f, 0.6f));
        }

        private void AddControl(string name, string label, Vector3 position, ControlKind kind,
            string value, Color color)
        {
            var control = GameObject.CreatePrimitive(PrimitiveType.Cube);
            control.name = name;
            control.transform.SetParent(_controlsRoot, false);
            control.transform.position = position;
            control.transform.localScale = kind == ControlKind.Rotate
                ? new Vector3(0.42f, 0.08f, 0.42f)
                : kind == ControlKind.Fold
                    ? new Vector3(0.62f, 0.08f, 0.42f)
                    : new Vector3(0.72f, 0.08f, 0.42f);
            Destroy(control.GetComponent<Collider>());
            var renderer = control.GetComponent<Renderer>();
            if (_board.RuntimeMaterialTemplate != null)
                renderer.sharedMaterial = _board.RuntimeMaterialTemplate;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);

            var text = new GameObject("Label").AddComponent<TextMesh>();
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.06f;
            text.color = Color.white;
            text.transform.SetParent(control.transform, false);
            text.transform.position = position + Vector3.up * 0.08f;
            text.transform.rotation = Quaternion.LookRotation(_camera.transform.forward,
                _camera.transform.up);
            _controls.Add(new GameplayControl { Kind = kind, Value = value, Transform = control.transform });
        }

        private void RemoveControls()
        {
            _controls.Clear();
            RotateAffordanceCount = 0;
            FoldAffordanceCount = 0;
            if (_controlsRoot == null)
                return;
            _controlsRoot.gameObject.SetActive(false);
            Destroy(_controlsRoot.gameObject);
            _controlsRoot = null;
        }

        private void ShowPacked()
        {
            if (_packedResult == null)
            {
                _packedResult = new GameObject("Packed Result");
                _packedResult.transform.SetParent(transform, false);
                var text = _packedResult.AddComponent<TextMesh>();
                text.text = "PACKED";
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 72;
                text.characterSize = 0.1f;
                text.color = new Color(0.91f, 0.9f, 0.86f);
                var outer = FixedGameplayCamera.OuterBounds(_session.State.Container.Mask);
                _packedResult.transform.position = new Vector3(outer.center.x, 0.8f, -outer.center.y);
                _packedResult.transform.rotation = Quaternion.LookRotation(_camera.transform.forward,
                    _camera.transform.up);
            }
            _packedResult.SetActive(true);
        }

        private void HidePacked()
        {
            if (_packedResult != null)
                _packedResult.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_pointer != null)
                _pointer.PointerEvent -= HandleControlPointer;
            if (_preview != null)
                _preview.ReleaseRequested -= HandleRelease;
        }
    }
}
