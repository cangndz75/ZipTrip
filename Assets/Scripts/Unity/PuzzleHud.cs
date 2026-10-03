using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZipTrip.Unity
{
    public enum PuzzleHudAction
    {
        None = 0,
        Rotate = 1,
        Undo = 2,
        Restart = 3,
        Next = 4
    }

    // Minimal first-playable HUD (ZT-040): level label, Rotate / Undo / Restart buttons and a plain "Packed!" card.
    // Like the legacy GameplayHud, taps are hit-tested through the single PointerInteractor flow (no EventSystem).
    // Not the Zip It ritual (ZT-043). ZT-040B styling: compact rounded icon buttons with small captions, a level pill
    // and a rounded card, all subordinate to the suitcase.
    public sealed class PuzzleHud : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float TopBand = 150f;
        public const float BottomBand = 250f;

        private readonly Dictionary<PuzzleHudAction, RectTransform> _buttons = new Dictionary<PuzzleHudAction, RectTransform>();
        private Font _font;
        private Text _label;
        private RectTransform _card;
        private Sprite _rounded;
        private readonly List<Object> _owned = new List<Object>();

        public bool CompletionVisible => _card != null && _card.gameObject.activeSelf;
        public bool RotateVisible => _buttons.TryGetValue(PuzzleHudAction.Rotate, out var rotate) && rotate.gameObject.activeSelf;
        public string LevelLabel => _label != null ? _label.text : null;

        /// <summary>Fraction of a pixelWidth x pixelHeight view covered by the top HUD band (width-matched scaling).</summary>
        public static float TopFraction(float pixelWidth, float pixelHeight) => TopBand * pixelWidth / ReferenceWidth / Mathf.Max(1f, pixelHeight);

        public static float BottomFraction(float pixelWidth, float pixelHeight) =>
            BottomBand * pixelWidth / ReferenceWidth / Mathf.Max(1f, pixelHeight);

        public void Build()
        {
            if (_font != null)
                return;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0f;

            _rounded = Own(PresentationKit.RoundedSprite(96, 40));
            Own(_rounded.texture);
            var undoIcon = Own(PresentationKit.UndoIcon(128));
            var restartIcon = Own(PresentationKit.RestartIcon(128));
            var rotateIcon = Own(PresentationKit.RotateIcon(128));
            foreach (var icon in new[] { undoIcon, restartIcon, rotateIcon })
                Own(icon.texture);

            var pill = Panel(transform, "Level Pill", new Vector2(0.5f, 1f), new Vector2(0f, -TopBand * 0.5f), new Vector2(280f, 76f),
                PresentationKit.WithAlpha(PresentationKit.Paper, 0.88f));
            _label = Label(pill, "", 38, PresentationKit.DeepBlueGreen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(280f, 76f));

            var row = BottomBand * 0.5f;
            AddButton(PuzzleHudAction.Undo, "Undo", new Vector2(0f, 0f), new Vector2(115f, row), new Vector2(150f, 150f),
                PresentationKit.WithAlpha(PresentationKit.Paper, 0.94f), icon: undoIcon);
            AddButton(PuzzleHudAction.Restart, "Restart", new Vector2(0f, 0f), new Vector2(285f, row), new Vector2(150f, 150f),
                PresentationKit.WithAlpha(PresentationKit.Paper, 0.94f), icon: restartIcon);
            AddButton(PuzzleHudAction.Rotate, "Rotate", new Vector2(1f, 0f), new Vector2(-135f, row), new Vector2(190f, 190f),
                PresentationKit.Mustard, icon: rotateIcon);

            _card = Panel(transform, "Packed Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 500f), PresentationKit.Paper);
            Label(_card, "Packed!", 104, PresentationKit.DeepBlueGreen, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(700f, 140f));
            AddButton(PuzzleHudAction.Next, "Next", new Vector2(0.5f, 0f), new Vector2(170f, 120f), new Vector2(280f, 130f), PresentationKit.Teal, _card,
                textColor: PresentationKit.Paper);
            AddButton(PuzzleHudAction.Restart, "Replay", new Vector2(0.5f, 0f), new Vector2(-170f, 120f), new Vector2(280f, 130f), PresentationKit.TrayCard, _card, register: false);
            _card.gameObject.SetActive(false);
        }

        public void SetLevel(string label) => _label.text = label;

        /// <summary>Capture only: draw the HUD through a camera so off-screen renders include it.</summary>
        public void RenderThrough(Camera camera)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
        }

        public void SetRotateVisible(bool visible) => _buttons[PuzzleHudAction.Rotate].gameObject.SetActive(visible);

        public void SetCompletionVisible(bool visible) => _card.gameObject.SetActive(visible);

        /// <summary>Topmost visible HUD action under a screen point, or None.</summary>
        public PuzzleHudAction Hit(Vector2 screenPosition)
        {
            if (CompletionVisible)
            {
                foreach (Transform child in _card)
                    if (child.name.StartsWith("Button ") && child.gameObject.activeSelf
                        && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)child, screenPosition, null))
                        return child.name == "Button Next" ? PuzzleHudAction.Next : PuzzleHudAction.Restart;
                return PuzzleHudAction.None;
            }
            foreach (var pair in _buttons)
                if (pair.Value.gameObject.activeInHierarchy && pair.Value.parent == transform
                    && RectTransformUtility.RectangleContainsScreenPoint(pair.Value, screenPosition, null))
                    return pair.Key;
            return PuzzleHudAction.None;
        }

        /// <summary>Screen-space centre of a visible button (tests and device scripts).</summary>
        public Vector2 ButtonCenter(PuzzleHudAction action)
        {
            var rect = action == PuzzleHudAction.Next ? (RectTransform)_card.Find("Button Next") : _buttons[action];
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        // Icon buttons: icon in the upper part, small caption below; text buttons: centred label. The whole rounded
        // rect is the hit target (Hit uses the button rect, not the icon).
        private void AddButton(PuzzleHudAction action, string text, Vector2 anchor, Vector2 position, Vector2 size, Color color,
            Transform parent = null, bool register = true, Sprite icon = null, Color? textColor = null)
        {
            var ink = textColor ?? PresentationKit.DeepBlueGreen;
            var button = Panel(parent ?? transform, "Button " + text, anchor, position, size, color);
            if (icon != null)
            {
                var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                var rect = (RectTransform)image.transform;
                rect.SetParent(button, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, size.y * 0.1f);
                rect.sizeDelta = Vector2.one * size.x * 0.62f;
                image.sprite = icon;
                image.color = ink;
                image.raycastTarget = false;
                Label(button, text, Mathf.RoundToInt(size.x * 0.17f), ink, new Vector2(0.5f, 0f), new Vector2(0f, size.y * 0.16f),
                    new Vector2(size.x, size.y * 0.3f));
            }
            else
            {
                Label(button, text, 46, ink, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            }
            if (register)
                _buttons[action] = button;
        }

        private RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (_rounded != null)
            {
                image.sprite = _rounded;
                image.type = Image.Type.Sliced;
            }
            return rect;
        }

        private T Own<T>(T asset) where T : Object
        {
            _owned.Add(asset);
            return asset;
        }

        private void OnDestroy()
        {
            foreach (var asset in _owned)
                if (asset != null)
                    Destroy(asset);
        }

        private Text Label(Transform parent, string text, int size, Color color, Vector2 anchor, Vector2 position, Vector2 box)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var rect = (RectTransform)label.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            label.font = _font;
            label.fontSize = size;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }
    }
}
