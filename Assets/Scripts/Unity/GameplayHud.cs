using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ZipTrip.Unity
{
    public enum HudActionKind { None, Rotate, Fold, Replay, Next, DevToggle, DevLevel, DevReset, DevOverlay, Dismiss }

    public readonly struct HudAction
    {
        public HudActionKind Kind { get; }
        public string Value { get; }

        public HudAction(HudActionKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }
    }

    public struct ChipSpec
    {
        public string ItemId;
        public bool Fold;
        public bool Folded;
        public Transform Anchor;
    }

    // Visual-only overlay. It never changes gameplay state; the controller maps hits to commands
    // through the single PointerInteractor flow (no EventSystem).
    public sealed class GameplayHud : MonoBehaviour
    {
        private const float PressScale = 0.9f;
        private const float ReleaseSeconds = 0.16f;
        private const float CompletionDelay = 0.22f;
        private const float BackdropSeconds = 0.25f;
        private const float CardSeconds = 0.32f;
        private const float BackdropAlpha = 0.5f;

        private sealed class Chip
        {
            public string Key;
            public ChipSpec Spec;
            public RectTransform Rect;
            public Image Background;
            public Image Border;
            public Text Label;
        }

        private sealed class Button
        {
            public HudAction Action;
            public RectTransform Rect;
        }

        private readonly List<Chip> _chips = new List<Chip>();
        private readonly List<Button> _drawerButtons = new List<Button>();
        private readonly List<Button> _completionButtons = new List<Button>();
        private Camera _camera;
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _safe;
        private RectTransform _chipLayer;
        private Text _levelLabel;
        private RectTransform _backdrop;
        private Image _backdropImage;
        private RectTransform _card;
        private CanvasGroup _cardGroup;
        private Text _cardSubtitle;
        private RectTransform _devHandle;
        private RectTransform _drawer;
        private RectTransform _pressed;
        private RectTransform _releasing;
        private float _releaseTime;
        private float _completionTime;
        private Vector2Int _screen;
        private Rect _safeArea;
        private Sprite _rounded;
        private Sprite _circle;
        private Sprite _rotateIcon;
        private Font _font;
        private string _currentLevel;

        public bool ChipsVisible { get; set; } = true;
        public bool CompletionVisible => _backdrop != null && _backdrop.gameObject.activeSelf;
        public bool DrawerOpen => _drawer != null && _drawer.gameObject.activeSelf;
        public bool HasDevDrawer => _devHandle != null;
        public int ChipCount => _chips.Count;
        public RectTransform CompletionCard => _card;

        public void Initialize(Camera camera)
        {
            if (_camera != null)
                throw new InvalidOperationException("HUD already initialized.");
            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _rounded = PresentationKit.RoundedSprite(128, 40);
            _circle = PresentationKit.RoundedSprite(128, 64);
            _rotateIcon = PresentationKit.RotateIcon(128);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            var root = (RectTransform)transform;

            _chipLayer = Layer("Chips", root);
            _chipLayer.anchorMin = _chipLayer.anchorMax = Vector2.zero;
            _chipLayer.pivot = Vector2.zero;
            _safe = Layer("Safe area", root);

            var label = Panel("Level label", _safe, _rounded, PresentationKit.Hex(0xF7F4EE, 0.92f), 0.5f);
            Anchor(label, new Vector2(0.5f, 1f), new Vector2(0f, -GameplayMetrics.HudHeight * 0.5f),
                new Vector2(220f, 64f));
            _levelLabel = Label(label, "", 34, PresentationKit.DeepBlueGreen, FontStyle.Bold);

            BuildCompletion(root);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            BuildDevDrawer();
#endif
            ApplyScreen();
        }

        public void SetLevel(string levelId)
        {
            _currentLevel = levelId;
            _levelLabel.text = LevelTitle(levelId);
            foreach (var button in _drawerButtons)
                if (button.Action.Kind == HudActionKind.DevLevel)
                    StyleButton(button.Rect, button.Action.Value == levelId);
        }

        public static string LevelTitle(string levelId) =>
            levelId != null && levelId.Length > 1 && levelId[0] == 'L' ? "Level " + levelId.Substring(1) : levelId;

        public void SetChips(IReadOnlyList<ChipSpec> specs)
        {
            // Reconcile by key so press feedback survives the authoritative rebuild after a command.
            for (var i = _chips.Count - 1; i >= 0; i--)
            {
                var keep = false;
                for (var j = 0; j < specs.Count; j++)
                    keep |= _chips[i].Key == Key(specs[j]);
                if (keep)
                    continue;
                if (_pressed == _chips[i].Rect) _pressed = null;
                if (_releasing == _chips[i].Rect) _releasing = null;
                Destroy(_chips[i].Rect.gameObject);
                _chips.RemoveAt(i);
            }
            for (var j = 0; j < specs.Count; j++)
            {
                var chip = _chips.Find(x => x.Key == Key(specs[j])) ?? CreateChip(specs[j]);
                chip.Spec = specs[j];
                if (specs[j].Fold)
                    StyleFold(chip);
            }
            PositionChips();
        }

        public bool TryHit(Vector2 screen, out HudAction action)
        {
            action = default;
            if (DrawerOpen)
            {
                foreach (var button in _drawerButtons)
                    if (Contains(button.Rect, screen, 0f))
                    {
                        action = button.Action;
                        return true;
                    }
                action = new HudAction(HudActionKind.Dismiss, null);
                return true;
            }
            if (_devHandle != null && Contains(_devHandle, screen, 12f))
            {
                action = new HudAction(HudActionKind.DevToggle, null);
                return true;
            }
            if (CompletionVisible)
            {
                foreach (var button in _completionButtons)
                    if (button.Rect.gameObject.activeSelf && Contains(button.Rect, screen, 0f))
                    {
                        action = button.Action;
                        return true;
                    }
                return false;
            }
            if (!ChipsVisible)
                return false;
            foreach (var chip in _chips)
                if (ChipHitRect(chip).Contains(screen))
                {
                    action = new HudAction(chip.Spec.Fold ? HudActionKind.Fold : HudActionKind.Rotate,
                        chip.Spec.ItemId);
                    return true;
                }
            return false;
        }

        // Screen-space hit rectangle of an item chip (visual size plus mobile padding).
        public Rect ChipHitRect(string itemId, bool fold)
        {
            foreach (var chip in _chips)
                if (chip.Spec.ItemId == itemId && chip.Spec.Fold == fold)
                    return ChipHitRect(chip);
            throw new ArgumentException("No chip for " + itemId);
        }

        private Rect ChipHitRect(Chip chip)
        {
            var center = (Vector2)_camera.WorldToScreenPoint(chip.Spec.Anchor.position);
            var dp = _scaler.scaleFactor;
            var size = new Vector2(chip.Spec.Fold ? GameplayMetrics.FoldChipWidth : GameplayMetrics.RotateChipWidth,
                GameplayMetrics.ChipHeight) + Vector2.one * (2f * GameplayMetrics.ChipHitPadding);
            return new Rect(center - size * dp * 0.5f, size * dp);
        }

        public void Press(HudAction action)
        {
            _pressed = FindVisual(action);
            if (_pressed != null)
                _pressed.localScale = Vector3.one * PressScale;
        }

        public void ReleasePress()
        {
            if (_pressed == null)
                return;
            _releasing = _pressed;
            _releaseTime = 0f;
            _pressed = null;
        }

        public void ToggleDrawer()
        {
            if (_drawer != null)
                _drawer.gameObject.SetActive(!_drawer.gameObject.activeSelf);
        }

        public void CloseDrawer()
        {
            if (_drawer != null)
                _drawer.gameObject.SetActive(false);
        }

        public void ShowCompletion(string levelId, bool showNext)
        {
            if (CompletionVisible)
                return;
            _cardSubtitle.text = LevelTitle(levelId);
            foreach (var button in _completionButtons)
                if (button.Action.Kind == HudActionKind.Next)
                    button.Rect.gameObject.SetActive(showNext);
            LayoutCompletionButtons(showNext);
            _completionTime = 0f;
            _backdrop.gameObject.SetActive(true);
            ApplyCompletion();
        }

        public void HideCompletion()
        {
            if (_backdrop != null)
                _backdrop.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            ApplyScreen();
            PositionChips();
            _chipLayer.gameObject.SetActive(ChipsVisible && !CompletionVisible);
            if (_releasing != null)
            {
                _releaseTime += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(_releaseTime / ReleaseSeconds);
                // Spring back with a small overshoot.
                _releasing.localScale = Vector3.one * Mathf.LerpUnclamped(PressScale, 1f, EaseOutBack(t));
                if (t >= 1f)
                    _releasing = null;
            }
            if (CompletionVisible)
            {
                _completionTime += Time.unscaledDeltaTime;
                ApplyCompletion();
            }
        }

        private void ApplyCompletion()
        {
            var b = Mathf.Clamp01((_completionTime - CompletionDelay) / BackdropSeconds);
            var color = PresentationKit.DeepBlueGreen;
            color.a = BackdropAlpha * EaseOut(b);
            _backdropImage.color = color;
            var c = Mathf.Clamp01((_completionTime - CompletionDelay - 0.08f) / CardSeconds);
            _cardGroup.alpha = EaseOut(c);
            _card.localScale = Vector3.one * Mathf.LerpUnclamped(0.9f, 1f, EaseOutBack(c));
        }

        private void ApplyScreen()
        {
            var screen = new Vector2Int(_camera.pixelWidth, _camera.pixelHeight);
            var safe = Screen.safeArea;
            if (screen == _screen && safe == _safeArea)
                return;
            _screen = screen;
            _safeArea = safe;
            _scaler.scaleFactor = GameplayMetrics.DpScale(screen.x, screen.y);
            _safe.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            _safe.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            _safe.offsetMin = _safe.offsetMax = Vector2.zero;
        }

        private void PositionChips()
        {
            var factor = _scaler.scaleFactor;
            foreach (var chip in _chips)
                if (chip.Spec.Anchor != null)
                    chip.Rect.anchoredPosition =
                        (Vector2)_camera.WorldToScreenPoint(chip.Spec.Anchor.position) / factor;
        }

        private RectTransform FindVisual(HudAction action)
        {
            if (action.Kind == HudActionKind.Rotate || action.Kind == HudActionKind.Fold)
                foreach (var chip in _chips)
                    if (chip.Spec.ItemId == action.Value && chip.Spec.Fold == (action.Kind == HudActionKind.Fold))
                        return chip.Rect;
            if (action.Kind == HudActionKind.DevToggle)
                return _devHandle;
            foreach (var button in _completionButtons)
                if (button.Action.Kind == action.Kind)
                    return button.Rect;
            foreach (var button in _drawerButtons)
                if (button.Action.Kind == action.Kind && button.Action.Value == action.Value)
                    return button.Rect;
            return null;
        }

        private static string Key(ChipSpec spec) => (spec.Fold ? "Fold:" : "Rotate:") + spec.ItemId;

        private Chip CreateChip(ChipSpec spec)
        {
            var width = spec.Fold ? GameplayMetrics.FoldChipWidth : GameplayMetrics.RotateChipWidth;
            var rect = Layer("Chip " + Key(spec), _chipLayer);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.sizeDelta = new Vector2(width, GameplayMetrics.ChipHeight);
            var sprite = spec.Fold ? _rounded : _circle;
            var shadow = Panel("Shadow", rect, sprite, PresentationKit.Hex(0x31515D, 0.2f), 2f);
            Stretch(shadow, new Vector2(0f, -5f));
            var chip = new Chip { Key = Key(spec), Spec = spec, Rect = rect };
            var border = Panel("Border", rect, sprite, PresentationKit.Teal, 2f);
            Stretch(border, Vector2.zero);
            chip.Border = border.GetComponent<Image>();
            var face = Panel("Face", rect, sprite, PresentationKit.Paper, 2f);
            Stretch(face, Vector2.zero);
            chip.Background = face.GetComponent<Image>();
            if (spec.Fold)
                chip.Label = Label(face, "", 32, PresentationKit.DeepBlueGreen, FontStyle.Bold);
            else
            {
                chip.Border.color = PresentationKit.Paper;
                var icon = Panel("Icon", face, _rotateIcon, PresentationKit.DeepBlueGreen, 1f);
                Anchor(icon, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
                icon.GetComponent<Image>().type = Image.Type.Simple;
            }
            _chips.Add(chip);
            return chip;
        }

        private static void StyleFold(Chip chip)
        {
            // Fold = mustard action; Open = outlined, so the two states read differently without color alone.
            chip.Label.text = chip.Spec.Folded ? "Open" : "Fold";
            chip.Background.color = chip.Spec.Folded ? PresentationKit.Paper : PresentationKit.Mustard;
            chip.Label.color = chip.Spec.Folded ? PresentationKit.Teal : PresentationKit.DeepBlueGreen;
            var inset = chip.Spec.Folded ? 4f : 0f;
            chip.Background.rectTransform.offsetMin = new Vector2(inset, inset);
            chip.Background.rectTransform.offsetMax = new Vector2(-inset, -inset);
        }

        private void BuildCompletion(RectTransform root)
        {
            _backdrop = Layer("Completion", root);
            _backdropImage = _backdrop.gameObject.AddComponent<Image>();
            _backdropImage.raycastTarget = false;
            _card = Layer("Packed card", _backdrop);
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(760f, 500f);
            _cardGroup = _card.gameObject.AddComponent<CanvasGroup>();
            var shadow = Panel("Shadow", _card, _rounded, PresentationKit.Hex(0x1F363E, 0.25f), 1f);
            Stretch(shadow, new Vector2(0f, -10f));
            var face = Panel("Face", _card, _rounded, PresentationKit.Paper, 1f);
            Stretch(face, Vector2.zero);
            var accent = Panel("Zipper accent", face, _circle, PresentationKit.Mustard, 4f);
            Anchor(accent, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(120f, 12f));
            var title = Label(face, "PACKED", 104, PresentationKit.DeepBlueGreen, FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(700f, 130f));
            _cardSubtitle = Label(face, "", 38, PresentationKit.Teal, FontStyle.Bold);
            Anchor(_cardSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(600f, 56f));
            _completionButtons.Add(new Button
            {
                Action = new HudAction(HudActionKind.Replay, null),
                Rect = CardButton(face, "Replay")
            });
            _completionButtons.Add(new Button
            {
                Action = new HudAction(HudActionKind.Next, null),
                Rect = CardButton(face, "Next level")
            });
            _backdrop.gameObject.SetActive(false);
        }

        private RectTransform CardButton(RectTransform parent, string text)
        {
            var button = Panel("Button " + text, parent, _rounded, PresentationKit.Teal, 1.4f);
            button.anchorMin = button.anchorMax = new Vector2(0.5f, 0f);
            button.sizeDelta = new Vector2(300f, 104f);
            var face = Panel("Face", button, _rounded, PresentationKit.Teal, 1.4f);
            Stretch(face, Vector2.zero);
            Label(face, text, 38, Color.white, FontStyle.Bold);
            return button;
        }

        private void LayoutCompletionButtons(bool showNext)
        {
            var replay = _completionButtons[0].Rect;
            var next = _completionButtons[1].Rect;
            replay.anchoredPosition = new Vector2(showNext ? -162f : 0f, 104f);
            next.anchoredPosition = new Vector2(162f, 104f);
            StyleButton(replay, !showNext);
            StyleButton(next, true);
        }

        // Filled teal (primary/selected) vs outlined paper (secondary).
        private static void StyleButton(RectTransform button, bool filled)
        {
            var face = (RectTransform)button.Find("Face");
            face.GetComponent<Image>().color = filled ? PresentationKit.Teal : PresentationKit.Paper;
            face.GetComponentInChildren<Text>().color = filled ? Color.white : PresentationKit.Teal;
            face.offsetMin = Vector2.one * (filled ? 0f : 4f);
            face.offsetMax = -Vector2.one * (filled ? 0f : 4f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void BuildDevDrawer()
        {
            _devHandle = Panel("Dev drawer handle", _safe, _circle, PresentationKit.Hex(0xF7F4EE, 0.7f), 2f);
            Anchor(_devHandle, new Vector2(1f, 1f), new Vector2(-60f, -GameplayMetrics.HudHeight * 0.5f),
                new Vector2(64f, 64f));
            for (var i = -1; i <= 1; i++)
            {
                var dot = Panel("Dot", _devHandle, _circle, PresentationKit.Hex(0x31515D, 0.55f), 2f);
                Anchor(dot, new Vector2(0.5f, 0.5f), new Vector2(i * 14f, 0f), new Vector2(9f, 9f));
            }

            _drawer = Panel("Dev drawer", _safe, _rounded, PresentationKit.Paper, 1.6f);
            Anchor(_drawer, new Vector2(1f, 1f), new Vector2(-232f, -GameplayMetrics.HudHeight - 150f),
                new Vector2(420f, 280f));
            var title = Label(_drawer, "DEVELOPMENT", 24, PresentationKit.Hex(0x31515D, 0.6f), FontStyle.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(380f, 36f));
            for (var i = 0; i < PhaseALevels.Ids.Count; i++)
            {
                var id = PhaseALevels.Ids[i];
                AddDrawerButton(id, new HudAction(HudActionKind.DevLevel, id),
                    new Vector2(-141f + i * 94f, 34f), new Vector2(84f, 84f));
            }
            AddDrawerButton("Reset", new HudAction(HudActionKind.DevReset, null),
                new Vector2(-94f, -76f), new Vector2(178f, 84f));
            AddDrawerButton("Debug", new HudAction(HudActionKind.DevOverlay, null),
                new Vector2(94f, -76f), new Vector2(178f, 84f));
            _drawer.gameObject.SetActive(false);
        }

        private void AddDrawerButton(string text, HudAction action, Vector2 position, Vector2 size)
        {
            var button = Panel("Button " + text, _drawer, _rounded, PresentationKit.Teal, 2.4f);
            Anchor(button, new Vector2(0.5f, 0.5f), position, size);
            var face = Panel("Face", button, _rounded, PresentationKit.Teal, 2.4f);
            Stretch(face, Vector2.zero);
            Label(face, text, 30, Color.white, FontStyle.Bold);
            StyleButton(button, action.Kind == HudActionKind.DevLevel && action.Value == _currentLevel);
            _drawerButtons.Add(new Button { Action = action, Rect = button });
        }
#endif

        // Overlay canvas world corners are screen pixels.
        private bool Contains(RectTransform rect, Vector2 screen, float paddingDp)
        {
            if (!rect.gameObject.activeInHierarchy)
                return false;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var pad = paddingDp * _scaler.scaleFactor;
            return screen.x >= corners[0].x - pad && screen.x <= corners[2].x + pad &&
                   screen.y >= corners[0].y - pad && screen.y <= corners[2].y + pad;
        }

        private static RectTransform Layer(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Sprite sprite, Color color,
            float pixelsPerUnitMultiplier)
        {
            var rect = Layer(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = pixelsPerUnitMultiplier;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private Text Label(Transform parent, string text, int size, Color color, FontStyle style)
        {
            var rect = Layer("Label", parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offset;
            rect.offsetMax = offset;
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);

        private static float EaseOutBack(float t)
        {
            const float c = 1.4f;
            return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
        }
    }
}
