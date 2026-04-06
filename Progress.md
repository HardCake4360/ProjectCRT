# Progress

## Updated
- 2026-03-27

## Current Goal
- 조사 파트 중심 구현
- 첫 적용 대상: `Bar` 씬의 `Bartender` NPC
- 대상 캐릭터: 리베카(Rebecca)
- 목표 범위: 첫 고정 대사 + 이후 조사 액션 기반 NPC 상호작용 + 로컬 LLM 연동

## Confirmed Scope
- 조사 파트의 핵심 요소는 다음 3가지로 고정
  - 수사를 진행하는 현장
  - 현장에 존재하는 NPC
  - 현장에 숨겨진 단서
- 이번 최소 구현은 `Bar` 씬의 바텐더 NPC 1명 기준으로 진행
- 최소 지원 액션
  - `Talk`
  - `AskTopic`
  - `PresentEvidence`
- 첫 상호작용은 서버 호출 없이 고정 대사
- 두 번째 상호작용부터는 로컬 LLM 서버 호출
- 서버도 함께 수정
- `personaKey` 방식으로 리베카 페르소나 추가
- NPC 조사 대화는 기존 터미널 UI를 사용하지 않음
- NPC 조사 대화는 별도의 전용 채팅 UI에서 진행
- UI 방향은 일반적인 LLM 채팅 서비스와 유사한 형태를 목표로 함

## Source References
- 조사 클라이언트 명세: `C:\Users\user\Downloads\Project_CRT_NPC_Investigation_Client_Spec.md`
- 사건/등장인물 요약: `C:\Users\user\Downloads\Project_CRT_Case_Design_KR.md`
- Unity 프로젝트 루트: `C:\Users\user\Documents\GitHub\ProjectCRT`
- 로컬 LLM 서버 루트: `C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app`

## Findings So Far
- 현재 Unity 프로젝트에는 대화, NPC 상호작용, 단서, 터미널, 스토리 상태의 기반 구조가 이미 존재함
- `DialogueManager`와 `DialogueObject`를 활용하면 첫 고정 대사 구현이 비교적 자연스럽게 가능함
- `NPC_script`는 현재 `OnInteract` 이벤트 기반으로 동작하므로 조사 전용 컨트롤러를 붙이기 쉬움
- 기존 로컬 LLM 서버는 Flask 기반이며 `POST /ask-stream` 엔드포인트와 `personaKey` 입력을 지원함
- 현재 서버는 조사 전용 구조화 응답(`signal`, `stateDelta`)을 기본 제공하지 않으므로 서버 확장이 필요함
- 리베카는 시나리오 문서상 범인이 아니라 간접 힌트를 제공하는 관찰자 NPC로 정의되어 있어 첫 구현 대상으로 적합함
- 기존 `terminalManager` UI는 조사 NPC 대화용으로 재사용하지 않고, 별도 프레젠테이션 계층이 필요함
- 전용 NPC 채팅 UI는 씬 수동 배치 없이도 테스트할 수 있도록 런타임 생성형으로 잡는 편이 현재 단계에 적합함

## Planned Implementation
1. 로컬 LLM 서버에 조사 전용 요청/응답 스키마 추가
2. `rebecca` 페르소나 JSON 추가
3. Unity에 조사 전용 DTO 및 상태 클래스 추가
4. NPC 조사 전용 채팅 UI 구현
5. `NpcInvestigationController` 구현
6. 첫 대화 고정 / 이후 서버 호출 분기 구현
7. `InvestigationInteractionUI`에 최소 액션 3종 연결
8. `BioSignalPresenter` 디버그 UI 구현
9. `Bar` 씬의 바텐더에 연결

## Open Technical Decisions
- 서버 확장 방식
  - 기존 `/ask-stream` 재사용
  - 또는 조사 전용 엔드포인트 신규 추가
- 바텐더 첫 고정 대사 자산 생성 방식
  - 기존 `DialogueObject` 기반 신규 에셋 생성
- 조사 UI 부착 방식
  - 전용 채팅 패널 우선
  - 이후 기존 창 UI 시스템과 통합 여부 검토

