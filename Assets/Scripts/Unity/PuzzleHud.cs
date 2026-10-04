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
        Next = 4,
        Fold = 5,
        Compress = 6
    }

    // First-playable HUD: level label, Rotate / Undo / Restart buttons and the Zip It luggage tag.
    // Like the legacy GameplayHud, taps are hit-tested through the single PointerInteractor flow (no EventSystem).
    // ZT-040B styling: compact rounded icon buttons with small captions, a level pill
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
        private Font _display;
        private bool _customFont;
        private Text _label;
        private RectTransform _levelShadow;
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
        /// <summary>Face used by the HUD's labels (Bricolage Grotesque in the shipped scene).</summary>
        public Font Font => _font;

        public bool CompletionVisible => _card != null && _card.gameObject.activeSelf;
        public bool CompletionMode { get; private set; }
        public bool RotateVisible => _buttons.TryGetValue(PuzzleHudAction.Rotate, out var rotate) && rotate.gameObject.activeSelf;
        public bool FoldVisible => _buttons.TryGetValue(PuzzleHudAction.Fold, out var fold) && fold.gameObject.activeSelf;
        public bool CompressVisible => _buttons.TryGetValue(PuzzleHudAction.Compress, out var compress) && compress.gameObject.activeSelf;
        public string LevelLabel => _label != null ? _label.text : null;

        /// <summary>
        /// Parent of every HUD element: the canvas inset to the device safe area (notch / cutout / home indicator).
        /// Re-evaluated every frame, so resolution, orientation and safe-area changes are followed. Camera framing
        /// still reserves the fixed TopBand / BottomBand; only the HUD moves.
        /// </summary>
        public RectTransform SafeArea { get; private set; }
        /// <summary>Normalized (0..1) safe rect currently applied to <see cref="SafeArea"/>.</summary>
        public Rect AppliedSafeArea { get; private set; }
        /// <summary>Test-only seam: normalized safe rect used instead of Screen.safeArea (null = the device's).</summary>
        public static Rect? SafeAreaOverride { get; set; }
        private GameObject _safeAreaDebug;
        /// <summary>Development-only tint over the safe area for review captures; never built in release players.</summary>
        public bool SafeAreaDebugVisible => _safeAreaDebug != null && _safeAreaDebug.activeSelf;

        /// <summary>Screen.safeArea (pixels) as a normalized rect, clamped to the screen.</summary>
        public static Rect NormalizeSafeArea(Rect safeArea, float screenWidth, float screenHeight)
        {
            if (screenWidth <= 0f || screenHeight <= 0f)
                return new Rect(0f, 0f, 1f, 1f);
            var xMin = Mathf.Clamp01(safeArea.xMin / screenWidth);
            var yMin = Mathf.Clamp01(safeArea.yMin / screenHeight);
            var xMax = Mathf.Clamp01(safeArea.xMax / screenWidth);
            var yMax = Mathf.Clamp01(safeArea.yMax / screenHeight);
            return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : new Rect(0f, 0f, 1f, 1f);
        }

        private void ApplySafeArea()
        {
            if (SafeArea == null)
                return;
            var safe = SafeAreaOverride ?? NormalizeSafeArea(Screen.safeArea, Screen.width, Screen.height);
            if (safe == AppliedSafeArea)
                return;
            AppliedSafeArea = safe;
            SafeArea.anchorMin = safe.min;
            SafeArea.anchorMax = safe.max;
        }

        /// <summary>Shows a translucent tint over the safe area (editor / development builds only; no-op in release).</summary>
        public void SetSafeAreaDebugVisible(bool visible)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_safeAreaDebug == null && visible)
            {
                _safeAreaDebug = new GameObject("Safe Area Debug", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)_safeAreaDebug.transform;
                rect.SetParent(SafeArea, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling();
                var image = _safeAreaDebug.GetComponent<Image>();
                image.color = new Color(0.2f, 0.9f, 0.4f, 0.18f);
                image.raycastTarget = false;
            }
            if (_safeAreaDebug != null)
                _safeAreaDebug.SetActive(visible);
#endif
        }

        /// <summary>Fraction of a pixelWidth x pixelHeight view covered by the top HUD band (width-matched scaling).</summary>
        public static float TopFraction(float pixelWidth, float pixelHeight) => TopBand * pixelWidth / ReferenceWidth / Mathf.Max(1f, pixelHeight);

        public static float BottomFraction(float pixelWidth, float pixelHeight) =>
            BottomBand * pixelWidth / ReferenceWidth / Mathf.Max(1f, pixelHeight);

        /// <param name="uiFont">ZT-040D.1 UI face (Bricolage Grotesque SemiBold); null = built-in fallback.</param>
        /// <param name="displayFont">Display face for one-off words (Bricolage Grotesque ExtraBold); null = uiFont.</param>
        public void Build(Font uiFont = null, Font displayFont = null)
        {
            if (_font != null)
                return;
            _customFont = uiFont != null;
            _font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _display = displayFont != null ? displayFont : _font;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 0f;
            SafeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
            SafeArea.SetParent(transform, false);
            SafeArea.offsetMin = SafeArea.offsetMax = Vector2.zero;
            ApplySafeArea();

            _rounded = Own(PresentationKit.RoundedSprite(96, 40));
            Own(_rounded.texture);
            var undoIcon = Own(PresentationKit.UndoIcon(128));
            var restartIcon = Own(PresentationKit.RestartIcon(128));
            var rotateIcon = Own(PresentationKit.RotateIcon(128));
            foreach (var icon in new[] { undoIcon, restartIcon, rotateIcon })
                Own(icon.texture);

            // Level pass: paper pill with a teal tab, top centre.
            _levelShadow = Shadowed(SafeArea, new Vector2(0.5f, 1f), new Vector2(0f, -TopBand * 0.5f), new Vector2(300f, 84f));
            var pill = Panel(SafeArea, "Level Pill", new Vector2(0.5f, 1f), new Vector2(0f, -TopBand * 0.5f), new Vector2(300f, 84f), PaperFill);
            var tab = Panel(pill, "Tab", new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(44f, 44f), PresentationKit.Teal);
            tab.sizeDelta = new Vector2(18f, 18f);
            tab.GetComponent<Image>().type = Image.Type.Simple;
            _label = Label(pill, "", 40, Ink, new Vector2(0.5f, 0.5f), new Vector2(14f, 0f), new Vector2(250f, 84f));

            var row = BottomBand * 0.5f;
            AddButton(PuzzleHudAction.Undo, "Undo", new Vector2(0f, 0f), new Vector2(115f, row), new Vector2(150f, 150f), PaperFill, icon: undoIcon);
            AddButton(PuzzleHudAction.Restart, "Restart", new Vector2(0f, 0f), new Vector2(285f, row), new Vector2(150f, 150f), PaperFill, icon: restartIcon);
            AddButton(PuzzleHudAction.Rotate, "Rotate", new Vector2(1f, 0f), new Vector2(-135f, row), new Vector2(190f, 190f),
                PresentationKit.Mustard, icon: rotateIcon);
            AddButton(PuzzleHudAction.Fold, "Fold", new Vector2(1f, 0f), new Vector2(-135f, row + 190f), new Vector2(150f, 150f),
                PresentationKit.Mustard);
            AddButton(PuzzleHudAction.Compress, "Compress", new Vector2(1f, 0f), new Vector2(-300f, row + 190f), new Vector2(150f, 150f),
                PresentationKit.Mustard);
            _buttons[PuzzleHudAction.Compress].GetComponentInChildren<Text>().fontSize = 29;
            SetModifierVisible(false, false);

            // Compact travel tag over the emptied mat; the closed suitcase remains unobstructed.
            var cardShadow = Shadowed(SafeArea, new Vector2(0.5f, 0f), new Vector2(0f, 390f), new Vector2(640f, 245f));
            _card = Panel(SafeArea, "Packed Tag", new Vector2(0.5f, 0f), new Vector2(0f, 390f), new Vector2(640f, 245f), PaperFill);
            cardShadow.SetParent(_card, true);
            cardShadow.SetAsFirstSibling();
            var tagHole = Panel(_card, "Tag Hole", new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(28f, 28f), PresentationKit.TrayCard);
            tagHole.GetComponent<Image>().type = Image.Type.Simple;
            Label(_card, "Packed!", 68, Ink, new Vector2(0.5f, 1f), new Vector2(0f, -75f), new Vector2(590f, 82f)).font = _display;
            AddButton(PuzzleHudAction.Next, "Next", new Vector2(0.5f, 0f), new Vector2(140f, 70f), new Vector2(245f, 92f), PresentationKit.Teal, _card,
                textColor: PaperFill);
            AddButton(PuzzleHudAction.Restart, "Replay", new Vector2(0.5f, 0f), new Vector2(-140f, 70f), new Vector2(245f, 92f), PresentationKit.TrayCard, _card, register: false);
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

        public void SetModifierVisible(bool fold, bool compress)
        {
            _buttons[PuzzleHudAction.Fold].gameObject.SetActive(fold);
            _buttons[PuzzleHudAction.Compress].gameObject.SetActive(compress);
        }

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
            ApplySafeArea();
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

        public void SetCompletionMode(bool active)
        {
            CompletionMode = active;
            _buttons[PuzzleHudAction.Undo].gameObject.SetActive(!active);
            _buttons[PuzzleHudAction.Restart].gameObject.SetActive(!active);
            if (active)
            {
                SetRotateVisible(false);
                SetModifierVisible(false, false);
            }
            _label.transform.parent.gameObject.SetActive(!active);
            _levelShadow.gameObject.SetActive(!active);
            if (!active)
                SetCompletionVisible(false);
        }

        /// <summary>Topmost visible HUD action under a screen point, or None.</summary>
        public PuzzleHudAction Hit(Vector2 screenPosition)
        {
            if (CompletionMode)
            {
                foreach (Transform child in _card)
                    if (child.name.StartsWith("Button ") && child.gameObject.activeSelf
                        && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)child, screenPosition, null))
                        return child.name == "Button Next" ? PuzzleHudAction.Next : PuzzleHudAction.Restart;
                return PuzzleHudAction.None;
            }
            foreach (var pair in _buttons)
                if (pair.Value.gameObject.activeInHierarchy && pair.Value.parent == SafeArea
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
            var shadow = Shadowed(parent ?? SafeArea, anchor, position, size);
            var button = Panel(parent ?? SafeArea, "Button " + text, anchor, position, size, color);
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
            // The authored weights replace the synthetic bold the built-in fallback needs.
            label.fontStyle = _customFont ? FontStyle.Normal : FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }
    }
}
