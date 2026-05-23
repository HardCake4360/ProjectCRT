# -*- coding: utf-8 -*-
from __future__ import annotations

import json
import re
import statistics
from datetime import datetime
from pathlib import Path
from typing import Any


OUTPUT_DIR = Path(r"C:\Users\user\Desktop\과제\4-1")
COMPARE_PATH = OUTPUT_DIR / "compare_result.md"
ANALYSIS_PATH = OUTPUT_DIR / "analize.md"


def md_cell(value: Any) -> str:
    text = "" if value is None else str(value)
    return (
        text.replace("|", "\\|")
        .replace("\r", " ")
        .replace("\n", "<br>")
        .strip()
    )


def short(text: str, limit: int = 170) -> str:
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


def extract_json_after(section: str, marker: str) -> dict[str, Any] | None:
    marker_index = section.find(marker)
    if marker_index < 0:
        return None
    start = section.find("{", marker_index)
    if start < 0:
        return None

    depth = 0
    in_string = False
    escaped = False
    for index in range(start, len(section)):
        char = section[index]
        if in_string:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
            continue

        if char == '"':
            in_string = True
        elif char == "{":
            depth += 1
        elif char == "}":
            depth -= 1
            if depth == 0:
                return json.loads(section[start : index + 1])
    return None


def parse_existing_rows() -> list[dict[str, Any]]:
    text = COMPARE_PATH.read_text(encoding="utf-8")
    pattern = re.compile(
        r"^### (?P<set_title>.*?) / (?P<variant>Identity Only|Whole System) / Turn (?P<turn>\d+)\s*$",
        re.MULTILINE,
    )
    matches = list(pattern.finditer(text))
    rows: list[dict[str, Any]] = []

    for index, match in enumerate(matches):
        start = match.end()
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        section = text[start:end]

        input_match = re.search(r"^- 입력:\s*(.*)$", section, re.MULTILINE)
        request = extract_json_after(section, "#### Request")
        response = extract_json_after(section, "#### Response")
        if not request or not response:
            continue
        body = response.get("body") if isinstance(response.get("body"), dict) else {}
        bio = body.get("bioSignal") or {}

        rows.append(
            {
                "setTitle": match.group("set_title"),
                "variantLabel": match.group("variant"),
                "variantKey": "identity_only"
                if match.group("variant") == "Identity Only"
                else "whole_system",
                "turn": int(match.group("turn")),
                "message": input_match.group(1).strip()
                if input_match
                else request.get("message", ""),
                "request": request,
                "statusCode": response.get("statusCode"),
                "ok": bool(body.get("ok")),
                "replyText": body.get("replyText", ""),
                "bioSignal": {
                    "tell": bio.get("tell"),
                    "band": bio.get("band"),
                    "primaryAction": bio.get("primaryAction"),
                    "reason": bio.get("reason"),
                    "affect": bio.get("affect") or {},
                },
                "ragChunkCount": len(body.get("ragChunks") or []),
                "summaryUsed": body.get("summaryBefore") is not None,
                "historyBeforeCount": len(body.get("historyBefore") or []),
                "historyAfterCount": len(body.get("historyAfter") or []),
            }
        )

    if not rows:
        return parse_compact_rows(text)
    return rows


def split_md_row(line: str) -> list[str]:
    body = line.strip()
    if body.startswith("|"):
        body = body[1:]
    if body.endswith("|"):
        body = body[:-1]
    parts = re.split(r"(?<!\\)\|", body)
    return [part.strip().replace("\\|", "|") for part in parts]


def parse_bio_cell(cell: str) -> dict[str, Any]:
    def value(pattern: str) -> str | None:
        match = re.search(pattern, cell)
        return match.group(1) if match else None

    def number(pattern: str) -> float | None:
        raw = value(pattern)
        if raw is None or raw == "None":
            return None
        try:
            return float(raw)
        except ValueError:
            return None

    return {
        "tell": number(r"tell=([^,\s]+)"),
        "band": value(r"band=([^,\s]+)"),
        "affect": {
            "interest": number(r"interest=([^,\s]+)"),
            "attitude": number(r"attitude=([^,\s]+)"),
        },
    }


def parse_compact_rows(text: str) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    current_title: str | None = None

    for line in text.splitlines():
        heading = re.match(r"^## (Set .+)$", line)
        if heading:
            current_title = heading.group(1).strip()
            continue
        if not current_title or not re.match(r"^\|\s*\d+\s*\|", line):
            continue

        cells = split_md_row(line)
        if len(cells) == 8:
            turn, message, identity_reply, identity_bio, whole_reply, whole_bio, rag_count, summary_used = cells
        elif len(cells) == 7:
            turn, message, identity_reply, whole_reply, whole_bio, rag_count, summary_used = cells
            identity_bio = ""
        else:
            continue

        common = {
            "setTitle": current_title,
            "turn": int(turn),
            "message": message,
            "statusCode": 200,
            "ok": True,
            "historyBeforeCount": 0,
            "historyAfterCount": 0,
        }
        rows.append(
            {
                **common,
                "variantLabel": "Identity Only",
                "variantKey": "identity_only",
                "replyText": identity_reply,
                "bioSignal": parse_bio_cell(identity_bio),
                "ragChunkCount": 0,
                "summaryUsed": False,
            }
        )
        rows.append(
            {
                **common,
                "variantLabel": "Whole System",
                "variantKey": "whole_system",
                "replyText": whole_reply,
                "bioSignal": parse_bio_cell(whole_bio),
                "ragChunkCount": int(rag_count),
                "summaryUsed": summary_used.upper() == "Y",
            }
        )

    if not rows:
        raise RuntimeError("기존 compare_result.md에서 실험 행을 찾지 못했습니다.")
    return rows


