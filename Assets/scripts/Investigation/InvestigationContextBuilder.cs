using System.Collections.Generic;
using UnityEngine;

public class InvestigationContextBuilder : MonoBehaviour
{
    [SerializeField] private string phase = "investigation";
    [SerializeField] private List<string> discoveredEvidenceIds = new();
    [SerializeField] private List<string> unlockedTopicIds = new();
    [SerializeField] private List<string> activeHypothesisIds = new();
    [SerializeField] private List<string> interactableObjectIds = new();

    public NpcInvestigationRequest BuildRequest(
        string sceneId,
        string playerId,
        string npcId,
        string personaKey,
        InvestigationInteractionPayload interaction,
        NpcConversationState conversationState)
    {
        conversationState ??= new NpcConversationState();

        return new NpcInvestigationRequest
        {
            sceneId = sceneId,
            phase = phase,
            playerId = playerId,
            npcId = npcId,
            personaKey = personaKey,
            interaction = interaction,
            sceneState = new InvestigationSceneStatePayload
            {
                discoveredEvidenceIds = new List<string>(discoveredEvidenceIds),
                unlockedTopicIds = BuildMergedTopics(conversationState),
                activeHypothesisIds = new List<string>(activeHypothesisIds),
                interactableObjectIds = new List<string>(interactableObjectIds)
            },
            npcLocalState = conversationState.ToPayload(),
            conversationContext = conversationState.ToConversationContext()
        };
    }

    public void RegisterEvidence(string evidenceId)
    {
        if (!string.IsNullOrWhiteSpace(evidenceId) && !discoveredEvidenceIds.Contains(evidenceId))
        {
            discoveredEvidenceIds.Add(evidenceId);
        }
    }

    public void RegisterTopic(string topicId)
    {
        if (!string.IsNullOrWhiteSpace(topicId) && !unlockedTopicIds.Contains(topicId))
        {
            unlockedTopicIds.Add(topicId);
        }
    }

    private List<string> BuildMergedTopics(NpcConversationState conversationState)
    {
        var merged = new List<string>(unlockedTopicIds);

        if (conversationState.knownTopicsUnlocked == null)
        {
            return merged;
        }

        foreach (string topic in conversationState.knownTopicsUnlocked)
        {
            if (!string.IsNullOrWhiteSpace(topic) && !merged.Contains(topic))
            {
                merged.Add(topic);
            }
        }

        return merged;
    }
}
