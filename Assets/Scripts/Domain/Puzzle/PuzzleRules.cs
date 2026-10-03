using System;
using System.Collections.Generic;
using ZipTrip.Domain.Board;

namespace ZipTrip.Domain.Puzzle
{
    public enum ItemSelectorKind
    {
        /// <summary>One exact item instance (PuzzleItem.InstanceId).</summary>
        Instance = 0,
        /// <summary>Every instance of an item definition (ItemSpec.Id).</summary>
        Definition = 1,
        /// <summary>Every instance whose definition carries the tag.</summary>
        Tag = 2
    }

    /// <summary>Strongly typed rule selector. Instance and definition identity are never conflated.</summary>
    public readonly struct ItemSelector
    {
        public ItemSelectorKind Kind { get; }
        public string Value { get; }

        private ItemSelector(ItemSelectorKind kind, string value)
        {
            Kind = kind;
            Value = value;
        }

        public static ItemSelector Instance(string instanceId) => Create(ItemSelectorKind.Instance, instanceId);
        public static ItemSelector Definition(string definitionId) => Create(ItemSelectorKind.Definition, definitionId);
        public static ItemSelector Tag(string tag) => Create(ItemSelectorKind.Tag, tag);

        public bool Matches(PuzzleItem item)
        {
            if (item == null)
                return false;
            switch (Kind)
            {
                case ItemSelectorKind.Instance: return item.InstanceId == Value;
                case ItemSelectorKind.Definition: return item.Definition.Id == Value;
                case ItemSelectorKind.Tag: return item.Definition.HasTag(Value);
                default: return false;
            }
        }

        public override string ToString() => $"{Kind}:{Value}";

        private static ItemSelector Create(ItemSelectorKind kind, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("MalformedSelector: " + kind, nameof(value));
            return new ItemSelector(kind, value);
        }
    }

    /// <summary>Per-rule status for live indicators. Id lists are in ordinal order.</summary>
    public sealed class RuleResult
    {
        public string RuleId { get; }
        public bool IsSatisfied { get; }
        /// <summary>Active items selected as subjects.</summary>
        public IReadOnlyList<string> SubjectIds { get; }
        /// <summary>Subjects that fail the rule.</summary>
        public IReadOnlyList<string> OffendingIds { get; }
        /// <summary>Other items involved in a failure (forbidden adjacency partners); empty otherwise.</summary>
        public IReadOnlyList<string> RelatedIds { get; }

        internal RuleResult(string ruleId, SortedSet<string> subjects, SortedSet<string> offending, SortedSet<string> related)
        {
            RuleId = ruleId;
            SubjectIds = new List<string>(subjects).AsReadOnly();
            OffendingIds = new List<string>(offending).AsReadOnly();
            RelatedIds = new List<string>(related).AsReadOnly();
            IsSatisfied = OffendingIds.Count == 0;
        }
    }

    /// <summary>
    /// A puzzle rule (ADR-0006 Decision 6). Rules never reject moves; they only report status (Decision 15).
    /// New rule kinds (Group, Balance) are added as further subclasses without changing the evaluator.
    /// </summary>
    public abstract class PuzzleRule
    {
        public string Id { get; }
        public ItemSelector Subjects { get; }

        protected PuzzleRule(string id, ItemSelector subjects)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("MissingRuleId", nameof(id));
            if (subjects.Value == null)
                throw new ArgumentException("MalformedSelector: " + id, nameof(subjects));
            Id = id;
            Subjects = subjects;
        }

        internal virtual void Validate(BoardSpec board)
        {
        }

        internal abstract RuleResult Evaluate(PuzzleState state, IReadOnlyList<PuzzleItem> active);

