using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ZipTrip.Application;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Domain.Items;

namespace ZipTrip.Unity
{
    // ZT-040 playable scene bootstrap for the ADR-0006 runtime: Resources/LevelsV2/<id>.json -> LevelJsonLoaderV2 ->
    // PuzzleLevel -> PuzzleSession -> board / tray presenters + drag controller + minimal HUD. Owns level selection,
    // restart (a fresh session from the authored level), Undo and the completion lock. No legacy gameplay controllers.
    // ZT-040B: the board is presented as an open suitcase on a linen packing surface; Source Tray items lie loose on
    // that surface in front of the suitcase. All of it is presentation; the board plane, coordinates and rules are
    // unchanged.
    // ZT-040C: with a container prefab the board sits inside authored container art (golden Cabin Suitcase); the
    // packing surface and loose items then rest at the container's base height in front of it. Without one, the
    // procedural ZT-040B suitcase is the fallback.
    public sealed class PuzzleGameplayScene : MonoBehaviour
    {
        public const string LevelFolder = "LevelsV2/";
        /// <summary>Loose-item scale bounds; the actual scale fits all Source Tray items into one row (ZT-040B pass 2).</summary>
        public const float MinTrayScale = 0.58f;
        public const float MaxTrayScale = 0.8f;
        /// <summary>How far the row of loose items may extend past the suitcase's outer width (both sides together).</summary>
        public const float TrayOverhang = 0.7f;
        public const float TrayItemGap = 0.15f;
        /// <summary>Distance from the suitcase interior's front edge to the loose items (clears wall + handle).</summary>
        public const float TrayGap = 1.5f;
        /// <summary>Gap between an authored container's front (handle included) and the loose items.</summary>
        public const float ContainerTrayGap = 0.1f;
        /// <summary>
        /// Height (world y above the hinge) of the open lid that camera framing reserves; the rest of the lid may rise
        /// into the top HUD band or past the screen edge, so the lid reads without shrinking the board.
        /// </summary>
        public const float LidFrameHeight = 1.8f;

        [SerializeField] private string[] levelIds = { "lv1-fit", "lv2-rotate" };
        [SerializeField] private int startLevel;
        [SerializeField] private Material materialTemplate;
        [SerializeField] private GoldenItemPrefabCatalog goldenCatalog;
        [SerializeField] private GameObject containerPrefab;
        [SerializeField] private Font uiFont;
        [SerializeField] private Font displayFont;
        [SerializeField] private Texture2D backdropSurface;
        [SerializeField] private Texture2D backdropProps;

        private PointerInteractor _pointer;
        private bool _gestureOnHud;
        private float _framedAspect;
        private Vector2Int _framedScreen;
        private PackingTable _table;
        private string _ruleDragKey;
        private PuzzleLevel _fixture;
        private int _remaining = -1;
        private PuzzleState _statusState;
        private Rect _objectiveSafeArea;
        private string _fixtureLabel;
        private Func<PuzzleItem, GameObject> _fixtureVisuals;
        private HapticsService _haptics;
        private AudioCueService _audio;
        private readonly Dictionary<string, bool> _ruleStates = new Dictionary<string, bool>();
        private readonly HashSet<string> _completedLevelIds = new HashSet<string>();

        public PuzzleLevel Level { get; private set; }
        public PuzzleSession Session { get; private set; }
        public PuzzleBoardPresenter Board { get; private set; }
        public PuzzleTrayPresenter Tray { get; private set; }
        /// <summary>ZT-041 limited staging pads; inactive when the level's staging capacity is 0.</summary>
        public PuzzleStagingPresenter Staging { get; private set; }
        /// <summary>ZT-042 live rule tags and in-suitcase rule cues; empty when the level authors no rules.</summary>
        public PuzzleRulesPresenter Rules { get; private set; }
        public PuzzleDragController Drag { get; private set; }
        public PuzzleHud Hud { get; private set; }
        public PuzzleCompletionPresenter Completion { get; private set; }
        public Camera Camera { get; private set; }
        /// <summary>Linen packing table under the suitcase (excluded from camera framing).</summary>
        public GameObject Surface => _table != null ? _table.Surface : null;
        /// <summary>ZT-040D table, felt mat, light rig and post stack.</summary>
        public PackingTable Table => _table;
        public Material MaterialTemplate => materialTemplate;
        /// <summary>Authored container art (null = procedural ZT-040B suitcase fallback).</summary>
        public GameObject ContainerPrefab => containerPrefab;
        public int LevelIndex { get; private set; }
        public string LevelId => levelIds[LevelIndex];
        /// <summary>incomplete -> complete edges seen in this scene (Δ-11 semantics come from PuzzleSession).</summary>
        public int CompletionCount { get; private set; }

