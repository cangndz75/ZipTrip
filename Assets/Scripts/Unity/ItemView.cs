using System;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public sealed class ItemView : MonoBehaviour
    {
        public ItemDefinition Item { get; private set; }
        public string ItemId { get; private set; }
        public string ShapeState { get; private set; }
        public Rotation Rotation { get; private set; }
        public ItemShape Footprint { get; private set; }
        public bool IsInTray { get; private set; }
        public Transform VisualRoot { get; private set; }
        public GameObject VisualPrefabInstance { get; private set; }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            if (Footprint == null)
                return false;
            var local = transform.InverseTransformPoint(worldPoint);
            for (var i = 0; i < Footprint.OccupiedCells.Count; i++)
            {
                var cell = Footprint.OccupiedCells[i];
                if (local.x >= cell.X && local.x < cell.X + 1f &&
                    -local.z >= cell.Y && -local.z < cell.Y + 1f)
                    return true;
            }
            return false;
        }

        public void Present(ItemDefinition item, string shapeState, Rotation rotation,
            bool isInTray, Vector3 position, float scale, Color color,
            GameObject visualPrefab = null, Material runtimeMaterialTemplate = null)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            var result = item.GetRotatedShape(shapeState, rotation);
            if (!result.IsAccepted)
                throw new ArgumentException("Invalid item rotation.", nameof(rotation));

            Item = item;
            ItemId = item.Id;
            ShapeState = shapeState;
            Rotation = rotation;
            Footprint = result.Shape;
            IsInTray = isInTray;
            transform.position = position;
            transform.localScale = Vector3.one * scale;
            VisualRoot = new GameObject("Visual Root").transform;
            VisualRoot.SetParent(transform, false);

            if (visualPrefab != null)
            {
                VisualPrefabInstance = Instantiate(visualPrefab, VisualRoot, false);
                VisualPrefabInstance.name = visualPrefab.name;
                ApplyVisualRotation(item.ShapeStates[shapeState], rotation,
                    VisualPrefabInstance.transform);
                return;
            }

            foreach (var cell in Footprint.OccupiedCells)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "Footprint " + cell.X + "," + cell.Y;
                block.transform.SetParent(VisualRoot, false);
                block.transform.localPosition = new Vector3(cell.X + 0.5f, 0.2f, -cell.Y - 0.5f);
                block.transform.localScale = new Vector3(0.86f, 0.28f, 0.86f);
                var renderer = block.GetComponent<Renderer>();
                if (runtimeMaterialTemplate != null)
                    renderer.sharedMaterial = runtimeMaterialTemplate;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", color);
                properties.SetColor("_Color", color);
                renderer.SetPropertyBlock(properties);
                Destroy(block.GetComponent<Collider>());
            }
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
