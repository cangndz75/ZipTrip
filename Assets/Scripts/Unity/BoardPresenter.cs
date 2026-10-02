using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public sealed class BoardPresenter : MonoBehaviour
    {
        public const float SeamRestAlpha = 0.07f;
        public const float SeamDragAlpha = 0.3f;
        private const float SeamFadeSeconds = 0.12f;

        private readonly List<ItemView> _itemViews = new List<ItemView>();
        private readonly List<TrayEntry> _trayEntries = new List<TrayEntry>();
        private readonly List<UnityEngine.Object> _boardAssets = new List<UnityEngine.Object>();
        private readonly List<UnityEngine.Object> _trayAssets = new List<UnityEngine.Object>();
        private Transform _boardRoot;
        private Transform _itemsRoot;
        private Material _seamMaterial;
        private float _seamAlpha = SeamRestAlpha;
        private TrayFrame? _trayFrame;
        [SerializeField] private GoldenItemPrefabCatalog goldenItemPrefabs;
        [SerializeField] private Material runtimeMaterialTemplate;
        [SerializeField] private GameObject cabinVisualPrefab;
        [SerializeField] private Material cabinShellMaterial;
        [SerializeField] private Material cabinLiningMaterial;
        [SerializeField] private Material cabinAccentMaterial;
        public GameState PresentedState { get; private set; }
        public IReadOnlyList<ItemView> ItemViews => _itemViews.AsReadOnly();
        public IReadOnlyList<TrayEntry> TrayEntries => _trayEntries.AsReadOnly();
        public int ValidCellCount { get; private set; }
        public int BlockedCellCount { get; private set; }
        public Material RuntimeMaterialTemplate => runtimeMaterialTemplate;
        public Bounds ContainerVisualBounds { get; private set; }
        public float TrayScale { get; private set; }
        public bool DragEmphasis { get; private set; }
        public float SeamAlpha => _seamAlpha;

        public void ConfigureGoldenItemPrefabs(GoldenItemPrefabCatalog catalog)
        {
            goldenItemPrefabs = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public void ConfigureRuntimeMaterial(Material material)
        {
            runtimeMaterialTemplate = material ?? throw new ArgumentNullException(nameof(material));
        }

        public void ConfigureTray(TrayFrame frame) => _trayFrame = frame;

        public Bounds PresentationBounds
        {
            get
            {
                var renderers = GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    throw new InvalidOperationException("No presentation geometry exists.");
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);
                return bounds;
            }
        }

        public void Present(LevelDefinition level, GameState state) => Rebuild(level, state);

        public void Rebuild(LevelDefinition level, GameState state)
        {
            if (level == null || state == null)
                throw new ArgumentNullException();
            RemoveRoot(ref _boardRoot);
            RemoveRoot(ref _itemsRoot);
            DestroyAll(_boardAssets);
            _itemViews.Clear();
            _seamMaterial = null;
            ValidCellCount = 0;
            BlockedCellCount = 0;
            PresentedState = state;
            var bounds = FixedGameplayCamera.OuterBounds(state.Container.Mask);
            for (var y = bounds.yMin; y < bounds.yMax; y++)
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                    if (state.Container.Mask.IsValid(new Cell(x, y))) ValidCellCount++; else BlockedCellCount++;

            _boardRoot = new GameObject("Container").transform;
            _boardRoot.SetParent(transform, false);
            if (StringComparer.Ordinal.Equals(state.Container.Id, "cabin_std"))
                AddCabinVisual();
            else
                AddBackpackVisual(state.Container.Mask, bounds);
            AddSeams(state.Container.Mask);
            ContainerVisualBounds = RendererBounds(_boardRoot, new Bounds(
                FixedGameplayCamera.ContainerCenter(state.Container.Mask),
                new Vector3(bounds.width, 0.1f, bounds.height)));
            AddFloorShadow(ContainerVisualBounds);

            RefreshItems(level, state);
        }

        public void SetDragEmphasis(bool emphasized) => DragEmphasis = emphasized;

        private void Update()
        {
            if (_seamMaterial == null)
                return;
            var target = DragEmphasis ? SeamDragAlpha : SeamRestAlpha;
            if (Mathf.Approximately(_seamAlpha, target))
                return;
            _seamAlpha = Mathf.MoveTowards(_seamAlpha, target,
                (SeamDragAlpha - SeamRestAlpha) * Time.unscaledDeltaTime / SeamFadeSeconds);
            SetSeamAlpha(_seamAlpha);
        }

        private void SetSeamAlpha(float alpha)
        {
            var color = PresentationKit.DeepBlueGreen;
            color.a = alpha;
            _seamMaterial.SetColor(PresentationKit.BaseColorId, color);
            _seamMaterial.SetColor(PresentationKit.ColorId, color);
        }

        private void AddCabinVisual()
        {
            if (cabinVisualPrefab == null || cabinShellMaterial == null ||
                cabinLiningMaterial == null || cabinAccentMaterial == null)
                throw new InvalidOperationException("Accepted Cabin presentation assets are missing.");
            var cabin = Instantiate(cabinVisualPrefab, _boardRoot);
            cabin.name = "Accepted Cabin suitcase";
            cabin.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var renderer in cabin.GetComponentsInChildren<MeshRenderer>())
            {
                var name = renderer.name;
                renderer.sharedMaterial = name.Contains("FlatLining") ? cabinLiningMaterial :
                    name.Contains("ZipperLine") ? cabinAccentMaterial : cabinShellMaterial;
            }
        }

        // Presentation-only backpack around the unchanged canonical mask: padded terracotta body,
        // cream fabric lining exactly over valid cells, mustard zipper trim and an open lid flap.
        private void AddBackpackVisual(ContainerMask mask, RectInt cells)
        {
            if (cabinShellMaterial == null || cabinLiningMaterial == null)
                return;
            var inner = PresentationKit.Fillet(PresentationKit.MaskOutline(mask), 0.2f);
            var body = new Rect(cells.xMin - 0.42f, -cells.yMax - 0.42f, cells.width + 0.84f,
                cells.height + 0.84f);
            var outer = PresentationKit.OuterContour(inner, body, 1.25f);
            var bodyMaterial = Own(_boardAssets, PresentationKit.Colored(cabinShellMaterial,
                PresentationKit.Terracotta));
            var flapMaterial = Own(_boardAssets, PresentationKit.Colored(cabinShellMaterial,
                PresentationKit.Terracotta * 0.86f));
            var zipperMaterial = Own(_boardAssets, PresentationKit.Colored(cabinShellMaterial,
                PresentationKit.Mustard));

            AddMesh("Backpack lining", PresentationKit.FlatFan(inner, -0.012f), cabinLiningMaterial);
            AddMesh("Backpack padded body",
                PresentationKit.BackpackShell(inner, outer, -0.012f, 0.16f, 0.08f, -0.35f), bodyMaterial);
            AddMesh("Backpack zipper trim",
                PresentationKit.Ribbon(PresentationKit.Blend(inner, outer, 0.45f), 0.07f, 0.245f, true),
                zipperMaterial);
            AddMesh("Backpack open lid",
                PresentationKit.Slab(new Rect(cells.xMin + 0.3f, -cells.yMin + 0.5f, cells.width - 0.6f, 1.25f),
                    0.55f, -0.35f, -0.04f), flapMaterial);
        }

        private void AddSeams(ContainerMask mask)
        {
            if (runtimeMaterialTemplate == null)
                return;
            _seamMaterial = Own(_boardAssets, PresentationKit.Transparent(runtimeMaterialTemplate,
                PresentationKit.DeepBlueGreen));
            _seamAlpha = DragEmphasis ? SeamDragAlpha : SeamRestAlpha;
            SetSeamAlpha(_seamAlpha);
            AddMesh("Lining seams", PresentationKit.Seams(mask, 0.035f, 0.004f), _seamMaterial);
        }

        private void AddFloorShadow(Bounds container)
        {
            if (runtimeMaterialTemplate == null)
                return;
            var texture = Own(_boardAssets, PresentationKit.SoftRect(64, 0.3f));
            var material = Own(_boardAssets, PresentationKit.Transparent(runtimeMaterialTemplate,
                PresentationKit.Hex(0x31515D, 0.24f), texture));
            var rect = new Rect(container.min.x - 0.55f + 0.18f, container.min.z - 0.7f,
                container.size.x + 1.1f, container.size.z + 1.0f);
            AddMesh("Floor contact shadow", PresentationKit.Quad(rect, container.min.y - 0.01f), material);
        }

        private GameObject AddMesh(string name, Mesh mesh, Material material)
        {
            Own(_boardAssets, mesh);
            return PresentationKit.MeshObject(name, _boardRoot, mesh, material);
        }

        public void RefreshItems(LevelDefinition level, GameState state)
        {
            if (level == null || state == null)
                throw new ArgumentNullException();
            RemoveRoot(ref _itemsRoot);
            DestroyAll(_trayAssets);
            _itemViews.Clear();
            _trayEntries.Clear();
            PresentedState = state;
            _itemsRoot = new GameObject("Items").transform;
            _itemsRoot.SetParent(transform, false);

            foreach (var placement in state.Placements)
                AddItem(level.Items[placement.ItemId], placement.ShapeState, placement.Rotation,
                    false, new Vector3(placement.Anchor.X, 0f, -placement.Anchor.Y), 1f);

            // Presentation-only tray below the board; these positions are never gameplay anchors.
            var firstTray = _itemViews.Count;
            for (var i = 0; i < state.Tray.Count; i++)
            {
                var tray = state.Tray[i];
                var view = AddItem(level.Items[tray.ItemId], tray.ShapeState, tray.Rotation,
                    true, Vector3.zero, 1f);
                _trayEntries.Add(view.MeasureTrayEntry());
            }

            var frame = _trayFrame ?? FallbackTrayFrame(state.Container.Mask);
            var slots = GameplayLayout.LayoutTray(_trayEntries, frame, out var scale);
            TrayScale = scale;
            for (var i = 0; i < slots.Length; i++)
            {
                var view = _itemViews[firstTray + i];
                view.transform.position = slots[i].Origin;
                view.transform.localScale = Vector3.one * slots[i].Scale;
                view.HitPadding = slots[i].HitPadding;
                view.TraySlot = slots[i];
            }
            AddTraySurfaces(slots, frame);
        }

        // Used when no camera composition exists (isolated presenter tests).
        private static TrayFrame FallbackTrayFrame(ContainerMask mask)
        {
            var outer = FixedGameplayCamera.OuterBounds(mask);
            return new TrayFrame
            {
                Band = new TrayBand
                {
                    XMin = outer.xMin - 0.4f, XMax = outer.xMax + 0.4f,
                    ZTop = -outer.yMax - 0.4f, ZBottom = -outer.yMax - 4f
                },
                WorldPerDp = 0.0065f,
                MaxScale = 0.38f
            };
        }

        private void AddTraySurfaces(TraySlot[] slots, TrayFrame frame)
        {
            if (slots.Length == 0 || runtimeMaterialTemplate == null)
                return;
            var dp = frame.WorldPerDp;
            var shelf = slots[0].Card;
            for (var i = 1; i < slots.Length; i++)
                shelf = Rect.MinMaxRect(Mathf.Min(shelf.xMin, slots[i].Card.xMin),
                    Mathf.Min(shelf.yMin, slots[i].Card.yMin), Mathf.Max(shelf.xMax, slots[i].Card.xMax),
                    Mathf.Max(shelf.yMax, slots[i].Card.yMax));
            var margin = 16f * dp;
            shelf = Rect.MinMaxRect(shelf.xMin - margin, shelf.yMin - margin, shelf.xMax + margin,
                shelf.yMax + margin);
            AddTraySurface("Tray shelf", PresentationKit.RoundedRect(shelf, 28f * dp), -0.06f,
                PresentationKit.TrayShelf);
            for (var i = 0; i < slots.Length; i++)
            {
                var card = slots[i].Card;
                var lip = new Rect(card.x, card.y - 5f * dp, card.width, card.height);
                AddTraySurface("Tray slot lip", PresentationKit.RoundedRect(lip, 18f * dp), -0.05f,
                    PresentationKit.TrayShelf * 0.9f);
                AddTraySurface("Tray slot", PresentationKit.RoundedRect(card, 18f * dp), -0.04f,
                    PresentationKit.TrayCard);
            }
        }

        private void AddTraySurface(string name, List<Vector2> contour, float y, Color color)
        {
            var mesh = Own(_trayAssets, PresentationKit.FlatFan(contour, y));
            var go = PresentationKit.MeshObject(name, _itemsRoot, mesh, runtimeMaterialTemplate);
            var block = new MaterialPropertyBlock();
            color.a = 1f;
            block.SetColor(PresentationKit.BaseColorId, color);
            block.SetColor(PresentationKit.ColorId, color);
            go.GetComponent<MeshRenderer>().SetPropertyBlock(block);
        }

        public ItemView FindItemView(string itemId)
        {
            for (var i = 0; i < _itemViews.Count; i++)
                if (StringComparer.Ordinal.Equals(_itemViews[i].ItemId, itemId))
                    return _itemViews[i];
            return null;
        }

        private ItemView AddItem(ItemDefinition item, string shapeState, Rotation rotation,
            bool inTray, Vector3 position, float scale)
        {
            var visual = new GameObject(item.Id + (inTray ? " tray" : " placed"));
            visual.transform.SetParent(_itemsRoot, false);
            var view = visual.AddComponent<ItemView>();
            var color = item.Id == "sneaker" ? new Color(0.77f, 0.44f, 0.35f) :
                item.Id == "camera" ? new Color(0.19f, 0.32f, 0.37f) :
                item.Id == "bottle" ? new Color(0.84f, 0.67f, 0.35f) :
                new Color(0.62f, 0.72f, 0.7f);
            GameObject visualPrefab = null;
            if (!string.IsNullOrEmpty(item.VisualPrefabId))
            {
                if (goldenItemPrefabs == null)
                    throw new InvalidOperationException("Golden item prefab catalog is missing.");
                visualPrefab = goldenItemPrefabs.Resolve(item.VisualPrefabId, shapeState);
            }
            view.Present(item, shapeState, rotation, inTray, position, scale, color,
                visualPrefab, runtimeMaterialTemplate);
            _itemViews.Add(view);
            return view;
        }

        private static Bounds RendererBounds(Transform root, Bounds fallback)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return fallback;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            bounds.Encapsulate(fallback);
            return bounds;
        }

        private static T Own<T>(List<UnityEngine.Object> owner, T asset) where T : UnityEngine.Object
        {
            owner.Add(asset);
            return asset;
        }

        private static void DestroyAll(List<UnityEngine.Object> assets)
        {
            for (var i = 0; i < assets.Count; i++)
                if (assets[i] != null)
                    Destroy(assets[i]);
            assets.Clear();
        }

        private static void RemoveRoot(ref Transform root)
        {
            if (root == null)
                return;
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
            root = null;
        }

        private void OnDestroy()
        {
            DestroyAll(_boardAssets);
            DestroyAll(_trayAssets);
        }
    }
}
