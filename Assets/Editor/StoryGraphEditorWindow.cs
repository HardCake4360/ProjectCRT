using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class StoryGraphEditorWindow : EditorWindow
{
    [MenuItem("Tools/Story Branch Editor")]
    public static void Open()
    {
        var wnd = GetWindow<StoryGraphEditorWindow>();
        wnd.titleContent = new GUIContent("Story Graph Editor");
    }

    public void CreateGUI()
    {
        var root = rootVisualElement;

        // GraphView 생성은 스킨 로드 후 한 프레임 뒤로 미룸
        EditorApplication.delayCall += () =>
        {
            if (this == null) return; // 닫힌 경우 방어

            var graphView = new StoryGraphView(this);
            graphView.StretchToParentSize();
            root.Add(graphView);
        };
    }
}
