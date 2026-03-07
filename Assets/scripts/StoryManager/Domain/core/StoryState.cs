using System;
using System.Collections.Generic;

public class StoryState
{
    private HashSet<string> flags = new();
    private Dictionary<string, int> questStages = new();

    public event Action OnChanged;

    public bool HasFlag(string flag) => flags.Contains(flag);

    public void SetFlag(string flag)
    {
        if (flags.Add(flag))
            OnChanged?.Invoke();
    }

    public int GetQuestStage(string questId)
        => questStages.TryGetValue(questId, out var v) ? v : 0;

    public void SetQuestStage(string questId, int stage)
    {
        questStages[questId] = stage;
        OnChanged?.Invoke();
    }
}

