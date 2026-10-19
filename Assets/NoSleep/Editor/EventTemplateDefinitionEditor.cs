using UnityEditor;
using UnityEngine;

namespace NoSleep.Editor
{
    [CustomEditor(typeof(EventTemplateDefinition))]
    public sealed class EventTemplateDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) DrawDefaultInspector();
            EditorGUILayout.HelpBox("이 에셋은 재사용할 제작 조합입니다. Event Editor에서 선택하면 새 ID로 복사해서 추가합니다. 기존 실행 상태를 공유하지 않습니다.", MessageType.Info);
            foreach (string error in ((EventTemplateDefinition)target).GetDiagnostics()) EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
    }
}
