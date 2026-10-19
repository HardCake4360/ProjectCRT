using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    public static class NpcAuthoringIdentity
    {
        public static bool EnsureIds(ScriptableObject asset)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || asset == null) return false;
            // Record before issuing IDs, including schedule IDs. Inspection in Play
            // and every content validator must leave the asset unchanged.
            Undo.RecordObject(asset, "Initialize NPC authoring identities");
            bool changed = asset is NpcDefinition npc ? npc.EnsureStableIds() :
                asset is NpcRouteDefinition route && route.EnsureStableIds();
            if (asset is NpcDefinition definition && definition.schedules != null)
            {
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < definition.schedules.Count; index++)
                {
                    var entry = definition.schedules[index]; if (entry == null || ids.Add(entry.scheduleId)) continue;
                    var copy = EventEditOperations.Clone(entry); copy.scheduleId = Guid.NewGuid().ToString("N");
                    definition.schedules[index] = copy; ids.Add(copy.scheduleId); changed = true;
                    Debug.LogWarning("Copied NPC schedule received a new identity; the first entry was preserved.", asset);
                }
            }
            if (changed) EditorUtility.SetDirty(asset);
            return changed;
        }
        public static void RegenerateCopiedAsset(ScriptableObject asset)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("NPC assets cannot be edited in Play mode.");
            Undo.RegisterCompleteObjectUndo(asset, "Regenerate copied NPC identities");
            if (asset is NpcDefinition npc)
            {
                npc.npcId = Guid.NewGuid().ToString("N");
                if (npc.schedules != null) foreach (var entry in npc.schedules) if (entry != null) entry.scheduleId = Guid.NewGuid().ToString("N");
            }
            else if (asset is NpcRouteDefinition route) route.routeId = Guid.NewGuid().ToString("N");
            else throw new ArgumentException("Select an NPC or NPC route asset.");
            EditorUtility.SetDirty(asset);
        }
        public static void DrawCopyRepair(ScriptableObject asset)
        {
            if (GUILayout.Button("복제한 에셋의 ID 새로 발급") &&
                EditorUtility.DisplayDialog("복제본 ID", "복제본만 선택했는지 확인하세요. 새 ID는 원본 저장 연결과 다른 정의가 됩니다. 데이터와 에셋 참조는 유지합니다.", "복제본에 발급", "취소"))
                RegenerateCopiedAsset(asset);
        }
    }

    [CustomPropertyDrawer(typeof(NpcScheduleEntry))]
    public sealed class NpcScheduleEntryDrawer : PropertyDrawer
    {
        static readonly string[] Fields = { "scheduleId", "firstDay", "lastDay", "startMinute", "endMinute", "conditions", "route", "stationCell", "yaw" };
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (property.isExpanded) foreach (string field in Fields)
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + EditorGUIUtility.standardVerticalSpacing;
            return height;
        }
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            if (!property.isExpanded) return;
            using (new EditorGUI.IndentLevelScope())
            foreach (string field in Fields)
            {
                var value = property.FindPropertyRelative(field);
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                row.height = EditorGUI.GetPropertyHeight(value, true);
                using (new EditorGUI.DisabledScope(field == "scheduleId")) EditorGUI.PropertyField(row, value, true);
            }
        }
    }

    [CustomEditor(typeof(NpcDefinition))]
    public sealed class NpcDefinitionEditor : UnityEditor.Editor
    {
        void OnEnable() => NpcAuthoringIdentity.EnsureIds(target as ScriptableObject);
        public override void OnInspectorGUI()
        {
            var npc = (NpcDefinition)target;
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(serializedObject.FindProperty("npcId"));
            bool locked = EditorApplication.isPlayingOrWillChangePlaymode;
            EditorGUILayout.HelpBox("NPC 정의를 맵 배치의 선택적 NPC 프로필에 연결합니다. 이벤트 정의는 대사·격파 페이지를 담당합니다. 일과는 마지막으로 일치하는 항목이 우선합니다.", MessageType.Info);
            EditorGUILayout.HelpBox("Speed는 초당 격자 칸 수입니다. 타일 크기나 맵 확대와 무관하게 같은 칸 이동 시간이 적용됩니다.", MessageType.Info);
            if (locked) EditorGUILayout.HelpBox("Play 중에는 제작 데이터를 편집하지 않습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(locked))
            {
                DrawPropertiesExcluding(serializedObject, "m_Script", "npcId");
                if (serializedObject.ApplyModifiedProperties()) NpcAuthoringIdentity.EnsureIds(npc);
                NpcAuthoringIdentity.DrawCopyRepair(npc);
            }
            foreach (string error in npc.Validate()) EditorGUILayout.HelpBox(error, MessageType.Error);
            var maps = ContentReferenceIndex.LoadAssets().OfType<MapDefinition>()
                .Where(map => map.events?.Any(placement => placement != null && placement.npc == npc) == true).ToArray();
            EditorGUILayout.LabelField("사용 중인 맵 · " + maps.Length, EditorStyles.boldLabel);
            foreach (var map in maps.Take(64))
            {
                var placement = map.events.First(value => value != null && value.npc == npc);
                if (GUILayout.Button(map.displayName + " · " + placement.id)) MapEditorWindow.Open(map, placement.cell);
            }
            foreach (var diagnostic in NpcContentValidator.CollectDiagnostics(ContentReferenceIndex.LoadAssets())
                .Where(row => row.Owner == npc || maps.Contains(row.Owner as MapDefinition)).Take(64))
                EditorGUILayout.HelpBox(diagnostic.Message, diagnostic.Severity == ContentDiagnosticSeverity.Error ? MessageType.Error : MessageType.Warning);
        }
    }
}
