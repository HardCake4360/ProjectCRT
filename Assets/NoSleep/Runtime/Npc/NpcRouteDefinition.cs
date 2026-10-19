using System;
using System.Collections.Generic;
using UnityEngine;

namespace NoSleep
{
    [Serializable]
    public sealed class NpcRoutePoint
    {
        public Vector2Int cell;
        public float yaw;
        [Min(0)] public float waitSeconds;
    }

    [CreateAssetMenu(fileName = "NpcRoute", menuName = "No Sleep/NPC/Route")]
    public sealed class NpcRouteDefinition : ScriptableObject
    {
        public string routeId;
        public List<NpcRoutePoint> points = new List<NpcRoutePoint>();
        public bool repeat;

        public bool EnsureStableIds()
        {
            if (!string.IsNullOrWhiteSpace(routeId)) return false;
            routeId = Guid.NewGuid().ToString("N");
            return true;
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (!EventProgram.ValidIdentity(routeId)) errors.Add("NPC route ID is missing or invalid.");
            if (points == null || points.Count == 0) errors.Add("NPC route requires at least one point.");
            if (points != null)
                for (int i = 0; i < points.Count; i++)
                {
                    var point = points[i];
                    if (point == null) { errors.Add("NPC route point " + i + " is missing."); continue; }
                    if (!float.IsFinite(point.yaw) || !float.IsFinite(point.waitSeconds) || point.waitSeconds < 0)
                        errors.Add("NPC route point " + i + " has an invalid yaw or wait duration.");
                }
            return errors;
        }
    }
}
