using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using ZipTrip.Domain;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    public enum RuleTagStatus
    {
        Inactive = 0,
        Satisfied = 1,
        Violated = 2
    }

    // ZT-042 live rule presentation. Shows only the level's authored rules as compact paper travel tags (kind icon,
    // short label, status badge) under the level pass, and world cues inside the suitcase. Every status comes from the
    // Domain: committed status from PuzzleSession.CurrentCompletion.Rules (RuleEvaluator), drag-time hints from
    // RuleEvaluator on the drag controller's Domain preview state. Nothing here re-derives zones, adjacency or access.
    // Interaction context: idle shows the strip plus cues only for currently violated forbidden-adjacency / access rules;
    // a drag shows only the overlays relevant to the dragged item (target zone, adjacency targets, forbidden partners).
    public sealed class PuzzleRulesPresenter : MonoBehaviour
    {
        public const float TagHeight = 64f;
        public const float TagGap = 14f;
        public const float StripTop = PuzzleHud.TopBand + 8f;
        public const float PulseDuration = 0.32f;
        public static readonly Color SatisfiedColor = PresentationKit.Teal;
        public static readonly Color ViolatedColor = PresentationKit.Terracotta;
        public static readonly Color InactiveColor = PresentationKit.Hex(0xB9B2A6);
        public static readonly Color ZoneTint = new Color(1f, 0.96f, 0.84f, 0.3f);
        /// <summary>Tags wrap into centred rows no wider than this (reference px).</summary>
        public const float MaxRowWidth = 1000f;
        public const float RowGap = 10f;
        public static readonly Color TargetTint = new Color(0.98f, 0.82f, 0.42f, 0.7f);
        public static readonly Color SuccessTint = new Color(0.36f, 0.8f, 0.6f, 0.72f);
        public static readonly Color WarningTint = new Color(0.86f, 0.32f, 0.24f, 0.62f);
        public static readonly Color AccessTint = new Color(0.95f, 0.62f, 0.24f, 0.6f);

        private sealed class Tag
        {
            public PuzzleRule Rule;
            public RectTransform Root;
            public Image Badge;
            public Text Mark;
            public RuleTagStatus Status;
            public string Label;
            public float Pulse = -1f;
        }

        private readonly List<Tag> _tags = new List<Tag>();
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private readonly Dictionary<string, SoftCellShape> _shapes = new Dictionary<string, SoftCellShape>(StringComparer.Ordinal);
        private readonly Dictionary<string, Material> _shapeMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly HashSet<string> _visibleShapes = new HashSet<string>(StringComparer.Ordinal);
        private PuzzleLevel _level;
        private PuzzleBoardPresenter _board;
        private Material _template;
        private RectTransform _strip;
        private Transform _overlayRoot;
        private IReadOnlyList<RuleResult> _committed;
        private string _dragKey;

        /// <summary>Rule ids shown in the strip, in RuleSet (ordinal id) order.</summary>
        public IReadOnlyList<string> RuleIds => _tags.ConvertAll(t => t.Rule.Id);
        public bool StripVisible => _strip != null && _strip.gameObject.activeSelf;
        public RuleTagStatus StatusOf(string ruleId) => _tags.Find(t => t.Rule.Id == ruleId)?.Status ?? RuleTagStatus.Inactive;
        public string LabelOf(string ruleId) => _tags.Find(t => t.Rule.Id == ruleId)?.Label;
        public bool IsPulsing(string ruleId) => (_tags.Find(t => t.Rule.Id == ruleId)?.Pulse ?? -1f) >= 0f;
        /// <summary>Visible world cue keys: "zone:&lt;rule&gt;", "target:&lt;item&gt;", "warn:&lt;item&gt;", "access:&lt;item&gt;".</summary>
        public IReadOnlyCollection<string> VisibleCues => _visibleShapes;
        public RectTransform Strip => _strip;
        /// <summary>Height of the tag rows (reference px; 0 when hidden).</summary>
        public float StripHeight { get; private set; }

        /// <summary>Builds the strip for a level (hidden when it authors no rules). Call after the HUD and board exist.</summary>
        public void Build(PuzzleLevel level, PuzzleHud hud, PuzzleBoardPresenter board, Material template)
        {
            Clear();
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _board = board;
            _template = PresentationKit.TemplateOrFallback(template);
            _overlayRoot = new GameObject("Rule Cues").transform;
            _overlayRoot.SetParent(transform, false);
            if (level.Rules.Rules.Count == 0 || hud == null)
                return;

            _strip = new GameObject("Rule Strip", typeof(RectTransform)).GetComponent<RectTransform>();
            _strip.SetParent(hud.transform, false);
            _strip.anchorMin = _strip.anchorMax = new Vector2(0.5f, 1f);
            _strip.pivot = new Vector2(0.5f, 1f);
            _strip.anchoredPosition = new Vector2(0f, -StripTop);
            var rounded = Own(PresentationKit.RoundedSprite(64, 26));
            Own(rounded.texture);
            var circle = Own(PresentationKit.RoundedSprite(64, 31));
            Own(circle.texture);

            var x = 0f;
            foreach (var rule in level.Rules.Rules)
            {
                var tag = new Tag { Rule = rule, Label = PuzzleRuleText.Label(rule, level.InitialState) };
                tag.Root = Panel(_strip, "Rule " + rule.Id, rounded, PuzzleHud.PaperFill);
                tag.Root.gameObject.AddComponent<CanvasGroup>();
                var shadow = Panel(tag.Root, "Drop Shadow", rounded, PuzzleHud.DropShadow);
                shadow.SetAsFirstSibling();
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                icon.rectTransform.SetParent(tag.Root, false);
                icon.sprite = Own(KindIcon(rule));
                Own(icon.sprite.texture);
                icon.color = PuzzleHud.Ink;
                icon.raycastTarget = false;
                var label = Text(tag.Root, tag.Label, hud.Font, 30, PuzzleHud.Ink);
                tag.Badge = new GameObject("Status", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                tag.Badge.rectTransform.SetParent(tag.Root, false);
                tag.Badge.sprite = circle;
                tag.Badge.raycastTarget = false;
                tag.Mark = Text(tag.Badge.rectTransform, "", hud.Font, 28, PuzzleHud.PaperFill);

                var textWidth = Mathf.Ceil(label.preferredWidth);
                var width = 16f + 40f + 10f + textWidth + 12f + 38f + 14f;
                tag.Root.sizeDelta = new Vector2(width, TagHeight);
                shadow.sizeDelta = new Vector2(width + 4f, TagHeight + 4f);
                shadow.anchoredPosition = new Vector2(0f, -5f);
                Place(icon.rectTransform, new Vector2(16f + 20f - width * 0.5f, 0f), new Vector2(40f, 40f));
                Place(label.rectTransform, new Vector2(16f + 40f + 10f + textWidth * 0.5f - width * 0.5f, 0f), new Vector2(textWidth + 4f, TagHeight));
                Place(tag.Badge.rectTransform, new Vector2(width * 0.5f - 14f - 19f, 0f), new Vector2(38f, 38f));
                Place(tag.Mark.rectTransform, Vector2.zero, new Vector2(38f, 38f));
                tag.Root.anchoredPosition = new Vector2(width, 0f); // width parked here; laid out below
                _tags.Add(tag);
            }
            // Wrap into centred rows under the level pass.
            var rows = new List<List<Tag>> { new List<Tag>() };
            var rowWidth = 0f;
            foreach (var tag in _tags)
            {
                var width = tag.Root.sizeDelta.x;
                if (rows[rows.Count - 1].Count > 0 && rowWidth + TagGap + width > MaxRowWidth)
                {
                    rows.Add(new List<Tag>());
                    rowWidth = 0f;
                }
                rowWidth += (rows[rows.Count - 1].Count > 0 ? TagGap : 0f) + width;
                rows[rows.Count - 1].Add(tag);
            }
            for (var r = 0; r < rows.Count; r++)
            {
                var total = rows[r].Sum(t => t.Root.sizeDelta.x) + TagGap * (rows[r].Count - 1);
                x = -total * 0.5f;
                foreach (var tag in rows[r])
                {
                    var width = tag.Root.sizeDelta.x;
                    tag.Root.anchoredPosition = new Vector2(x + width * 0.5f, -TagHeight * 0.5f - r * (TagHeight + RowGap));
                    x += width + TagGap;
                }
            }
            StripHeight = rows.Count * TagHeight + (rows.Count - 1) * RowGap;
        }

        /// <summary>Committed Domain results (PuzzleSession.CurrentCompletion.Rules). Animates only status changes.</summary>
        public void Sync(IReadOnlyList<RuleResult> results, bool animate)
        {
            _committed = results;
            foreach (var tag in _tags)
            {
                var status = StatusFrom(Find(results, tag.Rule.Id));
                if (animate && status != tag.Status)
                    tag.Pulse = 0f;
                tag.Status = status;
                tag.Badge.color = status == RuleTagStatus.Satisfied ? SatisfiedColor : status == RuleTagStatus.Violated ? ViolatedColor : InactiveColor;
                tag.Mark.text = status == RuleTagStatus.Violated ? "!" : status == RuleTagStatus.Satisfied ? "•" : "";
                var group = tag.Root.GetComponent<CanvasGroup>();
                group.alpha = status == RuleTagStatus.Inactive ? 0.55f : 1f;
            }
            _dragKey = null;
            ShowIdleCues();
        }

        /// <summary>
        /// Drag-time context for the dragged item (null clears it). <paramref name="preview"/> is RuleEvaluator on the
        /// drag controller's accepted Domain preview state, or null when there is no valid preview.
        /// </summary>
        public void SetDragContext(PuzzleItem dragged, IReadOnlyList<RuleResult> preview, string previewKey)
        {
            var key = dragged == null ? null : dragged.InstanceId + "|" + previewKey;
            if (key == _dragKey)
                return;
            _dragKey = key;
            if (dragged == null)
            {
                ShowIdleCues();
                return;
            }
            BeginCues();
            foreach (var rule in _level.Rules.Rules)
                switch (rule)
                {
                    case ZoneRule zone when rule.Subjects.Matches(dragged):
                        ShowZone("zone:" + rule.Id, zone.ZoneId);
                        break;
                    case AdjacencyRequiredRule required when rule.Subjects.Matches(dragged):
                    {
                        var met = preview != null && Find(preview, rule.Id) is RuleResult r && !r.OffendingIds.Contains(dragged.InstanceId);
                        foreach (var view in _board.ItemViews.Values)
                            if (view.InstanceId != dragged.InstanceId && required.Targets.Matches(ItemOf(view)))
                                ShowItem("target:" + view.InstanceId, view, met ? SuccessTint : TargetTint);
                        break;
                    }
                    case AdjacencyForbiddenRule forbidden when rule.Subjects.Matches(dragged) || forbidden.Targets.Matches(dragged):
                    {
                        if (preview == null || !(Find(preview, rule.Id) is RuleResult r))
                            break;
                        var involved = r.OffendingIds.Contains(dragged.InstanceId) || r.RelatedIds.Contains(dragged.InstanceId);
                        if (!involved)
                            break;
                        foreach (var id in r.OffendingIds)
                            WarnItem(id);
                        foreach (var id in r.RelatedIds)
                            WarnItem(id);
                        break;
                    }
                }
            EndCues();
        }

        private PuzzleItem ItemOf(PuzzleItemView view) =>
            _board.PresentedState != null && _board.PresentedState.TryGetItem(view.InstanceId, out var item) ? item : null;

        // Idle: only currently broken forbidden-adjacency pairs and blocked access subjects get a cue.
        private void ShowIdleCues()
        {
            BeginCues();
            if (_committed != null && _level != null)
                foreach (var rule in _level.Rules.Rules)
                {
                    var result = Find(_committed, rule.Id);
                    if (result == null || result.IsSatisfied)
                        continue;
                    if (rule is AdjacencyForbiddenRule)
                    {
                        foreach (var id in result.OffendingIds)
                            WarnItem(id);
                        foreach (var id in result.RelatedIds)
                            WarnItem(id);
                    }
                    else if (rule is AccessRule)
                        foreach (var id in result.OffendingIds)
                            if (_board.ItemViews.TryGetValue(id, out var view))
                                ShowItem("access:" + id, view, AccessTint);
                }
            EndCues();
        }

        private void WarnItem(string id)
        {
            if (_board.ItemViews.TryGetValue(id, out var view))
                ShowItem("warn:" + id, view, WarningTint);
        }

        private readonly HashSet<string> _frame = new HashSet<string>(StringComparer.Ordinal);

        private void BeginCues() => _frame.Clear();

        private void EndCues()
        {
            foreach (var pair in _shapes)
                if (!_frame.Contains(pair.Key))
                    pair.Value.Show(_overlayRoot, null, default, default, 0f, null);
            _visibleShapes.Clear();
            _visibleShapes.UnionWith(_frame);
        }

        // Soft glow under a board item's footprint (just below the item, so it reads as a halo around it).
        private void ShowItem(string key, PuzzleItemView view, Color tint)
        {
            if (!view.IsOnBoard || _board == null || !_board.Compartments.TryGetValue(view.Placement.Compartment, out var compartment))
                return;
            var material = MaterialFor(key, tint);
            ShapeFor(key).Show(_overlayRoot, view.Footprint.OccupiedCells, compartment.transform.position, view.Placement.Anchor,
                view.Placement.Layer * PuzzleBoardLayout.LayerHeight + 0.02f, material);
            _frame.Add(key);
        }

        // Zone area: every column of the zone in every compartment, as one soft tinted lining region.
        private void ShowZone(string key, string zoneId)
        {
            foreach (var compartment in _level.Spec.Board.Compartments)
            {
                if (!_board.Compartments.TryGetValue(compartment.Id, out var view))
                    continue;
                var cells = new List<Cell>();
                foreach (var cell in compartment.Mask.GetValidCells())
                    if (compartment.GetColumnZone(cell) == zoneId)
                        cells.Add(cell);
                var shapeKey = key + ":" + compartment.Id;
                ShapeFor(shapeKey).Show(_overlayRoot, cells, view.transform.position, default, 0.012f, MaterialFor(shapeKey, ZoneTint));
                if (cells.Count > 0)
                    _frame.Add(shapeKey);
            }
        }

        private SoftCellShape ShapeFor(string key)
        {
            if (!_shapes.TryGetValue(key, out var shape))
                _shapes.Add(key, shape = new SoftCellShape("Rule Cue " + key));
            return shape;
        }

        private Material MaterialFor(string key, Color tint)
        {
            if (!_shapeMaterials.TryGetValue(key, out var material))
                _shapeMaterials.Add(key, material = Own(PresentationKit.Transparent(_template, tint)));
            material.SetColor(PresentationKit.BaseColorId, tint);
            material.SetColor(PresentationKit.ColorId, tint);
            return material;
        }

        private void Update()
        {
            foreach (var tag in _tags)
            {
                if (tag.Pulse < 0f)
                    continue;
                tag.Pulse += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(tag.Pulse / PulseDuration);
                tag.Root.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
                if (t >= 1f)
                {
                    tag.Root.localScale = Vector3.one;
                    tag.Pulse = -1f;
                }
            }
        }

        private static RuleResult Find(IReadOnlyList<RuleResult> results, string ruleId)
        {
            if (results != null)
                foreach (var result in results)
                    if (result.RuleId == ruleId)
                        return result;
            return null;
        }

        // No active subject (e.g. every subject left the active domain) = not applicable.
        private static RuleTagStatus StatusFrom(RuleResult result) =>
            result == null || result.SubjectIds.Count == 0 ? RuleTagStatus.Inactive
            : result.IsSatisfied ? RuleTagStatus.Satisfied : RuleTagStatus.Violated;

        public void Clear()
        {
            if (_strip != null)
            {
                _strip.gameObject.SetActive(false);
                Destroy(_strip.gameObject);
                _strip = null;
            }
            _tags.Clear();
            StripHeight = 0f;
            foreach (var shape in _shapes.Values)
                shape.Dispose();
            _shapes.Clear();
            _shapeMaterials.Clear();
            _visibleShapes.Clear();
            if (_overlayRoot != null)
            {
                _overlayRoot.gameObject.SetActive(false);
                Destroy(_overlayRoot.gameObject);
                _overlayRoot = null;
            }
            foreach (var asset in _owned)
                if (asset != null)
                    Destroy(asset);
            _owned.Clear();
            _committed = null;
            _dragKey = null;
        }

        private void OnDestroy() => Clear();

        private T Own<T>(T asset) where T : UnityEngine.Object
        {
            _owned.Add(asset);
            return asset;
        }

        private static RectTransform Panel(Transform parent, string name, Sprite sprite, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            var image = rect.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static Text Text(Transform parent, string text, Font font, int size, Color color)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.rectTransform.SetParent(parent, false);
            label.font = font;
            label.fontSize = size;
            label.fontStyle = font != null && font.name.StartsWith("BricolageGrotesque") ? FontStyle.Normal : FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.color = color;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        // Kind icons drawn from signed distances (anti-aliased, white; tinted by the Image colour).
        private static Sprite KindIcon(PuzzleRule rule)
        {
            Func<Vector2, float> sdf;
            switch (rule)
            {
                case ZoneRule _:
                    // Rounded frame with its left half filled: "this part of the bag".
                    sdf = p => Mathf.Min(Ring(Box(p, new Vector2(0.5f, 0.5f), new Vector2(0.36f, 0.3f), 0.08f), 0.045f),
                        Box(p, new Vector2(0.32f, 0.5f), new Vector2(0.17f, 0.29f), 0.06f));
                    break;
                case AdjacencyRequiredRule _:
                    // Two blocks touching.
                    sdf = p => Mathf.Min(Box(p, new Vector2(0.31f, 0.5f), new Vector2(0.17f, 0.2f), 0.05f),
                        Ring(Box(p, new Vector2(0.69f, 0.5f), new Vector2(0.17f, 0.2f), 0.05f), 0.04f));
                    break;
                case AdjacencyForbiddenRule _:
                    // Two blocks kept apart by a slash.
                    sdf = p => Mathf.Min(Mathf.Min(Box(p, new Vector2(0.2f, 0.5f), new Vector2(0.13f, 0.18f), 0.04f),
                        Box(p, new Vector2(0.8f, 0.5f), new Vector2(0.13f, 0.18f), 0.04f)),
                        Segment(p, new Vector2(0.4f, 0.24f), new Vector2(0.6f, 0.76f)) - 0.04f);
                    break;
                default:
                    // Access: a block with a lift arrow above it.
                    sdf = p => Mathf.Min(Box(p, new Vector2(0.5f, 0.3f), new Vector2(0.26f, 0.13f), 0.05f),
                        Mathf.Min(Segment(p, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.86f)) - 0.04f,
                            Mathf.Min(Segment(p, new Vector2(0.5f, 0.86f), new Vector2(0.36f, 0.71f)) - 0.04f,
                                Segment(p, new Vector2(0.5f, 0.86f), new Vector2(0.64f, 0.71f)) - 0.04f)));
                    break;
            }
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Rule icon" };
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = sdf(new Vector2((x + 0.5f) / size, (y + 0.5f) / size));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d * size)));
                }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float Box(Vector2 p, Vector2 center, Vector2 half, float radius)
        {
            var q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + Vector2.one * radius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
        }

        private static float Ring(float d, float thickness) => Mathf.Abs(d) - thickness;

        private static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var pa = p - a;
            var ba = b - a;
            var h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude;
        }
    }
}
