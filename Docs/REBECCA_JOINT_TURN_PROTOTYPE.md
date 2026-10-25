# 레베카 대사·TII 통합 생성 시제품

작업일: 2026-10-23 (Asia/Seoul).

## 동작과 사용

Python 게이트웨이를 유지하고 레베카의 대사·Affect·patience·tell을 한 번의 OpenAI Responses 요청으로 생성한다. NPC ID와 personaKey가 모두 `rebecca`일 때만 새 `/investigation/npc/turn`을 사용한다. 다른 NPC 및 터미널은 기존 경로를 유지한다.

1. 평소 사용하던 외부 서버 진입점을 다시 실행한다. `server.py`, `server_test.py`, `server_for_experiment.py`에 같은 새 경로를 등록했다. 실행 중인 서버를 강제로 종료하거나 재시작하지 않았다.
2. `Assets/Scenes/Shangrila_CaseTest.unity` 등 기존 레베카가 배치된 씬에서 대화한다. 기존 조사 UI·첨부·Tell/Affect 표시를 사용하며 씬·프리팹 재배치는 필요하지 않다.
3. 정상 결과를 전부 받아 검증한 뒤 질문 전송으로부터 **최소 1초**가 지나면 대사와 TII를 함께 표시하고 저장한다. 생성이 1초보다 길면 완료까지 기다린다. JSON 중간 조각이나 미검증 대사를 표시하지 않는다.

롤백/비교: 해당 `NpcInvestigationClient`의 Inspector에서 `Enable Rebecca Joint Turn`을 끄면 레베카도 기존 reply/tell 두 요청으로 돌아간다. 자동 실패 전환은 하지 않는다. 새 서버가 실행되지 않으면 실패 안내 후 입력을 복구한다.

## 구현 범위

- 새 요청은 protocolVersion=`rebecca-joint-v1`, runtime sessionId, turnId와 기존 맥락을 보낸다. 서버가 같은 sessionId/turnId에 다른 자료를 받으면 충돌로 거절하고 완료 결과는 최대 128개·10분 동안 메모리에서 재사용한다.
- `ShangrilaCaseManager`가 있으면 현재 단계 값을 요청에 반영한다. 현재 도착/자유 조사 단계의 기반을 사용하며 전체 살인 사건 상태머신을 새로 구현한 것은 아니다.
- 서버 모델·키는 기존 게이트웨이 설정을 사용한다. Unity에 API 키를 넣지 않는다. 새 경로는 로컬 모델·FAISS·대화 요약기를 호출하지 않는다.
- 짧은 현재 세계관, 기존 레베카의 정체성·성격·말투, 최근 확정 발언 8개, 이전 TII, 현재 질문 및 첨부 공개 계약을 함께 제공한다. 구형 공통 사건 RAG나 서버 요약의 내용은 이 시제품에 혼합하지 않는다.
- Structured Outputs에서 대사, 대화행동, interest/attitude 변화량, patience 변화량, tell, 표현 태그, 사용한 주장 ID를 받는다. 2026-10-24 사용자 요청에 따라 `store=True`로 변경했으며 최대 출력은 1,000토큰이다. 성공 응답과 전달한 대화 맥락은 OpenAI 측에 저장되어 해당 프로젝트의 Responses 로그에서 확인할 수 있다. 모델은 기존 설정을 유지한다.
- interest는 턴당 ±0.20, attitude는 ±0.15, patience는 -10~+5 범위의 후보만 허용하고 최종 상태는 기존 값에 더해 한계값으로 제한한다. tell은 0~1의 이번 턴 동요이며 거짓말 확률이 아니다. band는 숫자에서 서버가 계산한다.
- 첨부 시 원본 `itemPrompt`나 `followingItemId`는 모델에 전달하지 않고 게임이 계산한 `responseDirectivePrompt`만 사용한다. 공개 허용 항목은 응답 ID에도 반드시 포함되어야 한다. 거부 상황에서도 최근 관계·대화를 유지한다.
- 기존 UI의 생각/응답 모션, TII 위젯, 인벤토리 해금 경로를 연결한다. 새로운 표정 애셋이나 애니메이션을 생성하지 않는다. 표현 태그는 결과에 포함되지만 신규 애셋 매핑까지 추가하지 않았다.

