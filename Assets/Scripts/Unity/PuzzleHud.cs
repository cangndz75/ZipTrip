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
    // subtitle), an authored packing dock with its own title/count, and grouped Geri Al / Baştan / contextual Döndür.
    // The green Sonraki hero exists only after the ZIP-02 completion sequence.
    public sealed class PuzzleHud : MonoBehaviour
    {
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float TopBand = 430f;
        public const float BottomBand = 170f;
        /// <summary>Header card bottom, reference px below the safe-area top (the objective note starts below it).</summary>
        public const float HeaderBottom = 180f;
        public static readonly Vector2 HeaderSize = new Vector2(720f, 156f);
        public static readonly Vector2 DockSize = new Vector2(1016f, 820f);
        public const float DockCenterY = 24f;
        public static readonly Vector2 RotateSize = new Vector2(228f, 104f);
        public static readonly Vector2 ControlSize = new Vector2(228f, 104f);

        private readonly Dictionary<PuzzleHudAction, RectTransform> _buttons = new Dictionary<PuzzleHudAction, RectTransform>();
        private Font _font;
        private Font _display;
        private Text _title;
        private Text _subtitle;
        private RectTransform _header;
        private RectTransform _dock;
        private Text _status;
        private Text _stagingTitle;
        private RectTransform _stagingTitleRow;
        private RectTransform _utilityBlock;
        private readonly Dictionary<string, Text> _stagingLabels = new Dictionary<string, Text>();
        private readonly HashSet<string> _liveStaging = new HashSet<string>();
        private readonly List<string> _staleStaging = new List<string>();
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
        public int StagingItemCardCount => _stagingLabels.Count;
        public string LevelLabel => _title != null ? _title.text : null;
        public string LevelSubtitle => _subtitle != null && _subtitle.gameObject.activeSelf ? _subtitle.text : null;
        /// <summary>Staging title-row count ("3 eşya kaldı"), independent of the contextual Rotate utility.</summary>
        public string StatusText => _status != null && _status.gameObject.activeInHierarchy ? _status.text : null;
        public RectTransform Header => _header;
        public RectTransform Dock => _utilityBlock;

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
        public static float TopFraction(float pixelWidth, float pixelHeight)
        {
            var referenceHeight = pixelHeight * ReferenceWidth / Mathf.Max(1f, pixelWidth);
            var band = Mathf.Lerp(310f, TopBand, Mathf.InverseLerp(1920f, 2340f, referenceHeight));
            return band * pixelWidth / ReferenceWidth / Mathf.Max(1f, pixelHeight);
        }

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
                HeaderSize, Color.white, 0.36f, 12f, PaperUi.Skin("travel_frame", 0));
            var badge = PaperUi.Image(_header, "Travel Badge", PaperUi.Skin("control_seal", 20), Color.white, new Vector2(92f, 92f));
            badge.anchoredPosition = new Vector2(-HeaderSize.x * 0.5f + 76f, 0f);
            PaperUi.Image(badge, "Suitcase", suitcaseIcon, PaperUi.Teal, new Vector2(60f, 60f));
            _title = PaperUi.Label(_header, "", _display, 64, PaperUi.Ink, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(450f, 74f));
            _subtitle = PaperUi.Label(_header, "", _font, 34, PaperUi.Teal, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(450f, 46f));

            // Cohesive staging dock overlay. The real item art and physical cards stay in the world; this layer supplies
            // the authored title/count/name treatment and keeps all utility actions in one compact, subordinate block.
            _dock = PaperUi.Card(SafeArea, "Dock", _rounded, new Vector2(0.5f, 0f), new Vector2(0f, DockCenterY), DockSize, PaperUi.Cream);
            _dock.Find("Face").gameObject.SetActive(false);
            _dock.Find("Shadow").gameObject.SetActive(false);
            _stagingTitleRow = PaperUi.Card(_dock, "Staging Title Row", _rounded, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(860f, 96f), Color.white, 0.22f, 6f, PaperUi.Skin("checklist", 24));
            _stagingTitle = PaperUi.Label(_stagingTitleRow, "Yerleştirilecek Eşyalar", _display, 40, PaperUi.Ink,
                TextAnchor.MiddleLeft, new Vector2(-55f, 0f), new Vector2(540f, 58f));
            _status = PaperUi.Label(_stagingTitleRow, "", _display, 30, PaperUi.Teal, TextAnchor.MiddleRight,
                new Vector2(270f, 0f), new Vector2(180f, 58f));

            _utilityBlock = PaperUi.Card(_dock, "Secondary Utilities", _rounded, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(800f, 140f), Color.white, 0.32f, 10f, PaperUi.Skin("checklist", 24));
            AddControl(PuzzleHudAction.Undo, "Geri Al", undoIcon, new Vector2(-250f, 0f));
            AddControl(PuzzleHudAction.Restart, "Baştan", restartIcon, Vector2.zero);
            var rotate = AddPill(_utilityBlock, PuzzleHudAction.Rotate, "Döndür", new Vector2(250f, 0f), RotateSize,
                PaperUi.Cream, PresentationKit.Hex(0xD9CDB8), 5f, 30);
            var rotateGlyph = PaperUi.Image(rotate, "Icon", rotateIcon, PaperUi.Teal, new Vector2(36f, 36f));
            rotateGlyph.anchoredPosition = new Vector2(-72f, 0f);
            rotate.Find("Label").GetComponent<RectTransform>().anchoredPosition = new Vector2(20f, 1f);

            // Modifier intents (Fold / Compress levels only): medium pills above the dock's right end.
            AddPill(SafeArea, PuzzleHudAction.Fold, "Katla", new Vector2(-150f, DockCenterY + 190f), new Vector2(220f, 96f),
                PaperUi.SoftMustard, PaperUi.MustardEdge, 5f, 34, new Vector2(1f, 0f));
            AddPill(SafeArea, PuzzleHudAction.Compress, "Sıkıştır", new Vector2(-390f, DockCenterY + 190f), new Vector2(220f, 96f),
                PaperUi.SoftMustard, PaperUi.MustardEdge, 5f, 34, new Vector2(1f, 0f));
            SetModifierVisible(false, false);
            SetRotateVisible(false);

            // Existing Zip It confirmation (PACKED-SLICE owns its redesign): Sonraki on the hero tier, Tekrar secondary.
            _card = PaperUi.Card(SafeArea, "Packed Tag", _rounded, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(680f, 410f),
                Color.white, 0.36f, 14f, PaperUi.Skin("travel_frame", 0));
            _stamp = PaperUi.Label(_card, "Paketlendi!", _display, 64, PaperUi.Ink, TextAnchor.MiddleCenter,
                new Vector2(0f, 142f), new Vector2(620f, 82f)).rectTransform;
            AddPill(_card, PuzzleHudAction.Next, "Sonraki  ›", new Vector2(0f, 24f), new Vector2(560f, 150f),
                PresentationKit.Hex(0x3DBA3F), PresentationKit.Hex(0x24892B), 8f, 48);
            AddPill(_card, PuzzleHudAction.Restart, "Tekrar", new Vector2(0f, -116f), new Vector2(560f, 96f), PaperUi.Cream,
                PresentationKit.Hex(0xD9CDB8), 5f, 38, register: false);
            _card.gameObject.SetActive(false);
            var next = (RectTransform)_card.Find("Button Sonraki  ›");
            next.name = "Button Sonraki";
            PaperUi.Face(next).color = Color.white;
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
            var x = -HeaderSize.x * 0.5f + 154f + 225f;
            _title.rectTransform.anchoredPosition = new Vector2(x, hasSubtitle ? 18f : 0f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(x, -34f);
        }

        /// <summary>Remaining count in the staging title row. Independent from the contextual Rotate utility.</summary>
        public void SetStatus(string text)
        {
            if (_status.text != (text ?? ""))
                _status.text = text ?? "";
            _status.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>
        /// Aligns presentation labels and the compact utility block with the real world-space tray cards. No slot or
        /// gameplay state is created here; the card count comes directly from the tray presenter.
        /// </summary>
        public void LayoutStagingDock(PuzzleTrayPresenter tray, Camera camera, Rect shell)
        {
            if (_dock == null || tray == null || camera == null || CompletionMode)
                return;
            var visible = tray.ItemViews.Count > 0;
            _stagingTitleRow.gameObject.SetActive(visible);
            var live = _liveStaging;
            live.Clear();
            var eventCamera = GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay
                ? null : GetComponent<Canvas>().worldCamera;
            if (visible)
            {
                var surfaceY = 0f;
                foreach (var view in tray.ItemViews.Values)
                {
                    surfaceY = tray.transform.position.y;
                    break;
                }
                var shellTop = camera.WorldToScreenPoint(new Vector3(shell.center.x, surfaceY, shell.yMax));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_dock, shellTop, eventCamera, out var titleLocal);
                _stagingTitleRow.anchoredPosition = titleLocal + new Vector2(0f, -30f);

                foreach (var pair in tray.ItemViews)
                {
                    live.Add(pair.Key);
                    var view = pair.Value;
                    if (!_stagingLabels.TryGetValue(pair.Key, out var label))
                    {
                        var plate = PaperUi.Card(_dock, "Item Name " + pair.Key, _rounded, new Vector2(0.5f, 0.5f),
                            Vector2.zero, new Vector2(242f, 64f), Color.white, 0.12f, 3f, PaperUi.Skin("checklist", 24));
                        label = PaperUi.Label(plate, PuzzleRuleText.StagingName(view.DefinitionId), _display, 30, PaperUi.Ink,
                            TextAnchor.MiddleCenter, Vector2.zero, new Vector2(230f, 60f));
                        label.horizontalOverflow = HorizontalWrapMode.Wrap;
                        _stagingLabels.Add(pair.Key, label);
                    }
                    var cells = view.Footprint.OccupiedCells;
                    var width = 0;
                    var depth = 0;
                    for (var i = 0; i < cells.Count; i++)
                    {
                        width = Mathf.Max(width, cells[i].X + 1);
                        depth = Mathf.Max(depth, cells[i].Y + 1);
                    }
                    var world = new Vector3(tray.RestPosition(pair.Key).x + width * tray.Scale * 0.5f,
                        surfaceY + 0.08f, shell.yMin + 0.35f);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_dock,
                        camera.WorldToScreenPoint(world), eventCamera, out var local);
                    label.transform.parent.GetComponent<RectTransform>().anchoredPosition = local;
                    var cardWidthPx = Mathf.Max(2, width) * tray.Scale * camera.pixelHeight
                        / (2f * camera.orthographicSize) * ReferenceWidth / camera.pixelWidth;
                    PaperUi.Resize(label.transform.parent.GetComponent<RectTransform>(), new Vector2(cardWidthPx, 70f));
                    label.rectTransform.sizeDelta = new Vector2(cardWidthPx - 16f, 64f);
                    label.fontSize = cardWidthPx < 190f ? 26 : 30;
                    label.text = PuzzleRuleText.StagingName(view.DefinitionId);
                }

                var shellRight = camera.WorldToScreenPoint(new Vector3(shell.center.x, surfaceY, shell.yMin));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_dock, shellRight, eventCamera, out var utilitiesLocal);
                utilitiesLocal.x = 0f;
                utilitiesLocal.y = Mathf.Max(utilitiesLocal.y - 70f, 80f - DockCenterY);
                _utilityBlock.anchoredPosition = utilitiesLocal;
            }
            var stale = _staleStaging;
            stale.Clear();
            foreach (var id in _stagingLabels.Keys)
                if (!live.Contains(id))
                    stale.Add(id);
            foreach (var id in stale)
            {
                Destroy(_stagingLabels[id].transform.parent.gameObject);
                _stagingLabels.Remove(id);
            }
        }

        /// <summary>
        /// Capture only: draw the HUD into <paramref name="camera"/>'s target so off-screen renders include it (via
        /// CaptureUi; null restores the overlay). Measure HUD rects with <see cref="CaptureCamera"/>.
        /// </summary>
        public void RenderThrough(Camera camera) => CaptureCamera = CaptureUi.Attach(GetComponent<Canvas>(), camera, 1f);

        /// <summary>The camera drawing the HUD while capturing (null in the overlay used by the game).</summary>
        public Camera CaptureCamera { get; private set; }

        public void SetRotateVisible(bool visible)
        {
            var rotate = _buttons[PuzzleHudAction.Rotate].gameObject;
            if (rotate.activeSelf == visible)
                return;
            rotate.SetActive(visible);
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
            var canvas = GetComponent<Canvas>();
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (CompletionMode)
            {
                foreach (Transform child in _card)
                    if (child.name.StartsWith("Button ") && child.gameObject.activeSelf
                        && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)child, screenPosition, eventCamera))
                        return child.name == "Button Sonraki" ? PuzzleHudAction.Next : PuzzleHudAction.Restart;
                return PuzzleHudAction.None;
            }
            foreach (var pair in _buttons)
                if (pair.Value.gameObject.activeInHierarchy && !pair.Value.IsChildOf(_card)
                    && RectTransformUtility.RectangleContainsScreenPoint(pair.Value, screenPosition, eventCamera))
                    return pair.Key;
            return PuzzleHudAction.None;
        }

        /// <summary>Screen-space centre of a visible button (tests and device scripts).</summary>
        public Vector2 ButtonCenter(PuzzleHudAction action)
        {
            var rect = action == PuzzleHudAction.Next ? (RectTransform)_card.Find("Button Sonraki") : _buttons[action];
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = GetComponent<Canvas>();
            return RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                (corners[0] + corners[2]) * 0.5f);
        }

        // Compact secondary utility: cream kit art, small teal glyph and restrained type.
        private void AddControl(PuzzleHudAction action, string caption, Sprite icon, Vector2 position)
        {
            var button = PaperUi.Card(_utilityBlock, "Button " + caption, _rounded, new Vector2(0.5f, 0.5f), position,
                ControlSize, Color.white, 0.22f, 5f, PaperUi.Skin("luggage_label", 32));
            button.gameObject.AddComponent<CanvasGroup>();
            PaperUi.Image(button, "Icon", icon, PaperUi.Teal, new Vector2(36f, 36f)).anchoredPosition = new Vector2(-72f, 0f);
            PaperUi.Label(button, caption, _display, 30, PaperUi.Ink, TextAnchor.MiddleCenter, new Vector2(20f, 1f), new Vector2(146f, 48f));
            _buttons[action] = button;
        }

        // Pill action: face colour over a darker under-edge (the card shadow, made opaque) for a restrained bevel.
        private RectTransform AddPill(Transform parent, PuzzleHudAction action, string text, Vector2 position, Vector2 size, Color face,
            Color edge, float drop, int fontSize, Vector2? anchor = null, bool register = true)
        {
            var skin = action == PuzzleHudAction.Next ? PaperUi.Skin("next_hero", 0) : PaperUi.Skin("luggage_label");
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
