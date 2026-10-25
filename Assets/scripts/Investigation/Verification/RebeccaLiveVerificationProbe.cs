#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ProjectCRT.Cases;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RebeccaLiveVerificationProbe : MonoBehaviour
{
    [Serializable] public sealed class Report
    {
        public bool passed;
        public string error, scene, question, reply, transcript, tellText, turnId;
        public float elapsedSeconds, tell, interest, attitude;
        public int patience;
        public List<string> checks = new();
    }
    Report report = new();
    static object Get(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
    void Check(bool condition, string text) { if (!condition) throw new Exception(text); report.checks.Add(text); }

    IEnumerator Start()
    {
        var run = Run();
        while (true)
        {
            bool more;
            try { more = run.MoveNext(); }
            catch (Exception e) { report.error = e.ToString(); break; }
            if (!more) { report.passed = true; break; }
            yield return run.Current;
        }
        Directory.CreateDirectory("Logs/NpcJointTurn");
        File.WriteAllText("Logs/NpcJointTurn/unity-live-ui.json", JsonUtility.ToJson(report, true));
        Debug.Log("Rebecca live verification: " + report.passed + " " + report.error);
    }

    IEnumerator Run()
    {
        report.scene = gameObject.scene.path;
        var manager = FindFirstObjectByType<ShangrilaCaseManager>();
        float deadline = Time.realtimeSinceStartup + 30;
        while (!manager.Ready && Time.realtimeSinceStartup < deadline) yield return null;
        Check(manager.InteractionsUnlocked, "Actual case scene is in free investigation using an isolated case slot");
        var npcs = FindObjectsByType<NpcInvestigationController>(FindObjectsSortMode.None);
        foreach (var item in npcs) Set(item, "saveConversationHistory", false);
        var npc = npcs.Single(n => (string)Get(n, "npcId") == "rebecca");
        // A new synthetic conversation prevents uploading an existing player's history.
        var state = new NpcConversationState { npcId = "rebecca", hasIntroduced = true };
        Set(npc, "conversationState", state);
        var client = (NpcInvestigationClient)Get(npc, "apiClient");
        Check(client.GetType() == typeof(NpcInvestigationClient) && client.UsesJointTurn("rebecca", "rebecca"),
            "Production UnityWebRequest client and joint-turn route are active; no mock client");
        var player = manager.Player;
        manager.walking.enabled = false;
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false; player.transform.position = new Vector3(-6.5f, 1.43f, 9.5f); cc.enabled = true;
        var cam = player.GetComponentInChildren<Camera>();
        var direction = npc.GetComponent<Collider>().bounds.center - cam.transform.position;
        var horizontal = new Vector3(direction.x, 0, direction.z);
        player.transform.rotation = Quaternion.LookRotation(horizontal);
        float pitch = -Mathf.Atan2(direction.y, horizontal.magnitude) * Mathf.Rad2Deg;
        Set(player, "xRotation", pitch); cam.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        MainLoop.Instance.SetMainLoopState(MainState.Main); Physics.SyncTransforms();
        yield return null;
        Check(player.TryInteractInView(), "Player interaction ray opens Rebecca in the authored case scene");
        var ui = InvestigationInteractionUI.Instance;
        var canvas = (Canvas)Get(ui, "rootCanvas");
        deadline = Time.realtimeSinceStartup + 12;
        while ((!canvas.enabled || !((Button)Get(ui, "sendButton")).interactable) && Time.realtimeSinceStartup < deadline) yield return null;
        yield return new WaitForSecondsRealtime(.5f);
        Check(canvas.enabled && NpcInvestigationController.CurrentInteraction == npc, "Existing closeup camera and investigation UI opened");
        var input = (TMP_InputField)Get(ui, "intentInput");
        var send = (Button)Get(ui, "sendButton");
        var transcript = (TMP_Text)Get(ui, "transcriptText");
        var tell = (TellMeterPresenter)Get(ui, "tellMeterPresenter");
        var affect = (AffectPlanePresenter)Get(ui, "affectPlanePresenter");
        Check(tell != null && affect != null && send.interactable, "Dialogue input, Tell meter and Affect plane are bound");
        report.question = "오랜만이야, 레베카. 제레미는 여기서 어떤 일을 하고 있어?";
        input.text = report.question;
        float started = Time.realtimeSinceStartup;
        send.onClick.Invoke(); // Same existing UI button path used by a player; invoke exactly once.
        report.turnId = (string)Get(npc, "activeTurnId");
        Check((bool)Get(npc, "isBusy") && !send.interactable, "Sending disables input while the real request is pending");
        yield return null;
        yield return new WaitForEndOfFrame(); Capture("unity-live-pending");
        var tellGraphic = (TellPulseGraphic)Get(tell, "pulseGraphic");
        bool pendingSeen = (bool)Get(tellGraphic, "pendingNoise");
        deadline = Time.realtimeSinceStartup + 100;
        while ((bool)Get(npc, "isBusy") && Time.realtimeSinceStartup < deadline)
        {
            pendingSeen |= (bool)Get(tellGraphic, "pendingNoise");
            if (Time.realtimeSinceStartup - started < .95f)
                Check(state.conversationCount == 0, "No reply or TII commits before the one-second gate");
            yield return null;
        }
        report.elapsedSeconds = Time.realtimeSinceStartup - started;
        Check(!(bool)Get(npc, "isBusy") && state.conversationCount == 1, "One real turn completed and committed once");
        Check(pendingSeen && report.elapsedSeconds >= 1f, "Pending Tell state is visible and minimum presentation delay is preserved");
        report.reply = state.recentExchanges.Last(x => x.speaker == "npc").text;
        report.transcript = transcript.text;
        report.tell = state.lastKnownTell; report.interest = state.lastKnownAffect.interest;
        report.attitude = state.lastKnownAffect.attitude; report.patience = state.lastKnownPatience;
        report.tellText = ((TMP_Text)Get(tell, "valueText"))?.text ?? "Graph-only Tell UI";
        Check(transcript.text.Contains(report.reply) && report.reply.Length > 3, "Generated NPC dialogue is present in the existing transcript");
        Check(Mathf.Abs((float)Get(tell, "currentTell") - report.tell) < .001f && !report.tellText.Contains("..."), "Displayed Tell value matches the committed model result");
        Check(Mathf.Abs((float)Get(affect, "currentInterest") - report.interest) < .001f &&
              Mathf.Abs((float)Get(affect, "currentAttitude") - report.attitude) < .001f, "Displayed Affect coordinates match the committed model result in the same completed frame");
        var graphic = (AffectPulseGraphic)Get(affect, "pulseGraphic");
        Check(Mathf.Abs((float)Get(graphic, "interest") - report.interest) < .001f &&
              Mathf.Abs((float)Get(graphic, "attitude") - report.attitude) < .001f, "Rendered Affect graphic contains the final coordinates");
        Check(send.interactable && input.interactable, "Input is restored after the successful response");
        yield return new WaitForSecondsRealtime(.75f);
        yield return new WaitForEndOfFrame(); Capture("unity-live-completed");
    }

    // Recover the already completed turn after a verifier-only failure. Never sends a request.
    public void InspectExistingResult() => StartCoroutine(InspectExisting());
    IEnumerator InspectExisting()
    {
        report = JsonUtility.FromJson<Report>(File.ReadAllText("Logs/NpcJointTurn/unity-live-ui.json"));
        report.error = null;
        try
        {
            var npc = FindObjectsByType<NpcInvestigationController>(FindObjectsSortMode.None)
                .Single(n => (string)Get(n, "npcId") == "rebecca");
            var state = (NpcConversationState)Get(npc, "conversationState");
            var ui = FindFirstObjectByType<InvestigationInteractionUI>();
            var tell = (TellMeterPresenter)Get(ui, "tellMeterPresenter");
            var affect = (AffectPlanePresenter)Get(ui, "affectPlanePresenter");
            var tellGraphic = (TellPulseGraphic)Get(tell, "pulseGraphic");
            var affectGraphic = (AffectPulseGraphic)Get(affect, "pulseGraphic");
            report.reply = state.recentExchanges.Last(x => x.speaker == "npc").text;
            report.transcript = ((TMP_Text)Get(ui, "transcriptText")).text;
            report.tell = state.lastKnownTell; report.interest = state.lastKnownAffect.interest;
            report.attitude = state.lastKnownAffect.attitude; report.patience = state.lastKnownPatience;
            report.tellText = ((TMP_Text)Get(tell, "valueText"))?.text ?? "Graph-only Tell UI";
            Check(state.conversationCount == 1 && !(bool)Get(npc, "isBusy"), "Exactly one real network turn has committed");
            Check(((Canvas)Get(ui, "rootCanvas")).enabled && report.transcript.Contains(report.reply), "Actual generated dialogue is visible in the existing investigation UI");
            Check(Mathf.Abs((float)Get(tell, "currentTell") - report.tell) < .001f &&
                Mathf.Abs((float)Get(tellGraphic, "tellNormalized") - report.tell) < .001f &&
                !(bool)Get(tellGraphic, "pendingNoise"), "Tell presenter and rendered waveform match the returned result, with pending noise cleared");
            Check(Mathf.Abs((float)Get(affect, "currentInterest") - report.interest) < .001f &&
                Mathf.Abs((float)Get(affect, "currentAttitude") - report.attitude) < .001f &&
                Mathf.Abs((float)Get(affectGraphic, "interest") - report.interest) < .001f &&
                Mathf.Abs((float)Get(affectGraphic, "attitude") - report.attitude) < .001f &&
                !(bool)Get(affectGraphic, "pending"), "Affect presenter and rendered graph match the returned coordinates, with pending state cleared");
            Check(((Button)Get(ui, "sendButton")).interactable && ((TMP_InputField)Get(ui, "intentInput")).interactable,
                "The existing question input and Send button are enabled after completion");
            // This is a lower bound: the pending image is captured after sending, and the
            // server audit is written before its HTTP response reaches the client.
            report.elapsedSeconds = (float)(File.GetLastWriteTimeUtc("Logs/NpcJointTurn/unity-live-server.json") -
                File.GetLastWriteTimeUtc("Logs/NpcJointTurn/unity-live-pending.png")).TotalSeconds;
            Check(report.elapsedSeconds >= 1f, "Real server response arrived more than one second after the pending capture (presentation-delay lower bound)");
            report.passed = true;
        }
        catch (Exception error) { report.passed = false; report.error = error.ToString(); }
        yield return new WaitForEndOfFrame();
        Capture("unity-live-completed");
        File.WriteAllText("Logs/NpcJointTurn/unity-live-ui.json", JsonUtility.ToJson(report, true));
        Debug.Log("Rebecca existing-result verification: " + report.passed + " " + report.error);
    }

    void Capture(string name)
    {
        Directory.CreateDirectory("Logs/NpcJointTurn");
        var texture = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes("Logs/NpcJointTurn/" + name + ".png", texture.EncodeToPNG());
        Destroy(texture);
    }
}
#endif
