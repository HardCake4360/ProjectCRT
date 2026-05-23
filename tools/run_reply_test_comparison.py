# -*- coding: utf-8 -*-
from __future__ import annotations

import json
import os
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

VARIANTS = [
    {
        "key": "identity_only",
        "label": "Identity Only",
        "variant": {
            "personaMode": "identity",
            "useRag": False,
            "useSummary": False,
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
            "updateSummary": True,
        },
    },
]

SETS = [
    {
        "setId": "set1_persona_consistency",
        "title": "Set 1. 페르소나 일관성",
        "purpose": "페르소나 전체 문서가 말투, 방어기제, 거리두기, 은유적 응답을 더 잘 유지하는지 확인한다.",
        "messages": [
            "그날 밤 바 안 분위기가 평소와 달랐나요?",
            "당신은 손님들의 표정을 자주 살피는 편이죠. 그날 유난히 이상한 사람은 없었나요?",
            "왜 계속 확실하게 말하지 않고 돌려 말하나요?",
            "당신이 보기엔 그날 밤 가장 불안해 보였던 사람은 누구였나요?",
            "지금까지 말한 걸 보면 당신은 뭔가 더 알고 있는 것 같습니다.",
        ],
    },
    {
        "setId": "set2_world_rag",
        "title": "Set 2. 세계관/RAG 반영",
        "purpose": "RAG 사용 조건에서 CRT, 노이즈, 사건 시간대, 세계관 문서 단서가 답변에 더 구체적으로 반영되는지 확인한다.",
        "messages": [
            "그날 밤 CRT 모니터에서 이상한 노이즈가 나왔다는 이야기를 들었나요?",
            "그 노이즈가 들린 시간대가 사건과 겹친다고 생각하나요?",
            "CRT 모니터 기록에는 신호가 흔들린 흔적이 있습니다. 이걸 어떻게 봐야 할까요?",
            "그때 모니터 근처에 있던 사람이나 물건을 기억하나요?",
            "노이즈, CRT, 사건 시간대를 연결하면 어떤 가능성이 남나요?",
        ],
    },
    {
        "setId": "set3_context_summary",
        "title": "Set 3. 컨텍스트 요약 유지",
        "purpose": "컨텍스트 요약 사용 조건에서 앞선 문답의 단서와 플레이어 의도를 후속 응답에 더 잘 유지하는지 확인한다.",
        "messages": [
            "우선 내가 확인한 건 CRT 노이즈와 사건 시간이 겹친다는 점입니다.",
            "그리고 제레미는 그 시간대에 손님이 많아서 정확히 기억나지 않는다고 했습니다.",
            "당신은 방금 내가 말한 두 가지 단서를 어떻게 연결하나요?",
            "그럼 지금 가장 먼저 다시 확인해야 할 것은 무엇이라고 보나요?",
            "지금까지의 대화를 바탕으로 내가 다음에 물어봐야 할 질문을 하나만 제안해 주세요.",
        ],
    },
]


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


def make_payload(
    set_id: str,
    variant_key: str,
    variant: dict[str, Any],
    turn: int,
    message: str,
) -> dict[str, Any]:
    return {
        "sessionId": f"compare_{set_id}_{variant_key}",
        "personaKey": PERSONA_KEY,
        "npcId": NPC_ID,
        "model": MODEL,
        "variant": variant,
        "resetSession": turn == 1,
        "message": message,
    }


