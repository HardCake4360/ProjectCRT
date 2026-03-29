using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NpcInvestigationClient : MonoBehaviour
{
    [SerializeField] private string serverBaseUrl = "http://localhost:5000";
    [SerializeField] private string endpoint = "/investigation/npc";

    public IEnumerator SendRequest(
        NpcInvestigationRequest request,
        Action onStreamStarted,
        Action<string> onStreamDelta,
        Action<BioSignalPayload> onSignalUpdated,
        Action<NpcInvestigationResponse> onSuccess,
        Action<string> onError)
    {
        string url = $"{serverBaseUrl}{endpoint}";
        string json = JsonUtility.ToJson(request);
        string requestedPersonaKey = request != null ? request.personaKey : string.Empty;
        var downloadHandler = new InvestigationStreamDownloadHandler();

        Debug.Log(
            $"[NpcInvestigationClient] Sending request from '{name}' on '{gameObject.scene.name}'\n" +
            $"personaKey={requestedPersonaKey}\n" +
            $"serverBaseUrl={serverBaseUrl}\n" +
            $"endpoint={endpoint}\n" +
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
        NpcInvestigationResponse streamedCompletion = null;
        string streamedError = string.Empty;

        while (!operation.isDone)
        {
            DrainStreamEvents(
                downloadHandler,
                ref streamStarted,
                ref streamedNpcText,
                ref streamedCompletion,
                ref streamedError,
                onStreamStarted,
                onStreamDelta,
                onSignalUpdated);

            yield return null;
        }

        DrainStreamEvents(
            downloadHandler,
            ref streamStarted,
            ref streamedNpcText,
            ref streamedCompletion,
            ref streamedError,
            onStreamStarted,
            onStreamDelta,
            onSignalUpdated);

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            string responseBody = downloadHandler.GetResponseText();
            string detail = BuildErrorDetail(url, requestedPersonaKey, webRequest.responseCode, webRequest.error, responseBody);
            Debug.LogError(
                $"[NpcInvestigationClient] Request failed\n" +
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
            string detail = BuildFailureDetail(requestedPersonaKey, streamedError);
            Debug.LogError(
                $"[NpcInvestigationClient] Streamed error response\n" +
                $"personaKey={requestedPersonaKey}\n" +
                $"error={streamedError}",
                this);
            onError?.Invoke(detail);
            yield break;
        }

        if (streamedCompletion != null)
        {
            onSuccess?.Invoke(streamedCompletion);
            yield break;
        }

        string responseText = downloadHandler.GetResponseText();
        Debug.Log(
            $"[NpcInvestigationClient] Request succeeded\n" +
            $"url={url}\n" +
            $"responseCode={webRequest.responseCode}\n" +
            $"responseBody={responseText}",
            this);
        if (string.IsNullOrWhiteSpace(responseText))
        {
            onError?.Invoke("Investigation API returned an empty response.");
            yield break;
        }

        NpcInvestigationResponse response;
        try
        {
            response = JsonUtility.FromJson<NpcInvestigationResponse>(responseText);
        }
        catch (Exception exception)
        {
            onError?.Invoke($"Failed to parse investigation response: {exception.Message}");
            yield break;
        }

        if (response == null)
        {
            onError?.Invoke("Investigation API returned an invalid payload.");
            yield break;
        }

        if (!response.ok)
        {
            string detail = BuildFailureDetail(requestedPersonaKey, response.error);
            if (IsMissingPersonaError(response.error))
            {
                Debug.LogError(
                    $"[NpcInvestigationClient] Missing persona on server\n" +
                    $"requestedPersonaKey={requestedPersonaKey}\n" +
                    $"responseError={response.error}",
                    this);
            }

            onError?.Invoke(detail);
            yield break;
        }

        onSuccess?.Invoke(response);
    }

    private void DrainStreamEvents(
        InvestigationStreamDownloadHandler downloadHandler,
        ref bool streamStarted,
        ref string streamedNpcText,
        ref NpcInvestigationResponse streamedCompletion,
        ref string streamedError,
        Action onStreamStarted,
        Action<string> onStreamDelta,
        Action<BioSignalPayload> onSignalUpdated)
    {
        while (downloadHandler.TryDequeueChunk(out NpcInvestigationStreamChunk chunk))
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
                case "signal":
                    onSignalUpdated?.Invoke(chunk.signal ?? BioSignalPayload.Default());
                    break;
                case "error":
                    streamedError = chunk.error;
                    break;
                case "complete":
                    streamedCompletion = chunk.response ?? BuildStreamCompletionFallback(streamedNpcText, chunk.signal);
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
            NpcInvestigationResponse response = JsonUtility.FromJson<NpcInvestigationResponse>(responseBody);
            return response != null ? response.error : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static NpcInvestigationResponse BuildStreamCompletionFallback(string streamedNpcText, BioSignalPayload signal)
    {
        return new NpcInvestigationResponse
        {
            ok = true,
            replyText = streamedNpcText ?? string.Empty,
            signal = signal ?? BioSignalPayload.Default(),
            stateDelta = new InvestigationStateDeltaPayload(),
            presentationHints = new InvestigationPresentationHintsPayload()
        };
    }

    private sealed class InvestigationStreamDownloadHandler : DownloadHandlerScript
    {
        private readonly Queue<NpcInvestigationStreamChunk> pendingChunks = new();
        private readonly StringBuilder rawText = new();
        private readonly StringBuilder lineBuffer = new();
        private readonly object gate = new();

        public InvestigationStreamDownloadHandler() : base(new byte[4096])
        {
        }

        public bool TryDequeueChunk(out NpcInvestigationStreamChunk chunk)
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
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            if (line[0] != '{')
            {
                return;
            }

            try
            {
                NpcInvestigationStreamChunk chunk = JsonUtility.FromJson<NpcInvestigationStreamChunk>(line);
                if (chunk != null && !string.IsNullOrWhiteSpace(chunk.type))
                {
                    pendingChunks.Enqueue(chunk);
                }
            }
            catch
            {
                // Keep the full response body for non-stream JSON parsing.
            }
        }
    }
}
