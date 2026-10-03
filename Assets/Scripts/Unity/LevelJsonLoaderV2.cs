using System;
using System.Collections.Generic;
using UnityEngine;
using ZipTrip.Domain;
using ZipTrip.Domain.Board;
using ZipTrip.Domain.Items;
using ZipTrip.Domain.Puzzle;

namespace ZipTrip.Unity
{
    /// <summary>
    /// Schema v2 adapter (Δ-10): JSON DTO -> parse -> map to Domain. All content validation beyond JSON shape is done
    /// by the Domain constructors (BoardSpec, PuzzleState, RuleSet, PuzzleObjective, PuzzleLevel). Item definitions are
    /// resolved from a caller-supplied ItemSpec catalog. This loader rejects every schemaVersion other than 2,
    /// including a missing one; v1 JSON is never reinterpreted.
    /// </summary>
    public static class LevelJsonLoaderV2
    {
        private const int MissingInteger = int.MinValue;

        public static PuzzleLevel Load(string json, IEnumerable<ItemSpec> catalog)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new FormatException("MalformedJson");
            if (catalog == null)
                throw new ArgumentException("MissingContentCatalog");

            LevelDto dto;
            try
            {
                dto = JsonUtility.FromJson<LevelDto>(json);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("MalformedJson", exception);
            }
            if (dto == null)
                throw new FormatException("MalformedJson");
            if (dto.schemaVersion != PuzzleLevel.SchemaVersion)
                throw new ArgumentException("UnsupportedSchemaVersion: " + (dto.schemaVersion == MissingInteger ? "missing" : dto.schemaVersion.ToString()));
            if (string.IsNullOrWhiteSpace(dto.id))
                throw new ArgumentException("MissingLevelId");

            try
            {
                return Map(dto, Catalog(catalog));
            }
            catch (ArgumentException exception) when (!exception.Message.Contains("[level "))
            {
                throw new ArgumentException($"{exception.Message} [level {dto.id}]", exception);
            }
        }

        private static PuzzleLevel Map(LevelDto dto, Dictionary<string, ItemSpec> catalog)
        {
            Require(dto.board != null && dto.board.compartments != null, "board.compartments");
            Require(dto.items != null, "items");
            Require(dto.rules != null, "rules");
            Require(dto.objective != null, "objective");
            Require(dto.stagingCapacity != MissingInteger, "stagingCapacity");

            var compartments = new List<Compartment>();
            foreach (var compartment in dto.board.compartments)
                compartments.Add(MapCompartment(compartment));
            var board = new BoardSpec(compartments);

            var destinations = new List<ExtractionDestinationSpec>();
            if (dto.destinations != null)
                foreach (var destination in dto.destinations)
                {
                    Require(destination != null && destination.capacity != MissingInteger, "destinations[].capacity");
                    destinations.Add(new ExtractionDestinationSpec(destination.id, destination.capacity,
                        destination.acceptedInstanceIds, destination.acceptedDefinitionIds, destination.acceptedTags,
                        MapRoles(destination.acceptedRoles)));
                }
            var spec = new PuzzleSpec(board, dto.stagingCapacity, destinations);

            var items = new List<PuzzleItem>();
            foreach (var item in dto.items)
                items.Add(MapItem(item, catalog));
            var state = new PuzzleState(spec, items);

            var rules = new List<PuzzleRule>();
            foreach (var rule in dto.rules)
                rules.Add(MapRule(rule));

            return new PuzzleLevel(dto.id, state, new RuleSet(board, rules), MapObjective(dto.objective));
        }

