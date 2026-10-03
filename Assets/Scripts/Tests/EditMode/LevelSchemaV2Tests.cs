using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ZipTrip.Domain;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;
using ZipTrip.Unity;
using static ZipTrip.Tests.EditMode.PuzzleFixtures;

namespace ZipTrip.Tests.EditMode
{
    public sealed class LevelSchemaV2Tests
    {
        // ---- Catalog and JSON helpers ----

        private static ItemSpec[] Catalog() => new[]
        {
            Block("book", 2, 1),
            Block("laptop", 2, 2, tags: new[] { "tech" }),
            Block("cable", 1, 1, tags: new[] { "tech", "small" }),
            Block("shoe", 2, 1, nest: new NestSpec(1, new[] { "socks" }, null), tags: new[] { "shoes" }),
            Block("bag", 2, 2, nest: new NestSpec(1, new[] { "shoe" }, null)),
            Block("socks", 1, 1, tags: new[] { "small" }),
            Block("mug", 1, 1, tags: new[] { "souvenir", "fragile" }),
            Block("globe", 1, 1, tags: new[] { "fragile" }),
            new ItemSpec("sweater", "open",
                new[] { new ItemStateSpec("open", Rect(3, 3), 1, AllRotations), new ItemStateSpec("folded", Rect(2, 4), 1, AllRotations) },
                new[] { "soft" },
                new[] { new StateTransition(ItemModifier.Fold, "open", "folded"), new StateTransition(ItemModifier.Fold, "folded", "open") }),
            Jacket()
        };

