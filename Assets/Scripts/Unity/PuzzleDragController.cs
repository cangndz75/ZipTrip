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
        NotAccessible = 4,
        /// <summary>Interaction is locked (e.g. the level is complete).</summary>
        InteractionLocked = 5
    }

    // ZT-039 drag / place for the ADR-0006 runtime. Pointer -> candidate (compartment, anchor, rotation) ->
    // PuzzleTransitions.Apply on the current state for the preview (pure, no session history) -> on drop exactly one
    // PuzzleSession.Apply with the same move -> explicit presenter Sync. Legality, layer and access always come from the
    // Domain; drag state (id, rotation, anchor, offset) is presentation-only and never canonical.
    // ZT-040B visual language: the grid stays invisible until a candidate exists; then only the snapped footprint
    // (soft green / red) and the guides of the cells around it are shown, and everything disappears on drop / cancel.
    public sealed class PuzzleDragController : MonoBehaviour
    {
        public const float LiftHeight = 0.5f;
        /// <summary>Chebyshev radius (in cells) of the local placement guides around the candidate footprint.</summary>
        public const int GuideRadius = 1;
        private const float GhostLift = 0.03f;
        private static readonly Color ValidColor = new Color(0.36f, 0.80f, 0.50f, 0.62f);
        private static readonly Color InvalidColor = new Color(0.92f, 0.30f, 0.24f, 0.62f);
        private static readonly Color MarkColor = new Color(0.62f, 0.07f, 0.05f, 0.95f);

        private readonly List<Transform> _ghostCells = new List<Transform>();
        private readonly List<Transform> _markers = new List<Transform>();
        private readonly List<Cell> _marked = new List<Cell>();
        private readonly HashSet<Cell> _guideCells = new HashSet<Cell>();
        private Material _validMaterial;
        private Material _invalidMaterial;
        private Material _markMaterial;
        private Texture2D _softTexture;
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
        /// <summary>Raised after every drop that reached PuzzleSession.Apply (accepted or rejected), after presenters re-sync.</summary>
        public event Action<PuzzleSessionStep> StepCommitted;
        /// <summary>False locks new drags (e.g. after completion). Presentation-only.</summary>
        public bool InteractionEnabled { get; set; } = true;

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
            if (_ghostRoot == null)
            {
                _ghostRoot = new GameObject("Placement Preview").transform;
                _ghostRoot.SetParent(transform, false);
            }
            EnsurePreviewMaterials();
            _view = null;
            _item = null;
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
            // Tray items are drawn smaller; keep the grabbed point under the pointer when the item grows to board size.
            var origin = view.transform.position;
            var scale = Mathf.Approximately(view.transform.lossyScale.x, 0f) ? 1f : view.transform.lossyScale.x;
            _grabOffset = new Vector3(origin.x - pointerWorld.x, 0f, origin.z - pointerWorld.z) / scale;
            view.transform.localScale = Vector3.one;
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

        /// <summary>True when the dragged item, or else the selected tray item, has more than one distinct orientation.</summary>
        public bool CanRotateSelection
        {
            get
            {
                var item = IsDragging ? _item : SelectedTrayItem();
                return item != null && PackSolverV2.UniqueRotations(item.State).Count > 1;
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
            var item = SelectedTrayItem();
            if (item == null)
                return false;
            var unique = PackSolverV2.UniqueRotations(item.State);
            if (unique.Count < 2)
                return false;
            var current = _tray.DisplayRotation(item);
            var index = 0;
            for (var i = 0; i < unique.Count; i++)
                if (unique[i] == current)
                    index = i;
            _tray.SetDisplayRotation(item.InstanceId, unique[(index + 1) % unique.Count]);
            _tray.Sync(_session.CurrentState);
            return true;
        }

        private PuzzleItem SelectedTrayItem() =>
            _tray.SelectedInstanceId != null && _session.CurrentState.TryGetItem(_tray.SelectedInstanceId, out var item)
            && item.Location.Kind == ItemLocationKind.SourceTray ? item : null;

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
            StepCommitted?.Invoke(step);
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

        public void HandlePointer(PointerSignal signal)
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
                    _ghostCells.Add(CreateQuad("Ghost Cell", 1.02f));
                var visible = i < count;
                _ghostCells[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var cell = new Cell(CandidateAnchor.X + cells[i].X, CandidateAnchor.Y + cells[i].Y);
                _ghostCells[i].position = origin + PuzzleBoardLayout.ColumnCenter(cell) + Vector3.up * elevation;
                PaintPreview(_ghostCells[i], PreviewValid ? _validMaterial : _invalidMaterial, PreviewValid ? ValidColor : InvalidColor);
                GhostCellCount++;
            }
            RenderGuides(cells);

            for (var i = 0; i < Math.Max(_marked.Count, _markers.Count); i++)
            {
                if (i >= _markers.Count)
                    _markers.Add(CreateQuad("Offending Cell", 0.5f));
                var visible = IsDragging && i < _marked.Count;
                _markers[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                _markers[i].position = origin + PuzzleBoardLayout.ColumnCenter(_marked[i]) + Vector3.up * (elevation + 0.02f);
                PaintPreview(_markers[i], _markMaterial, MarkColor);
            }
        }

        // Local placement guides: valid cells of the candidate compartment within GuideRadius of the footprint (the
        // footprint itself is drawn by the preview). No candidate -> no guides anywhere, so the idle board shows none.
        private void RenderGuides(IReadOnlyList<Cell> footprint)
        {
            if (footprint == null || footprint.Count == 0)
            {
                _board.HideGuides();
                return;
            }
            _guideCells.Clear();
            var covered = new HashSet<Cell>();
            foreach (var cell in footprint)
                covered.Add(new Cell(CandidateAnchor.X + cell.X, CandidateAnchor.Y + cell.Y));
            foreach (var cell in covered)
                for (var dy = -GuideRadius; dy <= GuideRadius; dy++)
                    for (var dx = -GuideRadius; dx <= GuideRadius; dx++)
                    {
                        var near = new Cell(cell.X + dx, cell.Y + dy);
                        if (!covered.Contains(near))
                            _guideCells.Add(near);
                    }
            _board.ShowGuides(CandidateCompartment, _guideCells);
        }

        private void EnsurePreviewMaterials()
        {
            if (_validMaterial != null)
                return;
            var template = PresentationKit.TemplateOrFallback(_board.Template);
            if (template == null)
                return;
            _softTexture = PresentationKit.SoftRect(64, 0.14f);
            _validMaterial = PresentationKit.Transparent(template, ValidColor, _softTexture);
            _invalidMaterial = PresentationKit.Transparent(template, InvalidColor, _softTexture);
            _markMaterial = PresentationKit.Transparent(template, MarkColor, _softTexture);
        }

        private static void PaintPreview(Transform quad, Material material, Color color)
        {
            var renderer = quad.GetComponent<Renderer>();
            if (material != null)
                renderer.sharedMaterial = material;
            else
                PuzzleItemView.Paint(renderer, null, color);
        }

        // Flat, upward-facing soft quad (no collider) for the snapped footprint and offending-cell marks.
        private Transform CreateQuad(string name, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(_ghostRoot, false);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(size, size, 1f);
            quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return quad.transform;
        }

        private void OnDestroy()
        {
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
            if (_validMaterial != null)
                Destroy(_validMaterial);
            if (_invalidMaterial != null)
                Destroy(_invalidMaterial);
            if (_markMaterial != null)
                Destroy(_markMaterial);
            if (_softTexture != null)
                Destroy(_softTexture);
        }
    }
}
