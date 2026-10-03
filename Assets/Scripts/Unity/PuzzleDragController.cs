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
    // ZT-040B visual language: the grid stays invisible until a candidate exists, and everything disappears on drop /
    // cancel. ZT-040C: the candidate is shown as one soft footprint-shaped glow (green fits / red does not) with the
    // offending region emphasised; no cell tiles or neighbour guides ("this item fits here", not "these cells").
    public sealed class PuzzleDragController : MonoBehaviour
    {
        public const float LiftHeight = 0.5f;
        private const float GhostLift = 0.03f;
        private const float InvalidPreviewHeight = LiftHeight - 0.08f;
        private const int PreviewPixelsPerCell = 16;
        private const float PreviewPad = 0.16f;
        private const float PreviewBlur = 0.1f;
        private static readonly Color ValidColor = new Color(0.36f, 0.80f, 0.50f, 0.62f);
        private static readonly Color InvalidColor = new Color(0.92f, 0.30f, 0.24f, 0.62f);
        private static readonly Color MarkColor = new Color(0.62f, 0.07f, 0.05f, 0.95f);

        private readonly List<Cell> _marked = new List<Cell>();
        private readonly PreviewShape _footprintShape = new PreviewShape("Candidate Footprint");
        private readonly PreviewShape _offendingShape = new PreviewShape("Offending Region");
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
        private Vector3 _dragStart;
        private Transform _ghostRoot;
        private PuzzleStagingPresenter _staging;

        public PuzzleSession Session => _session;
        public bool IsDragging => _view != null;
        public string DraggedInstanceId => _item?.InstanceId;
        public Rotation CandidateRotation { get; private set; }
        /// <summary>True when the dragged footprint is over a compartment (a drop then attempts a move).</summary>
        public bool HasCandidate { get; private set; }
        /// <summary>ZT-041: staging slot under the pointer that a drop would target (-1 = none). Excludes the item's own slot.</summary>
        public int CandidateStagingSlot { get; private set; } = -1;
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
                case ItemLocationKind.Staging:
                    // ZT-041: a staged item is external (always reachable) and may go back into the suitcase.
                    if (_staging == null || !_staging.ItemViews.TryGetValue(instanceId, out view))
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
            _dragStart = pointerWorld;
            view.Feedback?.PlayLift();
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
            if (!HasCandidate && CandidateStagingSlot < 0)
            {
                Cancel();
                return null;
            }
            var step = _session.Apply(CandidateStagingSlot >= 0
                ? PuzzleMove.MoveToStaging(_item.InstanceId, CandidateStagingSlot) : CandidateMove());
            LastStep = step;
            var id = _item.InstanceId;
            var direction = _lastPointer - _dragStart;
            EndDrag();
            // Feedback follows state: the view that now shows the item (placed or returned) plays it.
            var shown = ViewOf(id);
            if (step.Move.IsAccepted)
                shown?.Feedback?.PlaySettle();
            else
                shown?.Feedback?.PlayReject(direction);
            StepCommitted?.Invoke(step);
            return step;
        }

        public void Cancel()
        {
            if (IsDragging)
                EndDrag();
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

        public void SyncPresenters()
        {
            _board.Sync(_session.CurrentState);
            _tray.Sync(_session.CurrentState);
            _staging?.Sync(_session.CurrentState);
        }

        private PuzzleMove CandidateMove() =>
            PuzzleMove.PlaceInSuitcase(_item.InstanceId, CandidateCompartment, CandidateAnchor, CandidateRotation, _item.StateId);

        private void EndDrag()
        {
            _view?.Feedback?.CompleteAll();
            CandidateStagingSlot = -1;
            _staging?.SetHover(-1, false);
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
            var elevation = PreviewValid ? PreviewLayer * PuzzleBoardLayout.LayerHeight + GhostLift : InvalidPreviewHeight;
            EnsurePreviewMaterials();
            if (_footprintMaterial != null)
                _footprintMaterial.SetColor(PresentationKit.BaseColorId, PreviewValid ? ValidColor : InvalidColor);
            _footprintShape.Show(_ghostRoot, cells, origin, CandidateAnchor, elevation, _footprintMaterial);
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

        // One flat, upward-facing soft quad shaped like a set of cells (via a cached alpha mask); no collider.
        private sealed class PreviewShape
        {
            private readonly string _name;
            private GameObject _object;
            private Mesh _mesh;
            private Texture2D _mask;
            private string _key;

            public PreviewShape(string name) => _name = name;

            public Renderer Renderer => _object != null ? _object.GetComponent<Renderer>() : null;

            /// <param name="cells">Cells relative to <paramref name="anchor"/> (null or empty hides the shape).</param>
            public void Show(Transform parent, IReadOnlyList<Cell> cells, Vector3 origin, Cell anchor, float elevation, Material material)
            {
                if (cells == null || cells.Count == 0 || material == null)
                {
                    if (_object != null)
                        _object.SetActive(false);
                    return;
                }
                int minX = int.MaxValue, minY = int.MaxValue;
                foreach (var cell in cells)
                {
                    minX = Math.Min(minX, cell.X);
                    minY = Math.Min(minY, cell.Y);
                }
                var local = new List<Cell>(cells.Count);
                var key = new System.Text.StringBuilder();
                foreach (var cell in cells)
                {
                    local.Add(new Cell(cell.X - minX, cell.Y - minY));
                    key.Append(cell.X - minX).Append(',').Append(cell.Y - minY).Append(';');
                }
                if (_object == null)
                {
                    _mesh = new Mesh { name = _name };
                    _object = PresentationKit.MeshObject(_name, parent, _mesh, material);
                }
                if (key.ToString() != _key)
                {
                    _key = key.ToString();
                    if (_mask != null)
                        UnityEngine.Object.Destroy(_mask);
                    _mask = PresentationKit.CellMask(local, PreviewPixelsPerCell, PreviewPad, PreviewBlur);
                    int width = 0, depth = 0;
                    foreach (var cell in local)
                    {
                        width = Math.Max(width, cell.X + 1);
                        depth = Math.Max(depth, cell.Y + 1);
                    }
                    var quad = PresentationKit.Quad(new Rect(-PreviewPad, -(depth + PreviewPad), width + 2f * PreviewPad,
                        depth + 2f * PreviewPad), 0f);
                    _mesh.Clear();
                    _mesh.SetVertices(quad.vertices);
                    _mesh.SetUVs(0, quad.uv);
                    _mesh.SetTriangles(quad.triangles, 0);
                    _mesh.RecalculateNormals();
                    _mesh.RecalculateBounds();
                    UnityEngine.Object.Destroy(quad);
                }
                var renderer = _object.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                // The mask is per shape; the material is shared by nothing else while a drag is shown.
                material.SetTexture(PresentationKit.BaseMapId, _mask);
                _object.transform.position = origin + new Vector3(anchor.X + minX, elevation, -(anchor.Y + minY));
                _object.SetActive(true);
            }

            public void Dispose()
            {
                if (_mesh != null)
                    UnityEngine.Object.Destroy(_mesh);
                if (_mask != null)
                    UnityEngine.Object.Destroy(_mask);
            }
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
