using System;
using System.Collections.Generic;
using System.Text;

namespace ZipTrip.Domain.Puzzle
{
    /// <summary>
    /// A loaded, validated v2 level (ADR-0006, Δ-10 Domain side): spec, initial state, rules and objective.
    /// Construction is the content-validation authority shared by every loader:
    /// - the initial state must satisfy BoardInvariants (rules and the objective may be unmet);
    /// - no Source Tray item may be an ancestor (at any depth) of a nested child;
    /// - objectives and rule selectors must reference known instances, definitions and destinations.
    /// Failures throw ArgumentException with a reason code prefix, like the v1 LevelDefinition.
    /// </summary>
    public sealed class PuzzleLevel
    {
        public const int SchemaVersion = 2;

        public string Id { get; }
        public PuzzleSpec Spec { get; }
        public PuzzleState InitialState { get; }
        public RuleSet Rules { get; }
        public PuzzleObjective Objective { get; }

        public PuzzleLevel(string id, PuzzleState initialState, RuleSet rules, PuzzleObjective objective)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("MissingLevelId", nameof(id));
            if (initialState == null)
                throw new ArgumentException("MissingInitialState: " + id, nameof(initialState));
            if (rules == null)
                throw new ArgumentException("MissingRules: " + id, nameof(rules));
            if (objective == null)
                throw new ArgumentException("MissingObjective: " + id, nameof(objective));

            ValidateSourceTrayNesting(id, initialState);

            var invariants = BoardInvariants.Evaluate(initialState);
            if (!invariants.IsValid)
            {
                var details = new StringBuilder();
                foreach (var violation in invariants.Violations)
                    details.Append(details.Length == 0 ? "" : "; ").Append(violation);
                throw new ArgumentException($"InvalidInitialBoardState: {id} {details}");
            }

            ValidateObjective(id, initialState, objective);
            ValidateSelectors(id, initialState, rules);

            Id = id;
            Spec = initialState.Spec;
            InitialState = initialState;
            Rules = rules;
            Objective = objective;
        }

        // Gameplay can never put a nested child under a Source Tray item, at any depth, so content may not either.
        private static void ValidateSourceTrayNesting(string id, PuzzleState state)
        {
            foreach (var item in state.Items)
            {
                if (item.Location.Kind != ItemLocationKind.Nested)
                    continue;
                var root = item;
                while (root.Location.Kind == ItemLocationKind.Nested)
                    state.TryGetItem(root.Location.TargetId, out root);
                if (root.Location.Kind == ItemLocationKind.SourceTray)
                    throw new ArgumentException($"SourceTrayNestedAncestor: {id} {item.InstanceId} under {root.InstanceId}");
            }
        }

        private static void ValidateObjective(string id, PuzzleState state, PuzzleObjective objective)
        {
            foreach (var instanceId in objective.RequiredInstanceIds)
                if (!state.TryGetItem(instanceId, out _))
                    throw new ArgumentException($"UnknownObjectiveItem: {id} {instanceId}");
            if (objective.Profile != ObjectiveProfile.Extract)
                return;
            if (!state.TryGetItem(objective.ExtractionTargetInstanceId, out _))
                throw new ArgumentException($"UnknownObjectiveItem: {id} {objective.ExtractionTargetInstanceId}");
            if (!state.Spec.TryGetDestination(objective.ExtractionDestinationId, out _))
                throw new ArgumentException($"UnknownExtractionDestination: {id} {objective.ExtractionDestinationId}");
        }

        private static void ValidateSelectors(string id, PuzzleState state, RuleSet rules)
        {
            var definitions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in state.Items)
                definitions.Add(item.Definition.Id);

            foreach (var rule in rules.Rules)
            {
                ValidateSelector(id, rule.Id, rule.Subjects, state, definitions);
                if (rule is AdjacencyRequiredRule required)
                    ValidateSelector(id, rule.Id, required.Targets, state, definitions);
                if (rule is AdjacencyForbiddenRule forbidden)
                    ValidateSelector(id, rule.Id, forbidden.Targets, state, definitions);
            }
        }

        private static void ValidateSelector(string id, string ruleId, ItemSelector selector, PuzzleState state, HashSet<string> definitions)
        {
            // Tag selectors are syntax-checked by ItemSelector; whether a tag must match an item is an open authoring policy.
            if (selector.Kind == ItemSelectorKind.Instance && !state.TryGetItem(selector.Value, out _))
                throw new ArgumentException($"InvalidRuleSelector: {id} {ruleId} unknown instance {selector.Value}");
            if (selector.Kind == ItemSelectorKind.Definition && !definitions.Contains(selector.Value))
                throw new ArgumentException($"InvalidRuleSelector: {id} {ruleId} unknown definition {selector.Value}");
        }
    }
}