def run_experiment() -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    total = len(SETS) * len(VARIANTS) * 5
    done = 0
    for item_set in SETS:
        for variant_info in VARIANTS:
            for index, message in enumerate(item_set["messages"], start=1):
                payload = make_payload(
                    item_set["setId"],
                    variant_info["key"],
                    variant_info["variant"],
                    index,
                    message,
                )
                started = time.time()
                output = post_reply_test(payload)
                elapsed = round(time.time() - started, 2)
                body = output.get("body") if isinstance(output.get("body"), dict) else {}
                rows.append(
                    {
                        "setId": item_set["setId"],
                        "setTitle": item_set["title"],
                        "setPurpose": item_set["purpose"],
                        "variantKey": variant_info["key"],
                        "variantLabel": variant_info["label"],
                        "turn": index,
                        "message": message,
                        "request": payload,
                        "response": output,
                        "elapsedSeconds": elapsed,
                        "replyText": body.get("replyText", ""),
                        "bioSignal": body.get("bioSignal") or {},
                        "ragChunkCount": len(body.get("ragChunks") or []),
                        "summaryBefore": body.get("summaryBefore"),
                        "summaryAfter": body.get("summaryAfter"),
                        "historyBeforeCount": len(body.get("historyBefore") or []),
                        "historyAfterCount": len(body.get("historyAfter") or []),
                    }
                )
                done += 1
                print(
                    f"completed {done}/{total} {item_set['setId']} "
                    f"{variant_info['key']} turn={index} status={output['statusCode']}",
                    flush=True,
                )
    return rows


def get_row(rows: list[dict[str, Any]], set_id: str, variant_key: str, turn: int) -> dict[str, Any]:
    for row in rows:
        if row["setId"] == set_id and row["variantKey"] == variant_key and row["turn"] == turn:
            return row
    raise KeyError((set_id, variant_key, turn))


def short(text: str, limit: int = 150) -> str:
    cleaned = (text or "").replace("\n", " ").strip()
    return cleaned[: limit - 3] + "..." if len(cleaned) > limit else cleaned


def write_compare_result(rows: list[dict[str, Any]]) -> None:
    lines: list[str] = []
    lines.extend(
        [
            "# reply_test 비교 실험 결과",
            "",
            f"- 작성 시각: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
            f"- 모델: `{MODEL}`",
            f"- NPC: `{PERSONA_KEY}`",
            "- API: `POST /reply_test`",
            "- 비교 조건: `Identity Only` vs `Whole System`",
            "- 전체 호출 수: 30회 = 2조건 x 3세트 x 5턴",
            "",
            "## 비교 조건",
            "",
            "| 조건 | personaMode | useRag | useSummary | updateSummary | 설명 |",
            "|---|---|---:|---:|---:|---|",
            "| Identity Only | identity | false | false | false | 페르소나 문서의 identity 속성만 사용한다. 세계관 문서와 요약은 사용하지 않는다. |",
            "| Whole System | whole | true | true | true | 페르소나 전체 문서, 세계관 문서 검색, 컨텍스트 요약을 함께 사용한다. |",
            "",
            "## 입력 세트",
            "",
            "| Set | 목적 | 5회 입력 |",
            "|---|---|---|",
        ]
    )
    for item_set in SETS:
        messages = "<br>".join(f"{idx}. {msg}" for idx, msg in enumerate(item_set["messages"], start=1))
        lines.append(f"| {item_set['title']} | {item_set['purpose']} | {messages} |")

    lines.extend(
        [
            "",
            "## 전체 응답 비교 요약",
            "",
            "| Set | Turn | 입력 | Identity Only 출력 | Identity Bio | Whole System 출력 | Whole Bio | Whole RAG chunks | Whole summaryBefore |",
            "|---|---:|---|---|---|---|---|---:|---|",
        ]
    )
    for item_set in SETS:
        for turn, message in enumerate(item_set["messages"], start=1):
            identity = get_row(rows, item_set["setId"], "identity_only", turn)
            whole = get_row(rows, item_set["setId"], "whole_system", turn)
            identity_bio = identity.get("bioSignal") or {}
            whole_bio = whole.get("bioSignal") or {}
            identity_bio_text = (
                f"tell={identity_bio.get('tell')}, "
                f"band={identity_bio.get('band')}, "
                f"affect={identity_bio.get('affect')}"
            )
            whole_bio_text = (
                f"tell={whole_bio.get('tell')}, "
                f"band={whole_bio.get('band')}, "
                f"affect={whole_bio.get('affect')}"
            )
            lines.append(
                f"| {item_set['title']} | {turn} | {message} | "
                f"{short(identity['replyText'])} | {identity_bio_text} | "
                f"{short(whole['replyText'])} | {whole_bio_text} | "
                f"{whole['ragChunkCount']} | {short(json.dumps(whole['summaryBefore'], ensure_ascii=False), 90)} |"
            )

    lines.extend(["", "## 상세 기록", ""])
    for row in rows:
        lines.extend(
            [
                f"### {row['setTitle']} / {row['variantLabel']} / Turn {row['turn']}",
                "",
                f"- 입력: {row['message']}",
                f"- HTTP status: {row['response']['statusCode']}",
                f"- 응답 시간: {row['elapsedSeconds']}초",
                f"- RAG chunk 수: {row['ragChunkCount']}",
                f"- bioSignal: {json.dumps(row.get('bioSignal') or {}, ensure_ascii=False)}",
                f"- historyBefore 개수: {row['historyBeforeCount']}",
                f"- historyAfter 개수: {row['historyAfterCount']}",
                "",
                "#### Request",
                "```json",
                json.dumps(row["request"], ensure_ascii=False, indent=2),
                "```",
                "",
                "#### Response",
                "```json",
                json.dumps(row["response"], ensure_ascii=False, indent=2),
                "```",
                "",
            ]
        )

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    COMPARE_PATH.write_text("\n".join(lines), encoding="utf-8")


