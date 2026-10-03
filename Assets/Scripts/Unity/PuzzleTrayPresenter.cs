using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // Minimal Source Tray (ZT-039/040): one view per top-level Source Tray instance, keyed by instance id, in instance id
    // order, wrapped into rows no wider than RowWidth and drawn at Scale. Positions, display rotations and the selection
    // are presentation-only and never part of PuzzleState.
    public sealed class PuzzleTrayPresenter : MonoBehaviour
    {
        public const float SelectedLift = 0.25f;

        private readonly Dictionary<string, PuzzleItemView> _items = new Dictionary<string, PuzzleItemView>();
        private readonly Dictionary<string, Rotation> _displayRotations = new Dictionary<string, Rotation>(StringComparer.Ordinal);
        private PuzzleBoardPresenter _board;

        public IReadOnlyDictionary<string, PuzzleItemView> ItemViews => _items;
        /// <summary>Visual scale of tray items (1 = board size).</summary>
        public float Scale { get; set; } = 1f;
        /// <summary>Maximum row width in world units; 0 = a single row.</summary>
        public float RowWidth { get; set; }
        public float Gap { get; set; } = 1f;
        /// <summary>Centres each wrapped row within RowWidth (ZT-040B loose items laid out under the suitcase).</summary>
        public bool CenterRows { get; set; }
        public string SelectedInstanceId { get; private set; }

        /// <summary>Uses the board presenter's material and visual resolver so tray and board items look alike.</summary>
        public void Configure(PuzzleBoardPresenter board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
        }

        /// <summary>Rotation the tray shows (and a drag starts with) for an item; defaults to its first allowed rotation.</summary>
        public Rotation DisplayRotation(PuzzleItem item) =>
            _displayRotations.TryGetValue(item.InstanceId, out var rotation) && item.State.AllowsRotation(rotation)
                ? rotation : item.State.AllowedRotations[0];

        public void SetDisplayRotation(string instanceId, Rotation rotation) => _displayRotations[instanceId] = rotation;

        public void Select(string instanceId) => SelectedInstanceId = instanceId;

        public void Clear()
        {
            foreach (var view in _items.Values)
            {
                view.gameObject.SetActive(false); // excluded from same-frame bounds; Destroy is deferred
                Destroy(view.gameObject);
            }
            _items.Clear();
            _displayRotations.Clear();
            SelectedInstanceId = null;
        }

        public void Sync(PuzzleState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (_board == null)
                throw new InvalidOperationException("Configure the tray before syncing.");

            var live = new HashSet<string>(StringComparer.Ordinal);
            var row = new List<(PuzzleItemView View, float X)>();
            float x = 0f, z = 0f, rowDepth = 0f;
            foreach (var item in state.GetItems(ItemLocationKind.SourceTray))
            {
                live.Add(item.InstanceId);
                if (!_items.TryGetValue(item.InstanceId, out var view))
                {
                    view = new GameObject("Tray " + item.InstanceId).AddComponent<PuzzleItemView>();
                    _items.Add(item.InstanceId, view);
                }
                var rotation = DisplayRotation(item);
                item.State.TryGetFootprint(rotation, out var footprint);
                int width = 0, depth = 0;
                foreach (var cell in footprint.OccupiedCells)
                {
                    width = Mathf.Max(width, cell.X + 1);
                    depth = Mathf.Max(depth, cell.Y + 1);
                }
                if (RowWidth > 0f && x > 0f && x + width * Scale > RowWidth)
                {
                    CenterRow(row, x - Gap);
                    x = 0f;
                    z -= rowDepth + Gap;
                    rowDepth = 0f;
                }
                var lift = item.InstanceId == SelectedInstanceId ? SelectedLift : 0f;
                view.BindLoose(item, transform, new Vector3(x, lift, z), rotation, _board.ResolveVisual(item), _board.Template,
                    _board.ColorFor(item.Definition.Id));
                view.transform.localScale = Vector3.one * Scale;
                view.SetShadowDrop(Mathf.Approximately(Scale, 0f) ? 0f : lift / Scale);
                view.SetGhost(false, null);
                row.Add((view, x));
                x += width * Scale + Gap;
                rowDepth = Mathf.Max(rowDepth, depth * Scale);
            }
            CenterRow(row, x - Gap);

            var stale = new List<string>();
            foreach (var id in _items.Keys)
                if (!live.Contains(id))
                    stale.Add(id);
            foreach (var id in stale)
            {
                Destroy(_items[id].gameObject);
                _items.Remove(id);
                _displayRotations.Remove(id);
            }
            if (SelectedInstanceId != null && !live.Contains(SelectedInstanceId))
                SelectedInstanceId = null;
        }

        private void CenterRow(List<(PuzzleItemView View, float X)> row, float usedWidth)
        {
            if (CenterRows && RowWidth > 0f && row.Count > 0)
            {
                var shift = Mathf.Max(0f, (RowWidth - usedWidth) * 0.5f);
                foreach (var (view, x) in row)
                {
                    var position = view.transform.localPosition;
                    view.transform.localPosition = new Vector3(x + shift, position.y, position.z);
                }
            }
            row.Clear();
        }
    }
}
