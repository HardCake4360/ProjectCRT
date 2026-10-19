using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    [CustomEditor(typeof(CommonEventDefinition))]
    public sealed class CommonEventDefinitionEditor : UnityEditor.Editor
    {
        void OnEnable()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EventIdentityUpgrader.UpgradeCommon(target as CommonEventDefinition);
        }

        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Event Editor에서 열기")) EventEditorWindow.Open((CommonEventDefinition)target);
            bool readOnly = EditorApplication.isPlayingOrWillChangePlaymode;
            if (readOnly) EditorGUILayout.HelpBox("Play 중에는 제작 데이터를 편집할 수 없습니다.", MessageType.Info);
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(readOnly))
            {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("commonId"), new GUIContent("공통 이벤트 ID"));
            }
            var displayName = serializedObject.FindProperty("displayName");
            if (displayName != null) EditorGUILayout.PropertyField(displayName, new GUIContent("표시 이름"));
            EditorGUILayout.HelpBox("호출자의 배치·셀프 스위치 문맥을 유지합니다. 이름과 타입이 일치하는 인자를 전달하고, " +
                "매개변수 이름은 영문·숫자·밑줄로 지정하고 화자·대사에 {param:visitor}처럼 지역 값을 표시합니다. " +
                "첫 글자는 영문 또는 밑줄이어야 합니다. End는 현재 공통 호출을 반환합니다.", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("parameters"), new GUIContent("매개변수와 기본값"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("commands"), new GUIContent("공통 명령"), true);
            }
            var definition = (CommonEventDefinition)target;
            if (!readOnly && serializedObject.ApplyModifiedProperties())
            {
                EventIdentityUpgrader.RepairCopiedCommands(definition);
                EventIdentityUpgrader.UpgradeCommon(definition);
                serializedObject.Update();
            }
            foreach (string error in EventIdentityUpgrader.ValidateCommon(definition))
                EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
}
