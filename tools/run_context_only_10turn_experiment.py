# -*- coding: utf-8 -*-
from __future__ import annotations

import json
import re
import statistics
import time
from datetime import datetime
from pathlib import Path
from typing import Any

import requests


BASE_URL = "http://127.0.0.1:5000"
MODEL = "gemma3:12b"
PERSONA_KEY = "jeremy"
NPC_ID = "jeremy"
OUTPUT_DIR = Path(r"C:\Users\user\Desktop\과제\4-1")
COMPARE_PATH = OUTPUT_DIR / "compare_result.md"
ANALYSIS_PATH = OUTPUT_DIR / "analize.md"

MESSAGES = [
    "우선 두 가지 단서를 정리하겠습니다. 첫째, CRT 화면 이상 노이즈는 사건 추정 시간 직전에 발생했습니다. 둘째, 제레미는 그 시간대에 바 근처에 있었지만 정확한 손님 수를 기억하지 못한다고 했습니다. 이 두 단서를 어떻게 이해해야 할까요?",
    "방금 말한 첫 번째 단서가 왜 중요한지 다시 설명해 주세요.",
    "그럼 두 번째 단서는 첫 번째 단서와 어떤 점에서 연결되나요?",
    "지금까지의 답을 기준으로, 제가 다음에 확인해야 할 대상은 누구인가요? 이름이나 역할로 말해 주세요.",
    "그 대상에게 먼저 물어봐야 할 질문을 하나만 제안해 주세요.",
    "앞에서 말한 첫 번째 단서가 조작이 아니라 관찰 기록이라고 가정하면, 두 번째 단서의 의미는 어떻게 달라지나요?",
    "반대로 첫 번째 단서가 단순 오류라면, 두 번째 단서를 어떻게 다시 해석해야 하나요?",
    "이제 두 가능성 중 더 먼저 확인할 쪽을 고르고 이유를 말해 주세요.",
    "그 판단을 바탕으로 제레미의 진술에서 아직 비어 있는 부분을 하나만 짚어 주세요.",
    "마지막으로, 앞선 두 단서와 지금 고른 확인 방향을 한 문장으로 묶어서 사건 가능성을 제시해 주세요.",
]

VARIANTS = [
    {
        "key": "identity_only",
        "label": "Identity Only",
        "variant": {
            "personaMode": "identity",
            "useRag": False,
            "useSummary": False,
            "useHistory": False,
            "updateSummary": False,
        },
    },
    {
        "key": "whole_system",
        "label": "Whole System",
        "variant": {
            "personaMode": "whole",
            "useRag": True,
            "useSummary": True,
            "useHistory": True,
            "updateSummary": True,
        },
    },
]

EXPECTATIONS = {
    2: ["CRT", "노이즈", "화면", "기록"],
    3: ["제레미", "바", "손님", "기억", "시간대"],
    4: ["제레미", "바", "손님", "직원", "목격자"],
    5: ["질문", "물어", "확인"],
    6: ["CRT", "노이즈", "기록", "제레미", "바", "손님", "기억"],
    7: ["오류", "제레미", "바", "손님", "기억"],
    8: ["확인", "먼저", "가능성", "이유"],
    9: ["제레미", "진술", "비어", "기억", "손님", "시간대"],
    10: ["CRT", "노이즈", "제레미", "확인", "가능성"],
}


def post_reply_test(payload: dict[str, Any], timeout: int = 240) -> dict[str, Any]:
    response = requests.post(f"{BASE_URL}/reply_test", json=payload, timeout=timeout)
    try:
        body = response.json()
    except Exception:
        body = {"raw": response.text}
    return {
        "statusCode": response.status_code,
        "contentType": response.headers.get("content-type", ""),
        "body": body,
    }


def make_payload(variant_key: str, variant: dict[str, Any], turn: int, message: str) -> dict[str, Any]:
    return {
        "sessionId": f"context10_{variant_key}_{int(time.time())}",
        "personaKey": PERSONA_KEY,
        "npcId": NPC_ID,
        "model": MODEL,
        "variant": variant,
        "resetSession": turn == 1,
        "message": message,
    }