        public void Configure(Material template, GoldenItemPrefabCatalog catalog, params string[] levels)
        {
            materialTemplate = template;
            goldenCatalog = catalog;
            if (levels != null && levels.Length > 0)
                levelIds = levels;
        }

        public void ConfigureContainer(GameObject prefab) => containerPrefab = prefab;

        /// <summary>ZT-040D.1 HUD typography (Bricolage Grotesque); null keeps the built-in fallback.</summary>
        public void ConfigureFonts(Font ui, Font display)
        {
            uiFont = ui;
            displayFont = display;
        }

        public Font UiFont => uiFont;

        /// <summary>BACKDROP-SLICE-01 travel-world textures (surface, prop atlas); null keeps the plain linen table.</summary>
        public void ConfigureBackdrop(Texture2D surface, Texture2D props)
        {
            backdropSurface = surface;
            backdropProps = props;
        }

        public Texture2D BackdropSurface => backdropSurface;
        public Texture2D BackdropProps => backdropProps;

        private void Start()
        {
            if (Session == null)
                LoadLevel(startLevel);
        }

        public void LoadLevel(int index)
        {
            if (levelIds == null || levelIds.Length == 0)
                throw new InvalidOperationException("No levels configured.");
            EnsureRuntimeObjects();
            LevelIndex = (index % levelIds.Length + levelIds.Length) % levelIds.Length;
            var json = Resources.Load<TextAsset>(LevelFolder + LevelId);
            if (json == null)
                throw new InvalidOperationException("Missing level " + LevelFolder + LevelId);
            _fixture = null;
            _fixtureVisuals = null;
            Present(LevelJsonLoaderV2.Load(json.text, PuzzleItemCatalog.Create()), PuzzleRuleText.LevelTitle(LevelIndex + 1));
        }

        /// <summary>
        /// Loads an authored level that is not part of the shipped progression (test / staging fixtures, ZT-041).
        /// Restart reloads it; Next continues the shipped progression.
        /// </summary>
        public void LoadLevel(PuzzleLevel level, string label, Func<PuzzleItem, GameObject> fixtureVisuals = null)
        {
            EnsureRuntimeObjects();
            _fixture = level ?? throw new ArgumentNullException(nameof(level));
            _fixtureLabel = label;
            _fixtureVisuals = fixtureVisuals;
            Present(level, label);
        }

