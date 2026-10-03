using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using ZipTrip.Application;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // ZT-039 functional harness (development only, not level content). Builds a typed Pack scenario on the ADR-0006
    // runtime: Source Tray packing, a rotation-needing rod, twin instances, a masked cell, a pre-placed base to stack on
    // (layer 1) and an optional golden sweater. Keys: R rotate while dragging, Esc / right click cancel, X X-Ray, U undo.
    public sealed class PuzzleHarness : MonoBehaviour
    {
        private const float CameraPitch = 75f; // Same pitch as FixedGameplayCamera.
        private const float CameraDistance = 12f;
        private const float FrameMargin = 0.5f;

        [SerializeField] private Material materialTemplate;
        [SerializeField] private GoldenItemPrefabCatalog goldenCatalog;
        private float _framedAspect;

        public PuzzleSession Session { get; private set; }
        public PuzzleBoardPresenter Board { get; private set; }
        public PuzzleTrayPresenter Tray { get; private set; }
        public PuzzleDragController Drag { get; private set; }
        public Camera Camera { get; private set; }

        public void Configure(Material template, GoldenItemPrefabCatalog catalog)
        {
            materialTemplate = template;
            goldenCatalog = catalog;
        }

        private void Start() => Build();

        public void Build()
        {
            if (Session != null)
                return;
            var level = CreateLevel();
            Session = new PuzzleSession(level);

            Board = new GameObject("Puzzle Board").AddComponent<PuzzleBoardPresenter>();
            Board.transform.SetParent(transform, false);
            Board.Present(level.Spec.Board, materialTemplate, ResolveGolden);

            var boardDepth = level.Spec.Board.Compartments.Max(c => c.Height);
            Tray = new GameObject("Source Tray").AddComponent<PuzzleTrayPresenter>();
            Tray.transform.SetParent(transform, false);
            Tray.transform.localPosition = new Vector3(0f, 0f, -(boardDepth + 1.5f));
            Tray.Configure(Board);

            Camera = Camera.main != null ? Camera.main : new GameObject("Harness Camera").AddComponent<Camera>();
            var pointer = gameObject.GetComponent<PointerInteractor>();
            if (pointer == null)
                pointer = gameObject.AddComponent<PointerInteractor>();
            Drag = new GameObject("Drag Controller").AddComponent<PuzzleDragController>();
            Drag.transform.SetParent(transform, false);
            Drag.Initialize(Session, Board, Tray, Camera, pointer);
            FrameCamera();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || Board == null)
                return;
            if (keyboard.xKey.wasPressedThisFrame)
                Board.SetXRayEnabled(!Board.XRayEnabled);
            if (keyboard.uKey.wasPressedThisFrame && !Drag.IsDragging && Session.Undo().StateChanged)
                Drag.SyncPresenters();
        }

        private void LateUpdate()
        {
            if (Camera != null && !Mathf.Approximately(Camera.aspect, _framedAspect))
                FrameCamera();
        }

        // Presentation-level framing for the harness only: board plus a tray strip wide enough for every tray item.
        public void FrameCamera()
        {
            var bounds = new Bounds(Board.transform.position, Vector3.zero);
            foreach (var renderer in GetComponentsInChildren<Renderer>())
                bounds.Encapsulate(renderer.bounds);
            Camera.orthographic = true;
            Camera.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            Camera.transform.position = bounds.center - Camera.transform.forward * CameraDistance;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 40f;
            var size = 1f;
            for (var i = 0; i < 8; i++)
            {
                var corner = Camera.transform.InverseTransformPoint(new Vector3(
                    (i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z));
                size = Mathf.Max(size, (Mathf.Abs(corner.x) + FrameMargin) / Camera.aspect, Mathf.Abs(corner.y) + FrameMargin);
            }
            Camera.orthographicSize = size;
            _framedAspect = Camera.aspect;
        }

        private GameObject ResolveGolden(PuzzleItem item) =>
            goldenCatalog != null && item.Definition.Id == "sweater" ? goldenCatalog.Resolve("PF_Item_Sweater", item.StateId) : null;

        private static readonly Rotation[] All = { Rotation.Degrees0, Rotation.Degrees90, Rotation.Degrees180, Rotation.Degrees270 };

        private static IEnumerable<Cell> Rect(int w, int h) =>
            from y in Enumerable.Range(0, h) from x in Enumerable.Range(0, w) select new Cell(x, y);

        private static ItemSpec Block(string id, int w, int h) =>
            new ItemSpec(id, "default", new[] { new ItemStateSpec("default", new ItemShape(Rect(w, h)), 1, All) });

        private static PuzzleItem InTray(string id, ItemSpec spec) =>
            new PuzzleItem(id, spec, spec.DefaultStateId, ObjectiveRole.Required, ItemLocation.SourceTray);

        public static PuzzleLevel CreateLevel()
        {
            var board = new BoardSpec(new[]
            {
                new Compartment("main", 5, 4, 2, Rect(5, 4).Where(c => c != new Cell(4, 0))),
                new Compartment("pocket", 2, 2, 1, Rect(2, 2))
            });
            var dot = Block("dot", 1, 1);
            var sweater = new ItemSpec("sweater", "open", new[] { new ItemStateSpec("open", new ItemShape(Rect(3, 3)), 1, new[] { Rotation.Degrees0 }) });
            var state = new PuzzleState(new PuzzleSpec(board, 2), new[]
            {
                new PuzzleItem("base", Block("base", 2, 2), "default", ObjectiveRole.Required,
                    ItemLocation.InSuitcase(new Placement("main", new Cell(0, 0), 0, Rotation.Degrees0))),
                InTray("book", Block("book", 2, 1)),
                InTray("rod", Block("rod", 3, 1)),
                InTray("dot-a", dot),
                InTray("dot-b", dot),
                InTray("sweater", sweater)
            });
            return new PuzzleLevel("zt039-harness", state, new RuleSet(board, null),
                PuzzleObjective.Pack(new[] { "base", "book", "rod", "dot-a", "dot-b", "sweater" }));
        }
    }
}
