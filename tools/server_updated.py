#요구사항
"""
pip install flask flask-cors
virtualEnv\RAG_model\app\server.py
"""
import json
import os
import re
import threading
import time
from typing import Any, Dict, List

from flask import Flask, request, jsonify, Response, stream_with_context
from flask_cors import CORS

from persona_store import load_persona, list_persona_keys
from chatlog import append_log, read_log, reset_log, read_summary, write_summary
from summarizer import summarize_context
from summarizer_llm import summarize_context_llm
from summarizer import summarize_context as summarize_context_rule_based
from documentHandler import extract_text_from_pdf, split_text_to_chunks
from retriever import Retriever
from llmClient import build_prompt, build_prompt_v2, query_ollama, query_ollama_stream

app = Flask(__name__)
CORS(app)

PDF_NAME = "DatabasePrompt"  # PDF파일 이름
PDF_PATH = "app/data/pdfs/" + PDF_NAME + ".pdf"  # PDF 경로
INDEX_PATH = "app/data/index/world"
WORLD_DIR = "app/data/world"

retriever = Retriever()


def _default_interrogation_state() -> Dict[str, Any]:
    return {
        "tell": 0.0,
        "affect": {
            "interest": 0.0,
            "attitude": 0.0,
        },
        "patience": 100,
    }


def _clamp(value: float, min_value: float = 0.0, max_value: float = 1.0) -> float:
    return max(min_value, min(max_value, value))


