# Server Investigation Recovery Spec

## 목적

기존 `server.py` 원본을 기준으로, Unity 조사 파트와 연결되는 `NPC 조사 전용 서버 기능`을 다시 구현하기 위한 명세서다.

이 문서는 다음을 목표로 한다.

- 기존 `/ask-stream` 기능을 유지한다.
- 신규 `POST /investigation/npc` 엔드포인트를 추가한다.
- 바텐더 `리베카(Rebecca)` 조사 상호작용을 지원한다.
- 조사 결과를 Unity 클라이언트가 바로 소비할 수 있는 형태로 반환한다.

## 대상 파일

- 서버 메인: `virtualEnv/RAG_model/app/server.py`
- 페르소나 파일: `virtualEnv/RAG_model/app/data/personas/rebecca.json`

## 구현 범위

추가해야 할 기능은 아래와 같다.

1. `POST /investigation/npc` 엔드포인트 추가
2. 조사 전용 요청 payload 파싱
3. 조사 상황용 프롬프트 생성 함수 추가
4. LLM 응답을 조사 시스템 응답 형식으로 변환
5. `signal` 계산 로직 추가
6. `stateDelta` 생성 로직 추가
7. `personaKey = rebecca` 지원
8. 조사용 로그 기록 추가

## 엔드포인트

### `POST /investigation/npc`

조사 UI에서 NPC와 상호작용할 때 호출하는 전용 엔드포인트다.

## 요청 스펙

### 요청 JSON 예시

```json
{
  "sceneId": "bar_murder_case_01",
  "playerId": "player",
  "npcId": "rebecca",
  "personaKey": "rebecca",
  "phase": "investigation",
  "interaction": {
    "actionType": "Talk",
    "playerIntentText": "그날 밤 바 안 분위기가 어땠는지 묻는다.",
    "topicId": null,
    "evidenceId": null
  },
  "sceneState": {
    "knownClues": [],
    "unlockedTopics": []
  },
  "npcLocalState": {
    "conversationCount": 1,
    "lastKnownSignal": {
      "stress": 0.2,
      "distortion": 0.1,
      "focus": 0.7
    }
  },
  "conversationContext": {
    "recentExchanges": [
      {
        "speaker": "npc",
        "text": "어서 와. 오늘은 평소보다 질문이 많겠네."
      }
    ]
  }
}
```

### 요청 필드 설명

- `sceneId`: 현재 사건 또는 씬 식별자
- `playerId`: 플레이어 로그 식별자
- `npcId`: 대상 NPC 식별자
- `personaKey`: 페르소나 로딩 키
- `phase`: 현재 단계. 기본값은 `investigation`
- `interaction.actionType`: `Talk`, `AskTopic`, `PresentEvidence` 중 하나
- `interaction.playerIntentText`: 플레이어가 하려는 질문 또는 행동의 자연어 설명
- `interaction.topicId`: 특정 주제 질문 시 사용
- `interaction.evidenceId`: 증거 제시 시 사용
- `sceneState`: 현재 사건 진행 맥락
- `npcLocalState`: 클라이언트가 보관 중인 NPC 상태
- `conversationContext.recentExchanges`: 최근 대화 이력

## 응답 스펙

### 응답 JSON 예시

```json
{
  "ok": true,
  "replyText": "그날은 평소보다 다들 소리에 예민했어. 특히 어떤 손님은 작은 잡음에도 지나치게 반응했지.",
  "signal": {
    "stress": 0.36,
    "distortion": 0.21,
    "focus": 0.68
  },
  "stateDelta": {
    "unlockTopicIds": [
      "noise_time"
    ],
    "markStatements": [
      {
        "statementId": "rebecca_stmt_002",
        "text": "그날은 평소보다 다들 소리에 예민했어. 특히 어떤 손님은 작은 잡음에도 지나치게 반응했지."
      }
    ]
  },
  "presentationHints": {
    "animation": "guarded_idle",
    "voiceTone": "calm",
    "uiNoiseLevel": 0.21
  }
}
```

### 응답 필드 설명

- `ok`: 성공 여부
- `replyText`: NPC 대사 본문
- `signal.stress`: 긴장도
- `signal.distortion`: 왜곡 또는 흔들림 정도
- `signal.focus`: 집중도 또는 침착함
- `stateDelta.unlockTopicIds`: 새로 열린 조사 주제
- `stateDelta.markStatements`: 기록할 진술
- `presentationHints.animation`: Unity 애니메이션 힌트
- `presentationHints.voiceTone`: 음성 또는 톤 연출 힌트
- `presentationHints.uiNoiseLevel`: UI 연출용 수치

