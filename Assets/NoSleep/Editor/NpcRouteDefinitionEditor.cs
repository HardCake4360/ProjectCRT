using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    [CustomEditor(typeof(NpcRouteDefinition))]
    public sealed class NpcRouteDefinitionEditor : UnityEditor.Editor
    {
        MapDefinition previewMap;
        void OnEnable() => NpcAuthoringIdentity.EnsureIds(target as ScriptableObject);
        public override void OnInspectorGUI()
        {
            var route = (NpcRouteDefinition)target;
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("routeId"));
            bool locked = EditorApplication.isPlayingOrWillChangePlaymode;
            EditorGUILayout.HelpBox("경유지 좌표·방향·실시간 대기 초를 순서대로 편집합니다. 경로는 연결한 맵마다 범위와 이동 가능 여부를 검사합니다. 저장은 숫자 진행 번호를 사용합니다. 배포 뒤 같은 routeId의 경유지 순서를 바꾸거나 삭제하면 기존 저장 호환성을 보장하지 않습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(locked))
            {
                DrawPropertiesExcluding(serializedObject, "m_Script", "routeId");
                if (serializedObject.ApplyModifiedProperties()) NpcAuthoringIdentity.EnsureIds(route);
                NpcAuthoringIdentity.DrawCopyRepair(route);
            }
            foreach (string error in route.Validate()) EditorGUILayout.HelpBox(error, MessageType.Error);
            previewMap = (MapDefinition)EditorGUILayout.ObjectField("좌표 확인 맵", previewMap, typeof(MapDefinition), false);
            if (previewMap != null && route.points != null)
                for (int index = 0; index < route.points.Count; index++)
                {
                    var point = route.points[index]; if (point == null) continue;
                    bool walkable = previewMap.IsInBounds(point.cell) && previewMap.IsWalkable(point.cell);
                    if (!walkable) EditorGUILayout.HelpBox("경유지 " + (index + 1) + "이 범위 밖이거나 이동 불가입니다: " + point.cell, MessageType.Error);
                    if (GUILayout.Button("경유지 " + (index + 1) + " · " + point.cell)) MapEditorWindow.Open(previewMap, point.cell);
                }
            foreach (var diagnostic in NpcContentValidator.CollectDiagnostics(ContentReferenceIndex.LoadAssets()).Where(row => row.Owner == route).Take(64))
                EditorGUILayout.HelpBox(diagnostic.Message, diagnostic.Severity == ContentDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
        }
    }
}
