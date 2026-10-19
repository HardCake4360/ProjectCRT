using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    [CustomEditor(typeof(EventDefinition))]
    public sealed class EventDefinitionEditor : UnityEditor.Editor
    {
        void OnEnable()
        {
            // Capture legacy page indices before the first Inspector edit or reorder.
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EventIdentityUpgrader.UpgradeDefinition(target as EventDefinition);
        }

        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Event Editor에서 열기")) EventEditorWindow.Open((EventDefinition)target);
            bool readOnly = EditorApplication.isPlayingOrWillChangePlaymode;
            if (readOnly) EditorGUILayout.HelpBox("Play 중에는 제작 데이터를 편집할 수 없습니다.", MessageType.Info);
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(readOnly))
            {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"), new GUIContent("표시 이름"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("prefab"), new GUIContent("모델 프리팹"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fallbackColor"), new GUIContent("기본 모델 색상"));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("definitionId"), new GUIContent("저장 이벤트 ID"));
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("조건을 모두 만족하는 페이지 중 목록의 가장 아래 페이지가 실행됩니다. " +
                "Action은 상호작용, PlayerTouch는 접촉, Autorun은 자동 실행, Parallel은 배경 실행입니다. " +
                "Run Once 완료와 체크포인트는 목록 순서와 별개인 ID에 저장됩니다.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("pages"), new GUIContent("이벤트 페이지"), true);
            }
            bool edited = !readOnly && serializedObject.ApplyModifiedProperties();

            var definition = (EventDefinition)target;
            if (edited)
            {
                EventIdentityUpgrader.RepairCopiedEntries(definition);
                EventIdentityUpgrader.UpgradeDefinition(definition);
                serializedObject.Update();
            }
            foreach (string error in definition.ValidateIdentity()) EditorGUILayout.HelpBox(error, MessageType.Error);
            if (definition.pages == null || definition.pages.Count == 0)
                EditorGUILayout.HelpBox("페이지를 추가하고 Commands에 대사나 상태 변경 명령을 넣으세요.", MessageType.Info);
            else
                for (int index = 0; index < definition.pages.Count; index++)
                {
                    foreach (string error in EventIdentityUpgrader.ValidateDefinitionPage(definition, index))
                        EditorGUILayout.HelpBox($"페이지 {index + 1}: {error}", MessageType.Error);
                }
        }
    }

    [CustomPropertyDrawer(typeof(EventPage))]
    public sealed class EventPageDrawer : PropertyDrawer
    {
        const float Gap = 3;
        static readonly string[] Fields = { "pageId", "legacyPageIndex", "name", "conditions", "trigger", "runOnce",
            "parallelRepeat", "parallelIntervalSeconds", "visible", "blocksMovement", "commands" };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;
            foreach (string field in VisibleFields(property))
                height += Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string name = property.FindPropertyRelative("name")?.stringValue;
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label.text + " · " + name, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                foreach (string field in VisibleFields(property))
                {
                    var child = property.FindPropertyRelative(field);
                    line.y += line.height + Gap; line.height = EditorGUI.GetPropertyHeight(child, true);
                    using (new EditorGUI.DisabledScope(field == "pageId" || field == "legacyPageIndex"))
                        EditorGUI.PropertyField(line, child, new GUIContent(Label(field)), true);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        static IEnumerable<string> VisibleFields(SerializedProperty property)
        {
            var trigger = property.FindPropertyRelative("trigger");
            bool parallel = trigger != null && trigger.enumValueIndex >= 0 && trigger.enumValueIndex < trigger.enumNames.Length
                && trigger.enumNames[trigger.enumValueIndex] == "Parallel";
            foreach (string field in Fields)
                if (property.FindPropertyRelative(field) != null &&
                    (parallel || (field != "parallelRepeat" && field != "parallelIntervalSeconds"))) yield return field;
        }
        static string Label(string field) => field switch
        {
            "pageId" => "저장 페이지 ID", "legacyPageIndex" => "구버전 페이지 번호", "name" => "페이지 이름",
            "conditions" => "실행 조건", "trigger" => "실행 방식", "runOnce" => "한 번만 완료",
            "parallelRepeat" => "조건 유지 중 반복", "parallelIntervalSeconds" => "반복 간격 (실제 초)",
            "visible" => "모델 표시", "blocksMovement" => "이동 차단", "commands" => "명령", _ => field
        };
    }

    [CustomPropertyDrawer(typeof(EventCommand))]
    public sealed class EventCommandDrawer : PropertyDrawer
    {
        const float Gap = 3f;
        const float HintHeight = 34f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;
            height += Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("type"));
            height += Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("commandId"));
            foreach (string field in Fields(Type(property), property))
                height += Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true);
            if (Type(property) == EventCommandType.IfCondition || Type(property) == EventCommandType.ShowChoices)
                height += Gap + HintHeight;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            var type = property.FindPropertyRelative("type");
            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            string commandName = type.enumValueIndex >= 0 && type.enumValueIndex < type.enumDisplayNames.Length
                ? type.enumDisplayNames[type.enumValueIndex] : "Unknown";
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded,
                new GUIContent(label.text + " · " + commandName), true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                line.y += line.height + Gap;
                line.height = EditorGUI.GetPropertyHeight(type);
                EditorGUI.PropertyField(line, type, new GUIContent("명령"));
                var identity = property.FindPropertyRelative("commandId");
                line.y += line.height + Gap; line.height = EditorGUI.GetPropertyHeight(identity);
                using (new EditorGUI.DisabledScope(true)) EditorGUI.PropertyField(line, identity, new GUIContent("저장 명령 ID"));
                EventCommandType commandType = Type(property);
                foreach (string field in Fields(commandType, property))
                {
                    var child = property.FindPropertyRelative(field);
                    line.y += line.height + Gap;
                    line.height = EditorGUI.GetPropertyHeight(child, true);
                    EditorGUI.PropertyField(line, child, new GUIContent(FieldLabel(field, commandType)), true);
                }
                if (commandType == EventCommandType.IfCondition || commandType == EventCommandType.ShowChoices)
                {
                    line.y += line.height + Gap;
                    line.height = HintHeight;
                    EditorGUI.HelpBox(line, commandType == EventCommandType.IfCondition
                        ? "조건이 거짓이면 지정한 Label로 이동합니다. 참이면 다음 명령을 실행합니다."
                        : "선택지의 Target Label이 비어 있으면 다음 명령을 실행합니다.", MessageType.Info);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }

        static EventCommandType Type(SerializedProperty property) =>
            (EventCommandType)property.FindPropertyRelative("type").intValue;

        static IEnumerable<string> Fields(EventCommandType type, SerializedProperty property)
        {
            switch (type)
            {
                case EventCommandType.ShowDialogue: return new[] { "speaker", "text" };
                case EventCommandType.ShowChoices: return new[] { "text", "choices" };
                case EventCommandType.SetSwitch:
                case EventCommandType.SetSelfSwitch: return new[] { "key", "boolValue", "boolParameter" };
                case EventCommandType.SetVariable:
                case EventCommandType.AddVariable:
                case EventCommandType.AddItem: return new[] { "key", "intValue", "intParameter" };
                case EventCommandType.AddGold: return new[] { "intValue", "intParameter" };
                case EventCommandType.Wait: return new[] { "seconds" };
                case EventCommandType.TransferPlayer: return new[] { "map", "cell" };
                case EventCommandType.IfCondition: return property.FindPropertyRelative("useLocalCondition").boolValue
                    ? new[] { "useLocalCondition", "localCondition", "targetLabel" } : new[] { "useLocalCondition", "condition", "targetLabel" };
                case EventCommandType.Jump: return new[] { "targetLabel" };
                case EventCommandType.Label: return new[] { "label" };
                case EventCommandType.AdvanceTime: return new[] { "intValue" };
                case EventCommandType.SetObjective: return new[] { "key", "text", "intValue" };
                case EventCommandType.RecordClue: return new[] { "key", "speaker", "text" };
                case EventCommandType.JoinCompanion:
                case EventCommandType.LeaveCompanion:
                case EventCommandType.UnlockAction: return new[] { "key" };
                case EventCommandType.UseItem: return new[] { "item" };
                case EventCommandType.StayAwake: return new[] { "nightActivity" };
                case EventCommandType.StartEncounter: return new[] { "enemy", "key" };
                case EventCommandType.CallCommon: return new[] { "commonEvent", "arguments" };
                case EventCommandType.RequestForeground: return new[] { "foregroundEvent" };
                case EventCommandType.GrantOnce: return new[] { "mutations" };
                case EventCommandType.WaitGameMinutes: return new[] { "intValue", "intParameter" };
                case EventCommandType.Checkpoint: return Array.Empty<string>();
                case EventCommandType.MoveNpc: return new[] { "key", "cell", "boolValue", "boolParameter" };
                case EventCommandType.FaceNpc: return new[] { "key", "intValue", "intParameter", "boolValue", "boolParameter" };
                case EventCommandType.StopNpc: return new[] { "key", "boolValue", "boolParameter" };
                case EventCommandType.ResumeNpc: return new[] { "key" };
                default: return Array.Empty<string>();
            }
        }

        static string FieldLabel(string field, EventCommandType type)
        {
            switch (field)
            {
                case "speaker": return type == EventCommandType.RecordClue ? "단서 제목" : "화자";
                case "text": return type == EventCommandType.ShowChoices ? "선택 질문" : type == EventCommandType.SetObjective ? "목표 제목" : type == EventCommandType.RecordClue ? "단서 내용" : "대사";
                case "choices": return "선택지";
                case "key": return type == EventCommandType.MoveNpc || type == EventCommandType.FaceNpc || type == EventCommandType.StopNpc || type == EventCommandType.ResumeNpc ? "NPC 배치 ID (비우면 호출자)" : "상태 키";
                case "boolValue": return type == EventCommandType.MoveNpc || type == EventCommandType.FaceNpc || type == EventCommandType.StopNpc ? "영구 위치/방향 유지" : "설정 값";
                case "intValue": return type == EventCommandType.FaceNpc ? "방향 (도)" : type == EventCommandType.AdvanceTime ? "진행 시간 (분)" : type == EventCommandType.SetObjective ? "여정 단계" : type == EventCommandType.AddVariable || type == EventCommandType.AddGold ||
                    type == EventCommandType.AddItem ? "증감량" : type == EventCommandType.WaitGameMinutes ? "게임 시간 대기 (분)" : "설정 값";
                case "seconds": return "대기 시간 (초)";
                case "map": return "목적지 맵";
                case "cell": return type == EventCommandType.MoveNpc ? "NPC 목적지 타일" : "목적지 타일";
                case "condition": return "조건";
                case "targetLabel": return type == EventCommandType.IfCondition ? "거짓일 때 이동 Label" : "이동 Label";
                case "label": return "Label 이름";
                case "item": return "아이템 효과";
                case "nightActivity": return "밤새기 활동";
                case "enemy": return "접촉할 적";
                case "commonEvent": return "호출할 공통 이벤트";
                case "arguments": return "호출 인자";
                case "foregroundEvent": return "전경에 요청할 이벤트";
                case "mutations": return "한 번에 지급할 상태 변경";
                case "intParameter": return "정수 인자 사용 (비우면 위 값)";
                case "boolParameter": return "참/거짓 인자 사용 (비우면 위 값)";
                case "useLocalCondition": return "지역 매개변수 조건";
                case "localCondition": return "지역 조건";
                default: return field;
            }
        }
    }
}
