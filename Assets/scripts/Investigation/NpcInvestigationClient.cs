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

    public IEnumerator SendReplyRequest(
        NpcInvestigationRequest request,
        Action onStreamStarted,
        Action<string> onStreamDelta,
        Action<ConversationStatePayload> onConversationStateReceived,
        Action<NpcInvestigationReplyResponse> onSuccess,
        Action<string> onError)
    {
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

        using var webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
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

        while (!operation.isDone)
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
            onSuccess?.Invoke(streamedCompletion);
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

        onSuccess?.Invoke(response);
    }

    public IEnumerator SendTellRequest(
        NpcTellRequest request,
        Action<TellResultPayload> onSuccess,
        Action<string> onError)
    {
        string url = $"{serverBaseUrl}{tellEndpoint}";
        string json = JsonUtility.ToJson(request);
        string requestedPersonaKey = request != null ? request.personaKey : string.Empty;

        Debug.Log(
            $"[NpcInvestigationClient] Sending tell request from '{name}' on '{gameObject.scene.name}'\n" +
            $"personaKey={requestedPersonaKey}\n" +
            $"url={url}\n" +
            $"payload={json}",
            this);

        using var webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        byte[] body = Encoding.UTF8.GetBytes(json);
        webRequest.uploadHandler = new UploadHandlerRaw(body);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.SetRequestHeader("Accept", "application/json");

        yield return webRequest.SendWebRequest();

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

        onSuccess?.Invoke(response.tellResult ?? TellResultPayload.Default());
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
                    streamedCompletion = chunk.response ?? BuildReplyCompletionFallback(streamedNpcText, chunk.conversationState);
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

    private static NpcInvestigationReplyResponse BuildReplyCompletionFallback(string streamedNpcText, ConversationStatePayload conversationState)
    {
        return new NpcInvestigationReplyResponse
        {
            ok = true,
            replyText = streamedNpcText ?? string.Empty,
            conversationState = conversationState ?? ConversationStatePayload.Default(),
            stateDelta = new InvestigationStateDeltaPayload(),
            presentationHints = new InvestigationPresentationHintsPayload()
        };
    }

    private sealed class ReplyStreamDownloadHandler : DownloadHandlerScript
    {
        private readonly Queue<NpcInvestigationReplyStreamChunk> pendingChunks = new();
        private readonly StringBuilder rawText = new();
        private readonly StringBuilder lineBuffer = new();
        private readonly object gate = new();

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

            string chunkText = Encoding.UTF8.GetString(data, 0, dataLength);
            lock (gate)
            {
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