def md_cell(value: Any) -> str:
    text = "" if value is None else str(value)
    return text.replace("|", "\\|").replace("\r", " ").replace("\n", "<br>").strip()


def short(text: str, limit: int = 190) -> str:
    cleaned = re.sub(r"\s+", " ", text or "").strip()
    return cleaned[: limit - 3] + "..." if len(cleaned) > limit else cleaned


def bio_text(bio: dict[str, Any]) -> str:
    affect = bio.get("affect") or {}
    return (
        f"tell={bio.get('tell')}, "
        f"band={bio.get('band')}, "
        f"interest={affect.get('interest')}, "
        f"attitude={affect.get('attitude')}"
    )


def context_score(turn: int, reply: str) -> int:
    if turn == 1:
        return 1
    keywords = EXPECTATIONS.get(turn, [])
    if not keywords:
        return 0
    return min(2, sum(1 for keyword in keywords if keyword in reply))


def run_experiment() -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    total = len(MESSAGES) * len(VARIANTS)
    done = 0
    run_id = datetime.now().strftime("%Y%m%d_%H%M%S")
    for variant_info in VARIANTS:
        for turn, message in enumerate(MESSAGES, start=1):
            payload = {
                **make_payload(variant_info["key"], variant_info["variant"], turn, message),
                "sessionId": f"context10_{run_id}_{variant_info['key']}",
            }
            started = time.time()
            output = post_reply_test(payload)
            elapsed = round(time.time() - started, 2)
            body = output.get("body") if isinstance(output.get("body"), dict) else {}
            reply_text = body.get("replyText", "")
            rows.append(
                {
                    "turn": turn,
                    "message": message,
                    "variantKey": variant_info["key"],
                    "variantLabel": variant_info["label"],
                    "request": payload,
                    "statusCode": output["statusCode"],
                    "ok": bool(body.get("ok")),
                    "elapsedSeconds": elapsed,
                    "replyText": reply_text,
                    "bioSignal": body.get("bioSignal") or {},
                    "ragChunkCount": len(body.get("ragChunks") or []),
                    "summaryUsed": body.get("summaryBefore") is not None,
                    "historyBeforeCount": len(body.get("historyBefore") or []),
                    "historyAfterCount": len(body.get("historyAfter") or []),
                    "contextScore": context_score(turn, reply_text),
                }
            )
            done += 1
            print(
                f"completed {done}/{total} {variant_info['key']} turn={turn} "
                f"status={output['statusCode']}",
                flush=True,
            )
    return rows


def get_row(rows: list[dict[str, Any]], variant_key: str, turn: int) -> dict[str, Any]:
    for row in rows:
        if row["variantKey"] == variant_key and row["turn"] == turn:
            return row
    raise KeyError((variant_key, turn))


def variant_rows(rows: list[dict[str, Any]], variant_key: str) -> list[dict[str, Any]]:
    return [row for row in rows if row["variantKey"] == variant_key]


