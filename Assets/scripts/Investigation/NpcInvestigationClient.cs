using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NpcInvestigationClient : MonoBehaviour
{
    [SerializeField] private string serverBaseUrl = "http://localhost:5000";
    [SerializeField] private string replyEndpoint = "/investigation/npc/reply";
    [SerializeField] private string tellEndpoint = "/investigation/npc/tell";
    [Header("Rebecca Joint Dialogue / TII Prototype")]
    [SerializeField] private bool enableRebeccaJointTurn = true;
    [SerializeField] private string jointTurnEndpoint = "/investigation/npc/turn";
    [SerializeField, Min(1)] private int requestTimeoutSeconds = 90;
    private readonly Dictionary<string, List<RequestRegistration>> activeRequests = new();

    public int RequestTimeoutSeconds => Mathf.Max(1, requestTimeoutSeconds);

    public bool UsesJointTurn(string npcId, string personaKey) => enableRebeccaJointTurn &&
        string.Equals(npcId?.Trim(), "rebecca", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(personaKey?.Trim(), "rebecca", StringComparison.OrdinalIgnoreCase);

    private static bool InRange(float value, float min, float max) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= min && value <= max;

    public static bool TryValidateJointTurnResponse(NpcJointTurnResponse response, NpcInvestigationRequest request, out string error)
    {
        error = "레베카의 대사와 TII를 함께 확인하지 못했습니다. 다시 질문하세요.";
        if (request == null || response == null || !response.ok || !string.IsNullOrWhiteSpace(response.error) ||
            response.protocolVersion != NpcJointTurnResponse.Protocol || request.protocolVersion != NpcJointTurnResponse.Protocol ||
            string.IsNullOrWhiteSpace(request.sessionId) || response.sessionId != request.sessionId || response.turnId != request.turnId ||
            !TryValidateReplyResponse(response.reply, request.turnId, out _) ||
            !TryValidateTellResult(response.tellResult, request.turnId, out _)) return false;
        var state = response.reply.conversationState;
        if (state?.affect == null || !InRange(state.affect.interest, -1f, 1f) || !InRange(state.affect.attitude, -1f, 1f) ||
            state.patience < 0 || state.patience > 100 || response.usedClaimIds == null) return false;
        var previous = request.npcLocalState;
        float priorInterest = previous?.lastKnownAffect?.interest ?? 0f;
        float priorAttitude = previous?.lastKnownAffect?.attitude ?? 0f;
        int priorPatience = previous?.lastKnownPatience ?? 100;
        if (Mathf.Abs(state.affect.interest - priorInterest) > 0.201f ||
            Mathf.Abs(state.affect.attitude - priorAttitude) > 0.151f ||
            state.patience - priorPatience < -10 || state.patience - priorPatience > 5) return false;
        var directive = request.interaction?.attachedItemDirective;
        bool expectsClaim = directive != null && directive.thresholdSatisfied && !string.IsNullOrWhiteSpace(directive.responseDirectivePrompt);
        if (expectsClaim ? response.usedClaimIds.Count != 1 || response.usedClaimIds[0] != directive.itemId : response.usedClaimIds.Count != 0)
            return false;
        float tell = response.tellResult.tell;
        string expectedBand = tell < .25f ? "Stable" : tell < .5f ? "Guarded" : tell < .75f ? "Shaken" : "Disturbed";
        if (response.tellResult.band != expectedBand) return false;
        error = null;
        return true;
    }

    public virtual IEnumerator SendJointTurnRequest(NpcInvestigationRequest request,
        Action<NpcJointTurnResponse> onSuccess, Action<string> onError)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.turnId) || string.IsNullOrWhiteSpace(request.sessionId))
        { onError?.Invoke("대화 턴 식별자가 없습니다."); yield break; }
        string url = $"{serverBaseUrl}{jointTurnEndpoint}";
        using var registration = TrackRequest(request.turnId, new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST));
        var webRequest = registration.Request;
        webRequest.timeout = RequestTimeoutSeconds;
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Accept", "application/json");
        yield return webRequest.SendWebRequest();
        if (registration.IsCancelled) yield break;
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"레베카 통합 응답 요청 실패 (HTTP {webRequest.responseCode}). 서버를 확인하세요.");
            yield break;
        }
        NpcJointTurnResponse response;
        try { response = JsonUtility.FromJson<NpcJointTurnResponse>(webRequest.downloadHandler.text); }
        catch (Exception) { onError?.Invoke("레베카 통합 응답 형식이 올바르지 않습니다."); yield break; }
        if (TryValidateJointTurnResponse(response, request, out string error)) onSuccess?.Invoke(response);
        else onError?.Invoke(error);
    }

    public void CancelTurn(string turnId)
    {
        if (string.IsNullOrEmpty(turnId) || !activeRequests.TryGetValue(turnId, out var requests)) return;
        activeRequests.Remove(turnId);
        foreach (var registration in requests) registration.Cancel();
    }

    private void OnDisable()
    {
        foreach (string turnId in new List<string>(activeRequests.Keys)) CancelTurn(turnId);
    }

    private RequestRegistration TrackRequest(string turnId, UnityWebRequest request)
    {
        var registration = new RequestRegistration(this, turnId, request);
        if (!activeRequests.TryGetValue(turnId, out var requests))
            activeRequests.Add(turnId, requests = new List<RequestRegistration>());
        requests.Add(registration);
        return registration;
    }

    private sealed class RequestRegistration : IDisposable
    {
        private readonly NpcInvestigationClient owner;
        private readonly string turnId;
        private bool disposed;
        public UnityWebRequest Request { get; }
        public bool IsCancelled { get; private set; }

        public RequestRegistration(NpcInvestigationClient owner, string turnId, UnityWebRequest request)
        { this.owner = owner; this.turnId = turnId; Request = request; }

        public void Cancel()
        {
            if (disposed) return;
            IsCancelled = true;
            try { Request.Abort(); }
            finally { Dispose(); }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (owner.activeRequests.TryGetValue(turnId, out var requests))
            {
                requests.Remove(this);
                if (requests.Count == 0) owner.activeRequests.Remove(turnId);
            }
            Request.Dispose();
        }
    }

    public static bool TryValidateReplyResponse(NpcInvestigationReplyResponse response, string turnId, out string error)
    {
        error = "현재 질문에 대한 유효한 NPC 답변이 아닙니다. 다시 시도하세요.";
        if (response == null || !response.ok || string.IsNullOrWhiteSpace(turnId) || response.turnId != turnId ||
            !string.IsNullOrWhiteSpace(response.error)) return false;
        string reply = response.replyText?.Trim();
        if (string.IsNullOrWhiteSpace(reply) || reply == "..." || reply == "…" ||
            reply.StartsWith("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
            reply.StartsWith("[SERVER ERROR]", StringComparison.OrdinalIgnoreCase)) return false;
        error = null;
        return true;
    }

    public static bool TryValidateTellResult(TellResultPayload result, string turnId, out string error)
    {
        error = "현재 질문에 대한 tell 분석을 가져오지 못했습니다.";
        if (result == null || string.IsNullOrWhiteSpace(turnId) || result.turnId != turnId ||
            float.IsNaN(result.tell) || float.IsInfinity(result.tell) || result.tell < 0f || result.tell > 1f ||
            (result.reason ?? string.Empty).IndexOf("invalid JSON", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        error = null;
        return true;
    }

    public virtual IEnumerator SendReplyRequest(
        NpcInvestigationRequest request,
        Action onStreamStarted,
        Action<string> onStreamDelta,
        Action<ConversationStatePayload> onConversationStateReceived,
        Action<NpcInvestigationReplyResponse> onSuccess,
        Action<string> onError)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.turnId))
        { onError?.Invoke("질문 식별자가 없습니다."); yield break; }
        string url = $"{serverBaseUrl}{replyEndpoint}";
        string json = JsonUtility.ToJson(request);
        string requestedPersonaKey = request != null ? request.personaKey : string.Empty;
        var downloadHandler = new ReplyStreamDownloadHandler();

        Debug.Log(
            $"[NpcInvestigationClient] Sending reply request from '{name}' on '{gameObject.scene.name}'\n" +
            $"personaKey={requestedPersonaKey}\n" +
            $"url={url}\n" +
            $"payload={json}",
            this);

        using var registration = TrackRequest(request.turnId, new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST));
        UnityWebRequest webRequest = registration.Request;
        webRequest.timeout = RequestTimeoutSeconds;
        byte[] body = Encoding.UTF8.GetBytes(json);
        webRequest.uploadHandler = new UploadHandlerRaw(body);
        webRequest.downloadHandler = downloadHandler;
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Accept", "application/x-ndjson, application/json");

        UnityWebRequestAsyncOperation operation = webRequest.SendWebRequest();
        string streamedNpcText = string.Empty;
        bool streamStarted = false;
        NpcInvestigationReplyResponse streamedCompletion = null;
        string streamedError = string.Empty;

        while (!registration.IsCancelled && !operation.isDone)
        {
            DrainReplyStreamEvents(
                downloadHandler,
                ref streamStarted,
                ref streamedNpcText,
                ref streamedCompletion,
                ref streamedError,
                onStreamStarted,
                onStreamDelta,
                onConversationStateReceived);

            yield return null;
        }

        if (registration.IsCancelled) yield break;

        DrainReplyStreamEvents(
            downloadHandler,
            ref streamStarted,
            ref streamedNpcText,
            ref streamedCompletion,
            ref streamedError,
            onStreamStarted,
            onStreamDelta,
            onConversationStateReceived);

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            string responseBody = downloadHandler.GetResponseText();
            string detail = BuildErrorDetail(url, requestedPersonaKey, webRequest.responseCode, webRequest.error, responseBody);
            Debug.LogError(
                $"[NpcInvestigationClient] Reply request failed\n" +
                $"personaKey={requestedPersonaKey}\n" +
                $"url={url}\n" +
                $"responseCode={webRequest.responseCode}\n" +
                $"error={webRequest.error}\n" +
                $"responseBody={responseBody}",
                this);
            onError?.Invoke(detail);
            yield break;
        }

        if (!string.IsNullOrWhiteSpace(streamedError))
        {
            onError?.Invoke(BuildFailureDetail(requestedPersonaKey, streamedError));
            yield break;
        }

        if (streamedCompletion != null)
        {
            if (TryValidateReplyResponse(streamedCompletion, request.turnId, out string error)) onSuccess?.Invoke(streamedCompletion);
            else onError?.Invoke(error);
            yield break;
        }

        string responseText = downloadHandler.GetResponseText();
        if (string.IsNullOrWhiteSpace(responseText))
        {
            onError?.Invoke("Investigation reply API returned an empty response.");
            yield break;
        }

        NpcInvestigationReplyResponse response;
        try
        {
            response = JsonUtility.FromJson<NpcInvestigationReplyResponse>(responseText);
        }
        catch (Exception exception)
        {
            onError?.Invoke($"Failed to parse investigation reply response: {exception.Message}");
            yield break;
        }

        if (response == null)
        {
            onError?.Invoke("Investigation reply API returned an invalid payload.");
            yield break;
        }

        if (!response.ok)
        {
            onError?.Invoke(BuildFailureDetail(requestedPersonaKey, response.error));
            yield break;
        }

        if (TryValidateReplyResponse(response, request.turnId, out string validationError)) onSuccess?.Invoke(response);
        else onError?.Invoke(validationError);
    }

    public virtual IEnumerator SendTellRequest(
        NpcTellRequest request,
        Action<TellResultPayload> onSuccess,
        Action<string> onError)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.turnId))
        { onError?.Invoke("질문 식별자가 없습니다."); yield break; }
        string url = $"{serverBaseUrl}{tellEndpoint}";
        string json = JsonUtility.ToJson(request);
        string requestedPersonaKey = request != null ? request.personaKey : string.Empty;

        Debug.Log(
            $"[NpcInvestigationClient] Sending tell request from '{name}' on '{gameObject.scene.name}'\n" +
            $"personaKey={requestedPersonaKey}\n" +
            $"url={url}\n" +
            $"payload={json}",
            this);

        using var registration = TrackRequest(request.turnId, new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST));
        UnityWebRequest webRequest = registration.Request;
        webRequest.timeout = RequestTimeoutSeconds;
        byte[] body = Encoding.UTF8.GetBytes(json);
        webRequest.uploadHandler = new UploadHandlerRaw(body);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Accept", "application/json");

        yield return webRequest.SendWebRequest();

        if (registration.IsCancelled) yield break;

        string responseBody = webRequest.downloadHandler.text;
        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            string detail = BuildErrorDetail(url, requestedPersonaKey, webRequest.responseCode, webRequest.error, responseBody);
            Debug.LogError(
                $"[NpcInvestigationClient] Tell request failed\n" +
                $"personaKey={requestedPersonaKey}\n" +
                $"url={url}\n" +
                $"responseCode={webRequest.responseCode}\n" +
                $"error={webRequest.error}\n" +
                $"responseBody={responseBody}",
                this);
            onError?.Invoke(detail);
            yield break;
        }

        NpcTellResponse response;
        try
        {
            response = JsonUtility.FromJson<NpcTellResponse>(responseBody);
        }
        catch (Exception exception)
        {
            onError?.Invoke($"Failed to parse tell response: {exception.Message}");
            yield break;
        }

        if (response == null)
        {
            onError?.Invoke("Tell API returned an invalid payload.");
            yield break;
        }

        if (!response.ok)
        {
            onError?.Invoke(BuildFailureDetail(requestedPersonaKey, response.error));
            yield break;
        }

        if (response.turnId == request.turnId && TryValidateTellResult(response.tellResult, request.turnId, out _))
            onSuccess?.Invoke(response.tellResult);
        else onError?.Invoke("현재 질문에 대한 유효한 tell 응답이 아닙니다.");
    }

    private void DrainReplyStreamEvents(
        ReplyStreamDownloadHandler downloadHandler,
        ref bool streamStarted,
        ref string streamedNpcText,
        ref NpcInvestigationReplyResponse streamedCompletion,
        ref string streamedError,
        Action onStreamStarted,
        Action<string> onStreamDelta,
        Action<ConversationStatePayload> onConversationStateReceived)
    {
        while (downloadHandler.TryDequeueChunk(out NpcInvestigationReplyStreamChunk chunk))
        {
            if (chunk == null)
            {
                continue;
            }

            switch (chunk.type)
            {
                case "start":
                    if (!streamStarted)
                    {
                        streamStarted = true;
                        onStreamStarted?.Invoke();
                    }
                    break;
                case "delta":
                    if (!streamStarted)
                    {
                        streamStarted = true;
                        onStreamStarted?.Invoke();
                    }

                    streamedNpcText += chunk.text ?? string.Empty;
                    onStreamDelta?.Invoke(streamedNpcText);
                    break;
                case "state":
                case "conversationState":
                    onConversationStateReceived?.Invoke(chunk.conversationState ?? ConversationStatePayload.Default());
                    break;
                case "error":
                    streamedError = chunk.error;
                    break;
                case "complete":
                    if (chunk.response == null) streamedError = "서버의 최종 답변이 누락되었습니다.";
                    else streamedCompletion = chunk.response;
                    break;
            }
        }
    }

    private static string BuildErrorDetail(string url, string requestedPersonaKey, long responseCode, string webError, string responseBody)
    {
        string responseError = TryExtractResponseError(responseBody);
        if (IsMissingPersonaError(responseError))
        {
            return $"서버에 personaKey '{requestedPersonaKey}'가 없습니다. 페르소나 파일명/키를 확인하세요.";
        }

        return responseCode == 404
            ? $"Investigation API not found: {url}"
            : webError;
    }

    private static string BuildFailureDetail(string requestedPersonaKey, string responseError)
    {
        if (IsMissingPersonaError(responseError))
        {
            return $"서버에 personaKey '{requestedPersonaKey}'가 없습니다. 페르소나 파일명/키를 확인하세요.";
        }

        return string.IsNullOrWhiteSpace(responseError)
            ? "Investigation API returned a failure response."
            : responseError;
    }

    private static bool IsMissingPersonaError(string responseError)
    {
        return !string.IsNullOrWhiteSpace(responseError) &&
               responseError.IndexOf("persona not found", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string TryExtractResponseError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return string.Empty;
        }

        try
        {
            NpcInvestigationReplyResponse replyResponse = JsonUtility.FromJson<NpcInvestigationReplyResponse>(responseBody);
            if (replyResponse != null && !string.IsNullOrWhiteSpace(replyResponse.error))
            {
                return replyResponse.error;
            }
        }
        catch
        {
        }

        try
        {
            NpcTellResponse tellResponse = JsonUtility.FromJson<NpcTellResponse>(responseBody);
            return tellResponse != null ? tellResponse.error : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class ReplyStreamDownloadHandler : DownloadHandlerScript
    {
        private readonly Queue<NpcInvestigationReplyStreamChunk> pendingChunks = new();
        private readonly StringBuilder rawText = new();
        private readonly StringBuilder lineBuffer = new();
        private readonly object gate = new();
        private readonly Decoder decoder = Encoding.UTF8.GetDecoder();

        public ReplyStreamDownloadHandler() : base(new byte[4096])
        {
        }

        public bool TryDequeueChunk(out NpcInvestigationReplyStreamChunk chunk)
        {
            lock (gate)
            {
                if (pendingChunks.Count == 0)
                {
                    chunk = null;
                    return false;
                }

                chunk = pendingChunks.Dequeue();
                return true;
            }
        }

        public string GetResponseText()
        {
            lock (gate)
            {
                FlushRemainingLine_NoLock();
                return rawText.ToString();
            }
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0)
            {
                return false;
            }

            lock (gate)
            {
                char[] characters = new char[dataLength + 2];
                int count = decoder.GetChars(data, 0, dataLength, characters, 0, false);
                string chunkText = new string(characters, 0, count);
                rawText.Append(chunkText);
                lineBuffer.Append(chunkText);
                ProcessBufferedLines_NoLock();
            }

            return true;
        }

        protected override void CompleteContent()
        {
            lock (gate)
            {
                char[] remaining = new char[2];
                int count = decoder.GetChars(Array.Empty<byte>(), 0, 0, remaining, 0, true);
                rawText.Append(remaining, 0, count);
                lineBuffer.Append(remaining, 0, count);
                FlushRemainingLine_NoLock();
            }
        }

        private void ProcessBufferedLines_NoLock()
        {
            string buffered = lineBuffer.ToString();
            int lineStart = 0;

            for (int index = 0; index < buffered.Length; index++)
            {
                if (buffered[index] != '\n')
                {
                    continue;
                }

                string line = buffered.Substring(lineStart, index - lineStart).Trim();
                lineStart = index + 1;
                EnqueueChunkFromLine_NoLock(line);
            }

            if (lineStart > 0)
            {
                lineBuffer.Clear();
                lineBuffer.Append(buffered.Substring(lineStart));
            }
        }

        private void FlushRemainingLine_NoLock()
        {
            if (lineBuffer.Length == 0)
            {
                return;
            }

            string remaining = lineBuffer.ToString().Trim();
            lineBuffer.Clear();
            EnqueueChunkFromLine_NoLock(remaining);
        }

        private void EnqueueChunkFromLine_NoLock(string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line[0] != '{')
            {
                return;
            }

            try
            {
                NpcInvestigationReplyStreamChunk chunk = JsonUtility.FromJson<NpcInvestigationReplyStreamChunk>(line);
                if (chunk != null && !string.IsNullOrWhiteSpace(chunk.type))
                {
                    pendingChunks.Enqueue(chunk);
                }
            }
            catch
            {
            }
        }
    }
}
