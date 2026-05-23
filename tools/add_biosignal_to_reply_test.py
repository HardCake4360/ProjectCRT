# -*- coding: utf-8 -*-
from pathlib import Path


TARGET = Path(r"C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\server_for_experiment.py")

BIO_BLOCK = r'''

def reply_test_json_from_text(text: str) -> Dict[str, Any] | None:
    cleaned = reply_test_safe_text(text)
    if cleaned.startswith("```"):
        cleaned = re.sub(r"^```(?:json)?", "", cleaned).strip()
        cleaned = re.sub(r"```$", "", cleaned).strip()
    try:
        parsed = json.loads(cleaned)
        return parsed if isinstance(parsed, dict) else None
    except Exception:
        pass
    match = re.search(r"\{.*\}", cleaned, re.DOTALL)
    if not match:
        return None
    try:
        parsed = json.loads(match.group(0))
        return parsed if isinstance(parsed, dict) else None
    except Exception:
        return None


def reply_test_clamp_number(value: Any, default: float = 0.0) -> float:
    try:
        number = float(value)
    except Exception:
        number = default
    return round(max(-1.0, min(1.0, number)), 2)


def reply_test_tell_band(tell: float) -> str:
    tell = max(0.0, min(1.0, float(tell)))
    if tell < 0.25:
        return "Stable"
    if tell < 0.50:
        return "Guarded"
    if tell < 0.75:
        return "Shaken"
    return "Disturbed"


def reply_test_build_biosignal_prompt(
    persona_data: Dict[str, Any],
    message: str,
    reply_text: str,
    history: List[Dict[str, str]],
    summary: Dict[str, Any] | None,
    rag_chunks: List[Dict[str, Any]],
) -> str:
    return f"""
너는 추리 게임 NPC의 생체 징후 분석기다.
아래 정보만 보고 이번 턴의 tell과 affect를 계산한다.
반드시 JSON 객체 하나만 출력한다. 설명문, 코드블록, 마크다운은 금지한다.

[tell 정의]
- tell은 이번 질문 직후 NPC가 얼마나 동요했는지를 나타내는 0~1 수치다.
- 높은 tell은 예상치 못한 질문, 방어기제 자극, 긴장, 회피 충동, 감정적 흔들림을 뜻한다.
- tell은 거짓말 여부를 직접 의미하지 않는다.

[affect 정의]
- interest는 현재 질문/주제에 대한 관심도다. 범위는 -1~1이다.
- attitude는 플레이어에 대한 태도다. 범위는 -1~1이다.
- 값이 높을수록 관심/협조적이고, 낮을수록 무관심/방어적이다.

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

[NPC 응답]
{reply_test_safe_text(reply_text)}

[출력 형식]
{{
  "tell": 0.0,
  "band": "Stable|Guarded|Shaken|Disturbed",
  "primaryAction": "이번 질문/상황의 간단한 분류",
  "reason": "한 문장 근거",
  "affect": {{
    "interest": 0.0,
    "attitude": 0.0
  }}
}}
""".strip()


def reply_test_calculate_biosignal(
    persona_data: Dict[str, Any],
    message: str,
    reply_text: str,
    history: List[Dict[str, str]],
    summary: Dict[str, Any] | None,
    rag_chunks: List[Dict[str, Any]],
    model: str,
) -> Dict[str, Any]:
    prompt = reply_test_build_biosignal_prompt(
        persona_data,
        message,
        reply_text,
        history,
        summary,
        rag_chunks,
    )
    raw = reply_test_generate(prompt, model)
    parsed = reply_test_json_from_text(raw) or {}
    tell = round(max(0.0, min(1.0, reply_test_clamp_number(parsed.get("tell"), 0.0))), 2)
    affect = parsed.get("affect") if isinstance(parsed.get("affect"), dict) else {}
    band = parsed.get("band") or reply_test_tell_band(tell)
    if band not in {"Stable", "Guarded", "Shaken", "Disturbed"}:
        band = reply_test_tell_band(tell)
    return {
        "tell": tell,
        "band": band,
        "primaryAction": reply_test_safe_text(parsed.get("primaryAction") or "unknown"),
        "reason": reply_test_safe_text(parsed.get("reason") or ""),
        "affect": {
            "interest": reply_test_clamp_number(affect.get("interest"), 0.0),
            "attitude": reply_test_clamp_number(affect.get("attitude"), 0.0),
        },
        "raw": raw,
        "prompt": prompt,
    }
'''


def main() -> None:
    text = TARGET.read_text(encoding="utf-8")
    if "def reply_test_calculate_biosignal(" not in text:
        marker = '\n\n@app.route("/reply_test", methods=["POST"])'
        text = text.replace(marker, BIO_BLOCK + marker, 1)

    old = (
        "        reply_text = reply_test_generate(prompt, model)\n\n"
        "        reply_test_append_history(session_id, persona_key, normalized_variant, \"player\", message)\n"
    )
    new = (
        "        reply_text = reply_test_generate(prompt, model)\n"
        "        bio_signal = reply_test_calculate_biosignal(\n"
        "            persona_data,\n"
        "            message,\n"
        "            reply_text,\n"
        "            history_before,\n"
        "            summary_before,\n"
        "            rag_chunks,\n"
        "            model,\n"
        "        )\n\n"
        "        reply_test_append_history(session_id, persona_key, normalized_variant, \"player\", message)\n"
    )
    text = text.replace(old, new, 1)

    old_response = (
        "            \"replyText\": reply_text,\n"
        "            \"personaInput\": persona_data,\n"
    )
    new_response = (
        "            \"replyText\": reply_text,\n"
        "            \"bioSignal\": bio_signal,\n"
        "            \"personaInput\": persona_data,\n"
    )
    text = text.replace(old_response, new_response, 1)

    TARGET.write_text(text, encoding="utf-8")
    print(f"updated {TARGET}")


if __name__ == "__main__":
    main()
