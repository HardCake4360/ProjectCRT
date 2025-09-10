using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Text;

public class RAGHandler : MonoBehaviour
{
    [System.Serializable]
    public class AskRequest
    {
        public string question;
        public string user_id;
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

        public StreamingHandler(System.Action<string> onChunkCallback) : base()
        {
            onChunk = onChunkCallback;
        }

        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || data.Length == 0) return false;

            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
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
            if(buffer.Length > 0)
            {
                onChunk?.Invoke(buffer.ToString());
                buffer.Clear();
            }
        }
    }


    public IEnumerator AskServer(string question, System.Action<string,string[]> onResponse)
    {
        AskRequest requestData = new AskRequest { question = question };
        string json = JsonUtility.ToJson(requestData);
        Debug.Log("전송할 JSON: " + json);

        UnityWebRequest request = new UnityWebRequest("http://localhost:5000/ask", "POST");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResult = request.downloadHandler.text;
            AskResponse response = JsonUtility.FromJson<AskResponse>(jsonResult);
            onResponse?.Invoke(response.answer, response.context);
        }
        else
        {
            Debug.LogError("오류 발생: " + request.error);
        }
        request.Dispose();
    }

    public IEnumerator AskServerStream(string question, System.Action<string> onTextStream)
    {
        AskRequest requestData = new AskRequest { question = question,user_id = "test" };
        string json = JsonUtility.ToJson(requestData);
        Debug.Log("전송할 JSON: " + json);

        UnityWebRequest request = new UnityWebRequest("http://localhost:5000/ask-stream", "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new StreamingHandler(chunk =>
        {
            Debug.Log("받은 청크: " + chunk);  // 실시간 로그 확인
            onTextStream?.Invoke(chunk);
        });
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
            Debug.LogError("스트리밍 오류: " + request.error);
        request.Dispose();
    }

}
