// Assets/Editor/StoryNode.cs
using UnityEditor.Experimental.GraphView;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// GraphView API를 사용하기 위해 UnityEditor.Experimental.GraphView를 사용한다냥.
public class StoryNode : Node
{
    // 이 노드가 참조하는 실제 스토리 데이터이다냥.
    public EventObject eventObject;

    public StoryNode(EventObject obj)
    {
        eventObject = obj;
        title = obj.name;

        this.mainContainer.Add(new Label($"Is Main: {obj.IsMainEvent}"));

        // --- 포트 생성은 한 프레임 뒤로 미루기 ---
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;

            var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            inputPort.portName = "Previous Event";
            inputContainer.Add(inputPort);

            var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
            outputPort.portName = "Next Events";
            outputContainer.Add(outputPort);

            RefreshExpandedState();
            RefreshPorts();
        };
    }

}