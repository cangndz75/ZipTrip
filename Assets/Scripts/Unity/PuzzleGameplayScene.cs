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
        /// <summary>
        /// Height (world y above the hinge) of the open lid that camera framing reserves; the rest of the lid may rise
        /// into the top HUD band or past the screen edge, so the lid reads without shrinking the board.
        /// </summary>
        public const float LidFrameHeight = 3.5f;
        private const float SurfaceSize = 90f;

        [SerializeField] private string[] levelIds = { "lv1-fit", "lv2-rotate" };
        [SerializeField] private int startLevel;
        [SerializeField] private Material materialTemplate;
        [SerializeField] private GoldenItemPrefabCatalog goldenCatalog;

        private PointerInteractor _pointer;
        private bool _gestureOnHud;
        private float _framedAspect;
        private Vector2Int _framedScreen;
        private GameObject _surface;
        private Material _surfaceMaterial;
        private Texture2D _surfaceTexture;

        public PuzzleLevel Level { get; private set; }
        public PuzzleSession Session { get; private set; }
        public PuzzleBoardPresenter Board { get; private set; }
        public PuzzleTrayPresenter Tray { get; private set; }
        public PuzzleDragController Drag { get; private set; }
        public PuzzleHud Hud { get; private set; }
        public Camera Camera { get; private set; }
        /// <summary>Linen packing surface under the suitcase (excluded from camera framing).</summary>
        public GameObject Surface => _surface;
        public Material MaterialTemplate => materialTemplate;
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
            Level = LevelJsonLoaderV2.Load(json.text, PuzzleItemCatalog.Create());
            Session = new PuzzleSession(Level);

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
            // One centred row a little wider than the suitcase (keeps framing symmetric); wraps only if it cannot fit.
            Tray.RowWidth = Mathf.Max(boardWidth + SuitcaseShell.Wall * 2f + TrayOverhang, 4f);
            Tray.Scale = FitTrayScale(Level.InitialState, Tray.RowWidth);
            Tray.transform.localPosition = new Vector3((boardWidth - Tray.RowWidth) * 0.5f, 0f, -(boardDepth + TrayGap));
            PlaceSurface(boardWidth * 0.5f, -boardDepth);
            Drag.Initialize(Session, Board, Tray, Camera);
            Drag.InteractionEnabled = !Session.CurrentCompletion.IsComplete;

            Hud.SetLevel($"Level {LevelIndex + 1}");
            Hud.SetCompletionVisible(Session.CurrentCompletion.IsComplete);
            FrameCamera();
        }

        public void Restart() => LoadLevel(LevelIndex);

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
            Hud.SetCompletionVisible(complete);
            Drag.InteractionEnabled = !complete;
        }

        private void Update()
        {
            if (Session == null)
                return;
            Hud.SetRotateVisible(!Hud.CompletionVisible && Drag.CanRotateSelection);
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
            var lid = Board.Shell != null ? Board.Shell.Lid : null;
            foreach (var renderer in Board.GetComponentsInChildren<Renderer>())
                if (lid == null || !renderer.transform.IsChildOf(lid))
                    bounds.Encapsulate(renderer.bounds);
            if (lid != null)
                bounds.Encapsulate(lid.position + Vector3.up * LidFrameHeight);
            foreach (var renderer in Tray.GetComponentsInChildren<Renderer>())
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
            Drag = new GameObject("Drag Controller").AddComponent<PuzzleDragController>();
            Drag.transform.SetParent(transform, false);
            Drag.StepCommitted += step => RefreshCompletion(step.CompletionReached);
            Hud = new GameObject("Puzzle HUD").AddComponent<PuzzleHud>();
            Hud.transform.SetParent(transform, false);
            Hud.Build();
            _pointer = GetComponent<PointerInteractor>();
            if (_pointer == null)
                _pointer = gameObject.AddComponent<PointerInteractor>();
            _pointer.PointerEvent += HandlePointer;
        }

        // Large linen quad just below the board plane; deliberately not under Board / Tray so framing ignores it.
        private void PlaceSurface(float centerX, float centerZ)
        {
            if (_surface == null)
            {
                var template = PresentationKit.TemplateOrFallback(materialTemplate);
                if (template == null)
                    return;
                _surfaceTexture = PresentationKit.LinenTexture(128, 1709);
                _surfaceMaterial = PresentationKit.Matte(template, PresentationKit.Linen, _surfaceTexture, 0.05f);
                _surfaceMaterial.SetTextureScale(PresentationKit.BaseMapId, new Vector2(SurfaceSize / 2.2f, SurfaceSize / 2.2f));
                var half = SurfaceSize * 0.5f;
                _surface = PresentationKit.MeshObject("Packing Surface", transform,
                    PresentationKit.Quad(new Rect(-half, -half, SurfaceSize, SurfaceSize), SuitcaseShell.SurfaceY), _surfaceMaterial);
            }
            _surface.transform.localPosition = new Vector3(centerX, 0f, centerZ);
        }

        private void OnDestroy()
        {
            if (_pointer != null)
                _pointer.PointerEvent -= HandlePointer;
            if (_surface != null)
                Destroy(_surface.GetComponent<MeshFilter>().sharedMesh);
            if (_surfaceMaterial != null)
                Destroy(_surfaceMaterial);
            if (_surfaceTexture != null)
                Destroy(_surfaceTexture);
        }
    }
}
