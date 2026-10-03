using System;
using System.Collections.Generic;

namespace ZipTrip.Domain.Puzzle
{
    public enum ObjectiveProfile
    {
        Pack = 0,
        Extract = 1,
        Repack = 2
    }

    /// <summary>
    /// Authored level objective (ADR-0006 Decisions 1, 13, 16). Refers to exact item instances; kept separate from
    /// PuzzleState. Construct through <see cref="Pack"/>, <see cref="Repack"/> or <see cref="Extract"/>.
    /// </summary>
    public sealed class PuzzleObjective
    {
        public ObjectiveProfile Profile { get; }
        /// <summary>Pack / Repack: instances that must end in the suitcase (union of existing and incoming), ordinal order.</summary>
        public IReadOnlyList<string> RequiredInstanceIds { get; }
        /// <summary>Repack: required instances already in the starting layout.</summary>
        public IReadOnlyList<string> ExistingInstanceIds { get; }
        /// <summary>Repack: required incoming instances (e.g. souvenirs).</summary>
        public IReadOnlyList<string> IncomingInstanceIds { get; }
        /// <summary>Extract: the exact target instance; null otherwise.</summary>
        public string ExtractionTargetInstanceId { get; }
        /// <summary>Extract: the destination the target must reach; null otherwise.</summary>
        public string ExtractionDestinationId { get; }

        private PuzzleObjective(ObjectiveProfile profile, SortedSet<string> existing, SortedSet<string> incoming,
            string targetId, string destinationId)
        {
            Profile = profile;
            var required = new SortedSet<string>(existing, StringComparer.Ordinal);
            required.UnionWith(incoming);
            RequiredInstanceIds = new List<string>(required).AsReadOnly();
            ExistingInstanceIds = new List<string>(existing).AsReadOnly();
            IncomingInstanceIds = new List<string>(incoming).AsReadOnly();
            ExtractionTargetInstanceId = targetId;
            ExtractionDestinationId = destinationId;
        }

        public static PuzzleObjective Pack(IEnumerable<string> requiredInstanceIds)
        {
            var required = Ids(requiredInstanceIds, "requiredInstanceIds");
            if (required.Count == 0)
                throw new ArgumentException("MalformedObjective: Pack needs at least one required instance.");
            return new PuzzleObjective(ObjectiveProfile.Pack, required, Empty(), null, null);
        }

        public static PuzzleObjective Repack(IEnumerable<string> existingInstanceIds, IEnumerable<string> incomingInstanceIds)
        {
            var existing = Ids(existingInstanceIds, "existingInstanceIds");
            var incoming = Ids(incomingInstanceIds, "incomingInstanceIds");
            if (incoming.Count == 0)
                throw new ArgumentException("MalformedObjective: Repack needs at least one incoming instance.");
            if (existing.Overlaps(incoming))
                throw new ArgumentException("MalformedObjective: an instance cannot be both existing and incoming.");
            return new PuzzleObjective(ObjectiveProfile.Repack, existing, incoming, null, null);
        }

        public static PuzzleObjective Extract(string targetInstanceId, string destinationId)
        {
            if (string.IsNullOrWhiteSpace(targetInstanceId))
                throw new ArgumentException("MalformedObjective: missing extraction target instance id.");
            if (string.IsNullOrWhiteSpace(destinationId))
                throw new ArgumentException("MalformedObjective: missing extraction destination id.");
            return new PuzzleObjective(ObjectiveProfile.Extract, Empty(), Empty(), targetInstanceId, destinationId);
        }

        private static SortedSet<string> Empty() => new SortedSet<string>(StringComparer.Ordinal);

