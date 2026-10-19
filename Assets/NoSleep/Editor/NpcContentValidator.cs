using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class NpcContentValidator
    {
        public static List<string> CollectErrors() => CollectDiagnostics(ContentReferenceIndex.LoadAssets())
            .Where(row => row.Severity == ContentDiagnosticSeverity.Error).Select(row => row.ToString()).ToList();
        public static List<ContentDiagnostic> CollectDiagnostics(IEnumerable<ScriptableObject> source)
        {
            var assets = new HashSet<ScriptableObject>(source?.Where(value => value != null) ?? Enumerable.Empty<ScriptableObject>());
            var result = new List<ContentDiagnostic>();
            var ids = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
            var routeIds = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
            var scheduleIds = new Dictionary<string, ScriptableObject>(StringComparer.Ordinal);
            foreach (var route in assets.OfType<NpcRouteDefinition>())
            {
                foreach (string error in route.Validate()) Report(route, error);
                Unique(route.routeId, route, routeIds, "경로");
            }
            foreach (var npc in assets.OfType<NpcDefinition>())
            {
                foreach (string error in npc.Validate()) Report(npc, error);
                Unique(npc.npcId, npc, ids, "NPC");
                CheckRouteReference(npc, npc.route, "기본 경로");
                if (npc.enemy != null && !assets.Contains(npc.enemy)) Report(npc, "적 정의가 Assets/NoSleep 목록 밖에 있습니다.", ContentDiagnosticSeverity.Warning);
                if (npc.schedules != null) foreach (var schedule in npc.schedules)
                {
                    if (schedule == null) continue;
                    Unique(schedule.scheduleId, npc, scheduleIds, "일과");
                    CheckRouteReference(npc, schedule.route, "일과 경로");
                }
            }
            foreach (var map in assets.OfType<MapDefinition>())
                if (map.events != null) foreach (var placement in map.events)
                {
                    if (placement?.npc == null) continue; // A stationary event needs no NPC profile.
                    var npc = placement.npc;
                    if (!assets.Contains(npc)) Report(map, "배치 '" + placement.id + "'의 NPC 정의가 콘텐츠 목록 밖에 있습니다.", ContentDiagnosticSeverity.Warning);
                    if (!map.IsInBounds(placement.cell) || !map.IsWalkable(placement.cell))
                        Report(map, "NPC 배치 '" + placement.id + "'의 시작 칸이 범위 밖이거나 이동 불가입니다: " + placement.cell);
                    if (npc.enemy != null && (placement.definition == null || placement.definition.pages == null ||
                        !placement.definition.pages.Any(page => page != null && page.trigger == EventTrigger.PlayerTouch)))
                        Report(map, "적 NPC '" + placement.id + "'에는 PlayerTouch 이벤트 정의가 필요합니다.");
                    CheckBoundRoute(npc.route, map, placement.id);
                    if (npc.schedules != null) foreach (var schedule in npc.schedules)
                    {
                        if (schedule == null) continue;
                        if (schedule.route != null) CheckBoundRoute(schedule.route, map, placement.id);
                        else if (!map.IsInBounds(schedule.stationCell) || !map.IsWalkable(schedule.stationCell))
                            Report(npc, "맵 '" + map.mapId + "' / 배치 '" + placement.id + "'의 일과 정지점이 범위 밖이거나 이동 불가입니다: " + schedule.stationCell);
                    }
                }
            return result;
            void Unique(string id, ScriptableObject owner, Dictionary<string, ScriptableObject> seen, string label)
            {
                if (string.IsNullOrWhiteSpace(id)) return; // Validate reports the missing identity.
                if (seen.TryGetValue(id, out var previous)) Report(owner, label + " ID가 중복됩니다: " + id + " (다른 에셋: " + previous.name + ")");
                else seen.Add(id, owner);
            }
            void CheckRouteReference(NpcDefinition owner, NpcRouteDefinition route, string label)
            {
                if (route != null && !assets.Contains(route)) Report(owner, label + "가 Assets/NoSleep 목록 밖에 있습니다.", ContentDiagnosticSeverity.Warning);
            }
            void CheckBoundRoute(NpcRouteDefinition route, MapDefinition map, string placementId)
            {
                if (route?.points == null) return;
                for (int index = 0; index < route.points.Count; index++)
                {
                    var point = route.points[index]; if (point == null) continue;
                    if (!map.IsInBounds(point.cell) || !map.IsWalkable(point.cell))
                        Report(route, "맵 '" + map.mapId + "' / 배치 '" + placementId + "'의 경유지 " + (index + 1) +
                            "이 범위 밖이거나 이동 불가입니다: " + point.cell);
                }
            }
            void Report(ScriptableObject owner, string message, ContentDiagnosticSeverity severity = ContentDiagnosticSeverity.Error)
            {
                result.Add(new ContentDiagnostic { Severity = severity, Message = message,
                    Reference = new ContentReference { Owner = owner, Category = owner is NpcRouteDefinition ? ContentReferenceCategory.NpcRoute : ContentReferenceCategory.Npc,
                        Message = message, Key = owner is NpcRouteDefinition route ? route.routeId : owner is NpcDefinition npc ? npc.npcId : "" } });
            }
        }
    }
}
