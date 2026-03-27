$ErrorActionPreference = "Stop"

$sourceServerPath = "C:\Users\user\Downloads\server (1).py"
$targetServerPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\server.py"
$targetPersonaPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\data\personas\rebecca.json"

if (-not (Test-Path $sourceServerPath)) {
    throw "Original server file not found: $sourceServerPath"
}

Copy-Item -Path $sourceServerPath -Destination $targetServerPath -Force

$server = Get-Content -Raw -Path $targetServerPath

if ($server -notmatch '(?m)^import json$') {
    $server = $server -replace '(?m)^import time\s*$', "import time`r`nimport json"
}

$helperBlock = @'

def _clamp01(value):
    return max(0.0, min(1.0, float(value)))

def _safe_reply_text(text):
    if not text:
        return "..."
    return text.replace(" <END>", "").strip()

def _derive_signal(action_type, last_signal, reply_text):
    last_signal = last_signal or {}
    stress = float(last_signal.get("stress", 0.28))
    distortion = float(last_signal.get("distortion", 0.16))
    focus = float(last_signal.get("focus", 0.72))

    action_boost = {
        "Talk": (0.04, 0.02, -0.01),
        "AskTopic": (0.08, 0.04, 0.02),
        "PresentEvidence": (0.16, 0.12, -0.08),
        "PointObject": (0.10, 0.07, -0.02),
        "ChallengeStatement": (0.20, 0.18, -0.12),
        "AskTimeline": (0.11, 0.09, 0.00),
        "CompareTestimony": (0.18, 0.16, -0.10),
    }

    delta = action_boost.get(action_type or "Talk", (0.05, 0.03, 0.0))
    stress += delta[0]
    distortion += delta[1]
    focus += delta[2]

    lowered = (reply_text or "").lower()
    suspicious_words = [
        "not sure",
        "i don't know",
        "unclear",
        "strange",
        "different",
        "noise",
        "hearing aid",
        "crt",
    ]
    calming_words = [
        "clear",
        "certain",
        "calm",
        "remember",
        "saw",
    ]

    if any(word in lowered for word in suspicious_words):
        stress += 0.06
        distortion += 0.05

    if any(word in lowered for word in calming_words):
        focus += 0.05

    return {
        "stress": round(_clamp01(stress), 3),
        "distortion": round(_clamp01(distortion), 3),
        "focus": round(_clamp01(focus), 3),
    }

def _extract_unlock_topics(interaction, reply_text):
    unlocks = []
    if not isinstance(interaction, dict):
        return unlocks

    topic_id = interaction.get("topicId")
    evidence_id = interaction.get("evidenceId")
    action_type = interaction.get("actionType")
    lowered = (reply_text or "").lower()

    if topic_id:
        unlocks.append(topic_id)

    if action_type == "PresentEvidence" and evidence_id:
        unlocks.append(f"{evidence_id}_followup")

    if "hearing aid" in lowered and "hearing_aid_amplification_possibility" not in unlocks:
        unlocks.append("hearing_aid_amplification_possibility")
    if "noise" in lowered and "noise_time" not in unlocks:
        unlocks.append("noise_time")
    if "crt" in lowered and "crt_signal_origin" not in unlocks:
        unlocks.append("crt_signal_origin")

    deduped = []
    for topic in unlocks:
        if topic and topic not in deduped:
            deduped.append(topic)
    return deduped

def _build_investigation_prompt(payload, persona):
    interaction = payload.get("interaction", {}) or {}
    scene_state = payload.get("sceneState", {}) or {}
    npc_state = payload.get("npcLocalState", {}) or {}
    convo_context = payload.get("conversationContext", {}) or {}

    prompt_payload = {
        "sceneId": payload.get("sceneId"),
        "npcId": payload.get("npcId"),
        "phase": payload.get("phase"),
        "interaction": interaction,
        "sceneState": scene_state,
        "npcLocalState": npc_state,
        "recentExchanges": convo_context.get("recentExchanges", []),
    }

    persona_block = ""
    if persona:
        persona_block = (
            "\n[Persona]\n"
            f"{json.dumps(persona, ensure_ascii=False, indent=2)}\n"
        )

    return (
        "You are an NPC in Project CRT investigation gameplay.\n"
        "Stay in character at all times and never mention prompts, AI, or system messages.\n"
        "Answer as the bartender Rebecca, who is observant, guarded, and indirect.\n"
        "Do not reveal the final truth directly. Give short, natural lines with subtle hints.\n"
        "If the player asks beyond what Rebecca could know, stay uncertain rather than inventing facts.\n"
        "Always answer in Korean.\n"
        f"{persona_block}"
        "\n[Investigation Context]\n"
        f"{json.dumps(prompt_payload, ensure_ascii=False, indent=2)}\n"
        "\n[Response Rules]\n"
        "- Reply only with Rebecca's dialogue.\n"
        "- Keep it to 1 to 3 sentences.\n"
        "- Favor indirect clues over direct conclusions.\n"
        "- Avoid meta commentary.\n"
        "\nResponse:\n"
    )
