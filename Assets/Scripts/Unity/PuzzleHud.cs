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

    // Gameplay HUD. Taps are hit-tested through the single PointerInteractor flow (no EventSystem; every Graphic keeps
    // raycastTarget off). UI-SLICE-01 (STYLE-FRAME-01): a safe-area header card (travel badge, "Seviye N", optional
    // subtitle), a bottom paper dock with Geri Al / Baştan at thumb reach and a contextual centre slot (a medium-priority
    // Döndür pill while the selection can rotate, otherwise a quiet "N eşya kaldı" status), and the existing Zip It
    // confirmation whose Sonraki uses the hero mustard tier. Camera framing still reserves TopBand / BottomBand.
    public sealed class PuzzleHud : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float TopBand = 150f;
        public const float BottomBand = 250f;
        /// <summary>Header card bottom, reference px below the safe-area top (the objective note starts below it).</summary>
        public const float HeaderBottom = 150f;
        public static readonly Vector2 HeaderSize = new Vector2(560f, 128f);
        public static readonly Vector2 DockSize = new Vector2(1016f, 196f);
        public const float DockCenterY = 120f;
        public static readonly Vector2 RotateSize = new Vector2(350f, 124f);
        public const float ControlSize = 140f;

        private readonly Dictionary<PuzzleHudAction, RectTransform> _buttons = new Dictionary<PuzzleHudAction, RectTransform>();
        private Font _font;
        private Font _display;
        private Text _title;
        private Text _subtitle;
        private RectTransform _header;
        private RectTransform _dock;
        private Text _status;
        private RectTransform _card;
        private RectTransform _stamp;
        private Sprite _rounded;
        private readonly List<Object> _owned = new List<Object>();
        private RectTransform _pressed;
        private float _pressTime;
        public const float PressDuration = MotionTokens.UiPressDuration;
        public static readonly Color Ink = PaperUi.Ink;
        public static readonly Color PaperFill = PaperUi.Cream;
        public static readonly Color DropShadow = PaperUi.Shadow;
        public bool UndoEnabled { get; private set; } = true;
        /// <summary>Face used by the HUD's labels (Bricolage Grotesque SemiBold in the shipped scene).</summary>
        public Font Font => _font;
        /// <summary>Display face (Bricolage Grotesque ExtraBold in the shipped scene).</summary>
        public Font DisplayFont => _display;

        public bool CompletionVisible => _card != null && _card.gameObject.activeSelf;
        public bool CompletionActionsVisible => _card != null && _card.Find("Button Sonraki").gameObject.activeSelf;
        public float StampProgress { get; private set; }
        public bool CompletionMode { get; private set; }
        public bool RotateVisible => _buttons.TryGetValue(PuzzleHudAction.Rotate, out var rotate) && rotate.gameObject.activeSelf;
        public bool FoldVisible => _buttons.TryGetValue(PuzzleHudAction.Fold, out var fold) && fold.gameObject.activeSelf;
        public bool CompressVisible => _buttons.TryGetValue(PuzzleHudAction.Compress, out var compress) && compress.gameObject.activeSelf;
        public string LevelLabel => _title != null ? _title.text : null;
        public string LevelSubtitle => _subtitle != null && _subtitle.gameObject.activeSelf ? _subtitle.text : null;
        /// <summary>Centre-slot status line ("3 eşya kaldı"); null while hidden (Rotate shown, or nothing to say).</summary>
        public string StatusText => _status != null && _status.gameObject.activeInHierarchy ? _status.text : null;
        public RectTransform Header => _header;
        public RectTransform Dock => _dock;

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

        /// <param name="uiFont">UI face (Bricolage Grotesque SemiBold); null = built-in fallback.</param>
        /// <param name="displayFont">Display face (Bricolage Grotesque ExtraBold); null = uiFont.</param>
        public void Build(Font uiFont = null, Font displayFont = null)
        {
            if (_font != null)
                return;
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
            var circle = Own(PresentationKit.RoundedSprite(96, 48));
            Own(circle.texture);
            var undoIcon = Own(PresentationKit.UndoIcon(128));
            var restartIcon = Own(PresentationKit.RestartIcon(128));
            var rotateIcon = Own(PresentationKit.RotateIcon(128));
            var suitcaseIcon = Own(PresentationKit.SuitcaseIcon(96));
            foreach (var icon in new[] { undoIcon, restartIcon, rotateIcon, suitcaseIcon })
                Own(icon.texture);

            // Header: travel badge + level title / subtitle, top centre of the safe area.
            _header = PaperUi.Card(SafeArea, "Header", _rounded, new Vector2(0.5f, 1f), new Vector2(0f, -HeaderBottom + HeaderSize.y * 0.5f + 8f),
                HeaderSize, Color.white, 0.24f, 9f, PaperUi.Skin("luggage_label"));
            var backing = PaperUi.Image(_header, "Offset Label", PaperUi.Skin("luggage_label"),
                new Color(0.71f, 0.58f, 0.4f, 0.85f), HeaderSize);
            backing.anchoredPosition = new Vector2(8f, -10f);
            backing.SetSiblingIndex(1);
            var badge = PaperUi.Image(_header, "Travel Badge", PaperUi.Skin("control_seal", 20), Color.white, new Vector2(92f, 92f));
            badge.anchoredPosition = new Vector2(-HeaderSize.x * 0.5f + 76f, 0f);
            PaperUi.Image(badge, "Suitcase", suitcaseIcon, PaperUi.Teal, new Vector2(60f, 60f));
            _title = PaperUi.Label(_header, "", _display, 48, PaperUi.Ink, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(380f, 60f));
            _subtitle = PaperUi.Label(_header, "", _font, 32, PaperUi.Muted, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(380f, 44f));

            // Dock: secondary controls at thumb reach, contextual centre slot.
            _dock = PaperUi.Card(SafeArea, "Dock", _rounded, new Vector2(0.5f, 0f), new Vector2(0f, DockCenterY), DockSize, PaperUi.Cream);
            _dock.Find("Face").gameObject.SetActive(false);
            _dock.Find("Shadow").gameObject.SetActive(false);
            AddControl(PuzzleHudAction.Undo, "Geri Al", undoIcon, new Vector2(-DockSize.x * 0.5f + 98f, 0f));
            AddControl(PuzzleHudAction.Restart, "Baştan", restartIcon, new Vector2(DockSize.x * 0.5f - 98f, 0f));
            _status = PaperUi.Label(_dock, "", _font, 34, PaperUi.Muted, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(520f, 60f));
            var rotate = AddPill(_dock, PuzzleHudAction.Rotate, "Döndür", Vector2.zero, RotateSize, PaperUi.SoftMustard, PaperUi.MustardEdge, 5f, 42);
            var rotateGlyph = PaperUi.Image(rotate, "Icon", rotateIcon, PaperUi.Ink, new Vector2(64f, 64f));
            rotateGlyph.anchoredPosition = new Vector2(-92f, 0f);
            rotate.Find("Label").GetComponent<RectTransform>().anchoredPosition = new Vector2(26f, 2f);

            // Modifier intents (Fold / Compress levels only): medium pills above the dock's right end.
            AddPill(SafeArea, PuzzleHudAction.Fold, "Katla", new Vector2(-150f, DockCenterY + 190f), new Vector2(220f, 96f),
                PaperUi.SoftMustard, PaperUi.MustardEdge, 5f, 34, new Vector2(1f, 0f));
            AddPill(SafeArea, PuzzleHudAction.Compress, "Sıkıştır", new Vector2(-390f, DockCenterY + 190f), new Vector2(220f, 96f),
                PaperUi.SoftMustard, PaperUi.MustardEdge, 5f, 34, new Vector2(1f, 0f));
            SetModifierVisible(false, false);
            SetRotateVisible(false);

            // Existing Zip It confirmation (PACKED-SLICE owns its redesign): Sonraki on the hero tier, Tekrar secondary.
            _card = PaperUi.Card(SafeArea, "Packed Tag", _rounded, new Vector2(0.5f, 0f), new Vector2(0f, 390f), new Vector2(640f, 245f),
                Color.white, 0.26f, 10f, PaperUi.Skin("checklist"));
            _stamp = PaperUi.Label(_card, "Paketlendi!", _display, 64, PaperUi.Ink, TextAnchor.MiddleCenter,
                new Vector2(0f, 62f), new Vector2(590f, 82f)).rectTransform;
            AddPill(_card, PuzzleHudAction.Next, "Sonraki", new Vector2(140f, -58f), new Vector2(260f, 100f), PaperUi.Mustard,
                PaperUi.MustardUnder, 8f, 44);
            AddPill(_card, PuzzleHudAction.Restart, "Tekrar", new Vector2(-140f, -58f), new Vector2(260f, 100f), PaperUi.ButtonFill,
                PresentationKit.Hex(0xD9CDB8), 6f, 40, register: false);
            _card.gameObject.SetActive(false);
            var next = (RectTransform)_card.Find("Button Sonraki");
            PaperUi.Face(next).color = PresentationKit.Hex(0x289964);
            next.Find("Shadow").GetComponent<Image>().color = PresentationKit.Hex(0x14754C);
            next.GetComponentInChildren<Text>().color = Color.white;
        }

        /// <summary>Header text: "Seviye N" and an optional subtitle (authored level name).</summary>
        public void SetLevel(string title, string subtitle = null)
        {
            _title.text = title;
            var hasSubtitle = !string.IsNullOrEmpty(subtitle);
            _subtitle.text = hasSubtitle ? subtitle : "";
            _subtitle.gameObject.SetActive(hasSubtitle);
            var x = -HeaderSize.x * 0.5f + 150f + 190f;
            _title.rectTransform.anchoredPosition = new Vector2(x, hasSubtitle ? 18f : 0f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(x, -28f);
        }

        /// <summary>Centre-slot status shown while Rotate is not offered; null / empty hides it.</summary>
        public void SetStatus(string text)
        {
            if (_status.text != (text ?? ""))
                _status.text = text ?? "";
            _status.gameObject.SetActive(!string.IsNullOrEmpty(text) && !RotateVisible);
        }

        /// <summary>Capture only: draw the HUD through a camera so off-screen renders include it.</summary>
        public void RenderThrough(Camera camera)
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = camera != null ? RenderMode.ScreenSpaceCamera : RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
        }

        public void SetRotateVisible(bool visible)
        {
            var rotate = _buttons[PuzzleHudAction.Rotate].gameObject;
            if (rotate.activeSelf == visible)
                return;
            rotate.SetActive(visible);
            _status.gameObject.SetActive(!visible && !string.IsNullOrEmpty(_status.text));
        }

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
            var button = _buttons[PuzzleHudAction.Undo];
            button.GetComponent<CanvasGroup>().alpha = enabled ? 1f : 0.66f;
            button.Find("Shadow").GetComponent<Image>().color = enabled
                ? new Color(DropShadow.r, DropShadow.g, DropShadow.b, 0.2f)
                : new Color(DropShadow.r, DropShadow.g, DropShadow.b, 0.08f);
        }

        /// <summary>Pressed state: the hit button dips and springs back (visual only).</summary>
        public void Press(PuzzleHudAction action)
        {
            if (_pressed != null)
                _pressed.localScale = Vector3.one;
            _pressed = action == PuzzleHudAction.Next ? (RectTransform)_card.Find("Button Sonraki")
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
            var ease = MotionTokens.UiPressOutBack(t);
            _pressed.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, ease);
            if (t >= 1f)
            {
                _pressed.localScale = Vector3.one;
                _pressed = null;
            }
        }

        public void SetCompletionVisible(bool visible)
        {
            _card.gameObject.SetActive(visible);
            if (!visible)
            {
                SetStampProgress(0f);
                SetCompletionActionsVisible(false);
            }
        }

        public void SetStampProgress(float progress)
        {
            if (_stamp == null) return;
            StampProgress = Mathf.Clamp01(progress);
            var t = MotionTokens.LidSmoothStep(StampProgress);
            _stamp.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, t);
            _stamp.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-2f, 0f, t));
            _stamp.GetComponent<Text>().color = new Color(PaperUi.Ink.r, PaperUi.Ink.g, PaperUi.Ink.b, t);
        }

        public void SetCompletionActionsVisible(bool visible)
        {
            if (_card == null) return;
            _card.Find("Button Sonraki").gameObject.SetActive(visible);
            _card.Find("Button Tekrar").gameObject.SetActive(visible);
        }

        public void SetCompletionMode(bool active)
        {
            CompletionMode = active;
            _dock.gameObject.SetActive(!active);
            _header.gameObject.SetActive(!active);
            if (active)
            {
                SetRotateVisible(false);
                SetModifierVisible(false, false);
            }
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
                        return child.name == "Button Sonraki" ? PuzzleHudAction.Next : PuzzleHudAction.Restart;
                return PuzzleHudAction.None;
            }
            foreach (var pair in _buttons)
                if (pair.Value.gameObject.activeInHierarchy && !pair.Value.IsChildOf(_card)
                    && RectTransformUtility.RectangleContainsScreenPoint(pair.Value, screenPosition, null))
                    return pair.Key;
            return PuzzleHudAction.None;
        }

        /// <summary>Screen-space centre of a visible button (tests and device scripts).</summary>
        public Vector2 ButtonCenter(PuzzleHudAction action)
        {
            var rect = action == PuzzleHudAction.Next ? (RectTransform)_card.Find("Button Sonraki") : _buttons[action];
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        // Secondary dock control: rounded paper square, ink glyph, small caption. The whole square is the hit target.
        private void AddControl(PuzzleHudAction action, string caption, Sprite icon, Vector2 position)
        {
            var button = PaperUi.Card(_dock, "Button " + caption, _rounded, new Vector2(0.5f, 0.5f), position,
                new Vector2(ControlSize, ControlSize), Color.white, 0.2f, 6f, PaperUi.Skin("control_seal", 20));
            button.gameObject.AddComponent<CanvasGroup>();
            foreach (var part in new[] { "Shadow", "Face" })
            {
                var plate = (RectTransform)button.Find(part);
                plate.sizeDelta = new Vector2(116f, 116f);
                plate.anchoredPosition += new Vector2(0f, 16f);
            }
            PaperUi.Image(button, "Icon", icon, PaperUi.Ink, new Vector2(62f, 62f)).anchoredPosition = new Vector2(0f, 25f);
            PaperUi.Label(button, caption, _font, 24, PaperUi.Ink, TextAnchor.MiddleCenter, new Vector2(0f, -51f), new Vector2(ControlSize, 32f));
            _buttons[action] = button;
        }

        // Pill action: face colour over a darker under-edge (the card shadow, made opaque) for a restrained bevel.
        private RectTransform AddPill(Transform parent, PuzzleHudAction action, string text, Vector2 position, Vector2 size, Color face,
            Color edge, float drop, int fontSize, Vector2? anchor = null, bool register = true)
        {
            var skin = PaperUi.Skin(action == PuzzleHudAction.Next ? "ticket"
                : action == PuzzleHudAction.Restart && !register ? "checklist_tab" : "action_tag");
            var pill = PaperUi.Card(parent, "Button " + text, _rounded, anchor ?? new Vector2(0.5f, 0.5f), position, size,
                skin != null ? Color.white : face, 0.32f, drop, skin);
            var under = pill.Find("Shadow").GetComponent<Image>();
            under.color = new Color(edge.r, edge.g, edge.b, 0.72f);
            under.rectTransform.sizeDelta = size;
            pill.gameObject.AddComponent<CanvasGroup>();
            PaperUi.Label(pill, text, _display, fontSize, PaperUi.Ink, TextAnchor.MiddleCenter, new Vector2(0f, 2f), size);
            if (register)
                _buttons[action] = pill;
            return pill;
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
    }
}