        private void Present(PuzzleLevel level, string label)
        {
            Completion.ResetForLevel(null, default, 0f, Hud);
            Level = level;
            Session = new PuzzleSession(Level);
            _ruleStates.Clear();
            foreach (var rule in Session.CurrentCompletion.Rules)
                _ruleStates[rule.RuleId] = rule.IsSatisfied;

            Board.UseContainer(containerPrefab);
            Board.Present(Level.Spec.Board, materialTemplate,
                item => _fixtureVisuals?.Invoke(item)
                        ?? PuzzleItemCatalog.ResolveGolden(goldenCatalog, item.Definition.Id, item.StateId)
                        ?? ProceduralItemVisuals.Resolve(item.Definition.Id, materialTemplate));
            var boardWidth = 0f;
            var boardDepth = 0f;
            foreach (var frame in Board.CompartmentFrames())
            {
                boardWidth = Mathf.Max(boardWidth, frame.Origin.x + frame.Width);
                boardDepth = Mathf.Max(boardDepth, frame.Height);
            }
            Tray.Clear();
            Tray.Gap = TrayItemGap;
            Tray.CenterRows = true;
            if (Board.Container != null)
            {
                // Loose items rest on the same surface as the suitcase, in one centred row in front of it.
                var body = Board.ContainerFootprint;
                Tray.RowWidth = Mathf.Max(body.width, 4f);
                Tray.Scale = FitTrayScale(Level.InitialState, Tray.RowWidth);
                Tray.transform.localPosition = new Vector3(body.center.x - Tray.RowWidth * 0.5f,
                    Board.ContainerBottomY + PackingTable.MatThickness, body.yMin - ContainerTrayGap);
            }
            else
            {
                // One centred row a little wider than the suitcase (keeps framing symmetric); wraps only if it cannot fit.
                Tray.RowWidth = Mathf.Max(boardWidth + SuitcaseShell.Wall * 2f + TrayOverhang, 4f);
                Tray.Scale = FitTrayScale(Level.InitialState, Tray.RowWidth);
                Tray.transform.localPosition = new Vector3((boardWidth - Tray.RowWidth) * 0.5f, 0f, -(boardDepth + TrayGap));
            }
            LayoutStaging();
            Drag.Initialize(Session, Board, Tray, Camera, null, Staging);
            Drag.ConfigureCues(_haptics, _audio);
            LayoutTable();
            Drag.InteractionEnabled = !Session.CurrentCompletion.IsComplete;

            Hud.SetLevel(label, PuzzleRuleText.LevelSubtitle(Level.Id));
            _remaining = -1;
            _statusState = null;
            Rules.Build(Level, Hud, Board, materialTemplate);
            _objectiveSafeArea = default;
            Rules.Sync(Session.CurrentCompletion.Rules, false, Session.CurrentState);
            _ruleDragKey = null;
            Hud.SetCompletionMode(Session.CurrentCompletion.IsComplete);
            Completion.ResetForLevel(Board.Container, Board.ContainerFootprint,
                Board.Container != null ? Board.Container.Base.GetComponent<Renderer>().bounds.max.y + 0.04f : 0f,
                Hud, Level.Id == "lv1-fit" && Board.Container != null);
            Hud.SetCompletionVisible(Session.CurrentCompletion.IsComplete);
            FrameCamera();
        }

        // Drag-time rule hints come from RuleEvaluator on the drag controller's accepted Domain preview; evaluated only when
        // that preview changes, never per frame, and never animated (pulses are for committed changes only).
        private void UpdateRuleDragContext()
        {
            if (!Drag.IsDragging || !Session.CurrentState.TryGetItem(Drag.DraggedInstanceId, out var dragged))
            {
                if (_ruleDragKey != null)
                    Rules.SetDragContext(null, null, null);
                _ruleDragKey = null;
                return;
            }
            var previewKey = Drag.PreviewValid ? Drag.Preview.State.Hash.ToString() : "none";
            var key = dragged.InstanceId + "|" + previewKey;
            if (key == _ruleDragKey)
                return;
            _ruleDragKey = key;
            var preview = Drag.PreviewValid ? RuleEvaluator.Evaluate(Drag.Preview.State, Level.Rules) : null;
            Rules.SetDragContext(dragged, preview, previewKey);
        }

        public void Restart()
        {
            if (_fixture != null)
                LoadLevel(_fixture, _fixtureLabel, _fixtureVisuals);
            else
                LoadLevel(LevelIndex);
        }

        /// <summary>Gap between the loose-item row and the staging pads.</summary>
        public const float StagingGap = 0.6f;

