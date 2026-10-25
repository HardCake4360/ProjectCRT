#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only fixtures use synthetic replies and disable all personal save writes.
public sealed class InvestigationProbeClient : NpcInvestigationClient
{
    public bool HoldReply, HoldTell;
    public bool HoldJoint, FailJoint, InvalidJoint;
    public int JointCalls, ReplyCalls, TellCalls;
    public NpcInvestigationRequest JointRequest;
    public Action<NpcJointTurnResponse> JointCallback;
    public string Text = "FAST_REPLY_한글";
    public NpcInvestigationRequest LastRequest;
    public Action<NpcInvestigationReplyResponse> ReplyCallback;
    public Action<ConversationStatePayload> StateCallback;
    public Action<TellResultPayload> TellCallback;
    public override IEnumerator SendReplyRequest(NpcInvestigationRequest request, Action started, Action<string> delta,
        Action<ConversationStatePayload> state, Action<NpcInvestigationReplyResponse> success, Action<string> error)
    {
        ReplyCalls++;
        LastRequest = request; ReplyCallback = success; StateCallback = state;
        if (HoldReply) { while (true) yield return null; }
        success(new NpcInvestigationReplyResponse { turnId = request.turnId, replyText = Text });
    }
    public override IEnumerator SendTellRequest(NpcTellRequest request, Action<TellResultPayload> success, Action<string> error)
    {
        TellCalls++;
        TellCallback = success;
        if (HoldTell) { while (true) yield return null; }
        success(new TellResultPayload { turnId = request.turnId, tell = 0.2f });
    }

    public static NpcJointTurnResponse JointResult(NpcInvestigationRequest request) => new()
    {
        ok = true, protocolVersion = NpcJointTurnResponse.Protocol, sessionId = request.sessionId, turnId = request.turnId,
        reply = new NpcInvestigationReplyResponse
        {
            turnId = request.turnId, replyText = "JOINT_REPLY_레베카",
            conversationState = new ConversationStatePayload
            {
                affect = new InterrogationAffectPayload
                {
                    interest = Mathf.Clamp(request.npcLocalState.lastKnownAffect.interest + .1f, -1f, 1f),
                    attitude = Mathf.Clamp(request.npcLocalState.lastKnownAffect.attitude + .05f, -1f, 1f)
                },
                patience = Mathf.Max(0, request.npcLocalState.lastKnownPatience - 2)
            }
        },
        tellResult = new TellResultPayload { turnId = request.turnId, tell = .6f, band = "Shaken" },
        usedClaimIds = new List<string>()
    };

    public override IEnumerator SendJointTurnRequest(NpcInvestigationRequest request,
        Action<NpcJointTurnResponse> success, Action<string> error)
    {
        JointCalls++; JointRequest = request; JointCallback = success;
        if (HoldJoint) { while (true) yield return null; }
        if (FailJoint) { error("Joint generation unavailable"); yield break; }
        var response = JointResult(request);
        if (InvalidJoint) response.tellResult = null;
        success(response);
    }
}

