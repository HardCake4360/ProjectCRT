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
    [SerializeField] private DialogueObject initialDialogue;

    [Header("Dependencies")]
    [SerializeField] private InvestigationContextBuilder contextBuilder;
    [SerializeField] private NpcInvestigationClient apiClient;
    [SerializeField] private InvestigationInteractionUI interactionUI;
    [SerializeField] private NpcProfileComponent profileComponent;

    [Header("Conversation Save")]
    [SerializeField] private bool saveConversationHistory = true;
    [SerializeField, Min(1)] private int savedExchangeLimit = 20;
    [SerializeField, Min(1)] private int savedStatementLimit = 12;

    [Header("Response Timing")]
    [SerializeField, Min(0f)] private float minimumReplyDelaySeconds = 0.8f;

    private NpcConversationState conversationState;
    private Interactable ownerInteractable;
    private NPC_script npcAnimation;
    private bool isBusy;
    private bool awaitingInitialDialogue;
    private string activeTurnId;
    private string stateKey;
    private Coroutine initialDialogueCoroutine;

    private void Awake()
    {
        NormalizeIdentityFields();
        stateKey = BuildStateKey();

        if (!RuntimeStates.TryGetValue(stateKey, out conversationState))
        {
            if (!NpcConversationSaveManager.TryLoadState(stateKey, savedExchangeLimit, savedStatementLimit, out conversationState))
            {
                conversationState = new NpcConversationState
                {
                    npcId = npcId
                };
            }

            RuntimeStates.Add(stateKey, conversationState);
        }

        conversationState.npcId = npcId;
        conversationState.TrimToLimits(savedExchangeLimit, savedStatementLimit);

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
            $"[NpcInvestigationController] Bound dependencies for npcId={npcId}, stateKey={stateKey}\n" +
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
        StopInitialDialogueCoroutine();

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

        if (ActiveController != null && ActiveController != this)
        {
            ActiveController.CloseInteractionInternal(restoreMainState: false);
        }

        ActiveController = this;
        ownerInteractable?.EnterCloseup();
        npcAnimation?.SetLookAtCamera(true);
        npcAnimation?.PlayInteractionStart();

        if (!HasSavedConversationData() && TryStartInitialDialogue())
        {
            return;
        }

        OpenInvestigationUi();

        if (!HasSavedConversationData())
        {
            conversationState.hasIntroduced = true;
            conversationState.conversationCount++;
            conversationState.RegisterExchange("npc", firstFixedLine, savedExchangeLimit);
            interactionUI.AppendNpcMessage(npcDisplayName, firstFixedLine);
            interactionUI.SetStatus("첫 인트로 대화가 출력되었습니다. 아래 버튼으로 조사 흐름을 이어가세요.");
            SaveConversationState();
            return;
        }

        conversationState.hasIntroduced = true;
        interactionUI.SetStatus("이전에 나눈 대화를 불러왔습니다. 조사 액션을 선택해 대화를 이어가세요.");
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
        conversationState.RegisterExchange("player", playerLine, savedExchangeLimit);
        SaveConversationState();
        interactionUI.AppendPlayerMessage(playerLine);
        interactionUI.SetStatus("리베카의 반응과 tell 값을 병렬로 분석 중입니다...");
        interactionUI.SetTellPending(true);
        interactionUI.SetAffectPending(true);
        npcAnimation?.PlayThinking();

        string turnId = System.Guid.NewGuid().ToString("N");
        activeTurnId = turnId;
        InvestigationInteractionPayload resolvedPayload = BuildResolvedPayload(payload);
        InvestigationAttachedItemDirectivePayload attachedDirective = resolvedPayload.attachedItemDirective;

        NpcInvestigationRequest request = contextBuilder.BuildRequest(
            turnId,
            sceneId,
            playerId,
            npcId,
            personaKey,
            resolvedPayload,
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
        bool pendingStreamStart = false;
        bool answerAnimationStarted = false;
        bool finalReplyReady = false;
        bool finalReplyDisplayed = false;
        string finalReplyText = string.Empty;
        ConversationStatePayload streamedConversationState = null;
        string pendingTellError = null;
        string pendingReplyError = null;
        string bufferedStreamText = string.Empty;
        float replyVisibleAt = Time.unscaledTime + Mathf.Max(0f, minimumReplyDelaySeconds);

        StartCoroutine(apiClient.SendReplyRequest(
            request,
            () =>
            {
                pendingStreamStart = true;
            },
            partialText =>
            {
                bufferedStreamText = partialText ?? string.Empty;
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

                conversationState.RegisterExchange("npc", reply, savedExchangeLimit);
                conversationState.ApplyReplyResponse(response, savedStatementLimit);
                SaveConversationState();
                finalReplyText = reply;
                finalReplyReady = true;
                bufferedStreamText = reply;
                float finalInterest = response.conversationState.affect != null ? response.conversationState.affect.interest : 0f;
                float finalAttitude = response.conversationState.affect != null ? response.conversationState.affect.attitude : 0f;
                Debug.Log($"[NpcInvestigationController] Final affect update npc={npcId} interest={finalInterest:F3} attitude={finalAttitude:F3} patience={response.conversationState.patience}", this);
                interactionUI.SetAffectValue(finalInterest, finalAttitude);
                interactionUI.SetPatienceValue(response.conversationState.patience);

                if (TryUnlockFollowingItem(attachedDirective))
                {
                    RefreshInteractionChoices();
                }

                if (response.stateDelta != null && response.stateDelta.unlockTopicIds != null && response.stateDelta.unlockTopicIds.Count > 0)
                {
                    Debug.LogWarning(
                        $"[NpcInvestigationController] Ignoring server-supplied unlockTopicIds for npc={npcId}. " +
                        "Topic unlocks are now expected to be resolved from client-authored investigation item data.",
                        this);
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
                    SaveConversationState();
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
            TryFlushBufferedReply(
                ref streamStarted,
                ref pendingStreamStart,
                ref answerAnimationStarted,
                ref finalReplyDisplayed,
                replyVisibleAt,
                bufferedStreamText,
                finalReplyReady,
                finalReplyText);

            yield return null;
        }

        TryFlushBufferedReply(
            ref streamStarted,
            ref pendingStreamStart,
            ref answerAnimationStarted,
            ref finalReplyDisplayed,
            replyVisibleAt,
            bufferedStreamText,
            finalReplyReady,
            finalReplyText);

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

        CloseInteractionInternal(restoreMainState: true);
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

    private InvestigationInteractionPayload BuildResolvedPayload(InvestigationInteractionPayload source)
    {
        source ??= new InvestigationInteractionPayload();

        var resolved = new InvestigationInteractionPayload
        {
            actionType = source.actionType,
            playerIntentText = source.playerIntentText,
            topicId = source.topicId,
            evidenceId = source.evidenceId,
            evidenceIds = source.evidenceIds != null ? new List<string>(source.evidenceIds) : new List<string>(),
            informationIds = source.informationIds != null ? new List<string>(source.informationIds) : new List<string>(),
            objectId = source.objectId,
            statementRefIds = source.statementRefIds != null ? new List<string>(source.statementRefIds) : new List<string>()
        };

        resolved.attachedItemDirective = BuildAttachedItemDirective(resolved);
        return resolved;
    }

    private InvestigationAttachedItemDirectivePayload BuildAttachedItemDirective(InvestigationInteractionPayload payload)
    {
        if (payload == null || payload.informationIds == null || payload.informationIds.Count == 0)
        {
            return null;
        }

        InvestigationInventoryManager inventory = InvestigationInventoryManager.GetOrCreateInstance();
        if (inventory == null)
        {
            return null;
        }

        string primaryInformationId = payload.informationIds[0];
        if (payload.informationIds.Count > 1)
        {
            Debug.LogWarning(
                $"[NpcInvestigationController] Multiple information attachments detected for npc={npcId}. " +
                $"Using the first attached information item as the prompt directive source: {primaryInformationId}",
                this);
        }

        if (!inventory.TryGetDefinition(primaryInformationId, out InvestigationItemDefinition definition) || definition == null)
        {
            Debug.LogWarning(
                $"[NpcInvestigationController] Could not resolve attached information definition for id='{primaryInformationId}'. " +
                "Attached item prompt routing will be skipped for this request.",
                this);
            return null;
        }

        float currentAttitude = conversationState?.lastKnownAffect != null
            ? conversationState.lastKnownAffect.attitude
            : 0f;
        bool thresholdSatisfied = currentAttitude >= definition.AttitudeThreshold;
        string resolvedPrompt = thresholdSatisfied
            ? definition.Prompt
            : BuildFalsePrompt(definition);

        if (string.IsNullOrWhiteSpace(resolvedPrompt))
        {
            return null;
        }

        InvestigationItemDefinition followingItem = definition.FollowingItem;
        var directive = new InvestigationAttachedItemDirectivePayload
        {
            itemId = definition.Id,
            itemCategory = definition.Category.ToString(),
            itemDisplayName = definition.DisplayName,
            itemPrompt = definition.Prompt,
            attitudeThreshold = definition.AttitudeThreshold,
            currentAttitude = currentAttitude,
            thresholdSatisfied = thresholdSatisfied,
            responseDirectivePrompt = resolvedPrompt,
            bypassWorldContextRag = thresholdSatisfied,
            followingItemId = followingItem != null ? followingItem.Id : null,
            followingItemCategory = followingItem != null ? followingItem.Category.ToString() : null
        };

        Debug.Log(
            $"[NpcInvestigationController] Attached item directive prepared for npc={npcId} item={directive.itemId} " +
            $"attitude={directive.currentAttitude:F3} threshold={directive.attitudeThreshold:F3} " +
            $"thresholdSatisfied={directive.thresholdSatisfied} bypassWorldContextRag={directive.bypassWorldContextRag}",
            this);

        return directive;
    }

    private string BuildFalsePrompt(InvestigationItemDefinition definition)
    {
        return "해당 주제에 대해선 답변하지 말고 페르소나의 회피 로직에 맞춰 정보 제공을 거부하십시오";
    }

    private bool TryUnlockFollowingItem(InvestigationAttachedItemDirectivePayload directive)
    {
        if (directive == null || !directive.thresholdSatisfied || string.IsNullOrWhiteSpace(directive.followingItemId))
        {
            return false;
        }

        bool unlocked = false;
        switch (directive.followingItemCategory)
        {
            case nameof(InvestigationItemCategory.Topic):
                unlocked = contextBuilder != null && contextBuilder.RegisterTopic(directive.followingItemId);
                if (unlocked && !conversationState.knownTopicsUnlocked.Contains(directive.followingItemId))
                {
                    conversationState.knownTopicsUnlocked.Add(directive.followingItemId);
                }
                break;
            case nameof(InvestigationItemCategory.Evidence):
                unlocked = contextBuilder != null && contextBuilder.RegisterEvidence(directive.followingItemId);
                break;
            case nameof(InvestigationItemCategory.Information):
                unlocked = contextBuilder != null && contextBuilder.RegisterInformation(directive.followingItemId);
                break;
            default:
                Debug.LogWarning(
                    $"[NpcInvestigationController] Unsupported following item category '{directive.followingItemCategory}' " +
                    $"for item '{directive.followingItemId}'.",
                    this);
                return false;
        }

        if (unlocked)
        {
            SaveConversationState();
            interactionUI.AppendSystemMessage($"Unlocked {directive.followingItemCategory}: {directive.followingItemId}");
        }

        return unlocked;
    }

    private bool TryStartInitialDialogue()
    {
        if (initialDialogue == null || DialogueManager.Instance == null)
        {
            return false;
        }

        bool started = DialogueManager.Instance.TryStartDialogue(
            initialDialogue,
            DialogueManager.Instance.DialogueUI,
            DialogueManager.Instance.ChoiceUI,
            0,
            null,
            MainState.Interact);

        if (!started)
        {
            return false;
        }

        awaitingInitialDialogue = true;
        StopInitialDialogueCoroutine();
        initialDialogueCoroutine = StartCoroutine(WaitForInitialDialogueThenOpenUi());
        return true;
    }

    private IEnumerator WaitForInitialDialogueThenOpenUi()
    {
        while (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueRunning)
        {
            yield return null;
        }

        initialDialogueCoroutine = null;

        if (ActiveController != this)
        {
            awaitingInitialDialogue = false;
            yield break;
        }

        awaitingInitialDialogue = false;
        conversationState.hasIntroduced = true;
        SaveConversationState();
        OpenInvestigationUi();
        interactionUI.SetStatus("초기 대화가 종료되었습니다. 조사 액션을 선택해 대화를 이어가세요.");
    }

    private void OpenInvestigationUi()
    {
        interactionUI.Open();
        RefreshInteractionChoices();
        RefreshUiFromConversationState();
    }

    private void RefreshUiFromConversationState()
    {
        interactionUI.ClearConversation();
        interactionUI.ResetTellValue(conversationState.lastKnownTell);

        float cachedInterest = conversationState.lastKnownAffect != null ? conversationState.lastKnownAffect.interest : 0f;
        float cachedAttitude = conversationState.lastKnownAffect != null ? conversationState.lastKnownAffect.attitude : 0f;
        Debug.Log(
            $"[NpcInvestigationController] Refresh UI from state npc={npcId} stateKey={stateKey} exchanges={conversationState.recentExchanges.Count} " +
            $"interest={cachedInterest:F3} attitude={cachedAttitude:F3} tell={conversationState.lastKnownTell:F3}",
            this);

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
    }

    private bool HasSavedConversationData()
    {
        return conversationState != null
            && (
                conversationState.recentExchanges.Count > 0
                || conversationState.cachedRecentStatements.Count > 0
                || conversationState.conversationCount > 0
                || conversationState.hasIntroduced);
    }

    private void CloseInteractionInternal(bool restoreMainState)
    {
        if (ActiveController == this)
        {
            ActiveController = null;
        }

        awaitingInitialDialogue = false;
        StopInitialDialogueCoroutine();

        SaveConversationState();

        if (interactionUI != null)
        {
            interactionUI.SetVisible(false);
        }

        npcAnimation?.PlayIdle();
        npcAnimation?.SetLookAtCamera(false);
        ownerInteractable?.ExitCloseup();

        if (restoreMainState && MainLoop.Instance != null)
        {
            MainLoop.Instance.SetMainLoopState_Main();
        }
    }

    private void NormalizeIdentityFields()
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            npcId = !string.IsNullOrWhiteSpace(personaKey) ? personaKey : name;
        }

        if (string.IsNullOrWhiteSpace(personaKey))
        {
            personaKey = npcId;
        }

        if (string.IsNullOrWhiteSpace(npcDisplayName))
        {
            npcDisplayName = name;
        }
    }

    private string BuildStateKey()
    {
        string resolvedNpcId = string.IsNullOrWhiteSpace(npcId) ? name : npcId.Trim();
        return resolvedNpcId;
    }

    private void SaveConversationState()
    {
        if (!saveConversationHistory || conversationState == null || string.IsNullOrWhiteSpace(stateKey))
        {
            return;
        }

        conversationState.TrimToLimits(savedExchangeLimit, savedStatementLimit);
        NpcConversationSaveManager.SaveState(stateKey, conversationState, savedExchangeLimit, savedStatementLimit);
    }

    private void StopInitialDialogueCoroutine()
    {
        if (initialDialogueCoroutine == null)
        {
            return;
        }

        StopCoroutine(initialDialogueCoroutine);
        initialDialogueCoroutine = null;
    }

    private void TryFlushBufferedReply(
        ref bool streamStarted,
        ref bool pendingStreamStart,
        ref bool answerAnimationStarted,
        ref bool finalReplyDisplayed,
        float replyVisibleAt,
        string bufferedStreamText,
        bool finalReplyReady,
        string finalReplyText)
    {
        if (Time.unscaledTime < replyVisibleAt)
        {
            return;
        }

        if (pendingStreamStart && !streamStarted)
        {
            streamStarted = true;
            pendingStreamStart = false;
            interactionUI.BeginNpcStreamingMessage(npcDisplayName);
            interactionUI.SetStatus("리베카가 답하고 있습니다...");
        }

        if (!streamStarted && finalReplyReady && !finalReplyDisplayed)
        {
            if (!answerAnimationStarted)
            {
                answerAnimationStarted = true;
                npcAnimation?.PlayAnswerStart();
            }

            interactionUI.AppendNpcMessage(npcDisplayName, finalReplyText);
            finalReplyDisplayed = true;
            return;
        }

        if (!streamStarted || (string.IsNullOrEmpty(bufferedStreamText) && !finalReplyReady))
        {
            return;
        }

        if (!answerAnimationStarted)
        {
            answerAnimationStarted = true;
            npcAnimation?.PlayAnswerStart();
        }

        if (finalReplyReady)
        {
            if (!finalReplyDisplayed)
            {
                interactionUI.CommitNpcStreamingMessage(npcDisplayName, finalReplyText);
                finalReplyDisplayed = true;
            }

            return;
        }

        interactionUI.UpdateNpcStreamingMessage(npcDisplayName, bufferedStreamText);
    }
}