공식 계약 근거: [Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs), [현재 기본 모델의 지원 기능](https://developers.openai.com/api/docs/models/gpt-5.4-nano).

## 실패·취소·기억

레베카 경로는 대사와 TII가 모두 유효해야 성공한다. 거절·미완료 응답·잘못된 JSON·범위 밖 수치·잘못된 주장 ID·현재 질문/세션과 다른 결과는 반영하지 않는다. 실패 시 이전 TII를 유지하며 분석 실패를 `0 / Stable` 반응으로 만들지 않는다.

최소 표시 지연 동안에도 결과는 후보 상태다. 대화를 닫거나 씬을 전환하면 그 후보를 버리고 이력·감정·후속 정보 해금을 적용하지 않는다. 실제 성공 시에만 질문/대답·대화 횟수·Affect·patience·tell을 저장한다. 화면에는 질문이 먼저 보여도 실패한 질문은 확정 대화 이력에 추가하지 않는다.

서버는 이 시제품의 대화 로그/요약을 디스크에 쓰지 않는다. Unity가 보내는 확정 최근 기록을 사용하므로 취소 요청으로 서버 기억이 오염되지 않는다. 이미 시작된 OpenAI 호출은 Unity 취소 후에도 서버에서 끝날 수 있고 비용이 발생할 수 있다. 원격 모델의 취소나 과금 취소를 보장하지 않는다.

NPC 전체 대화 기록과 사건 저장을 하나의 되감기 스냅샷으로 통합하는 작업은 아직 범위 밖이다. 기존 저장 이후의 NPC 기록까지 되감는 기능이나 장기 요약 기억을 새로 완성했다고 간주하지 않는다.

## 적용 파일

- Unity: `NpcInvestigationClient.cs`, `NpcInvestigationController.cs`, `NpcInvestigationModels.cs`.
- 서버 모듈 원본: `tools/npc_gateway/npc_joint_turn.py`.
- 외부 서버 대상: `C:/Users/user/Documents/GitHub/Local-LLM-AI/virtualEnv/RAG_model/app`.
- 설치: `tools/install_npc_joint_gateway.py --app <위 경로> --apply`. 기존 서버 로직을 교체하지 않고 main guard 앞에 등록 코드만 넣는다. 적용 전 원본은 Git 제외 경로 `Logs/NpcJointTurn/original`에 보관한다. 원본 모듈을 수정하면 설치 도구로 외부 복사본도 갱신해야 한다.

## 검증 결과

- Unity 6000.0.32f1 실제 Play 검사 **37개 통과**, 최종 단일 프로세스 종료 코드 0. 기존 22개와 레베카 통합 경로 15개를 확인했다. 최종 로그: `Logs/RebeccaJointTurnUnity-final.log`, 보고서: `Logs/InvestigationHardeningVerification.json`.
- 새 Python 검사 **16개 통과**. 공동 생성·허용 주장·수치·실패·중복·실제 Flask test client 경로 및 설치된 OpenAI SDK의 요청 직렬화/출력 파싱을 확인했다. SDK 검사는 외부 네트워크가 없는 `httpx.MockTransport`와 가짜 키/응답만 사용한다.
- 기존 외부 서버의 오프라인 회귀 검사 **7개 통과**. 외부 세 진입점의 구문과 경로 등록, 원본 모듈/설치 모듈의 바이트 일치도 확인했다.
- 실제 API 검증은 처음 자동 승인 검토에서 차단됐으나, 이후 사용자가 레베카 페르소나·합성 대화 전송과 과금을 포함한 3회 호출을 명시적으로 승인했다. 승인 후 2회 시도했다. 첫 시도는 게이트웨이 502만 기록됐고, 두 번째 진단에서 공급자가 `429 / credit_balance_exhausted`를 반환한 것을 확인했다. 같은 원인의 반복 호출을 피하기 위해 세 번째는 실행하지 않았다. 성공한 생성 결과가 없어 실제 대사 품질·진술 준수율·생성 지연은 아직 평가하지 못했다. 기록: `Logs/NpcJointTurn/live-smoke.json`.

검사 명령:

```powershell
& 'C:\Users\user\anaconda3\envs\RagEnv\python.exe' -X utf8 tools/tests/test_npc_joint_turn.py -v
python -X utf8 tools/tests/test_npc_hardening_server.py --app 'C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app'
```

실제 모델 검증 도구는 `tools/tests/smoke_npc_joint_turn.py --app <서버 경로> --live --report Logs/NpcJointTurn/live-smoke.json`이다. 기존 설정의 키는 출력하거나 보고서에 기록하지 않으며 개인 플레이 기록 대신 합성 대화와 검증용 첨부를 사용한다. 보고서에 시도 횟수를 호출 전에 저장해 같은 보고서로 재실행해도 총 3회를 넘지 않는다. 실패 시 인증 키가 포함될 수 있는 원문 오류 메시지 대신 예외 종류·HTTP 상태·허용된 오류 코드만 기록한다.

### 2026-10-24 로그 저장 및 마지막 실제 호출

- 모듈 원본과 외부 서버 복사본의 `store=True` 적용 및 SHA256 일치를 확인했다. 기존 실행 중 서버에는 재시작 후 적용된다. UI와 최소 표시 지연 1초는 변경하지 않았다.
- 서버와 같은 우선순위(환경 변수 우선, 없으면 `apiKey.env`)로 키와 SDK의 조직·프로젝트·주소 설정을 확인했다. 실제 키는 해당 파일과 일치하며 조직/프로젝트를 별도로 지정하지 않았다. 주소는 `https://api.openai.com/v1/`, 모델은 `gpt-5.4-nano`, SDK 재시도는 0회다.
- 남은 승인 1회로 합성 질문을 전송했다. 2026-10-24 **17:27 KST**, OpenAI HTTP 200 / `completed`, Flask 통합 경로 HTTP 200 및 대사·TII 검증 성공. 입력 1,412토큰, 출력 123토큰, SDK 호출 5.015초. 총 승인 3회 중 성공 1회·실패 2회이며 남은 승인 횟수는 0이다.
- 응답 헤더가 확인한 프로젝트는 `proj_dcRkxu1SwimRpmUuUclGG1WH`다. 요청 ID `req_2edc10b35f0b46a5948c8443e431bfb1`, 응답 ID `resp_00d0cc7a61eead40006adc6bf20bc887d096050468874cdbc3`. 대시보드의 해당 프로젝트 Responses 로그에서 확인할 수 있도록 응답 저장을 요청했다. 대시보드 표시 자체를 직접 확인한 것은 아니다.
- `Logs/NpcJointTurn/live-smoke.json`의 `latestAttemptPassed=true`가 마지막 성공을 나타낸다. `passed=false`는 과거 실패 2회를 포함한 모든 시도가 통과하지 않았다는 뜻이다. 이전 `credit_balance_exhausted`의 원인이 어떻게 해소됐는지나 계정 잔액은 이번 성공만으로 단정하지 않는다.
- 저장 설정을 반영한 기존 오프라인 검사 16개와 새 호출 예산·요청 추적 검사 2개를 통과했다. 새 검사는 `httpx.MockTransport`만 사용해 실제 API에 연결하지 않으며 마지막 호출 실패 때도 재시도/네 번째 호출 없이 요청 ID·프로젝트 헤더를 보존하는지 확인한다.
- `--check-config`는 API 호출 없이 키 원문을 제외한 설정을 확인한다. 실제 검증 보고서에는 요청/응답 ID, 조직·프로젝트 헤더, 토큰 사용량, UTC 시각을 추가했다. 한 번의 기본 대화 성공이며 첨부 대화·장기 롤플레이 품질까지 검증한 것은 아니다.

## 시제품 한계

### Unity 실제 연결 검증 진행 상태 (2026-10-24)

- 기존 서버/Unity가 실행 중이지 않아 `tools/run_npc_gateway_verification.py`로 실제 `server_test.py`를 import하고 기존 인덱스와 경로를 사용하는 localhost:5000 서버를 기동했다. 시작 모델 선택기의 별도 유료 검증 호출은 생략했다. 서버는 실행 상태로 유지했다.
- 실제 `Shangrila_CaseTest` 씬을 열고 기존 플레이어 상호작용 레이 → 레베카 클로즈업 → 기존 조사 UI → 실제 전송 버튼 → 기본 `NpcInvestigationClient` / UnityWebRequest 경로를 사용했다. 사건 진행은 `Logs/NpcJointTurn/case-live-*.json` 임시 슬롯으로 분리하고 NPC 저장을 비활성화했으며 전송 상태를 합성 대화로 초기화했다.
- Unity의 실제 통합 요청 1회가 HTTP 200으로 성공했다. turnId `a0eb5cb62ff24430b7f796232145c374`, 대사와 함께 Tell=0.05, interest=0.2, attitude=0.1, patience=95가 반환됐다. 기록: `Logs/NpcJointTurn/unity-live-server.json`.
- 전송 직후 입력 비활성화와 대기 화면은 `Logs/NpcJointTurn/unity-live-pending.png`에 기록했다. 첫 검증 도구가 선택적 숫자 텍스트(`TellMeterPresenter.valueText`)를 필수로 가정해 예외가 났다. 현재 프리팹은 이 참조를 비워 두고 파형을 표시하므로 게임 UI 변경 없이 검사 대상을 그래프 내부 값으로 수정했다.
- 완료된 대사/그래프의 최종 표시 검증은 아직 보류 상태다. Computer Use의 Unity 창 접근 승인이 시간 초과됐고, 백그라운드 Unity가 수정 스크립트 갱신을 기다린다. Unity 창을 활성화하면 이미 받은 턴의 현재 상태를 검사하는 복구 코드가 실행된다. 복구 코드는 새 API 요청을 보내지 않는다. 최종 보고서: `Logs/NpcJointTurn/unity-live-ui.json`, 완료 화면: `Logs/NpcJointTurn/unity-live-completed.png`(검사 후 생성).
- 현재 서버 로그의 `providerCalls=[]`는 0회 호출을 뜻하지 않는다. 첫 실행용 추적 래퍼가 경로 등록 시 캡처된 클라이언트 함수에 연결되지 않았기 때문이다. 이후 실행용 래퍼는 해당 연결을 수정했지만 추가 API 호출을 피하기 위해 재시작하지 않았다. 이번 연결 성공의 근거는 실제 Unity 요청의 HTTP 200 결과이며 토큰 사용량은 이 보고서에서 확인하지 못했다.

필드 형식과 허용 ID 검사는 자유 대사 속 모든 사실의 정확성을 보장하지 않는다. 핵심 진술의 문장 단위 보호·전체 사건 공개 그래프·장기 기억·대사/TII 사람 평가·배포용 인증/요청 한도는 후속 범위다. 이 결과를 모든 NPC에 적용하거나 기존 서버의 모든 기능을 제거한 것으로 보지 않는다.
