"""Run the existing server for a local Unity verification, with safe request tracing.

Skips the interactive model selector and its extra billed startup probe. The
existing server routes, persona loader, index initialization and OpenAI client
remain in use. No API credentials or request conversation history are logged.
"""
import argparse
from datetime import datetime, timezone
import importlib
import inspect
import json
import os
from pathlib import Path
import sys
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--app", required=True)
    parser.add_argument("--report", required=True)
    args = parser.parse_args()
    app_path = Path(args.app).resolve(strict=True)
    sys.path.insert(0, str(app_path))
    server = importlib.import_module("server_test")
    server.LLM_PROVIDER = "openai"
    report = Path(args.report).resolve()
    audit = {"pid": os.getpid(), "entry": str(app_path / "server_test.py"),
             "startedAtUtc": datetime.now(timezone.utc).isoformat(), "providerCalls": [], "turns": []}

    def save():
        report.parent.mkdir(parents=True, exist_ok=True)
        report.write_text(json.dumps(audit, ensure_ascii=False, indent=2), encoding="utf-8")

    # The registered route resolves this global at request time through its hook.
    original_client = server._require_openai_client

    def tracked_client():
        client = original_client()
        create = client.responses.create

        def tracked_create(**kwargs):
            item = {"model": kwargs.get("model"), "store": kwargs.get("store"), "status": "started"}
            audit["providerCalls"].append(item)
            save()
            started = time.monotonic()
            try:
                response = create(**kwargs)
                usage = getattr(response, "usage", None)
                item.update(status=response.status, responseId=response.id, requestId=getattr(response, "_request_id", None),
                            inputTokens=getattr(usage, "input_tokens", None), outputTokens=getattr(usage, "output_tokens", None))
                return response
            except Exception as error:
                item.update(status="failed", exception=type(error).__name__, httpStatus=getattr(error, "status_code", None))
                raise
            finally:
                item["seconds"] = round(time.monotonic() - started, 3)
                save()
        client.responses.create = tracked_create
        return client

    server._require_openai_client = tracked_client
    # Registration captured the original callable before this wrapper was installed.
    route = server.app.view_functions["investigation_npc_joint_turn"]
    inspect.getclosurevars(route).nonlocals["gateway"].get_client = tracked_client
    from flask import jsonify, request

    @server.app.route("/__verification/health")
    def health():
        return jsonify(ok=True, pid=os.getpid(), provider=server.LLM_PROVIDER, model=server.OPENAI_MODEL)

    @server.app.after_request
    def record_turn(response):
        if request.path == "/investigation/npc/turn":
            audit["turns"].append({"httpStatus": response.status_code, "result": response.get_json(silent=True)})
            save()
        return response

    server.init_index()
    save()
    print("[UNITY VERIFICATION] Existing gateway ready on 127.0.0.1:5000; no startup API probe.", flush=True)
    from waitress import serve
    serve(server.app, host="127.0.0.1", port=5000)


if __name__ == "__main__":
    main()
