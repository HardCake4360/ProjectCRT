"""Offline joint-turn gateway tests. No credentials, model or real player files."""
import copy
import importlib.util
import json
from pathlib import Path
import sys
import types
import unittest
from unittest.mock import Mock, patch

try:
    from flask import Flask
except ImportError:
    Flask = None
try:
    import httpx
    from openai import OpenAI
except ImportError:
    OpenAI = None

MODULE = Path(__file__).resolve().parents[1] / "npc_gateway" / "npc_joint_turn.py"
spec = importlib.util.spec_from_file_location("npc_joint_turn", MODULE)
gateway = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gateway)


def payload():
    return {"protocolVersion": gateway.PROTOCOL, "sessionId": "session", "turnId": "turn",
            "npcId": "rebecca", "personaKey": "rebecca", "phase": "FreeInvestigation",
            "interaction": {"actionType": "Talk", "playerIntentText": "제레미는 어떤 직원이야?"},
            "npcLocalState": {"lastKnownAffect": {"interest": .2, "attitude": .1}, "lastKnownPatience": 80},
            "conversationContext": {"recentExchanges": [
                {"speaker": "player", "text": "오랜만이네."}, {"speaker": "npc", "text": "반가워, 산."}]}}


def candidate():
    return {"replyText": "제레미? 성실한 아이야. 여기 일을 많이 도와주고 있어.",
            "dialogueAct": "answer", "interestDelta": .1, "attitudeDelta": .05,
            "patienceDelta": -2, "tell": .3, "expression": "thoughtful", "voiceTone": "warm", "usedClaimIds": []}


