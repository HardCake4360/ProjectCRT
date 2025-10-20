// Assets/Editor/StoryGraphEditorWindow.cs
using UnityEditor;
using UnityEngine;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class StoryGraphEditorWindow : EditorWindow
{
    // 유니티 메뉴에 "Tools/Story Branch Editor"를 추가한다냥.
    [MenuItem("Tools/Story Branch Editor")]
    public static void Open()
    {
        // 윈도우를 열고 제목을 설정한다냥.
        StoryGraphEditorWindow wnd = GetWindow<StoryGraphEditorWindow>();
        wnd.titleContent = new GUIContent("Story Graph Editor");
    }

    // 윈도우가 열릴 때 호출되는 메서드이다냥.
    public void CreateGUI()
    {
        // 1. UIElements의 루트 요소를 가져온다냥.
        VisualElement root = rootVisualElement;

        // 2. StoryGraphView 인스턴스를 생성한다냥. (핵심 작업 공간)
        var graphView = new StoryGraphView(this);

        // 3. GraphView를 루트 요소의 크기에 맞게 채운다냥.
        graphView.StretchToParentSize();

        // 4. GraphView를 윈도우에 추가한다냥.
        root.Add(graphView);
    }
}