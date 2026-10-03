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
    public sealed class PuzzleBoardPresenter : MonoBehaviour
    {
        public static readonly Color GhostColor = new Color(0.55f, 0.75f, 0.78f, 0.22f);
        private static readonly Color[] Palette =
            { PresentationKit.Teal, PresentationKit.Terracotta, PresentationKit.Mustard, PresentationKit.DeepBlueGreen };

        private readonly Dictionary<string, PuzzleCompartmentView> _compartments = new Dictionary<string, PuzzleCompartmentView>();
        private readonly Dictionary<string, PuzzleItemView> _items = new Dictionary<string, PuzzleItemView>();
        private Func<PuzzleItem, GameObject> _visualResolver;
        private Material _template;
        private Material _ghostMaterial;

        public BoardSpec Board { get; private set; }
        public PuzzleState PresentedState { get; private set; }
        public bool XRayEnabled { get; private set; }
        public IReadOnlyDictionary<string, PuzzleCompartmentView> Compartments => _compartments;
        /// <summary>Board views keyed by item instance id (suitcase-resident items only).</summary>
        public IReadOnlyDictionary<string, PuzzleItemView> ItemViews => _items;

        /// <param name="materialTemplate">Shared runtime material (e.g. BoardPresenter.RuntimeMaterialTemplate); may be null.</param>
        /// <param name="visualResolver">Optional prefab lookup per item; null or a null result uses footprint blocks.</param>
        public void Present(BoardSpec board, Material materialTemplate = null, Func<PuzzleItem, GameObject> visualResolver = null)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            _template = materialTemplate;
            _visualResolver = visualResolver;
            if (_ghostMaterial == null && materialTemplate != null)
                _ghostMaterial = PresentationKit.Transparent(materialTemplate, GhostColor);

            foreach (var view in _compartments.Values)
                Destroy(view.gameObject);
            _compartments.Clear();
            _items.Clear();
            PresentedState = null;

            var origins = PuzzleBoardLayout.CompartmentOrigins(board);
            foreach (var compartment in board.Compartments)
            {
                var view = new GameObject("Compartment " + compartment.Id).AddComponent<PuzzleCompartmentView>();
                view.transform.SetParent(transform, false);
                view.transform.localPosition = origins[compartment.Id];
                view.Build(compartment, _template);
                _compartments.Add(compartment.Id, view);
            }
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
                    _visualResolver?.Invoke(item), _template, ColorFor(item.Definition.Id));
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

        private static Color ColorFor(string definitionId)
        {
            var hash = 0;
            foreach (var c in definitionId)
                hash = unchecked(hash * 31 + c);
            return Palette[(hash & int.MaxValue) % Palette.Length];
        }

        private void OnDestroy()
        {
            if (_ghostMaterial != null)
                Destroy(_ghostMaterial);
        }
    }
}
