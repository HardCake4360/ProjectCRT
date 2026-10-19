using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NoSleep
{
    [Serializable]
    public sealed class NpcState
    {
        public string mapId;
        public string placementId;
        public string npcId;
        public Vector2Int cell;
        public float yaw;
        public string routeId = "";
        public int waypointIndex;
        public bool persistentOverride;
        public string scheduleId = "";
        public int lastEvaluatedMinutes;

        public NpcState Clone() => new NpcState { mapId = mapId, placementId = placementId, npcId = npcId,
            cell = cell, yaw = yaw, routeId = routeId, waypointIndex = waypointIndex, persistentOverride = persistentOverride,
            scheduleId = scheduleId, lastEvaluatedMinutes = lastEvaluatedMinutes };

        public static void ValidateRecords(IReadOnlyList<NpcState> states)
        {
            if (states == null) return;
            var scopes = new HashSet<(string mapId, string placementId)>();
            foreach (NpcState state in states)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.mapId) || string.IsNullOrWhiteSpace(state.placementId) || string.IsNullOrWhiteSpace(state.npcId))
                    throw new InvalidDataException("An NPC state is missing its map, placement or definition identity.");
                if (!scopes.Add((state.mapId, state.placementId))) throw new InvalidDataException("Duplicate NPC state: " + state.mapId + "/" + state.placementId);
                if (!float.IsFinite(state.yaw) || state.waypointIndex < 0 || state.lastEvaluatedMinutes < 0 || (string.IsNullOrEmpty(state.routeId) && state.waypointIndex != 0))
                    throw new InvalidDataException("Invalid NPC facing or route checkpoint: " + state.mapId + "/" + state.placementId);
            }
        }
    }
}