def _to_float(value: Any, default: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def _to_int(value: Any, default: int = 0) -> int:
    try:
        return int(value)
    except (TypeError, ValueError):
        return default


def _safe_reply_text(text: Any) -> str:
    if text is None:
        return "..."

    cleaned = str(text)
    cleaned = cleaned.replace("<|eot_id|>", " ")
    cleaned = cleaned.replace("</s>", " ")
    cleaned = cleaned.replace("<end_of_turn>", " ")
    cleaned = re.sub(r"<\|.*?\|>", " ", cleaned)
    cleaned = re.sub(r"\r\n?", "\n", cleaned)
    cleaned = re.sub(r"\n{3,}", "\n\n", cleaned)
    cleaned = re.sub(r"[ \t]+", " ", cleaned)
    cleaned = re.sub(r"[ \t]+\n", "\n", cleaned)
    cleaned = cleaned.strip()
    return cleaned or "..."


def _clone_affect(affect: Dict[str, Any]) -> Dict[str, float]:
    affect = affect or {}
    return {
        "interest": round(_clamp(_to_float(affect.get("interest"), 0.0), -1.0, 1.0), 2),
        "attitude": round(_clamp(_to_float(affect.get("attitude"), 0.0), -1.0, 1.0), 2),
    }


def _normalize_interrogation_state(state: Dict[str, Any]) -> Dict[str, Any]:
    state = state or {}
    affect = _clone_affect(state.get("affect"))
    return {
        "tell": round(_clamp(_to_float(state.get("tell"), 0.0), 0.0, 1.0), 2),
        "affect": affect,
        "patience": _to_int(_clamp(_to_int(state.get("patience"), 100), 0, 100), 100),
    }


def _interrogation_state_to_presentation_hints(state: Dict[str, Any]) -> Dict[str, Any]:
    state = _normalize_interrogation_state(state)
    tell = state["tell"]
    interest = state["affect"]["interest"]
    attitude = state["affect"]["attitude"]
    patience = state["patience"]

    if tell >= 0.7:
        animation = "tense_idle"
    elif tell >= 0.35 or patience <= 30:
        animation = "guarded_idle"
    else:
        animation = "calm_idle"

    if attitude <= -0.4 or patience <= 20:
        voice_tone = "hostile"
    elif attitude >= 0.35 and interest >= 0.2:
        voice_tone = "cooperative"
    else:
        voice_tone = "guarded"

    return {
        "animation": animation,
        "voiceTone": voice_tone,
        "uiNoiseLevel": round(tell, 2),
    }


def _derive_interrogation_state(
    action_type: str,
    interaction: Dict[str, Any],
    scene_state: Dict[str, Any],
    npc_local_state: Dict[str, Any],
    conversation_context: Dict[str, Any],
) -> Dict[str, Any]:
    affect = _clone_affect((npc_local_state or {}).get("lastKnownAffect"))
    patience = _to_int((npc_local_state or {}).get("lastKnownPatience"), 100)
    patience = _to_int(_clamp(patience, 0, 100), 100)

    action = (action_type or "Talk").strip()
    player_intent_text = (interaction or {}).get("playerIntentText") or ""
    topic_id = (interaction or {}).get("topicId")
    evidence_id = (interaction or {}).get("evidenceId")
    discovered_evidence_ids = set((scene_state or {}).get("discoveredEvidenceIds") or [])
    unlocked_topic_ids = set((scene_state or {}).get("unlockedTopicIds") or [])
    recent_exchanges = (conversation_context or {}).get("recentExchanges") or []

    tell = 0.08
    patience_cost = 4

    if action == "PresentEvidence":
        tell += 0.42
        affect["interest"] += 0.06
        affect["attitude"] -= 0.08
        patience_cost += 10
    elif action == "AskTopic":
        tell += 0.20
        affect["interest"] += 0.04
        patience_cost += 6
    else:
        tell += 0.05
        affect["interest"] += 0.01

    if topic_id:
        if topic_id in unlocked_topic_ids:
            affect["interest"] += 0.14
            tell += 0.06
        else:
            affect["interest"] -= 0.08
            affect["attitude"] -= 0.03

    if evidence_id:
        if evidence_id in discovered_evidence_ids:
            tell += 0.16
            affect["interest"] += 0.05
        else:
            affect["interest"] -= 0.05
            affect["attitude"] -= 0.05

    if re.search(r"왜|정말|확실|거짓|숨기|모순|설명", player_intent_text):
        tell += 0.10
        affect["attitude"] -= 0.04

    if re.search(r"노이즈|잡음|보청기|CRT|브라운관", player_intent_text, re.IGNORECASE):
        tell += 0.08
        affect["interest"] += 0.08

    repeated_question = False
    normalized_intent = _safe_reply_text(player_intent_text)
    for exchange in recent_exchanges[-4:]:
        if exchange.get("speaker") != "player":
            continue
        if _safe_reply_text(exchange.get("text")) == normalized_intent:
            repeated_question = True
            break

    if repeated_question:
        affect["interest"] -= 0.12
        affect["attitude"] -= 0.06
        patience_cost += 6

    if affect["interest"] >= 0.4:
        affect["attitude"] += 0.05
        patience_cost -= 1
    elif affect["interest"] <= -0.3:
        affect["attitude"] -= 0.06
        patience_cost += 3

    if affect["attitude"] >= 0.4:
        patience_cost -= 2
    elif affect["attitude"] <= -0.4:
        patience_cost += 4

    if patience <= 35:
        affect["attitude"] -= 0.04
        tell += 0.04
        patience_cost += 2

    next_patience = max(0, min(100, patience - max(1, patience_cost)))

    return {
        "tell": round(_clamp(tell, 0.0, 1.0), 2),
        "affect": {
            "interest": round(_clamp(affect["interest"], -1.0, 1.0), 2),
            "attitude": round(_clamp(affect["attitude"], -1.0, 1.0), 2),
        },
        "patience": next_patience,
    }


def _extract_unlock_topics(interaction: Dict[str, Any], reply_text: str) -> List[str]:
    unlocks: List[str] = []
    text = reply_text or ""
    action_type = (interaction or {}).get("actionType") or "Talk"
    evidence_id = (interaction or {}).get("evidenceId")

    keyword_rules = [
        (r"노이즈|잡음|화이트\s*노이즈", "noise_time"),
        (r"보청기", "hearing_aid_amplification_possibility"),
        (r"CRT|브라운관", "crt_signal_origin"),
    ]

    for pattern, topic_id in keyword_rules:
        if re.search(pattern, text, re.IGNORECASE) and topic_id not in unlocks:
            unlocks.append(topic_id)

    if action_type == "PresentEvidence" and evidence_id:
        followup_id = f"{evidence_id}_followup"
        if followup_id not in unlocks:
            unlocks.append(followup_id)

    return unlocks


def _build_statement_id(npc_id: str, npc_state: Dict[str, Any]) -> str:
    count = int(_to_float((npc_state or {}).get("conversationCount"), 0)) + 1
    safe_npc = re.sub(r"[^a-zA-Z0-9_]+", "_", npc_id or "npc").strip("_") or "npc"
    return f"{safe_npc}_stmt_{count:03d}"


def _build_investigation_prompt(
    payload: Dict[str, Any],
    persona: Dict[str, Any],
    interrogation_state: Dict[str, Any],
) -> str:
    scene_id = payload.get("sceneId") or "unknown_scene"
    npc_id = payload.get("npcId") or "unknown_npc"
    phase = payload.get("phase") or "investigation"
    interaction = payload.get("interaction") or {}
    scene_state = payload.get("sceneState") or {}
    npc_local_state = payload.get("npcLocalState") or {}
    conversation_context = payload.get("conversationContext") or {}

    action_type = interaction.get("actionType") or "Talk"
    player_intent_text = interaction.get("playerIntentText") or ""
    topic_id = interaction.get("topicId")
    evidence_id = interaction.get("evidenceId")

    recent_exchanges = conversation_context.get("recentExchanges") or []
    recent_lines = []
    for ex in recent_exchanges[-6:]:
        speaker = ex.get("speaker", "unknown")
        text = _safe_reply_text(ex.get("text", ""))
        if text:
            recent_lines.append(f"- {speaker}: {text}")

    persona_json = json.dumps(persona or {}, ensure_ascii=False, indent=2)
    scene_state_json = json.dumps(scene_state, ensure_ascii=False, indent=2)
    npc_state_json = json.dumps(npc_local_state, ensure_ascii=False, indent=2)
    interrogation_state_json = json.dumps(_normalize_interrogation_state(interrogation_state), ensure_ascii=False, indent=2)

    prompt = f"""
너는 한국어 추리 게임 속 NPC다. 지금부터 조사 파트 전용 응답만 생성한다.

[페르소나 데이터]
{persona_json}

[현재 조사 정보]
- sceneId: {scene_id}
- phase: {phase}
- npcId: {npc_id}
- actionType: {action_type}
- playerIntentText: {_safe_reply_text(player_intent_text)}
- topicId: {topic_id}
- evidenceId: {evidence_id}

[사건 상태]
{scene_state_json}

[NPC 로컬 상태]
{npc_state_json}

[이번 질문에 대한 심문 상태]
{interrogation_state_json}

[최근 대화]
{os.linesep.join(recent_lines) if recent_lines else '- 없음'}

[응답 원칙]
- 반드시 한국어로만 대답한다.
- 아래 심문 상태를 보고 답변의 길이, 협조성, 방어성, 동요 정도를 조절한다.
- tell이 높을수록 머뭇거림, 회피, 자기수정, 말꼬임, 제한적인 모순 가능성을 보인다.
- affect.interest가 높을수록 답변이 자세해지고, 낮을수록 짧고 건조해진다.
- affect.attitude가 낮을수록 차갑고 방어적이며, 높을수록 협조적이다.
- patience가 낮을수록 짧고 예민한 답변을 한다. patience가 매우 낮으면 더 이상 길게 설명하지 않으려 한다.
- 사건 상태와 최근 대화와 모순되지 않도록 한다.
- 시스템 설명, 상태 수치, JSON, 메타 발언은 절대 출력하지 않는다.

이제 NPC의 실제 대사만 출력해라.
""".strip()
    return prompt


def _build_investigation_error_response(error_message: str):
    return {
        "ok": False,
        "error": str(error_message),
        "replyText": "",
        "interrogationState": _default_interrogation_state(),
        "stateDelta": {
            "unlockTopicIds": [],
            "markStatements": [],
        },
        "presentationHints": {},
    }


def _load_world_chunks():
    chunks = []
    if not os.path.isdir(WORLD_DIR):
        return chunks
    for fname in os.listdir(WORLD_DIR):
        path = os.path.join(WORLD_DIR, fname)
        if os.path.isfile(path) and any(fname.lower().endswith(ext) for ext in [".txt", ".md"]):
            with open(path, "r", encoding="utf-8") as f:
                text = f.read().strip()
                if text:
                    chunks.extend([p for p in text.split("\n\n") if p.strip()])
    return chunks


def init_index():
    if not os.path.exists(INDEX_PATH + ".index"):
        world_chunks = _load_world_chunks()
        if world_chunks:
            print(f" 세계관 문서 {len(world_chunks)}개 청크 적재")
            retriever.build_index(world_chunks)
            retriever.save_index(INDEX_PATH)
            print("인덱스 로드 완료")
        else:
            print("세계관 문서 없음")
    else:
        retriever.load_index(INDEX_PATH)
        print("📚 기존 인덱스 로드 완료")


@app.route("/log/summary", methods=["GET"])
def log_summary():
    user_id = request.args.get("user_id", "anonymous")
    hist = read_log(user_id)
    history = [{"speaker": h.get("speaker"), "text": h.get("text")} for h in hist]
    res = summarize_context(history, recent_turns=20, max_tokens=384)
    return jsonify(res)


@app.route("/log/summary-llm", methods=["GET"])
def log_summary_llm():
    user_id = request.args.get("user_id", "anonymous")
    hist = read_log(user_id)
    history = [{"speaker": h.get("speaker"), "text": h.get("text")} for h in hist]
    result = summarize_context_llm(
        history=history,
        recent_turns=20,
        model="gemma3:12b",
        max_tokens=384,
        fallback_rule_based=True,
        rule_based_fn=summarize_context_rule_based,
    )
    return jsonify(result)


@app.route("/log/reset", methods=["POST"])
def log_reset():
    data = request.get_json() or {}
    user_id = data.get("user_id", "anonymous")
    reset_log(user_id)
    return jsonify({"ok": True})


def _investigation_stream_chunk(chunk_type, **payload):
    chunk = {"type": chunk_type}
    chunk.update(payload)
    return json.dumps(chunk, ensure_ascii=False) + "\n"


@app.route("/investigation/npc", methods=["POST"])
def investigation_npc():
    try:
        payload = request.get_json() or {}
        user_id = payload.get("playerId") or payload.get("userId") or "anonymous"
        npc_id = payload.get("npcId") or "npc"
        persona_key = payload.get("personaKey") or npc_id or ""
        phase = payload.get("phase") or "investigation"
        interaction = payload.get("interaction") or {}
        action_type = interaction.get("actionType") or "Talk"
        player_intent_text = interaction.get("playerIntentText") or ""
        scene_state = payload.get("sceneState") or {}
        npc_local_state = payload.get("npcLocalState") or {}
        conversation_context = payload.get("conversationContext") or {}

        if not persona_key:
            return jsonify(_build_investigation_error_response("personaKey가 없습니다.")), 400

        persona = load_persona(persona_key)
        if persona is None:
            return jsonify(_build_investigation_error_response(f"persona not found: {persona_key}")), 404

        if not player_intent_text and action_type == "Talk":
            return jsonify(_build_investigation_error_response("interaction.playerIntentText가 없습니다.")), 400

        print(
            f"[INVESTIGATION] phase={phase} user={user_id} npc={npc_id} "
            f"persona={persona_key} action={action_type}"
        )

        append_log(
            user_id,
            "user",
            f"[INVESTIGATION/{npc_id}/{action_type}] {_safe_reply_text(player_intent_text)}",
        )

        interrogation_state = _derive_interrogation_state(
            action_type,
            interaction,
            scene_state,
            npc_local_state,
            conversation_context,
        )
        prompt = _build_investigation_prompt(payload, persona, interrogation_state)
        statement_id = _build_statement_id(npc_id, npc_local_state)
        message_id = f"{statement_id}_stream"
        start_time = time.time()

        def _update_summary_background():
            try:
                hist = read_log(user_id, max_items=60)
                history = [{"speaker": h.get("speaker"), "text": h.get("text")} for h in hist]

                summary = summarize_context_llm(
                    history=history,
                    recent_turns=20,
                    model="gemma3:12b",
                    max_tokens=256,
                    fallback_rule_based=True,
                    rule_based_fn=summarize_context_rule_based,
                )
                write_summary(user_id, summary)
                print(f"[SUMMARY] updated for user={user_id}")
            except Exception as e:
                print(f"[WARN] summary update failed after investigation stream: {e}")

        def generate():
            first_chunk_logged = False
            streamed_text = ""

            yield _investigation_stream_chunk(
                "start",
                messageId=message_id,
                npcDisplayName=(persona.get("identity") or {}).get("name", npc_id),
            )

            yield _investigation_stream_chunk(
                "state",
                messageId=message_id,
                interrogationState=interrogation_state,
            )

            try:
                for chunk in query_ollama_stream(prompt, "gemma3:12b"):
                    if not first_chunk_logged:
                        latency = time.time() - start_time
                        print(
                            f"[LATENCY] investigation user={user_id} npc={npc_id} "
                            f"persona={persona_key} took {latency:.2f}s to first token"
                        )
                        first_chunk_logged = True

                    streamed_text += chunk
                    yield _investigation_stream_chunk(
                        "delta",
                        messageId=message_id,
                        text=chunk,
                    )

                reply_text = _safe_reply_text(streamed_text)
                unlock_topic_ids = _extract_unlock_topics(interaction, reply_text)
                response_payload = {
                    "ok": True,
                    "replyText": reply_text,
                    "interrogationState": interrogation_state,
                    "stateDelta": {
                        "unlockTopicIds": unlock_topic_ids,
                        "markStatements": [
                            {
                                "statementId": statement_id,
                                "text": reply_text,
                            }
                        ],
                    },
                    "presentationHints": _interrogation_state_to_presentation_hints(interrogation_state),
                    "error": "",
                }

                yield _investigation_stream_chunk(
                    "complete",
                    messageId=message_id,
                    response=response_payload,
                )

                append_log(user_id, persona_key, reply_text)

                print(
                    f"[INVESTIGATION][RESULT] npc={npc_id} tell={interrogation_state['tell']} "
                    f"interest={interrogation_state['affect']['interest']} "
                    f"attitude={interrogation_state['affect']['attitude']} "
                    f"patience={interrogation_state['patience']} "
                    f"unlocks={unlock_topic_ids}"
                )

                threading.Thread(target=_update_summary_background, daemon=True).start()

            except Exception as stream_error:
                import traceback

                print("[ERROR] investigation_npc stream 예외 발생:")
                traceback.print_exc()
                yield _investigation_stream_chunk(
                    "error",
                    messageId=message_id,
                    error=str(stream_error),
                )

        return Response(
            stream_with_context(generate()),
            content_type="application/x-ndjson; charset=utf-8",
        )

    except Exception as e:
        import traceback

        print("[ERROR] investigation_npc 예외 발생:")
        traceback.print_exc()
        return jsonify(_build_investigation_error_response(str(e))), 500


@app.route("/ask-stream", methods=["POST"])
def ask_stream():
    try:
        data = request.get_json() or {}
        question = data.get("question")
        user_id = data.get("user_id", "anonymous")
        persona_key = data.get("personaKey") or ""
        print(f"[DEBUG] 질문: {question}, 유저: {user_id}, 페르소나: {persona_key or 'assistant'}")

        if not question:
            return "질문이 없습니다", 400

        start_time = time.time()
        append_log(user_id, "user", question)

        top_chunks = retriever.search(question)
        persona = load_persona(persona_key) if persona_key else None

        hist = read_log(user_id, max_items=60)
        history = [{"speaker": h.get("speaker"), "text": h.get("text")} for h in hist]

        summary = read_summary(user_id)
        merged_chunks = top_chunks
        if summary and (summary.get("summary") or "").strip():
            summary_line = f"[대화요약] {summary['summary']}"
            merged_chunks = [summary_line] + top_chunks

        prompt = build_prompt_v2(merged_chunks, question, persona)

        for i, c in enumerate(top_chunks, 1):
            head = (c[:20] + "…") if len(c) > 20 else c
            print(f"[{i}] {head}")
        print("=================================\n")

        def _update_summary_background():
            try:
                hist = read_log(user_id, max_items=60)
                history = [{"speaker": h.get("speaker"), "text": h.get("text")} for h in hist]

                summary = summarize_context_llm(
                    history=history,
                    recent_turns=20,
                    model="gemma3:12b",
                    max_tokens=256,
                    fallback_rule_based=True,
                    rule_based_fn=summarize_context_rule_based,
                )
                write_summary(user_id, summary)
                print(f"[SUMMARY] updated for user={user_id}")

                summary_text = summary.get("summary") or ""
                rel = summary.get("relationship_state", {})
                mode = "LLM" if summary.get("user_goal") is not None else "RULE-BASED(FALLBACK)"

                print("\n[SUMMARY UPDATE] =======================")
                print(f"User: {user_id} | Persona: {persona_key or 'assistant'}")
                print(f"[MODE] {mode}")
                print(f"[요약] {summary_text}")
                print(f"[USER_GOAL] {summary.get('user_goal')}")
                print(f"[대화 주제] {', '.join(summary.get('topics', []))}")
                print(f"[관계 상태] {rel}")
                print("=======================================\n")
            except Exception as e:
                print(f"[WARN] summary update failed: {e}")

        def generate():
            nonlocal start_time
            first_chunk_logged = False
            buffer = []
            for chunk in query_ollama_stream(prompt, "gemma3:12b"):
                if not first_chunk_logged:
                    latency = time.time() - start_time
                    print(f"[LATENCY] user={user_id} persona={persona_key or 'assistant'} took {latency:.2f}s to first token")
                    first_chunk_logged = True
                buffer.append(chunk)
                yield chunk + "\n"

            full_answer = "".join(buffer).strip()
            speaker = persona_key if persona_key else "assistant"
            try:
                append_log(user_id, speaker, full_answer)
            except Exception as log_err:
                print(f"[WARN] 답변 로그 기록 실패: {log_err}")

            threading.Thread(target=_update_summary_background, daemon=True).start()

        return Response(stream_with_context(generate()), content_type="text/plain")

    except Exception as e:
        import traceback
        print("[ERROR] ask_stream 예외 발생:")
        traceback.print_exc()
        return f"[SERVER ERROR] {str(e)}", 500


if __name__ == "__main__":
    from waitress import serve

    init_index()
    serve(app, host="0.0.0.0", port=5000)
    # app.run(port=5000, debug=True)
