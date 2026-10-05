using UnityEngine;

namespace ZipTrip.Unity
{
    /// <summary>
    /// Capture only (tests, review renders): draws a screen-space canvas into the gameplay camera's target. The canvas
    /// becomes a world-space plane just in front of that camera, scaled to exactly the visible view, so the gameplay
    /// camera's ART-CC02 vertical presentation squash maps every canvas unit to the same pixel the game's overlay
    /// would use (a ScreenSpaceCamera canvas mis-sizes itself under a custom projection). Production keeps every
    /// canvas in ScreenSpaceOverlay and never calls this.
    /// </summary>
    public static class CaptureUi
    {
        /// <returns><paramref name="camera"/> (the canvas's worldCamera), or null when the overlay is restored.</returns>
        public static Camera Attach(Canvas canvas, Camera camera, float planeDistance)
        {
            var follow = canvas.GetComponent<Follow>();
            if (camera == null)
            {
                if (follow != null)
                    Object.Destroy(follow);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                return null;
            }
            if (follow == null)
                follow = canvas.gameObject.AddComponent<Follow>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;
            follow.Distance = planeDistance;
            follow.Place();
            return camera;
        }

        private sealed class Follow : MonoBehaviour
        {
            public float Distance;

            private void LateUpdate() => Place();

            public void Place()
            {
                var canvas = GetComponent<Canvas>();
                var camera = canvas.worldCamera;
                if (camera == null || camera.pixelWidth <= 0)
                    return;
                var rect = (RectTransform)transform;
                // Canvas units stay width-matched reference px (as under the overlay's CanvasScaler).
                var size = new Vector2(1080f, 1080f * camera.pixelHeight / camera.pixelWidth);
                rect.sizeDelta = size;
                // Visible world extent at the plane, through the actual (possibly squashed) projection.
                var inverse = camera.projectionMatrix.inverse;
                var world = new Vector2(inverse.MultiplyPoint(new Vector3(1f, 0f, 0f)).x * 2f,
                    inverse.MultiplyPoint(new Vector3(0f, 1f, 0f)).y * 2f);
                rect.SetPositionAndRotation(camera.transform.position + camera.transform.forward * Distance,
                    camera.transform.rotation);
                rect.localScale = new Vector3(world.x / size.x, world.y / size.y, 1f);
            }
        }
    }
}
