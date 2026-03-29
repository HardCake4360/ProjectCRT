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

    private NpcConversationState conversationState;
    private bool isBusy;

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

        Debug.Log(
            $"[NpcInvestigationController] Bound dependencies for npcId={npcId}\n" +
            $"controllerObject={name}\n" +
            $"apiClientObject={(apiClient != null ? apiClient.name : "null")}\n" +
            $"uiObject={(interactionUI != null ? interactionUI.name : "null")}",
            this);
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
        interactionUI.Open(npcDisplayName);
        interactionUI.ClearConversation();
        interactionUI.PresentSignal(conversationState.lastKnownSignal);
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
        interactionUI.SetStatus("리베카의 반응을 분석 중입니다...");

        NpcInvestigationRequest request = contextBuilder.BuildRequest(
            sceneId,
            playerId,
            npcId,
            personaKey,
            payload,
            conversationState);

        bool completed = false;
        bool streamStarted = false;
        BioSignalPayload streamedSignal = conversationState.lastKnownSignal;

        yield return apiClient.SendRequest(
            request,
            () =>
            {
                streamStarted = true;
                interactionUI.BeginNpcStreamingMessage(npcDisplayName);
                interactionUI.SetStatus("리베카가 답하고 있습니다...");
            },
            partialText =>
            {
                interactionUI.UpdateNpcStreamingMessage(npcDisplayName, partialText);
            },
            signal =>
            {
                streamedSignal = signal ?? BioSignalPayload.Default();
                interactionUI.PresentSignal(streamedSignal);
            },
            response =>
            {
                string reply = string.IsNullOrWhiteSpace(response.replyText)
                    ? "..."
                    : response.replyText.Trim();

                if (response.signal == null)
                {
                    response.signal = streamedSignal ?? BioSignalPayload.Default();
                }

                conversationState.RegisterExchange("npc", reply);
                conversationState.ApplyResponse(response);

                if (streamStarted)
                {
                    interactionUI.CommitNpcStreamingMessage(npcDisplayName, reply);
                }
                else
                {
                    interactionUI.AppendNpcMessage(npcDisplayName, reply);
                }

                interactionUI.PresentSignal(conversationState.lastKnownSignal);
                interactionUI.SetStatus("새 조사 액션을 선택할 수 있습니다.");

                if (response.stateDelta != null && response.stateDelta.unlockTopicIds != null && response.stateDelta.unlockTopicIds.Count > 0)
                {
                    foreach (string topicId in response.stateDelta.unlockTopicIds)
                    {
                        contextBuilder.RegisterTopic(topicId);
                    }
                    interactionUI.AppendSystemMessage($"Unlocked topics: {string.Join(", ", response.stateDelta.unlockTopicIds)}");
                }

                completed = true;
            },
            error =>
            {
                if (streamStarted)
                {
                    interactionUI.CancelNpcStreamingMessage();
                }

                interactionUI.AppendSystemMessage($"서버 오류: {error}");
                interactionUI.SetStatus("연결 실패. 서버 상태를 확인하세요.");
                completed = true;
            });

        while (!completed)
        {
            yield return null;
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
}
