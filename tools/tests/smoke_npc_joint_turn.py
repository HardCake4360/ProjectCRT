"""Explicit live smoke test of the installed Flask route; writes no game/history data."""
import argparse
import ast
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import time
import uuid


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--app", required=True)
    parser.add_argument("--live", action="store_true")
    parser.add_argument("--check-config", action="store_true", help="Inspect configuration without any API request.")
    parser.add_argument("--report", required=True)
    parser.add_argument("--max-new-attempts", type=int, default=3)
    args = parser.parse_args()
    if not args.live and not args.check_config:
        parser.error("Pass --check-config for offline inspection or --live to use the remaining approved attempt budget.")
    if not 1 <= args.max_new_attempts <= 3:
        parser.error("max-new-attempts must be 1..3")
    report = Path(args.report)
    if report.exists():
        audit = json.loads(report.read_text(encoding="utf-8"))
    else:
        audit = {"passed": False, "attempts": [], "turns": [], "sessionId": "smoke_" + uuid.uuid4().hex}
    if args.live and len(audit["attempts"]) >= 3:
        print("The approved three-attempt budget has been used; no API call made.")
        return 2

    def save_audit():
        audit["remainingApprovedAttempts"] = max(0, 3 - len(audit["attempts"]))
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text(json.dumps(audit, ensure_ascii=False, indent=2), encoding="utf-8")
    app_path = Path(args.app).resolve()
    # Match the existing gateway's env file without printing or storing credentials.
    env_file = app_path / "apiKey.env"
    credential_from_environment = bool(os.environ.get("OPENAI_API_KEY"))
    file_settings = {}
    if env_file.exists():
        for line in env_file.read_text(encoding="utf-8-sig").splitlines():
            stripped = line.strip()
            if not stripped or stripped.startswith("#") or "=" not in stripped:
                continue
            key, value = stripped.split("=", 1)
            key, value = key.strip(), value.strip().strip('"').strip("'")
            if key in ("OPENAI_API_KEY", "OPENAI_PROJECT_ID", "OPENAI_ORG_ID", "OPENAI_BASE_URL"):
                file_settings[key] = value
                os.environ.setdefault(key, value)
    if not os.environ.get("OPENAI_API_KEY"):
        print("OpenAI credentials are not configured; live test not run.")
        return 2
    settings = {}
    tree = ast.parse((app_path / "server_test.py").read_text(encoding="utf-8-sig"))
    for node in tree.body:
        if isinstance(node, ast.Assign) and len(node.targets) == 1 and isinstance(node.targets[0], ast.Name):
            if node.targets[0].id in ("OPENAI_MODEL", "OPENAI_REASONING_EFFORT"):
                settings[node.targets[0].id] = ast.literal_eval(node.value)
    from flask import Flask
    from openai import OpenAI
    import httpx
    spec = importlib.util.spec_from_file_location("npc_joint_turn", app_path / "npc_joint_turn.py")
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    diagnostic = {}

    def label(value):
        return value if isinstance(value, str) and re.fullmatch(r"[A-Za-z0-9_.\[\]-]{1,180}", value) else None

    def capture_headers(response):
        # Allowlist only diagnostic metadata; never log authentication/request headers or raw error bodies.
        diagnostic.update({"providerHttpStatus": response.status_code,
            "requestId": label(response.headers.get("x-request-id")),
            "responseOrganization": label(response.headers.get("openai-organization")),
            "responseProject": label(response.headers.get("openai-project"))})

    client = OpenAI(timeout=75, max_retries=0,
        http_client=httpx.Client(timeout=75, event_hooks={"response": [capture_headers]}))
    original_create = client.responses.create
    configuration = {"credentialSource": "process_environment" if credential_from_environment else str(env_file),
        "keyPresent": bool(client.api_key), "keyMatchesProjectFile": client.api_key == file_settings.get("OPENAI_API_KEY"),
        "keyFormatLooksValid": client.api_key.startswith("sk-") and not any(c.isspace() for c in client.api_key),
        "baseScheme": client.base_url.scheme, "baseHost": client.base_url.host, "basePath": client.base_url.path,
        "configuredOrganization": label(client.organization), "configuredProject": label(client.project),
        "model": settings["OPENAI_MODEL"], "reasoningEffort": settings.get("OPENAI_REASONING_EFFORT", "none"),
        "maxRetries": client.max_retries, "store": True}
    audit["configuration"] = configuration
    save_audit()
    print("Configuration (no key material):", json.dumps(configuration, ensure_ascii=False))
    if args.check_config and not args.live:
        client.close()
        return 0
    if (client.base_url.scheme != "https" or client.base_url.host != "api.openai.com"
            or client.base_url.path.rstrip("/") != "/v1"):
        print("Unexpected API endpoint; no live request made.")
        client.close()
        return 2

    def tracked_create(**kwargs):
        started = time.monotonic()
        diagnostic.clear()
        diagnostic["clientRequestId"] = "crt_smoke_" + uuid.uuid4().hex
        diagnostic["store"] = kwargs.get("store")
        if kwargs.get("store") is not True:
            raise RuntimeError("Live verification requires stored Responses; no API request made.")
        kwargs["extra_headers"] = {"X-Client-Request-Id": diagnostic["clientRequestId"]}
        try:
            result = original_create(**kwargs)
            usage = getattr(result, "usage", None)
            diagnostic.update({"status": getattr(result, "status", None),
                "responseId": label(getattr(result, "id", None)),
                "inputTokens": getattr(usage, "input_tokens", None), "outputTokens": getattr(usage, "output_tokens", None)})
            return result
        except Exception as error:
            body = getattr(error, "body", None)
            body = body if isinstance(body, dict) else {}
            diagnostic.update({"exception": type(error).__name__, "httpStatus": getattr(error, "status_code", None),
                               "code": label(body.get("code")), "param": label(body.get("param"))})
            # Schema diagnostics contain schema keys, unlike authentication errors which can echo a key.
            if type(error).__name__ == "BadRequestError" and str(body.get("param", "")).startswith("text.format"):
                diagnostic["schemaError"] = str(body.get("message", ""))[:700]
            raise
        finally:
            diagnostic["seconds"] = round(time.monotonic() - started, 3)

    client.responses.create = tracked_create
    app = Flask("rebecca_joint_smoke")
    module.register_joint_turn_routes(app, lambda: client, lambda: settings["OPENAI_MODEL"],
        lambda _: json.loads((app_path / "data" / "personas" / "rebecca.json").read_text(encoding="utf-8-sig")),
        lambda: settings.get("OPENAI_REASONING_EFFORT", "none"))
    state = {"lastKnownAffect": {"interest": 0, "attitude": 0}, "lastKnownPatience": 100}
    results = audit["turns"]
    history = []
    for item in results:
        history.extend([{"speaker": "player", "text": item["question"]}, {"speaker": "npc", "text": item["reply"]}])
        state = {"lastKnownAffect": item["tii"]["affect"], "lastKnownPatience": item["tii"]["patience"]}
    audit["model"] = settings["OPENAI_MODEL"]
    session = audit["sessionId"]
    questions = ["오랜만이야, 레베카. 제레미는 여기서 어떤 일을 하고 있어?",
                 "네가 동료를 아낀다는 건 알겠어. 부담 주려는 건 아니니까 편하게 이야기해 줘.",
                 "이 안내문에 적힌 내용을 알려줄래?"]
    with app.test_client() as transport:
        remaining = min(args.max_new_attempts, 3 - len(audit["attempts"]))
        for _ in range(remaining):
            # After an earlier failure, prioritize a valid basic turn and then the attachment contract.
            index = 0 if not results else 2 if len(audit["attempts"]) == 2 else 1
            question = questions[index]
            request = {"protocolVersion": module.PROTOCOL, "sessionId": session, "turnId": uuid.uuid4().hex,
                "npcId": "rebecca", "personaKey": "rebecca", "phase": "FreeInvestigation", "sceneId": "smoke_only",
                "interaction": {"actionType": "Talk", "playerIntentText": question},
                "npcLocalState": state, "conversationContext": {"recentExchanges": history}}
            if index == 2:
                request["interaction"]["attachedItemDirective"] = {"itemId": "smoke_notice", "itemDisplayName": "검증용 안내문",
                    "thresholdSatisfied": True, "responseDirectivePrompt": "이것은 통신 검증용 임시 안내문이며 실제 사건 단서가 아니라고 설명한다."}
            attempt = {"testIndex": index, "status": "started", "startedAtUtc": datetime.now(timezone.utc).isoformat()}
            audit["attempts"].append(attempt)
            save_audit()  # Persist the budget before a network call; restarting cannot silently exceed it.
            diagnostic.clear()
            response = transport.post("/investigation/npc/turn", json=request)
            data = response.get_json()
            attempt.update({"httpStatus": response.status_code, "provider": dict(diagnostic)})
            if response.status_code != 200 or not data.get("ok"):
                attempt["status"] = "failed"
                attempt["error"] = data.get("error", "unknown")
                audit["result"] = "latest_attempt_failed"
                audit["latestAttemptPassed"] = False
                save_audit()
                print("Live turn failed:", response.status_code, data.get("error", "unknown"))
                print("Sanitized diagnostic:", json.dumps(diagnostic, ensure_ascii=False))
                client.close()
                return 1
            attempt["status"] = "passed"
            audit["result"] = "latest_joint_turn_passed"
            audit["latestAttemptPassed"] = True
            reply, tii = data["reply"], data["tellResult"]
            results.append({"question": question, "reply": reply["replyText"], "tii": reply["conversationState"],
                            "tell": tii, "claims": data["usedClaimIds"]})
            state = {"lastKnownAffect": reply["conversationState"]["affect"], "lastKnownPatience": reply["conversationState"]["patience"]}
            history.extend([{"speaker": "player", "text": question}, {"speaker": "npc", "text": reply["replyText"]}])
            save_audit()
    audit["passed"] = bool(results) and all(x["status"] == "passed" for x in audit["attempts"])
    save_audit()
    print("Live Flask route:", len(results), "successful joint turns in", len(audit["attempts"]), "approved attempts; model:", settings["OPENAI_MODEL"])
    client.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
