using System.Collections.Generic;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // ZT-042 presentation strings for rule tags. Localization-ready: every user-facing word comes from these keyed
    // tables (English defaults here; a localization pass replaces the tables, not the call sites). The Domain carries
    // ids only and never any display text.
    public static class PuzzleRuleText
    {
        /// <summary>Tag templates by rule kind; {0} = subject, {1} = target or zone.</summary>
        public static readonly Dictionary<string, string> Templates = new Dictionary<string, string>
        {
            ["rule.zone"] = "{0} → {1}",
            ["rule.adjacencyRequired"] = "{0} + {1}",
            ["rule.adjacencyForbidden"] = "{0} ≠ {1}",
            ["rule.access"] = "{0} on top"
        };

        /// <summary>Display names for item definitions, tags and zones; unknown keys fall back to a capitalised id.</summary>
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["item.laptop"] = "Laptop",
            ["item.book"] = "Book",
            ["item.sneaker"] = "Sneakers",
            ["item.sweater"] = "Sweater",
            ["tag.tech"] = "Tech",
            ["tag.clothes"] = "Clothes",
            ["tag.shoes"] = "Shoes",
            ["tag.soft"] = "Soft",
            ["zone.left"] = "Left",
            ["zone.right"] = "Right",
            ["zone.bottom"] = "Bottom",
            ["zone.top"] = "Top"
        };

        public static string Label(PuzzleRule rule, PuzzleState state)
        {
            switch (rule)
            {
                case ZoneRule zone:
                    return string.Format(Templates["rule.zone"], Name(rule.Subjects, state), Lookup("zone." + zone.ZoneId, zone.ZoneId));
                case AdjacencyRequiredRule required:
                    return string.Format(Templates["rule.adjacencyRequired"], Name(rule.Subjects, state), Name(required.Targets, state));
                case AdjacencyForbiddenRule forbidden:
                    return string.Format(Templates["rule.adjacencyForbidden"], Name(rule.Subjects, state), Name(forbidden.Targets, state));
                case AccessRule _:
                    return string.Format(Templates["rule.access"], Name(rule.Subjects, state));
                default:
                    return rule.Id;
            }
        }

        // Instance selectors name the instance's definition; definition and tag selectors name themselves.
        private static string Name(ItemSelector selector, PuzzleState state)
        {
            switch (selector.Kind)
            {
                case ItemSelectorKind.Instance:
                    return state.TryGetItem(selector.Value, out var item)
                        ? Lookup("item." + item.Definition.Id, item.Definition.Id) : Capitalise(selector.Value);
                case ItemSelectorKind.Definition:
                    return Lookup("item." + selector.Value, selector.Value);
                default:
                    return Lookup("tag." + selector.Value, selector.Value);
            }
        }

        private static string Lookup(string key, string id) => Names.TryGetValue(key, out var name) ? name : Capitalise(id);

        private static string Capitalise(string id) => string.IsNullOrEmpty(id) ? id : char.ToUpperInvariant(id[0]) + id.Substring(1);
    }
}
