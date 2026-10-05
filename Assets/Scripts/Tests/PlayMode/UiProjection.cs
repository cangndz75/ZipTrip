using UnityEngine;

namespace ZipTrip.Tests.PlayMode
{
    // Screen position of HUD geometry through the camera that actually draws its canvas (null for the game's overlay,
    // the CaptureUi camera while capturing), never through the gameplay camera and its ART-CC02 vertical squash.
    internal static class UiProjection
    {
        public static Vector2 Screen(RectTransform rect, Vector3 world)
        {
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            return RectTransformUtility.WorldToScreenPoint(
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, world);
        }
    }
}
