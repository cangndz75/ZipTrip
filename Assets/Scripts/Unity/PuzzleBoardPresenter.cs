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

        public BoardSpec Board { get; private set; }
        public PuzzleState PresentedState { get; private set; }
        public bool XRayEnabled { get; private set; }
        public IReadOnlyDictionary<string, PuzzleCompartmentView> Compartments => _compartments;
        /// <summary>Board views keyed by item instance id (suitcase-resident items only).</summary>
        public IReadOnlyDictionary<string, PuzzleItemView> ItemViews => _items;

        /// <param name="materialTemplate">Shared runtime material (e.g. BoardPresenter.RuntimeMaterialTemplate); may be null.</param>
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
        }
    }
}
