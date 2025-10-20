using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

// StoryNode 클래스가 정의되어 있어야 한다냥 (이전에 설명드린 노드 시각화 클래스)
// 예를 들어: public class StoryNode : Node { public EventObject eventObject; /* ... */ }

public class StoryGraphView : GraphView
{
    private EditorWindow editorWindow;
    private Vector2 nodePosition = new Vector2(50, 50);

    // 생성자: GraphView의 기본 설정을 한다냥.
    public StoryGraphView(EditorWindow window)
    {
        editorWindow = window;

        // 줌 인/아웃 및 패닝을 위한 조작기를 추가한다냥.
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        // 그래프 배경 격자 무늬를 추가한다냥.
        GridBackground grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        // 프로젝트에서 EventObject들을 불러와 노드를 생성한다냥.
        LoadAllEventObjectsAndBuildGraph();
    }

    // 이 그래프 뷰에서 생성 가능한 연결(Port)의 타입을 정의한다냥.
    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        // 연결하려는 Port와 방향이 반대이고 타입이 같은 Port들만 필터링하여 반환한다냥.
        var compatiblePorts = new List<Port>();

        ports.ForEach(port =>
        {
            if (startPort != port && startPort.node != port.node && startPort.direction != port.direction)
            {
                compatiblePorts.Add(port);
            }
        });

        return compatiblePorts;
    }

    // 두 Port가 연결될 수 있는지 확인하는 규칙이다냥.
    // GraphView API가 업데이트되어 이 메서드는 더 이상 필수가 아니며, GetCompatiblePorts에서 대부분의 검사를 처리한다냥.
    // 하지만 명시적으로 규칙을 정의할 수 있다냥.
    // public override PortConnection.Rule GetConnectionRule(Port output, Port input)
    // {
    //     if (output.direction == Direction.Output && input.direction == Direction.Input)
    //     {
    //         return PortConnection.Rule.Default;
    //     }
    //     return PortConnection.Rule.Disabled;
    // }

    /// <summary>
    /// 프로젝트의 모든 EventObject를 로드하고 노드 그래프를 구축
    /// </summary>
    private void LoadAllEventObjectsAndBuildGraph()
    {
        // 1. 모든 EventObject 파일을 찾아서 로드한다냥.
        string[] guids = AssetDatabase.FindAssets("t:EventObject");
        var eventObjects = new List<EventObject>();
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            eventObjects.Add(AssetDatabase.LoadAssetAtPath<EventObject>(path));
        }

        // 2. EventObject들을 StoryNode로 변환하고 GraphView에 추가한다냥.
        var nodeMap = new Dictionary<EventObject, StoryNode>();
        foreach (var eventObj in eventObjects)
        {
            var node = CreateStoryNode(eventObj); // 노드 위치는 나중에 파일로 저장/로드해야 한다냥.
            AddElement(node);
            nodeMap[eventObj] = node;
        }

        // 3. NextEvents 연결 정보를 기반으로 Edge를 생성한다냥.
        foreach (var kvp in nodeMap)
        {
            StoryNode sourceNode = kvp.Value;
            EventObject sourceEvent = kvp.Key;

            // StoryNode에 Output Port가 하나만 있다고 가정한다냥.
            Port outputPort = sourceNode.outputContainer.Children().OfType<Port>().FirstOrDefault(p => p.direction == Direction.Output);

            if (outputPort == null) continue; // 출력 포트가 없으면 건너뛴다냥.

            // NextEvents 배열 순회
            if (sourceEvent.NextEvents != null)
            {
                foreach (var nextEvent in sourceEvent.NextEvents)
                {
                    if (nextEvent != null && nodeMap.TryGetValue(nextEvent, out StoryNode targetNode))
                    {
                        // TargetNode에 Input Port가 하나만 있다고 가정한다냥.
                        Port inputPort = targetNode.inputContainer.Children().OfType<Port>().FirstOrDefault(p => p.direction == Direction.Input);

                        if (inputPort != null)
                        {
                            // Edge(연결선)을 생성하고 GraphView에 추가한다냥.
                            Edge edge = outputPort.ConnectTo(inputPort);
                            AddElement(edge);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// EventObject를 인자로 받아 StoryNode를 생성하고 초기 위치를 설정한다냥.
    /// </summary>
    private StoryNode CreateStoryNode(EventObject eventObject)
    {
        var node = new StoryNode(eventObject);

        // TODO: 실제 프로젝트에서는 노드의 위치를 별도로 저장하여 불러와야 한다냥.
        // 여기서는 임시 위치를 지정한다냥.
        node.SetPosition(new Rect(nodePosition.x, nodePosition.y, 200, 150));
        nodePosition.y += 180; // 다음 노드가 겹치지 않도록 위치를 조정한다냥.

        return node;
    }
}