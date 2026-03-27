$ErrorActionPreference = "Stop"

$repoPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI"
$gitExe = "C:\Progra~1\Git\cmd\git.exe"
$commitId = "d9c0cd7"
$serverRelativePath = "virtualEnv/RAG_model/app/server.py"
$serverPath = Join-Path $repoPath $serverRelativePath
$personaPath = "C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\data\personas\rebecca.json"

$baseServer = & $gitExe -c "safe.directory=$repoPath" -C $repoPath show "${commitId}:${serverRelativePath}"
if (-not $baseServer) {
    throw "Failed to read clean server.py from git history."
}

$server = ($baseServer -join "`r`n")

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
    suspicious_words = ["노이즈", "보청기", "crt", "기억", "불분명", "이상"]
    calming_words = ["확실", "분명", "차분", "기억해", "봤어"]

    if any(word in reply_text for word in suspicious_words) or any(word in lowered for word in ["noise", "hearing aid", "crt"]):
        stress += 0.06
        distortion += 0.05

    if any(word in reply_text for word in calming_words) or any(word in lowered for word in ["clear", "certain", "calm", "remember"]):
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

    if "보청기" in reply_text or "hearing aid" in lowered:
        unlocks.append("hearing_aid_amplification_possibility")
    if "노이즈" in reply_text or "noise" in lowered:
        unlocks.append("noise_time")
    if "CRT" in reply_text or "crt" in lowered:
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
            "\n[페르소나]\n"
            f"{json.dumps(persona, ensure_ascii=False, indent=2)}\n"
        )

    return (
        "당신은 Project CRT 수사 파트의 NPC다.\n"
        "메타 발언이나 시스템 언급 없이, 바텐더 리베카로만 답한다.\n"
        "정답을 직접 밝히지 말고 짧고 자연스럽게 반응한다.\n"
        "확실하지 않은 내용은 단정하지 말고 관찰자 시점의 간접 힌트만 준다.\n"
        "반드시 한국어로 답한다.\n"
        f"{persona_block}"
        "\n[조사 컨텍스트]\n"
        f"{json.dumps(prompt_payload, ensure_ascii=False, indent=2)}\n"
        "\n[응답 규칙]\n"
        "- 1~3문장으로 짧게 말한다.\n"
        "- 리베카의 대사만 출력한다.\n"
        "- 단서가 될 만한 분위기, 반응, 관찰을 우선한다.\n"
        "- 없는 사실을 만들어내지 않는다.\n"
        "\n응답:\n"
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

Set-Content -Path $serverPath -Value $server -Encoding UTF8

$persona = @'
{
  "identity": {
    "name": "리베카",
    "role": "Bar CRT의 바텐더",
    "age": null,
    "background": "리베카는 CRT 테마의 바에서 오래 일한 바텐더다. 손님의 말보다 표정과 반응을 더 잘 기억하고, 사건이 일어난 밤의 분위기를 또렷하게 감지하고 있다.",
    "goals": [
      "바의 질서를 유지한다",
      "손님들의 반응을 관찰한다",
      "진실을 직접 단정하지 않는다"
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
      "통제감",
      "안전",
      "질서 유지"
    ],
    "defense_mechanisms": [
      "직설 대신 관찰로 돌려 말하기",
      "감정을 숨기고 건조하게 반응하기",
      "불편한 질문에는 짧게 선을 긋기"
    ],
    "emotional_baseline": "평소에는 차분하고 냉정하지만, 사건의 핵심을 건드리는 질문에는 미묘하게 경계심을 드러낸다."
  },
  "linguistic_style": {
    "tone": "짧고 건조하며 관찰적인 말투",
    "quirks": [
      "해석보다 본 사실을 먼저 말한다",
      "감정 표현이 적다",
      "결론보다 분위기와 반응을 먼저 짚는다"
    ],
    "banned_phrases": [
      "나는 AI",
      "프롬프트",
      "시스템 메시지",
      "정답은"
    ]
  },
  "behavioral_constraints": {
    "avoid_meta": true,
    "avoid_out_of_world_info": true,
    "max_reply_tokens": 220
  }
}
'@

Set-Content -Path $personaPath -Value $persona -Encoding UTF8

Write-Output "Restored clean server.py from git history and reapplied investigation route."
Write-Output "Rewrote rebecca.json in UTF-8 Korean."
