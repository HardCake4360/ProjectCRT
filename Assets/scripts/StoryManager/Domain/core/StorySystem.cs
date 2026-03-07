using System;
using System.Collections.Generic;

public class StorySystem
{
    private readonly StoryState state;
    private readonly Dictionary<string, QuestRuntime> activeQuests = new();

    public StoryState State => state;

    public StorySystem(StoryState state)
    {
        this.state = state;
    }

    public void StartQuest(string questId, QuestDefinition definition)
    {
        if (activeQuests.ContainsKey(questId))
            return;

        var runtime = new QuestRuntime(
            definition.Nodes,
            state,
            definition.StartNodeId
        );

        activeQuests.Add(questId, runtime);
    }

    public bool IsQuestActive(string questId)
        => activeQuests.ContainsKey(questId);

    public QuestRuntime GetQuest(string questId)
        => activeQuests.TryGetValue(questId, out var q) ? q : null;
}