        // ZT-041: with staging capacity > 0 the pads share the packing row with the loose items (pads on the right, the
        // loose row narrowed), at the same resting height, so staging adds no extra band and the suitcase keeps its size.
        // Capacity 0 (Lv1 / Lv2) leaves the ZT-040D layout untouched.
        private void LayoutStaging()
        {
            Staging.Build(Level.Spec.StagingCapacity, materialTemplate);
            if (Staging.Capacity == 0)
                return;
            var total = Tray.RowWidth;
            Tray.RowWidth = Mathf.Max(total - Staging.RowWidth - StagingGap, 3f);
            Tray.Scale = FitTrayScale(Level.InitialState, Tray.RowWidth);
            var origin = Tray.transform.localPosition;
            Staging.transform.localPosition = new Vector3(origin.x + Tray.RowWidth + StagingGap, origin.y, origin.z);
        }

        public void NextLevel() => LoadLevel(LevelIndex + 1);

        public bool Undo()
        {
            if (Completion.CurrentPhase != PuzzleCompletionPresenter.Phase.Idle || Drag.IsDragging || !Session.Undo().StateChanged)
                return false;
            Drag.CancelCandidate();
            Drag.SyncPresenters();
            _haptics?.Play(FeelCue.Undo);
            _audio?.Play(FeelCue.Undo);
            RefreshCompletion();
            return true;
        }

        /// <summary>Single pointer flow (ADR-0004): HUD buttons first, everything else to the drag controller.</summary>
        public void HandlePointer(PointerSignal signal)
        {
            if (Hud.CompletionMode && signal.Phase == PointerPhase.Down
                && Completion.CurrentPhase != PuzzleCompletionPresenter.Phase.Confirmed)
            {
                Completion.Accelerate();
                return;
            }
            if (signal.Phase == PointerPhase.Down && !Drag.IsDragging)
            {
                // Compact objective chip: a tap expands / collapses the note (presentation only); any other press
                // collapses it and carries on as usual.
                if (Rules.HitObjective(signal.ScreenPosition, Hud.GetComponent<Canvas>().worldCamera))
                {
                    _gestureOnHud = true;
                    Rules.ToggleExpanded();
                    return;
                }
                Rules.Collapse();
                var action = Hud.Hit(signal.ScreenPosition);
                if (action != PuzzleHudAction.None)
                {
                    _gestureOnHud = true;
                    Hud.Press(action);
                    Perform(action);
                    return;
                }
            }
            if (_gestureOnHud)
            {
                if (signal.Phase == PointerPhase.Up || signal.Phase == PointerPhase.Cancel)
                    _gestureOnHud = false;
                return;
            }
            if (Hud.CompletionMode)
                return;
            Drag.HandlePointer(signal);
        }

        public void Perform(PuzzleHudAction action)
        {
            if (Hud.CompletionMode && (Completion.CurrentPhase != PuzzleCompletionPresenter.Phase.Confirmed
                || action != PuzzleHudAction.Restart && action != PuzzleHudAction.Next))
                return;
            switch (action)
            {
                case PuzzleHudAction.Rotate: Drag.RotateSelection(); break;
                case PuzzleHudAction.Fold: Drag.SelectModifier(ItemModifier.Fold); break;
                case PuzzleHudAction.Compress: Drag.SelectModifier(ItemModifier.Compress); break;
                case PuzzleHudAction.Undo: Undo(); break;
                case PuzzleHudAction.Restart: Restart(); break;
                case PuzzleHudAction.Next: NextLevel(); break;
            }
        }

        private void RefreshCompletion(bool edge = false)
        {
            foreach (var rule in Session.CurrentCompletion.Rules)
            {
                if (!edge && _ruleStates.TryGetValue(rule.RuleId, out var previous) && previous != rule.IsSatisfied)
                {
                    var cue = rule.IsSatisfied ? FeelCue.RuleSatisfied : FeelCue.RuleViolated;
                    _haptics?.Play(cue);
                    _audio?.Play(cue);
                }
                _ruleStates[rule.RuleId] = rule.IsSatisfied;
            }
            if (edge)
                CompletionCount++;
            var complete = Session.CurrentCompletion.IsComplete;
            Rules.Sync(Session.CurrentCompletion.Rules, !edge, Session.CurrentState);
            if (edge)
            {
                Drag.Cancel();
                _ruleDragKey = null;
                Staging.SetHover(-1, false);
                Rules.ClearForCompletion();
                Hud.SetCompletionMode(true);
                Completion.Begin(_completedLevelIds.Contains(Level.Id));
                _completedLevelIds.Add(Level.Id);
            }
            else if (!complete)
            {
                Completion.ResetForLevel(Board.Container, Board.ContainerFootprint, 0f, Hud,
                    Level.Id == "lv1-fit" && Board.Container != null);
                Hud.SetCompletionMode(false);
            }
            Drag.InteractionEnabled = !complete;
        }

