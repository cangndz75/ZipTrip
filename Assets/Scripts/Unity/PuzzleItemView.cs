using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // One suitcase-resident PuzzleItem (one view per instance; thickness is a dimension, not extra views).
    // Root = placement inside its compartment; VisualRoot holds either the resolved prefab or footprint blocks.
    public sealed class PuzzleItemView : MonoBehaviour
    {
        private readonly List<(Renderer Renderer, Material[] Materials)> _original = new List<(Renderer, Material[])>();
        private string _visualKey;

        public string InstanceId { get; private set; }
        public string DefinitionId { get; private set; }
        public string StateId { get; private set; }
        public Placement Placement { get; private set; }
        public int Thickness { get; private set; }
        public ItemShape Footprint { get; private set; }
        public Transform VisualRoot { get; private set; }
        public bool UsesPrefab { get; private set; }
        public bool IsGhosted { get; private set; }

        internal void Bind(PuzzleItem item, Transform compartmentItems, GameObject prefab, Material template, Color color)
        {
            var placement = item.Location.Placement;
            item.State.TryGetFootprint(placement.Rotation, out var footprint);
            InstanceId = item.InstanceId;
            DefinitionId = item.Definition.Id;
            StateId = item.StateId;
            Placement = placement;
            Thickness = item.State.Thickness;
            Footprint = footprint;
            transform.SetParent(compartmentItems, false);
            transform.localPosition = PuzzleBoardLayout.ItemLocalPosition(placement);

            var key = $"{DefinitionId}|{StateId}|{(int)placement.Rotation}|{(prefab != null ? prefab.GetInstanceID() : 0)}";
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
                ItemView.ApplyVisualRotation(item.State.Footprint, placement.Rotation, visual.transform);
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
                    _original.Add((renderer, renderer.sharedMaterials));
                    if (ghostMaterial == null)
                    {
                        renderer.enabled = false;
                        continue;
                    }
                    var ghosts = new Material[renderer.sharedMaterials.Length];
                    for (var i = 0; i < ghosts.Length; i++)
                        ghosts[i] = ghostMaterial;
                    renderer.sharedMaterials = ghosts;
                }
                return;
            }
            foreach (var (renderer, materials) in _original)
            {
                if (renderer == null)
                    continue;
                renderer.sharedMaterials = materials;
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