class JointTurnTests(unittest.TestCase):
    def setUp(self):
        self.create = Mock(return_value=types.SimpleNamespace(status="completed", output=[], output_text=json.dumps(candidate())))
        self.client = types.SimpleNamespace(responses=types.SimpleNamespace(create=self.create))
        self.service = gateway.JointTurnGateway(lambda: self.client, lambda: "gpt-5.4-nano",
            lambda _: {"identity": {"name": "레베카"}, "personality": {}, "linguistic_style": {}})

    def test_one_call_returns_joint_result_and_no_unlocks(self):
        result = self.service.generate(payload())
        self.create.assert_called_once()
        self.assertEqual(result["reply"]["conversationState"], {"affect": {"interest": .3, "attitude": .15}, "patience": 78})
        self.assertEqual(result["tellResult"]["band"], "Guarded")
        self.assertEqual(result["reply"]["stateDelta"]["unlockTopicIds"], [])
        self.assertEqual(result["reply"]["stateDelta"]["markStatements"][0]["statementId"], "rebecca_turn")
        kwargs = self.create.call_args.kwargs
        self.assertTrue(kwargs["store"])
        self.assertTrue(kwargs["text"]["format"]["strict"])
        self.assertEqual(kwargs["model"], "gpt-5.4-nano")
        self.assertEqual([m["role"] for m in kwargs["input"]], ["developer", "user", "assistant", "user"])
        self.assertEqual(sum(m["content"] == payload()["interaction"]["playerIntentText"] for m in kwargs["input"]), 1)

    def test_other_npcs_rejected_before_provider(self):
        for key in ("npcId", "personaKey"):
            p = payload(); p[key] = "jeremy"
            with self.assertRaises(gateway.TurnError): self.service.generate(p)
        self.create.assert_not_called()

    def test_payload_validation_before_provider(self):
        for key, value in (("sessionId", ""), ("turnId", ""), ("protocolVersion", "old"), ("interaction", "bad")):
            p = payload(); p[key] = value
            with self.assertRaises(gateway.TurnError): self.service.generate(p)
        self.create.assert_not_called()

    def test_cancelled_history_not_loaded_from_server_memory(self):
        # Both requests carry exactly the caller's committed prior turns, with no server append.
        p = payload(); self.service.generate(p)
        p["turnId"] = "next"; self.service.generate(p)
        self.assertEqual(self.create.call_args.kwargs["input"], self.create.call_args_list[0].kwargs["input"])

    def test_attached_refusal_preserves_history_without_hidden_prompt(self):
        p = payload()
        p["interaction"]["attachedItemDirective"] = {"itemId": "secret", "itemDisplayName": "물건",
            "thresholdSatisfied": False, "itemPrompt": "HIDDEN_MURDER_FACT", "followingItemId": "SECRET_UNLOCK",
            "responseDirectivePrompt": "이 주제에 관해 아는 내용을 밝히지 않는다."}
        self.service.generate(p)
        text = json.dumps(self.create.call_args.kwargs["input"], ensure_ascii=False)
        self.assertNotIn("HIDDEN_MURDER_FACT", text)
        self.assertNotIn("SECRET_UNLOCK", text)
        self.assertIn("반가워, 산.", text)

    def test_required_claim_missing_or_unexpected_fails(self):
        p = payload()
        p["interaction"]["attachedItemDirective"] = {"itemId": "approved", "itemDisplayName": "안내문",
            "thresholdSatisfied": True, "responseDirectivePrompt": "영업 안내문의 내용만 설명한다."}
        for used in ([], ["other"], ["approved", "approved"]):
            c = candidate(); c["usedClaimIds"] = used; self.create.return_value.output_text = json.dumps(c)
            with self.assertRaises(gateway.TurnError): self.service.generate(p)
        c["usedClaimIds"] = ["approved"]; self.create.return_value.output_text = json.dumps(c)
        self.assertEqual(self.service.generate(p)["usedClaimIds"], ["approved"])

    def test_failed_or_partial_output_never_succeeds_or_caches(self):
        for status, text in (("incomplete", json.dumps(candidate())), ("failed", ""), ("completed", "{broken")):
            self.create.return_value = types.SimpleNamespace(status=status, output_text=text, output=[])
            with self.assertRaises(gateway.TurnError): self.service.generate(payload())
        self.assertFalse(self.service._cache)
        self.assertFalse(self.service._pending)

    def test_refusal_even_with_other_text_is_not_a_success(self):
        self.create.return_value.output = [types.SimpleNamespace(content=[types.SimpleNamespace(type="refusal")])]
        with self.assertRaises(gateway.TurnError): self.service.generate(payload())

    def test_invalid_tii_never_becomes_stable_default(self):
        for key, value in (("tell", float("nan")), ("tell", 1.1), ("interestDelta", .21), ("attitudeDelta", -.16),
                           ("patienceDelta", -11), ("patienceDelta", 1.5), ("tell", True), ("voiceTone", "invented"),
                           ("replyText", "...")):
            c = candidate(); c[key] = value
            self.create.return_value.output_text = json.dumps(c)
            with self.assertRaises(gateway.TurnError, msg=(key, value)): self.service.generate(payload())

    def test_state_bounds_and_tell_bands(self):
        for tell, band in ((.249, "Stable"), (.25, "Guarded"), (.5, "Shaken"), (.75, "Disturbed"), (1, "Disturbed")):
            p = payload(); p["turnId"] = str(tell)
            p["npcLocalState"] = {"lastKnownAffect": {"interest": 1, "attitude": 1}, "lastKnownPatience": 0}
            c = candidate(); c["tell"] = tell
            self.create.return_value.output_text = json.dumps(c)
            r = self.service.generate(p)
            self.assertEqual(r["reply"]["conversationState"], {"affect": {"interest": 1, "attitude": 1}, "patience": 0})
            self.assertEqual(r["tellResult"]["band"], band)

    def test_same_turn_cached_different_payload_conflicts(self):
        p = payload(); first = self.service.generate(p)
        first["reply"]["replyText"] = "mutated"
        second = self.service.generate(p)
        self.create.assert_called_once()
        self.assertNotEqual(first, second)
        p["interaction"]["playerIntentText"] = "different"
        with self.assertRaises(gateway.TurnError) as error: self.service.generate(p)
        self.assertEqual(error.exception.status, 409)

    def test_in_flight_duplicate_does_not_call_provider_twice(self):
        def nested(**kwargs):
            with self.assertRaises(gateway.TurnError) as error: self.service.generate(payload())
            self.assertEqual(error.exception.status, 409)
            return types.SimpleNamespace(status="completed", output=[], output_text=json.dumps(candidate()))
        self.create.side_effect = nested
        self.service.generate(payload()); self.create.assert_called_once()

    def test_provider_error_is_sanitized_by_route(self):
        routes = {}
        app = types.SimpleNamespace(route=lambda path, **kw: lambda f: routes.setdefault(path, f))
        request = types.SimpleNamespace(content_length=100, get_json=lambda **kw: payload())
        with patch.dict(sys.modules, {"flask": types.SimpleNamespace(jsonify=lambda x: x, request=request)}):
            gateway.register_joint_turn_routes(app, lambda: self.client, lambda: "gpt-5.4-nano", lambda _: {})
            self.create.side_effect = RuntimeError("DO_NOT_LEAK_CREDENTIAL_OR_PROMPT")
            result, status = routes["/investigation/npc/turn"]()
            self.assertEqual(status, 502)
            self.assertFalse(result["ok"])
            self.assertNotIn("DO_NOT_LEAK", result["error"])
            request.content_length = 70000
            self.assertEqual(routes["/investigation/npc/turn"]()[1], 413)


