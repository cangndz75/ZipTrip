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
        public const float ReturnDurationSeconds = 0.2f;
        public const float FoldDurationSeconds = 0.55f;
        public const float CompletionStaggerSeconds = 0.05f;

        private sealed class GameplayControl
        {
            public bool Fold;
            public string ItemId;
            public Transform Transform;
        }

        private readonly List<GameplayControl> _controls = new List<GameplayControl>();
        private readonly List<ChipSpec> _chipSpecs = new List<ChipSpec>();
        private BoardPresenter _board;
        private Camera _camera;
        private PointerInteractor _pointer;
        private DragPreviewPresenter _preview;
        private GameplayHud _hud;
        private Action<string> _levelLoader;
        private LevelDefinition _level;
        private PackSession _session;
        private Transform _controlsRoot;
        private bool _controlPointerHeld;
        private bool _completed;
        private Vector2Int _composedScreen;
        private Rect _composedSafeArea;

        public PackSession Session => _session;
        public LevelDefinition Level => _level;
        public GameplayHud Hud => _hud;
        public bool IsAnimating { get; private set; }
        public bool IsCompleted => _completed;
        public int LevelCompletedCount { get; private set; }
        public bool PackedVisible => _hud.CompletionVisible;
        public int RotateAffordanceCount { get; private set; }
        public int FoldAffordanceCount { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public event Action<string, CommandResult> CommandResolved;
        public event Action AuthoritativeStateRefreshed;
        public event Action DebugOverlayRequested;
#endif

        public void Initialize(LevelDefinition level, BoardPresenter board, Camera camera,
            PointerInteractor pointer, DragPreviewPresenter preview, GameplayHud hud,
            Action<string> levelLoader)
        {
            if (_board != null)
                throw new InvalidOperationException("Pack gameplay already initialized.");
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
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
            _completed = false;
            _preview.InteractionEnabled = false;
            _preview.CancelActiveImmediate();
            RemoveControls();
            _hud.HideCompletion();
            _hud.CloseDrawer();
            _hud.SetLevel(level.Id);
            _session = new PackSession(level.InitialState, level.Items.Values);
            _board.Rebuild(level, _session.State);
            Compose();
            _preview.RefreshPresentedState();
            LevelCompletedCount = 0;
            RebuildControls();
            RefreshInteraction();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            AuthoritativeStateRefreshed?.Invoke();
#endif
        }

        // Frames camera and tray once per level from the full initial tray; never during play.
        private void Compose()
        {
            _composedScreen = new Vector2Int(_camera.pixelWidth, _camera.pixelHeight);
            _composedSafeArea = Screen.safeArea;
            var container = _session.State.Container;
            var frame = GameplayLayout.Compose(_composedScreen, _composedSafeArea,
                _board.ContainerVisualBounds, FixedGameplayCamera.ContainerCenter(container.Mask),
                FixedGameplayCamera.OuterBounds(container.Mask).width, _board.TrayEntries);
            _camera.GetComponent<FixedGameplayCamera>().ApplyFrame(container, frame);
            _board.ConfigureTray(frame.Tray);
            _board.RefreshItems(_level, _session.State);
        }

        private void Update()
        {
            // Resolution or safe-area change (editor resize, multi-window): recompose when idle.
            if (_session == null || IsAnimating || _preview.ActiveItem != null)
                return;
            if (_composedScreen == new Vector2Int(_camera.pixelWidth, _camera.pixelHeight) &&
                _composedSafeArea == Screen.safeArea)
                return;
            _board.Rebuild(_level, _session.State);
            Compose();
            RefreshAuthoritativePresentation();
        }

        public bool HasRotateAffordance(string itemId) => FindControl(itemId, false) != null;

        public bool HasFoldAffordance(string itemId) => FindControl(itemId, true) != null;

        public Transform FindControl(string itemId, bool fold)
        {
            for (var i = 0; i < _controls.Count; i++)
                if (_controls[i].Fold == fold && StringComparer.Ordinal.Equals(_controls[i].ItemId, itemId))
                    return _controls[i].Transform;
            return null;
        }

        public bool TryRotateTrayItem(string itemId)
        {
            if (IsAnimating || _completed || _preview.ActiveItem != null)
                return false;
            var item = _level.Items[itemId];
            if (!TrayAffordances.HasRotate(item))
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
            // State is already rotated; the new view turns in from the previous orientation.
            var delta = Mathf.DeltaAngle((int)current, (int)next);
            _board.FindItemView(itemId).PlayRotateFrom(-delta);
            return true;
        }

        public bool TryFoldTrayItem(string itemId)
        {
            if (IsAnimating || _completed || _preview.ActiveItem != null)
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
            RefreshInteraction();
            SetControlsVisible(false);
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
            _completed = false;
            _hud.HideCompletion();
            RefreshAuthoritativePresentation();
            LevelCompletedCount = 0;
            RefreshInteraction();
        }

        public bool TryActivateControl(Vector2 screenPosition)
        {
            if (_preview.ActiveItem != null || !_hud.TryHit(screenPosition, out var action))
                return false;
            _hud.Press(action);
            switch (action.Kind)
            {
                case HudActionKind.Rotate:
                    return TryRotateTrayItem(action.Value);
                case HudActionKind.Fold:
                    return TryFoldTrayItem(action.Value);
                case HudActionKind.Replay:
                case HudActionKind.DevReset:
                    _hud.CloseDrawer();
                    ResetLevel();
                    return true;
                case HudActionKind.Next:
                    _levelLoader(NextLevelId(_level.Id));
                    return true;
                case HudActionKind.DevLevel:
                    _hud.CloseDrawer();
                    _levelLoader(action.Value);
                    return true;
                case HudActionKind.DevToggle:
                    _hud.ToggleDrawer();
                    return true;
                case HudActionKind.DevOverlay:
                    _hud.CloseDrawer();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    DebugOverlayRequested?.Invoke();
#endif
                    return true;
                case HudActionKind.Dismiss:
                    _hud.CloseDrawer();
                    return true;
                default:
                    return false;
            }
        }

        private static string NextLevelId(string id)
        {
            for (var i = 0; i < PhaseALevels.Ids.Count - 1; i++)
                if (PhaseALevels.Ids[i] == id)
                    return PhaseALevels.Ids[i + 1];
            return null;
        }

        private void HandleControlPointer(PointerSignal signal)
        {
            if (signal.Phase == PointerPhase.Down && TryActivateControl(signal.ScreenPosition))
            {
                _controlPointerHeld = true;
                RefreshInteraction();
            }
            else if (signal.Phase == PointerPhase.Up || signal.Phase == PointerPhase.Cancel)
            {
                _hud.ReleasePress();
                if (_controlPointerHeld)
                {
                    _controlPointerHeld = false;
                    RefreshInteraction();
                }
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
                // Rejection: shake (second feedback channel beside color) while easing home.
                request.Item.PlayReject();
                StartCoroutine(Tween(request.Item.transform, request.VisualPosition,
                    request.RestPosition, Vector3.one, request.RestScale, ReturnDurationSeconds));
                return;
            }

            var itemId = request.Item.ItemId;
            RefreshAuthoritativePresentation();
            var snapped = _board.FindItemView(itemId)
                ?? throw new InvalidOperationException("Accepted item view is missing: " + itemId);
            var targetPosition = snapped.transform.position;
            var targetScale = snapped.transform.localScale;
            snapped.transform.position = request.VisualPosition;
            snapped.transform.localScale = Vector3.one;
            snapped.PlaySettle(SnapDurationSeconds);
            EmitEvents(result);
            StartCoroutine(Tween(snapped.transform, request.VisualPosition, targetPosition,
                Vector3.one, targetScale, SnapDurationSeconds));
        }

        private void RefreshAuthoritativePresentation()
        {
            _board.RefreshItems(_level, _session.State);
            _preview.RefreshPresentedState();
            RebuildControls();
        }

        private void RefreshInteraction() =>
            _preview.InteractionEnabled = !IsAnimating && !_controlPointerHeld && !_completed;

        private IEnumerator Tween(Transform target, Vector3 startPosition, Vector3 endPosition,
            Vector3 startScale, Vector3 endScale, float duration)
        {
            IsAnimating = true;
            RefreshInteraction();
            var elapsed = 0f;
            if (target != null)
            {
                target.position = startPosition;
                target.localScale = startScale;
            }
            yield return null;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                t = 1f - (1f - t) * (1f - t);
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
            RefreshInteraction();
        }

        private IEnumerator AnimateFold(string itemId, ItemView oldView)
        {
            var halfDuration = FoldDurationSeconds * 0.5f;
            var compressed = new Vector3(0.82f, 0.55f, 0.82f);
            var oldFeedback = oldView == null ? null : oldView.FeedbackRoot;
            if (oldView != null)
                oldView.ResetFeedback();
            var elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (oldFeedback != null)
                    oldFeedback.localScale = Vector3.Lerp(Vector3.one, compressed,
                        Mathf.Clamp01(elapsed / halfDuration));
                yield return null;
            }

            RefreshAuthoritativePresentation();
            var acceptedView = _board.FindItemView(itemId)
                ?? throw new InvalidOperationException("Folded item view is missing: " + itemId);
            var acceptedFeedback = acceptedView.FeedbackRoot;
            acceptedFeedback.localScale = compressed;
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / halfDuration);
                acceptedFeedback.localScale = t < 0.75f
                    ? Vector3.Lerp(compressed, Vector3.one * 1.06f, t / 0.75f)
                    : Vector3.Lerp(Vector3.one * 1.06f, Vector3.one, (t - 0.75f) / 0.25f);
                yield return null;
            }
            acceptedFeedback.localScale = Vector3.one;
            IsAnimating = false;
            RefreshInteraction();
            SetControlsVisible(true);
        }

        private void EmitEvents(CommandResult result)
        {
            for (var i = 0; i < result.Events.Count; i++)
                if (result.Events[i] is LevelCompletedEvent)
                {
                    LevelCompletedCount++;
                    _completed = true;
                    RefreshInteraction();
                    // Confirmation: placed items settle in reading order, then the result card enters.
                    var views = _board.ItemViews;
                    for (var v = 0; v < views.Count; v++)
                        if (!views[v].IsInTray)
                            views[v].PlaySettle(SnapDurationSeconds + CompletionStaggerSeconds * (v + 1));
                    _hud.ShowCompletion(_level.Id, NextLevelId(_level.Id) != null);
                }
        }

        private int FindTray(string itemId)
        {
            for (var i = 0; i < _session.State.Tray.Count; i++)
                if (StringComparer.Ordinal.Equals(_session.State.Tray[i].ItemId, itemId))
                    return i;
            return -1;
        }

        // Control anchors sit on the board plane at chip centers; the HUD draws chips over them.
        private void RebuildControls()
        {
            RemoveControls();
            _controlsRoot = new GameObject("ZT-013 Controls").transform;
            _controlsRoot.SetParent(transform, false);
            for (var i = 0; i < _board.ItemViews.Count; i++)
            {
                var view = _board.ItemViews[i];
                if (!view.IsInTray || view.TraySlot == null)
                    continue;
                var slot = view.TraySlot.Value;
                if (TrayAffordances.HasRotate(view.Item))
                {
                    AddControl("Rotate " + view.ItemId, slot.RotateChip, false, view.ItemId, false);
                    RotateAffordanceCount++;
                }
                if (TrayAffordances.HasFold(view.Item))
                {
                    var folded = !StringComparer.Ordinal.Equals(view.ShapeState, view.Item.BaseStateId);
                    AddControl((folded ? "Open " : "Fold ") + view.ItemId, slot.FoldChip, true,
                        view.ItemId, folded);
                    FoldAffordanceCount++;
                }
            }
            _hud.SetChips(_chipSpecs);
            SetControlsVisible(!IsAnimating && !_completed);
        }

        private void AddControl(string name, Vector3 position, bool fold, string itemId, bool folded)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(_controlsRoot, false);
            anchor.position = position + Vector3.up * 0.02f;
            _controls.Add(new GameplayControl { Fold = fold, ItemId = itemId, Transform = anchor });
            _chipSpecs.Add(new ChipSpec { ItemId = itemId, Fold = fold, Folded = folded, Anchor = anchor });
        }

        private void SetControlsVisible(bool visible)
        {
            if (_controlsRoot != null)
                _controlsRoot.gameObject.SetActive(visible);
            _hud.ChipsVisible = visible;
        }

        private void RemoveControls()
        {
            _controls.Clear();
            _chipSpecs.Clear();
            RotateAffordanceCount = 0;
            FoldAffordanceCount = 0;
            if (_controlsRoot == null)
                return;
            _controlsRoot.gameObject.SetActive(false);
            Destroy(_controlsRoot.gameObject);
            _controlsRoot = null;
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
