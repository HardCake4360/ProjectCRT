using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NpcInvestigationController : MonoBehaviour
{
    private static readonly Dictionary<string, NpcConversationState> RuntimeStates = new();
    private static NpcInvestigationController ActiveController;

    [Header("Identity")]
    [SerializeField] private string npcId = "rebecca";
    [SerializeField] private string npcDisplayName = "리베카";
    [SerializeField] private string sceneId = "bar_murder_case_01";
    [SerializeField] private string playerId = "player";
    [SerializeField] private string personaKey = "rebecca";

    [Header("First Fixed Dialogue")]
    [TextArea(3, 5)]
    [SerializeField] private string firstFixedLine = "어서 와. 오늘은 평소보다 질문이 많겠네. 다들 노이즈 얘기만 하고 있어.";

    [Header("Dependencies")]
    [SerializeField] private InvestigationContextBuilder contextBuilder;
    [SerializeField] private NpcInvestigationClient apiClient;
    [SerializeField] private InvestigationInteractionUI interactionUI;
    [SerializeField] private NpcProfileComponent profileComponent;

    private NpcConversationState conversationState;
    private Interactable ownerInteractable;
    private NPC_script npcAnimation;
    private bool isBusy;
    private string activeTurnId;

    private void Awake()
    {
        if (!RuntimeStates.TryGetValue(npcId, out conversationState))
        {
            conversationState = new NpcConversationState
            {
                npcId = npcId
            };
            RuntimeStates.Add(npcId, conversationState);
        }

        if (contextBuilder == null)
        {
            contextBuilder = FindFirstObjectByType<InvestigationContextBuilder>();
            if (contextBuilder == null)
            {
                contextBuilder = gameObject.AddComponent<InvestigationContextBuilder>();
            }
        }

        if (apiClient == null)
        {
            apiClient = FindFirstObjectByType<NpcInvestigationClient>();
            if (apiClient == null)
            {
                apiClient = gameObject.AddComponent<NpcInvestigationClient>();
            }
        }

        if (interactionUI == null)
        {
            interactionUI = InvestigationInteractionUI.GetOrCreateInstance();
        }

        if (profileComponent == null)
        {
            profileComponent = GetComponent<NpcProfileComponent>();
        }

        if (ownerInteractable == null)
        {
            ownerInteractable = GetComponent<Interactable>();
        }

        if (npcAnimation == null)
        {
            npcAnimation = GetComponent<NPC_script>();
        }

        Debug.Log(
            $"[NpcInvestigationController] Bound dependencies for npcId={npcId}\n" +
            $"controllerObject={name}\n" +
            $"apiClientObject={(apiClient != null ? apiClient.name : "null")}\n" +
            $"uiObject={(interactionUI != null ? interactionUI.name : "null")}\n" +
            $"profileComponent={(profileComponent != null ? profileComponent.name : "null")}",
            this);

        if (profileComponent == null)
        {
            Debug.LogWarning(
                $"[NpcInvestigationController] No NpcProfileComponent found on '{name}'. " +
                "NPC-specific interrogation rules will not be included in the request.",
                this);
        }
    }

    private void OnEnable()
    {
        BindUiEvents();
    }

    private void OnDisable()
    {
        if (interactionUI != null)
        {
            interactionUI.ActionRequested -= HandleActionRequested;
            interactionUI.CloseRequested -= HandleCloseRequested;
        }
    }

    public void BeginInteraction()
    {
        if (interactionUI == null)
        {
            interactionUI = InvestigationInteractionUI.GetOrCreateInstance();
        }

        BindUiEvents();

        if (interactionUI == null)
        {
            Debug.LogWarning("InvestigationInteractionUI is not available.");
            return;
        }

        ActiveController = this;
        ownerInteractable?.EnterCloseup();
        npcAnimation?.SetLookAtCamera(true);
        npcAnimation?.PlayInteractionStart();
        interactionUI.Open(npcDisplayName);
        RefreshInteractionChoices();
        interactionUI.ClearConversation();
        interactionUI.ResetTellValue(conversationState.lastKnownTell);
        float cachedInterest = conversationState.lastKnownAffect != null ? conversationState.lastKnownAffect.interest : 0f;
        float cachedAttitude = conversationState.lastKnownAffect != null ? conversationState.lastKnownAffect.attitude : 0f;
        Debug.Log($"[NpcInvestigationController] Reset affect npc={npcId} interest={cachedInterest:F3} attitude={cachedAttitude:F3}", this);
        interactionUI.ResetAffectValue(cachedInterest, cachedAttitude);
        interactionUI.SetPatienceValue(conversationState.lastKnownPatience);
        interactionUI.AppendSystemMessage("대화형 조사 UI가 열렸습니다.");

        foreach (var exchange in conversationState.recentExchanges)
        {
            if (exchange.speaker == "player")
            {
                interactionUI.AppendPlayerMessage(exchange.text);
            }
            else if (exchange.speaker == "npc")
            {
                interactionUI.AppendNpcMessage(npcDisplayName, exchange.text);
            }
        }

        if (!conversationState.hasIntroduced)
        {
            conversationState.hasIntroduced = true;
            conversationState.conversationCount++;
            conversationState.RegisterExchange("npc", firstFixedLine);
            interactionUI.AppendNpcMessage(npcDisplayName, firstFixedLine);
            interactionUI.SetStatus("첫 인트로 대화가 출력되었습니다. 아래 버튼으로 조사 흐름을 이어가세요.");
        }
        else
        {
            interactionUI.SetStatus("조사 액션을 선택해 리베카의 반응을 확인하세요.");
        }
    }

    private void HandleActionRequested(InvestigationInteractionPayload payload)
    {
        if (ActiveController != this || isBusy || payload == null)
        {
            return;
        }

        StartCoroutine(RunInteraction(payload));
    }

    private IEnumerator RunInteraction(InvestigationInteractionPayload payload)
    {
        isBusy = true;
        interactionUI.SetBusy(true);

        string playerLine = string.IsNullOrWhiteSpace(payload.playerIntentText)
            ? "질문을 던진다."
            : payload.playerIntentText;

        conversationState.conversationCount++;
        conversationState.RegisterExchange("player", playerLine);
        interactionUI.AppendPlayerMessage(playerLine);
        interactionUI.SetStatus("리베카의 반응과 tell 값을 병렬로 분석 중입니다...");
        interactionUI.SetTellPending(true);
        interactionUI.SetAffectPending(true);
        npcAnimation?.PlayThinking();

        string turnId = System.Guid.NewGuid().ToString("N");
        activeTurnId = turnId;

        NpcInvestigationRequest request = contextBuilder.BuildRequest(
            turnId,
            sceneId,
            playerId,
            npcId,
            personaKey,
            payload,
            conversationState,
            profileComponent != null ? profileComponent.ToPayload() : null);

        NpcTellRequest tellRequest = new NpcTellRequest
        {
            turnId = turnId,
            playerId = playerId,
            npcId = npcId,
            personaKey = personaKey,
            questionText = playerLine
        };

        bool replyCompleted = false;
        bool tellCompleted = false;
        bool streamStarted = false;
        bool answerAnimationStarted = false;
        ConversationStatePayload streamedConversationState = null;
        string pendingTellError = null;
        string pendingReplyError = null;

        StartCoroutine(apiClient.SendReplyRequest(
            request,
            () =>
            {
                streamStarted = true;
                interactionUI.BeginNpcStreamingMessage(npcDisplayName);
                interactionUI.SetStatus("리베카가 답하고 있습니다...");
            },
            partialText =>
            {
                if (!answerAnimationStarted && !string.IsNullOrEmpty(partialText))
                {
                    answerAnimationStarted = true;
                    npcAnimation?.PlayAnswerStart();
                }

                interactionUI.UpdateNpcStreamingMessage(npcDisplayName, partialText);
            },
            conversationPayload =>
            {
                streamedConversationState = conversationPayload ?? ConversationStatePayload.Default();
                float streamedInterest = streamedConversationState.affect != null ? streamedConversationState.affect.interest : 0f;
                float streamedAttitude = streamedConversationState.affect != null ? streamedConversationState.affect.attitude : 0f;
                Debug.Log($"[NpcInvestigationController] Stream affect update npc={npcId} interest={streamedInterest:F3} attitude={streamedAttitude:F3} patience={streamedConversationState.patience}", this);
                interactionUI.SetAffectValue(streamedInterest, streamedAttitude);
                interactionUI.SetPatienceValue(streamedConversationState.patience);
            },
            response =>
            {
                if (response == null)
                {
                    npcAnimation?.PlayInteractionIdle();
                    interactionUI.SetAffectPending(false);
                    pendingReplyError = "빈 응답이 반환되었습니다.";
                    replyCompleted = true;
                    return;
                }

                if (response.conversationState == null)
                {
                    response.conversationState = streamedConversationState ?? ConversationStatePayload.Default();
                }

                string reply = string.IsNullOrWhiteSpace(response.replyText)
                    ? "..."
                    : response.replyText.Trim();

                if (!answerAnimationStarted)
                {
                    answerAnimationStarted = true;
                    npcAnimation?.PlayAnswerStart();
                }

                conversationState.RegisterExchange("npc", reply);
                conversationState.ApplyReplyResponse(response);
                float finalInterest = response.conversationState.affect != null ? response.conversationState.affect.interest : 0f;
                float finalAttitude = response.conversationState.affect != null ? response.conversationState.affect.attitude : 0f;
                Debug.Log($"[NpcInvestigationController] Final affect update npc={npcId} interest={finalInterest:F3} attitude={finalAttitude:F3} patience={response.conversationState.patience}", this);
                interactionUI.SetAffectValue(finalInterest, finalAttitude);
                interactionUI.SetPatienceValue(response.conversationState.patience);

                if (streamStarted)
                {
                    interactionUI.CommitNpcStreamingMessage(npcDisplayName, reply);
                }
                else
                {
                    interactionUI.AppendNpcMessage(npcDisplayName, reply);
                }

                if (response.stateDelta != null && response.stateDelta.unlockTopicIds != null && response.stateDelta.unlockTopicIds.Count > 0)
                {
                    foreach (string topicId in response.stateDelta.unlockTopicIds)
                    {
                        contextBuilder.RegisterTopic(topicId);
                    }
                    RefreshInteractionChoices();
                    interactionUI.AppendSystemMessage($"Unlocked topics: {string.Join(", ", response.stateDelta.unlockTopicIds)}");
                }

                replyCompleted = true;
                if (!tellCompleted)
                {
                    interactionUI.SetStatus("답변 도착. tell 값을 마무리 분석 중입니다...");
                }
            },
            error =>
            {
                if (streamStarted)
                {
                    interactionUI.CancelNpcStreamingMessage();
                }

                pendingReplyError = error;
                interactionUI.SetAffectPending(false);
                npcAnimation?.PlayInteractionIdle();
                replyCompleted = true;
            }));

        StartCoroutine(apiClient.SendTellRequest(
            tellRequest,
            tellResult =>
            {
                if (tellResult != null && tellResult.turnId == activeTurnId)
                {
                    conversationState.ApplyTellResult(tellResult);
                    interactionUI.SetTellValue(tellResult.tell);
                }
                else
                {
                    interactionUI.SetTellPending(false);
                }

                tellCompleted = true;
                if (replyCompleted && string.IsNullOrWhiteSpace(pendingReplyError))
                {
                    interactionUI.SetStatus("새 조사 액션을 선택할 수 있습니다.");
                }
            },
            error =>
            {
                interactionUI.SetTellPending(false);
                pendingTellError = error;
                tellCompleted = true;
            }));

        while (!replyCompleted || !tellCompleted)
        {
            yield return null;
        }

        if (!string.IsNullOrWhiteSpace(pendingReplyError))
        {
            interactionUI.AppendSystemMessage($"서버 오류: {pendingReplyError}");
            interactionUI.SetStatus("연결 실패. 서버 상태를 확인하세요.");
        }
        else if (!string.IsNullOrWhiteSpace(pendingTellError))
        {
            interactionUI.AppendSystemMessage($"tell 계산 지연/실패: {pendingTellError}");
            interactionUI.SetStatus("응답은 완료됐지만 tell 값을 가져오지 못했습니다.");
        }
        else
        {
            interactionUI.SetStatus("새 조사 액션을 선택할 수 있습니다.");
        }

        isBusy = false;
        interactionUI.SetBusy(false);
    }

    private void HandleCloseRequested()
    {
        if (ActiveController != this)
        {
            return;
        }

        ActiveController = null;
        if (interactionUI != null)
        {
            interactionUI.SetVisible(false);
        }

        npcAnimation?.PlayIdle();
        npcAnimation?.SetLookAtCamera(false);
        ownerInteractable?.ExitCloseup();

        if (MainLoop.Instance != null)
        {
            MainLoop.Instance.SetMainLoopState_Main();
        }
    }

    private void BindUiEvents()
    {
        if (interactionUI == null)
        {
            interactionUI = InvestigationInteractionUI.GetOrCreateInstance();
        }

        if (interactionUI == null)
        {
            return;
        }

        interactionUI.ActionRequested -= HandleActionRequested;
        interactionUI.CloseRequested -= HandleCloseRequested;
        interactionUI.ActionRequested += HandleActionRequested;
        interactionUI.CloseRequested += HandleCloseRequested;
    }

    private void RefreshInteractionChoices()
    {
        if (interactionUI == null || contextBuilder == null)
        {
            return;
        }

        List<string> evidenceIds = contextBuilder.GetDiscoveredEvidenceIds();
        List<string> informationIds = contextBuilder.GetDiscoveredInformationIds();
        List<string> attachmentIds = new(evidenceIds);
        foreach (string informationId in informationIds)
        {
            if (!string.IsNullOrWhiteSpace(informationId) && !attachmentIds.Contains(informationId))
            {
                attachmentIds.Add(informationId);
            }
        }

        interactionUI.ConfigureSelectionOptions(
            contextBuilder.GetUnlockedTopicIds(conversationState),
            attachmentIds,
            informationIds);
    }
}
