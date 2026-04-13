using System.Collections.Generic;
using UnityEngine;

public class InvestigationContextBuilder : MonoBehaviour
{
    [SerializeField] private string phase = "investigation";
    [SerializeField] private List<string> discoveredEvidenceIds = new();
    [SerializeField] private List<string> discoveredInformationIds = new();
    [SerializeField] private List<string> unlockedTopicIds = new();
    [SerializeField] private List<string> activeHypothesisIds = new();
    [SerializeField] private List<string> interactableObjectIds = new();

    public NpcInvestigationRequest BuildRequest(
        string turnId,
        string sceneId,
        string playerId,
        string npcId,
        string personaKey,
        InvestigationInteractionPayload interaction,
        NpcConversationState conversationState,
        NpcInterrogationProfilePayload interrogationProfile)
    {
        conversationState ??= new NpcConversationState();

        return new NpcInvestigationRequest
        {
            turnId = turnId,
            sceneId = sceneId,
            phase = phase,
            playerId = playerId,
            npcId = npcId,
            personaKey = personaKey,
            interaction = interaction,
            sceneState = new InvestigationSceneStatePayload
            {
                discoveredEvidenceIds = GetDiscoveredEvidenceIds(),
                discoveredInformationIds = GetDiscoveredInformationIds(),
                unlockedTopicIds = BuildMergedTopics(conversationState),
                activeHypothesisIds = new List<string>(activeHypothesisIds),
                interactableObjectIds = new List<string>(interactableObjectIds)
            },
            npcLocalState = conversationState.ToPayload(),
            conversationContext = conversationState.ToConversationContext(),
            interrogationProfile = interrogationProfile
        };
    }

    public void RegisterEvidence(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
        {
            return;
        }

        if (!discoveredEvidenceIds.Contains(evidenceId))
        {
            discoveredEvidenceIds.Add(evidenceId);
        }

        InvestigationInventoryManager.GetOrCreateInstance().UnlockEvidence(evidenceId);
    }

    public void RegisterTopic(string topicId)
    {
        if (string.IsNullOrWhiteSpace(topicId))
        {
            return;
        }

        if (!unlockedTopicIds.Contains(topicId))
        {
            unlockedTopicIds.Add(topicId);
        }

        InvestigationInventoryManager.GetOrCreateInstance().UnlockTopic(topicId);
    }

    public void RegisterInformation(string informationId)
    {
        if (string.IsNullOrWhiteSpace(informationId))
        {
            return;
        }

        if (!discoveredInformationIds.Contains(informationId))
        {
            discoveredInformationIds.Add(informationId);
        }

        InvestigationInventoryManager.GetOrCreateInstance().UnlockInformation(informationId);
    }

    public List<string> GetDiscoveredEvidenceIds()
    {
        var merged = new List<string>(discoveredEvidenceIds);
        AddUniqueRange(merged, InvestigationInventoryManager.GetOrCreateInstance().GetUnlockedIds(InvestigationItemCategory.Evidence));
        return merged;
    }

    public List<string> GetDiscoveredInformationIds()
    {
        var merged = new List<string>(discoveredInformationIds);
        AddUniqueRange(merged, InvestigationInventoryManager.GetOrCreateInstance().GetUnlockedIds(InvestigationItemCategory.Information));
        return merged;
    }

    public List<string> GetUnlockedTopicIds(NpcConversationState conversationState)
    {
        return BuildMergedTopics(conversationState);
    }

    private List<string> BuildMergedTopics(NpcConversationState conversationState)
    {
        var merged = new List<string>(unlockedTopicIds);
        AddUniqueRange(merged, InvestigationInventoryManager.GetOrCreateInstance().GetUnlockedIds(InvestigationItemCategory.Topic));

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

    private void AddUniqueRange(List<string> destination, IEnumerable<string> source)
    {
        if (source == null)
        {
            return;
        }

        foreach (string item in source)
        {
            if (!string.IsNullOrWhiteSpace(item) && !destination.Contains(item))
            {
                destination.Add(item);
            }
        }
    }
}