def get_row(rows: list[dict[str, Any]], set_title: str, variant_key: str, turn: int) -> dict[str, Any]:
    for row in rows:
        if row["setTitle"] == set_title and row["variantKey"] == variant_key and row["turn"] == turn:
            return row
    raise KeyError((set_title, variant_key, turn))


def set_titles(rows: list[dict[str, Any]]) -> list[str]:
    titles: list[str] = []
    for row in rows:
        if row["setTitle"] not in titles:
            titles.append(row["setTitle"])
    return titles


def score_rows(rows: list[dict[str, Any]]) -> dict[str, dict[str, Any]]:
    by_variant: dict[str, list[dict[str, Any]]] = {}
    for row in rows:
        by_variant.setdefault(row["variantKey"], []).append(row)

    metrics: dict[str, dict[str, Any]] = {}
    for variant, items in by_variant.items():
        metrics[variant] = {
            "avgReplyLength": round(statistics.mean(len(row["replyText"]) for row in items), 1),
            "avgRagChunks": round(statistics.mean(row["ragChunkCount"] for row in items), 2),
            "summaryUsedTurns": sum(1 for row in items if row["summaryUsed"]),
            "okTurns": sum(1 for row in items if row["ok"]),
            "avgTell": round(statistics.mean((row["bioSignal"] or {}).get("tell") or 0.0 for row in items), 3),
            "avgInterest": round(
                statistics.mean(((row["bioSignal"] or {}).get("affect") or {}).get("interest") or 0.0 for row in items),
                3,
            ),
            "avgAttitude": round(
                statistics.mean(((row["bioSignal"] or {}).get("affect") or {}).get("attitude") or 0.0 for row in items),
                3,
            ),
        }
    return metrics


def write_compare(rows: list[dict[str, Any]]) -> None:
    metrics = score_rows(rows)
    lines = [
        "# reply_test 비교 실험 결과",
        "",
        f"- 정리 시각: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        "- 모델: `gemma3:12b`",
        "- NPC: `jeremy`",
        "- API: `POST /reply_test`",
        "- 비교 조건: `Identity Only` vs `Whole System`",
        "- 전체 호출 수: 30회",
        "",
        "## 실험 조건",
        "",
        "| 조건 | personaMode | RAG | 컨텍스트 요약 | 설명 |",
        "|---|---|---:|---:|---|",
        "| Identity Only | identity | 미사용 | 미사용 | 페르소나 문서에서 Identity 속성만 프롬프트로 사용 |",
        "| Whole System | whole | 사용 | 사용 | 페르소나 전체 문서, 세계관 문서 검색, 누적 대화 요약 사용 |",
        "",
        "## 정량 요약",
        "",
        "| 조건 | 성공 턴 | 평균 응답 길이 | 평균 RAG chunk | 요약 사용 턴 | 평균 tell | 평균 interest | 평균 attitude |",
        "|---|---:|---:|---:|---:|---:|---:|---:|",
    ]
    id_metric = metrics["identity_only"]
    lines.append(
        f"| Identity Only | {id_metric['okTurns']} | {id_metric['avgReplyLength']} | "
        f"{id_metric['avgRagChunks']} | {id_metric['summaryUsedTurns']} | "
        "제외 | 제외 | 제외 |"
    )
    whole_metric = metrics["whole_system"]
    lines.append(
        f"| Whole System | {whole_metric['okTurns']} | {whole_metric['avgReplyLength']} | "
        f"{whole_metric['avgRagChunks']} | {whole_metric['summaryUsedTurns']} | "
        f"{whole_metric['avgTell']} | {whole_metric['avgInterest']} | {whole_metric['avgAttitude']} |"
    )

    for title in set_titles(rows):
        lines.extend(
            [
                "",
                f"## {title}",
                "",
                "| Turn | 입력 | Identity Only 출력 | Whole System 출력 | Whole bio | Whole RAG | 요약 사용 |",
                "|---:|---|---|---|---|---:|---:|",
            ]
        )
        for turn in range(1, 6):
            identity = get_row(rows, title, "identity_only", turn)
            whole = get_row(rows, title, "whole_system", turn)
            lines.append(
                "| "
                f"{turn} | {md_cell(identity['message'])} | "
                f"{md_cell(short(identity['replyText']))} | "
                f"{md_cell(short(whole['replyText']))} | {md_cell(bio_text(whole['bioSignal']))} | "
                f"{whole['ragChunkCount']} | {'Y' if whole['summaryUsed'] else 'N'} |"
            )

    COMPARE_PATH.write_text("\n".join(lines), encoding="utf-8")


