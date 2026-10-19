using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoSleep
{
    public static class NpcSaveValidation
    {
        public static void Validate(GameState state, IEnumerable<MapDefinition> maps, SurvivalSettings settings = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            NpcState.ValidateRecords(state.npcStates);
            var catalog = new List<MapDefinition>();
            if (maps != null) foreach (MapDefinition map in maps) if (map != null && !catalog.Contains(map)) catalog.Add(map);
            bool hasNpc = state.npcStates != null && state.npcStates.Count > 0;
            foreach (MapDefinition map in catalog) if (map.events != null && map.events.Exists(value => value?.npc != null)) hasNpc = true;
            if (!hasNpc) return; // Existing non-NPC maps keep their established behavior.
            if (state.survival == null) throw new InvalidDataException("NPC state requires a game-time checkpoint.");
            var byId = new Dictionary<string, MapDefinition>(StringComparer.Ordinal);
            foreach (MapDefinition map in catalog)
            {
                if (string.IsNullOrWhiteSpace(map.mapId) || byId.ContainsKey(map.mapId)) throw Invalid("Missing or duplicate NPC catalog map ID", map.mapId);
                byId.Add(map.mapId, map);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                if (map.events == null) continue;
                foreach (MapEventPlacement placement in map.events)
                {
                    if (placement == null) continue;
                    if (string.IsNullOrWhiteSpace(placement.id) || !ids.Add(placement.id)) throw Invalid("Missing or duplicate NPC catalog placement ID", map.mapId);
                    if (placement.npc == null) continue;
                    ValidateDefinition(map, placement);
                }
            }
            if (state.npcStates != null)
                foreach (NpcState saved in state.npcStates)
                {
                    if (!byId.TryGetValue(saved.mapId, out MapDefinition map)) throw Invalid("Unknown NPC map", saved.mapId);
                    MapEventPlacement placement = map.events?.Find(value => value != null && value.id == saved.placementId);
                    if (placement?.npc == null || placement.npc.npcId != saved.npcId) throw Invalid("Removed or changed NPC placement", saved.mapId + "/" + saved.placementId);
                    ValidateRecord(state, map, placement, saved);
                }
            foreach (MapDefinition map in catalog)
            {
                var occupied = new HashSet<Vector2Int>();
                foreach (NpcState effective in ResolveMapStates(state, map, settings))
                {
                    MapEventPlacement placement = map.events.Find(value => value != null && value.id == effective.placementId);
                    ValidateRecord(state, map, placement, effective);
                    if (!NpcMovementService.IsPlacementActive(state, map, placement)) continue;
                    if (StaticBlocks(state, map, effective.cell) || !occupied.Add(effective.cell))
                        throw Invalid("NPC position collides with another actor or static event", map.mapId + "/" + effective.placementId);
                }
            }
        }

        public static List<NpcState> ResolveMapStates(GameState state, MapDefinition map, SurvivalSettings settings = null)
        {
            if (state == null || map == null) throw new ArgumentNullException(state == null ? nameof(state) : nameof(map));
            var result = new List<NpcState>();
            if (map.events == null) return result;
            GameState working = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(state)); working.Normalize();
            foreach (MapEventPlacement placement in map.events)
            {
                if (placement?.npc == null) continue;
                bool Occupied(Vector2Int cell)
                {
                    if (!map.IsWalkable(cell) || StaticBlocks(working, map, cell)) return true;
                    foreach (MapEventPlacement other in map.events)
                    {
                        if (other?.npc == null || other == placement || !NpcMovementService.IsPlacementActive(working, map, other)) continue;
                        Vector2Int otherCell = working.GetNpcState(map.mapId, other.id)?.cell ?? other.cell;
                        if (otherCell == cell) return true;
                    }
                    return false;
                }
                NpcState resolved = NpcMovementService.ResolveBindingState(working, map, placement, settings, Occupied);
                working.SetNpcState(resolved, false); result.Add(resolved.Clone());
            }
            return result;
        }

        public static bool CanPlayerOccupy(GameState state, MapDefinition map, Vector3 localPosition, float radius, IReadOnlyList<NpcState> resolved = null)
        {
            if (map == null) return false;
            float size = Mathf.Max(0.1f, map.cellSize); radius = Mathf.Max(0, radius);
            int minX = Mathf.FloorToInt((localPosition.x - radius) / size + 0.5f), maxX = Mathf.FloorToInt((localPosition.x + radius) / size + 0.5f);
            int minY = Mathf.FloorToInt((localPosition.z - radius) / size + 0.5f), maxY = Mathf.FloorToInt((localPosition.z + radius) / size + 0.5f);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float nearX = Mathf.Clamp(localPosition.x, (x - 0.5f) * size, (x + 0.5f) * size);
                    float nearY = Mathf.Clamp(localPosition.z, (y - 0.5f) * size, (y + 0.5f) * size);
                    float dx = localPosition.x - nearX, dz = localPosition.z - nearY;
                    if (dx * dx + dz * dz > radius * radius + 0.000001f) continue;
                    var cell = new Vector2Int(x, y);
                    if (!map.IsWalkable(cell) || StaticBlocks(state, map, cell)) return false;
                    if (resolved != null) foreach (NpcState npc in resolved)
                    {
                        MapEventPlacement placement = map.events?.Find(value => value != null && value.id == npc.placementId);
                        if (npc.cell == cell && PlayerBlocks(state, map, placement)) return false;
                    }
                }
            return true;
        }

        public static bool TryFindPlayerPosition(GameState state, MapDefinition map, Vector3 requestedLocal, float radius, IReadOnlyList<NpcState> resolved, out Vector3 localPosition)
        {
            if (CanPlayerOccupy(state, map, requestedLocal, radius, resolved)) { localPosition = requestedLocal; return true; }
            bool found = false; double nearest = double.MaxValue; localPosition = default;
            // Row-major traversal supplies a stable tie-break for equal distances.
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    Vector3 candidate = map.CellToWorld(new Vector2Int(x, y));
                    if (!CanPlayerOccupy(state, map, candidate, radius, resolved)) continue;
                    double dx = candidate.x - requestedLocal.x, dz = candidate.z - requestedLocal.z;
                    double distance = dx * dx + dz * dz;
                    if (found && distance >= nearest) continue;
                    found = true; nearest = distance; localPosition = candidate;
                }
            return found;
        }

        static bool StaticBlocks(GameState state, MapDefinition map, Vector2Int cell)
        {
            if (map.events == null) return false;
            foreach (MapEventPlacement placement in map.events)
            {
                if (placement == null || placement.npc != null || placement.cell != cell || placement.definition == null) continue;
                EventPage page = placement.definition.ResolvePage(state, map.mapId + "/" + placement.id);
                if (page != null && page.blocksMovement) return true;
            }
            return false;
        }

        static bool PlayerBlocks(GameState state, MapDefinition map, MapEventPlacement placement)
        {
            if (placement?.npc == null || !placement.npc.blocksMovement || !NpcMovementService.IsPlacementActive(state, map, placement)) return false;
            return placement.definition == null || placement.definition.ResolvePage(state, map.mapId + "/" + placement.id)?.blocksMovement == true;
        }

        static void ValidateDefinition(MapDefinition map, MapEventPlacement placement)
        {
            List<string> errors = placement.npc.Validate();
            if (errors.Count > 0) throw Invalid("Invalid NPC definition: " + string.Join("; ", errors), map.mapId + "/" + placement.id);
            if (!map.IsWalkable(placement.cell) || !float.IsFinite(placement.yaw)) throw Invalid("Invalid authored NPC cell or facing", map.mapId + "/" + placement.id);
            void CheckRoute(NpcRouteDefinition route)
            {
                if (route == null) return;
                List<string> routeErrors = route.Validate();
                if (routeErrors.Count > 0) throw Invalid("Invalid NPC route: " + string.Join("; ", routeErrors), route.routeId);
                foreach (var point in route.points) if (!map.IsWalkable(point.cell)) throw Invalid("NPC route point is outside walkable terrain", map.mapId + "/" + route.routeId);
            }
            CheckRoute(placement.npc.route);
            if (placement.npc.schedules != null) foreach (var schedule in placement.npc.schedules)
            {
                CheckRoute(schedule.route);
                if (schedule.route == null && !map.IsWalkable(schedule.stationCell)) throw Invalid("NPC schedule station is outside walkable terrain", map.mapId + "/" + schedule.scheduleId);
            }
        }

        static void ValidateRecord(GameState state, MapDefinition map, MapEventPlacement placement, NpcState saved)
        {
            if (!map.IsWalkable(saved.cell)) throw Invalid("Saved NPC cell is outside walkable terrain", saved.mapId + "/" + saved.placementId);
            if (saved.lastEvaluatedMinutes > state.survival.elapsedMinutes) throw Invalid("NPC schedule checkpoint is in the future", saved.mapId + "/" + saved.placementId);
            if (!string.IsNullOrEmpty(saved.scheduleId) && (placement.npc.schedules == null || !placement.npc.schedules.Exists(value => value != null && value.scheduleId == saved.scheduleId)))
                throw Invalid("Removed NPC schedule", saved.scheduleId);
            if (!string.IsNullOrEmpty(saved.routeId))
            {
                NpcRouteDefinition route = placement.npc.FindRoute(saved.routeId);
                if (route == null || route.points == null || saved.waypointIndex >= route.points.Count) throw Invalid("Removed NPC route or invalid waypoint checkpoint", saved.routeId);
            }
        }
        static InvalidDataException Invalid(string message, string scope) => new InvalidDataException(message + ": " + scope);
    }
}
