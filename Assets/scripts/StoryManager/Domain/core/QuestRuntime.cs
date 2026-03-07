using System;
using System.Collections.Generic;

public class QuestRuntime
{
    private readonly Dictionary<string, QuestNode> nodes;
    private readonly StoryState state;

    public string CurrentNodeId { get; private set; }

    public QuestRuntime(
        Dictionary<string, QuestNode> nodes,
        StoryState state,
        string startNodeId)
    {
        this.nodes = nodes;
        this.state = state;
        CurrentNodeId = startNodeId;

        state.OnChanged += Evaluate;
    }

    private void Evaluate()
    {
        var node = nodes[CurrentNodeId];

        if (node.Condition == null || node.Condition.Evaluate(state))
        {
            foreach (var effect in node.Effects)
                effect.Apply(state);

            if (node.NextNodeId != null)
                CurrentNodeId = node.NextNodeId;
        }
    }
}
