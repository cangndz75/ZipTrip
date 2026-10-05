using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    // One flat, upward-facing soft quad shaped like a set of cells (via a cached alpha mask); no collider. Shared by the
    // drag preview (ZT-040C) and the live rule overlays (ZT-042): zone areas, target and warning glows.
    internal sealed class SoftCellShape
    {
        public const int PixelsPerCell = 16;
        public const float Pad = 0.16f;
        public const float Blur = 0.1f;
        public const int EdgePixelsPerCell = 32;
        public const float EdgeBlur = 0.07f;

        private readonly string _name;
        private readonly bool _edge;
        private GameObject _object;
        private Mesh _mesh;
        private Texture2D _mask;
        private string _key;

        /// <param name="edge">Outline glow only (ART-CC02 active rule region) instead of a filled shape.</param>
        public SoftCellShape(string name, bool edge = false)
        {
            _name = name;
            _edge = edge;
        }

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
                _mask = _edge ? PresentationKit.CellMask(local, EdgePixelsPerCell, Pad, EdgeBlur, true)
                    : PresentationKit.CellMask(local, PixelsPerCell, Pad, Blur);
                int width = 0, depth = 0;
                foreach (var cell in local)
                {
                    width = Math.Max(width, cell.X + 1);
                    depth = Math.Max(depth, cell.Y + 1);
                }
                var quad = PresentationKit.Quad(new Rect(-Pad, -(depth + Pad), width + 2f * Pad,
                    depth + 2f * Pad), 0f);
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
}
