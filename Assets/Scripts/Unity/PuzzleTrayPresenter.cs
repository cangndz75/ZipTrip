using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // Minimal Source Tray strip (ZT-039): one view per top-level Source Tray instance, keyed by instance id, laid out
    // left to right in instance id order. Positions are presentation-only and never part of PuzzleState.
    public sealed class PuzzleTrayPresenter : MonoBehaviour
    {
        public const float ItemGap = 1f;

        private readonly Dictionary<string, PuzzleItemView> _items = new Dictionary<string, PuzzleItemView>();
        private PuzzleBoardPresenter _board;

        public IReadOnlyDictionary<string, PuzzleItemView> ItemViews => _items;

        /// <summary>Uses the board presenter's material and visual resolver so tray and board items look alike.</summary>
        public void Configure(PuzzleBoardPresenter board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
        }

        public void Sync(PuzzleState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (_board == null)
                throw new InvalidOperationException("Configure the tray before syncing.");

            var live = new HashSet<string>(StringComparer.Ordinal);
            var x = 0f;
            foreach (var item in state.GetItems(ItemLocationKind.SourceTray))
            {
                live.Add(item.InstanceId);
                if (!_items.TryGetValue(item.InstanceId, out var view))
                {
                    view = new GameObject("Tray " + item.InstanceId).AddComponent<PuzzleItemView>();
                    _items.Add(item.InstanceId, view);
                }
                var rotation = item.State.AllowedRotations[0];
                view.BindLoose(item, transform, new Vector3(x, 0f, 0f), rotation, _board.ResolveVisual(item), _board.Template,
                    _board.ColorFor(item.Definition.Id));
                view.SetGhost(false, null);
                var width = 0;
                foreach (var cell in view.Footprint.OccupiedCells)
                    width = Mathf.Max(width, cell.X + 1);
                x += width + ItemGap;
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
    }
}
