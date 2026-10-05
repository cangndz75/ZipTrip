using UnityEngine;

namespace ZipTrip.Unity
{
    // Fixed, controlled framing for the ADR-0006 playable scene: the ADR-0010 fixed pitch and ADR-0002 orthographic
    // projection, sized so the gameplay bounds fit between the HUD bands at the current aspect. No free camera.
    public static class PuzzleCameraFraming
    {
        public const float Pitch = 65f; // ADR-0010 fixed camera pitch (25° tilt from top-down; supersedes ADR-0002 75°).
        public const float Distance = 12f;
        public const float Margin = 0.25f;

        // ART-CC02 composition (ADR-0010 amendment): the 9:16 reference frame is fitted into the screen without
        // stretching and centred; the suitcase body is fitted to BodyWidth of its width with its lower silhouette at
        // BodyBottom of its height (from the top). VerticalScale compresses the orthographic projection vertically
        // (presentation only) so the deep 5x7 board reads as a wide packing board: picking goes through the same
        // projection matrix (Camera.ScreenPointToRay), so cells, colliders and coordinates are unchanged.
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float BodyWidth = 0.96f;
        public const float BodyBottom = 0.78f;
        public const float VerticalScale = 0.8f;
        /// <summary>
        /// Reference px kept between the suitcase bottom and the screen bottom for the dock (title row, loose-item cards,
        /// utilities). 19.5:9 leaves more than this below BodyBottom; 16:9 (no spare height) lifts the suitcase instead
        /// of clipping the dock.
        /// </summary>
        public const float DockReserve = 560f;
        /// <summary>Screen-height per world unit of board depth relative to screen-width per world unit.</summary>
        public static readonly float GroundToScreen = Mathf.Sin(Pitch * Mathf.Deg2Rad) * VerticalScale;

        /// <summary>The 9:16 reference frame inside a pixelWidth x pixelHeight view (pixels, origin bottom-left).</summary>
        public static Rect ReferenceFrame(float pixelWidth, float pixelHeight)
        {
            var scale = Mathf.Min(pixelWidth / ReferenceWidth, pixelHeight / ReferenceHeight);
            var width = ReferenceWidth * scale;
            var height = ReferenceHeight * scale;
            return new Rect((pixelWidth - width) * 0.5f, (pixelHeight - height) * 0.5f, width, height);
        }

        /// <param name="topFraction">Screen-height fraction covered by the top HUD band.</param>
        /// <param name="bottomFraction">Screen-height fraction covered by the bottom HUD band.</param>
        public static void Frame(Camera camera, Bounds bounds, float topFraction, float bottomFraction)
        {
            Orient(camera, bounds);
            camera.ResetProjectionMatrix();

            var band = Mathf.Clamp(1f - topFraction - bottomFraction, 0.2f, 1f);
            var (halfWidth, minY, maxY) = Extent(camera, bounds);
            var halfHeight = Mathf.Max(-minY, maxY);
            var size = Mathf.Max((halfWidth + Margin) / Mathf.Max(0.01f, camera.aspect), (halfHeight + Margin) / band);
            camera.orthographicSize = size;
            // Centre the bounds in the band between the HUDs rather than in the full screen.
            camera.transform.position -= camera.transform.up * (bottomFraction - topFraction) * size;
        }

        /// <summary>
        /// ART-CC02 composition: <paramref name="body"/> (the suitcase body, lid excluded) spans BodyWidth of the
        /// reference frame's width, centred, with its lowest projected point at BodyBottom of the frame's height (or
        /// higher when the dock would not fit below it).
        /// </summary>
        public static void Compose(Camera camera, Bounds body)
        {
            Orient(camera, body);
            camera.ResetProjectionMatrix();
            var frame = ReferenceFrame(camera.pixelWidth, camera.pixelHeight);
            var (halfWidth, minY, _) = Extent(camera, body);
            // Horizontal pixels per unit = pixelHeight / (2 size); the body is BodyWidth of the frame width.
            var size = halfWidth * camera.pixelHeight / (BodyWidth * frame.width);
            camera.orthographicSize = size;
            camera.projectionMatrix = Matrix4x4.Scale(new Vector3(1f, VerticalScale, 1f)) * camera.projectionMatrix;
            var pixelsPerUnit = VerticalScale * camera.pixelHeight / (2f * size);
            var bottom = Mathf.Max(frame.yMax - BodyBottom * frame.height, DockReserve * frame.width / ReferenceWidth);
            // Screen y of the lowest point = pixelHeight/2 + (minY - shift) * pixelsPerUnit.
            var shift = minY - (bottom - camera.pixelHeight * 0.5f) / pixelsPerUnit;
            camera.transform.position += camera.transform.up * shift;
        }

        private static void Orient(Camera camera, Bounds bounds)
        {
            camera.orthographic = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            camera.transform.rotation = Quaternion.Euler(Pitch, 0f, 0f);
            camera.transform.position = bounds.center - camera.transform.forward * Distance;
        }

        private static (float HalfWidth, float MinY, float MaxY) Extent(Camera camera, Bounds bounds)
        {
            float halfWidth = 0f, minY = float.MaxValue, maxY = float.MinValue;
            for (var i = 0; i < 8; i++)
            {
                var corner = camera.transform.InverseTransformPoint(new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(corner.x));
                minY = Mathf.Min(minY, corner.y);
                maxY = Mathf.Max(maxY, corner.y);
            }
            return (halfWidth, minY, maxY);
        }
    }
}
