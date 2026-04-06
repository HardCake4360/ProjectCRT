using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InterrogationActionWeightConfig
{
    public string actionType = "Talk";
    public float tellDelta;
    public float interestDelta;
    public float attitudeDelta;
    public int patienceCost = 4;
}

[Serializable]
public class InterrogationKeywordRuleConfig
{
    public string pattern;
    public float tellDelta;
    public float interestDelta;
    public float attitudeDelta;
    public int patienceCost;
}

[Serializable]
public class InterrogationTopicRuleConfig
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
public class InterrogationEvidenceRuleConfig
{
    public string evidenceId;
    public float discoveredTellDelta;
    public float discoveredInterestDelta;
    public float discoveredAttitudeDelta;
    public float undiscoveredTellDelta;
    public float undiscoveredInterestDelta;
    public float undiscoveredAttitudeDelta;
}

public class NpcProfileComponent : MonoBehaviour
{
    [Header("Base State")]
    [Range(-1f, 1f)]
    [SerializeField] private float baseInterest;
    [Range(-1f, 1f)]
    [SerializeField] private float baseAttitude;
    [Range(0, 100)]
    [SerializeField] private int basePatience = 100;

    [Header("Action Weights")]
    [SerializeField] private List<InterrogationActionWeightConfig> actionWeights = new()
    {
        new InterrogationActionWeightConfig
        {
            actionType = "Talk",
            tellDelta = 0.05f,
            interestDelta = 0.01f,
            attitudeDelta = 0f,
            patienceCost = 4
        },
        new InterrogationActionWeightConfig
        {
            actionType = "AskTopic",
            tellDelta = 0.20f,
            interestDelta = 0.04f,
            attitudeDelta = 0f,
            patienceCost = 6
        },
        new InterrogationActionWeightConfig
        {
            actionType = "PresentEvidence",
            tellDelta = 0.42f,
            interestDelta = 0.06f,
            attitudeDelta = -0.08f,
            patienceCost = 10
        }
    };

    [Header("Keyword Rules")]
    [SerializeField] private List<InterrogationKeywordRuleConfig> pressureKeywordRules = new();
    [SerializeField] private List<InterrogationKeywordRuleConfig> sensitiveKeywordRules = new();

    [Header("Topic Rules")]
    [SerializeField] private List<InterrogationTopicRuleConfig> topicRules = new();

    [Header("Evidence Rules")]
    [SerializeField] private List<InterrogationEvidenceRuleConfig> evidenceRules = new();

    public NpcInterrogationProfilePayload ToPayload()
    {
        return new NpcInterrogationProfilePayload
        {
            baseInterest = baseInterest,
            baseAttitude = baseAttitude,
            basePatience = basePatience,
            actionWeights = ConvertActionWeights(actionWeights),
            pressureKeywordRules = ConvertKeywordRules(pressureKeywordRules),
            sensitiveKeywordRules = ConvertKeywordRules(sensitiveKeywordRules),
            topicRules = ConvertTopicRules(topicRules),
            evidenceRules = ConvertEvidenceRules(evidenceRules)
        };
    }

    private static List<InterrogationActionWeightPayload> ConvertActionWeights(List<InterrogationActionWeightConfig> source)
    {
        var payload = new List<InterrogationActionWeightPayload>();
        if (source == null)
        {
            return payload;
        }

        foreach (InterrogationActionWeightConfig item in source)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.actionType))
            {
                continue;
            }

            payload.Add(new InterrogationActionWeightPayload
            {
                actionType = item.actionType,
                tellDelta = item.tellDelta,
                interestDelta = item.interestDelta,
                attitudeDelta = item.attitudeDelta,
                patienceCost = item.patienceCost
            });
        }

        return payload;
    }

    private static List<InterrogationKeywordRulePayload> ConvertKeywordRules(List<InterrogationKeywordRuleConfig> source)
    {
        var payload = new List<InterrogationKeywordRulePayload>();
        if (source == null)
        {
            return payload;
        }

        foreach (InterrogationKeywordRuleConfig item in source)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.pattern))
            {
                continue;
            }

            payload.Add(new InterrogationKeywordRulePayload
            {
                pattern = item.pattern,
                tellDelta = item.tellDelta,
                interestDelta = item.interestDelta,
                attitudeDelta = item.attitudeDelta,
                patienceCost = item.patienceCost
            });
        }

        return payload;
    }

    private static List<InterrogationTopicRulePayload> ConvertTopicRules(List<InterrogationTopicRuleConfig> source)
    {
        var payload = new List<InterrogationTopicRulePayload>();
        if (source == null)
        {
            return payload;
        }

        foreach (InterrogationTopicRuleConfig item in source)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.topicId))
            {
                continue;
            }

            payload.Add(new InterrogationTopicRulePayload
            {
                topicId = item.topicId,
                knownTellDelta = item.knownTellDelta,
                knownInterestDelta = item.knownInterestDelta,
                knownAttitudeDelta = item.knownAttitudeDelta,
                unknownTellDelta = item.unknownTellDelta,
                unknownInterestDelta = item.unknownInterestDelta,
                unknownAttitudeDelta = item.unknownAttitudeDelta
            });
        }

        return payload;
    }

    private static List<InterrogationEvidenceRulePayload> ConvertEvidenceRules(List<InterrogationEvidenceRuleConfig> source)
    {
        var payload = new List<InterrogationEvidenceRulePayload>();
        if (source == null)
        {
            return payload;
        }

        foreach (InterrogationEvidenceRuleConfig item in source)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.evidenceId))
            {
                continue;
            }

            payload.Add(new InterrogationEvidenceRulePayload
            {
                evidenceId = item.evidenceId,
                discoveredTellDelta = item.discoveredTellDelta,
                discoveredInterestDelta = item.discoveredInterestDelta,
                discoveredAttitudeDelta = item.discoveredAttitudeDelta,
                undiscoveredTellDelta = item.undiscoveredTellDelta,
                undiscoveredInterestDelta = item.undiscoveredInterestDelta,
                undiscoveredAttitudeDelta = item.undiscoveredAttitudeDelta
            });
        }

        return payload;
    }
}
