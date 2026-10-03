using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // One BoardSpec compartment: floor tiles exactly over valid mask cells; masked cells get no tile.
    public sealed class PuzzleCompartmentView : MonoBehaviour
    {
        public string CompartmentId { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Layers { get; private set; }
        public int ValidCellCount { get; private set; }
        public Transform ItemsRoot { get; private set; }

        internal void Build(Compartment compartment, Material template)
        {
            CompartmentId = compartment.Id;
            Width = compartment.Width;
            Height = compartment.Height;
            Layers = compartment.Layers;
            ItemsRoot = new GameObject("Items").transform;
            ItemsRoot.SetParent(transform, false);
            foreach (var column in compartment.Mask.GetValidCells())
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = $"Cell {column.X},{column.Y}";
                UnityEngine.Object.Destroy(tile.GetComponent<Collider>());
                tile.transform.SetParent(transform, false);
                tile.transform.localPosition = PuzzleBoardLayout.ColumnCenter(column) + new Vector3(0f, -0.03f, 0f);
                tile.transform.localScale = new Vector3(0.94f, 0.04f, 0.94f);
                PuzzleItemView.Paint(tile.GetComponent<Renderer>(), template, PresentationKit.WarmOffWhite);
                ValidCellCount++;
            }
        }
    }
}