public sealed class InvestigationHardeningProbe : MonoBehaviour
{
    public bool Done;
    public readonly List<string> Passed = new();
    public string Failure;
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
    private void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        Passed.Add(name);
    }
    private string Transcript => Get<TMP_Text>(InvestigationInteractionUI.Instance, "transcriptText").text;
    private bool Busy => Get<bool>(InvestigationInteractionUI.Instance, "isBusy");
    private NpcInvestigationController CreateNpc(out InvestigationProbeClient client)
    {
        var root = new GameObject("NPC verification"); root.SetActive(false);
        client = root.AddComponent<InvestigationProbeClient>();
        var context = root.AddComponent<InvestigationContextBuilder>();
        root.AddComponent<NpcProfileComponent>();
        var controller = root.AddComponent<NpcInvestigationController>();
        Set(controller, "npcId", "verification_" + Guid.NewGuid().ToString("N"));
        Set(controller, "saveConversationHistory", false);
        Set(controller, "apiClient", client); Set(controller, "contextBuilder", context);
        Set(controller, "minimumReplyDelaySeconds", 1f); Set(controller, "tellGracePeriodSeconds", 0.05f);
        root.SetActive(true);
        return controller;
    }
    private void Ask(NpcInvestigationController npc, string question, List<string> information = null) =>
        typeof(NpcInvestigationController).GetMethod("HandleActionRequested", Fields).Invoke(npc,
            new object[] { new InvestigationInteractionPayload { actionType = "Talk", playerIntentText = question,
                informationIds = information ?? new List<string>() } });

    private IEnumerator Start()
    {
        IEnumerator checks = Run();
        while (true)
        {
            bool advanced; object next;
            try { advanced = checks.MoveNext(); next = advanced ? checks.Current : null; }
            catch (Exception error) { Failure = error.ToString(); break; }
            if (!advanced) break;
            yield return next;
        }
        Done = true;
    }

    private IEnumerator Run()
    {
        var inventory = InvestigationInventoryManager.GetOrCreateInstance();
        Set(inventory, "saveOnUnlock", false);
        ValidateProtocol();
        var db = Resources.Load<InvestigationItemDatabase>("Investigation/InvestigationItemDatabase");
        Check(db.TryGetDefinition("test", out _), "Following item resolves from database");

        var a = CreateNpc(out var clientA);
        yield return null;
        a.BeginInteraction();
        Ask(a, "unique first question");
        Check(!clientA.LastRequest.conversationContext.recentExchanges.Exists(x => x.speaker == "player" && x.text == "unique first question"),
            "Current question is excluded from prior context");
        yield return new WaitForSecondsRealtime(0.2f);
        Check(Busy && !Transcript.Contains("FAST_REPLY_한글"), "Minimum reply delay is preserved");
        yield return new WaitForSecondsRealtime(1.05f);
        Check(!Busy && Transcript.Contains("FAST_REPLY_한글"), "Fast reply displays after minimum delay");

        Ask(a, "unique first question");
        Check(clientA.LastRequest.conversationContext.recentExchanges.Exists(x => x.speaker == "player" && x.text == "unique first question"),
            "Actual prior repeated question remains available to server");
        a.CloseInteraction(); a.BeginInteraction();

        clientA.HoldTell = true;
        Ask(a, "reply with stalled tell");
        yield return new WaitForSecondsRealtime(1.15f);
        Check(!Busy, "Stalled tell does not block the next question");
        clientA.HoldTell = false;

        clientA.HoldReply = true;
        typeof(NpcInvestigationClient).GetField("requestTimeoutSeconds", Fields).SetValue(clientA, 1);
        Ask(a, "stalled reply");
        yield return new WaitForSecondsRealtime(6.2f);
        Check(!Busy, "Controller deadline recovers from a stalled reply");
        clientA.HoldReply = false;
        typeof(NpcInvestigationClient).GetField("requestTimeoutSeconds", Fields).SetValue(clientA, 90);

        var track = typeof(NpcInvestigationClient).GetMethod("TrackRequest", Fields);
        track.Invoke(clientA, new object[] { "cancel_probe", new UnityEngine.Networking.UnityWebRequest("http://127.0.0.1:1/") });
        clientA.CancelTurn("cancel_probe"); clientA.CancelTurn("cancel_probe");
        var registrations = (System.Collections.IDictionary)typeof(NpcInvestigationClient).GetField("activeRequests", Fields).GetValue(clientA);
        Check(registrations.Count == 0, "Cancelled native requests are removed and disposal is idempotent");

        // Use a temporary in-memory definition so failure cannot unlock personal content.
        var nextItem = ScriptableObject.CreateInstance<InvestigationItemDefinition>();
        Set(nextItem, "id", "verification_following"); Set(nextItem, "category", InvestigationItemCategory.Topic);
        var item = ScriptableObject.CreateInstance<InvestigationItemDefinition>();
        Set(item, "id", "verification_information"); Set(item, "category", InvestigationItemCategory.Information);
        Set(item, "prompt", "Test prompt"); Set(item, "attitudeThreshold", -1f); Set(item, "followingItem", nextItem);
        var temporaryDb = ScriptableObject.CreateInstance<InvestigationItemDatabase>();
        Set(temporaryDb, "items", new List<InvestigationItemDefinition> { item, nextItem });
        Set(inventory, "database", temporaryDb);
        int npcLines = Get<NpcConversationState>(a, "conversationState").recentExchanges.FindAll(x => x.speaker == "npc").Count;
        clientA.Text = "[ERROR] simulated failure";
        Ask(a, "failure", new List<string> { item.Id });
        yield return new WaitForSecondsRealtime(0.1f);
        Check(!Busy && !inventory.GetUnlockedIds(InvestigationItemCategory.Topic).Contains(nextItem.Id), "Failed reply never unlocks following item");
        Check(Get<NpcConversationState>(a, "conversationState").recentExchanges.FindAll(x => x.speaker == "npc").Count == npcLines,
            "Failed reply is not recorded as NPC speech");
        Set(inventory, "database", db);

        clientA.HoldReply = clientA.HoldTell = true;
        Ask(a, "pending A");
        var oldReply = clientA.ReplyCallback; var oldState = clientA.StateCallback; var oldTell = clientA.TellCallback;
        string oldTurn = clientA.LastRequest.turnId;
        a.CloseInteraction();
        var b = CreateNpc(out _); yield return null; b.BeginInteraction();
        string before = Transcript;
        oldReply(new NpcInvestigationReplyResponse { turnId = oldTurn, replyText = "STALE_REPLY" });
        oldState(new ConversationStatePayload { patience = 0 });
        oldTell(new TellResultPayload { turnId = oldTurn, tell = 1 });
        Check(Transcript == before && !Busy, "Late callbacks cannot alter another NPC view");
        b.CloseInteraction();

        var scene = SceneManager.CreateScene("NPC verification scene");
        SceneManager.MoveGameObjectToScene(a.gameObject, scene);
        a.BeginInteraction(); Ask(a, "pending before unload");
        oldReply = clientA.ReplyCallback; oldTurn = clientA.LastRequest.turnId;
        yield return SceneManager.UnloadSceneAsync(scene);
        b.BeginInteraction();
        before = Transcript;
        oldReply(new NpcInvestigationReplyResponse { turnId = oldTurn, replyText = "UNLOADED_REPLY" });
        Check(!Busy && before == Transcript, "Scene unload cancels request and restores shared UI");
        b.CloseInteraction();

        var terminal = new GameObject("Terminal verification").AddComponent<Interpreter>();
        var text = new GameObject("Output").AddComponent<TextMeshProUGUI>();
        bool completed = false;
        IEnumerator command = terminal.Interpret("/set_persona", text, () => completed = true);
        while (command.MoveNext()) yield return command.Current;
        Check(completed && text.text.Contains("/set_persona"), "Missing terminal argument restores input completion");
        Destroy(terminal.gameObject); Destroy(text.gameObject); Destroy(b.gameObject);

        var jointChecks = VerifyJointTurns();
        try { while (jointChecks.MoveNext()) yield return jointChecks.Current; }
        finally { (jointChecks as IDisposable)?.Dispose(); }
    }

    private IEnumerator VerifyJointTurns()
    {
        var npc = CreateNpc(out var client);
        Set(npc, "npcId", "rebecca"); Set(npc, "personaKey", "rebecca");
        yield return null;
        npc.BeginInteraction();
        var state = Get<NpcConversationState>(npc, "conversationState");
        int exchanges = state.recentExchanges.Count, count = state.conversationCount;
        float initialTell = state.lastKnownTell;
        float initialInterest = state.lastKnownAffect.interest;
        Ask(npc, "joint unique question");
        Check(client.JointCalls == 1 && client.ReplyCalls == 0 && client.TellCalls == 0,
            "Rebecca sends one joint request and no legacy reply/tell");
        Check(!client.JointRequest.conversationContext.recentExchanges.Exists(x => x.text == "joint unique question"),
            "Joint request contains committed prior history only");
        yield return new WaitForSecondsRealtime(.2f);
        Check(Busy && !Transcript.Contains("JOINT_REPLY_레베카") &&
              state.lastKnownTell == initialTell && state.lastKnownAffect.interest == initialInterest && state.recentExchanges.Count == exchanges,
            "Joint dialogue and TII wait for the same one-second gate without committing early");
        yield return new WaitForSecondsRealtime(1.05f);
        Check(!Busy && Transcript.Contains("JOINT_REPLY_레베카") && state.lastKnownTell == .6f &&
              Mathf.Abs(state.lastKnownAffect.interest - initialInterest - .1f) < .001f &&
              state.recentExchanges.Count == exchanges + 2 && state.conversationCount == count + 1,
            "Joint dialogue, TII and history commit together after delay");

        var valid = InvestigationProbeClient.JointResult(client.JointRequest);
        Check(NpcInvestigationClient.TryValidateJointTurnResponse(valid, client.JointRequest, out _), "Valid joint contract accepted");
        valid.sessionId = "wrong session";
        Check(!NpcInvestigationClient.TryValidateJointTurnResponse(valid, client.JointRequest, out _), "Cross-session joint result rejected");
        valid = InvestigationProbeClient.JointResult(client.JointRequest); valid.reply.conversationState.affect.attitude = float.NaN;
        Check(!NpcInvestigationClient.TryValidateJointTurnResponse(valid, client.JointRequest, out _), "Non-finite joint affect rejected");
        valid = InvestigationProbeClient.JointResult(client.JointRequest); valid.tellResult.band = "Stable";
        Check(!NpcInvestigationClient.TryValidateJointTurnResponse(valid, client.JointRequest, out _), "Inconsistent joint tell band rejected");
        valid = InvestigationProbeClient.JointResult(client.JointRequest); valid.usedClaimIds.Add("unauthorized");
        Check(!NpcInvestigationClient.TryValidateJointTurnResponse(valid, client.JointRequest, out _), "Unapproved joint claim rejected");

        exchanges = state.recentExchanges.Count; count = state.conversationCount;
        float tell = state.lastKnownTell, attitude = state.lastKnownAffect.attitude;
        client.InvalidJoint = true;
        Ask(npc, "invalid joint tell");
        yield return new WaitForSecondsRealtime(.15f);
        Check(!Busy && state.recentExchanges.Count == exchanges && state.conversationCount == count &&
              state.lastKnownTell == tell && state.lastKnownAffect.attitude == attitude,
            "Malformed joint TII rejects dialogue and preserves all prior state");
        client.InvalidJoint = false; client.FailJoint = true;
        Ask(npc, "failed joint generation");
        yield return new WaitForSecondsRealtime(.15f);
        Check(!Busy && state.recentExchanges.Count == exchanges && state.lastKnownTell == tell,
            "Joint provider failure restores UI without a fake Stable reaction");

        client.FailJoint = false;
        Ask(npc, "cancel before presentation");
        var staleRequest = client.JointRequest; var stale = client.JointCallback;
        npc.CloseInteraction();
        yield return new WaitForSecondsRealtime(1.05f);
        Check(state.recentExchanges.Count == exchanges && state.conversationCount == count,
            "Closing before minimum delay discards a successful joint candidate");
        npc.BeginInteraction(); client.HoldJoint = true;
        Ask(npc, "new pending joint question");
        string before = Transcript;
        stale(InvestigationProbeClient.JointResult(staleRequest));
        Check(Transcript == before && state.lastKnownTell == tell && Busy,
            "Late joint response cannot mutate a subsequent turn");
        npc.CloseInteraction();

        npc.BeginInteraction();
        typeof(NpcInvestigationClient).GetField("requestTimeoutSeconds", Fields).SetValue(client, 1);
        Ask(npc, "joint timeout");
        yield return new WaitForSecondsRealtime(6.2f);
        Check(!Busy && state.recentExchanges.Count == exchanges && state.lastKnownTell == tell,
            "Joint timeout recovers input without committing state");
        npc.CloseInteraction();

        // A non-Rebecca identity keeps the established transport even with Rebecca's persona string.
        Check(!client.UsesJointTurn("jeremy", "rebecca") && !client.UsesJointTurn("rebecca", "jeremy"),
            "Other NPCs retain the legacy transport");
        Destroy(npc.gameObject);
    }

    private void ValidateProtocol()
    {
        foreach (string bad in new[] { "", "...", "…", "[ERROR] offline", "[SERVER ERROR] offline" })
            Check(!NpcInvestigationClient.TryValidateReplyResponse(new NpcInvestigationReplyResponse { turnId = "turn", replyText = bad }, "turn", out _),
                "Invalid reply rejected: " + bad);
        Check(!NpcInvestigationClient.TryValidateReplyResponse(new NpcInvestigationReplyResponse { turnId = "old", replyText = "reply" }, "turn", out _), "Wrong turn rejected");
        Check(!NpcInvestigationClient.TryValidateTellResult(new TellResultPayload { turnId = "turn", tell = float.NaN }, "turn", out _), "Invalid tell rejected");

        var handlerType = typeof(NpcInvestigationClient).GetNestedType("ReplyStreamDownloadHandler", BindingFlags.NonPublic);
        string wire = "{\"type\":\"delta\",\"text\":\"레베카의 답변😀\"}\n";
        byte[] bytes = Encoding.UTF8.GetBytes(wire);
        for (int split = 1; split < bytes.Length; split++)
        {
            using var handler = (UnityEngine.Networking.DownloadHandler)Activator.CreateInstance(handlerType, true);
            var receive = handlerType.GetMethod("ReceiveData", Fields);
            byte[] first = new byte[split], second = new byte[bytes.Length - split];
            Array.Copy(bytes, first, split); Array.Copy(bytes, split, second, 0, second.Length);
            receive.Invoke(handler, new object[] { first, first.Length }); receive.Invoke(handler, new object[] { second, second.Length });
            handlerType.GetMethod("CompleteContent", Fields).Invoke(handler, null);
            if ((string)handlerType.GetMethod("GetResponseText").Invoke(handler, null) != wire) throw new Exception("UTF-8 boundary " + split);
        }
        Check(true, "All UTF-8 packet boundaries preserve Korean and emoji");

        string plain = "레베카의 답변😀\n";
        bytes = Encoding.UTF8.GetBytes(plain);
        for (int split = 1; split < bytes.Length; split++)
        {
            string received = "";
            using var handler = new RAGHandler.StreamingHandler(chunk => received += chunk);
            var type = typeof(RAGHandler.StreamingHandler);
            var receive = type.GetMethod("ReceiveData", Fields);
            byte[] first = new byte[split], second = new byte[bytes.Length - split];
            Array.Copy(bytes, first, split); Array.Copy(bytes, split, second, 0, second.Length);
            receive.Invoke(handler, new object[] { first, first.Length }); receive.Invoke(handler, new object[] { second, second.Length });
            type.GetMethod("CompleteContent", Fields).Invoke(handler, null);
            if (received != plain.TrimEnd('\n')) throw new Exception("Terminal UTF-8 boundary " + split);
        }
        Check(true, "Terminal UTF-8 decoding preserves every packet boundary");
    }
}
#endif
