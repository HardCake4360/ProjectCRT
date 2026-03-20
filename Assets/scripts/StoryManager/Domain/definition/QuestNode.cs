using System.Collections.Generic;

public class QuestNode
{
    public string Id;
    public ICondition Condition;
    public List<IEffect> Effects = new();
    public string NextNodeId;
}