        private static PuzzleLevel Fixture(string name) =>
            LevelJsonLoaderV2.Load(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Scripts/Tests/EditMode/Fixtures/LevelsV2", name)), Catalog());

        // Main 4x3, L = 2, (3,1) masked, bottom row is zone "bottom".
        private const string Main =
            "{'id':'main','width':4,'height':3,'layers':2,'rows':['####','###.','####'],'zones':[{'id':'bottom','cells':[{'x':0,'y':2},{'x':1,'y':2},{'x':2,'y':2},{'x':3,'y':2}]}]}";

        private const string Tray = "[{'id':'tray','capacity':1,'acceptedRoles':['extractionTarget']}]";
        private const string PackBook = "{'profile':'pack','requiredInstanceIds':['book-1']}";

        private static string Json(string items, string rules = "[]", string objective = PackBook, int staging = 2,
            string destinations = Tray, string compartments = "[" + Main + "]", string version = "2") =>
            ("{'schemaVersion':" + version + ",'id':'T','board':{'compartments':" + compartments + "},'stagingCapacity':" + staging +
             ",'destinations':" + destinations + ",'items':[" + items + "],'rules':" + rules + ",'objective':" + objective + "}").Replace('\'', '"');

        private static string InTray(string id, string definition, string extra = "") =>
            "{'id':'" + id + "','definitionId':'" + definition + "'" + extra + ",'location':{'kind':'sourceTray'}}";

        private static string Staged(string id, string definition) =>
            "{'id':'" + id + "','definitionId':'" + definition + "','location':{'kind':'staging'}}";

        private static string Nested(string id, string definition, string parent) =>
            "{'id':'" + id + "','definitionId':'" + definition + "','location':{'kind':'nested','parentId':'" + parent + "'}}";

        private static string InDestination(string id, string definition, string destination, string role = "") =>
            "{'id':'" + id + "','definitionId':'" + definition + "'" + (role == "" ? "" : ",'role':'" + role + "'") +
            ",'location':{'kind':'destination','destinationId':'" + destination + "'}}";

        private static string Placed(string id, string definition, int x, int y, int layer = 0, int rotation = 0, string compartment = "main", string extra = "") =>
            "{'id':'" + id + "','definitionId':'" + definition + "'" + extra + ",'location':{'kind':'suitcase','compartmentId':'" + compartment +
            "','x':" + x + ",'y':" + y + ",'layer':" + layer + ",'rotation':" + rotation + "}}";

        private static string Rule(string id, string kind, string subject, string target = null, string zone = null) =>
            "{'id':'" + id + "','kind':'" + kind + "','subject':" + subject + (target == null ? "" : ",'target':" + target) +
            (zone == null ? "" : ",'zoneId':'" + zone + "'") + "}";

        private static string Sel(string kind, string value) => "{'kind':'" + kind + "','value':'" + value + "'}";

        private static PuzzleLevel Load(string json) => LevelJsonLoaderV2.Load(json, Catalog());

        private static void Rejected(string json, string code)
        {
            Assert.That(() => Load(json), Throws.InstanceOf<Exception>().With.Message.StartsWith(code));
        }

        // ---- Fixtures ----

        [Test]
        public void Fixtures_LoadIntoDomainObjects()
        {
            var simple = Fixture("pack-simple.json");
            Assert.That(simple.Id, Is.EqualTo("fixture-pack-simple"));
            Assert.That(simple.InitialState.GetItems(ItemLocationKind.SourceTray).Count, Is.EqualTo(3));
            Assert.That(simple.Objective.Profile, Is.EqualTo(ObjectiveProfile.Pack));

            var layered = Fixture("pack-layered.json");
            layered.InitialState.TryGetItem("globe-1", out var globe);
            Assert.That(globe.Location.Placement.Layer, Is.EqualTo(1), "authored layer kept as written");
            Assert.That(layered.Spec.Board.Compartments.Select(c => c.Id), Is.EqualTo(new[] { "main", "pocket" }));
            Assert.That(layered.Spec.Board.Compartments[0].IsValidColumn(new Cell(3, 0)), Is.False);
            Assert.That(layered.Rules.Rules.Select(r => r.Id), Is.EqualTo(new[] { "globe-on-soft", "jacket-bottom" }));

            var extract = Fixture("extract.json");
            Assert.That(extract.Objective.ExtractionTargetInstanceId, Is.EqualTo("laptop-a"));
            Assert.That(extract.Spec.Destinations.Single().AcceptedInstanceIds, Is.EqualTo(new[] { "laptop-a" }));
            Assert.That(extract.Spec.Destinations.Single().AcceptedDefinitionIds, Is.Empty);
            Assert.That(extract.InitialState.GetChildren("shoe-1").Single().InstanceId, Is.EqualTo("socks-1"));

            var repack = Fixture("repack.json");
            Assert.That(repack.Objective.ExistingInstanceIds, Is.EqualTo(new[] { "book-1", "laptop-1", "socks-1" }));
            Assert.That(repack.Objective.IncomingInstanceIds, Is.EqualTo(new[] { "globe-1", "mug-1" }));
            Assert.That(repack.Spec.StagingCapacity, Is.EqualTo(1));
        }

        [Test]
        public void InitialRuleViolationAndIncompleteObjective_AreAllowed()
        {
            var extract = Fixture("extract.json");
            Assert.That(RuleEvaluator.Evaluate(extract.InitialState, extract.Rules).Single().IsSatisfied, Is.False);
            Assert.That(CompletionEvaluator.Evaluate(extract.InitialState, extract.Objective, extract.Rules).IsComplete, Is.False);
            Assert.That(BoardInvariants.Evaluate(extract.InitialState).IsValid, Is.True);
        }

        [Test]
        public void LoadedFixture_PlaysThroughTheSharedKernel()
        {
            var level = Fixture("extract.json");
            var state = PuzzleTransitions.Apply(level.InitialState, PuzzleMove.MoveToStaging("book-1")).State;
            state = PuzzleTransitions.Apply(state, PuzzleMove.MoveToDestination("laptop-a", "security-tray")).State;
            state = PuzzleTransitions.Apply(state, PuzzleMove.PlaceInSuitcase("book-1", "main", new Cell(0, 0), Rotation.Degrees0)).State;
            Assert.That(CompletionEvaluator.Evaluate(state, level.Objective, level.Rules).IsComplete, Is.True);
        }

        // ---- Version ----

        [Test]
        public void UnsupportedOrMissingVersion_IsRejected_AndV1JsonIsNotReinterpreted()
        {
            Rejected(Json(InTray("book-1", "book"), version: "3"), "UnsupportedSchemaVersion: 3");
            Rejected(Json(InTray("book-1", "book")).Replace("\"schemaVersion\":2,", ""), "UnsupportedSchemaVersion: missing");
            var v1 = File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Resources/Levels/L1.json"));
            Rejected(v1, "UnsupportedSchemaVersion: 1");
        }

        // ---- Ids / references ----

        [Test]
        public void IdAndReferenceErrors_AreRejected()
        {
            Rejected(Json(InTray("book-1", "book") + "," + Staged("book-1", "book")), "DuplicateInstanceId");
            Rejected(Json(InTray("book-1", "kettle")), "UnknownItemDefinition");
            Rejected(Json(InTray("book-1", "book", ",'stateId':'folded'")), "UnknownState");
            Rejected(Json(Placed("book-1", "book", 0, 0, compartment: "lid")), "UnknownCompartment");
            Rejected(Json(InTray("book-1", "book") + "," + Nested("socks-1", "socks", "ghost")), "UnknownNestParent");
            Rejected(Json(InTray("book-1", "book") + "," + Nested("shoe-1", "shoe", "socks-1") + "," + Nested("socks-1", "socks", "shoe-1")), "NestCycle");
            Rejected(Json(InTray("book-1", "book", ",'role':'vip'")), "UnknownObjectiveRole");
        }

        [Test]
        public void LocationShapeErrors_AreRejected()
        {
            Rejected(Json("{'id':'book-1','definitionId':'book','location':{'kind':'shelf'}}"), "UnknownLocationKind");
            Rejected(Json("{'id':'book-1','definitionId':'book','location':{'kind':'sourceTray','x':1}}"), "UnexpectedField");
            Rejected(Json("{'id':'book-1','definitionId':'book','location':{'kind':'staging','parentId':'x'}}"), "UnexpectedField");
            Rejected(Json("{'id':'book-1','definitionId':'book','location':{'kind':'suitcase','compartmentId':'main','x':0,'y':0,'rotation':0}}"),
                "MalformedRequiredField");
            Rejected(Json("{'id':'book-1','definitionId':'book'}"), "MalformedRequiredField");
            Rejected(Json(Placed("book-1", "book", 0, 2, rotation: 45)), "InvalidInitialPlacement");
            Rejected(Json(InTray("book-1", "book"), compartments: "[{'id':'main','width':4,'height':2,'layers':2,'rows':['####','###']}]"),
                "InvalidCompartmentMask");
        }

        // ---- Source Tray nesting ----

        [Test]
        public void SourceTrayAncestor_IsRejectedDirectlyAndIndirectly_StagedParentAccepted()
        {
            Rejected(Json(InTray("book-1", "book") + "," + InTray("shoe-1", "shoe") + "," + Nested("socks-1", "socks", "shoe-1")),
                "SourceTrayNestedAncestor");

            // Ids sort so the grandchild is checked first: proves the walk reaches the Source Tray root.
            Assert.That(() => Load(Json(InTray("book-1", "book") + "," + InTray("c-bag", "bag") + "," + Nested("b-shoe", "shoe", "c-bag") + "," +
                Nested("a-socks", "socks", "b-shoe"))),
                Throws.ArgumentException.With.Message.StartsWith("SourceTrayNestedAncestor").And.Message.Contains("a-socks under c-bag"));

            var staged = Load(Json(InTray("book-1", "book") + "," + Staged("shoe-1", "shoe") + "," + Nested("socks-1", "socks", "shoe-1")));
            Assert.That(staged.InitialState.GetChildren("shoe-1").Count, Is.EqualTo(1));
        }

        // ---- Placement / invariants ----

        [Test]
        public void InitialPlacementInvariants_AreEnforced()
        {
            Rejected(Json(Placed("book-1", "book", 3, 0)), "InvalidInitialBoardState: T OutOfBounds");
            Rejected(Json(Placed("book-1", "book", 2, 1)), "InvalidInitialBoardState: T MaskedCell");
            Rejected(Json(Placed("book-1", "book", 0, 0) + "," + Placed("book-2", "book", 1, 0)), "InvalidInitialBoardState: T Overlap");
            Rejected(Json(Placed("book-1", "book", 0, 0, layer: 1)), "InvalidInitialBoardState: T Unsupported");

            var stacked = Load(Json(Placed("book-1", "book", 0, 0) + "," + Placed("globe-1", "globe", 1, 0, layer: 1)));
            stacked.InitialState.TryGetItem("globe-1", out var globe);
            Assert.That(globe.Location.Placement.Layer, Is.EqualTo(1));
            Rejected(Json(Placed("book-1", "book", 0, 0) + "," + Placed("globe-1", "globe", 1, 0, layer: 0)), "InvalidInitialBoardState: T Overlap");
        }

        // ---- Staging ----

        [Test]
        public void StagingCapacity_IsValidated()
        {
            Rejected(Json(InTray("book-1", "book"), staging: -1), "InvalidStagingCapacity");
            Rejected(Json(InTray("book-1", "book") + "," + Staged("a", "mug") + "," + Staged("b", "globe"), staging: 1),
                "InvalidInitialBoardState: T StagingOverCapacity");
            Assert.That(Load(Json(InTray("book-1", "book") + "," + Staged("shoe-1", "shoe") + "," + Nested("socks-1", "socks", "shoe-1"), staging: 1)),
                Is.Not.Null, "nested children do not take their own staging slot");
        }

        // ---- Destination ----

        [Test]
        public void DestinationErrors_AndInstanceVsDefinitionIdentity()
        {
            Rejected(Json(InTray("book-1", "book") + "," + InDestination("l", "laptop", "bin", "extractionTarget")), "UnknownDestination");
            Rejected(Json(InTray("book-1", "book") + "," + InDestination("l", "laptop", "tray", "extractionTarget") + "," +
                InDestination("m", "laptop", "tray", "extractionTarget")), "InvalidInitialBoardState: T DestinationOverCapacity");
            Rejected(Json(InTray("book-1", "book") + "," + InDestination("m", "mug", "tray")), "InvalidInitialBoardState: T DestinationNotAccepted");

            var byInstance = "[{'id':'tray','capacity':1,'acceptedInstanceIds':['laptop-a']}]";
            Rejected(Json(InTray("book-1", "book") + "," + Placed("laptop-a", "laptop", 0, 0) + "," + InDestination("laptop-b", "laptop", "tray"),
                destinations: byInstance), "InvalidInitialBoardState: T DestinationNotAccepted");
            Assert.That(Load(Json(InTray("book-1", "book") + "," + Placed("laptop-b", "laptop", 0, 0) + "," + InDestination("laptop-a", "laptop", "tray"),
                destinations: byInstance)).Spec.Destinations.Single().AcceptedInstanceIds, Is.EqualTo(new[] { "laptop-a" }));
            Rejected(Json(InTray("book-1", "book"), destinations: "[{'id':'tray','capacity':0,'acceptedRoles':['extractionTarget']}]"),
                "InvalidDestinationCapacity");
        }

        // ---- Objectives ----

        [Test]
        public void ObjectiveErrors_AreRejected()
        {
            Rejected(Json(InTray("book-1", "book"), objective: "{'profile':'pack','requiredInstanceIds':['book-1','ghost']}"), "UnknownObjectiveItem");
            Rejected(Json(InTray("book-1", "book"), objective: "{'profile':'pack','requiredInstanceIds':['book-1','book-1']}"), "MalformedObjective");
            Rejected(Json(InTray("book-1", "book") + "," + Placed("mug-1", "mug", 0, 0),
                objective: "{'profile':'repack','existingInstanceIds':['mug-1'],'incomingInstanceIds':['mug-1']}"), "MalformedObjective");
            Rejected(Json(InTray("book-1", "book"), objective: "{'profile':'extract','targetInstanceId':'ghost','destinationId':'tray'}"),
                "UnknownObjectiveItem");
            Rejected(Json(InTray("book-1", "book"), objective: "{'profile':'extract','targetInstanceId':'book-1','destinationId':'bin'}"),
                "UnknownExtractionDestination");
            Rejected(Json(InTray("book-1", "book"), objective: "{'profile':'pack','requiredInstanceIds':['book-1'],'targetInstanceId':'book-1'}"),
                "UnexpectedField");
            Rejected(Json(InTray("book-1", "book"), objective: "{'profile':'sort'}"), "UnknownObjectiveProfile");
        }

        // ---- Rules ----

        [Test]
        public void RuleErrors_AreRejected()
        {
            var book = InTray("book-1", "book");
            Rejected(Json(book, "[" + Rule("r", "access", Sel("instance", "book-1")) + "," + Rule("r", "access", Sel("tag", "x")) + "]"), "DuplicateRuleId");
            Rejected(Json(book, "[" + Rule("r", "zone", Sel("instance", "book-1"), zone: "top") + "]"), "UnknownZone");
            Rejected(Json(book, "[" + Rule("r", "access", Sel("instance", "ghost")) + "]"), "InvalidRuleSelector");
            Rejected(Json(book, "[" + Rule("r", "access", Sel("definition", "kettle")) + "]"), "InvalidRuleSelector");
            Rejected(Json(book, "[" + Rule("r", "access", Sel("colour", "red")) + "]"), "InvalidRuleSelector");
            Rejected(Json(book, "[" + Rule("r", "access", Sel("tag", " ")) + "]"), "MalformedSelector");
            Rejected(Json(book, "[" + Rule("r", "access", Sel("instance", "book-1"), Sel("tag", "x")) + "]"), "MalformedRule");
            Rejected(Json(book, "[" + Rule("r", "adjacencyRequired", Sel("instance", "book-1")) + "]"), "MalformedRule");
            Rejected(Json(book, "[" + Rule("r", "zone", Sel("instance", "book-1")) + "]"), "MalformedRule");
            Rejected(Json(book, "[" + Rule("r", "access", Sel("instance", "book-1"), zone: "bottom") + "]"), "MalformedRule");
            Rejected(Json(book, "[" + Rule("r", "group", Sel("tag", "x")) + "]"), "UnknownRuleKind");

            var ok = Load(Json(book, "[" + Rule("r", "zone", Sel("tag", "unused-tag"), zone: "bottom") + "]"));
            Assert.That(ok.Rules.Rules.Single().Id, Is.EqualTo("r"), "zero-match tags are not rejected (open authoring policy)");
        }

        [Test]
        public void ErrorsCarryTheLevelId_AndDomainReason()
        {
            Assert.That(() => Load(Json(InTray("book-1", "kettle"))),
                Throws.ArgumentException.With.Message.Contains("[level T]").And.Message.Contains("kettle"));
        }

        // ---- Determinism ----

        [Test]
        public void ReorderedEquivalentInput_ProducesEquivalentDomainOutput()
        {
            const string pocket = "{'id':'pocket','width':2,'height':1,'layers':1,'rows':['##']}";
            var a = Load(Json(
                Placed("book-1", "book", 0, 0) + "," + InTray("mug-1", "mug") + "," + Placed("laptop-a", "laptop", 0, 1, extra: ",'role':'extractionTarget'"),
                "[" + Rule("b-rule", "access", Sel("tag", "tech")) + "," + Rule("a-rule", "zone", Sel("instance", "mug-1"), zone: "bottom") + "]",
                "{'profile':'pack','requiredInstanceIds':['mug-1','book-1']}",
                destinations: "[{'id':'tray','capacity':1,'acceptedTags':['tech','fragile'],'acceptedRoles':['extractionTarget']}]",
                compartments: "[" + Main + "," + pocket + "]"));
            var b = Load(Json(
                InTray("mug-1", "mug") + "," + Placed("laptop-a", "laptop", 0, 1, extra: ",'role':'extractionTarget'") + "," + Placed("book-1", "book", 0, 0),
                "[" + Rule("a-rule", "zone", Sel("instance", "mug-1"), zone: "bottom") + "," + Rule("b-rule", "access", Sel("tag", "tech")) + "]",
                "{'profile':'pack','requiredInstanceIds':['book-1','mug-1']}",
                destinations: "[{'id':'tray','capacity':1,'acceptedTags':['fragile','tech'],'acceptedRoles':['extractionTarget']}]",
                compartments: "[" + pocket + "," + Main + "]"));

            Assert.That(b.InitialState.Hash, Is.EqualTo(a.InitialState.Hash));
            Assert.That(b.InitialState.ToStableBytes(), Is.EqualTo(a.InitialState.ToStableBytes()));
            Assert.That(b.Spec.Board, Is.EqualTo(a.Spec.Board));
            Assert.That(b.Rules.Rules.Select(r => r.Id), Is.EqualTo(new[] { "a-rule", "b-rule" }));
            Assert.That(a.Rules.Rules.Select(r => r.Id), Is.EqualTo(new[] { "a-rule", "b-rule" }));
            Assert.That(b.Objective.RequiredInstanceIds, Is.EqualTo(a.Objective.RequiredInstanceIds));
            Assert.That(b.Spec.Destinations.Single().AcceptedTags, Is.EqualTo(a.Spec.Destinations.Single().AcceptedTags));
        }
    }
}
