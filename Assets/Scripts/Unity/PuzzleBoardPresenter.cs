using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // Structural ADR-0006 board presentation (ZT-038). Reads BoardSpec / PuzzleState only and never decides
    // occupancy, support, access, rules, completion or layers. Sync is explicit: call Sync(session.CurrentState)
    // after a state-changing step. Items outside the suitcase (tray, staging, nested, destination) have no board view.
    // ZT-040B: the compartments sit inside a presentation-only SuitcaseShell; cell guides stay hidden unless the drag
    // controller reveals them around a candidate.
    // ZT-040C: with a container prefab (UseContainer) the board is seated inside authored container art instead: the
    // art is scaled/placed around the unchanged board (ContainerFit), never the other way round. The procedural
    // SuitcaseShell remains the fallback when no prefab is given (tests, debug).
    public sealed class PuzzleBoardPresenter : MonoBehaviour
    {
        /// <summary>Container models face +Z (handle side); the board's near side is -Z, towards the camera.</summary>
        public const float ContainerYaw = 180f;
        /// <summary>Lining margin (cells) kept between the board and the authored interior walls.</summary>
        public const float ContainerPadding = 0.15f;
        /// <summary>Spare interior width (cells, per side) at or above which a padded filler closes the gap.</summary>
        public const float FillerMinGap = 0.5f;
        public const float FillerHeight = 0.42f;
        public static readonly Color ContainerLining = new Color(0.10f, 0.27f, 0.28f);
        /// <summary>
        /// ZT-040C.1 gameplay open pose (local X). The authored -77.08 is nearly edge-on to the 75-degree camera; at -90
        /// the lining and straps face it. Smaller angles tilt the lid over the cavity and show its exterior instead.
        /// </summary>
        public const float GameplayLidOpenDegrees = -90f;

        // ZT-040D: paper-white held silhouette, strong enough to read over felt, table and the footprint glow.
        public static readonly Color GhostColor = new Color(0.97f, 0.95f, 0.9f, 0.5f);
        // Functional fallback-block colours (not final art): palette family plus two muted extras to reduce collisions.
        private static readonly Color[] Palette =
        {
            PresentationKit.Teal, PresentationKit.Terracotta, PresentationKit.Mustard, PresentationKit.DeepBlueGreen,
            PresentationKit.Hex(0x7FA88B), PresentationKit.Hex(0xB98AA0)
        };

        private readonly Dictionary<string, PuzzleCompartmentView> _compartments = new Dictionary<string, PuzzleCompartmentView>();
        private readonly Dictionary<string, PuzzleItemView> _items = new Dictionary<string, PuzzleItemView>();
        private readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>();
        private Func<PuzzleItem, GameObject> _visualResolver;
        private Material _template;
        private Material _ghostMaterial;
        private Material _guideMaterial;
        private Texture2D _guideTexture;
        private SuitcaseShell _shell;
        private GameObject _containerPrefab;
        private ContainerRig _container;
        private Transform _containerDecor;
        private readonly List<UnityEngine.Object> _decorAssets = new List<UnityEngine.Object>();

        public BoardSpec Board { get; private set; }
        public PuzzleState PresentedState { get; private set; }
        public bool XRayEnabled { get; private set; }
        public IReadOnlyDictionary<string, PuzzleCompartmentView> Compartments => _compartments;
        /// <summary>Board views keyed by item instance id (suitcase-resident items only).</summary>
        public IReadOnlyDictionary<string, PuzzleItemView> ItemViews => _items;
        /// <summary>Procedural ZT-040B suitcase; null while an authored container is used.</summary>
        public SuitcaseShell Shell => _shell;
        /// <summary>Authored container rig (ZT-040C); null on the procedural fallback.</summary>
        public ContainerRig Container => _container;
        /// <summary>Lid of whichever container is shown (camera framing reserves only its lower part).</summary>
        public Transform Lid => _container != null ? _container.Lid : _shell != null ? _shell.Lid : null;
        /// <summary>Seated interior rectangle (board-local contour space x, z) of the shown container.</summary>
        public Rect ContainerInterior { get; private set; }
        /// <summary>Board-local XZ footprint of the container body (lid excluded), and the height it rests on.</summary>
        public Rect ContainerFootprint { get; private set; }
        public float ContainerBottomY { get; private set; }
        public float ContainerScale { get; private set; } = 1f;

        /// <summary>Authored container art for the next Present (null = procedural ZT-040B suitcase).</summary>
        public void UseContainer(GameObject containerPrefab) => _containerPrefab = containerPrefab;

        /// <param name="materialTemplate">Shared runtime material (e.g. PuzzleGameplayScene.MaterialTemplate); may be null.</param>
        /// <param name="visualResolver">Optional prefab lookup per item; null or a null result uses footprint blocks.</param>
        /// <param name="compartmentOrigins">Optional scene-authored local origins per compartment id; missing ids use the
        /// deterministic side-by-side fallback (PuzzleBoardLayout), which is not a production layout contract.</param>
        public void Present(BoardSpec board, Material materialTemplate = null, Func<PuzzleItem, GameObject> visualResolver = null,
            IReadOnlyDictionary<string, Vector3> compartmentOrigins = null)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            _template = materialTemplate;
            _visualResolver = visualResolver;
            if (_ghostMaterial == null && materialTemplate != null)
                _ghostMaterial = PresentationKit.Transparent(materialTemplate, GhostColor);
            var guideTemplate = PresentationKit.TemplateOrFallback(materialTemplate);
            if (_guideMaterial == null && guideTemplate != null)
            {
                _guideTexture = PresentationKit.SoftRect(64, 0.22f);
                _guideMaterial = PresentationKit.Transparent(guideTemplate, PresentationKit.WithAlpha(PresentationKit.Paper, 0.2f), _guideTexture);
            }

            // Deactivate as well as destroy: Destroy is deferred to the end of the frame, and the old level must not
            // count towards bounds (camera framing) measured in this frame.
            foreach (var view in _compartments.Values)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            _compartments.Clear();
            _items.Clear();
            PresentedState = null;

            var origins = PuzzleBoardLayout.CompartmentOrigins(board);
            foreach (var compartment in board.Compartments)
            {
                var view = new GameObject("Compartment " + compartment.Id).AddComponent<PuzzleCompartmentView>();
                view.transform.SetParent(transform, false);
                view.transform.localPosition = compartmentOrigins != null && compartmentOrigins.TryGetValue(compartment.Id, out var authored)
                    ? authored : origins[compartment.Id];
                view.Build(compartment, _guideMaterial);
                _compartments.Add(compartment.Id, view);
            }
            BuildShell(board);
        }

        // Suitcase around the union of the compartment rectangles; interior cells that are not playable (masked cells,
        // gaps between side-by-side compartments) get padded fillers so only real cells show lining.
        private void BuildShell(BoardSpec board)
        {
            if (_shell == null && _containerPrefab == null)
            {
                _shell = new GameObject("Suitcase").AddComponent<SuitcaseShell>();
                _shell.transform.SetParent(transform, false);
            }
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var view in _compartments.Values)
            {
                var origin = view.transform.localPosition;
                minX = Mathf.Min(minX, origin.x);
                maxX = Mathf.Max(maxX, origin.x + view.Width);
                minZ = Mathf.Min(minZ, origin.z - view.Height);
                maxZ = Mathf.Max(maxZ, origin.z);
            }
            var fillers = new List<Rect>();
            for (var z = Mathf.FloorToInt(minZ); z < Mathf.CeilToInt(maxZ); z++)
                for (var x = Mathf.FloorToInt(minX); x < Mathf.CeilToInt(maxX); x++)
                    if (!IsPlayable(board, x + 0.5f, z + 0.5f))
                        fillers.Add(new Rect(x, z, 1f, 1f));
            var interior = new Rect(minX, minZ, maxX - minX, maxZ - minZ);
            if (_containerPrefab != null)
            {
                BuildContainer(interior, fillers);
                return;
            }
            _shell.Build(interior, fillers, _template);
            ContainerInterior = _shell.Interior;
            ContainerFootprint = _shell.Body;
            ContainerBottomY = SuitcaseShell.SurfaceY;
            ContainerScale = 1f;
        }

        // Seats the authored container around the board (uniform scale, board centred, interior floor on the lining
        // height) and adds presentation-only padding where the interior is wider than the board or cells are masked.
        private void BuildContainer(Rect board, List<Rect> fillers)
        {
            if (_container == null || _container.Root == null)
            {
                var instance = Instantiate(_containerPrefab, transform, false);
                instance.name = "Container " + _containerPrefab.name;
                _container = ContainerRig.Bind(instance.transform);
                _container.SetLidOpenPose(Quaternion.Euler(GameplayLidOpenDegrees, 0f, 0f));
                // ZT-040D: the standing lid would throw one hard shadow across the whole lining under the top-left key.
                foreach (var renderer in _container.Lid.GetComponentsInChildren<Renderer>())
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _container.SetLidClosed(false);
            }
            var root = _container.Root;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            var min = _container.InteriorMinLocal;
            var max = _container.InteriorMaxLocal;
            var fit = ContainerFit.Seat(board, min, max, ContainerPadding, SuitcaseShell.LiningY, ContainerYaw);
            root.localPosition = fit.Position;
            root.localRotation = fit.Rotation;
            root.localScale = Vector3.one * fit.Scale;
            ContainerScale = fit.Scale;
            ContainerInterior = fit.Interior(min, max);

            var body = new Bounds();
            var any = false;
            foreach (var renderer in _container.Base.GetComponentsInChildren<Renderer>())
            {
                var local = new Bounds(transform.InverseTransformPoint(renderer.bounds.center), renderer.bounds.size);
                if (any) body.Encapsulate(local); else body = local;
                any = true;
            }
            ContainerFootprint = Rect.MinMaxRect(body.min.x, body.min.z, body.max.x, body.max.z);
            ContainerBottomY = body.min.y;

            ClearContainerDecor();
            var template = PresentationKit.TemplateOrFallback(_template);
            if (template == null)
                return;
            _containerDecor = new GameObject("Container Presentation").transform;
            _containerDecor.SetParent(transform, false);
            var left = board.xMin - ContainerInterior.xMin;
            var right = ContainerInterior.xMax - board.xMax;
            if (left >= FillerMinGap)
                fillers.Add(new Rect(ContainerInterior.xMin, ContainerInterior.yMin, left, ContainerInterior.height));
            if (right >= FillerMinGap)
                fillers.Add(new Rect(board.xMax, ContainerInterior.yMin, right, ContainerInterior.height));
            if (fillers.Count > 0)
            {
                var quilt = OwnDecor(PresentationKit.QuiltTexture(128, 2, 4021));
                var padding = OwnDecor(PresentationKit.Matte(template, PresentationKit.Shade(ContainerLining, 1.25f), quilt, 0.06f));
                foreach (var filler in fillers)
                    AddDecor("Padded Filler", PresentationKit.Slab(Inset(filler, 0.05f), 0.16f, SuitcaseShell.LiningY, FillerHeight), padding);
            }
            // ZT-040D grounding: tight dark core + wide soft falloff, offset away from the top-left key.
            var footprint = ContainerFootprint;
            var core = OwnDecor(PresentationKit.SoftRect(64, 0.2f));
            var soft = OwnDecor(PresentationKit.SoftRect(64, 0.45f));
            AddDecor("Contact Shadow", PresentationKit.Quad(new Rect(footprint.xMin - 0.08f, footprint.yMin - 0.18f,
                footprint.width + 0.2f, footprint.height + 0.2f), ContainerBottomY + 0.006f),
                OwnDecor(PresentationKit.Transparent(template, PresentationKit.WithAlpha(PresentationKit.Shadow, 0.5f), core)));
            AddDecor("Soft Shadow", PresentationKit.Quad(new Rect(footprint.xMin - 0.55f, footprint.yMin - 1.1f,
                footprint.width + 1.5f, footprint.height + 1.3f), ContainerBottomY + 0.004f),
                OwnDecor(PresentationKit.Transparent(template, PresentationKit.WithAlpha(PresentationKit.Shadow, 0.3f), soft)));
            // Interior depth: soft occlusion where the lining floor meets the walls (darkens edges, clear centre).
            var occlusion = OwnDecor(InnerOcclusion(64, 0.16f));
            AddDecor("Lining Occlusion", PresentationKit.Quad(ContainerInterior, SuitcaseShell.LiningY + 0.004f),
                OwnDecor(PresentationKit.Transparent(template, PresentationKit.WithAlpha(PresentationKit.Shadow, 0.55f), occlusion)));
        }

        // Alpha 1 at the rim of the texture falling to 0 within `band` (fraction of the size) - an inner-edge glow mask.
        private static Texture2D InnerOcclusion(int size, float band)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Lining occlusion" };
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var d = Mathf.Min(Mathf.Min(x + 0.5f, size - x - 0.5f), Mathf.Min(y + 0.5f, size - y - 0.5f)) / size;
                    var a = 1f - Mathf.Clamp01(d / band);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            texture.Apply(false, true);
            return texture;
        }

        private void AddDecor(string name, Mesh mesh, Material material) =>
            PresentationKit.MeshObject(name, _containerDecor, OwnDecor(mesh), material);

        private T OwnDecor<T>(T asset) where T : UnityEngine.Object
        {
            _decorAssets.Add(asset);
            return asset;
        }

        private void ClearContainerDecor()
        {
            if (_containerDecor != null)
            {
                _containerDecor.gameObject.SetActive(false);
                Destroy(_containerDecor.gameObject);
                _containerDecor = null;
            }
            foreach (var asset in _decorAssets)
                if (asset != null)
                    Destroy(asset);
            _decorAssets.Clear();
        }

        private static Rect Inset(Rect rect, float amount) =>
            new Rect(rect.xMin + amount, rect.yMin + amount, rect.width - 2f * amount, rect.height - 2f * amount);

        private bool IsPlayable(BoardSpec board, float x, float z)
        {
            foreach (var view in _compartments.Values)
            {
                var local = new Vector3(x, 0f, z) - view.transform.localPosition;
                var column = new Cell(Mathf.FloorToInt(local.x), Mathf.FloorToInt(-local.z));
                if (board.TryGetCompartment(view.CompartmentId, out var compartment) && compartment.Mask.IsValid(column))
                    return true;
            }
            return false;
        }

        /// <summary>Reveals placement guides for the given cells of one compartment and hides every other guide.</summary>
        public void ShowGuides(string compartmentId, ICollection<Cell> cells)
        {
            foreach (var view in _compartments.Values)
                view.ShowGuides(view.CompartmentId == compartmentId ? cells : null);
        }

        public void HideGuides()
        {
            foreach (var view in _compartments.Values)
                view.HideGuides();
        }

        public void Sync(PuzzleState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (Board == null || !ReferenceEquals(state.Spec.Board, Board) && !state.Spec.Board.Equals(Board))
                throw new InvalidOperationException("Present the state's BoardSpec before syncing.");

            var live = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in state.GetItems(ItemLocationKind.Suitcase))
            {
                live.Add(item.InstanceId);
                if (!_items.TryGetValue(item.InstanceId, out var view))
                {
                    view = new GameObject("Item " + item.InstanceId).AddComponent<PuzzleItemView>();
                    _items.Add(item.InstanceId, view);
                }
                view.Bind(item, _compartments[item.Location.Placement.Compartment].ItemsRoot,
                    ResolveVisual(item), _template, ColorFor(item.Definition.Id));
            }

            var stale = new List<string>();
            foreach (var id in _items.Keys)
                if (!live.Contains(id))
                    stale.Add(id);
            foreach (var id in stale)
            {
                Destroy(_items[id].gameObject);
                _items.Remove(id);
            }

            PresentedState = state;
            ApplyXRay();
        }

        /// <summary>World frames of the compartment roots, for pointer projection.</summary>
        public IReadOnlyList<CompartmentFrame> CompartmentFrames()
        {
            var frames = new List<CompartmentFrame>();
            foreach (var view in _compartments.Values)
                frames.Add(new CompartmentFrame(view.CompartmentId, view.transform.position, view.Width, view.Height));
            frames.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return frames.AsReadOnly();
        }

        internal Material Template => _template;

        internal Material GhostMaterial => _ghostMaterial;

        internal GameObject ResolveVisual(PuzzleItem item) => _visualResolver?.Invoke(item);

        /// <summary>Lower-layer readability: ghosts every item above layer 0. Rendering only.</summary>
        public void SetXRayEnabled(bool enabled)
        {
            XRayEnabled = enabled;
            ApplyXRay();
        }

        private void ApplyXRay()
        {
            foreach (var view in _items.Values)
                view.SetGhost(XRayEnabled && view.Placement.Layer > 0, _ghostMaterial);
        }

        // Fallback-block colour per definition, assigned in first-seen order (deterministic for a given sync order) so
        // up to six definitions never share a colour. Instances of one definition share it.
        internal Color ColorFor(string definitionId)
        {
            if (!_colors.TryGetValue(definitionId, out var color))
                _colors.Add(definitionId, color = Palette[_colors.Count % Palette.Length]);
            return color;
        }

        private void OnDestroy()
        {
            if (_ghostMaterial != null)
                Destroy(_ghostMaterial);
            if (_guideMaterial != null)
                Destroy(_guideMaterial);
            if (_guideTexture != null)
                Destroy(_guideTexture);
            ClearContainerDecor();
        }
    }
}