        private void Update()
        {
            if (Session == null)
                return;
            Hud.SetRotateVisible(!Hud.CompletionMode && Drag.CanRotateSelection);
            Hud.SetModifierVisible(!Hud.CompletionMode && Drag.CanSelectModifier(ItemModifier.Fold),
                !Hud.CompletionMode && Drag.CanSelectModifier(ItemModifier.Compress));
            Hud.SetUndoEnabled(Session.UndoDepth > 0 && !Hud.CompletionMode);
            // States are immutable: recount the loose items only when the session moves to a new one.
            if (!ReferenceEquals(Session.CurrentState, _statusState))
            {
                _statusState = Session.CurrentState;
                var remaining = _statusState.GetItems(ItemLocationKind.SourceTray).Count
                    + _statusState.GetItems(ItemLocationKind.Staging).Count;
                if (remaining != _remaining)
                {
                    _remaining = remaining;
                    Hud.SetStatus(PuzzleRuleText.Remaining(remaining));
                }
            }
            if (!Hud.CompletionMode)
                UpdateRuleDragContext();
            var keyboard = Keyboard.current;
            if (!Hud.CompletionMode && keyboard != null && !Drag.IsDragging && keyboard.rKey.wasPressedThisFrame)
                Drag.RotateSelection();
            if (!Hud.CompletionMode && keyboard != null && keyboard.uKey.wasPressedThisFrame)
                Undo();
        }

        private void LateUpdate()
        {
            if (Camera != null && (!Mathf.Approximately(Camera.aspect, _framedAspect) || _framedScreen != new Vector2Int(Screen.width, Screen.height)))
                FrameCamera();
            if (Hud != null && Hud.AppliedSafeArea != _objectiveSafeArea)
                LayoutObjective();
            if (_table != null)
                _table.LayoutTray(Tray, Camera);
        }

        // Room between the header (inside the safe area) and the playable bed's back edge, in reference px; the rules
        // presenter picks the full note or the compact chip from it. Re-run on framing and safe-area changes only.
        private void LayoutObjective()
        {
            _objectiveSafeArea = Hud.AppliedSafeArea;
            if (Camera == null || Board == null || Camera.pixelWidth <= 0)
                return;
            var toReference = PuzzleHud.ReferenceWidth / Camera.pixelWidth;
            var bedTop = 0f;
            foreach (var frame in Board.CompartmentFrames())
                bedTop = Mathf.Max(bedTop, Camera.WorldToScreenPoint(frame.Origin).y);
            var bedFromTop = (Camera.pixelHeight - bedTop) * toReference;
            var safeTop = (1f - _objectiveSafeArea.yMax) * Camera.pixelHeight * toReference;
            Rules.LayoutObjective(bedFromTop - safeTop - PuzzleHud.HeaderBottom);
        }

        // Largest scale (within bounds) that lays every Source Tray item, at its display rotation, in one row.
        private static float FitTrayScale(PuzzleState state, float rowWidth)
        {
            float cells = 0f;
            var count = 0;
            foreach (var item in state.GetItems(ItemLocationKind.SourceTray))
            {
                item.State.TryGetFootprint(item.State.AllowedRotations[0], out var footprint);
                var width = 0;
                foreach (var cell in footprint.OccupiedCells)
                    width = Mathf.Max(width, cell.X + 1);
                cells += width;
                count++;
            }
            if (count == 0)
                return MaxTrayScale;
            // 0.995: an exact fit must not wrap the last item on float rounding.
            return Mathf.Clamp((rowWidth - (count - 1) * TrayItemGap) / cells * 0.995f, MinTrayScale, MaxTrayScale);
        }

