using System;
using System.Collections.Generic;

public class QuestDefinition
{
    public string QuestId;
    public string StartNodeId;
    public Dictionary<string, QuestNode> Nodes;
}
