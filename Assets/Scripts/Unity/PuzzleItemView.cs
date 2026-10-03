using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // One PuzzleItem visual (one view per instance; thickness is a dimension, not extra views). On the board the root
    // is the placement inside its compartment; in the Source Tray or while dragged it is positioned by presentation
    // only. VisualRoot holds either the resolved prefab or footprint blocks for the shown rotation. A soft contact
    // shadow (ZT-040B) sits beside VisualRoot, never inside it, and is hidden while the view is ghosted.
    public sealed class PuzzleItemView : MonoBehaviour
    {
        public const float ShadowPad = 0.22f;
        private const float ShadowAlpha = 0.6f; // ZT-040D: still reads on the dark lining and felt
        private static readonly Vector3 ShadowOffset = new Vector3(0.06f, 0f, -0.08f);

        private readonly List<(Renderer Renderer, Material[] Materials, MaterialPropertyBlock Block)> _original =
            new List<(Renderer, Material[], MaterialPropertyBlock)>();
        private string _visualKey;
        private GameObject _shadow;
        private Mesh _shadowMesh;
        private Material _shadowMaterial;
        private Texture2D _shadowTexture;
        private float _shadowDrop;

        public string InstanceId { get; private set; }
        public string DefinitionId { get; private set; }
        public string StateId { get; private set; }
        /// <summary>Board placement; only meaningful when <see cref="IsOnBoard"/>.</summary>
        public Placement Placement { get; private set; }
        public bool IsOnBoard { get; private set; }
        /// <summary>Rotation currently shown (placement rotation on the board, candidate rotation while dragged).</summary>
        public Rotation Rotation { get; private set; }
        public int Thickness { get; private set; }
        public ItemShape Footprint { get; private set; }
        public Transform VisualRoot { get; private set; }
        /// <summary>Presentation motion (ZT-040D); animates only its own root, never this transform.</summary>
        public ItemFeedback Feedback { get; private set; }
        public bool UsesPrefab { get; private set; }
        public bool IsGhosted { get; private set; }
        public bool ShadowVisible => _shadow != null && _shadow.activeSelf;

        internal void Bind(PuzzleItem item, Transform compartmentItems, GameObject prefab, Material template, Color color)
        {
            var placement = item.Location.Placement;
            Placement = placement;
            IsOnBoard = true;
            _shadowDrop = 0f;
            transform.SetParent(compartmentItems, false);
            transform.localPosition = PuzzleBoardLayout.ItemLocalPosition(placement);
            SetVisual(item, placement.Rotation, prefab, template, color);
        }

        // Off-board visual (Source Tray): position is presentation-only and never canonical.
        internal void BindLoose(PuzzleItem item, Transform parent, Vector3 localPosition, Rotation rotation, GameObject prefab,
            Material template, Color color)
        {
            IsOnBoard = false;
            transform.SetParent(parent, false);
            transform.localPosition = localPosition;
            SetVisual(item, rotation, prefab, template, color);
        }

        // XZ hit test against the shown footprint (exact cells, no padding).
        public bool ContainsWorldPointXZ(Vector3 world)
        {
            if (Footprint == null)
                return false;
            var scale = Mathf.Approximately(transform.lossyScale.x, 0f) ? 1f : transform.lossyScale.x;
            var local = (world - transform.position) / scale;
            foreach (var cell in Footprint.OccupiedCells)
                if (local.x >= cell.X && local.x < cell.X + 1f && -local.z >= cell.Y && -local.z < cell.Y + 1f)
                    return true;
            return false;
        }

        internal void SetVisual(PuzzleItem item, Rotation rotation, GameObject prefab, Material template, Color color)
        {
            item.State.TryGetFootprint(rotation, out var footprint);
            InstanceId = item.InstanceId;
            DefinitionId = item.Definition.Id;
            StateId = item.StateId;
            Rotation = rotation;
            Thickness = item.State.Thickness;
            Footprint = footprint;

            var key = $"{DefinitionId}|{StateId}|{(int)rotation}|{(prefab != null ? prefab.GetInstanceID() : 0)}";
            if (key == _visualKey)
                return;
            _visualKey = key;
            SetGhost(false, null);
            if (VisualRoot != null)
                Destroy(VisualRoot.gameObject);
            // ZT-040D: item root (placement) > Feedback Root (motion only, pivot at the footprint centre) > Visual Root.
            if (Feedback == null)
            {
                var feedbackRoot = new GameObject("Feedback Root").transform;
                feedbackRoot.SetParent(transform, false);
                Feedback = gameObject.AddComponent<ItemFeedback>();
                Feedback.Attach(feedbackRoot);
            }
            Feedback.CompleteAll();
            int pivotWidth = 0, pivotDepth = 0;
            foreach (var cell in footprint.OccupiedCells)
            {
                pivotWidth = Math.Max(pivotWidth, cell.X + 1);
                pivotDepth = Math.Max(pivotDepth, cell.Y + 1);
            }
            var pivot = new Vector3(pivotWidth * 0.5f, 0f, -pivotDepth * 0.5f);
            Feedback.SetPivot(pivot);
            Feedback.CompleteAll();
            VisualRoot = new GameObject("Visual Root").transform;
            VisualRoot.SetParent(Feedback.Root, false);
            VisualRoot.localPosition = -pivot;
            UsesPrefab = prefab != null;
            BuildShadow(footprint, template);

            if (prefab != null)
            {
                // Orientation helper carried over from the ZT-016 ItemView, so golden meshes keep their accepted alignment.
                var visual = Instantiate(prefab, VisualRoot, false);
                visual.name = prefab.name;
                // Procedural visual templates are kept inactive and hidden; golden prefabs are already active.
                visual.hideFlags = HideFlags.None;
                visual.SetActive(true);
                ApplyVisualRotation(item.State.Footprint, rotation, visual.transform);
                return;
            }

            var height = PuzzleBoardLayout.ItemHeight(Thickness);
            foreach (var cell in footprint.OccupiedCells)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Footprint {cell.X},{cell.Y}";
                Destroy(block.GetComponent<Collider>());
                block.transform.SetParent(VisualRoot, false);
                block.transform.localPosition = PuzzleBoardLayout.FootprintCellCenter(cell) + new Vector3(0f, height * 0.5f, 0f);
                block.transform.localScale = new Vector3(0.86f, height, 0.86f);
                Paint(block.GetComponent<Renderer>(), template, color);
            }
        }

        // Soft footprint-shaped shadow on the surface under the item (presentation only, no gameplay meaning).
        private void BuildShadow(ItemShape footprint, Material template)
        {
            template = PresentationKit.TemplateOrFallback(template);
            if (template == null || footprint == null)
                return;
            if (_shadow == null)
            {
                _shadowMesh = new Mesh { name = "Contact shadow" };
                _shadow = PresentationKit.MeshObject("Contact Shadow", transform, _shadowMesh, null);
            }
            if (_shadowTexture != null)
                Destroy(_shadowTexture);
            if (_shadowMaterial != null)
                Destroy(_shadowMaterial);
            _shadowTexture = PresentationKit.FootprintShadow(footprint, 12, ShadowPad, 0.14f);
            _shadowMaterial = PresentationKit.Transparent(template, PresentationKit.WithAlpha(PresentationKit.Shadow, ShadowAlpha), _shadowTexture);
            _shadow.GetComponent<MeshRenderer>().sharedMaterial = _shadowMaterial;

            int width = 0, depth = 0;
            foreach (var cell in footprint.OccupiedCells)
            {
                width = Math.Max(width, cell.X + 1);
                depth = Math.Max(depth, cell.Y + 1);
            }
            var rect = new Rect(-ShadowPad, -(depth + ShadowPad), width + 2f * ShadowPad, depth + 2f * ShadowPad);
            var quad = PresentationKit.Quad(rect, 0.004f);
            _shadowMesh.Clear();
            _shadowMesh.SetVertices(quad.vertices);
            _shadowMesh.SetUVs(0, quad.uv);
            _shadowMesh.SetTriangles(quad.triangles, 0);
            _shadowMesh.RecalculateNormals();
            _shadowMesh.RecalculateBounds();
            Destroy(quad);
            _shadow.SetActive(!IsGhosted);
            PlaceShadow();
        }

        /// <summary>Keeps the contact shadow on the surface while the item is lifted by <paramref name="localLift"/>.</summary>
        internal void SetShadowDrop(float localLift)
        {
            _shadowDrop = localLift;
            PlaceShadow();
        }

        private void PlaceShadow()
        {
            if (_shadow != null)
                _shadow.transform.localPosition = ShadowOffset - Vector3.up * _shadowDrop;
        }

        // Presentation-only: swaps this view's renderers to the shared ghost material (or hides them without one).
        internal void SetGhost(bool ghost, Material ghostMaterial)
        {
            if (ghost == IsGhosted)
                return;
            IsGhosted = ghost;
            if (_shadow != null)
                _shadow.SetActive(!ghost);
            if (VisualRoot == null)
                return;
            if (ghost)
            {
                _original.Clear();
                foreach (var renderer in VisualRoot.GetComponentsInChildren<Renderer>())
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    _original.Add((renderer, renderer.sharedMaterials, block));
                    if (ghostMaterial == null)
                    {
                        renderer.enabled = false;
                        continue;
                    }
                    var ghosts = new Material[renderer.sharedMaterials.Length];
                    for (var i = 0; i < ghosts.Length; i++)
                        ghosts[i] = ghostMaterial;
                    renderer.sharedMaterials = ghosts;
                    // An opaque per-renderer colour would override the ghost material's transparent colour.
                    var ghostBlock = new MaterialPropertyBlock();
                    var color = ghostMaterial.GetColor(PresentationKit.BaseColorId);
                    ghostBlock.SetColor(PresentationKit.BaseColorId, color);
                    ghostBlock.SetColor(PresentationKit.ColorId, color);
                    renderer.SetPropertyBlock(ghostBlock);
                }
                return;
            }
            foreach (var (renderer, materials, block) in _original)
            {
                if (renderer == null)
                    continue;
                renderer.sharedMaterials = materials;
                renderer.SetPropertyBlock(block);
                renderer.enabled = true;
            }
            _original.Clear();
        }

        private void OnDestroy()
        {
            if (_shadowMesh != null)
                Destroy(_shadowMesh);
            if (_shadowMaterial != null)
                Destroy(_shadowMaterial);
            if (_shadowTexture != null)
                Destroy(_shadowTexture);
        }

        internal static void Paint(Renderer renderer, Material template, Color color)
        {
            if (template != null)
                renderer.sharedMaterial = template;
            var block = new MaterialPropertyBlock();
            block.SetColor(PresentationKit.BaseColorId, color);
            block.SetColor(PresentationKit.ColorId, color);
            renderer.SetPropertyBlock(block);
        }

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