@unittest.skipIf(Flask is None, "Flask is not installed in this interpreter")
class FlaskRouteTests(unittest.TestCase):
    def setUp(self):
        self.create = Mock(return_value=types.SimpleNamespace(status="completed", output=[], output_text=json.dumps(candidate())))
        client = types.SimpleNamespace(responses=types.SimpleNamespace(create=self.create))
        app = Flask("joint_turn_offline_test")
        gateway.register_joint_turn_routes(app, lambda: client, lambda: "test-model", lambda _: {})
        self.transport = app.test_client()

    def test_actual_flask_route_returns_joint_json_and_replays_cached_turn(self):
        first = self.transport.post("/investigation/npc/turn", json=payload())
        self.assertEqual(first.status_code, 200)
        self.assertEqual(first.get_json()["protocolVersion"], gateway.PROTOCOL)
        self.assertEqual(first.get_json()["tellResult"]["tell"], .3)
        second = self.transport.post("/investigation/npc/turn", json=payload())
        self.assertEqual(first.get_json(), second.get_json())
        self.create.assert_called_once()

    def test_actual_flask_rejects_invalid_requests_and_provider_failure(self):
        self.assertEqual(self.transport.post("/investigation/npc/turn", data="{bad", content_type="application/json").status_code, 400)
        p = payload(); p["npcId"] = "jeremy"
        self.assertEqual(self.transport.post("/investigation/npc/turn", json=p).status_code, 400)
        p = payload(); p["padding"] = "x" * 70000
        self.assertEqual(self.transport.post("/investigation/npc/turn", json=p).status_code, 413)
        self.create.assert_not_called()
        self.create.side_effect = RuntimeError("secret provider details")
        response = self.transport.post("/investigation/npc/turn", json=payload())
        self.assertEqual(response.status_code, 502)
        self.assertNotIn("secret provider", response.get_data(as_text=True))


@unittest.skipIf(OpenAI is None, "OpenAI SDK is not installed in this interpreter")
class OpenAISdkContractTests(unittest.TestCase):
    def test_sdk_serialization_and_output_text_with_no_network(self):
        requests = []

        def respond(request):
            requests.append(json.loads(request.content))
            return httpx.Response(200, json={
                "id": "resp_offline", "object": "response", "created_at": 0,
                "status": "completed", "model": "gpt-5.4-nano",
                "output": [{"id": "msg_offline", "type": "message", "status": "completed", "role": "assistant",
                            "content": [{"type": "output_text", "text": json.dumps(candidate()), "annotations": []}]}],
            })

        # MockTransport has no network path; this never loads or transmits the user's key.
        with httpx.Client(transport=httpx.MockTransport(respond)) as http_client:
            client = OpenAI(api_key="offline-fixture", http_client=http_client, max_retries=0)
            service = gateway.JointTurnGateway(lambda: client, lambda: "gpt-5.4-nano", lambda _: {})
            result = service.generate(payload())
        self.assertTrue(result["ok"])
        self.assertEqual(len(requests), 1)
        self.assertEqual(requests[0]["text"]["format"]["type"], "json_schema")
        self.assertTrue(requests[0]["store"])


if __name__ == "__main__":
    unittest.main()