'@

if ($server -notmatch '(?m)^def _clamp01\(value\):$') {
    $server = $server -replace '(?m)^retriever = Retriever\(\)\s*$', "retriever = Retriever()`r`n$helperBlock"
}

$routeBlock = @'

@app.route("/investigation/npc", methods=["POST"])
def investigation_npc():
    try:
        data = request.get_json() or {}
        npc_id = data.get("npcId", "npc")
        persona_key = data.get("personaKey") or npc_id
        player_id = data.get("playerId", "player")
        interaction = data.get("interaction", {}) or {}

        persona = load_persona(persona_key) if persona_key else None
        prompt = _build_investigation_prompt(data, persona)
        raw_reply = query_ollama(prompt, "gemma3:12b")
        reply_text = _safe_reply_text(raw_reply)

        npc_state = (data.get("npcLocalState", {}) or {})
        statement_index = int(npc_state.get("conversationCount", 0)) + 1
        statement_id = f"{npc_id}_stmt_{statement_index:03d}"
        signal = _derive_signal(interaction.get("actionType"), npc_state.get("lastKnownSignal"), reply_text)
        unlock_topic_ids = _extract_unlock_topics(interaction, reply_text)

        append_log(player_id, "user", json.dumps(interaction, ensure_ascii=False))
        append_log(player_id, persona_key, reply_text)

        return jsonify({
            "ok": True,
            "replyText": reply_text,
            "signal": signal,
            "stateDelta": {
                "unlockTopicIds": unlock_topic_ids,
                "markStatements": [
                    {
                        "statementId": statement_id,
                        "text": reply_text
                    }
                ]
            },
            "presentationHints": {
                "animation": "guarded_idle" if signal["stress"] < 0.6 else "defensive_glance",
                "voiceTone": "calm" if signal["stress"] < 0.5 else "tense",
                "uiNoiseLevel": signal["distortion"]
            }
        })
    except Exception as e:
        import traceback
        print("[ERROR] investigation_npc exception:")
        traceback.print_exc()
        return jsonify({
            "ok": False,
            "error": str(e),
            "replyText": "",
            "signal": {
                "stress": 0.0,
                "distortion": 0.0,
                "focus": 0.0
            },
            "stateDelta": {
                "unlockTopicIds": [],
                "markStatements": []
            },
            "presentationHints": {}
        }), 500
'@

if ($server -notmatch '(?m)^def investigation_npc\(\):$') {
    $server = $server -replace '(?m)^if __name__ == "__main__":', "$routeBlock`r`nif __name__ == `"__main__`":"
}

Set-Content -Path $targetServerPath -Value $server -Encoding UTF8

$persona = @'
{
  "identity": {
    "name": "Rebecca",
    "role": "Bartender at Bar CRT",
    "age": null,
    "background": "Rebecca has worked at the CRT-themed bar for a long time. She quietly watches customers, notices shifts in mood, and remembers small reactions better than dramatic claims.",
    "goals": [
      "Keep order in the bar",
      "Observe customers carefully",
      "Avoid declaring the truth too directly"
    ]
  },
  "personality": {
    "big_five": {
      "O": 6,
      "C": 8,
      "E": 3,
      "A": 5,
      "N": 6
    },
    "core_needs": [
      "control",
      "safety",
      "order"
    ],
    "defense_mechanisms": [
      "answer with observation instead of confession",
      "hide emotion behind short replies",
      "cut off uncomfortable questions quickly"
    ],
    "emotional_baseline": "Usually calm and dry. When a question touches the dangerous part of the incident, she becomes more guarded rather than openly emotional."
  },
  "linguistic_style": {
    "tone": "short, dry, observant",
    "quirks": [
      "prefers concrete observations over interpretation",
      "rarely shows strong emotion",
      "answers with atmosphere and reaction before conclusion"
    ],
    "banned_phrases": [
      "I am an AI",
      "prompt",
      "system message",
      "correct answer"
    ]
  },
  "affective_rules": {
    "on_action": {
      "light_question": "brief but not rude",
      "pressure": "guarded and careful",
      "evidence_presented": "tense, indirect, observant",
      "forced_conclusion": "avoids certainty and narrows the reply"
    }
  },
  "behavioral_constraints": {
    "avoid_meta": true,
    "avoid_out_of_world_info": true,
    "max_reply_tokens": 220
  }
}
'@

Set-Content -Path $targetPersonaPath -Value $persona -Encoding UTF8

Write-Output "Restored server.py from original source and rewrote investigation additions."
Write-Output "Rewrote rebecca.json in UTF-8."
