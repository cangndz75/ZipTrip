using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    public enum DragBeginResult
    {
        Started = 0,
        AlreadyDragging = 1,
        UnknownItem = 2,
        /// <summary>Only Source Tray and suitcase items are draggable in ZT-039 (no staging, nest or destination UI).</summary>
        NotDraggable = 3,
        /// <summary>The suitcase item cannot be taken (AccessQueries); e.g. it is blocked.</summary>
        NotAccessible = 4,
        /// <summary>Interaction is locked (e.g. the level is complete).</summary>
        InteractionLocked = 5
    }

    // ZT-039 drag / place for the ADR-0006 runtime. Pointer -> candidate (compartment, anchor, rotation) ->
    // PuzzleTransitions.Apply on the current state for the preview (pure, no session history) -> on drop exactly one
    // PuzzleSession.Apply with the same move -> explicit presenter Sync. Legality, layer and access always come from the
    // Domain; drag state (id, rotation, anchor, offset) is presentation-only and never canonical.
    // ZT-040B visual language: the grid stays invisible until a candidate exists, and everything disappears on drop /
    // cancel. ZT-040C: the candidate is shown as one soft footprint-shaped glow (green fits / red does not) with the
    // offending region emphasised; no cell tiles or neighbour guides ("this item fits here", not "these cells").
    public sealed class PuzzleDragController : MonoBehaviour
    {
        public const float LiftHeight = 0.5f;
        private const float GhostLift = 0.03f;
        private const float InvalidPreviewHeight = LiftHeight - 0.08f;
        // ART-CC02 blue = valid drop (current interaction); gold is reserved for the active rule region.
        private static readonly Color ValidColor = new Color(0.30f, 0.70f, 0.95f, 0.62f);
        private static readonly Color InvalidColor = new Color(0.92f, 0.30f, 0.24f, 0.62f);
        private static readonly Color MarkColor = new Color(0.62f, 0.07f, 0.05f, 0.95f);

        private readonly List<Cell> _marked = new List<Cell>();
        private readonly SoftCellShape _footprintShape = new SoftCellShape("Candidate Footprint");
        private readonly SoftCellShape _offendingShape = new SoftCellShape("Offending Region");
        private Material _footprintMaterial;
        private Material _markMaterial;
        private PuzzleSession _session;
        private PuzzleBoardPresenter _board;
        private PuzzleTrayPresenter _tray;
        private Camera _camera;
        private PointerInteractor _pointer;
        private PuzzleItemView _view;
        private PuzzleItem _item;
        private Vector3 _grabOffset;
        private Vector3 _lastPointer;
        private bool _hasDragSample;
        private Vector3 _dragStart;
        private Transform _ghostRoot;
        private PuzzleStagingPresenter _staging;
        private string _selectedOutsideId;
        private string _candidateStateId;
        private ItemModifier? _candidateModifier;
        private bool _temporaryNestedView;
        private HapticsService _haptics;
        private AudioCueService _audio;

        public void ConfigureCues(HapticsService haptics, AudioCueService audio)
        {
            _haptics = haptics;
            _audio = audio;
        }

        public PuzzleSession Session => _session;
        public bool IsDragging => _view != null;
        public string DraggedInstanceId => _item?.InstanceId;
        public Rotation CandidateRotation { get; private set; }
        /// <summary>True when the dragged footprint is over a compartment (a drop then attempts a move).</summary>
        public bool HasCandidate { get; private set; }
        /// <summary>ZT-041: staging slot under the pointer that a drop would target (-1 = none). Excludes the item's own slot.</summary>
        public int CandidateStagingSlot { get; private set; } = -1;
        public string CandidateNestParent { get; private set; }
        public string SelectedCandidateStateId => _candidateStateId;
        public string CandidateCompartment { get; private set; }
        public Cell CandidateAnchor { get; private set; }
        /// <summary>Domain outcome of the candidate move on the current state; null without a candidate.</summary>
        public MoveResult Preview { get; private set; }
        public bool PreviewValid => Preview != null && Preview.IsAccepted;
        /// <summary>Domain-resolved layer of a valid preview; -1 otherwise.</summary>
        public int PreviewLayer { get; private set; } = -1;
        public IReadOnlyList<Cell> MarkedCells => _marked;
        /// <summary>Footprint cells represented by the visible candidate glow (0 = no preview shown).</summary>
        public int GhostCellCount { get; private set; }
        /// <summary>The single candidate footprint glow and the offending-region glow (presentation only).</summary>
        public Renderer FootprintPreview => _footprintShape.Renderer;
        public Renderer OffendingPreview => _offendingShape.Renderer;
        public PuzzleSessionStep LastStep { get; private set; }
        /// <summary>Raised after every drop that reached PuzzleSession.Apply (accepted or rejected), after presenters re-sync.</summary>
        public event Action<PuzzleSessionStep> StepCommitted;
        /// <summary>False locks new drags (e.g. after completion). Presentation-only.</summary>
        public bool InteractionEnabled { get; set; } = true;

        public void Initialize(PuzzleSession session, PuzzleBoardPresenter board, PuzzleTrayPresenter tray, Camera camera,
            PointerInteractor pointer = null, PuzzleStagingPresenter staging = null)
        {
            _staging = staging;
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _tray = tray ?? throw new ArgumentNullException(nameof(tray));
            _tray.DisplayItem = DisplayItem;
            if (_staging != null)
                _staging.DisplayItem = DisplayItem;
            _camera = camera;
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
            _pointer = pointer;
            if (_pointer != null)
                _pointer.PointerEvent += HandlePointer;
            if (_ghostRoot == null)
            {
                _ghostRoot = new GameObject("Placement Preview").transform;
                _ghostRoot.SetParent(transform, false);
            }
            EnsurePreviewMaterials();
            _view = null;
            _item = null;
            _selectedOutsideId = null;
            _candidateStateId = null;
            _candidateModifier = null;
            _temporaryNestedView = false;
            CandidateNestParent = null;
            HasCandidate = false;
            Preview = null;
            LastStep = null;
            RenderGhost();
            SyncPresenters();
        }

        public DragBeginResult BeginDrag(string instanceId, Vector3 pointerWorld)
        {
            if (IsDragging)
                return DragBeginResult.AlreadyDragging;
            if (!InteractionEnabled)
                return DragBeginResult.InteractionLocked;
            var state = _session.CurrentState;
            if (!state.TryGetItem(instanceId, out var item))
                return DragBeginResult.UnknownItem;

            PuzzleItemView view;
            switch (item.Location.Kind)
            {
                case ItemLocationKind.SourceTray:
                    if (!_tray.ItemViews.TryGetValue(instanceId, out view))
                        return DragBeginResult.UnknownItem;
                    _tray.Select(instanceId);
                    SelectOutside(item);
                    break;
                case ItemLocationKind.Suitcase:
                    if (!AccessQueries.CanTake(state, instanceId))
                        return DragBeginResult.NotAccessible;
                    if (!_board.ItemViews.TryGetValue(instanceId, out view))
                        return DragBeginResult.UnknownItem;
                    break;
                case ItemLocationKind.Staging:
                    // ZT-041: a staged item is external (always reachable) and may go back into the suitcase.
                    if (_staging == null || !_staging.ItemViews.TryGetValue(instanceId, out view))
                        return DragBeginResult.UnknownItem;
                    SelectOutside(item);
                    break;
                case ItemLocationKind.Nested:
                    if (AccessQueries.GetAccess(state, instanceId) != ItemAccess.Accessible
                        && AccessQueries.GetAccess(state, instanceId) != ItemAccess.External)
                        return DragBeginResult.NotAccessible;
                    var parentView = ViewOf(item.Location.TargetId);
                    if (parentView == null)
                        return DragBeginResult.UnknownItem;
                    view = new GameObject("Nested Drag " + instanceId).AddComponent<PuzzleItemView>();
                    view.BindLoose(item, transform, transform.InverseTransformPoint(pointerWorld + new Vector3(-0.5f, 0f, 0.5f)),
                        item.State.AllowedRotations[0], _board.ResolveVisual(item), _board.Template, _board.ColorFor(item.Definition.Id));
                    _temporaryNestedView = true;
                    break;
                default:
                    return DragBeginResult.NotDraggable;
            }

            _item = item;
            _view = view;
            UpdateNestTargetCues();
            // FIX-SLICE-00: the lifted item keeps its real art (an X-ray ghost is lifted too; Sync restores it). Fit is
            // read from the footprint glow on the board, the contact shadow under it and a red tint when it does not fit.
            _view.SetGhost(false, null);
            CandidateRotation = view.Rotation;
            // Tray items are drawn smaller; keep the grabbed point under the pointer when the item grows to board size.
            var origin = view.transform.position;
            var scale = Mathf.Approximately(view.transform.lossyScale.x, 0f) ? 1f : view.transform.lossyScale.x;
            _grabOffset = new Vector3(origin.x - pointerWorld.x, 0f, origin.z - pointerWorld.z) / scale;
            view.transform.localScale = Vector3.one;
            _dragStart = pointerWorld;
            _hasDragSample = false;
            view.Feedback?.PlayLift();
            _haptics?.Play(FeelCue.ItemLift);
            _audio?.Play(FeelCue.ItemLift, view.Feedback.Profile.Family);
            UpdateDrag(pointerWorld);
            return DragBeginResult.Started;
        }

        public void UpdateDrag(Vector3 pointerWorld)
        {
            if (!IsDragging)
                return;
            if (_hasDragSample)
                _view.Feedback?.SetDragTilt((pointerWorld - _lastPointer) / Mathf.Max(Time.unscaledDeltaTime, 0.016f));
            _lastPointer = pointerWorld;
            _hasDragSample = true;
            var anchorWorld = new Vector3(pointerWorld.x, 0f, pointerWorld.z) + _grabOffset;
            _view.transform.position = anchorWorld + Vector3.up * LiftHeight;
            var lossy = _view.transform.lossyScale.y;
            _view.SetShadowDrop(Mathf.Approximately(lossy, 0f) ? 0f : LiftHeight / lossy);

            _marked.Clear();
            CandidateNestParent = null;
            // ZT-041: a staging pad under the finger takes priority over the board; the Domain previews the slot move.
            CandidateStagingSlot = StagingSlotUnder(pointerWorld);
            if (CandidateStagingSlot >= 0)
            {
                HasCandidate = false;
                CandidateCompartment = null;
                CandidateAnchor = default;
                Preview = PuzzleTransitions.Apply(_session.CurrentState, PuzzleMove.MoveToStaging(_item.InstanceId, CandidateStagingSlot));
                PreviewLayer = -1;
                _staging.SetHover(CandidateStagingSlot, Preview.IsAccepted);
                RenderGhost();
                return;
            }
            _staging?.SetHover(-1, false);
            foreach (var view in _board.ItemViews.Values)
                if (view.NestInvalidCue)
                    view.SetNestInvalidCue(false, _board.Template);
            CandidateNestParent = NestParentUnder(pointerWorld);
            if (CandidateNestParent != null)
            {
                HasCandidate = false;
                CandidateCompartment = null;
                CandidateAnchor = default;
                Preview = PuzzleTransitions.Apply(_session.CurrentState, PuzzleMove.NestInto(_item.InstanceId, CandidateNestParent));
                _board.ItemViews[CandidateNestParent].SetNestInvalidCue(!Preview.IsAccepted, _board.Template);
                PreviewLayer = -1;
                RenderGhost();
                return;
            }
            HasCandidate = PuzzleBoardProjection.TryProject(_board.CompartmentFrames(), anchorWorld, _view.Footprint,
                out var compartment, out var anchor);
            CandidateCompartment = HasCandidate ? compartment : null;
            CandidateAnchor = HasCandidate ? anchor : default;
            Preview = HasCandidate ? PuzzleTransitions.Apply(_session.CurrentState, CandidateMove()) : null;
            PreviewLayer = -1;
            if (PreviewValid)
            {
                Preview.State.TryGetItem(_item.InstanceId, out var placed);
                PreviewLayer = placed.Location.Placement.Layer;
            }
            else if (Preview?.Report != null)
            {
                foreach (var violation in Preview.Report.Involving(_item.InstanceId))
                    if (violation.HasCell && violation.Cell.Compartment == compartment && !_marked.Contains(violation.Cell.Column))
                        _marked.Add(violation.Cell.Column);
            }
            RenderGhost();
        }

        /// <summary>Next allowed rotation (ADR-0003 order). Candidate-only: no state change, no move.</summary>
        public void RotateCandidate()
        {
            if (!IsDragging)
                return;
            var shown = DisplayItem(_item);
            var allowed = shown.State.AllowedRotations;
            var index = 0;
            for (var i = 0; i < allowed.Count; i++)
                if (allowed[i] == CandidateRotation)
                    index = i;
            CandidateRotation = allowed[(index + 1) % allowed.Count];
            _view.SetVisual(shown, CandidateRotation, _board.ResolveVisual(shown), _board.Template,
                _board.ColorFor(_item.Definition.Id));
            _haptics?.Play(FeelCue.Rotate);
            _audio?.Play(FeelCue.Rotate);
            UpdateDrag(_lastPointer);
        }

        /// <summary>True when the dragged item, or else the selected tray item, has more than one distinct orientation.</summary>
        public bool CanRotateSelection
        {
            get
            {
                var item = IsDragging ? _item : SelectedOutsideItem();
                return item != null && PackSolverV2.UniqueRotations(DisplayItem(item).State).Count > 1;
            }
        }

        /// <summary>
        /// Touch rotate affordance: rotates the drag candidate, or the selected Source Tray item's display orientation
        /// (which the next drag starts with). Presentation-only: no state change, no move.
        /// </summary>
        public bool RotateSelection()
        {
            if (IsDragging)
            {
                RotateCandidate();
                return true;
            }
            var item = SelectedOutsideItem();
            if (item == null)
                return false;
            var shown = DisplayItem(item);
            var unique = PackSolverV2.UniqueRotations(shown.State);
            if (unique.Count < 2)
                return false;
            var current = item.Location.Kind == ItemLocationKind.SourceTray
                ? _tray.DisplayRotation(shown) : _staging.DisplayRotation(shown);
            var index = 0;
            for (var i = 0; i < unique.Count; i++)
                if (unique[i] == current)
                    index = i;
            if (item.Location.Kind == ItemLocationKind.SourceTray)
                _tray.SetDisplayRotation(item.InstanceId, unique[(index + 1) % unique.Count]);
            else
                _staging.SetDisplayRotation(item.InstanceId, unique[(index + 1) % unique.Count]);
            SyncPresenters();
            _haptics?.Play(FeelCue.Rotate);
            _audio?.Play(FeelCue.Rotate);
            return true;
        }

        private PuzzleItem SelectedOutsideItem() =>
            _selectedOutsideId != null && _session.CurrentState.TryGetItem(_selectedOutsideId, out var item)
            && (item.Location.Kind == ItemLocationKind.SourceTray || item.Location.Kind == ItemLocationKind.Staging) ? item : null;

        private PuzzleItem DisplayItem(PuzzleItem item) =>
            item.InstanceId == _selectedOutsideId && _candidateStateId != null && _candidateStateId != item.StateId
                ? item.With(_candidateStateId, item.Location) : item;

        private void SelectOutside(PuzzleItem item)
        {
            if (_selectedOutsideId == item.InstanceId)
                return;
            _selectedOutsideId = item.InstanceId;
            _candidateStateId = item.StateId;
            _candidateModifier = null;
        }

        public bool CanSelectModifier(ItemModifier modifier)
        {
            var item = IsDragging ? _item : SelectedOutsideItem();
            if (item == null || item.Location.Kind != ItemLocationKind.SourceTray && item.Location.Kind != ItemLocationKind.Staging)
                return false;
            return item.Definition.TryGetTransition(_candidateStateId ?? item.StateId, modifier, out _)
                || _candidateStateId != null && _candidateStateId != item.StateId && _candidateModifier == modifier;
        }

        public bool SelectModifier(ItemModifier modifier)
        {
            if (!InteractionEnabled || !CanSelectModifier(modifier))
                return false;
            var item = IsDragging ? _item : SelectedOutsideItem();
            var current = _candidateStateId ?? item.StateId;
            _candidateStateId = item.Definition.TryGetTransition(current, modifier, out var target)
                ? target.Id : item.StateId;
            _candidateModifier = _candidateStateId == item.StateId ? null : modifier;
            var shown = DisplayItem(item);
            var rotation = IsDragging ? CandidateRotation : item.Location.Kind == ItemLocationKind.SourceTray
                ? _tray.DisplayRotation(item) : _staging.DisplayRotation(item);
            if (!shown.State.AllowsRotation(rotation))
                rotation = shown.State.AllowedRotations[0];
            if (IsDragging)
            {
                CandidateRotation = rotation;
                _view.SetVisual(shown, rotation, _board.ResolveVisual(shown), _board.Template, _board.ColorFor(item.Definition.Id));
                UpdateDrag(_lastPointer);
            }
            else
            {
                if (item.Location.Kind == ItemLocationKind.SourceTray)
                    _tray.SetDisplayRotation(item.InstanceId, rotation);
                else
                    _staging.SetDisplayRotation(item.InstanceId, rotation);
                SyncPresenters();
            }
            var feedback = ViewOf(item.InstanceId)?.Feedback;
            if (modifier == ItemModifier.Fold)
                feedback?.PlayFold();
            else
                feedback?.PlayCompress();
            return true;
        }

        /// <summary>
        /// Commits the candidate as one PuzzleSession move and re-syncs presentation. Releasing away from every
        /// compartment is a cancel (null result, no move attempted).
        /// </summary>
        public PuzzleSessionStep Drop()
        {
            if (!IsDragging)
                return null;
            if (!HasCandidate && CandidateStagingSlot < 0 && CandidateNestParent == null)
            {
                Cancel();
                return null;
            }
            var step = _session.Apply(CandidateStagingSlot >= 0
                ? PuzzleMove.MoveToStaging(_item.InstanceId, CandidateStagingSlot)
                : CandidateNestParent != null ? PuzzleMove.NestInto(_item.InstanceId, CandidateNestParent) : CandidateMove());
            LastStep = step;
            var id = _item.InstanceId;
            var nestParent = CandidateNestParent;
            var direction = _lastPointer - _dragStart;
            EndDrag();
            // Feedback follows state: the view that now shows the item (placed or returned) plays it.
            var shown = ViewOf(id);
            if (step.Move.IsAccepted)
            {
                shown?.Feedback?.PlaySettle();
                _haptics?.Play(FeelCue.ItemSettle);
                _audio?.Play(FeelCue.ItemSettle, shown != null ? shown.Feedback.Profile.Family : MaterialFamily.Neutral);
                if (nestParent != null)
                    ViewOf(nestParent)?.Feedback?.PlaySettle();
            }
            else
            {
                shown?.Feedback?.PlayReject(direction);
                _haptics?.Play(FeelCue.Reject);
                _audio?.Play(FeelCue.Reject);
            }
            StepCommitted?.Invoke(step);
            return step;
        }

        public void Cancel()
        {
            if (IsDragging)
                EndDrag();
            else
                CancelCandidate();
        }

        public void CancelCandidate()
        {
            _candidateStateId = null;
            _candidateModifier = null;
            _selectedOutsideId = null;
            SyncPresenters();
        }

        private string NestParentUnder(Vector3 world)
        {
            if (_item == null || _item.Location.Kind == ItemLocationKind.SourceTray)
                return null;
            PuzzleItemView best = null;
            foreach (var view in _board.ItemViews.Values)
                if (view.InstanceId != _item.InstanceId && view.ContainsWorldPointXZ(world)
                    && _session.CurrentState.TryGetItem(view.InstanceId, out var parent)
                    && parent.Definition.Nest.Capacity > 0
                    && (best == null || view.Placement.Layer > best.Placement.Layer))
                    best = view;
            return best?.InstanceId;
        }

        private void UpdateNestTargetCues()
        {
            if (_item == null || _item.Location.Kind == ItemLocationKind.SourceTray)
                return;
            foreach (var view in _board.ItemViews.Values)
                if (view.InstanceId != _item.InstanceId)
                {
                    var move = PuzzleMove.NestInto(_item.InstanceId, view.InstanceId);
                    view.SetNestTargetCue(PuzzleTransitions.Apply(_session.CurrentState, move).IsAccepted, _board.Template);
                }
        }

        private void ClearNestTargetCues()
        {
            foreach (var view in _board.ItemViews.Values)
            {
                view.SetNestTargetCue(false, _board.Template);
                view.SetNestInvalidCue(false, _board.Template);
            }
        }

        /// <summary>The view currently presenting an item: board, staging or tray.</summary>
        private PuzzleItemView ViewOf(string id) =>
            _board.ItemViews.TryGetValue(id, out var view) ? view
            : _staging != null && _staging.ItemViews.TryGetValue(id, out view) ? view
            : _tray.ItemViews.TryGetValue(id, out view) ? view : null;

        // Staging pad under the pointer, measured on the pads' own plane (they rest below the board plane). The item's
        // own slot is not a target (dropping there is a cancel, never a staging -> staging move).
        private int StagingSlotUnder(Vector3 pointerWorld)
        {
            if (_staging == null || _staging.Capacity == 0)
                return -1;
            var onPads = pointerWorld;
            if (_camera != null)
                onPads = GridProjector.ScreenToWorld(_camera, _camera.WorldToScreenPoint(new Vector3(pointerWorld.x, 0f, pointerWorld.z)),
                    _staging.transform.position.y);
            var slot = _staging.SlotAt(onPads);
            return slot >= 0 && _item.Location.Kind == ItemLocationKind.Staging && _item.Location.StagingSlot == slot ? -1 : slot;
        }

        /// <summary>Topmost board item (higher layer first), then tray items, whose shown footprint covers the point.</summary>
        public bool TryPickItem(Vector3 world, out string instanceId) => TryPickItem(world, world, out instanceId);

        /// <summary>
        /// As above, with tray items hit-tested at their own resting plane (<paramref name="trayWorld"/>): loose items may
        /// lie on a surface below the board plane (ZT-040C), and must be picked where they are drawn.
        /// </summary>
        public bool TryPickItem(Vector3 boardWorld, Vector3 trayWorld, out string instanceId)
        {
            foreach (var view in _board.ItemViews.Values)
                if (TryPickNestedChild(view, boardWorld, out instanceId))
                    return true;
            if (_staging != null)
                foreach (var view in _staging.ItemViews.Values)
                    if (TryPickNestedChild(view, trayWorld, out instanceId))
                        return true;
            PuzzleItemView best = null;
            foreach (var view in _board.ItemViews.Values)
                if (view.ContainsWorldPointXZ(boardWorld) && (best == null || view.Placement.Layer > best.Placement.Layer))
                    best = view;
            if (best == null && _staging != null)
                foreach (var view in _staging.ItemViews.Values)
                    if (view.ContainsWorldPointXZ(trayWorld))
                        best = view;
            if (best == null)
                foreach (var view in _tray.ItemViews.Values)
                    if (view.ContainsWorldPointXZ(trayWorld))
                        best = view;
            instanceId = best?.InstanceId;
            return best != null;
        }

        private bool TryPickNestedChild(PuzzleItemView parentView, Vector3 world, out string instanceId)
        {
            if (parentView.ContainsContainedCueWorldXZ(world))
                foreach (var child in _session.CurrentState.GetChildren(parentView.InstanceId))
                    if (AccessQueries.GetAccess(_session.CurrentState, child.InstanceId) == ItemAccess.Accessible
                        || AccessQueries.GetAccess(_session.CurrentState, child.InstanceId) == ItemAccess.External)
                    {
                        instanceId = child.InstanceId;
                        return true;
                    }
            instanceId = null;
            return false;
        }

        public void SyncPresenters()
        {
            _board.Sync(_session.CurrentState);
            _tray.Sync(_session.CurrentState);
            _staging?.Sync(_session.CurrentState);
        }

        private PuzzleMove CandidateMove() =>
            PuzzleMove.PlaceInSuitcase(_item.InstanceId, CandidateCompartment, CandidateAnchor, CandidateRotation,
                _candidateStateId ?? _item.StateId);

        private void EndDrag()
        {
            _view?.Feedback?.CompleteAll();
            _view?.SetRejectTint(false);
            _view?.SetShadowDrop(0f);
            if (_temporaryNestedView && _view != null)
            {
                _view.gameObject.SetActive(false);
                Destroy(_view.gameObject);
            }
            _temporaryNestedView = false;
            CandidateStagingSlot = -1;
            CandidateNestParent = null;
            ClearNestTargetCues();
            _staging?.SetHover(-1, false);
            _view = null;
            _item = null;
            _candidateStateId = null;
            _candidateModifier = null;
            HasCandidate = false;
            Preview = null;
            PreviewLayer = -1;
            _marked.Clear();
            RenderGhost();
            SyncPresenters();
        }

        public void HandlePointer(PointerSignal signal)
        {
            if (_camera == null)
                return;
            var world = GridProjector.ScreenToWorld(_camera, signal.ScreenPosition);
            switch (signal.Phase)
            {
                case PointerPhase.Down:
                    var trayWorld = GridProjector.ScreenToWorld(_camera, signal.ScreenPosition, _tray.transform.position.y);
                    if (!IsDragging && TryPickItem(world, trayWorld, out var id))
                    {
                        // Grab offset from where the item is drawn; from then on the drag follows the board plane.
                        var onTray = _tray.ItemViews.ContainsKey(id) || _staging != null && _staging.ItemViews.ContainsKey(id);
                        if (BeginDrag(id, onTray ? trayWorld : world) == DragBeginResult.Started && onTray)
                            UpdateDrag(world);
                    }
                    break;
                case PointerPhase.Move:
                    UpdateDrag(world);
                    break;
                case PointerPhase.Up:
                    Drop();
                    break;
                case PointerPhase.Cancel:
                    Cancel();
                    break;
            }
        }

        private void Update()
        {
            if (!IsDragging)
                return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                RotateCandidate();
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame || mouse != null && mouse.rightButton.wasPressedThisFrame)
                Cancel();
        }

        private void RenderGhost()
        {
            _board.HideGuides();
            var cells = IsDragging && HasCandidate ? _view.Footprint.OccupiedCells : null;
            GhostCellCount = cells?.Count ?? 0;
            Vector3 origin = default;
            if (GhostCellCount > 0)
                foreach (var frame in _board.CompartmentFrames())
                    if (frame.Id == CandidateCompartment)
                        origin = frame.Origin;
            // Valid: on the resolved layer. Invalid: just under the lifted item, so the red reads over whatever blocks it.
            var elevation = PreviewValid ? PreviewLayer * PuzzleBoardLayout.LayerHeight + _board.FloorLift + GhostLift : InvalidPreviewHeight;
            EnsurePreviewMaterials();
            if (_footprintMaterial != null)
                _footprintMaterial.SetColor(PresentationKit.BaseColorId, PreviewValid ? ValidColor : InvalidColor);
            _footprintShape.Show(_ghostRoot, cells, origin, CandidateAnchor, elevation, _footprintMaterial);
            _view?.SetRejectTint(IsDragging && Preview != null && !Preview.IsAccepted);
            _offendingShape.Show(_ghostRoot, IsDragging && _marked.Count > 0 ? _marked : null, origin, default,
                elevation + 0.01f, _markMaterial);
        }

        private void EnsurePreviewMaterials()
        {
            if (_footprintMaterial != null || _board == null)
                return;
            var template = PresentationKit.TemplateOrFallback(_board.Template);
            if (template == null)
                return;
            _footprintMaterial = PresentationKit.Transparent(template, ValidColor);
            _markMaterial = PresentationKit.Transparent(template, MarkColor);
        }

        private void OnDestroy()
        {
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
            if (_footprintMaterial != null)
                Destroy(_footprintMaterial);
            if (_markMaterial != null)
                Destroy(_markMaterial);
            _footprintShape.Dispose();
            _offendingShape.Dispose();
        }
    }
}