        public void FrameCamera()
        {
            if (Completion != null && Completion.CurrentPhase != PuzzleCompletionPresenter.Phase.Idle)
                return;
            var bounds = new Bounds(Board.transform.position, Vector3.zero);
            var lid = Board.Lid;
            foreach (var renderer in Board.GetComponentsInChildren<Renderer>())
                if ((lid == null || !renderer.transform.IsChildOf(lid))
                    && renderer.name != "Contact Shadow" && renderer.name != "Soft Shadow")
                    bounds.Encapsulate(renderer.bounds);
            if (lid != null)
                bounds.Encapsulate(lid.position + Vector3.up * LidFrameHeight);
            foreach (var renderer in Tray.GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);
            if (Staging.Capacity > 0)
                foreach (var renderer in Staging.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(renderer.bounds);
            PuzzleCameraFraming.Frame(Camera, bounds, PuzzleHud.TopFraction(Camera.pixelWidth, Camera.pixelHeight),
                PuzzleHud.BottomFraction(Camera.pixelWidth, Camera.pixelHeight));
            _framedAspect = Camera.aspect;
            _framedScreen = new Vector2Int(Screen.width, Screen.height);
            _table?.LayoutTray(Tray, Camera);
            _table?.LayoutProps(Camera, Board.ContainerFootprint);
            LayoutObjective();
        }

        private void EnsureRuntimeObjects()
        {
            if (Board != null)
                return;
            Camera = Camera.main != null ? Camera.main : new GameObject("Gameplay Camera").AddComponent<Camera>();
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = PresentationKit.Linen;
            Board = new GameObject("Puzzle Board").AddComponent<PuzzleBoardPresenter>();
            Board.transform.SetParent(transform, false);
            Tray = new GameObject("Source Tray").AddComponent<PuzzleTrayPresenter>();
            Tray.transform.SetParent(transform, false);
            Tray.Configure(Board);
            Rules = new GameObject("Rules").AddComponent<PuzzleRulesPresenter>();
            Rules.transform.SetParent(transform, false);
            Staging = new GameObject("Staging").AddComponent<PuzzleStagingPresenter>();
            Staging.transform.SetParent(transform, false);
            Staging.Configure(Board);
            Drag = new GameObject("Drag Controller").AddComponent<PuzzleDragController>();
            Drag.transform.SetParent(transform, false);
            _haptics = gameObject.AddComponent<HapticsService>();
            _audio = gameObject.AddComponent<AudioCueService>();
            Drag.StepCommitted += step => RefreshCompletion(step.CompletionReached);
            Hud = new GameObject("Puzzle HUD").AddComponent<PuzzleHud>();
            Hud.transform.SetParent(transform, false);
            Hud.Build(uiFont, displayFont);
            Completion = new GameObject("Zip It Completion").AddComponent<PuzzleCompletionPresenter>();
            Completion.transform.SetParent(transform, false);
            Completion.Configure(Rules, _haptics, _audio);
            _pointer = GetComponent<PointerInteractor>();
            if (_pointer == null)
                _pointer = gameObject.AddComponent<PointerInteractor>();
            _pointer.PointerEvent += HandlePointer;
            _table = new GameObject("Packing Table").AddComponent<PackingTable>();
            _table.transform.SetParent(transform, false);
            _table.Build(materialTemplate, Camera, backdropSurface, backdropProps);
        }

        // Table under the suitcase; the compact Source Tray shell follows in LateUpdate (neither is under Board / Tray, so camera
        // framing ignores both).
        private void LayoutTable()
        {
            if (_table == null)
                return;
            var body = Board.ContainerFootprint;
            var surfaceY = Board.Container != null ? Board.ContainerBottomY : SuitcaseShell.SurfaceY;
            _table.Layout(body.center, surfaceY);
        }

        private void OnDestroy()
        {
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
        }
    }
}
