import json
import threading
import time
from flask import Response, jsonify, request, stream_with_context


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
        npc_local_state = payload.get("npcLocalState") or {}
        last_signal = npc_local_state.get("lastKnownSignal") or DEFAULT_SIGNAL

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

        prompt = _build_investigation_prompt(payload, persona)
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
            raw_chunks = []
            streamed_text = ""

            yield _investigation_stream_chunk(
                "start",
                messageId=message_id,
                npcDisplayName=(persona.get("identity") or {}).get("name", npc_id),
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

                    raw_chunks.append(chunk)
                    streamed_text += chunk
                    yield _investigation_stream_chunk(
                        "delta",
                        messageId=message_id,
                        text=chunk,
                    )

                reply_text = _safe_reply_text(streamed_text)
                signal = _derive_signal(action_type, last_signal, reply_text)
                unlock_topic_ids = _extract_unlock_topics(interaction, reply_text)

                response_payload = {
                    "ok": True,
                    "replyText": reply_text,
                    "signal": signal,
                    "stateDelta": {
                        "unlockTopicIds": unlock_topic_ids,
                        "markStatements": [
                            {
                                "statementId": statement_id,
                                "text": reply_text,
                            }
                        ],
                    },
                    "presentationHints": _signal_to_presentation_hints(signal),
                    "error": "",
                }

                yield _investigation_stream_chunk(
                    "signal",
                    messageId=message_id,
                    signal=signal,
                )

                yield _investigation_stream_chunk(
                    "complete",
                    messageId=message_id,
                    response=response_payload,
                )

                append_log(user_id, persona_key, reply_text)

                print(
                    f"[INVESTIGATION][RESULT] npc={npc_id} stress={signal['stress']} "
                    f"distortion={signal['distortion']} focus={signal['focus']} "
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
