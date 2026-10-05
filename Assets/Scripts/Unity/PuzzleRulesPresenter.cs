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

    // ZT-042 live rule presentation; UI-SLICE-01 objective note (STYLE-FRAME-01). The level's authored rules are listed
    // on a compact paper travel note pinned on the open lid (title, one status dot + sentence per rule), and shown as world
    // cues inside the suitcase. Every status comes from the Domain: committed status from
    // PuzzleSession.CurrentCompletion.Rules (RuleEvaluator), drag-time hints from RuleEvaluator on the drag controller's
    // Domain preview state. Nothing here re-derives zones, adjacency or access.
    // Responsive: when the space between the header and the playable bed cannot hold the note at its readable type size,
    // the note collapses to a one-line chip ("✓ Pasaport  ○ Şampuan"); tapping the chip expands the note over the lid
    // until the next tap. Presentation state only: never reads or writes gameplay state.
    // Interaction context: idle shows cues only for currently violated forbidden-adjacency / access rules; a drag shows
    // only the overlays relevant to the dragged item (target zone, adjacency targets, forbidden partners).
    public sealed class PuzzleRulesPresenter : MonoBehaviour
    {
        public const float PulseDuration = MotionTokens.RulePulseDuration;
        /// <summary>Gap kept between the header and the note, and between the note and the playable bed (reference px).</summary>
        public const float NoteMargin = 10f;
        public const float BedClearance = 24f;
        public const int RuleFontSize = 34;
        public const int TitleFontSize = 46;
        public const float RowHeight = 84f;
        public const float ChipHeight = 60f;
        public const float NoteLeft = 40f;
        public static readonly Color SatisfiedColor = PresentationKit.Teal;
        public static readonly Color ViolatedColor = PresentationKit.Terracotta;
        public static readonly Color InactiveColor = PresentationKit.Hex(0xB9B2A6);
        public static readonly Color ZoneTint = new Color(1f, 0.96f, 0.84f, 0.3f);
        public static readonly Color TargetTint = new Color(0.98f, 0.82f, 0.42f, 0.7f);
        public static readonly Color SuccessTint = new Color(0.36f, 0.8f, 0.6f, 0.72f);
        public static readonly Color WarningTint = new Color(0.86f, 0.32f, 0.24f, 0.62f);
        public static readonly Color AccessTint = new Color(0.95f, 0.62f, 0.24f, 0.6f);

        // One status mark (filled dot + check / "!", or an empty ring), used in the note and in the chip.
        private sealed class Mark
        {
            public RectTransform Root;
            public Image Dot;
            public Image Ring;
            public Image Check;
            public Image Bang;
        }

        private sealed class Tag
        {
            public PuzzleRule Rule;
            public string Label;
            public string Short;
            public Mark Note;
            public Mark Chip;
            public RuleTagStatus Status;
            public bool Pending;
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
        private RectTransform _note;
        private RectTransform _chip;
        private Vector2 _noteSize;
        private Vector2 _chipSize;
        private bool _hidden;
        private Transform _overlayRoot;
        private IReadOnlyList<RuleResult> _committed;
        private string _dragKey;

        /// <summary>Rule ids shown on the note, in RuleSet (ordinal id) order.</summary>
        public IReadOnlyList<string> RuleIds => _tags.ConvertAll(t => t.Rule.Id);
        public int CompletionRuleCount => _tags.Count;
        public int ConfirmedRuleCount { get; private set; }
        /// <summary>The objective (full note or compact chip) is on screen.</summary>
        public bool ObjectiveVisible => _note != null && (_note.gameObject.activeSelf || _chip.gameObject.activeSelf);
        /// <summary>Not enough room for the full note: the chip is the resting presentation.</summary>
        public bool Compact { get; private set; }
        /// <summary>Compact mode with the note temporarily expanded by a tap.</summary>
        public bool Expanded { get; private set; }
        public RectTransform Note => _note;
        public RectTransform Chip => _chip;
        /// <summary>Full note height (reference px) at the readable type size; the layout switches to the chip below it.</summary>
        public float NoteHeight => _noteSize.y;
        public RuleTagStatus StatusOf(string ruleId) => _tags.Find(t => t.Rule.Id == ruleId)?.Status ?? RuleTagStatus.Inactive;
        /// <summary>
        /// Violated only because its subjects are still waiting in the Source Tray: shown as an open "to do" ring, not as
        /// a broken rule. <see cref="StatusOf"/> keeps the Domain status.
        /// </summary>
        public bool IsPending(string ruleId) => _tags.Find(t => t.Rule.Id == ruleId)?.Pending ?? false;
        public string LabelOf(string ruleId) => _tags.Find(t => t.Rule.Id == ruleId)?.Label;
        public bool IsPulsing(string ruleId) => (_tags.Find(t => t.Rule.Id == ruleId)?.Pulse ?? -1f) >= 0f;
        /// <summary>Visible world cue keys: "zone:&lt;rule&gt;", "target:&lt;item&gt;", "warn:&lt;item&gt;", "access:&lt;item&gt;".</summary>
        public IReadOnlyCollection<string> VisibleCues => _visibleShapes;

        /// <summary>Builds the note and chip for a level (nothing when it authors no rules). Call after the HUD and board exist.</summary>
        public void Build(PuzzleLevel level, PuzzleHud hud, PuzzleBoardPresenter board, Material template, Texture2D travelProps = null)
        {
            Clear();
            _level = level ?? throw new ArgumentNullException(nameof(level));
            _board = board;
            _template = PresentationKit.TemplateOrFallback(template);
            _overlayRoot = new GameObject("Rule Cues").transform;
            _overlayRoot.SetParent(transform, false);
            if (level.Rules.Rules.Count == 0 || hud == null)
                return;

            var rounded = Own(PresentationKit.RoundedSprite(96, 40));
            Own(rounded.texture);
            var circle = Own(PresentationKit.RoundedSprite(64, 32));
            Own(circle.texture);
            var ring = Own(PresentationKit.RingSprite(64, 7f));
            Own(ring.texture);
            var check = Own(PresentationKit.CheckIcon(64));
            Own(check.texture);
            foreach (var rule in level.Rules.Rules)
                _tags.Add(new Tag
                {
                    Rule = rule,
                    Label = PuzzleRuleText.Label(rule, level.InitialState),
                    Short = PuzzleRuleText.Subject(rule, level.InitialState)
                });

            // Major authored mission card: fixed readable content width plus a destination-polaroid column.
            var title = PuzzleRuleText.Templates["objective.title"];
            _noteSize = new Vector2(1000f, 120f + _tags.Count * RowHeight + 18f);
            _note = PaperUi.Card(hud.SafeArea, "Objective Note", rounded, new Vector2(0f, 1f), Vector2.zero, _noteSize, PaperUi.Paper,
                0.38f, 14f, PaperUi.Skin("travel_frame", 0));
            PaperUi.Face(_note).color = Color.white;
            _note.localRotation = Quaternion.Euler(0f, 0f, 0.6f);
            var top = _noteSize.y * 0.5f;
            var left = -_noteSize.x * 0.5f;
            PaperUi.Label(_note, title, hud.DisplayFont, TitleFontSize, PaperUi.Ink, TextAnchor.MiddleLeft,
                new Vector2(left + 370f, top - 66f), new Vector2(650f, 64f));
            var destination = Resources.Load<Texture2D>("UiSlice011/santorini_vacation");
            if (destination != null || travelProps != null)
            {
                var postcardRect = destination != null
                    ? new Rect(destination.width * 0.40f, destination.height * 0.49f, destination.width * 0.6f, destination.width * 0.6f)
                    : new Rect(0f, Mathf.Max(0f, travelProps.height - 722f), Mathf.Min(362f, travelProps.width), Mathf.Min(284f, travelProps.height));
                var postcard = Own(Sprite.Create(destination != null ? destination : travelProps, postcardRect, new Vector2(0.5f, 0.5f), 100f));
                var polaroid = PaperUi.Card(_note, "Santorini Polaroid", rounded, new Vector2(0.5f, 0.5f),
                    new Vector2(367f, 22f), new Vector2(218f, 244f), Color.white, 0.32f, 8f);
                PaperUi.Image(polaroid, "Santorini", postcard, Color.white, new Vector2(192f, 192f)).anchoredPosition = new Vector2(0f, 12f);
                PaperUi.Label(polaroid, "SANTORINI", hud.DisplayFont, 22, PaperUi.Teal, TextAnchor.MiddleCenter,
                    new Vector2(0f, -101f), new Vector2(190f, 30f));
                polaroid.localRotation = Quaternion.Euler(0f, 0f, -5f);
            }
            var tape = PaperUi.Image(_note, "Tape", PaperUi.Skin("tape", 0), Color.white,
                new Vector2(86f, 28f));
            tape.anchoredPosition = new Vector2(367f, 148f);
            tape.localRotation = Quaternion.Euler(0f, 0f, -28f);
            for (var i = 0; i < _tags.Count; i++)
            {
                var y = top - 120f - RowHeight * (i + 0.5f);
                PaperUi.Image(_note, "Rule Line", null, PresentationKit.Hex(0xD7B779), new Vector2(660f, 2f)).anchoredPosition =
                    new Vector2(-130f, y - RowHeight * 0.5f + 2f);
                var badge = PaperUi.Image(_note, "Rule Number " + (i + 1), circle, PresentationKit.Teal, new Vector2(46f, 46f));
                badge.anchoredPosition = new Vector2(left + 28f + 23f, y);
                PaperUi.Label(badge, (i + 1).ToString(), hud.DisplayFont, 27, Color.white, TextAnchor.MiddleCenter,
                    Vector2.zero, new Vector2(46f, 46f));
                _tags[i].Note = MakeMark(_note, "Status " + _tags[i].Rule.Id,
                    new Vector2(left + 28f + 46f + 12f + 17f, y), circle, ring, check, hud.Font);
                var definitionId = PuzzleRuleText.SubjectDefinitionId(_tags[i].Rule, level.InitialState);
                var itemIcon = Own(PresentationKit.ItemIcon(definitionId, 64));
                Own(itemIcon.texture);
                var icon = PaperUi.Image(_note, "Item Icon " + (definitionId ?? "rule"), itemIcon, PaperUi.Ink, new Vector2(48f, 48f));
                icon.anchoredPosition = new Vector2(left + 28f + 46f + 12f + 34f + 12f + 24f, y);
                var labelLeft = left + 28f + 46f + 12f + 34f + 12f + 48f + 14f;
                var width = _noteSize.x - (labelLeft - left) - 290f;
                var label = PaperUi.Label(_note, PuzzleRuleText.RichLabel(_tags[i].Rule, level.InitialState), hud.Font,
                    RuleFontSize, PaperUi.Ink, TextAnchor.MiddleLeft,
                    new Vector2(labelLeft + width * 0.5f, y + 1f), new Vector2(width, RowHeight));
                label.supportRichText = true;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            // Compact chip: one status mark + subject name per rule.
            var names = new List<Text>();
            var chipWidth = 58f; // keep the final label clear of the paper's folded corner
            _chip = PaperUi.Card(hud.SafeArea, "Objective Chip", rounded, new Vector2(0f, 1f), Vector2.zero, Vector2.one, PaperUi.Paper,
                0.24f, 6f, PaperUi.Skin("checklist_tab", 24));
            PaperUi.Face(_chip).color = Color.white;
            foreach (var tag in _tags)
            {
                var name = PaperUi.Label(_chip, tag.Short, hud.Font, RuleFontSize, PaperUi.Ink, TextAnchor.MiddleLeft, Vector2.zero,
                    new Vector2(10f, ChipHeight));
                name.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(name.preferredWidth) + 4f, ChipHeight);
                names.Add(name);
                chipWidth += 30f + 10f + name.rectTransform.sizeDelta.x + 22f;
            }
            _chipSize = new Vector2(chipWidth, ChipHeight);
            PaperUi.Resize(_chip, _chipSize);
            var x = -chipWidth * 0.5f + 22f;
            for (var i = 0; i < _tags.Count; i++)
            {
                _tags[i].Chip = MakeMark(_chip, "Status " + _tags[i].Rule.Id, new Vector2(x + 15f, 0f), circle, ring, check, hud.Font);
                x += 30f + 10f;
                names[i].rectTransform.anchoredPosition = new Vector2(x + names[i].rectTransform.sizeDelta.x * 0.5f, 1f);
                x += names[i].rectTransform.sizeDelta.x + 22f;
            }
            Compact = false;
            Expanded = false;
            _hidden = false;
            LayoutObjective(float.MaxValue);
        }

        private static Mark MakeMark(Transform parent, string name, Vector2 position, Sprite circle, Sprite ring, Sprite check, Font font)
        {
            var root = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            root.anchoredPosition = position;
            root.sizeDelta = new Vector2(30f, 30f);
            var mark = new Mark
            {
                Root = root,
                Dot = PaperUi.Image(root, "Dot", circle, SatisfiedColor, new Vector2(30f, 30f)).GetComponent<Image>(),
                Ring = PaperUi.Image(root, "Ring", ring, InactiveColor, new Vector2(30f, 30f)).GetComponent<Image>(),
                Check = PaperUi.Image(root, "Check", PaperUi.Skin("stamp_check", 0) ?? check, Color.white,
                    new Vector2(30f, 30f)).GetComponent<Image>(),
                Bang = PaperUi.Image(root, "Bang", PaperUi.Skin("stamp_cross", 0), Color.white,
                    new Vector2(30f, 30f)).GetComponent<Image>()
            };
            return mark;
        }

        private static void Apply(Mark mark, RuleTagStatus status, bool pending)
        {
            var open = status == RuleTagStatus.Inactive || pending;
            mark.Dot.gameObject.SetActive(!open);
            mark.Dot.color = status == RuleTagStatus.Violated ? ViolatedColor : SatisfiedColor;
            mark.Ring.gameObject.SetActive(open);
            mark.Check.gameObject.SetActive(!open && status == RuleTagStatus.Satisfied);
            mark.Bang.gameObject.SetActive(!open && status == RuleTagStatus.Violated);
        }

        /// <summary>
        /// Chooses the full note or the compact chip from the room (reference px) between the header and the playable bed,
        /// and pins it under the header on the lid's left side. Never changes size of type: below the room the full note
        /// needs, the chip is used instead.
        /// </summary>
        public void LayoutObjective(float availableHeight)
        {
            if (_note == null)
                return;
            Compact = availableHeight < _noteSize.y + NoteMargin + BedClearance;
            if (!Compact)
                Expanded = false;
            var top = -(PuzzleHud.HeaderBottom + NoteMargin);
            _note.anchoredPosition = new Vector2(NoteLeft + _noteSize.x * 0.5f, top - _noteSize.y * 0.5f);
            _chip.anchoredPosition = new Vector2(NoteLeft + _chipSize.x * 0.5f, top - _chipSize.y * 0.5f);
            ApplyVisibility();
        }

        /// <summary>Compact mode: the chip (or the expanded note) contains the screen point.</summary>
        public bool HitObjective(Vector2 screenPosition, Camera eventCamera = null)
        {
            if (_note == null || _hidden || !Compact)
                return false;
            var target = Expanded ? _note : _chip;
            return RectTransformUtility.RectangleContainsScreenPoint(target, screenPosition, eventCamera);
        }

        /// <summary>Compact mode: expands the chip into the note, or collapses it again. Presentation only.</summary>
        public void ToggleExpanded()
        {
            if (!Compact)
                return;
            Expanded = !Expanded;
            ApplyVisibility();
        }

        public void Collapse()
        {
            if (!Expanded)
                return;
            Expanded = false;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (_note == null)
                return;
            _note.gameObject.SetActive(!_hidden && (!Compact || Expanded));
            _chip.gameObject.SetActive(!_hidden && Compact && !Expanded);
        }

        /// <summary>
        /// Committed Domain results (PuzzleSession.CurrentCompletion.Rules) and the state they were evaluated on (only to
        /// tell "not packed yet" from "packed in the wrong place"). Animates only status changes.
        /// </summary>
        public void Sync(IReadOnlyList<RuleResult> results, bool animate, PuzzleState state = null)
        {
            _committed = results;
            foreach (var tag in _tags)
            {
                var result = Find(results, tag.Rule.Id);
                var status = StatusFrom(result);
                var pending = status == RuleTagStatus.Violated && state != null && result.OffendingIds.All(id =>
                    state.TryGetItem(id, out var item) && item.Location.Kind == ItemLocationKind.SourceTray);
                if (animate && (status != tag.Status || pending != tag.Pending))
                    tag.Pulse = 0f;
                tag.Status = status;
                tag.Pending = pending;
                Apply(tag.Note, status, pending);
                Apply(tag.Chip, status, pending);
            }
            if (_note != null && _hidden)
            {
                _hidden = false;
                ApplyVisibility();
            }
            _dragKey = null;
            ShowIdleCues();
        }

        public void ClearForCompletion()
        {
            _dragKey = null;
            foreach (var tag in _tags)
            {
                tag.Pulse = -1f;
                tag.Note.Root.localScale = tag.Chip.Root.localScale = Vector3.one;
            }
            BeginCues();
            EndCues();
            Expanded = false;
            _hidden = true;
            ApplyVisibility();
        }

        public void BeginCompletionCascade()
        {
            ConfirmedRuleCount = 0;
            _hidden = false;
            ApplyVisibility();
            foreach (var tag in _tags)
            {
                tag.Pulse = -1f;
                Apply(tag.Note, RuleTagStatus.Inactive, true);
                Apply(tag.Chip, RuleTagStatus.Inactive, true);
            }
            BeginCues();
            EndCues();
        }

        public void ConfirmCompletionRule(int index)
        {
            if (index != ConfirmedRuleCount || index >= _tags.Count)
                return;
            var tag = _tags[index];
            Apply(tag.Note, tag.Status, false);
            Apply(tag.Chip, tag.Status, false);
            tag.Pulse = 0f;
            ConfirmedRuleCount++;
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
                tag.Note.Root.localScale = tag.Chip.Root.localScale = Vector3.one * (1f + 0.25f * MotionTokens.SinePulse(t));
                if (t >= 1f)
                {
                    tag.Note.Root.localScale = tag.Chip.Root.localScale = Vector3.one;
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
            foreach (var panel in new[] { _note, _chip })
                if (panel != null)
                {
                    panel.gameObject.SetActive(false);
                    Destroy(panel.gameObject);
                }
            _note = _chip = null;
            _tags.Clear();
            Compact = Expanded = false;
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
    }
}
