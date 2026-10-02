using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public readonly struct DragReleaseRequest
    {
        public ItemView Item { get; }
        public Cell CandidateAnchor { get; }
        public Vector3 VisualPosition { get; }
        public Vector3 RestPosition { get; }
        public Vector3 RestScale { get; }

        public DragReleaseRequest(ItemView item, Cell candidateAnchor, Vector3 visualPosition,
            Vector3 restPosition, Vector3 restScale)
        {
            Item = item;
            CandidateAnchor = candidateAnchor;
            VisualPosition = visualPosition;
            RestPosition = restPosition;
            RestScale = restScale;
        }
    }

    // Visual preview only; Application resolves every release request.
    public sealed class DragPreviewPresenter : MonoBehaviour
    {
        private const float LiftHeight = 0.15f;
        private const float GhostHeight = 0.02f;
        private static readonly ProfilerMarker DragMarker = new ProfilerMarker("ZipTrip.DragPreview.Move");
        private static readonly Color ValidColor = new Color(78f / 255f, 159f / 255f, 162f / 255f, 0.32f);
        private static readonly Color InvalidColor = new Color(195f / 255f, 111f / 255f, 88f / 255f, 0.32f);
        private static readonly Color ValidOutlineColor = new Color(233f / 255f, 229f / 255f, 221f / 255f, 0.9f);

        private BoardPresenter _board;
        private Camera _camera;
        private PointerInteractor _pointer;
        private PlacementBoard _placementBoard;
        private PlacementValidationScratch _scratch;
        private Transform _ghostRoot;
        private Transform[] _ghostCells;
        private Transform[] _validOutlines;
        private Renderer[] _ghostRenderers;
        private Transform[] _markerA;
        private Transform[] _markerB;
        private Material _ghostMaterial;
        private Material _markerMaterial;
        private Material _outlineMaterial;
        private MaterialPropertyBlock _validBlock;
        private MaterialPropertyBlock _invalidBlock;
        private ItemView _activeItem;
        private Vector3 _restPosition;
        private Vector3 _restScale;
        private Vector3 _grabOffset;
        private float _outlineHeight;
        private float _outlineZOffset;

        public event Action<DragReleaseRequest> ReleaseRequested;
        public ItemView ActiveItem => _activeItem;
        public bool InteractionEnabled { get; set; } = true;
        public Cell CandidateAnchor { get; private set; }
        public PlacementValidationSummary Validation { get; private set; }
        public int GhostCellCount { get; private set; }
        public int GhostMarkerCount { get; private set; }
        public bool GhostVisible => _ghostRoot != null && _ghostRoot.gameObject.activeSelf;

        public void Initialize(BoardPresenter board, Camera camera, PointerInteractor pointer)
        {
            if (_board != null)
                throw new InvalidOperationException("Drag preview already initialized.");
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _pointer = pointer ?? throw new ArgumentNullException(nameof(pointer));
            RefreshPresentedState();
            _pointer.PointerEvent += HandlePointer;
        }

        public void RefreshPresentedState()
        {
            if (_board == null || _board.PresentedState == null)
                return;
            CancelActiveImmediate();
            _placementBoard = new PlacementBoard(_board.PresentedState.Container,
                _board.PresentedState.Occupancy);
            var capacity = 1;
            for (var i = 0; i < _board.ItemViews.Count; i++)
                capacity = Math.Max(capacity, _board.ItemViews[i].Footprint.CellCount);
            _scratch = new PlacementValidationScratch(capacity);
            DestroyGhost();
            CreateGhost(capacity);
        }

        public void HandlePointer(PointerSignal signal)
        {
            if (_board == null)
                return;
            if (signal.Phase == PointerPhase.Down)
            {
                if (InteractionEnabled && _activeItem == null)
                    Begin(signal.ScreenPosition);
            }
            else if (signal.Phase == PointerPhase.Move)
            {
                if (_activeItem != null)
                    Move(GridProjector.ScreenToWorld(_camera, signal.ScreenPosition));
            }
            else if (signal.Phase == PointerPhase.Up || signal.Phase == PointerPhase.Cancel)
                End();
        }

        public bool IsMarked(Cell cell)
        {
            for (var i = 0; i < GhostMarkerCount; i++)
                if (_scratch.OffendingCells[i] == cell && _markerA[i].gameObject.activeSelf &&
                    _markerB[i].gameObject.activeSelf)
                    return true;
            return false;
        }

        private void Begin(Vector2 screenPosition)
        {
            var world = GridProjector.ScreenToWorld(_camera, screenPosition);
            // Later children are visually frontmost in the current deterministic presentation order.
            for (var i = _board.ItemViews.Count - 1; i >= 0; i--)
            {
                var view = _board.ItemViews[i];
                if (!view.ContainsWorldPoint(world))
                    continue;
                _activeItem = view;
                _restPosition = view.transform.position;
                _restScale = view.transform.localScale;
                _grabOffset = _restPosition - world;
                view.transform.localScale = Vector3.one;
                var renderers = view.VisualRoot.GetComponentsInChildren<Renderer>();
                var visualTop = renderers[0].bounds.max.y;
                for (var rendererIndex = 1; rendererIndex < renderers.Length; rendererIndex++)
                    visualTop = Mathf.Max(visualTop, renderers[rendererIndex].bounds.max.y);
                _outlineHeight = visualTop + LiftHeight + 0.03f;
                // Keep the raised outline screen-aligned with the ghost under the fixed camera.
                _outlineZOffset = -(_outlineHeight - GhostHeight) *
                    _camera.transform.up.y / _camera.transform.up.z;
                Move(world);
                return;
            }
        }

        private void Move(Vector3 pointerWorld)
        {
            using (DragMarker.Auto())
            {
                var desired = pointerWorld + _grabOffset;
                _activeItem.transform.position = desired + Vector3.up * LiftHeight;
                CandidateAnchor = GridProjector.WorldToAnchor(desired);
                Validation = PlacementValidator.ValidateNonAlloc(_placementBoard, _activeItem.Item,
                    CandidateAnchor, _activeItem.Rotation, _activeItem.ShapeState, _scratch);
                RenderGhost();
            }
        }

        private void End()
        {
            if (_activeItem == null)
                return;
            var request = new DragReleaseRequest(_activeItem, CandidateAnchor,
                _activeItem.transform.position, _restPosition, _restScale);
            var item = _activeItem;
            _activeItem = null;
            HideGhost();
            if (ReleaseRequested != null)
                ReleaseRequested(request);
            else
            {
                item.transform.position = _restPosition;
                item.transform.localScale = _restScale;
            }
        }

        public void CancelActiveImmediate()
        {
            if (_activeItem != null)
            {
                _activeItem.transform.position = _restPosition;
                _activeItem.transform.localScale = _restScale;
                _activeItem = null;
            }
            HideGhost();
        }

        private void HideGhost()
        {
            GhostCellCount = 0;
            GhostMarkerCount = 0;
            if (_ghostRoot != null)
                _ghostRoot.gameObject.SetActive(false);
        }

        private void RenderGhost()
        {
            var count = _activeItem.Footprint.CellCount;
            GhostCellCount = count;
            GhostMarkerCount = Validation.OffendingCellCount;
            var colorBlock = Validation.IsValid ? _validBlock : _invalidBlock;
            for (var i = 0; i < _ghostCells.Length; i++)
            {
                var visible = i < count;
                _ghostCells[i].gameObject.SetActive(visible);
                _validOutlines[i].gameObject.SetActive(visible && Validation.IsValid);
                if (visible)
                {
                    var cell = _scratch.RotatedCells[i];
                    var center = new Vector3(CandidateAnchor.X + cell.X + 0.5f,
                        GhostHeight, -CandidateAnchor.Y - cell.Y - 0.5f);
                    _ghostCells[i].position = center;
                    _validOutlines[i].position = new Vector3(center.x, _outlineHeight,
                        center.z + _outlineZOffset);
                    _ghostRenderers[i].SetPropertyBlock(colorBlock);
                }

                var marked = i < GhostMarkerCount;
                _markerA[i].gameObject.SetActive(marked);
                _markerB[i].gameObject.SetActive(marked);
                if (marked)
                {
                    var cell = _scratch.OffendingCells[i];
                    var position = new Vector3(cell.X + 0.5f, GhostHeight + 0.03f,
                        -cell.Y - 0.5f);
                    _markerA[i].position = position;
                    _markerB[i].position = position;
                }
            }
            _ghostRoot.gameObject.SetActive(true);
        }

        private void CreateGhost(int capacity)
        {
            _ghostRoot = new GameObject("ZT-012 Ghost Preview").transform;
            _ghostRoot.SetParent(transform, false);
            _ghostCells = new Transform[capacity];
            _validOutlines = new Transform[capacity];
            _ghostRenderers = new Renderer[capacity];
            _markerA = new Transform[capacity];
            _markerB = new Transform[capacity];
            _ghostMaterial = TransparentMaterial(_board.RuntimeMaterialTemplate);
            _markerMaterial = TransparentMaterial(_board.RuntimeMaterialTemplate);
            _outlineMaterial = TransparentMaterial(_board.RuntimeMaterialTemplate);
            _outlineMaterial.SetColor("_BaseColor", ValidOutlineColor);
            _outlineMaterial.SetColor("_Color", ValidOutlineColor);
            _markerMaterial.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.85f));
            _markerMaterial.SetColor("_Color", new Color(1f, 1f, 1f, 0.85f));
            _validBlock = ColorBlock(ValidColor);
            _invalidBlock = ColorBlock(InvalidColor);
            for (var i = 0; i < capacity; i++)
            {
                var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = "Ghost cell";
                cell.transform.SetParent(_ghostRoot, false);
                cell.transform.localScale = new Vector3(0.96f, 0.01f, 0.96f);
                _ghostCells[i] = cell.transform;
                _ghostRenderers[i] = cell.GetComponent<Renderer>();
                _ghostRenderers[i].sharedMaterial = _ghostMaterial;
                Destroy(cell.GetComponent<Collider>());
                _validOutlines[i] = CreateValidOutline();
                _markerA[i] = CreateMarker("Invalid X A", -45f);
                _markerB[i] = CreateMarker("Invalid X B", 45f);
            }
            _ghostRoot.gameObject.SetActive(false);
        }

        private Transform CreateValidOutline()
        {
            var outline = new GameObject("Valid outline").transform;
            outline.SetParent(_ghostRoot, false);
            CreateOutlineBar(outline, new Vector3(-0.47f, 0f, 0f), new Vector3(0.06f, 0.02f, 1f));
            CreateOutlineBar(outline, new Vector3(0.47f, 0f, 0f), new Vector3(0.06f, 0.02f, 1f));
            CreateOutlineBar(outline, new Vector3(0f, 0f, -0.47f), new Vector3(1f, 0.02f, 0.06f));
            CreateOutlineBar(outline, new Vector3(0f, 0f, 0.47f), new Vector3(1f, 0.02f, 0.06f));
            return outline;
        }

        private void CreateOutlineBar(Transform parent, Vector3 position, Vector3 scale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Cream outline bar";
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = position;
            bar.transform.localScale = scale;
            bar.GetComponent<Renderer>().sharedMaterial = _outlineMaterial;
            Destroy(bar.GetComponent<Collider>());
        }

        private Transform CreateMarker(string name, float angle)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(_ghostRoot, false);
            bar.transform.localScale = new Vector3(0.08f, 0.02f, 1.18f);
            bar.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            bar.GetComponent<Renderer>().sharedMaterial = _markerMaterial;
            Destroy(bar.GetComponent<Collider>());
            return bar.transform;
        }

        private static Material TransparentMaterial(Material template)
        {
            var shader = template != null ? template.shader :
                Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                throw new InvalidOperationException("Ghost preview shader is unavailable.");
            var material = new Material(shader);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        private static MaterialPropertyBlock ColorBlock(Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            return block;
        }

        private void OnDestroy()
        {
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
            DestroyGhost();
        }

        private void DestroyGhost()
        {
            if (_ghostRoot != null)
                Destroy(_ghostRoot.gameObject);
            if (_ghostMaterial != null)
                Destroy(_ghostMaterial);
            if (_markerMaterial != null)
                Destroy(_markerMaterial);
            if (_outlineMaterial != null)
                Destroy(_outlineMaterial);
            _ghostRoot = null;
            _ghostMaterial = null;
            _markerMaterial = null;
            _outlineMaterial = null;
        }
    }
}
