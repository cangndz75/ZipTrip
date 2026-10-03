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
    // only. VisualRoot holds either the resolved prefab or footprint blocks for the shown rotation.
    public sealed class PuzzleItemView : MonoBehaviour
    {
        private readonly List<(Renderer Renderer, Material[] Materials, MaterialPropertyBlock Block)> _original =
            new List<(Renderer, Material[], MaterialPropertyBlock)>();
        private string _visualKey;

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
        public bool UsesPrefab { get; private set; }
        public bool IsGhosted { get; private set; }

        internal void Bind(PuzzleItem item, Transform compartmentItems, GameObject prefab, Material template, Color color)
        {
            var placement = item.Location.Placement;
            Placement = placement;
            IsOnBoard = true;
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
            var local = world - transform.position;
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
            VisualRoot = new GameObject("Visual Root").transform;
            VisualRoot.SetParent(transform, false);
            UsesPrefab = prefab != null;

            if (prefab != null)
            {
                // Same orientation helper as the legacy ItemView, so golden meshes keep their accepted alignment.
                var visual = Instantiate(prefab, VisualRoot, false);
                visual.name = prefab.name;
                ItemView.ApplyVisualRotation(item.State.Footprint, rotation, visual.transform);
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

        // Presentation-only: swaps this view's renderers to the shared ghost material (or hides them without one).
        internal void SetGhost(bool ghost, Material ghostMaterial)
        {
            if (ghost == IsGhosted)
                return;
            IsGhosted = ghost;
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

        internal static void Paint(Renderer renderer, Material template, Color color)
        {
            if (template != null)
                renderer.sharedMaterial = template;
            var block = new MaterialPropertyBlock();
            block.SetColor(PresentationKit.BaseColorId, color);
            block.SetColor(PresentationKit.ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
