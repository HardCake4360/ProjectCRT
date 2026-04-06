using System;
using System.Collections.Generic;

[Serializable]
public class InvestigationInteractionPayload
{
    public string actionType;
    public string playerIntentText;
    public string topicId;
    public string evidenceId;
    public string objectId;
    public List<string> statementRefIds = new();
}

[Serializable]
public class InvestigationSceneStatePayload
{
    public List<string> discoveredEvidenceIds = new();
    public List<string> unlockedTopicIds = new();
    public List<string> activeHypothesisIds = new();
    public List<string> interactableObjectIds = new();
}

[Serializable]
public class InterrogationAffectPayload
{
    public float interest;
    public float attitude;

    public static InterrogationAffectPayload Default()
    {
        return new InterrogationAffectPayload
        {
            interest = 0f,
            attitude = 0f
        };
    }
}

[Serializable]
public class ConversationStatePayload
{
    public InterrogationAffectPayload affect = InterrogationAffectPayload.Default();
    public int patience = 100;

    public static ConversationStatePayload Default()
    {
        return new ConversationStatePayload
        {
            affect = InterrogationAffectPayload.Default(),
            patience = 100
        };
    }
}

[Serializable]
public class TellResultPayload
{
    public string turnId;
    public float tell;
    public string band;
    public string primaryAction;
    public string reason;

    public static TellResultPayload Default()
    {
        return new TellResultPayload
        {
            turnId = string.Empty,
            tell = 0f,
            band = "Stable",
            primaryAction = "unknown",
            reason = string.Empty
        };
    }
}

[Serializable]
public class InvestigationNpcLocalStatePayload
{
    public bool hasIntroduced;
    public int conversationCount;
    public List<string> knownTopicsUnlocked = new();
    public InterrogationAffectPayload lastKnownAffect = InterrogationAffectPayload.Default();
    public int lastKnownPatience = 100;
    public string lastInteractionTime;
    public List<InvestigationStatementRecord> cachedRecentStatements = new();
}

[Serializable]
public class InvestigationExchange
{
    public string speaker;
    public string text;
}

[Serializable]
public class InvestigationConversationContextPayload
{
    public List<InvestigationExchange> recentExchanges = new();
}

[Serializable]
public class InterrogationActionWeightPayload
{
    public string actionType;
    public float tellDelta;
    public float interestDelta;
    public float attitudeDelta;
    public int patienceCost;
}

[Serializable]
public class InterrogationKeywordRulePayload
{
    public string pattern;
    public float tellDelta;
    public float interestDelta;
    public float attitudeDelta;
    public int patienceCost;
}

[Serializable]
public class InterrogationTopicRulePayload
{
    public string topicId;
    public float knownTellDelta;
    public float knownInterestDelta;
    public float knownAttitudeDelta;
    public float unknownTellDelta;
    public float unknownInterestDelta;
    public float unknownAttitudeDelta;
}

[Serializable]
public class InterrogationEvidenceRulePayload
{
    public string evidenceId;
    public float discoveredTellDelta;
    public float discoveredInterestDelta;
    public float discoveredAttitudeDelta;
    public float undiscoveredTellDelta;
    public float undiscoveredInterestDelta;
    public float undiscoveredAttitudeDelta;
}

[Serializable]
public class NpcInterrogationProfilePayload
{
    public float baseInterest;
    public float baseAttitude;
    public int basePatience = 100;
    public List<InterrogationActionWeightPayload> actionWeights = new();
    public List<InterrogationKeywordRulePayload> pressureKeywordRules = new();
    public List<InterrogationKeywordRulePayload> sensitiveKeywordRules = new();
    public List<InterrogationTopicRulePayload> topicRules = new();
    public List<InterrogationEvidenceRulePayload> evidenceRules = new();
}

[Serializable]
public class NpcInvestigationRequest
{
    public string turnId;
    public string sceneId;
    public string phase;
    public string playerId;
    public string npcId;
    public string personaKey;
    public InvestigationInteractionPayload interaction;
    public InvestigationSceneStatePayload sceneState;
    public InvestigationNpcLocalStatePayload npcLocalState;
    public InvestigationConversationContextPayload conversationContext;
    public NpcInterrogationProfilePayload interrogationProfile;
}

[Serializable]
public class NpcTellRequest
{
    public string turnId;
    public string playerId;
    public string npcId;
    public string personaKey;
    public string questionText;
}

[Serializable]
public class InvestigationStatementRecord
{
    public string statementId;
    public string text;
}

[Serializable]
public class InvestigationStateDeltaPayload
{
    public List<string> unlockTopicIds = new();
    public List<InvestigationStatementRecord> markStatements = new();
}

[Serializable]
public class InvestigationPresentationHintsPayload
{
    public string animation;
    public string voiceTone;
    public float uiNoiseLevel;
}

[Serializable]
public class NpcInvestigationReplyResponse
{
    public bool ok = true;
    public string turnId;
    public string replyText;
    public ConversationStatePayload conversationState = ConversationStatePayload.Default();
    public InvestigationStateDeltaPayload stateDelta = new();
    public InvestigationPresentationHintsPayload presentationHints = new();
    public string error;
}

[Serializable]
public class NpcTellResponse
{
    public bool ok = true;
    public string turnId;
    public TellResultPayload tellResult = TellResultPayload.Default();
    public string error;
}

[Serializable]
public class NpcInvestigationReplyStreamChunk
{
    public string type;
    public string messageId;
    public string npcDisplayName;
    public string text;
    public string error;
    public ConversationStatePayload conversationState;
    public NpcInvestigationReplyResponse response;
}
