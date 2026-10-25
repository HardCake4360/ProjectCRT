"""Exercise the live-test budget and trace logging using an in-memory HTTP transport only."""
import importlib.util
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import unittest
from unittest.mock import patch

import httpx

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("smoke_npc_joint_turn", Path(__file__).with_name("smoke_npc_joint_turn.py"))
smoke = importlib.util.module_from_spec(spec)
spec.loader.exec_module(smoke)


class SmokeAuditTests(unittest.TestCase):
    def setUp(self):
        test_root = (ROOT / "Logs/NpcJointTurn/offline-tests").resolve()
        test_root.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=test_root)
        if not Path(self.temp.name).resolve().is_relative_to(test_root):
            raise RuntimeError("Temporary test directory escaped its workspace root.")
        self.addCleanup(self.temp.cleanup)
        self.app = Path(self.temp.name) / "app"
        self.app.mkdir()
        (self.app / "data/personas").mkdir(parents=True)
        (self.app / "data/personas/rebecca.json").write_text("{}", encoding="utf-8")
        (self.app / "apiKey.env").write_text("OPENAI_API_KEY=sk-offline-fixture\nOPENAI_PROJECT_ID=proj_fixture\n", encoding="utf-8")
        (self.app / "server_test.py").write_text('OPENAI_MODEL="gpt-5.4-nano"\nOPENAI_REASONING_EFFORT="none"\n', encoding="utf-8")
        shutil.copy2(ROOT / "tools/npc_gateway/npc_joint_turn.py", self.app / "npc_joint_turn.py")
        self.report = Path(self.temp.name) / "audit.json"
        self.report.write_text(json.dumps({"passed": False, "attempts": [{"status": "failed"}] * 2,
            "turns": [], "sessionId": "offline_budget_check"}), encoding="utf-8")
        self.requests = []

    def run_smoke(self, live):
        real_client = httpx.Client

        def respond(request):
            self.requests.append(request)
            return httpx.Response(429, headers={"x-request-id": "req_offline", "openai-project": "proj_fixture"},
                json={"error": {"message": "offline quota fixture", "type": "insufficient_quota", "code": "credit_balance_exhausted"}})

        class OfflineClient(real_client):
            def __init__(self, **kwargs):
                super().__init__(transport=httpx.MockTransport(respond), **kwargs)

        argv = ["smoke", "--app", str(self.app), "--report", str(self.report),
                "--live" if live else "--check-config", "--max-new-attempts", "1"]
        with patch.dict(os.environ, {}, clear=True), patch.object(sys, "argv", argv), patch.object(httpx, "Client", OfflineClient):
            return smoke.main()

    def test_configuration_check_never_spends_remaining_attempt(self):
        self.assertEqual(self.run_smoke(False), 0)
        audit = json.loads(self.report.read_text(encoding="utf-8"))
        self.assertEqual(audit["remainingApprovedAttempts"], 1)
        self.assertEqual(audit["configuration"]["configuredProject"], "proj_fixture")
        self.assertEqual(self.requests, [])

    def test_last_attempt_keeps_trace_and_prevents_retry_or_fourth_request(self):
        self.assertEqual(self.run_smoke(True), 1)
        self.assertEqual(len(self.requests), 1)
        self.assertTrue(json.loads(self.requests[0].content)["store"])
        self.assertEqual(self.requests[0].headers["OpenAI-Project"], "proj_fixture")
        audit = json.loads(self.report.read_text(encoding="utf-8"))
        self.assertEqual(audit["remainingApprovedAttempts"], 0)
        provider = audit["attempts"][-1]["provider"]
        self.assertEqual(provider["requestId"], "req_offline")
        self.assertEqual(provider["responseProject"], "proj_fixture")
        self.assertEqual(provider["code"], "credit_balance_exhausted")
        self.assertNotIn("sk-offline-fixture", self.report.read_text(encoding="utf-8"))
        self.assertEqual(self.run_smoke(True), 2)
        self.assertEqual(len(self.requests), 1)


if __name__ == "__main__":
    unittest.main()