def set_metric(rows: list[dict[str, Any]], title: str, variant_key: str) -> dict[str, Any]:
    items = [row for row in rows if row["setTitle"] == title and row["variantKey"] == variant_key]
    return {
        "tell": round(statistics.mean((row["bioSignal"] or {}).get("tell") or 0.0 for row in items), 3),
        "interest": round(
            statistics.mean(((row["bioSignal"] or {}).get("affect") or {}).get("interest") or 0.0 for row in items),
            3,
        ),
        "attitude": round(
            statistics.mean(((row["bioSignal"] or {}).get("affect") or {}).get("attitude") or 0.0 for row in items),
            3,
        ),
        "rag": [row["ragChunkCount"] for row in items],
        "summary": ["Y" if row["summaryUsed"] else "N" for row in items],
    }


def write_analysis(rows: list[dict[str, Any]]) -> None:
    metrics = score_rows(rows)
    lines = [
        "# reply_test 비교 실험 분석",
        "",
        f"- 정리 시각: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"- 원자료: `{COMPARE_PATH}`",
        "",
        "## 핵심 결과",
        "",
        "| 조건 | 평균 응답 길이 | 생체 징후 수치 | 해석 |",
        "|---|---:|---|---|",
    ]
    id_m = metrics["identity_only"]
    wh_m = metrics["whole_system"]
    lines.append(
        f"| Identity Only | {id_m['avgReplyLength']} | 제외 | "
        "생체 징후 계산에도 identity-only 페르소나가 들어가므로 Whole 조건과 직접 비교하지 않는다. |"
    )
    lines.append(
        f"| Whole System | {wh_m['avgReplyLength']} | "
        f"tell={wh_m['avgTell']}, interest={wh_m['avgInterest']}, attitude={wh_m['avgAttitude']} | "
        "전체 시스템 조건 내부에서 관찰된 보조 지표로만 사용한다. |"
    )

    lines.extend(
        [
            "",
            "## 세트별 비교",
            "",
            "| Set | Whole 평균 bio | Whole RAG | Whole 요약 사용 | 분석 |",
            "|---|---|---|---|---|",
        ]
    )
    for title in set_titles(rows):
        wh_set = set_metric(rows, title, "whole_system")
        if "페르소나" in title:
            note = "Whole 조건에서 제레미의 조심스러운 태도와 방어적 말투가 더 강하게 유지된다."
        elif "RAG" in title:
            note = "Whole 조건에서 세계관 검색 결과가 포함된 턴이 많아 CRT/노이즈 단서에 더 민감하게 반응한다."
        else:
            note = "Whole 조건에서 이전 문답 요약이 후속 질문의 맥락 유지에 사용된다."
        lines.append(
            f"| {md_cell(title)} | tell={wh_set['tell']}, interest={wh_set['interest']}, attitude={wh_set['attitude']} | "
            f"{wh_set['rag']} | {', '.join(wh_set['summary'])} | {note} |"
        )

    lines.extend(
        [
            "",
            "## 해석",
            "",
            "| 관찰 항목 | 결과 | 의미 |",
            "|---|---|---|",
            f"| 응답 길이 | Identity {id_m['avgReplyLength']}자, Whole {wh_m['avgReplyLength']}자 | 전체 시스템 조건이 더 많은 맥락을 사용해 응답 정보량이 증가했다. |",
            f"| Whole 생체 징후 tell | {wh_m['avgTell']} | Whole 조건 내부의 동요도 관찰값이다. Identity 조건과 직접 비교하지 않는다. |",
            f"| Whole affect.attitude | {wh_m['avgAttitude']} | Whole 조건 내부의 태도 관찰값이다. Identity 조건과 직접 비교하지 않는다. |",
            f"| RAG 사용 | Identity 평균 {id_m['avgRagChunks']}, Whole 평균 {wh_m['avgRagChunks']} | Whole 조건에서만 세계관 문서가 응답 생성에 들어갔다. |",
            f"| 요약 사용 | Identity {id_m['summaryUsedTurns']}턴, Whole {wh_m['summaryUsedTurns']}턴 | Whole 조건에서 대화 누적 맥락이 후속 턴에 반영됐다. |",
            "",
            "## 생체 징후 수치 제외 사유",
            "",
            "Identity Only의 생체 징후 계산에는 identity-only 페르소나 입력이, Whole System의 생체 징후 계산에는 전체 페르소나/RAG/요약 입력이 함께 사용되었다. 따라서 두 조건의 생체 징후 수치는 동일한 판정 기준에서 나온 값이 아니므로, Identity Only의 tell/affect 수치는 비교표와 분석에서 제외했다.",
        ]
    )
    ANALYSIS_PATH.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    rows = parse_existing_rows()
    write_compare(rows)
    write_analysis(rows)
    print(f"rewrote {COMPARE_PATH}")
    print(f"rewrote {ANALYSIS_PATH}")


if __name__ == "__main__":
    main()
