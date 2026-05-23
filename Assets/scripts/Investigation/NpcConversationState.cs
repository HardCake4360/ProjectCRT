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
    public InterrogationAffectPayload lastKnownAffect = InterrogationAffectPayload.Default();
    public float lastKnownTell;
    public int lastKnownPatience = 100;
    public string lastInteractionTime;
    public List<InvestigationStatementRecord> cachedRecentStatements = new();
    public List<InvestigationExchange> recentExchanges = new();

    public NpcConversationSaveEntry ToSaveEntry(string stateKey, int exchangeLimit, int statementLimit)
    {
        TrimToLimits(exchangeLimit, statementLimit);

        return new NpcConversationSaveEntry
        {
            stateKey = stateKey,
            npcId = npcId,
            hasIntroduced = hasIntroduced,
            conversationCount = conversationCount,
            knownTopicsUnlocked = new List<string>(knownTopicsUnlocked),
            lastKnownAffect = CloneAffect(lastKnownAffect),
            lastKnownTell = lastKnownTell,
            lastKnownPatience = lastKnownPatience,
            lastInteractionTime = lastInteractionTime,
            cachedRecentStatements = CloneStatements(cachedRecentStatements),
            recentExchanges = CloneExchanges(recentExchanges)
        };
    }

    public static NpcConversationState FromSaveEntry(NpcConversationSaveEntry entry, int exchangeLimit, int statementLimit)
    {
        if (entry == null)
        {
            return new NpcConversationState();
        }

        var state = new NpcConversationState
        {
            npcId = entry.npcId,
            hasIntroduced = entry.hasIntroduced,
            conversationCount = entry.conversationCount,
            knownTopicsUnlocked = entry.knownTopicsUnlocked != null ? new List<string>(entry.knownTopicsUnlocked) : new List<string>(),
            lastKnownAffect = CloneAffect(entry.lastKnownAffect),
            lastKnownTell = entry.lastKnownTell,
            lastKnownPatience = entry.lastKnownPatience,
            lastInteractionTime = entry.lastInteractionTime,
            cachedRecentStatements = CloneStatements(entry.cachedRecentStatements),
            recentExchanges = CloneExchanges(entry.recentExchanges)
        };

        state.TrimToLimits(exchangeLimit, statementLimit);
        return state;
    }

    public InvestigationNpcLocalStatePayload ToPayload()
    {
        return new InvestigationNpcLocalStatePayload
        {
            hasIntroduced = hasIntroduced,
            conversationCount = conversationCount,
            knownTopicsUnlocked = new List<string>(knownTopicsUnlocked),
            lastKnownAffect = CloneAffect(lastKnownAffect),
            lastKnownPatience = lastKnownPatience,
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

    public void RegisterExchange(string speaker, string text, int maxHistory = 20)
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

        TrimExchangeHistory(maxHistory);
    }

    public void ApplyReplyResponse(NpcInvestigationReplyResponse response, int maxStatements = 12)
    {
        if (response == null)
        {
            return;
        }

        if (response.conversationState != null)
        {
            lastKnownAffect = CloneAffect(response.conversationState.affect);
            lastKnownPatience = response.conversationState.patience;
        }

        lastInteractionTime = DateTime.UtcNow.ToString("o");

        if (response.stateDelta == null)
        {
            return;
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

        TrimStatementHistory(maxStatements);
    }

    public void ApplyTellResult(TellResultPayload tellResult)
    {
        if (tellResult == null)
        {
            return;
        }

        lastKnownTell = tellResult.tell;
    }

    public void TrimToLimits(int exchangeLimit, int statementLimit)
    {
        TrimExchangeHistory(exchangeLimit);
        TrimStatementHistory(statementLimit);
    }

    private void TrimExchangeHistory(int maxHistory)
    {
        int limit = Mathf.Max(1, maxHistory);
        while (recentExchanges.Count > limit)
        {
            recentExchanges.RemoveAt(0);
        }
    }

    private void TrimStatementHistory(int maxStatements)
    {
        int limit = Mathf.Max(1, maxStatements);
        while (cachedRecentStatements.Count > limit)
        {
            cachedRecentStatements.RemoveAt(0);
        }
    }

    private static InterrogationAffectPayload CloneAffect(InterrogationAffectPayload affect)
    {
        affect ??= InterrogationAffectPayload.Default();
        return new InterrogationAffectPayload
        {
            interest = affect.interest,
            attitude = affect.attitude
        };
    }

    private static List<InvestigationStatementRecord> CloneStatements(List<InvestigationStatementRecord> source)
    {
        var clone = new List<InvestigationStatementRecord>();
        if (source == null)
        {
            return clone;
        }

        foreach (InvestigationStatementRecord item in source)
        {
            if (item == null)
            {
                continue;
            }

            clone.Add(new InvestigationStatementRecord
            {
                statementId = item.statementId,
                text = item.text
            });
        }

        return clone;
    }

    private static List<InvestigationExchange> CloneExchanges(List<InvestigationExchange> source)
    {
        var clone = new List<InvestigationExchange>();
        if (source == null)
        {
            return clone;
        }

        foreach (InvestigationExchange item in source)
        {
            if (item == null)
            {
                continue;
            }

            clone.Add(new InvestigationExchange
            {
                speaker = item.speaker,
                text = item.text
            });
        }

        return clone;
    }
}
