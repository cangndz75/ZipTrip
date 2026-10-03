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
    // and a rounded card, all subordinate to the suitcase. ZT-040D: one paper-and-ink shape language with soft drop
    // shadows, a pressed state, a dimmed Undo when there is nothing to undo, mustard reserved for Rotate, and the
    // temporary Packed card moved off the suitcase. Hit targets are unchanged.
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
        private RectTransform _pressed;
        private float _pressTime;
        public const float PressDuration = 0.16f;
        public static readonly Color Ink = PresentationKit.DeepBlueGreen;
        public static readonly Color PaperFill = PresentationKit.Hex(0xF7F1E6);
        public static readonly Color DropShadow = new Color(0.12f, 0.16f, 0.17f, 0.22f);
        public bool UndoEnabled { get; private set; } = true;

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

            // Level pass: paper pill with a teal tab, top centre.
            Shadowed(transform, new Vector2(0.5f, 1f), new Vector2(0f, -TopBand * 0.5f), new Vector2(300f, 84f));
            var pill = Panel(transform, "Level Pill", new Vector2(0.5f, 1f), new Vector2(0f, -TopBand * 0.5f), new Vector2(300f, 84f), PaperFill);
            var tab = Panel(pill, "Tab", new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(44f, 44f), PresentationKit.Teal);
            tab.sizeDelta = new Vector2(18f, 18f);
            tab.GetComponent<Image>().type = Image.Type.Simple;
            _label = Label(pill, "", 40, Ink, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(250f, 84f));

            var row = BottomBand * 0.5f;
            AddButton(PuzzleHudAction.Undo, "Undo", new Vector2(0f, 0f), new Vector2(115f, row), new Vector2(150f, 150f), PaperFill, icon: undoIcon);
            AddButton(PuzzleHudAction.Restart, "Restart", new Vector2(0f, 0f), new Vector2(285f, row), new Vector2(150f, 150f), PaperFill, icon: restartIcon);
            AddButton(PuzzleHudAction.Rotate, "Rotate", new Vector2(1f, 0f), new Vector2(-135f, row), new Vector2(190f, 190f),
                PresentationKit.Mustard, icon: rotateIcon);

            // Temporary completion (ZT-043 replaces it): sits low, over the emptied mat, so the packed suitcase stays visible.
            var cardShadow = Shadowed(transform, new Vector2(0.5f, 0f), new Vector2(0f, 560f), new Vector2(760f, 340f));
            _card = Panel(transform, "Packed Card", new Vector2(0.5f, 0f), new Vector2(0f, 560f), new Vector2(760f, 340f), PaperFill);
            cardShadow.SetParent(_card, true);
            cardShadow.SetAsFirstSibling();
            Label(_card, "PACKED", 84, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -95f), new Vector2(700f, 110f));
            AddButton(PuzzleHudAction.Next, "Next", new Vector2(0.5f, 0f), new Vector2(170f, 95f), new Vector2(280f, 120f), PresentationKit.Teal, _card,
                textColor: PaperFill);
            AddButton(PuzzleHudAction.Restart, "Replay", new Vector2(0.5f, 0f), new Vector2(-170f, 95f), new Vector2(280f, 120f), PresentationKit.TrayCard, _card, register: false);
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

        /// <summary>Dims Undo when there is nothing to undo (visual state only; the tap stays a harmless no-op).</summary>
        public void SetUndoEnabled(bool enabled)
        {
            if (enabled == UndoEnabled)
                return;
            UndoEnabled = enabled;
            var group = _buttons[PuzzleHudAction.Undo].GetComponent<CanvasGroup>();
            group.alpha = enabled ? 1f : 0.6f;
        }

        /// <summary>Pressed state: the hit button dips and springs back (visual only).</summary>
        public void Press(PuzzleHudAction action)
        {
            if (_pressed != null)
                _pressed.localScale = Vector3.one;
            _pressed = action == PuzzleHudAction.Next ? (RectTransform)_card.Find("Button Next")
                : _buttons.TryGetValue(action, out var button) ? button : null;
            _pressTime = 0f;
        }

        private void Update()
        {
            if (_pressed == null)
                return;
            _pressTime += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(_pressTime / PressDuration);
            // 0.92 dip then out-back to rest.
            var back = t - 1f;
            var ease = back * back * (2.6f * back + 1.6f) + 1f;
            _pressed.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, ease);
            if (t >= 1f)
            {
                _pressed.localScale = Vector3.one;
                _pressed = null;
            }
        }

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
            var ink = textColor ?? Ink;
            var shadow = Shadowed(parent ?? transform, anchor, position, size);
            var button = Panel(parent ?? transform, "Button " + text, anchor, position, size, color);
            button.gameObject.AddComponent<CanvasGroup>();
            shadow.SetParent(button, true);
            shadow.SetAsFirstSibling();
            if (icon != null)
            {
                var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                var rect = (RectTransform)image.transform;
                rect.SetParent(button, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, size.y * 0.11f);
                rect.sizeDelta = Vector2.one * size.x * 0.5f;
                image.sprite = icon;
                image.color = ink;
                image.raycastTarget = false;
                Label(button, text, Mathf.RoundToInt(size.x * 0.15f), ink, new Vector2(0.5f, 0f), new Vector2(0f, size.y * 0.17f),
                    new Vector2(size.x, size.y * 0.3f));
            }
            else
            {
                Label(button, text, 46, ink, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            }
            if (register)
                _buttons[action] = button;
        }

        // Soft drop shadow under a panel: same rounded shape, offset down, slightly larger and translucent.
        private RectTransform Shadowed(Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var shadow = Panel(parent, "Drop Shadow", anchor, position + new Vector2(0f, -7f), size + new Vector2(6f, 6f), DropShadow);
            return shadow;
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
