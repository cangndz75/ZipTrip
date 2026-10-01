using System;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    // Converts coordinates only. Domain decides whether the returned anchor is legal.
    public static class GridProjector
    {
        private static readonly Plane BoardPlane = new Plane(Vector3.up, Vector3.zero);

        public static Cell ScreenToAnchor(Camera camera, Vector2 screenPosition)
        {
            if (camera == null)
                throw new ArgumentNullException(nameof(camera));
            var ray = camera.ScreenPointToRay(screenPosition);
            if (!BoardPlane.Raycast(ray, out var distance))
                throw new InvalidOperationException("Screen ray does not intersect the board plane.");
            return WorldToAnchor(ray.GetPoint(distance));
        }

        public static Cell WorldToAnchor(Vector3 worldPosition) =>
            new Cell(NearestIntegerLowerOnTie(worldPosition.x),
                NearestIntegerLowerOnTie(-worldPosition.z));

        private static int NearestIntegerLowerOnTie(float value)
        {
            var lower = Mathf.FloorToInt(value);
            return value - lower > 0.5f ? lower + 1 : lower;
        }
    }
}
