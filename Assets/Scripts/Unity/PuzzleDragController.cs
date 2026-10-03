using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZipTrip.Application;
using ZipTrip.Domain;
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
        NotAccessible = 4
    }

    // ZT-039 drag / place for the ADR-0006 runtime. Pointer -> candidate (compartment, anchor, rotation) ->
    // PuzzleTransitions.Apply on the current state for the preview (pure, no session history) -> on drop exactly one
    // PuzzleSession.Apply with the same move -> explicit presenter Sync. Legality, layer and access always come from the
    // Domain; drag state (id, rotation, anchor, offset) is presentation-only and never canonical.
    public sealed class PuzzleDragController : MonoBehaviour
    {
        public const float LiftHeight = 0.5f;
        private const float GhostLift = 0.03f;
        private static readonly Color ValidColor = new Color(0.30f, 0.72f, 0.46f, 1f);
        private static readonly Color InvalidColor = new Color(0.86f, 0.27f, 0.22f, 1f);
        private static readonly Color MarkColor = new Color(0.55f, 0.08f, 0.06f, 1f);

        private readonly List<Transform> _ghostCells = new List<Transform>();
        private readonly List<Transform> _markers = new List<Transform>();
        private readonly List<Cell> _marked = new List<Cell>();
        private PuzzleSession _session;
        private PuzzleBoardPresenter _board;
        private PuzzleTrayPresenter _tray;
        private Camera _camera;
        private PointerInteractor _pointer;
        private PuzzleItemView _view;
        private PuzzleItem _item;
        private Vector3 _grabOffset;
        private Vector3 _lastPointer;
        private Transform _ghostRoot;

        public PuzzleSession Session => _session;
        public bool IsDragging => _view != null;
        public string DraggedInstanceId => _item?.InstanceId;
        public Rotation CandidateRotation { get; private set; }
        /// <summary>True when the dragged footprint is over a compartment (a drop then attempts a move).</summary>
        public bool HasCandidate { get; private set; }
        public string CandidateCompartment { get; private set; }
        public Cell CandidateAnchor { get; private set; }
        /// <summary>Domain outcome of the candidate move on the current state; null without a candidate.</summary>
        public MoveResult Preview { get; private set; }
        public bool PreviewValid => Preview != null && Preview.IsAccepted;
        /// <summary>Domain-resolved layer of a valid preview; -1 otherwise.</summary>
        public int PreviewLayer { get; private set; } = -1;
        public IReadOnlyList<Cell> MarkedCells => _marked;
        public int GhostCellCount { get; private set; }
        public PuzzleSessionStep LastStep { get; private set; }

        public void Initialize(PuzzleSession session, PuzzleBoardPresenter board, PuzzleTrayPresenter tray, Camera camera,
            PointerInteractor pointer = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _tray = tray ?? throw new ArgumentNullException(nameof(tray));
            _camera = camera;
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
            _pointer = pointer;
            if (_pointer != null)
                _pointer.PointerEvent += HandlePointer;
            _ghostRoot = new GameObject("Placement Preview").transform;
            _ghostRoot.SetParent(transform, false);
            SyncPresenters();
        }

        public DragBeginResult BeginDrag(string instanceId, Vector3 pointerWorld)
        {
            if (IsDragging)
                return DragBeginResult.AlreadyDragging;
            var state = _session.CurrentState;
            if (!state.TryGetItem(instanceId, out var item))
                return DragBeginResult.UnknownItem;

            PuzzleItemView view;
            switch (item.Location.Kind)
            {
                case ItemLocationKind.SourceTray:
                    if (!_tray.ItemViews.TryGetValue(instanceId, out view))
                        return DragBeginResult.UnknownItem;
                    break;
                case ItemLocationKind.Suitcase:
                    if (!AccessQueries.CanTake(state, instanceId))
                        return DragBeginResult.NotAccessible;
                    if (!_board.ItemViews.TryGetValue(instanceId, out view))
                        return DragBeginResult.UnknownItem;
                    break;
                default:
                    return DragBeginResult.NotDraggable;
            }

            _item = item;
            _view = view;
            // The lifted item is ghosted so the Domain preview cells underneath stay readable; Sync restores it.
            _view.SetGhost(true, _board.GhostMaterial);
            CandidateRotation = view.Rotation;
            var origin = view.transform.position;
            _grabOffset = new Vector3(origin.x - pointerWorld.x, 0f, origin.z - pointerWorld.z);
            UpdateDrag(pointerWorld);
            return DragBeginResult.Started;
        }

        public void UpdateDrag(Vector3 pointerWorld)
        {
            if (!IsDragging)
                return;
            _lastPointer = pointerWorld;
            var anchorWorld = new Vector3(pointerWorld.x, 0f, pointerWorld.z) + _grabOffset;
            _view.transform.position = anchorWorld + Vector3.up * LiftHeight;

            _marked.Clear();
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
            var allowed = _item.State.AllowedRotations;
            var index = 0;
            for (var i = 0; i < allowed.Count; i++)
                if (allowed[i] == CandidateRotation)
                    index = i;
            CandidateRotation = allowed[(index + 1) % allowed.Count];
            _view.SetVisual(_item, CandidateRotation, _board.ResolveVisual(_item), _board.Template,
                _board.ColorFor(_item.Definition.Id));
            _view.SetGhost(true, _board.GhostMaterial);
            UpdateDrag(_lastPointer);
        }

        /// <summary>
        /// Commits the candidate as one PuzzleSession move and re-syncs presentation. Releasing away from every
        /// compartment is a cancel (null result, no move attempted).
        /// </summary>
        public PuzzleSessionStep Drop()
        {
            if (!IsDragging)
                return null;
            if (!HasCandidate)
            {
                Cancel();
                return null;
            }
            var step = _session.Apply(CandidateMove());
            LastStep = step;
            EndDrag();
            return step;
        }

        public void Cancel()
        {
            if (IsDragging)
                EndDrag();
        }

        /// <summary>Topmost board item (higher layer first), then tray items, whose shown footprint covers the point.</summary>
        public bool TryPickItem(Vector3 world, out string instanceId)
        {
            PuzzleItemView best = null;
            foreach (var view in _board.ItemViews.Values)
                if (view.ContainsWorldPointXZ(world) && (best == null || view.Placement.Layer > best.Placement.Layer))
                    best = view;
            if (best == null)
                foreach (var view in _tray.ItemViews.Values)
                    if (view.ContainsWorldPointXZ(world))
                        best = view;
            instanceId = best?.InstanceId;
            return best != null;
        }

        public void SyncPresenters()
        {
            _board.Sync(_session.CurrentState);
            _tray.Sync(_session.CurrentState);
        }

        private PuzzleMove CandidateMove() =>
            PuzzleMove.PlaceInSuitcase(_item.InstanceId, CandidateCompartment, CandidateAnchor, CandidateRotation, _item.StateId);

        private void EndDrag()
        {
            _view = null;
            _item = null;
            HasCandidate = false;
            Preview = null;
            PreviewLayer = -1;
            _marked.Clear();
            RenderGhost();
            SyncPresenters();
        }

        private void HandlePointer(PointerSignal signal)
        {
            if (_camera == null)
                return;
            var world = GridProjector.ScreenToWorld(_camera, signal.ScreenPosition);
            switch (signal.Phase)
            {
                case PointerPhase.Down:
                    if (!IsDragging && TryPickItem(world, out var id))
                        BeginDrag(id, world);
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
            GhostCellCount = 0;
            var cells = IsDragging && HasCandidate ? _view.Footprint.OccupiedCells : null;
            var count = cells?.Count ?? 0;
            Vector3 origin = default;
            if (count > 0)
                foreach (var frame in _board.CompartmentFrames())
                    if (frame.Id == CandidateCompartment)
                        origin = frame.Origin;
            var elevation = (PreviewValid ? PreviewLayer : 0) * PuzzleBoardLayout.LayerHeight + GhostLift;

            for (var i = 0; i < Math.Max(count, _ghostCells.Count); i++)
            {
                if (i >= _ghostCells.Count)
                    _ghostCells.Add(CreateQuad("Ghost Cell", 0.9f));
                var visible = i < count;
                _ghostCells[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var cell = new Cell(CandidateAnchor.X + cells[i].X, CandidateAnchor.Y + cells[i].Y);
                _ghostCells[i].position = origin + PuzzleBoardLayout.ColumnCenter(cell) + Vector3.up * elevation;
                PuzzleItemView.Paint(_ghostCells[i].GetComponent<Renderer>(), _board.Template, PreviewValid ? ValidColor : InvalidColor);
                GhostCellCount++;
            }

            for (var i = 0; i < Math.Max(_marked.Count, _markers.Count); i++)
            {
                if (i >= _markers.Count)
                    _markers.Add(CreateQuad("Offending Cell", 0.45f));
                var visible = IsDragging && i < _marked.Count;
                _markers[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                _markers[i].position = origin + PuzzleBoardLayout.ColumnCenter(_marked[i]) + Vector3.up * (elevation + 0.02f);
                PuzzleItemView.Paint(_markers[i].GetComponent<Renderer>(), _board.Template, MarkColor);
            }
        }

        private Transform CreateQuad(string name, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(_ghostRoot, false);
            quad.transform.localScale = new Vector3(size, 0.02f, size);
            return quad.transform;
        }

        private void OnDestroy()
        {
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
        }
    }
}
