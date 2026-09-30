using System;
using System.Collections.Generic;

namespace ZipTrip.Domain
{
    public sealed class CommandResult
    {
        public bool IsAccepted { get; }
        public GameState NewState { get; }
        public IReadOnlyList<IGameEvent> Events { get; }
        public string Reason { get; }

        private CommandResult(bool isAccepted, GameState newState,
            IReadOnlyList<IGameEvent> events, string reason)
        {
            IsAccepted = isAccepted;
            NewState = newState;
            Events = events;
            Reason = reason;
        }

        public static CommandResult Accepted(GameState newState, IEnumerable<IGameEvent> events)
        {
            if (newState == null)
                throw new ArgumentNullException(nameof(newState));
            if (events == null)
                throw new ArgumentNullException(nameof(events));

            var snapshot = new List<IGameEvent>();
            foreach (var gameEvent in events)
            {
                if (gameEvent == null)
                    throw new ArgumentException("Events cannot contain null.", nameof(events));
                snapshot.Add(gameEvent);
            }
            return new CommandResult(true, newState, snapshot.AsReadOnly(), null);
        }

        public static CommandResult Rejected(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Rejection reason is required.", nameof(reason));

            return new CommandResult(false, null, Array.Empty<IGameEvent>(), reason);
        }
    }
}
