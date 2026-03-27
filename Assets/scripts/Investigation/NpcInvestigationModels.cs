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
public class BioSignalPayload
{
    public float stress;
    public float distortion;
    public float focus;

    public static BioSignalPayload Default()
    {
        return new BioSignalPayload
        {
            stress = 0.1f,
            distortion = 0.05f,
            focus = 0.8f
        };
    }
}

[Serializable]
public class InvestigationNpcLocalStatePayload
{
    public bool hasIntroduced;
    public int conversationCount;
    public List<string> knownTopicsUnlocked = new();
    public BioSignalPayload lastKnownSignal = BioSignalPayload.Default();
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
public class NpcInvestigationRequest
{
    public string sceneId;
    public string phase;
    public string playerId;
    public string npcId;
    public string personaKey;
    public InvestigationInteractionPayload interaction;
    public InvestigationSceneStatePayload sceneState;
    public InvestigationNpcLocalStatePayload npcLocalState;
    public InvestigationConversationContextPayload conversationContext;
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
public class NpcInvestigationResponse
{
    public bool ok = true;
    public string replyText;
    public BioSignalPayload signal = BioSignalPayload.Default();
    public InvestigationStateDeltaPayload stateDelta = new();
    public InvestigationPresentationHintsPayload presentationHints = new();
    public string error;
}
