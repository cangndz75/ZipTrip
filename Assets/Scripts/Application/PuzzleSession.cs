using System;
using System.Collections.Generic;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Application
{
    public enum PuzzleSessionAction
    {
        Apply = 0,
        Undo = 1
    }

    /// <summary>Synchronous outcome of one session call; presentation reads everything it needs from here.</summary>
    public sealed class PuzzleSessionStep
    {
        public PuzzleSessionAction Action { get; }
        /// <summary>True for an accepted move or a performed undo; false for a rejected move or an empty undo.</summary>
        public bool StateChanged { get; }
        /// <summary>Domain move result for Apply (accepted or rejected); null for Undo.</summary>
        public MoveResult Move { get; }
        /// <summary>True only on an incomplete -> complete edge caused by this call.</summary>
        public bool CompletionReached { get; }
        public PuzzleState State { get; }
        public int MoveCount { get; }
        public CompletionResult Completion { get; }

        internal PuzzleSessionStep(PuzzleSessionAction action, bool stateChanged, MoveResult move, bool completionReached,
            PuzzleState state, int moveCount, CompletionResult completion)
        {
            Action = action;
            StateChanged = stateChanged;
            Move = move;
            CompletionReached = completionReached;
            State = state;
            MoveCount = moveCount;
            Completion = completion;
        }
    }

    /// <summary>
    /// Application runtime for one ADR-0006 level. Owns only temporal concerns: the current immutable state, undo
    /// snapshots, the move count of the current undoable path and completion-edge detection. Legality, rules and
    /// completion come from the Domain (PuzzleTransitions, CompletionEvaluator). Not thread-safe; no Unity types.
    /// </summary>
    public sealed class PuzzleSession
    {
        private readonly Stack<Snapshot> _undo = new Stack<Snapshot>();
        private Snapshot _current;

        public PuzzleLevel Level { get; }
        public PuzzleState CurrentState => _current.State;
        /// <summary>Accepted relocations on the current undoable path (Undo restores the previous value). Not in the state hash.</summary>
        public int MoveCount => _current.MoveCount;
        public CompletionResult CurrentCompletion => _current.Completion;
        public int UndoDepth => _undo.Count;
        public bool CanUndo => _undo.Count > 0;

        public PuzzleSession(PuzzleLevel level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            // An authored start that is already complete is reported as complete but is not a player completion edge.
            _current = new Snapshot(level.InitialState, 0, Evaluate(level.InitialState));
        }

        /// <summary>Applies one committed relocation. A rejected move changes nothing and is not an exception.</summary>
        public PuzzleSessionStep Apply(PuzzleMove move)
        {
            var result = PuzzleTransitions.Apply(_current.State, move);
            if (!result.IsAccepted)
                return Step(PuzzleSessionAction.Apply, false, result, false);

            var previous = _current;
            _undo.Push(previous);
            _current = new Snapshot(result.State, previous.MoveCount + 1, Evaluate(result.State));
            return Step(PuzzleSessionAction.Apply, true, result, IsCompletionEdge(previous, _current));
        }

        /// <summary>Restores the previous snapshot exactly (state, move count, completion). Undo is not a move.</summary>
        public PuzzleSessionStep Undo()
        {
            if (_undo.Count == 0)
                return Step(PuzzleSessionAction.Undo, false, null, false);

            var previous = _current;
            _current = _undo.Pop();
            return Step(PuzzleSessionAction.Undo, true, null, IsCompletionEdge(previous, _current));
        }

        private CompletionResult Evaluate(PuzzleState state) =>
            CompletionEvaluator.Evaluate(state, Level.Objective, Level.Rules);

        private static bool IsCompletionEdge(Snapshot before, Snapshot after) =>
            !before.Completion.IsComplete && after.Completion.IsComplete;

        private PuzzleSessionStep Step(PuzzleSessionAction action, bool changed, MoveResult move, bool completionReached) =>
            new PuzzleSessionStep(action, changed, move, completionReached, _current.State, _current.MoveCount, _current.Completion);

        private readonly struct Snapshot
        {
            public PuzzleState State { get; }
            public int MoveCount { get; }
            public CompletionResult Completion { get; }

            public Snapshot(PuzzleState state, int moveCount, CompletionResult completion)
            {
                State = state;
                MoveCount = moveCount;
                Completion = completion;
            }
        }
    }
}
