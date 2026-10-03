using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // One BoardSpec compartment. Every valid mask cell owns a placement guide ("Cell x,y"; masked cells get none) that
    // is hidden while idle, so the suitcase lining, not a grid, is what the player sees. The drag controller reveals
    // guides only around the snapped candidate footprint (ZT-040B). Guides are presentation-only.
    public sealed class PuzzleCompartmentView : MonoBehaviour
    {
        private readonly Dictionary<Cell, Renderer> _guides = new Dictionary<Cell, Renderer>();

        public string CompartmentId { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Layers { get; private set; }
        public int ValidCellCount { get; private set; }
        public Transform ItemsRoot { get; private set; }
        public int VisibleGuideCount { get; private set; }

        internal void Build(Compartment compartment, Material guideMaterial)
        {
            CompartmentId = compartment.Id;
            Width = compartment.Width;
            Height = compartment.Height;
            Layers = compartment.Layers;
            ItemsRoot = new GameObject("Items").transform;
            ItemsRoot.SetParent(transform, false);
            foreach (var column in compartment.Mask.GetValidCells())
            {
                var guide = GameObject.CreatePrimitive(PrimitiveType.Quad);
                guide.name = $"Cell {column.X},{column.Y}";
                UnityEngine.Object.Destroy(guide.GetComponent<Collider>());
                guide.transform.SetParent(transform, false);
                guide.transform.localPosition = PuzzleBoardLayout.ColumnCenter(column) + new Vector3(0f, GuideY, 0f);
                guide.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                guide.transform.localScale = new Vector3(0.86f, 0.86f, 1f);
                var renderer = guide.GetComponent<Renderer>();
                if (guideMaterial != null)
                    renderer.sharedMaterial = guideMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.enabled = false;
                _guides.Add(column, renderer);
                ValidCellCount++;
            }
        }

        /// <summary>Lining height of the guides (just above the suitcase lining, below every item).</summary>
        public const float GuideY = SuitcaseShell.LiningY + 0.008f;

        /// <summary>Shows exactly the given valid cells' guides and hides all others. Unknown cells are ignored.</summary>
        public void ShowGuides(ICollection<Cell> cells)
        {
            VisibleGuideCount = 0;
            foreach (var pair in _guides)
            {
                var visible = cells != null && cells.Contains(pair.Key);
                pair.Value.enabled = visible;
                if (visible)
                    VisibleGuideCount++;
            }
        }

        public void HideGuides() => ShowGuides(null);

        public bool IsGuideVisible(Cell cell) => _guides.TryGetValue(cell, out var renderer) && renderer.enabled;
    }
}