def write_compare(rows: list[dict[str, Any]]) -> None:
    id_rows = variant_rows(rows, "identity_only")
    wh_rows = variant_rows(rows, "whole_system")
    lines = [
        "# reply_test 문맥 유지 재실험 결과",
        "",
        f"- 작성 시각: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"- 모델: `{MODEL}`",
        f"- NPC: `{PERSONA_KEY}`",
        "- 범위: 문맥 유지 실험 10턴",
        "- 전체 호출 수: 20회 = 2조건 x 10턴",
        "",
        "## 실험 조건",
        "",
        "| 조건 | personaMode | RAG | 요약 | history | 생체 징후 해석 |",
        "|---|---|---:|---:|---:|---|",
        "| Identity Only | identity | 미사용 | 미사용 | 미사용 | 비교에서 제외 |",
        "| Whole System | whole | 사용 | 사용 | 사용 | 조건 내부 보조 지표로만 기록 |",
        "",
        "## 정량 요약",
        "",
        "| 조건 | 성공 턴 | 평균 응답 길이(문자 수) | 평균 문맥 점수 | RAG 사용 턴 | 요약 사용 턴 | history 사용 턴 |",
        "|---|---:|---:|---:|---:|---:|---:|",
        (
            f"| Identity Only | {sum(1 for r in id_rows if r['ok'])} | "
            f"{statistics.mean(len(r['replyText']) for r in id_rows):.1f} | "
            f"{statistics.mean(r['contextScore'] for r in id_rows):.2f} | "
            f"{sum(1 for r in id_rows if r['ragChunkCount'] > 0)} | "
            f"{sum(1 for r in id_rows if r['summaryUsed'])} | "
            f"{sum(1 for r in id_rows if r['historyBeforeCount'] > 0)} |"
        ),
        (
            f"| Whole System | {sum(1 for r in wh_rows if r['ok'])} | "
            f"{statistics.mean(len(r['replyText']) for r in wh_rows):.1f} | "
            f"{statistics.mean(r['contextScore'] for r in wh_rows):.2f} | "
            f"{sum(1 for r in wh_rows if r['ragChunkCount'] > 0)} | "
            f"{sum(1 for r in wh_rows if r['summaryUsed'])} | "
            f"{sum(1 for r in wh_rows if r['historyBeforeCount'] > 0)} |"
        ),
        "",
        "## 10턴 비교표",
        "",
        "| Turn | 입력 | Identity Only 출력 | Identity 문맥 점수 | Whole System 출력 | Whole 문맥 점수 | Whole RAG | Whole 요약 | Whole bio |",
        "|---:|---|---|---:|---|---:|---:|---:|---|",
    ]
    for turn, message in enumerate(MESSAGES, start=1):
        identity = get_row(rows, "identity_only", turn)
        whole = get_row(rows, "whole_system", turn)
        lines.append(
            f"| {turn} | {md_cell(message)} | "
            f"{md_cell(short(identity['replyText']))} | {identity['contextScore']} | "
            f"{md_cell(short(whole['replyText']))} | {whole['contextScore']} | "
            f"{whole['ragChunkCount']} | {'Y' if whole['summaryUsed'] else 'N'} | "
            f"{md_cell(bio_text(whole['bioSignal']))} |"
        )

    lines.extend(
        [
            "",
            "## 입력 전체",
            "",
            "| Turn | 입력 | 평가 의도 |",
            "|---:|---|---|",
            "| 1 | " + md_cell(MESSAGES[0]) + " | 이후 턴에서 참조할 두 단서 주입 |",
            "| 2 | " + md_cell(MESSAGES[1]) + " | 첫 번째 단서가 CRT 노이즈임을 회수해야 함 |",
            "| 3 | " + md_cell(MESSAGES[2]) + " | 두 번째 단서가 제레미의 위치/기억 불확실성임을 회수해야 함 |",
            "| 4 | " + md_cell(MESSAGES[3]) + " | 앞선 단서를 바탕으로 확인 대상을 특정해야 함 |",
            "| 5 | " + md_cell(MESSAGES[4]) + " | 직전 대상에 대한 후속 질문을 구성해야 함 |",
            "| 6 | " + md_cell(MESSAGES[5]) + " | 첫 번째 단서의 가정 변화가 두 번째 단서 해석에 미치는 영향을 설명해야 함 |",
            "| 7 | " + md_cell(MESSAGES[6]) + " | 반대 가정에서도 두 번째 단서를 유지해 재해석해야 함 |",
            "| 8 | " + md_cell(MESSAGES[7]) + " | 누적된 두 가능성 중 우선 확인 방향을 골라야 함 |",
            "| 9 | " + md_cell(MESSAGES[8]) + " | 선택한 방향을 제레미 진술의 빈칸과 연결해야 함 |",
            "| 10 | " + md_cell(MESSAGES[9]) + " | 두 단서와 확인 방향을 한 문장으로 종합해야 함 |",
        ]
    )

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    COMPARE_PATH.write_text("\n".join(lines), encoding="utf-8")


