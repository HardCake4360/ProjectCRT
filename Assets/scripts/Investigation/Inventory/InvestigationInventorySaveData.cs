using System;
using System.Collections.Generic;

[Serializable]
public class InvestigationInventorySaveData
{
    public int version = 1;
    public string caseId = "bar_case_001";
    public List<string> topicIds = new();
    public List<string> evidenceIds = new();
    public List<string> informationIds = new();
}
