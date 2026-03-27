using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class NpcInvestigationClient : MonoBehaviour
{
    [SerializeField] private string serverBaseUrl = "http://localhost:5000";
    [SerializeField] private string endpoint = "/investigation/npc";

    public IEnumerator SendRequest(
        NpcInvestigationRequest request,
        Action<NpcInvestigationResponse> onSuccess,
        Action<string> onError)
    {
        string url = $"{serverBaseUrl}{endpoint}";
        string json = JsonUtility.ToJson(request);

        using var webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        byte[] body = Encoding.UTF8.GetBytes(json);
        webRequest.uploadHandler = new UploadHandlerRaw(body);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");

        yield return webRequest.SendWebRequest();

        if (webRequest.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(webRequest.error);
            yield break;
        }

        string responseText = webRequest.downloadHandler.text;
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
            onError?.Invoke(string.IsNullOrWhiteSpace(response.error) ? "Investigation API returned a failure response." : response.error);
            yield break;
        }

        onSuccess?.Invoke(response);
    }
}