## 에러 응답

예외 발생 시 아래 구조를 반환한다.

```json
{
  "ok": false,
  "error": "error message",
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
}
```

## 추가 함수 명세

### `_build_investigation_prompt(payload, persona)`

입력:

- 조사 요청 JSON
- `personaKey`로 로딩한 persona 데이터

역할:

- 리베카의 캐릭터성을 반영한다.
- 메타 발언을 금지한다.
- 정답 직접 공개를 금지한다.
- 짧은 대사를 유도한다.
- 조사 문맥과 최근 대화를 포함한다.
- 반드시 한국어 응답을 유도한다.

### `_safe_reply_text(text)`

역할:

- LLM 응답 후처리
- 불필요한 종료 토큰 제거
- 공백 정리
- 빈 문자열이면 `"..."` 반환

### `_derive_signal(action_type, last_signal, reply_text)`

역할:

- 현재 액션 종류와 이전 signal 상태를 기반으로 새 signal 계산

권장 규칙:

- `Talk`: 변화가 작다
- `AskTopic`: 중간 수준 변화
- `PresentEvidence`: `stress`, `distortion` 상승폭이 가장 크다

추가 보정:

- `노이즈`, `보청기`, `CRT`, 불확실성 표현이 있으면 긴장도 상승 가능
- `분명`, `확실`, `봤다`, `기억한다` 같은 표현이 있으면 focus 보정 가능

### `_extract_unlock_topics(interaction, reply_text)`

역할:

- 액션 종류와 응답 내용을 보고 새 topic ID 추출

예시 규칙:

- `노이즈` 관련 언급 시 `noise_time`
- `보청기` 관련 언급 시 `hearing_aid_amplification_possibility`
- `CRT` 관련 언급 시 `crt_signal_origin`
- `PresentEvidence` 액션에서 `evidenceId`가 있으면 `${evidenceId}_followup` 해금 가능

## 프롬프트 요구사항

리베카 프롬프트는 아래 성격을 만족해야 한다.

- 바텐더로서 관찰자 시점 유지
- 범인처럼 굴지 않음
- 직접적인 진실 공개 금지
- 질문에 짧고 건조하게 응답
- 분위기, 표정, 반응, 소리 같은 간접 힌트 선호
- 모르면 모른다고 하거나 애매하게 반응
- 반드시 한국어 응답

## 로그 처리

기존 `append_log`를 재사용한다.

- 유저 액션은 `user` speaker로 기록 가능
- NPC 응답은 `rebecca` speaker로 기록
- 일반 `/ask-stream` 흐름과 분리되더라도 조사 응답은 로그에 남겨야 한다

## Rebecca Persona 파일

### 파일 경로

- `virtualEnv/RAG_model/app/data/personas/rebecca.json`

### 최소 예시 구조

```json
{
  "identity": {
    "name": "리베카",
    "role": "Bar CRT의 바텐더"
  },
  "personality": {
    "tone": "짧고 건조하며 관찰적"
  },
  "behavioral_constraints": {
    "avoid_meta": true,
    "avoid_out_of_world_info": true,
    "max_reply_tokens": 220
  }
}
```

## 비기능 요구사항

- 기존 `/ask-stream` 동작은 유지되어야 한다
- 기존 요약 및 로그 기능과 충돌하지 않아야 한다
- Python 3 기준으로 동작해야 한다
- 파일 저장 인코딩은 `UTF-8`을 사용한다
- 가능하면 `UTF-8 without BOM` 사용을 권장한다
- 한글 문자열을 포함하더라도 저장 인코딩을 바꾸지 않는다

## 권장 구현 순서

1. 원본 `server.py`를 기준으로 백업
2. `import json` 추가 여부 확인
3. helper 함수 4종 추가
4. `/investigation/npc` 엔드포인트 추가
5. `rebecca.json` 추가 또는 복구
6. UTF-8 저장 확인
7. `/ask-stream` 기존 기능 회귀 테스트
8. `/investigation/npc` 수동 요청 테스트

## 테스트 체크리스트

- `/ask-stream`가 기존처럼 응답하는가
- `/investigation/npc`가 200 OK를 반환하는가
- `replyText`가 비어 있지 않은가
- `signal` 세 필드가 모두 숫자인가
- `stateDelta.unlockTopicIds`가 배열인가
- `stateDelta.markStatements`가 배열인가
- `personaKey = rebecca`일 때 리베카다운 어조가 나오는가
- 한글이 파일에서 깨지지 않는가
