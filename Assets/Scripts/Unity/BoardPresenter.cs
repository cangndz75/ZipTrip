using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public sealed class BoardPresenter : MonoBehaviour
    {
        private readonly List<ItemView> _itemViews = new List<ItemView>();
        private Transform _boardRoot;
        private Transform _itemsRoot;
        [SerializeField] private GoldenItemPrefabCatalog goldenItemPrefabs;
        [SerializeField] private Material runtimeMaterialTemplate;
        [SerializeField] private GameObject cabinVisualPrefab;
        [SerializeField] private Material cabinShellMaterial;
        [SerializeField] private Material cabinLiningMaterial;
        [SerializeField] private Material cabinAccentMaterial;
        public GameState PresentedState { get; private set; }
        public IReadOnlyList<ItemView> ItemViews => _itemViews.AsReadOnly();
        public int ValidCellCount { get; private set; }
        public int BlockedCellCount { get; private set; }
        public Material RuntimeMaterialTemplate => runtimeMaterialTemplate;

        public void ConfigureGoldenItemPrefabs(GoldenItemPrefabCatalog catalog)
        {
            goldenItemPrefabs = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public void ConfigureRuntimeMaterial(Material material)
        {
            runtimeMaterialTemplate = material ?? throw new ArgumentNullException(nameof(material));
        }
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
            _itemViews.Clear();
            ValidCellCount = 0;
            BlockedCellCount = 0;
            PresentedState = state;
            var bounds = FixedGameplayCamera.OuterBounds(state.Container.Mask);
            _boardRoot = new GameObject("Container cells").transform;
            _boardRoot.SetParent(transform, false);
            if (StringComparer.Ordinal.Equals(state.Container.Id, "cabin_std"))
                AddCabinVisual();
            for (var y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var valid = state.Container.Mask.IsValid(new Cell(x, y));
                    if (valid) ValidCellCount++; else BlockedCellCount++;
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = (valid ? "Valid " : "Blocked ") + x + "," + y;
                    tile.transform.SetParent(_boardRoot, false);
                    tile.transform.position = new Vector3(x + 0.5f, -0.06f, -y - 0.5f);
                    tile.transform.localScale = new Vector3(0.96f, 0.1f, 0.96f);
                    if (runtimeMaterialTemplate != null)
                        tile.GetComponent<Renderer>().sharedMaterial = runtimeMaterialTemplate;
                    var properties = new MaterialPropertyBlock();
                    var color = valid ? new Color(0.28f, 0.59f, 0.6f) : new Color(0.76f, 0.44f, 0.35f);
                    properties.SetColor("_BaseColor", color);
                    properties.SetColor("_Color", color);
                    tile.GetComponent<Renderer>().SetPropertyBlock(properties);
                    Destroy(tile.GetComponent<Collider>());
                }
            }

            RefreshItems(level, state);
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

        public void RefreshItems(LevelDefinition level, GameState state)
        {
            if (level == null || state == null)
                throw new ArgumentNullException();
            RemoveRoot(ref _itemsRoot);
            _itemViews.Clear();
            PresentedState = state;
            _itemsRoot = new GameObject("Items").transform;
            _itemsRoot.SetParent(transform, false);
            var bounds = FixedGameplayCamera.OuterBounds(state.Container.Mask);

            foreach (var placement in state.Placements)
                AddItem(level.Items[placement.ItemId], placement.ShapeState, placement.Rotation,
                    false, new Vector3(placement.Anchor.X, 0f, -placement.Anchor.Y), 1f);

            // Presentation-only sequence below the board; these positions are never gameplay anchors.
            for (var i = 0; i < state.Tray.Count; i++)
            {
                var tray = state.Tray[i];
                var x = bounds.xMin + (i + 0.5f) * bounds.width / state.Tray.Count - 0.5f;
                AddItem(level.Items[tray.ItemId], tray.ShapeState, tray.Rotation,
                    true, new Vector3(x, 0f, -bounds.yMax - 0.5f), 0.38f);
            }
        }

        public ItemView FindItemView(string itemId)
        {
            for (var i = 0; i < _itemViews.Count; i++)
                if (StringComparer.Ordinal.Equals(_itemViews[i].ItemId, itemId))
                    return _itemViews[i];
            return null;
        }

        private void AddItem(ItemDefinition item, string shapeState, Rotation rotation,
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
        }

        private static void RemoveRoot(ref Transform root)
        {
            if (root == null)
                return;
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
            root = null;
        }
    }
}