        private static Compartment MapCompartment(CompartmentDto dto)
        {
            Require(dto != null && dto.width != MissingInteger && dto.height != MissingInteger && dto.layers != MissingInteger,
                "compartments[].width/height/layers");
            Require(dto.rows != null, "compartments[].rows");
            if (dto.rows.Length != dto.height)
                throw new ArgumentException($"InvalidCompartmentMask: {dto.id} has {dto.rows.Length} rows, expected {dto.height}");

            // Mask rows are written top (y = 0) to bottom: '#' = valid cell, '.' = blocked.
            var valid = new List<Cell>();
            for (var y = 0; y < dto.rows.Length; y++)
            {
                var row = dto.rows[y] ?? "";
                if (row.Length != dto.width)
                    throw new ArgumentException($"InvalidCompartmentMask: {dto.id} row {y} has {row.Length} cells, expected {dto.width}");
                for (var x = 0; x < row.Length; x++)
                {
                    if (row[x] == '#')
                        valid.Add(new Cell(x, y));
                    else if (row[x] != '.')
                        throw new ArgumentException($"InvalidCompartmentMask: {dto.id} ({x},{y}) '{row[x]}'");
                }
            }

            var zones = new List<KeyValuePair<Cell, string>>();
            if (dto.zones != null)
                foreach (var zone in dto.zones)
                {
                    Require(zone != null && zone.cells != null, "compartments[].zones[].cells");
                    foreach (var cell in zone.cells)
                    {
                        Require(cell != null && cell.x != MissingInteger && cell.y != MissingInteger, "zones[].cells[].x/y");
                        zones.Add(new KeyValuePair<Cell, string>(new Cell(cell.x, cell.y), zone.id));
                    }
                }
            return new Compartment(dto.id, dto.width, dto.height, dto.layers, valid, zones);
        }

        private static PuzzleItem MapItem(ItemDto dto, Dictionary<string, ItemSpec> catalog)
        {
            Require(dto != null, "items[]");
            if (string.IsNullOrWhiteSpace(dto.definitionId) || !catalog.TryGetValue(dto.definitionId, out var definition))
                throw new ArgumentException($"UnknownItemDefinition: {dto.id} {dto.definitionId}");
            var stateId = string.IsNullOrEmpty(dto.stateId) ? definition.DefaultStateId : dto.stateId;
            return new PuzzleItem(dto.id, definition, stateId, MapRole(dto.role, dto.id), MapLocation(dto.location, dto.id));
        }

        private static ItemLocation MapLocation(LocationDto dto, string instanceId)
        {
            var kind = dto?.kind;
            if (string.IsNullOrEmpty(kind))
                throw new FormatException("MalformedRequiredField: items[].location.kind " + instanceId);

            var isSuitcase = kind == "suitcase";
            var placementFields = !string.IsNullOrEmpty(dto.compartmentId) || dto.x != MissingInteger || dto.y != MissingInteger
                || dto.layer != MissingInteger || dto.rotation != MissingInteger;
            if (!isSuitcase && placementFields)
                throw new ArgumentException($"UnexpectedField: {instanceId} placement fields on a {kind} location");
            if (kind != "nested" && !string.IsNullOrEmpty(dto.parentId))
                throw new ArgumentException($"UnexpectedField: {instanceId} parentId on a {kind} location");
            if (kind != "destination" && !string.IsNullOrEmpty(dto.destinationId))
                throw new ArgumentException($"UnexpectedField: {instanceId} destinationId on a {kind} location");
            if (kind != "staging" && dto.slot != MissingInteger)
                throw new ArgumentException($"UnexpectedField: {instanceId} slot on a {kind} location");

            switch (kind)
            {
                case "sourceTray":
                    return ItemLocation.SourceTray;
                case "staging":
                    Require(dto.slot != MissingInteger, "location.slot " + instanceId);
                    if (dto.slot < 0)
                        throw new ArgumentException($"InvalidStagingSlot: {instanceId} slot {dto.slot}");
                    return ItemLocation.InStaging(dto.slot);
                case "nested":
                    Require(!string.IsNullOrEmpty(dto.parentId), "location.parentId " + instanceId);
                    return ItemLocation.NestedIn(dto.parentId);
                case "destination":
                    Require(!string.IsNullOrEmpty(dto.destinationId), "location.destinationId " + instanceId);
                    return ItemLocation.InDestination(dto.destinationId);
                case "suitcase":
                    // Authored initial layer is explicit; it is validated by BoardInvariants, never re-resolved.
                    Require(!string.IsNullOrEmpty(dto.compartmentId) && dto.x != MissingInteger && dto.y != MissingInteger
                        && dto.layer != MissingInteger && dto.rotation != MissingInteger,
                        "location.compartmentId/x/y/layer/rotation " + instanceId);
                    if (!Enum.IsDefined(typeof(Rotation), dto.rotation))
                        throw new ArgumentException($"InvalidInitialPlacement: {instanceId} rotation {dto.rotation}");
                    return ItemLocation.InSuitcase(new Placement(dto.compartmentId, new Cell(dto.x, dto.y), dto.layer, (Rotation)dto.rotation));
                default:
                    throw new ArgumentException($"UnknownLocationKind: {instanceId} {kind}");
            }
        }

