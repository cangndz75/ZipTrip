using UnityEngine;

namespace ZipTrip.Unity
{
    // Fixed, controlled framing for the ADR-0006 playable scene: the FixedGameplayCamera pitch and orthographic
    // projection, sized so the gameplay bounds fit between the HUD bands at the current aspect. No free camera.
    public static class PuzzleCameraFraming
    {
        public const float Pitch = 75f; // Same as FixedGameplayCamera.
        public const float Distance = 12f;
        public const float Margin = 0.35f;

        /// <param name="topFraction">Screen-height fraction covered by the top HUD band.</param>
        /// <param name="bottomFraction">Screen-height fraction covered by the bottom HUD band.</param>
        public static void Frame(Camera camera, Bounds bounds, float topFraction, float bottomFraction)
        {
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            camera.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            camera.transform.position = bounds.center - camera.transform.forward * Distance;

            var band = Mathf.Clamp(1f - topFraction - bottomFraction, 0.2f, 1f);
            float halfWidth = 0f, halfHeight = 0f;
            for (var i = 0; i < 8; i++)
            {
                var corner = camera.transform.InverseTransformPoint(new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(corner.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(corner.y));
            }
            var size = Mathf.Max((halfWidth + Margin) / Mathf.Max(0.01f, camera.aspect), (halfHeight + Margin) / band);
            camera.orthographicSize = size;
            // Centre the bounds in the band between the HUDs rather than in the full screen.
            camera.transform.position -= camera.transform.up * (bottomFraction - topFraction) * size;
        }
    }
}
