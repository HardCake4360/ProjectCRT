# -*- coding: utf-8 -*-
from __future__ import annotations

from pathlib import Path


APP_DIR = Path(r"C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app")
SOURCE = APP_DIR / "server.py"
TARGET = APP_DIR / "server_for_experiment.py"


REPLY_TEST_BLOCK = r'''

# ---------------------------------------------------------------------------
# Experiment-only API.
# This block is intentionally self-contained and does not reuse the production
# investigation reply pipeline. It exists only in server_for_experiment.py.
# ---------------------------------------------------------------------------

REPLY_TEST_DIR = os.path.join(DATA_DIR, "reply_test")
REPLY_TEST_HISTORY_DIR = os.path.join(REPLY_TEST_DIR, "histories")
REPLY_TEST_SUMMARY_DIR = os.path.join(REPLY_TEST_DIR, "summaries")
os.makedirs(REPLY_TEST_HISTORY_DIR, exist_ok=True)
os.makedirs(REPLY_TEST_SUMMARY_DIR, exist_ok=True)


def reply_test_safe_key(value: Any, default: str = "default") -> str:
    text = str(value or default).strip() or default
    return re.sub(r"[^A-Za-z0-9_.-]+", "_", text)[:120]


def reply_test_safe_text(value: Any) -> str:
    if value is None:
        return ""
    text = str(value)
    text = text.replace("<|eot_id|>", " ")
    text = text.replace("</s>", " ")
    text = text.replace("<end_of_turn>", " ")
    text = re.sub(r"<\|.*?\|>", " ", text)
    text = re.sub(r"\r\n?", "\n", text)
    text = re.sub(r"\n{3,}", "\n\n", text)
    text = re.sub(r"[ \t]+", " ", text)
    return text.strip()


def reply_test_load_persona(persona_key: str) -> Dict[str, Any] | None:
    path = os.path.join(DATA_DIR, "personas", f"{reply_test_safe_key(persona_key)}.json")
    if not os.path.isfile(path):
        return None
    with open(path, "r", encoding="utf-8") as persona_file:
        return json.load(persona_file)


def reply_test_select_persona(persona: Dict[str, Any], persona_mode: str) -> Dict[str, Any]:
    mode = (persona_mode or "whole").strip().lower()
    if mode in {"identity", "identity_only", "identity-only"}:
        return {"identity": persona.get("identity") or {}}
    return persona or {}


def reply_test_variant_key(session_id: str, persona_key: str, variant: Dict[str, Any]) -> str:
    parts = [
        reply_test_safe_key(session_id),
        reply_test_safe_key(persona_key),
        reply_test_safe_key(variant.get("personaMode") or "whole"),
        "rag1" if variant.get("useRag") else "rag0",
        "sum1" if variant.get("useSummary") else "sum0",
    ]
    return "__".join(parts)


def reply_test_history_path(session_id: str, persona_key: str, variant: Dict[str, Any]) -> str:
    return os.path.join(REPLY_TEST_HISTORY_DIR, reply_test_variant_key(session_id, persona_key, variant) + ".jsonl")


def reply_test_summary_path(session_id: str, persona_key: str, variant: Dict[str, Any]) -> str:
    return os.path.join(REPLY_TEST_SUMMARY_DIR, reply_test_variant_key(session_id, persona_key, variant) + ".json")


def reply_test_read_history(session_id: str, persona_key: str, variant: Dict[str, Any], limit: int = 20) -> List[Dict[str, str]]:
    path = reply_test_history_path(session_id, persona_key, variant)
    if not os.path.isfile(path):
        return []
    rows: List[Dict[str, str]] = []
    with open(path, "r", encoding="utf-8") as history_file:
        for line in history_file:
            try:
                item = json.loads(line)
            except Exception:
                continue
            rows.append({
                "speaker": reply_test_safe_text(item.get("speaker") or ""),
                "text": reply_test_safe_text(item.get("text") or ""),
            })
    return rows[-limit:]


def reply_test_append_history(session_id: str, persona_key: str, variant: Dict[str, Any], speaker: str, text: str) -> None:
    path = reply_test_history_path(session_id, persona_key, variant)
    item = {
        "time": time.strftime("%Y-%m-%d %H:%M:%S"),
        "speaker": speaker,
        "text": reply_test_safe_text(text),
    }
    with open(path, "a", encoding="utf-8") as history_file:
        history_file.write(json.dumps(item, ensure_ascii=False) + "\n")


def reply_test_reset_session(session_id: str, persona_key: str, variant: Dict[str, Any]) -> None:
    for path in [
        reply_test_history_path(session_id, persona_key, variant),
        reply_test_summary_path(session_id, persona_key, variant),
    ]:
        if os.path.exists(path):
            os.remove(path)


def reply_test_read_summary(session_id: str, persona_key: str, variant: Dict[str, Any]) -> Dict[str, Any] | None:
    path = reply_test_summary_path(session_id, persona_key, variant)
    if not os.path.isfile(path):
        return None
    try:
        with open(path, "r", encoding="utf-8") as summary_file:
            return json.load(summary_file)
    except Exception:
        return None


def reply_test_write_summary(session_id: str, persona_key: str, variant: Dict[str, Any], summary: Dict[str, Any]) -> None:
    path = reply_test_summary_path(session_id, persona_key, variant)
    with open(path, "w", encoding="utf-8") as summary_file:
        json.dump(summary, summary_file, ensure_ascii=False, indent=2)


def reply_test_world_chunks() -> List[Dict[str, str]]:
    chunks: List[Dict[str, str]] = []
    if not os.path.isdir(WORLD_DIR):
        return chunks
    for filename in sorted(os.listdir(WORLD_DIR)):
        if not filename.lower().endswith((".txt", ".md")):
            continue
        path = os.path.join(WORLD_DIR, filename)
        if not os.path.isfile(path):
            continue
        try:
            with open(path, "r", encoding="utf-8") as world_file:
                text = world_file.read()
        except Exception:
            continue
        for index, chunk in enumerate(re.split(r"\n\s*\n", text), start=1):
            cleaned = reply_test_safe_text(chunk)
            if cleaned:
                chunks.append({"source": filename, "chunkIndex": index, "text": cleaned})
    return chunks


def reply_test_tokenize(text: str) -> set[str]:
    return set(re.findall(r"[A-Za-z0-9가-힣_]{2,}", text or ""))


def reply_test_search_world(query: str, top_k: int = 4) -> List[Dict[str, Any]]:
    query_tokens = reply_test_tokenize(query)
    scored: List[Dict[str, Any]] = []
    for chunk in reply_test_world_chunks():
        chunk_tokens = reply_test_tokenize(chunk["text"])
        overlap = len(query_tokens & chunk_tokens)
        if overlap <= 0:
            continue
        scored.append({
            "source": chunk["source"],
            "chunkIndex": chunk["chunkIndex"],
            "score": overlap,
            "text": chunk["text"],
        })
    scored.sort(key=lambda item: item["score"], reverse=True)
    return scored[:top_k]


def reply_test_format_history(history: List[Dict[str, str]]) -> str:
    if not history:
        return "- 없음"
    return "\n".join(f"- {item['speaker']}: {item['text']}" for item in history[-10:])


def reply_test_format_summary(summary: Dict[str, Any] | None) -> str:
    if not summary:
        return "- 없음"
    return json.dumps(summary, ensure_ascii=False, indent=2)


def reply_test_format_rag(chunks: List[Dict[str, Any]]) -> str:
    if not chunks:
        return "- 없음"
    lines = []
    for index, chunk in enumerate(chunks, start=1):
        lines.append(
            f"- chunk{index} source={chunk.get('source')}#{chunk.get('chunkIndex')} score={chunk.get('score')}: "
            f"{reply_test_safe_text(chunk.get('text'))}"
        )
    return "\n".join(lines)


def reply_test_build_prompt(
    persona_data: Dict[str, Any],
    message: str,
    history: List[Dict[str, str]],
    summary: Dict[str, Any] | None,
    rag_chunks: List[Dict[str, Any]],
) -> str:
    return f"""
너는 한국어 추리 게임 속 NPC다.
아래 입력만 참고해서 NPC의 실제 대사만 출력한다.
JSON, 시스템 설명, 분석문, 메타 발언은 출력하지 않는다.

[페르소나 입력]
{json.dumps(persona_data, ensure_ascii=False, indent=2)}

[세계관 문서 검색 결과]
{reply_test_format_rag(rag_chunks)}

[컨텍스트 요약]
{reply_test_format_summary(summary)}

[최근 문답]
{reply_test_format_history(history)}

[현재 플레이어 입력]
{reply_test_safe_text(message)}

[응답 규칙]
- 반드시 한국어로 답한다.
- 페르소나 입력에 포함된 정보만 성격 기준으로 사용한다.
- 세계관 문서 검색 결과가 있으면 사건 사실은 그 내용을 우선한다.
- 컨텍스트 요약이 있으면 이전 대화의 목표와 밝혀진 정보를 유지한다.
- 모르면 모른다고 하거나 관찰한 범위 안에서만 답한다.
- 너무 길게 설명하지 말고 NPC 대사처럼 말한다.

NPC 대사:
""".strip()


def reply_test_generate(prompt: str, model: str) -> str:
    chunks: List[str] = []
    previous_provider = LLM_PROVIDER
    previous_model = ACTIVE_MODEL
    try:
        globals()["LLM_PROVIDER"] = "ollama"
        globals()["ACTIVE_MODEL"] = model or previous_model
        for chunk in _query_active_llm_stream(prompt):
            chunks.append(chunk)
    finally:
        globals()["LLM_PROVIDER"] = previous_provider
        globals()["ACTIVE_MODEL"] = previous_model
    return reply_test_safe_text("".join(chunks))


def reply_test_make_summary(history: List[Dict[str, str]], model: str) -> Dict[str, Any]:
    history_text = reply_test_format_history(history[-12:])
    prompt = f"""
다음 NPC 심문 문답을 후속 대화를 위한 짧은 컨텍스트 요약 JSON으로 정리하라.
반드시 JSON 객체 하나만 출력한다.

[문답]
{history_text}

[출력 형식]
{{
  "summary": "현재까지의 핵심 대화 요약",
  "knownFacts": ["확인된 정보"],
  "openQuestions": ["아직 불명확한 질문"],
  "relationshipState": "NPC와 플레이어 사이의 현재 분위기"
}}
""".strip()
    try:
        raw = reply_test_generate(prompt, model)
        parsed = json.loads(raw)
        if isinstance(parsed, dict):
            return parsed
    except Exception:
        pass
    joined = " / ".join(item["text"] for item in history[-6:])
    return {
        "summary": joined[:500],
        "knownFacts": [],
        "openQuestions": [],
        "relationshipState": "unknown",
    }


@app.route("/reply_test", methods=["POST"])
def reply_test():
    try:
        payload = request.get_json() or {}
        session_id = payload.get("sessionId") or "reply_test_default"
        persona_key = payload.get("personaKey") or payload.get("npcId") or ""
        npc_id = payload.get("npcId") or persona_key or "npc"
        message = payload.get("message") or payload.get("question") or ""
        model = payload.get("model") or "gemma3:12b"
        variant = payload.get("variant") or {}
        persona_mode = variant.get("personaMode") or payload.get("personaMode") or "whole"
        use_rag = bool(variant.get("useRag", payload.get("useRag", False)))
        use_summary = bool(variant.get("useSummary", payload.get("useSummary", False)))
        update_summary = bool(variant.get("updateSummary", payload.get("updateSummary", use_summary)))
        reset_session = bool(payload.get("resetSession", False))

        normalized_variant = {
            "personaMode": persona_mode,
            "useRag": use_rag,
            "useSummary": use_summary,
        }

        if not persona_key:
            return jsonify({"ok": False, "error": "personaKey is required"}), 400
        if not message:
            return jsonify({"ok": False, "error": "message is required"}), 400

        persona = reply_test_load_persona(persona_key)
        if persona is None:
            return jsonify({"ok": False, "error": f"persona not found: {persona_key}"}), 404

        if reset_session:
            reply_test_reset_session(session_id, persona_key, normalized_variant)

        persona_data = reply_test_select_persona(persona, persona_mode)
        history_before = reply_test_read_history(session_id, persona_key, normalized_variant)
        summary_before = reply_test_read_summary(session_id, persona_key, normalized_variant) if use_summary else None
        rag_chunks = reply_test_search_world(message) if use_rag else []
        prompt = reply_test_build_prompt(persona_data, message, history_before, summary_before, rag_chunks)
        reply_text = reply_test_generate(prompt, model)

        reply_test_append_history(session_id, persona_key, normalized_variant, "player", message)
        reply_test_append_history(session_id, persona_key, normalized_variant, npc_id, reply_text)
        history_after = reply_test_read_history(session_id, persona_key, normalized_variant)
        summary_after = summary_before
        if update_summary:
            summary_after = reply_test_make_summary(history_after, model)
            reply_test_write_summary(session_id, persona_key, normalized_variant, summary_after)

        return jsonify({
            "ok": True,
            "sessionId": session_id,
            "npcId": npc_id,
            "personaKey": persona_key,
            "model": model,
            "variant": {
                "personaMode": persona_mode,
                "useRag": use_rag,
                "useSummary": use_summary,
                "updateSummary": update_summary,
            },
            "message": message,
            "replyText": reply_text,
            "personaInput": persona_data,
            "ragChunks": rag_chunks,
            "summaryBefore": summary_before,
            "summaryAfter": summary_after,
            "historyBefore": history_before,
            "historyAfter": history_after,
            "prompt": prompt,
            "error": "",
        })

    except Exception as error:
        import traceback

        print("[ERROR] reply_test 예외 발생:")
        traceback.print_exc()
        return jsonify({"ok": False, "error": str(error)}), 500
'''