        private static PuzzleRule MapRule(RuleDto dto)
        {
            Require(dto != null && !string.IsNullOrEmpty(dto.kind), "rules[].kind");
            var hasTarget = !string.IsNullOrEmpty(dto.target?.kind) || !string.IsNullOrEmpty(dto.target?.value);
            var isAdjacency = dto.kind == "adjacencyRequired" || dto.kind == "adjacencyForbidden";
            if (isAdjacency != hasTarget)
                throw new ArgumentException($"MalformedRule: {dto.id} {dto.kind} {(hasTarget ? "has an unexpected" : "needs a")} target selector");
            if ((dto.kind == "zone") != !string.IsNullOrEmpty(dto.zoneId))
                throw new ArgumentException($"MalformedRule: {dto.id} {dto.kind} {(dto.kind == "zone" ? "needs a" : "has an unexpected")} zoneId");

            var subject = MapSelector(dto.subject, dto.id);
            switch (dto.kind)
            {
                case "zone": return new ZoneRule(dto.id, subject, dto.zoneId);
                case "access": return new AccessRule(dto.id, subject);
                case "adjacencyRequired": return new AdjacencyRequiredRule(dto.id, subject, MapSelector(dto.target, dto.id));
                case "adjacencyForbidden": return new AdjacencyForbiddenRule(dto.id, subject, MapSelector(dto.target, dto.id));
                default: throw new ArgumentException($"UnknownRuleKind: {dto.id} {dto.kind}");
            }
        }

        private static ItemSelector MapSelector(SelectorDto dto, string ruleId)
        {
            switch (dto?.kind)
            {
                case "instance": return ItemSelector.Instance(dto.value);
                case "definition": return ItemSelector.Definition(dto.value);
                case "tag": return ItemSelector.Tag(dto.value);
                default: throw new ArgumentException($"InvalidRuleSelector: {ruleId} kind '{dto?.kind}'");
            }
        }

        private static PuzzleObjective MapObjective(ObjectiveDto dto)
        {
            var has = new Func<string[], bool>(list => list != null && list.Length > 0);
            switch (dto.profile)
            {
                case "pack":
                    Unexpected(has(dto.existingInstanceIds) || has(dto.incomingInstanceIds) || !string.IsNullOrEmpty(dto.targetInstanceId) || !string.IsNullOrEmpty(dto.destinationId), "pack");
                    return PuzzleObjective.Pack(dto.requiredInstanceIds);
                case "repack":
                    Unexpected(has(dto.requiredInstanceIds) || !string.IsNullOrEmpty(dto.targetInstanceId) || !string.IsNullOrEmpty(dto.destinationId), "repack");
                    return PuzzleObjective.Repack(dto.existingInstanceIds, dto.incomingInstanceIds);
                case "extract":
                    Unexpected(has(dto.requiredInstanceIds) || has(dto.existingInstanceIds) || has(dto.incomingInstanceIds), "extract");
                    return PuzzleObjective.Extract(dto.targetInstanceId, dto.destinationId);
                default:
                    throw new ArgumentException("UnknownObjectiveProfile: " + dto.profile);
            }
        }

