"""Offline regression checks; no model, credentials or real conversation data are used."""
import argparse
import ast
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import re
import sys
import tempfile
import time
import types
import typing
import unittest
from unittest.mock import Mock, patch

parser = argparse.ArgumentParser()
parser.add_argument("--app", required=True)
args, unittest_args = parser.parse_known_args()
APP = Path(args.app).resolve()
REPO = Path(__file__).resolve().parents[2]


def module(name):
    spec = importlib.util.spec_from_file_location(name, APP / (name + ".py"))
    result = importlib.util.module_from_spec(spec)
    with patch.dict(sys.modules, {"requests": types.SimpleNamespace()}):
        spec.loader.exec_module(result)
    return result


def functions(filename, names, namespace):
    tree = ast.parse((APP / filename).read_text(encoding="utf-8-sig"))
    nodes = [node for node in tree.body if isinstance(node, ast.FunctionDef) and node.name in names]
    for node in nodes:
        node.decorator_list = []
    exec(compile(ast.Module(body=nodes, type_ignores=[]), filename, "exec"), namespace)


class ServerHardeningTests(unittest.TestCase):
    def setUp(self):
        self.llm = module("llmClient")

    def stream(self, records):
        response = Mock()
        response.__enter__ = Mock(return_value=response)
        response.__exit__ = Mock(return_value=False)
        response.iter_lines.return_value = [json.dumps(item, ensure_ascii=False).encode() for item in records]
        post = Mock(return_value=response)
        self.llm.requests = types.SimpleNamespace(post=post)
        return post

    def test_connection_failure_raises(self):
        self.llm.requests = types.SimpleNamespace(post=Mock(side_effect=ConnectionError("offline")))
        with self.assertRaises(ConnectionError):
            list(self.llm.query_ollama_stream("question"))

    def test_error_and_truncated_streams_fail(self):
        for records in ([{"error": "model missing"}], [{"response": "partial"}], [{"response": "broken"}, {"error": "failed"}]):
            self.stream(records)
            with self.assertRaises(RuntimeError):
                list(self.llm.query_ollama_stream("question"))

    def test_valid_unicode_and_request_timeout(self):
        post = self.stream([{"response": "레베카"}, {"response": "의 답변", "done": True}])
        self.assertEqual("".join(self.llm.query_ollama_stream("question")), "레베카의 답변")
        self.assertEqual(post.call_args.kwargs["timeout"], (5, 75))

    def route(self, filename, llm_stream):
        payload = {"turnId": "turn", "playerId": "test", "npcId": "rebecca", "personaKey": "rebecca",
                   "interaction": {"actionType": "Talk", "playerIntentText": "question"}}
        noop = lambda *a, **kw: None
        state = {"affect": {"interest": 0, "attitude": 0}, "patience": 100}
        namespace = dict(vars(typing), json=json, re=re, time=time)
        namespace.update({"request": types.SimpleNamespace(get_json=lambda: payload),
            "load_persona": lambda _: {"identity": {"name": "레베카"}},
            "Response": lambda body, **kw: body, "stream_with_context": lambda body: body, "jsonify": lambda body: body,
            "retriever": types.SimpleNamespace(search=lambda _: []), "_append_scoped_log": noop,
            "_read_scoped_summary": lambda *a: None, "_derive_conversation_state": lambda *a: state,
            "_build_reply_prompt": lambda *a: "prompt", "_build_reply_prompt_general": lambda *a: "prompt",
            "_normalize_attached_item_directive": lambda *a: {}, "_build_reply_prompt_attached": lambda *a: "prompt",
            "_build_statement_id": lambda *a: "statement", "_query_active_llm_stream": llm_stream,
            "_extract_unlock_topics": lambda *a: [], "_conversation_state_to_presentation_hints": lambda *a: {},
            "threading": types.SimpleNamespace(Thread=lambda **kw: types.SimpleNamespace(start=noop))})
        functions(filename, {"_safe_reply_text", "_validated_reply_text", "_reply_stream_chunk", "investigation_npc_reply"}, namespace)
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            return [json.loads(line) for line in namespace["investigation_npc_reply"]()]

    def test_routes_never_complete_failed_or_empty_reply(self):
        for filename in ("server.py", "server_test.py", "server_for_experiment.py"):
            for text in ("", "...", "[ERROR] connection refused"):
                chunks = self.route(filename, lambda _, text=text: iter([text]))
                self.assertEqual(chunks[-1]["type"], "error", (filename, text))
                self.assertNotIn("complete", [chunk["type"] for chunk in chunks])

    def test_routes_complete_valid_reply(self):
        for filename in ("server.py", "server_test.py", "server_for_experiment.py"):
            chunks = self.route(filename, lambda _: iter(["정상 답변"]))
            complete = chunks[-1]
            self.assertEqual(complete["type"], "complete")
            self.assertEqual(complete["response"]["turnId"], "turn")

    def test_memory_rejects_stale_summary_and_reset_resurrection(self):
        memory_type = module("conversation_memory").ConversationMemory
        with tempfile.TemporaryDirectory(dir=REPO / "Logs") as directory:
            memory = memory_type(directory)
            memory.append("player", "rebecca", "player", "first")
            old_revision, old_history = memory.snapshot("player", "rebecca")
            self.assertEqual(len(old_history), 1)
            memory.append("player", "rebecca", "player", "second")
            revision, _ = memory.snapshot("player", "rebecca")
            self.assertTrue(memory.write_summary("player", "rebecca", {"summary": "new"}, revision))
            self.assertFalse(memory.write_summary("player", "rebecca", {"summary": "old"}, old_revision))
            self.assertEqual(memory.read_summary("player", "rebecca")["summary"], "new")
            memory.append("player", "jeremy", "npc", "preserve")
            memory.reset("player", "rebecca")
            self.assertFalse(memory.write_summary("player", "rebecca", {"summary": "resurrect"}, revision))
            self.assertIsNone(memory.read_summary("player", "rebecca"))
            self.assertEqual(memory.read("player", "rebecca"), [])
            self.assertEqual(len(memory.read("player", "jeremy")), 1)

    def test_repetition_penalty_only_for_prior_questions(self):
        names = {"_clamp", "_clamp_signed", "_to_float", "_to_int", "_safe_reply_text", "_default_profile",
                 "_default_conversation_state", "_normalize_conversation_state", "_normalize_affect", "_normalize_profile",
                 "_find_action_weight", "_apply_keyword_rules", "_apply_topic_rules", "_apply_evidence_rules", "_derive_conversation_state"}
        for filename in ("server.py", "server_test.py"):
            namespace = dict(vars(typing), json=json, re=re)
            functions(filename, names, namespace)
            derive = namespace["_derive_conversation_state"]
            current = {"playerIntentText": "unique question"}
            fresh = derive("Talk", current, {}, {}, {"recentExchanges": []}, {})
            repeated = derive("Talk", current, {}, {}, {"recentExchanges": [{"speaker": "player", "text": "unique question"}]}, {})
            self.assertEqual(fresh["patience"] - repeated["patience"], 6)


if __name__ == "__main__":
    unittest.main(argv=[sys.argv[0]] + unittest_args)
