using System.Collections.Generic;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    // ZT-042 / UI-SLICE-01 presentation strings. Localization-ready: every user-facing word comes from these keyed tables
    // (Turkish, the locked STYLE-FRAME-01 copy; a localization pass replaces the tables, not the call sites). The Domain
    // carries ids only and never any display text.
    public static class PuzzleRuleText
    {
        /// <summary>Rule sentences by rule kind; {0} = subject, {1} = target or zone. Plus the objective note title.</summary>
        public static readonly Dictionary<string, string> Templates = new Dictionary<string, string>
        {
            ["rule.zone"] = "{0} {1} bölgede olmalı",
            ["rule.adjacencyRequired"] = "{0}, {1} yanında olmalı",
            ["rule.adjacencyForbidden"] = "{0}, {1} yanında olmamalı",
            ["rule.access"] = "{0} üstte olmalı",
            ["objective.title"] = "Yolculuk Hazırlıkları"
        };

        /// <summary>Display names for item definitions, tags and zones; unknown keys fall back to a capitalised id.</summary>
        public static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["item.laptop"] = "Laptop",
            ["item.book"] = "Kitap",
            ["item.sneaker"] = "Spor ayakkabı",
            ["item.sweater"] = "Kazak",
            ["item.passport"] = "Pasaport",
            ["item.towel"] = "Havlu",
            ["item.shampoo"] = "Şampuan",
            ["item.sunglasses"] = "Gözlük",
            ["item.travel-pouch"] = "Çanta",
            ["tag.tech"] = "Elektronik",
            ["tag.clothes"] = "Kıyafet",
            ["tag.shoes"] = "Ayakkabı",
            ["tag.soft"] = "Yumuşak",
            ["zone.left"] = "sol",
            ["zone.right"] = "sağ",
            ["zone.bottom"] = "alt",
            ["zone.top"] = "üst",
            ["zone.upper"] = "üst"
        };

        /// <summary>Authored level names shown under "Seviye N", by level id (none = title only).</summary>
        public static readonly Dictionary<string, string> LevelSubtitles = new Dictionary<string, string>
        {
            ["golden-lv1-candidate"] = "İlk Yolculuk"
        };

        public static string LevelTitle(int number) => "Seviye " + number;

        public static string LevelSubtitle(string levelId) =>
            levelId != null && LevelSubtitles.TryGetValue(levelId, out var subtitle) ? subtitle : null;

        /// <summary>Centre-slot status for the items still outside the suitcase; null when there are none.</summary>
        public static string Remaining(int count) => count > 0 ? count + " eşya kaldı" : null;

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

        /// <summary>Short form for the compact rule chip: the rule's subject name ("Pasaport").</summary>
        public static string Subject(PuzzleRule rule, PuzzleState state) => Name(rule.Subjects, state);

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