def write_analysis(rows: list[dict[str, Any]]) -> None:
    id_rows = variant_rows(rows, "identity_only")
    wh_rows = variant_rows(rows, "whole_system")
    id_avg = statistics.mean(row["contextScore"] for row in id_rows)
    wh_avg = statistics.mean(row["contextScore"] for row in wh_rows)
    wh_tell = statistics.mean((row["bioSignal"] or {}).get("tell") or 0.0 for row in wh_rows)
    wh_attitude = statistics.mean(((row["bioSignal"] or {}).get("affect") or {}).get("attitude") or 0.0 for row in wh_rows)

    lines = [
        "# reply_test 문맥 유지 재실험 분석",
        "",
        f"- 작성 시각: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"- 원자료: `{COMPARE_PATH}`",
        "",
        "## 핵심 결과",
        "",
        "| 조건 | history | 요약 | 평균 문맥 점수 | 해석 |",
        "|---|---:|---:|---:|---|",
        f"| Identity Only | 미사용 | 미사용 | {id_avg:.2f} | 현재 입력만으로 답해야 하므로 지시어 회수와 누적 추론이 제한된다. |",
        f"| Whole System | 사용 | 사용 | {wh_avg:.2f} | 이전 문답과 요약을 통해 첫 번째/두 번째 단서, 확인 대상, 후속 질문 흐름을 유지할 수 있다. |",
        "",
        "## 턴별 문맥 점수",
        "",
        "| Turn | Identity Only | Whole System | 차이 |",
        "|---:|---:|---:|---:|",
    ]
    for turn in range(1, 11):
        identity = get_row(rows, "identity_only", turn)
        whole = get_row(rows, "whole_system", turn)
        lines.append(
            f"| {turn} | {identity['contextScore']} | {whole['contextScore']} | "
            f"{whole['contextScore'] - identity['contextScore']} |"
        )

    lines.extend(
        [
            "",
            "## 관찰",
            "",
            "| 항목 | 결과 | 의미 |",
            "|---|---|---|",
            f"| 평균 응답 길이 | Identity {statistics.mean(len(r['replyText']) for r in id_rows):.1f}자, Whole {statistics.mean(len(r['replyText']) for r in wh_rows):.1f}자 | Whole 조건이 더 많은 맥락을 반영해 응답 정보량이 증가했는지 확인할 수 있다. |",
            f"| RAG 사용 | Whole 조건 {sum(1 for r in wh_rows if r['ragChunkCount'] > 0)}/10턴 | 세계관 문서가 일부 턴에서 응답 생성에 투입되었다. |",
            f"| 요약 사용 | Whole 조건 {sum(1 for r in wh_rows if r['summaryUsed'])}/10턴 | 2턴 이후 대부분에서 누적 요약이 사용되었다. |",
            f"| Whole 생체 징후 | 평균 tell={wh_tell:.3f}, 평균 attitude={wh_attitude:.3f} | Whole 조건 내부의 보조 관찰값이며 Identity와 직접 비교하지 않는다. |",
            "",
            "## 해석상 주의",
            "",
            "이번 재실험에서는 Identity Only 조건에서 history를 명시적으로 끄도록 `reply_test`에 `useHistory=false`를 사용했다. 따라서 기존 실험보다 컨텍스트 요약 기능의 효과를 분리해서 보기 쉽다. 다만 문맥 점수는 키워드 회수 기반의 간이 지표이므로, 논문 본문에서는 대표 턴의 실제 응답을 함께 제시하는 것이 좋다.",
        ]
    )
    ANALYSIS_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    rows = run_experiment()
    write_compare(rows)
    write_analysis(rows)
    print(f"WROTE {COMPARE_PATH}")
    print(f"WROTE {ANALYSIS_PATH}")


if __name__ == "__main__":
    main()
