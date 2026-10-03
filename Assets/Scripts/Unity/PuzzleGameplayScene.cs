using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ZipTrip.Application;
using ZipTrip.Domain.Puzzle;

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
        public const float TrayItemGap = 0.3f;
        /// <summary>Distance from the suitcase interior's front edge to the loose items (clears wall + handle).</summary>
        public const float TrayGap = 1.5f;
        /// <summary>Gap between an authored container's front (handle included) and the loose items.</summary>
        public const float ContainerTrayGap = 0.55f;
        /// <summary>
        /// Height (world y above the hinge) of the open lid that camera framing reserves; the rest of the lid may rise
        /// into the top HUD band or past the screen edge, so the lid reads without shrinking the board.
        /// </summary>
        public const float LidFrameHeight = 3.5f;

        [SerializeField] private string[] levelIds = { "lv1-fit", "lv2-rotate" };
        [SerializeField] private int startLevel;
        [SerializeField] private Material materialTemplate;
        [SerializeField] private GoldenItemPrefabCatalog goldenCatalog;
        [SerializeField] private GameObject containerPrefab;
        [SerializeField] private Font uiFont;
        [SerializeField] private Font displayFont;

        private PointerInteractor _pointer;
        private bool _gestureOnHud;
        private float _framedAspect;
        private Vector2Int _framedScreen;
        private PackingTable _table;
        private string _ruleDragKey;
        private PuzzleLevel _fixture;
        private string _fixtureLabel;

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
            Present(LevelJsonLoaderV2.Load(json.text, PuzzleItemCatalog.Create()), $"Level {LevelIndex + 1}");
        }

        /// <summary>
        /// Loads an authored level that is not part of the shipped progression (test / staging fixtures, ZT-041).
        /// Restart reloads it; Next continues the shipped progression.
        /// </summary>
        public void LoadLevel(PuzzleLevel level, string label)
        {
            EnsureRuntimeObjects();
            _fixture = level ?? throw new ArgumentNullException(nameof(level));
            _fixtureLabel = label;
            Present(level, label);
        }

        private void Present(PuzzleLevel level, string label)
        {
            Level = level;
            Session = new PuzzleSession(Level);

            Board.UseContainer(containerPrefab);
            Board.Present(Level.Spec.Board, materialTemplate,
                item => PuzzleItemCatalog.ResolveGolden(goldenCatalog, item.Definition.Id, item.StateId)
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
                Tray.RowWidth = Mathf.Max(body.width + TrayOverhang, 4f);
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
            LayoutTable();
            Drag.InteractionEnabled = !Session.CurrentCompletion.IsComplete;

            Hud.SetLevel(label);
            Rules.Build(Level, Hud, Board, materialTemplate);
            Rules.Sync(Session.CurrentCompletion.Rules, false);
            _ruleDragKey = null;
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
                LoadLevel(_fixture, _fixtureLabel);
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
            if (Drag.IsDragging || !Session.Undo().StateChanged)
                return false;
            Drag.SyncPresenters();
            RefreshCompletion();
            return true;
        }

        /// <summary>Single pointer flow (ADR-0004): HUD buttons first, everything else to the drag controller.</summary>
        public void HandlePointer(PointerSignal signal)
        {
            if (signal.Phase == PointerPhase.Down && !Drag.IsDragging)
            {
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
            Drag.HandlePointer(signal);
        }

        public void Perform(PuzzleHudAction action)
        {
            switch (action)
            {
                case PuzzleHudAction.Rotate: Drag.RotateSelection(); break;
                case PuzzleHudAction.Undo: Undo(); break;
                case PuzzleHudAction.Restart: Restart(); break;
                case PuzzleHudAction.Next: NextLevel(); break;
            }
        }

        private void RefreshCompletion(bool edge = false)
        {
            if (edge)
                CompletionCount++;
            var complete = Session.CurrentCompletion.IsComplete;
            Rules.Sync(Session.CurrentCompletion.Rules, true);
            Hud.SetCompletionVisible(complete);
            Drag.InteractionEnabled = !complete;
        }

        private void Update()
        {
            if (Session == null)
                return;
            Hud.SetRotateVisible(!Hud.CompletionVisible && Drag.CanRotateSelection);
            Hud.SetUndoEnabled(Session.UndoDepth > 0 && !Hud.CompletionVisible);
            UpdateRuleDragContext();
            var keyboard = Keyboard.current;
            if (keyboard != null && !Drag.IsDragging && keyboard.rKey.wasPressedThisFrame)
                Drag.RotateSelection();
            if (keyboard != null && keyboard.uKey.wasPressedThisFrame)
                Undo();
        }

        private void LateUpdate()
        {
            if (Camera != null && (!Mathf.Approximately(Camera.aspect, _framedAspect) || _framedScreen != new Vector2Int(Screen.width, Screen.height)))
                FrameCamera();
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
            var bounds = new Bounds(Board.transform.position, Vector3.zero);
            var lid = Board.Lid;
            foreach (var renderer in Board.GetComponentsInChildren<Renderer>())
                if (lid == null || !renderer.transform.IsChildOf(lid))
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
            Drag.StepCommitted += step => RefreshCompletion(step.CompletionReached);
            Hud = new GameObject("Puzzle HUD").AddComponent<PuzzleHud>();
            Hud.transform.SetParent(transform, false);
            Hud.Build(uiFont, displayFont);
            _pointer = GetComponent<PointerInteractor>();
            if (_pointer == null)
                _pointer = gameObject.AddComponent<PointerInteractor>();
            _pointer.PointerEvent += HandlePointer;
            _table = new GameObject("Packing Table").AddComponent<PackingTable>();
            _table.transform.SetParent(transform, false);
            _table.Build(materialTemplate, Camera);
        }

        // Table under the suitcase, felt mat under the loose items' row (deliberately not under Board / Tray, so camera
        // framing ignores both).
        private void LayoutTable()
        {
            if (_table == null)
                return;
            var body = Board.ContainerFootprint;
            var surfaceY = Board.Container != null ? Board.ContainerBottomY : SuitcaseShell.SurfaceY;
            var origin = Tray.transform.localPosition;
            var depth = 0f;
            foreach (var view in Tray.ItemViews.Values)
            {
                var rows = 0;
                foreach (var cell in view.Footprint.OccupiedCells)
                    rows = Mathf.Max(rows, cell.Y + 1);
                depth = Mathf.Max(depth, rows * Tray.Scale);
            }
            var area = new Rect(origin.x, origin.z - Mathf.Max(depth, 1f), Tray.RowWidth, Mathf.Max(depth, 1f));
            _table.Layout(body.center, surfaceY, area);
        }

        private void OnDestroy()
        {
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
        }
    }
}
