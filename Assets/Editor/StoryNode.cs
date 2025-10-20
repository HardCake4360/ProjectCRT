// Assets/Editor/StoryNode.cs
using UnityEditor.Experimental.GraphView;
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
        title = obj.name; // 노드 제목 설정이다냥.

        // 노드에 데이터 필드를 추가하는 공간이다냥.
        this.mainContainer.Add(new Label($"Is Main: {obj.IsMainEvent}"));

        // 여기에 obj.conditions의 상태를 표시하는 필드 등을 추가할 수 있다냥.

        // --- 입력 포트 설정 (Input Port) ---
        // 이 노드로 들어오는 연결이다냥.
        var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        inputPort.portName = "Previous Event";
        inputContainer.Add(inputPort); // 노드의 왼쪽 (입력) 컨테이너에 추가한다냥.

        // --- 출력 포트 설정 (Output Port) ---
        // 이 노드에서 다음 이벤트로 나가는 연결이다냥.
        var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        outputPort.portName = "Next Events";
        outputContainer.Add(outputPort); // 노드의 오른쪽 (출력) 컨테이너에 추가한다냥.

        // 노드 크기를 콘텐츠에 맞춘다냥.
        RefreshExpandedState();
        RefreshPorts();
    }
}