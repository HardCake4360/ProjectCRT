using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public sealed class MapConnectionsWindow : EditorWindow
    {
        ContentReferenceIndex index;
        MapConnectionAnalysis analysis;
        Vector2 scroll, graphScroll;
        string search = "";
        bool showGraph = true;
        StateKeyRegistry registry;
        MapDefinition policySource, policyDestination;
        bool oneWay, required;
        [MenuItem("No Sleep/Map Connections")]
        public static void Open() => GetWindow<MapConnectionsWindow>("No Sleep Map Links");
        void OnEnable() { Refresh(); Undo.undoRedoPerformed += Refresh; }
        void OnDisable() => Undo.undoRedoPerformed -= Refresh;
        void Refresh()
        {
            index = ContentReferenceIndex.Build();
            analysis = MapConnectionAnalyzer.Collect(index.Maps, index.Registries);
            Repaint();
        }
        void OnGUI()
        {
            if (analysis == null) Refresh();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("새로 고침", EditorStyles.toolbarButton, GUILayout.Width(80))) Refresh();
                search = GUILayout.TextField(search, EditorStyles.toolbarTextField);
            }
            EditorGUILayout.HelpBox("Assets/NoSleep의 모든 맵 에셋을 대상으로 직접·공통 호출·전경 요청의 이동을 수집합니다. 씬 부트스트랩의 실제 맵 목록과 전체 퀘스트 도달 가능성은 이 그래프의 검증 범위가 아닙니다.", MessageType.Info);
            showGraph = EditorGUILayout.Foldout(showGraph, "정적 연결 그래프 · 노드 클릭으로 맵 열기", true);
            if (showGraph) DrawGraph();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawPolicy();
            EditorGUILayout.LabelField("연결 · " + analysis.Connections.Count, EditorStyles.boldLabel);
            int rows = 0;
            foreach (var connection in analysis.Connections)
            {
                if (!Matches(connection.Source?.mapId + " " + connection.Destination?.mapId + " " + connection.Via)) continue;
                if (++rows > 250) { EditorGUILayout.LabelField("최대 250개 연결입니다. 검색을 좁히세요."); break; }
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField((connection.Source?.mapId ?? "?") + " → " + (connection.Destination?.mapId ?? "(누락)") +
                        " " + connection.Cell + (connection.IsConditional ? " · 조건부" : ""), EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(connection.PlacementId + " · " + connection.Via, EditorStyles.wordWrappedMiniLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("명령 위치")) ContentBrowserWindow.OpenReference(new ContentReference {
                            Owner = connection.Owner, PageIndex = connection.PageIndex, CommandIndex = connection.CommandIndex });
                        if (GUILayout.Button("출발 배치") && connection.Source != null) MapEditorWindow.Open(connection.Source, connection.SourceCell);
                        using (new EditorGUI.DisabledScope(connection.Destination == null))
                            if (GUILayout.Button("목적지 칸")) MapEditorWindow.Open(connection.Destination, connection.Cell);
                        using (new EditorGUI.DisabledScope(registry == null || connection.Destination == null || EditorApplication.isPlayingOrWillChangePlaymode))
                            if (GUILayout.Button("단방향 의도 표시")) SetPolicy(connection.Source, connection.Destination, true, false, false);
                    }
                }
            }
            EditorGUILayout.Space(); EditorGUILayout.LabelField("연결 진단", EditorStyles.boldLabel);
            rows = 0;
            foreach (var diagnostic in analysis.Diagnostics)
            {
                if (!Matches(diagnostic.ToString())) continue;
                if (++rows > 250) { EditorGUILayout.LabelField("최대 250행입니다. 검색을 좁히세요."); break; }
                EditorGUILayout.HelpBox(diagnostic.Message, diagnostic.Severity == ContentDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
                ContentBrowserWindow.DrawLocation(diagnostic.Reference);
            }
            EditorGUILayout.EndScrollView();
        }
        bool Matches(string text) => string.IsNullOrEmpty(search) || (text ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        void DrawPolicy()
        {
            EditorGUILayout.LabelField("제작자 연결 정책", EditorStyles.boldLabel);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("Play 중에는 제작 정책을 저장할 수 없습니다.", MessageType.Info);
            registry = (StateKeyRegistry)EditorGUILayout.ObjectField("선언 에셋", registry, typeof(StateKeyRegistry), false);
            if (registry == null)
            {
                EditorGUILayout.HelpBox("Content Browser의 선언 목록에서 에셋을 만들거나 기존 선언 에셋을 선택하세요. 정책은 제작 데이터에만 저장됩니다.", MessageType.Info);
                if (GUILayout.Button("Content Browser 열기")) ContentBrowserWindow.Open();
                return;
            }
            policySource = (MapDefinition)EditorGUILayout.ObjectField("출발", policySource, typeof(MapDefinition), false);
            policyDestination = (MapDefinition)EditorGUILayout.ObjectField("목적지", policyDestination, typeof(MapDefinition), false);
            oneWay = EditorGUILayout.Toggle("의도된 단방향", oneWay);
            required = EditorGUILayout.Toggle("필수 연결", required);
            using (new EditorGUI.DisabledScope(policySource == null || policyDestination == null || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("이 방향의 정책 저장")) SetPolicy(policySource, policyDestination, oneWay, required);
        }
        void SetPolicy(MapDefinition source, MapDefinition destination, bool allowOneWay, bool requireConnection, bool updateRequired = true)
        {
            if (registry == null || source == null || destination == null) return;
            Undo.RecordObject(registry, "Edit map connection policy");
            registry.mapLinks ??= new List<MapLinkPolicy>();
            var policy = registry.mapLinks.FirstOrDefault(x => x != null && x.source == source && x.destination == destination);
            if (policy == null) { policy = new MapLinkPolicy { source = source, destination = destination }; registry.mapLinks.Add(policy); }
            // The row shortcut changes one-way intent without erasing a required policy.
            policy.allowOneWay = allowOneWay;
            if (updateRequired) policy.requireConnection = requireConnection;
            EditorUtility.SetDirty(registry); AssetDatabase.SaveAssets(); Refresh();
        }
        void DrawGraph()
        {
            var maps = index.Maps.Take(60).ToArray();
            if (maps.Length == 0) return;
            if (index.Maps.Count > maps.Length) EditorGUILayout.LabelField("그래프는 최대 60개 맵을 표시합니다. 연결 목록은 검색할 수 있습니다.");
            graphScroll = EditorGUILayout.BeginScrollView(graphScroll, GUILayout.Height(300));
            const float width = 180, height = 56, stepX = 260, stepY = 100;
            Rect canvas = GUILayoutUtility.GetRect(3 * stepX + 30, Mathf.Max(290, ((maps.Length + 2) / 3) * stepY + 30));
            var nodes = new Dictionary<MapDefinition, Rect>();
            for (int i = 0; i < maps.Length; i++)
                nodes.Add(maps[i], new Rect(canvas.x + 20 + (i % 3) * stepX, canvas.y + 15 + (i / 3) * stepY, width, height));
            Handles.BeginGUI();
            foreach (var connection in analysis.Connections.Take(300))
            {
                if (connection.Destination == null || !nodes.TryGetValue(connection.Source, out Rect from) || !nodes.TryGetValue(connection.Destination, out Rect to)) continue;
                Vector3 start = new Vector3(from.xMax, from.center.y), end = new Vector3(to.xMin, to.center.y);
                Color color = connection.IsConditional ? new Color(0.95f, 0.65f, 0.2f) : new Color(0.3f, 0.7f, 0.95f);
                Handles.DrawBezier(start, end, start + Vector3.right * 45, end - Vector3.right * 45, color, null, 2);
                Handles.color = color;
                Handles.DrawAAPolyLine(2, end + new Vector3(9, -5), end, end + new Vector3(9, 5));
            }
            Handles.EndGUI();
            foreach (var pair in nodes)
                if (GUI.Button(pair.Value, pair.Key.displayName + "\n" + pair.Key.mapId)) MapEditorWindow.Open(pair.Key, pair.Key.spawnCell);
            EditorGUILayout.EndScrollView();
        }
    }
}
