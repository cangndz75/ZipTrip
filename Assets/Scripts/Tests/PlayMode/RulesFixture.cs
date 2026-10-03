using System.Linq;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // ZT-042 rules fixture (test-only, real schema v2 path, not shipped progression). Cabin 5x7, two layers, a "left"
    // zone on columns 0-1, staging capacity 2. Book (0,0) and laptop (2,0) start packed; sneakers and sweater wait on
    // the mat. Rules: r1 book -> left (zone), r2 sneakers + book (adjacency required), r3 clothes != tech (adjacency
    // forbidden), r4 laptop on top (access). Initially r1/r3/r4 hold and r2 does not.
    public static class RulesFixture
    {
        private static string Zone() =>
            string.Join(",", Enumerable.Range(0, 7).SelectMany(y => new[] { 0, 1 }.Select(x => "{\"x\":" + x + ",\"y\":" + y + "}")));

        private static string Json(string laptopLocation, string laptopRole, string required, string destinations) => @"{
  ""schemaVersion"": 2,
  ""id"": ""zt042-rules-fixture"",
  ""board"": { ""compartments"": [
    { ""id"": ""main"", ""width"": 5, ""height"": 7, ""layers"": 2,
      ""rows"": [""#####"", ""#####"", ""#####"", ""#####"", ""#####"", ""#####"", ""#####""],
      ""zones"": [ { ""id"": ""left"", ""cells"": [" + Zone() + @"] } ] }
  ] },
  ""stagingCapacity"": 2," + destinations + @"
  ""items"": [
    { ""id"": ""book-1"", ""definitionId"": ""book"", ""role"": ""required"",
      ""location"": { ""kind"": ""suitcase"", ""compartmentId"": ""main"", ""x"": 0, ""y"": 0, ""layer"": 0, ""rotation"": 0 } },
    { ""id"": ""laptop-1"", ""definitionId"": ""laptop"", ""role"": """ + laptopRole + @""", ""location"": " + laptopLocation + @" },
    { ""id"": ""sneaker-1"", ""definitionId"": ""sneaker"", ""role"": ""required"", ""location"": { ""kind"": ""sourceTray"" } },
    { ""id"": ""sweater-1"", ""definitionId"": ""sweater"", ""role"": ""required"", ""location"": { ""kind"": ""sourceTray"" } }
  ],
  ""rules"": [
    { ""id"": ""r1-zone"", ""kind"": ""zone"", ""subject"": { ""kind"": ""instance"", ""value"": ""book-1"" }, ""zoneId"": ""left"" },
    { ""id"": ""r2-adjacent"", ""kind"": ""adjacencyRequired"", ""subject"": { ""kind"": ""instance"", ""value"": ""sneaker-1"" },
      ""target"": { ""kind"": ""instance"", ""value"": ""book-1"" } },
    { ""id"": ""r3-apart"", ""kind"": ""adjacencyForbidden"", ""subject"": { ""kind"": ""tag"", ""value"": ""clothes"" },
      ""target"": { ""kind"": ""tag"", ""value"": ""tech"" } },
    { ""id"": ""r4-access"", ""kind"": ""access"", ""subject"": { ""kind"": ""instance"", ""value"": ""laptop-1"" } }
  ],
  ""objective"": { ""profile"": ""pack"", ""requiredInstanceIds"": [" + required + @"] }
}";

        public static string Main => Json(
            @"{ ""kind"": ""suitcase"", ""compartmentId"": ""main"", ""x"": 2, ""y"": 0, ""layer"": 0, ""rotation"": 0 }", "required",
            @"""book-1"", ""laptop-1"", ""sneaker-1"", ""sweater-1""", "");

        /// <summary>Variant: the laptop starts in an extraction destination, so it has left the active rule domain.</summary>
        public static string LaptopExtracted => Json(@"{ ""kind"": ""destination"", ""destinationId"": ""security"" }", "none",
            @"""book-1"", ""sneaker-1"", ""sweater-1""",
            @"
  ""destinations"": [ { ""id"": ""security"", ""capacity"": 1, ""acceptedInstanceIds"": [""laptop-1""] } ],");

        public static PuzzleLevel Load(string json = null) => LevelJsonLoaderV2.Load(json ?? Main, PuzzleItemCatalog.Create());
    }
}