        internal SortedSet<string> Select(IReadOnlyList<PuzzleItem> active, ItemSelector selector)
        {
            var ids = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var item in active)
                if (selector.Matches(item))
                    ids.Add(item.InstanceId);
            return ids;
        }

        internal RuleResult Unary(PuzzleState state, IReadOnlyList<PuzzleItem> active, Func<PuzzleState, string, bool> predicate)
        {
            var subjects = Select(active, Subjects);
            var offending = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var subject in subjects)
                if (!predicate(state, subject))
                    offending.Add(subject);
            return new RuleResult(Id, subjects, offending, new SortedSet<string>(StringComparer.Ordinal));
        }
    }

    /// <summary>Every subject's effective footprint lies entirely in the XY-column zone (ADR-0006 rule semantics).</summary>
    public sealed class ZoneRule : PuzzleRule
    {
        public string ZoneId { get; }

        public ZoneRule(string id, ItemSelector subjects, string zoneId) : base(id, subjects)
        {
            if (string.IsNullOrWhiteSpace(zoneId))
                throw new ArgumentException("MissingZoneId: " + id, nameof(zoneId));
            ZoneId = zoneId;
        }

        internal override void Validate(BoardSpec board)
        {
            foreach (var compartment in board.Compartments)
                foreach (var zone in compartment.ColumnZoneIds)
                    if (zone == ZoneId)
                        return;
            throw new ArgumentException($"UnknownZone: {Id} {ZoneId}");
        }

        internal override RuleResult Evaluate(PuzzleState state, IReadOnlyList<PuzzleItem> active) =>
            Unary(state, active, IsInZone);

        private bool IsInZone(PuzzleState state, string instanceId)
        {
            // A nested child uses its outermost parent's placement; outside the suitcase there is no zone.
            state.TryGetItem(instanceId, out var item);
            while (item.Location.Kind == ItemLocationKind.Nested)
                state.TryGetItem(item.Location.TargetId, out item);
            if (item.Location.Kind != ItemLocationKind.Suitcase)
                return false;
            foreach (var cell in PuzzleState.GetPhysicalCells(item))
                if (state.Spec.Board.GetZone(cell) != ZoneId)
                    return false;
            return true;
        }
    }

    /// <summary>Every subject is accessible (AccessQueries; nested children follow their parent).</summary>
    public sealed class AccessRule : PuzzleRule
    {
        public AccessRule(string id, ItemSelector subjects) : base(id, subjects)
        {
        }

        internal override RuleResult Evaluate(PuzzleState state, IReadOnlyList<PuzzleItem> active) =>
            Unary(state, active, AccessQueries.IsAccessible);
    }

    /// <summary>Every subject is adjacent (AdjacencyQueries) to at least one distinct target.</summary>
    public sealed class AdjacencyRequiredRule : PuzzleRule
    {
        public ItemSelector Targets { get; }

        public AdjacencyRequiredRule(string id, ItemSelector subjects, ItemSelector targets) : base(id, subjects)
        {
            if (targets.Value == null)
                throw new ArgumentException("MalformedSelector: " + id, nameof(targets));
            Targets = targets;
        }

        internal override RuleResult Evaluate(PuzzleState state, IReadOnlyList<PuzzleItem> active)
        {
            var subjects = Select(active, Subjects);
            var targets = Select(active, Targets);

            // Decision 16: a requirement on targets that have all left the active domain no longer has to be met
            // (extracting the laptop must not make "cable touches laptop" unsatisfiable).
            var targetsLeftDomain = false;
            foreach (var item in state.Items)
                targetsLeftDomain |= Targets.Matches(item) && !targets.Contains(item.InstanceId);

            var offending = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var subject in subjects)
            {
                var hasActiveTarget = false;
                var touches = false;
                foreach (var target in targets)
                {
                    if (target == subject)
                        continue;
                    hasActiveTarget = true;
                    touches |= AdjacencyQueries.AreAdjacent(state, subject, target);
                }
                if (!touches && (hasActiveTarget || !targetsLeftDomain))
                    offending.Add(subject);
            }
            return new RuleResult(Id, subjects, offending, new SortedSet<string>(StringComparer.Ordinal));
        }
    }

    /// <summary>No subject is adjacent (AdjacencyQueries) to any distinct target.</summary>
    public sealed class AdjacencyForbiddenRule : PuzzleRule
    {
        public ItemSelector Targets { get; }

        public AdjacencyForbiddenRule(string id, ItemSelector subjects, ItemSelector targets) : base(id, subjects)
        {
            if (targets.Value == null)
                throw new ArgumentException("MalformedSelector: " + id, nameof(targets));
            Targets = targets;
        }

        internal override RuleResult Evaluate(PuzzleState state, IReadOnlyList<PuzzleItem> active)
        {
            var subjects = Select(active, Subjects);
            var targets = Select(active, Targets);
            var offending = new SortedSet<string>(StringComparer.Ordinal);
            var related = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var subject in subjects)
                foreach (var target in targets)
                    if (target != subject && AdjacencyQueries.AreAdjacent(state, subject, target))
                    {
                        offending.Add(subject);
                        related.Add(target);
                    }
            return new RuleResult(Id, subjects, offending, related);
        }
    }

    /// <summary>An authored, validated set of rules with unique ids, held in ordinal id order.</summary>
    public sealed class RuleSet
    {
        public IReadOnlyList<PuzzleRule> Rules { get; }

        public RuleSet(BoardSpec board, IEnumerable<PuzzleRule> rules)
        {
            if (board == null)
                throw new ArgumentException("MissingBoard", nameof(board));
            var ordered = new List<PuzzleRule>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (rules != null)
                foreach (var rule in rules)
                {
                    if (rule == null)
                        throw new ArgumentException("MissingRule", nameof(rules));
                    if (!ids.Add(rule.Id))
                        throw new ArgumentException("DuplicateRuleId: " + rule.Id, nameof(rules));
                    rule.Validate(board);
                    ordered.Add(rule);
                }
            ordered.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            Rules = ordered.AsReadOnly();
        }
    }

    /// <summary>
    /// Shared rule evaluator (ADR-0006 Decisions 6, 15, 16). Evaluates every rule over the active rule domain:
    /// all items except those in an extraction destination (directly or through a nesting parent). Spatial facts
    /// come only from BoardSpec, AccessQueries and AdjacencyQueries.
    /// </summary>
    public static class RuleEvaluator
    {
        public static IReadOnlyList<RuleResult> Evaluate(PuzzleState state, RuleSet rules)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            var active = GetActiveDomain(state);
            var results = new List<RuleResult>(rules.Rules.Count);
            foreach (var rule in rules.Rules)
                results.Add(rule.Evaluate(state, active));
            return results.AsReadOnly();
        }

        public static bool AllSatisfied(IReadOnlyList<RuleResult> results)
        {
            foreach (var result in results)
                if (!result.IsSatisfied)
                    return false;
            return true;
        }

        /// <summary>Items participating in the suitcase objective, ordinal instance id order.</summary>
        public static IReadOnlyList<PuzzleItem> GetActiveDomain(PuzzleState state)
        {
            var active = new List<PuzzleItem>();
            foreach (var item in state.Items)
                if (AccessQueries.GetAccess(state, item.InstanceId) != ItemAccess.Terminal)
                    active.Add(item);
            return active.AsReadOnly();
        }
    }
}