def main() -> None:
    source = SOURCE.read_text(encoding="utf-8")
    text = source

    text = text.replace(
        "retriever = Retriever()\n",
        (
            "class ExperimentSafeRetriever:\n"
            "    def __init__(self):\n"
            "        self.text_chunks = []\n"
            "        self.index = None\n"
            "\n"
            "    def build_index(self, text_chunks):\n"
            "        self.text_chunks = list(text_chunks or [])\n"
            "\n"
            "    def save_index(self, path):\n"
            "        return None\n"
            "\n"
            "    def load_index(self, path):\n"
            "        return None\n"
            "\n"
            "    def search(self, query, top_k=3):\n"
            "        return []\n"
            "\n"
            "\n"
            "retriever = ExperimentSafeRetriever()\n"
        ),
        1,
    )

    marker = '\n\nif __name__ == "__main__":'
    if "def reply_test():" not in text:
        text = text.replace(marker, REPLY_TEST_BLOCK + marker, 1)

    text = text.replace(
        "    _select_startup_model()\n    init_index()\n    serve(app, host=\"0.0.0.0\", port=5000)\n",
        (
            "    ACTIVE_MODEL = \"gemma3:12b\"\n"
            "    LLM_PROVIDER = \"ollama\"\n"
            "    serve(app, host=\"0.0.0.0\", port=5000)\n"
        ),
        1,
    )

    TARGET.write_text(text, encoding="utf-8")
    print(f"created {TARGET}")


if __name__ == "__main__":
    main()