## Current Status
- 요구사항 정리 완료
- 첫 적용 NPC 및 사건 범위 확정 완료
- 서버 루트 및 기존 엔드포인트 구조 확인 완료
- 페르소나 파일 형식 확인 완료
- NPC 대화 UI 방향이 전용 채팅 UI로 확정됨
- 로컬 LLM 서버에 `/investigation/npc` 엔드포인트 추가 완료
- 로컬 LLM 서버에 `rebecca` 페르소나 추가 완료
- Unity 조사 DTO/상태/컨텍스트 빌더 구현 완료
- Unity 전용 NPC 채팅 UI 런타임 생성 골격 구현 완료
- Unity 전용 NPC 채팅 UI를 프리팹 기반으로 전환 완료
- `NpcInvestigationUI.prefab`에 텍스트, 버튼, 스크롤 뷰가 배치된 실체형 레이아웃 반영 완료
- 프리팹 기반 UI에 맞춰 입력 초기화와 제목/안내 문구 정리 완료
- `InvestigationInteractionUI`의 기존 런타임 UI 생성 코드를 제거하고 프리팹 로드/자식 참조 보정 방식으로 전환 완료
- 프리팹 참조 보정 로직을 경로 고정식에서 자식 이름 탐색식으로 보강하고 누락 필드 로그 추가
- 조사 UI 상호작용 중에는 커서를 해제하고 플레이어 시야/이동을 고정하도록 `MainLoop` 상태 처리 보강
- `/investigation/npc` fallback 경로 제거, 조사 전용 엔드포인트만 사용하도록 클라이언트 정리
- 조사 API 404 원인 추적을 위해 요청 URL, 응답 본문, 바인딩된 클라이언트 인스턴스 로그 추가
- 조사 API 에러에서 `persona not found`를 감지해 요청한 `personaKey`를 Unity 로그와 UI 메시지에 명확히 표시하도록 개선
- 조사 클라이언트를 NDJSON 기반 스트리밍 수신 준비 구조로 확장하고, NPC 답변을 부분 갱신할 수 있도록 채팅 UI 프레젠터 보강
- 스트리밍 API 명세 문서를 프로젝트 루트에 작성하고, 바탕화면 전달본 생성 준비
- `ask-stream` 패턴을 참고한 `/investigation/npc` 스트리밍 지원용 코드 블록을 별도 [codeBlock.py](/C:/Users/user/Documents/GitHub/ProjectCRT/codeBlock.py) 로 분리 작성
- 클라이언트 상태 모델을 `stress/distortion/focus`에서 `tell/affect/patience` 기준으로 전환 시작
- 임시 생체징후 표시 UI와 `BioSignalPresenter` 제거, 조사 상태는 데이터 흐름 중심으로만 유지
- 서버 수정용 `server.py` 템플릿을 프로젝트의 [tools/server_updated.py](/C:/Users/user/Documents/GitHub/ProjectCRT/tools/server_updated.py) 로 준비
- NPC별 심문 규칙을 인스펙터에서 관리할 수 있도록 [NpcProfileComponent.cs](/C:/Users/user/Documents/GitHub/ProjectCRT/Assets/scripts/Investigation/NpcProfileComponent.cs) 추가
- `NpcInvestigationRequest`에 NPC 프로필 데이터를 포함하도록 컨트롤러/컨텍스트 빌더 확장
- 외부 서버 경로 문제 해결용 스크립트 [fix_external_server_paths.ps1](/C:/Users/user/Documents/GitHub/ProjectCRT/tools/fix_external_server_paths.ps1) 추가
- 한글 깨짐을 피하기 위해 외부 서버 파일 직접 수정 대신 수동 복사/붙여넣기용 임시 파일 [server_path_fix_temp.py](/C:/Users/user/Documents/GitHub/ProjectCRT/server_path_fix_temp.py) 생성
- `server.py` 전체를 그대로 덮어쓸 수 있도록 경로 수정 반영본 [server_path_fixed_full_copy.py](/C:/Users/user/Documents/GitHub/ProjectCRT/server_path_fixed_full_copy.py) 생성
- 오늘 반영한 `tell/affect/patience`, `interrogationProfile`, 경로 수정까지 포함한 전체 서버 복붙용 파일 [server_today_full_copy.py](/C:/Users/user/Documents/GitHub/ProjectCRT/server_today_full_copy.py) 생성
- Unity `NpcInvestigationController` 및 `NPC_script` 연동 추가 완료
- `Bar` 씬의 `Bartender` 오브젝트에 조사 컨트롤러 연결 완료
- 실제 플레이 검증은 아직 진행 전
- 빠른 구현을 위해 서버에 남겨둔 일부 하드코딩(`_extract_unlock_topics`의 키워드 매핑, 엔진 기본 임계값/가중치)은 프로젝트 종료 전 반드시 클라이언트 데이터/NpcProfileComponent 쪽으로 이관 예정

## Next Step
- 클라이언트 컴파일 정리 및 새 상태 스키마 기준으로 서버 계약 정렬
- 프리팹 레이아웃 미세 조정 및 폰트/컬러 스타일 보정
- `Bar` 씬에서 바텐더 상호작용 기준 실제 채팅 루프 검증

## Implemented Files
- `Assets/scripts/Investigation/NpcInvestigationModels.cs`
- `Assets/scripts/Investigation/NpcConversationState.cs`
- `Assets/scripts/Investigation/InvestigationContextBuilder.cs`
- `Assets/scripts/Investigation/NpcInvestigationClient.cs`
- `Assets/scripts/Investigation/BioSignalPresenter.cs`
- `Assets/scripts/Investigation/NpcDialoguePresenter.cs`
- `Assets/scripts/Investigation/InvestigationInteractionUI.cs`
- `Assets/scripts/Investigation/NpcInvestigationController.cs`
- `Assets/scripts/NPC/NPC_script.cs`
- `Assets/resources/prefab/NpcInvestigationUI.prefab`
- `C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\server.py`
- `C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\data\personas\rebecca.json`
- `server_today_full_copy.py`에서 로그/요약 키를 `user_id` 기준이 아니라 `personaKey` 우선 기준으로 변경해, 현재 대화 중인 페르소나의 기록과 컨텍스트만 읽고 쓰도록 조정.
- 로그 구조 방향 수정: 파일은 플레이어 기준 단일 JSONL을 유지하고, 각 레코드에 `personaKey`를 함께 저장한 뒤 현재 대화 중인 `personaKey`로만 필터링해서 기록/컨텍스트/요약을 읽도록 `server_today_full_copy.py`를 재조정.
- `server_today_full_copy.py`에서 규칙 기반 `tell` 보정(`actionWeights.tellDelta`, keyword/topic/evidence의 tellDelta`)을 제거하고, `tell`은 LLM 분류기 `_derive_tell_llm(...)` 결과만 쓰도록 정리.