        private static SortedSet<string> Ids(IEnumerable<string> values, string name)
        {
            var set = Empty();
            if (values == null)
                return set;
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("MalformedObjective: blank instance id in " + name);
                if (!set.Add(value))
                    throw new ArgumentException($"MalformedObjective: duplicate instance id {value} in {name}");
            }
            return set;
        }
    }

    public enum CompletionFailureKind
    {
        /// <summary>A required instance is not in the suitcase (directly or under a suitcase-resident parent).</summary>
        RequiredItemNotInSuitcase = 0,
        /// <summary>The exact extraction target is not directly in its destination.</summary>
        ExtractionTargetNotInDestination = 1,
        /// <summary>An item is in staging.</summary>
        StagingNotEmpty = 2,
        /// <summary>The state violates a board invariant (see <see cref="CompletionResult.Invariants"/>).</summary>
        InvariantViolation = 3,
        /// <summary>An applicable puzzle rule is unsatisfied.</summary>
        RuleViolation = 4
    }

    public readonly struct CompletionFailure
    {
        public CompletionFailureKind Kind { get; }
        /// <summary>The item concerned (objective and staging failures); null otherwise.</summary>
        public string InstanceId { get; }
        /// <summary>The failing rule (rule failures); null otherwise.</summary>
        public string RuleId { get; }

        internal CompletionFailure(CompletionFailureKind kind, string instanceId = null, string ruleId = null)
        {
            Kind = kind;
            InstanceId = instanceId;
            RuleId = ruleId;
        }

        public override string ToString() => $"{Kind} {InstanceId}{RuleId}";
    }

    /// <summary>
    /// Completion diagnostics. Failures are ordered by kind, then instance / rule id. The full invariant report and
    /// rule results are kept for later indicators.
    /// </summary>
    public sealed class CompletionResult
    {
        public bool IsComplete => Failures.Count == 0;
        public IReadOnlyList<CompletionFailure> Failures { get; }
        public InvariantReport Invariants { get; }
        public IReadOnlyList<RuleResult> Rules { get; }

        internal CompletionResult(List<CompletionFailure> failures, InvariantReport invariants, IReadOnlyList<RuleResult> rules)
        {
            Failures = failures.AsReadOnly();
            Invariants = invariants;
            Rules = rules;
        }
    }

    /// <summary>
    /// Pure completion evaluation (ADR-0006 Decision 16) composed from BoardInvariants and RuleEvaluator.
    /// No history: detecting the incomplete -> complete transition belongs to the session.
    /// </summary>
    public static class CompletionEvaluator
    {
        public static CompletionResult Evaluate(PuzzleState state, PuzzleObjective objective, RuleSet rules)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (objective == null)
                throw new ArgumentNullException(nameof(objective));
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            var failures = new List<CompletionFailure>();

            foreach (var id in objective.RequiredInstanceIds)
                if (!IsInSuitcase(state, id))
                    failures.Add(new CompletionFailure(CompletionFailureKind.RequiredItemNotInSuitcase, id));

            if (objective.Profile == ObjectiveProfile.Extract && !IsInDestination(state, objective.ExtractionTargetInstanceId, objective.ExtractionDestinationId))
                failures.Add(new CompletionFailure(CompletionFailureKind.ExtractionTargetNotInDestination, objective.ExtractionTargetInstanceId));

            foreach (var staged in state.GetItems(ItemLocationKind.Staging))
                failures.Add(new CompletionFailure(CompletionFailureKind.StagingNotEmpty, staged.InstanceId));

            var invariants = BoardInvariants.Evaluate(state);
            if (!invariants.IsValid)
                failures.Add(new CompletionFailure(CompletionFailureKind.InvariantViolation));

            var ruleResults = RuleEvaluator.Evaluate(state, rules);
            foreach (var rule in ruleResults)
                if (!rule.IsSatisfied)
                    failures.Add(new CompletionFailure(CompletionFailureKind.RuleViolation, ruleId: rule.RuleId));

            return new CompletionResult(failures, invariants, ruleResults);
        }

        /// <summary>
        /// Objective membership: the item has a suitcase placement, or its outermost parent does. Items whose
        /// outermost parent is in the Source Tray, staging or a destination are not in the suitcase. Never throws.
        /// </summary>
        public static bool IsInSuitcase(PuzzleState state, string instanceId)
        {
            if (state == null || !state.TryGetItem(instanceId, out var item))
                return false;
            // PuzzleState guarantees every parent exists and the chain is acyclic.
            while (item.Location.Kind == ItemLocationKind.Nested)
                state.TryGetItem(item.Location.TargetId, out item);
            return item.Location.Kind == ItemLocationKind.Suitcase;
        }

        /// <summary>True only when the exact instance is directly in the named destination.</summary>
        public static bool IsInDestination(PuzzleState state, string instanceId, string destinationId)
        {
            return state != null && state.TryGetItem(instanceId, out var item)
                && item.Location.Kind == ItemLocationKind.Destination
                && item.Location.TargetId == destinationId;
        }
    }
}
