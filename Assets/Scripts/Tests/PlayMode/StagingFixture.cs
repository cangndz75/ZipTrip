using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;

namespace ZipTrip.Tests.PlayMode
{
    // ZT-041 staging fixture (test-only, not shipped progression): Cabin 5x7, two layers, staging capacity 2.
    // book-1 lies under sneaker-1 (book inaccessible until the sneaker leaves), laptop-1 is accessible, sweater-1 waits
    // in the Source Tray, so the Pack objective is incomplete at start.
    public static class StagingFixture
    {
        public const string Json = @"{
  ""schemaVersion"": 2,
  ""id"": ""zt041-staging-fixture"",
  ""board"": { ""compartments"": [
    { ""id"": ""main"", ""width"": 5, ""height"": 7, ""layers"": 2,
      ""rows"": [""#####"", ""#####"", ""#####"", ""#####"", ""#####"", ""#####"", ""#####""] }
  ] },
  ""stagingCapacity"": 2,
  ""items"": [
    { ""id"": ""book-1"", ""definitionId"": ""book"", ""role"": ""required"",
      ""location"": { ""kind"": ""suitcase"", ""compartmentId"": ""main"", ""x"": 0, ""y"": 0, ""layer"": 0, ""rotation"": 0 } },
    { ""id"": ""sneaker-1"", ""definitionId"": ""sneaker"", ""role"": ""required"",
      ""location"": { ""kind"": ""suitcase"", ""compartmentId"": ""main"", ""x"": 0, ""y"": 0, ""layer"": 1, ""rotation"": 0 } },
    { ""id"": ""laptop-1"", ""definitionId"": ""laptop"", ""role"": ""required"",
      ""location"": { ""kind"": ""suitcase"", ""compartmentId"": ""main"", ""x"": 2, ""y"": 0, ""layer"": 0, ""rotation"": 0 } },
    { ""id"": ""sweater-1"", ""definitionId"": ""sweater"", ""role"": ""required"", ""location"": { ""kind"": ""sourceTray"" } }
  ],
  ""rules"": [],
  ""objective"": { ""profile"": ""pack"", ""requiredInstanceIds"": [""book-1"", ""sneaker-1"", ""laptop-1"", ""sweater-1""] }
}";

        public static PuzzleLevel Load() => LevelJsonLoaderV2.Load(Json, PuzzleItemCatalog.Create());
    }
}