        private static ObjectiveRole MapRole(string role, string instanceId)
        {
            switch (role)
            {
                case null:
                case "":
                case "none": return ObjectiveRole.None;
                case "required": return ObjectiveRole.Required;
                case "extractionTarget": return ObjectiveRole.ExtractionTarget;
                default: throw new ArgumentException($"UnknownObjectiveRole: {instanceId} {role}");
            }
        }

        private static IEnumerable<ObjectiveRole> MapRoles(string[] roles)
        {
            var result = new List<ObjectiveRole>();
            if (roles != null)
                foreach (var role in roles)
                    result.Add(MapRole(role, "destination"));
            return result;
        }

        private static Dictionary<string, ItemSpec> Catalog(IEnumerable<ItemSpec> catalog)
        {
            var byId = new Dictionary<string, ItemSpec>(StringComparer.Ordinal);
            foreach (var spec in catalog)
            {
                if (spec == null || byId.ContainsKey(spec.Id))
                    throw new ArgumentException("DuplicateOrNullItemDefinition");
                byId.Add(spec.Id, spec);
            }
            return byId;
        }

        private static void Require(bool condition, string field)
        {
            if (!condition)
                throw new FormatException("MalformedRequiredField: " + field);
        }

        private static void Unexpected(bool present, string profile)
        {
            if (present)
                throw new ArgumentException($"UnexpectedField: objective fields not used by profile {profile}");
        }

        [Serializable] private sealed class LevelDto
        {
            public int schemaVersion = MissingInteger;
            public string id = null;
            public BoardDto board = null;
            public int stagingCapacity = MissingInteger;
            public DestinationDto[] destinations = null;
            public ItemDto[] items = null;
            public RuleDto[] rules = null;
            public ObjectiveDto objective = null;
        }

        [Serializable] private sealed class BoardDto
        {
            public CompartmentDto[] compartments = null;
        }

        [Serializable] private sealed class CompartmentDto
        {
            public string id = null;
            public int width = MissingInteger;
            public int height = MissingInteger;
            public int layers = MissingInteger;
            public string[] rows = null;
            public ZoneDto[] zones = null;
        }

        [Serializable] private sealed class ZoneDto
        {
            public string id = null;
            public CellDto[] cells = null;
        }

        [Serializable] private sealed class CellDto
        {
            public int x = MissingInteger;
            public int y = MissingInteger;
        }

        [Serializable] private sealed class DestinationDto
        {
            public string id = null;
            public int capacity = MissingInteger;
            public string[] acceptedInstanceIds = null;
            public string[] acceptedDefinitionIds = null;
            public string[] acceptedTags = null;
            public string[] acceptedRoles = null;
        }

        [Serializable] private sealed class ItemDto
        {
            public string id = null;
            public string definitionId = null;
            public string stateId = null;
            public string role = null;
            public LocationDto location = null;
        }

        [Serializable] private sealed class LocationDto
        {
            public string kind = null;
            public string compartmentId = null;
            public int x = MissingInteger;
            public int y = MissingInteger;
            public int layer = MissingInteger;
            public int rotation = MissingInteger;
            public string parentId = null;
            public string destinationId = null;
            /// <summary>ZT-041: required for a staging location (explicit slot identity, no implicit assignment).</summary>
            public int slot = MissingInteger;
        }

        [Serializable] private sealed class RuleDto
        {
            public string id = null;
            public string kind = null;
            public SelectorDto subject = null;
            public SelectorDto target = null;
            public string zoneId = null;
        }

        [Serializable] private sealed class SelectorDto
        {
            public string kind = null;
            public string value = null;
        }

        [Serializable] private sealed class ObjectiveDto
        {
            public string profile = null;
            public string[] requiredInstanceIds = null;
            public string[] existingInstanceIds = null;
            public string[] incomingInstanceIds = null;
            public string targetInstanceId = null;
            public string destinationId = null;
        }
    }
}
