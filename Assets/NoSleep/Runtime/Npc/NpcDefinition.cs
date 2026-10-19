using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    [Serializable]
    public sealed class NpcScheduleEntry
    {
        public string scheduleId;
        [Min(1)] public int firstDay = 1;
        [Min(0)] public int lastDay;
        [Range(0, 1439)] public int startMinute;
        [Range(0, 1440)] public int endMinute = 1440;
        public List<StateCondition> conditions = new List<StateCondition>();
        public NpcRouteDefinition route;
        public Vector2Int stationCell;
        public float yaw;
    }

    [CreateAssetMenu(fileName = "Npc", menuName = "No Sleep/NPC/Definition")]
    public sealed class NpcDefinition : ScriptableObject
    {
        public string npcId;
        public string displayName = "NPC";
        public GameObject prefab;
        public Color fallbackColor = new Color(0.4f, 0.7f, 0.85f);
        [Tooltip("Grid cells per second.")]
        [Min(0.01f)] public float speed = 1.5f;
        public bool blocksMovement = true;
        public NpcRouteDefinition route;
        public List<NpcScheduleEntry> schedules = new List<NpcScheduleEntry>();
        public EnemyDefinition enemy;
        public bool approachPlayer = true;
        [Min(0)] public float detectionRadius = 5;
        [Min(0)] public float leashRadius = 8;
        [Min(0)] public float contactRadius = 1.05f;
        public bool requireLineOfSight = true;
        [Min(0)] public int maxBlockedRetries = 4;
        [Min(0.01f)] public float retryInterval = 0.25f;
        [Range(1, 65536)] public int pathNodeBudget = 2048;

        public bool EnsureStableIds()
        {
            bool changed = false;
            if (string.IsNullOrWhiteSpace(npcId)) { npcId = Guid.NewGuid().ToString("N"); changed = true; }
            if (schedules != null) foreach (var entry in schedules)
                if (entry != null && string.IsNullOrWhiteSpace(entry.scheduleId))
                { entry.scheduleId = Guid.NewGuid().ToString("N"); changed = true; }
            return changed;
        }

        public NpcRouteDefinition FindRoute(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (route != null && route.routeId == id) return route;
            if (schedules != null) foreach (var entry in schedules)
                if (entry?.route != null && entry.route.routeId == id) return entry.route;
            return null;
        }

        public NpcScheduleEntry FindSchedule(string id)
        {
            if (string.IsNullOrEmpty(id) || schedules == null) return null;
            return schedules.Find(entry => entry != null && entry.scheduleId == id);
        }

        public NpcScheduleEntry ResolveSchedule(GameState state, string eventKey, SurvivalSettings settings)
        {
            int elapsed = Mathf.Max(0, state?.survival?.elapsedMinutes ?? 0);
            int startHour = state?.survival != null && state.survival.initialized ? state.survival.startHour : (settings == null ? 8 : settings.initialHour);
            long absolute = (long)Mathf.Clamp(startHour, 0, 23) * 60 + elapsed;
            int day = (int)(absolute / 1440) + 1;
            int minute = (int)(absolute % 1440);
            NpcScheduleEntry selected = null;
            if (schedules != null) foreach (var entry in schedules)
            {
                if (entry == null) continue;
                int anchorDay = day;
                bool timeMatches;
                if (entry.startMinute < entry.endMinute) timeMatches = minute >= entry.startMinute && minute < entry.endMinute;
                else
                {
                    timeMatches = minute >= entry.startMinute || minute < entry.endMinute;
                    if (minute < entry.endMinute) anchorDay--;
                }
                if (!timeMatches || anchorDay < entry.firstDay || (entry.lastDay > 0 && anchorDay > entry.lastDay)) continue;
                bool matches = true;
                if (entry.conditions != null) foreach (var condition in entry.conditions)
                    if (condition == null || !condition.Evaluate(state, eventKey)) { matches = false; break; }
                if (matches) selected = entry;
            }
            return selected;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (!EventProgram.ValidIdentity(npcId)) errors.Add("NPC identity is missing or invalid.");
            if (!float.IsFinite(speed) || speed <= 0) errors.Add("NPC speed must be positive and finite.");
            if (!float.IsFinite(detectionRadius) || !float.IsFinite(leashRadius) || !float.IsFinite(contactRadius) || detectionRadius < 0 || leashRadius < detectionRadius || contactRadius < 0)
                errors.Add("NPC detection/contact radii must be finite and nonnegative, and leash radius must cover detection radius.");
            if (maxBlockedRetries < 0 || maxBlockedRetries > 100 || !float.IsFinite(retryInterval) || retryInterval <= 0 || pathNodeBudget < 1 || pathNodeBudget > 65536)
                errors.Add("NPC retry and path budgets are invalid.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var routes = new HashSet<NpcRouteDefinition>();
            if (route != null) routes.Add(route);
            if (schedules != null) for (int i = 0; i < schedules.Count; i++)
            {
                var entry = schedules[i];
                if (entry == null) { errors.Add("NPC schedule " + i + " is missing."); continue; }
                if (!EventProgram.ValidIdentity(entry.scheduleId) || !ids.Add(entry.scheduleId)) errors.Add("NPC schedule identity is missing, invalid or duplicated at " + i + ".");
                if (entry.firstDay < 1 || entry.lastDay < 0 || (entry.lastDay != 0 && entry.lastDay < entry.firstDay) || entry.startMinute < 0 || entry.startMinute > 1439 || entry.endMinute < 0 || entry.endMinute > 1440 || entry.startMinute == entry.endMinute || !float.IsFinite(entry.yaw))
                    errors.Add("NPC schedule " + i + " has an invalid day/time/facing range.");
                if (entry.conditions != null) foreach (var condition in entry.conditions)
                    if (condition == null || !Enum.IsDefined(typeof(ConditionKind), condition.kind) || !Enum.IsDefined(typeof(NumberComparison), condition.comparison) ||
                        (condition.kind != ConditionKind.Always && condition.kind != ConditionKind.Day && condition.kind != ConditionKind.EffectiveFatigue && string.IsNullOrWhiteSpace(condition.key)))
                        errors.Add("NPC schedule " + i + " has an invalid condition.");
                if (entry.route != null) routes.Add(entry.route);
            }
            var routeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in routes)
            {
                errors.AddRange(item.Validate());
                if (!string.IsNullOrEmpty(item.routeId) && !routeIds.Add(item.routeId)) errors.Add("NPC route IDs collide across referenced routes.");
            }
            return errors;
        }
    }
}
