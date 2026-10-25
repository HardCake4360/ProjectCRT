using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;

public class RAGHandler : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverBaseUrl = "http://localhost:5000";
    [SerializeField] private string userId = "test";
    [SerializeField, Min(1)] private int requestTimeoutSeconds = 90;

    [Header("Persona")]
    [SerializeField] private string personaKey = "";  // 빈 값이면 페르소나 미사용

    [System.Serializable]
    public class AskRequest
    {
        public string question;
        public string user_id;
        public string personaKey; // 빈 문자열이면 서버에서 미사용으로 처리
    }

    [System.Serializable]
    public class AskResponse
    {
        public string answer;
        public string[] context;
    }

    public class StreamingHandler : DownloadHandlerScript
    {
        System.Action<string> onChunk;
        private StringBuilder buffer = new StringBuilder();
        private readonly Decoder decoder = Encoding.UTF8.GetDecoder();

        public StreamingHandler(System.Action<string> onChunkCallback) : base()
        {
            onChunk = onChunkCallback;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0) return false;

            char[] characters = new char[dataLength + 2];
            int count = decoder.GetChars(data, 0, dataLength, characters, 0, false);
            string chunk = new string(characters, 0, count);
            buffer.Append(chunk);

            //줄바꿈 기준으로 chunk쪼개기
            string[] lines = buffer.ToString().Split('\n');

            for (int i = 0; i < lines.Length - 1; i++)
            {
                onChunk?.Invoke(lines[i]);
            }

            buffer.Clear();
            buffer.Append(lines[lines.Length - 1]);// 마지막 줄은 덜 완성된 스트림이므로 보류

            return true;
        }

        protected override void CompleteContent()
        {
            char[] remaining = new char[2];
            int count = decoder.GetChars(Array.Empty<byte>(), 0, 0, remaining, 0, true);
            buffer.Append(remaining, 0, count);
            if(buffer.Length > 0)
            {
                onChunk?.Invoke(buffer.ToString());
                buffer.Clear();
            }
        }
    }

    private string AskUrl => $"{serverBaseUrl}/ask";
    private string AskStream => $"{serverBaseUrl}/ask-stream";

    /// <summary>
    /// 스트리밍 호출 (/ask-stream).
    /// 서버의 ask_stream(question, personaKey?) 포맷에 맞춰 user_id와 personaKey 포함.
    /// </summary>
    public IEnumerator AskServerStream(string question, Action<string> onTextStream)
    {
        var requestData = new AskRequest
        {
            question = question,
            user_id = string.IsNullOrEmpty(userId) ? "anonymous" : userId,
            personaKey = personaKey ?? ""
        };

        string json = JsonUtility.ToJson(requestData);
        Debug.Log("[AskServerStream] 전송 JSON: " + json);

        using (UnityWebRequest request = new UnityWebRequest(AskStream, "POST"))
        {
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // 스트리밍 수신 핸들러
            request.downloadHandler = new StreamingHandler(chunk =>
            {
                // 실시간 수신 텍스트 콜백
                onTextStream?.Invoke(chunk);
            });

            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onTextStream?.Invoke("\n서버 연결에 실패했습니다. 다시 질문할 수 있습니다.");
                Debug.LogWarning("[AskServerStream] 스트리밍 오류: " + request.error);
            }
        }
    }

    // 런타임에 페르소나 교체하고 싶을 때 호출
    public void SetPersonaKey(string key)
    {
        personaKey = key ?? "";
    }
    public string GetPersonaKey()
    {
        return personaKey;
    }

    // 런타임에 서버/유저 설정 변경이 필요할 때
    public void Configure(string newServerBaseUrl = null, string newUserId = null, string newPersonaKey = null)
    {
        if (!string.IsNullOrEmpty(newServerBaseUrl)) serverBaseUrl = newServerBaseUrl;
        if (!string.IsNullOrEmpty(newUserId)) userId = newUserId;
        if (newPersonaKey != null) personaKey = newPersonaKey;
    }

}