def score_rows(rows: list[dict[str, Any]]) -> dict[str, Any]:
    by_variant: dict[str, list[dict[str, Any]]] = {}
    for row in rows:
        by_variant.setdefault(row["variantKey"], []).append(row)
    return {
        variant: {
            "avgReplyLength": round(statistics.mean(len(row["replyText"]) for row in items), 1),
            "avgRagChunks": round(statistics.mean(row["ragChunkCount"] for row in items), 2),
            "summaryUsedTurns": sum(1 for row in items if row["summaryBefore"] is not None),
            "okTurns": sum(1 for row in items if row["response"]["statusCode"] == 200 and row["response"]["body"].get("ok")),
            "avgTell": round(
                statistics.mean((row.get("bioSignal") or {}).get("tell", 0.0) for row in items),
                3,
            ),
            "avgInterest": round(
                statistics.mean(((row.get("bioSignal") or {}).get("affect") or {}).get("interest", 0.0) for row in items),
                3,
            ),
            "avgAttitude": round(
                statistics.mean(((row.get("bioSignal") or {}).get("affect") or {}).get("attitude", 0.0) for row in items),
                3,
            ),
        }
        for variant, items in by_variant.items()
    }


def write_analysis(rows: list[dict[str, Any]]) -> None:
    metrics = score_rows(rows)
    set_notes = []
    for item_set in SETS:
        identity_replies = [
            get_row(rows, item_set["setId"], "identity_only", turn)["replyText"]
            for turn in range(1, 6)
        ]
        whole_replies = [
            get_row(rows, item_set["setId"], "whole_system", turn)["replyText"]
            for turn in range(1, 6)
        ]
        whole_rag = [
            get_row(rows, item_set["setId"], "whole_system", turn)["ragChunkCount"]
            for turn in range(1, 6)
        ]
        whole_summary = [
            get_row(rows, item_set["setId"], "whole_system", turn)["summaryBefore"] is not None
            for turn in range(1, 6)
        ]
        identity_bio = [
            get_row(rows, item_set["setId"], "identity_only", turn).get("bioSignal") or {}
            for turn in range(1, 6)
        ]
        whole_bio = [
            get_row(rows, item_set["setId"], "whole_system", turn).get("bioSignal") or {}
            for turn in range(1, 6)
        ]
        set_notes.append(
            {
                "title": item_set["title"],
                "identityExamples": identity_replies,
                "wholeExamples": whole_replies,
                "wholeRagCounts": whole_rag,
                "wholeSummaryUsed": whole_summary,
                "identityBio": identity_bio,
                "wholeBio": whole_bio,
            }
        )

    lines = [
        "# reply_test 비교 실험 분석",
        "",
        f"- 작성 시각: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"- 원자료: `{COMPARE_PATH}`",
        "",
        "## 핵심 결론",
        "",
        "이번 실험은 페르소나 identity만 제공한 기준선과, 페르소나 전체 문서/RAG/컨텍스트 요약을 함께 제공한 조건을 같은 15개 입력으로 비교했다. 분석의 초점은 응답의 인물성, 세계관 정보 반영, 이전 대화 유지 여부다.",
        "",
        "## 정량 요약",
        "",
        "```json",
        json.dumps(metrics, ensure_ascii=False, indent=2),
        "```",
        "",
        "## 세트별 분석",
        "",
    ]

    for note in set_notes:
        lines.append(f"### {note['title']}")
        lines.append("")
        lines.append(f"- Whole System RAG chunk 수: {note['wholeRagCounts']}")
        lines.append(f"- Whole System summaryBefore 사용 여부: {note['wholeSummaryUsed']}")
        lines.append(f"- Identity Only bioSignal: {json.dumps(note['identityBio'], ensure_ascii=False)}")
        lines.append(f"- Whole System bioSignal: {json.dumps(note['wholeBio'], ensure_ascii=False)}")
        lines.append(f"- Identity Only 대표 응답 1턴: {note['identityExamples'][0]}")
        lines.append(f"- Whole System 대표 응답 1턴: {note['wholeExamples'][0]}")
        lines.append("")

    lines.extend(
        [
            "## 종합 해석",
            "",
            "1. `Identity Only` 조건은 NPC의 이름, 역할, 배경 정도만 제공되므로 기본적인 직업 정체성은 유지하지만, 말투나 방어기제 같은 세부 페르소나 특성은 약해질 가능성이 크다.",
            "2. `Whole System` 조건은 페르소나 전체 문서를 사용하므로 응답의 말투, 태도, 정보 공개 방식이 더 구체화되는지 확인할 수 있다.",
            "3. `Whole System`의 `ragChunks`가 1개 이상인 턴에서는 세계관 문서가 실제 프롬프트에 포함된다. 해당 턴의 응답이 CRT, 노이즈, 사건 시간대 같은 문서 단서를 더 구체적으로 언급하는지 비교해야 한다.",
            "4. `summaryBefore`는 2턴 이후부터 이전 턴의 요약이 들어갔는지 보여준다. Whole System 조건에서 뒤쪽 턴이 앞선 단서와 플레이어의 목표를 더 잘 이어받는다면 컨텍스트 요약의 효과를 주장할 수 있다.",
            "5. 이번 재실험은 `bioSignal.tell`과 `bioSignal.affect`도 함께 기록하므로, 대사 품질뿐 아니라 질문에 따른 동요와 태도 변화가 조건별로 달라지는지도 비교할 수 있다.",
            "",
            "## 논문에 쓸 수 있는 요약 문장",
            "",
            "> 동일 입력에 대해 identity-only 기준선과 전체 시스템 조건을 비교한 결과, 전체 시스템 조건은 페르소나 전체 문서, 세계관 문서 검색 결과, 누적 대화 요약을 함께 사용함으로써 NPC 응답과 생체 징후 산출이 더 많은 맥락 정보를 참조하도록 구성되었다. 특히 RAG chunk와 summaryBefore가 포함된 턴에서는 모델이 현재 질문뿐 아니라 사건 정보와 이전 문답 흐름을 동시에 참조할 수 있어, 단발성 응답보다 조사 대화의 연속성과 반응 해석 단서를 강화하는 구조를 확인할 수 있었다.",
            "",
            "## 한계",
            "",
            "- `server_for_experiment.py`의 RAG는 실험 안정성을 위해 SentenceTransformer/FAISS가 아니라 세계관 문서에 대한 경량 토큰 검색으로 구현했다. 따라서 이 실험은 RAG 개념의 사용 여부 비교에는 적합하지만, 벡터 검색 품질 자체를 검증하지는 않는다.",
            "- 응답 평가는 아직 자동 점수화가 아니라 결과 문서 기반의 정성 분석이다. 논문 표에는 페르소나 일관성, 세계관 정확성, 컨텍스트 유지, 정보 구체성, 환각 억제 항목을 5점 척도로 추가 평가하면 더 설득력이 높다.",
        ]
    )

    ANALYSIS_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    rows = run_experiment()
    write_compare_result(rows)
    write_analysis(rows)
    print(f"WROTE {COMPARE_PATH}")
    print(f"WROTE {ANALYSIS_PATH}")


if __name__ == "__main__":
    main()
