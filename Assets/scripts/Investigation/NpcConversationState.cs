using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NpcConversationState
{
    public string npcId;
    public bool hasIntroduced;
    public int conversationCount;
    public List<string> knownTopicsUnlocked = new();
    public BioSignalPayload lastKnownSignal = BioSignalPayload.Default();
    public string lastInteractionTime;
    public List<InvestigationStatementRecord> cachedRecentStatements = new();
    public List<InvestigationExchange> recentExchanges = new();

    public InvestigationNpcLocalStatePayload ToPayload()
    {
        return new InvestigationNpcLocalStatePayload
        {
            hasIntroduced = hasIntroduced,
            conversationCount = conversationCount,
            knownTopicsUnlocked = new List<string>(knownTopicsUnlocked),
            lastKnownSignal = lastKnownSignal ?? BioSignalPayload.Default(),
            lastInteractionTime = lastInteractionTime,
            cachedRecentStatements = new List<InvestigationStatementRecord>(cachedRecentStatements)
        };
    }

    public InvestigationConversationContextPayload ToConversationContext(int maxExchanges = 8)
    {
        int startIndex = Mathf.Max(0, recentExchanges.Count - maxExchanges);
        var payload = new InvestigationConversationContextPayload();

        for (int i = startIndex; i < recentExchanges.Count; i++)
        {
            payload.recentExchanges.Add(recentExchanges[i]);
        }

        return payload;
    }

    public void RegisterExchange(string speaker, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        recentExchanges.Add(new InvestigationExchange
        {
            speaker = speaker,
            text = text.Trim()
        });

        const int maxHistory = 20;
        if (recentExchanges.Count > maxHistory)
        {
            recentExchanges.RemoveAt(0);
        }
    }

    public void ApplyResponse(NpcInvestigationResponse response)
    {
        if (response == null)
        {
            return;
        }

        lastKnownSignal = response.signal ?? BioSignalPayload.Default();
        lastInteractionTime = DateTime.UtcNow.ToString("o");

        if (response.stateDelta == null)
        {
            return;
        }

        if (response.stateDelta.unlockTopicIds != null)
        {
            foreach (string topicId in response.stateDelta.unlockTopicIds)
            {
                if (!string.IsNullOrWhiteSpace(topicId) && !knownTopicsUnlocked.Contains(topicId))
                {
                    knownTopicsUnlocked.Add(topicId);
                }
            }
        }

        if (response.stateDelta.markStatements != null)
        {
            foreach (var statement in response.stateDelta.markStatements)
            {
                if (statement == null || string.IsNullOrWhiteSpace(statement.statementId))
                {
                    continue;
                }

                bool exists = false;
                foreach (var cached in cachedRecentStatements)
                {
                    if (cached.statementId == statement.statementId)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    cachedRecentStatements.Add(statement);
                }
            }
        }

        const int maxStatements = 12;
        while (cachedRecentStatements.Count > maxStatements)
        {
            cachedRecentStatements.RemoveAt(0);
        }
    }
}
