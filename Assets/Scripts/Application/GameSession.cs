using System;
using System.Collections.Generic;
using ZipTrip.Domain;

namespace ZipTrip.Application
{
    public sealed class GameSession
    {
        private readonly Stack<GameState> _undo = new Stack<GameState>();

        public GameState State { get; private set; }
        public int UndoDepth => _undo.Count;

        public GameSession(GameState initialState)
        {
            State = initialState ?? throw new ArgumentNullException(nameof(initialState));
        }

        public CommandResult Execute(ICommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            var result = command.Execute(State)
                ?? throw new InvalidOperationException("Command returned no result.");
            if (result.IsAccepted)
            {
                _undo.Push(State);
                State = result.NewState;
            }
            return result;
        }

        public bool Undo()
        {
            if (_undo.Count == 0)
                return false;

            State = _undo.Pop();
            return true;
        }
    }
}
