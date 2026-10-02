#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using ZipTrip.Domain;

namespace ZipTrip.Unity
{
    public sealed class DebugGameplayOverlay : MonoBehaviour
    {
        private const int LogCapacity = 10;
        private readonly List<string> _log = new List<string>(LogCapacity);
        private readonly StringBuilder _builder = new StringBuilder(1024);
        private PackGameplayController _gameplay;
        private DragPreviewPresenter _preview;
        private ItemView _lastItem;
        private Cell _lastAnchor;
        private Rotation _lastRotation;
        private string _lastShapeState;
        private string _logText = string.Empty;

        public bool IsVisible { get; private set; }
        public string SnapshotText { get; private set; } = string.Empty;
        public IReadOnlyList<string> LogEntries => _log.AsReadOnly();
        public string LastCopiedJson { get; private set; }

        public void Initialize(PackGameplayController gameplay, DragPreviewPresenter preview)
        {
            _gameplay = gameplay ?? throw new ArgumentNullException(nameof(gameplay));
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            _gameplay.CommandResolved += HandleCommand;
            _gameplay.AuthoritativeStateRefreshed += RefreshSnapshot;
            RefreshSnapshot();
        }

        public void Toggle() => IsVisible = !IsVisible;

        public string CopyCurrentState()
        {
            LastCopiedJson = DebugLevelExporter.Export(_gameplay.Level, _gameplay.Session.State);
            GUIUtility.systemCopyBuffer = LastCopiedJson;
            return LastCopiedJson;
        }

        public void RefreshSnapshot()
        {
            if (_gameplay == null)
                return;
            var state = _gameplay.Session.State;
            _builder.Clear();
            _builder.Append("level=").Append(_gameplay.Level.Id)
                .Append(" container=").Append(state.Container.Id).Append('\n');
            AppendMask(state);
            _builder.Append("occupied[").Append(state.Occupancy.Count).Append("]=");
            AppendCells(state.Occupancy);
            _builder.Append('\n');

            var item = _preview.ActiveItem;
            if (item == null)
                _builder.Append("item=none\n");
            else
            {
                _builder.Append("item=").Append(item.ItemId)
                    .Append(" anchor=").Append(_preview.CandidateAnchor)
                    .Append(" rotation=").Append((int)item.Rotation)
                    .Append(" shapeState=").Append(item.ShapeState).Append('\n')
                    .Append("footprint=");
                AppendCells(item.Footprint.OccupiedCells);
                _builder.Append('\n');
            }
            _builder.Append("stateHash=0x").Append(StateHash.Compute(state).ToString("X16"));
            SnapshotText = _builder.ToString();
            _lastItem = item;
            if (item != null)
            {
                _lastAnchor = _preview.CandidateAnchor;
                _lastRotation = item.Rotation;
                _lastShapeState = item.ShapeState;
            }
        }

        private void Update()
        {
            var item = _preview == null ? null : _preview.ActiveItem;
            if (item == _lastItem && (item == null ||
                (_preview.CandidateAnchor == _lastAnchor && item.Rotation == _lastRotation &&
                 StringComparer.Ordinal.Equals(item.ShapeState, _lastShapeState))))
                return;
            RefreshSnapshot();
        }

        private void HandleCommand(string command, CommandResult result)
        {
            _builder.Clear();
            _builder.Append(result.IsAccepted ? "[ACCEPTED] " : "[REJECTED] ").Append(command);
            if (!result.IsAccepted)
                _builder.Append(" reason=").Append(result.Reason);
            else if (result.Events.Count > 0)
            {
                _builder.Append(" events=");
                for (var i = 0; i < result.Events.Count; i++)
                {
                    if (i > 0) _builder.Append(',');
                    _builder.Append(result.Events[i].GetType().Name.Replace("Event", string.Empty));
                }
            }
            if (_log.Count == LogCapacity)
                _log.RemoveAt(0);
            _log.Add(_builder.ToString());
            _builder.Clear();
            for (var i = 0; i < _log.Count; i++)
                _builder.Append(_log[i]).Append('\n');
            _logText = _builder.ToString();
            RefreshSnapshot();
        }

        // Opened from the development drawer; nothing is drawn over normal gameplay.
        private void OnGUI()
        {
            if (!IsVisible)
                return;
            if (GUI.Button(new Rect(8f, 8f, 110f, 36f), "Hide DBG"))
                Toggle();
            GUI.Box(new Rect(8f, 50f, 440f, 520f), string.Empty);
            GUI.Label(new Rect(18f, 58f, 420f, 330f), SnapshotText);
            GUI.Label(new Rect(18f, 365f, 420f, 150f), _logText);
            if (GUI.Button(new Rect(18f, 525f, 180f, 36f), "Copy Level JSON"))
                CopyCurrentState();
        }

        private void AppendMask(GameState state)
        {
            _builder.Append("mask:\n");
            for (var y = 0; y < GridSize.Height; y++)
            {
                for (var x = 0; x < GridSize.Width; x++)
                {
                    var cell = new Cell(x, y);
                    _builder.Append(!state.Container.Mask.IsValid(cell) ? '#' :
                        Contains(state.Occupancy, cell) ? 'O' : '.');
                }
                _builder.Append('\n');
            }
        }

        private void AppendCells(IReadOnlyList<Cell> cells)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                if (i > 0) _builder.Append(',');
                _builder.Append(cells[i]);
            }
        }

        private static bool Contains(IReadOnlyList<Cell> cells, Cell target)
        {
            for (var i = 0; i < cells.Count; i++)
                if (cells[i] == target) return true;
            return false;
        }

        private void OnDestroy()
        {
            if (_gameplay == null) return;
            _gameplay.CommandResolved -= HandleCommand;
            _gameplay.AuthoritativeStateRefreshed -= RefreshSnapshot;
        }
    }
}
#endif
