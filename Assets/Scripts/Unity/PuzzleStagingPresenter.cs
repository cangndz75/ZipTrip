using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // ZT-041 limited staging presentation (ADR-0006 D11): one felt parking pad per staging slot (capacity comes from
    // PuzzleSpec.StagingCapacity), staged items parked on their slot, slot hit targets and a hover state for the drag
    // controller. Reads PuzzleState only: it never owns occupancy, never decides legality (PuzzleTransitions does) and
    // has no undo logic of its own. Deliberately separate from the Source Tray presenter: staging is a limited,
    // temporary holding area, the tray is the unplaced pool. Hidden entirely when the capacity is 0.
    public sealed class PuzzleStagingPresenter : MonoBehaviour
    {
        public const float PadSize = 2.7f;
        public const float PadGap = 0.35f;
        /// <summary>Clearance between a parked item and its pad edge (cells).</summary>
        public const float ParkMargin = 0.32f;
        // Muted sage parking felt with darker stitching: related to the suitcase lining, clearly not the taupe Source
        // Tray mat, so "set aside temporarily" reads differently from "not packed yet".
        public static readonly Color PadFelt = PresentationKit.Hex(0xA9B5A0);
        public static readonly Color PadStitch = PresentationKit.Hex(0x5E6B58);
        public static readonly Color ValidTint = PresentationKit.Hex(0x9CC9A4);
        public static readonly Color InvalidTint = PresentationKit.Hex(0xD89A86);

        private readonly Dictionary<string, PuzzleItemView> _items = new Dictionary<string, PuzzleItemView>(StringComparer.Ordinal);
        private readonly List<Transform> _pads = new List<Transform>();
        private readonly List<Material> _padMaterials = new List<Material>();
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        private PuzzleBoardPresenter _board;

        public int Capacity { get; private set; }
        /// <summary>Slot currently highlighted by a drag (-1 = none) and whether that drop would be accepted.</summary>
        public int HoverSlot { get; private set; } = -1;
        public bool HoverValid { get; private set; }
        /// <summary>Staged item views keyed by instance id.</summary>
        public IReadOnlyDictionary<string, PuzzleItemView> ItemViews => _items;
        public IReadOnlyList<Transform> Pads => _pads;
        /// <summary>World width of the pad row (0 when hidden).</summary>
        public float RowWidth => Capacity > 0 ? Capacity * PadSize + (Capacity - 1) * PadGap : 0f;

        public void Configure(PuzzleBoardPresenter board) => _board = board ?? throw new ArgumentNullException(nameof(board));

        /// <summary>Rebuilds the pads for a level. Capacity 0 hides the whole presenter.</summary>
        public void Build(int capacity, Material template)
        {
            Clear();
            Capacity = Mathf.Max(0, capacity);
            gameObject.SetActive(Capacity > 0);
            if (Capacity == 0)
                return;
            template = PresentationKit.TemplateOrFallback(template);
            var felt = Own(PresentationKit.FeltTexture(64, 5153));
            var stitch = Own(PresentationKit.Matte(template, PadStitch, null, 0.1f));
            var shadowTexture = Own(PresentationKit.SoftRect(64, 0.35f));
            var shadow = Own(PresentationKit.Transparent(template, PresentationKit.WithAlpha(PresentationKit.Shadow, 0.26f), shadowTexture));
            for (var slot = 0; slot < Capacity; slot++)
            {
                var pad = new GameObject("Staging Slot " + slot).transform;
                pad.SetParent(transform, false);
                pad.localPosition = new Vector3(slot * (PadSize + PadGap), 0f, 0f);
                var rect = new Rect(0f, -PadSize, PadSize, PadSize);
                var material = Own(PresentationKit.Matte(template, PadFelt, felt, 0.03f));
                _padMaterials.Add(material);
                PresentationKit.MeshObject("Shadow", pad, Own(PresentationKit.Quad(new Rect(rect.xMin - 0.12f, rect.yMin - 0.22f,
                    rect.width + 0.3f, rect.height + 0.3f), -PackingTable.MatThickness + 0.002f)), shadow);
                PresentationKit.MeshObject("Pad", pad, Own(PresentationKit.Slab(rect, 0.32f, -PackingTable.MatThickness, 0f)), material)
                    .GetComponent<MeshRenderer>().receiveShadows = true;
                var inset = new Rect(rect.xMin + 0.2f, rect.yMin + 0.2f, rect.width - 0.4f, rect.height - 0.4f);
                PresentationKit.MeshObject("Stitch", pad, Own(PresentationKit.DashedPath(PresentationKit.RoundedRect(inset, 0.22f, 6),
                    0.14f, 0.1f, 0.04f, 0.003f, true)), stitch);
                _pads.Add(pad);
            }
            SetHover(-1, false);
        }

        /// <summary>Presents every staged item on its canonical slot (StagingSlot from state).</summary>
        public void Sync(PuzzleState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            var live = new HashSet<string>(StringComparer.Ordinal);
            if (Capacity > 0)
                foreach (var item in state.GetItems(ItemLocationKind.Staging))
                {
                    var slot = item.Location.StagingSlot;
                    if (slot < 0 || slot >= Capacity)
                        continue; // invalid states are reported by BoardInvariants, never drawn
                    live.Add(item.InstanceId);
                    if (!_items.TryGetValue(item.InstanceId, out var view))
                    {
                        view = new GameObject("Staged " + item.InstanceId).AddComponent<PuzzleItemView>();
                        _items.Add(item.InstanceId, view);
                    }
                    var rotation = item.State.AllowedRotations[0];
                    item.State.TryGetFootprint(rotation, out var footprint);
                    int width = 0, depth = 0;
                    foreach (var cell in footprint.OccupiedCells)
                    {
                        width = Mathf.Max(width, cell.X + 1);
                        depth = Mathf.Max(depth, cell.Y + 1);
                    }
                    var scale = Mathf.Min(1f, (PadSize - 2f * ParkMargin) / Mathf.Max(width, depth));
                    // Parked centred on its pad.
                    var local = new Vector3((PadSize - width * scale) * 0.5f, 0f, -(PadSize - depth * scale) * 0.5f);
                    view.BindLoose(item, _pads[slot], local, rotation, _board.ResolveVisual(item), _board.Template,
                        _board.ColorFor(item.Definition.Id));
                    view.transform.localScale = Vector3.one * scale;
                    view.SetGhost(false, null);
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
        }

        /// <summary>Slot whose pad contains the world point (XZ), or -1.</summary>
        public int SlotAt(Vector3 world)
        {
            for (var slot = 0; slot < _pads.Count; slot++)
            {
                var local = _pads[slot].InverseTransformPoint(world);
                if (local.x >= 0f && local.x < PadSize && -local.z >= 0f && -local.z < PadSize)
                    return slot;
            }
            return -1;
        }

        public Vector3 SlotCenter(int slot) => _pads[slot].TransformPoint(new Vector3(PadSize * 0.5f, 0f, -PadSize * 0.5f));

        /// <summary>Drag hover response (presentation only): warm green when the Domain preview accepts, muted red when not.</summary>
        public void SetHover(int slot, bool valid)
        {
            HoverSlot = slot;
            HoverValid = slot >= 0 && valid;
            for (var i = 0; i < _pads.Count; i++)
            {
                var color = i != slot ? PadFelt : Color.Lerp(PadFelt, valid ? ValidTint : InvalidTint, 0.75f);
                _padMaterials[i].SetColor(PresentationKit.BaseColorId, color);
                _padMaterials[i].SetColor(PresentationKit.ColorId, color);
                // Hovered pad lifts slightly so it reads as "this one".
                _pads[i].localPosition = new Vector3(_pads[i].localPosition.x, i == slot ? 0.04f : 0f, _pads[i].localPosition.z);
            }
        }

        public void Clear()
        {
            foreach (var view in _items.Values)
                if (view != null)
                {
                    view.gameObject.SetActive(false);
                    Destroy(view.gameObject);
                }
            _items.Clear();
            foreach (var pad in _pads)
                if (pad != null)
                {
                    pad.gameObject.SetActive(false);
                    Destroy(pad.gameObject);
                }
            _pads.Clear();
            _padMaterials.Clear();
            foreach (var asset in _owned)
                if (asset != null)
                    Destroy(asset);
            _owned.Clear();
            HoverSlot = -1;
            HoverValid = false;
        }

        private T Own<T>(T asset) where T : UnityEngine.Object
        {
            _owned.Add(asset);
            return asset;
        }

        private void OnDestroy() => Clear();
    }
}
