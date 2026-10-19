using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NoSleep.Editor
{
    public sealed class MapConnection
    {
        public MapDefinition Source { get; internal set; }
        public MapDefinition Destination { get; internal set; }
        public Vector2Int SourceCell { get; internal set; }
        public Vector2Int Cell { get; internal set; }
        public string PlacementId { get; internal set; }
        public ScriptableObject Owner { get; internal set; }
        public int PageIndex { get; internal set; }
        public int CommandIndex { get; internal set; }
        public bool IsConditional { get; internal set; }
        public string Via { get; internal set; }
    }
    public sealed class MapConnectionAnalysis
    {
        internal readonly List<MapConnection> connections = new List<MapConnection>();
        internal readonly List<ContentDiagnostic> diagnostics = new List<ContentDiagnostic>();
        public IReadOnlyList<MapConnection> Connections => connections;
        public IReadOnlyList<ContentDiagnostic> Diagnostics => diagnostics;
        public IReadOnlyList<ContentDiagnostic> GetDiagnostics() => diagnostics;
    }

    // This is a conservative authored graph, not a proof of runtime reachability.
    public static class MapConnectionAnalyzer
    {
        public static MapConnectionAnalysis Collect(IEnumerable<MapDefinition> maps, IEnumerable<StateKeyRegistry> registries = null)
        {
            var result = new MapConnectionAnalysis();
            var catalog = new HashSet<MapDefinition>(maps?.Where(x => x != null) ?? Enumerable.Empty<MapDefinition>());
            var policies = new List<(StateKeyRegistry registry, MapLinkPolicy link)>();
            if (registries != null) foreach (var registry in registries)
                if (registry != null && registry.mapLinks != null) foreach (var link in registry.mapLinks)
                    if (link != null) policies.Add((registry, link));
            foreach (var map in catalog.OrderBy(x => x.mapId, StringComparer.Ordinal))
            {
                if (!map.IsInBounds(map.spawnCell) || !map.IsWalkable(map.spawnCell))
                    Report(map, -1, -1, "맵 시작점이 범위 밖이거나 이동할 수 없습니다: " + map.spawnCell);
                int budget = 16384;
                if (map.events == null) continue;
                foreach (var placement in map.events)
                {
                    if (placement?.definition == null) continue;
                    var stack = new HashSet<ScriptableObject>();
                    WalkEvent(placement.definition, false, placement.definition.displayName, 0);
                    void WalkEvent(EventDefinition definition, bool conditional, string path, int depth)
                    {
                        if (!Enter(definition, depth)) return;
                        try
                        {
                            if (definition.pages == null) return;
                            for (int pageIndex = 0; pageIndex < definition.pages.Count; pageIndex++)
                            {
                                var page = definition.pages[pageIndex];
                                if (page == null) continue;
                                WalkCommands(definition, pageIndex, page.commands,
                                    conditional || definition.pages.Count > 1 || (page.conditions?.Count ?? 0) > 0, path + "/" + page.name, depth);
                            }
                        }
                        finally { stack.Remove(definition); }
                    }
                    void WalkCommon(CommonEventDefinition common, bool conditional, string path, int depth)
                    {
                        if (!Enter(common, depth)) return;
                        try { WalkCommands(common, -1, common.commands, conditional, path, depth); }
                        finally { stack.Remove(common); }
                    }
                    bool Enter(ScriptableObject owner, int depth)
                    {
                        if (owner == null) return false;
                        if (depth >= 16 || stack.Contains(owner))
                        { Report(owner, -1, -1, "연결 분석 중 재귀 또는 호출 깊이 제한에 도달했습니다.", ContentDiagnosticSeverity.Warning); return false; }
                        stack.Add(owner); return true;
                    }
                    void WalkCommands(ScriptableObject owner, int pageIndex, List<EventCommand> commands, bool conditional, string path, int depth)
                    {
                        if (commands == null) return;
                        // Branches are deliberately not executed by the editor. Even a
                        // transfer after a label might be skipped or selected by a jump.
                        conditional |= commands.Any(x => x != null && (x.type == EventCommandType.IfCondition ||
                            x.type == EventCommandType.ShowChoices || x.type == EventCommandType.Jump));
                        for (int commandIndex = 0; commandIndex < commands.Count; commandIndex++)
                        {
                            if (--budget < 0)
                            {
                                if (budget == -1) Report(map, -1, -1, "맵 연결 분석 예산을 초과했습니다. 호출 구조를 단순하게 나누세요.", ContentDiagnosticSeverity.Warning);
                                return;
                            }
                            var command = commands[commandIndex]; if (command == null) continue;
                            if (command.type == EventCommandType.TransferPlayer)
                            {
                                var connection = new MapConnection { Source = map, Destination = command.map, SourceCell = placement.cell,
                                    PlacementId = placement.id, Cell = command.cell, Owner = owner, PageIndex = pageIndex,
                                    CommandIndex = commandIndex, IsConditional = conditional, Via = path };
                                result.connections.Add(connection);
                                if (command.map == null) Report(owner, pageIndex, commandIndex, "이동 목적지 맵 참조가 없습니다.");
                                else if (!catalog.Contains(command.map)) Report(owner, pageIndex, commandIndex, "목적지 맵이 분석 대상 에셋 목록에 없습니다: " + command.map.mapId);
                                else if (!command.map.IsInBounds(command.cell) || !command.map.IsWalkable(command.cell))
                                    Report(owner, pageIndex, commandIndex, "목적지 좌표가 범위 밖이거나 이동할 수 없습니다: " + command.map.mapId + " " + command.cell);
                                if (conditional) Report(owner, pageIndex, commandIndex, "조건부 연결입니다. 정적 분석으로 실제 접근 가능성을 보장하지 않습니다.", ContentDiagnosticSeverity.Warning);
                            }
                            else if (command.type == EventCommandType.CallCommon)
                                WalkCommon(command.commonEvent, conditional, path + "/Common:" + (command.commonEvent?.displayName ?? "?"), depth + 1);
                            else if (command.type == EventCommandType.RequestForeground)
                                WalkEvent(command.foregroundEvent, true, path + "/Foreground:" + (command.foregroundEvent?.displayName ?? "?"), depth + 1);
                        }
                    }
                }
            }
            foreach (var connection in result.connections)
            {
                if (connection.Destination == null || connection.Source == connection.Destination) continue;
                bool returns = result.connections.Any(x => x.Source == connection.Destination && x.Destination == connection.Source);
                bool intentional = policies.Any(x => x.link.source == connection.Source && x.link.destination == connection.Destination && x.link.allowOneWay);
                if (!returns && !intentional) Report(connection.Owner, connection.PageIndex, connection.CommandIndex,
                    "돌아오는 연결이 없습니다: " + connection.Source.mapId + " → " + connection.Destination.mapId + ". 의도한 단방향이면 레지스트리에 표시하세요.", ContentDiagnosticSeverity.Warning);
            }
            foreach (var policy in policies)
            {
                var link = policy.link;
                if (link.source == null || link.destination == null || !catalog.Contains(link.source) || !catalog.Contains(link.destination))
                    Report(policy.registry, -1, -1, "연결 정책의 출발/목적지 맵이 없거나 분석 대상 에셋 목록에 없습니다.");
                else if (link.requireConnection && !result.connections.Any(x => x.Source == link.source && x.Destination == link.destination))
                    Report(policy.registry, -1, -1, "선언한 필수 연결이 없습니다: " + link.source.mapId + " → " + link.destination.mapId);
            }
            return result;

            void Report(ScriptableObject owner, int pageIndex, int commandIndex, string message, ContentDiagnosticSeverity severity = ContentDiagnosticSeverity.Error)
            {
                result.diagnostics.Add(new ContentDiagnostic { Severity = severity, Message = message,
                    Reference = new ContentReference { Owner = owner, PageIndex = pageIndex, CommandIndex = commandIndex,
                        Category = ContentReferenceCategory.Map, Message = message, Key = owner is MapDefinition map ? map.mapId : "" } });
            }
        }
    }
}
