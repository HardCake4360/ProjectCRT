# Progress

## Current status — 2026-10-21
- Current planning authority is the user's two v1.0 PDFs: 샹그리아 살인사건 시나리오 and 세계관 설정집. See `Docs/PROJECT_DIRECTION.md` for confirmed/proposed/undecided distinctions. Reuse existing features and follow these sources for future work.
- The old Park/cat case is excluded from project development. The new story starts before San's LSPD assignment, makes Jeremy the victim, and concludes through three evidence-backed final deductions.
- `ProjectCRT_Demo` is discarded. RPG runtime/editor/test assemblies and the RPG build scene are disabled; source and data are preserved.
- Investigation requests now cancel on close/scene changes, reject invalid replies, recover from timeouts, preserve the intended 1-second reply delay, and decode streaming Korean correctly.
- Server error handling and ordered conversation-memory updates are applied to the external Local-LLM-AI source. Restart that server to load the changes.
- Validation: 22 Unity checks and 7 offline server regression tests passed. Details: `Docs/INVESTIGATION_HARDENING.md`.

## Updated
- 2026-03-27
- 2026-04-12: Added NPC investigation animation clip flow and controller timing hooks.
- 2026-04-13: Split NPC thinking animation into startThinking one-shot followed by Thinking loop.
- 2026-04-13: Fixed investigation UI button recovery when prefab button objects contain invalid Selectable components.
- 2026-04-13: Changed incomplete investigation UI reference logs from errors to warnings.
- 2026-04-13: Made optional investigation UI elements non-blocking so missing StatusText/ScrollRect/AffectPlanePresenter does not prevent Talk button binding.
- 2026-04-13: Made investigation log/title text optional and allowed tell/affect signal UI to run with remaining graphic-only bindings.
- 2026-04-13: Investigated UnityEditor GameObjectInspector stale target warning after UI prefab/runtime object changes; no gameplay script change required.
- 2026-04-13: Updated investigation UI color handling so scripts preserve prefab/component alpha while only changing RGB values.
- 2026-04-13: Added affect pending/arrival visual feedback: affect graphic darkens while waiting, flashes white on value arrival, then fades back to the normal signal color.
- 2026-04-13: Added an editor inspector preview for `AffectPulseGraphic` with Pending Dark, Arrival Flash, and Reset buttons for checking the affect effect without entering play mode.
- 2026-04-13: Added inspector-configurable minimum affect pending time so fast affect responses remain dark for at least the configured duration before the arrival flash is applied.
- 2026-04-13: Added NPC conversation camera look-at IK support through `NPC_script` settings and an animator-side `NpcLookAtIKDriver`, enabled during investigation interaction and disabled on close.
- 2026-04-13: Reworked investigation message input flow: Enter sends, Shift+Enter allows new lines, Ask Topic/Present Evidence moved behind a speed-dial style action menu, and selected topic/evidence attachments are included in the outgoing payload.
- 2026-04-13: Added `NpcInvestigationUIPrefabUpdater` editor utility to update the actual `NpcInvestigationUI.prefab` with attachment bar, floating action button, speed dial root, and selection list bindings.
- 2026-04-13: Tried to run the prefab updater through Unity batchmode, but Unity blocked it because the project was already open in another editor instance; the updater is set to run from the active editor on script reload.
- 2026-04-13: Synchronized `NpcInvestigationUI.prefab` with the code-generated investigation UI structure and updated the prefab updater to enforce layout/color/active-state values even when nodes already exist.
- 2026-04-13: Stopped runtime investigation UI from reapplying speed-dial button RectTransform values when prefab bindings already exist, and changed the prefab updater to manual menu execution only so authored prefab layout is not overwritten on editor reload.
- 2026-04-13: Replaced the investigation UI `TalkButton` concept with `SendButton/sendButton`, keeping only migration compatibility for old prefab objects while the outgoing action protocol still uses `Talk` for normal message requests.
- 2026-04-13: Removed runtime-created fixed investigation UI layout fallbacks. Fixed UI elements such as send/action buttons, speed dial root, selection list root, and attachment bar must now come from `NpcInvestigationUI.prefab`; runtime code only fills dynamic selection-list contents.
- 2026-04-13: Changed the NPC investigation prefab updater menu to update serialized references and labels only, without applying RectTransform/layout values to the authored prefab.
- 2026-04-13: Fixed `NpcInvestigationUI.prefab` button bindings: `AskTopicButton` and `PresentEvidenceButton` were still serialized as TMP input fields, so they were converted back to `Button` components and re-linked to `InvestigationInteractionUI`.
- 2026-04-13: Added the first prop/inventory implementation pass: investigation item definitions/database, minimal JSON unlock save data, `InspectableProp` information unlocks, Tab inventory controller, Q/E category cycling, and prefab-loaded `InvestigationInventoryUI` support.
- 2026-04-13: Added an editor-only inventory UI prefab creator that creates `Assets/resources/prefab/InvestigationInventoryUI.prefab` only when missing, so authored prefab layout is preserved after the first creation.
- 2026-04-13: Extended investigation context and NPC attachment selection so unlocked prop information is stored as `information`, shown in the inventory information tab, and can also be attached through the existing Present Evidence flow for server compatibility.
- 2026-04-13: Reverted the reference-image inventory UI prefab creator attempt and restored the previous panel-style `InvestigationInventoryUI` prefab creator. The generated prefab file remains deleted until Unity recompiles and recreates it from the restored creator.
- 2026-04-13: Recreated `Assets/resources/prefab/InvestigationInventoryUI.prefab` from scratch without using the reference image. Unity batchmode creation was blocked because the project was already open, so a minimal prefab asset was restored with the required runtime bindings and the panel-style creator remains available for editor-side regeneration.
- 2026-04-13: Continued prop/inventory implementation pass: `InspectableProp` unlocks prop data as the `information` category, inventory save JSON stores only category id lists, Tab toggles prefab-loaded inventory UI, and Q/E cycles topic/evidence/information tabs while main player control is paused.
- 2026-04-13: Added a Resources database fallback path for investigation item definitions so runtime-created inventory managers can resolve ScriptableObject item details from `Resources/Investigation/InvestigationItemDatabase` when configured.
- 2026-04-13: Confirmed `InvestigationInventoryUI.prefab` is currently missing on disk; the editor prefab creator is set to recreate it from the active Unity editor after script reload because batchmode cannot safely run while the project is already open.
- 2026-04-13: Tried to execute the inventory prefab creator through Unity batchmode, but Unity exited abnormally in the current multi-editor/open-project state before writing a log or prefab. Use `Tools/Investigation/Create Inventory UI Prefab` in the active editor if automatic script-reload creation does not run.
- 2026-04-14: Added NPC-style dialogue hooks to `InspectableProp`: it now exposes `OnInteract`, supports an optional assigned `DialogueObject`, and provides `StaticDiaEventTrigger(DialogueObject)` so prop inspection can play existing dialogue scripts without immediately returning to main control while dialogue is active.
- 2026-04-20: Hardened NPC investigation attachment selection: runtime-created topic/evidence option rows now get explicit RectTransform placement, LayoutElement sizing, non-raycast text labels, and evidence selection debug logs so Present Evidence item clicks reliably update selected attachments.
- 2026-04-27: Reworked NPC investigation UI refresh flow for multi-NPC use: per-NPC runtime conversation state is now keyed by `sceneId::npcId`, opening a different NPC closes the previous active investigation view cleanly, and the shared UI is rebuilt from the current NPC's saved exchanges/signal state each time it opens.
- 2026-04-27: Adjusted NPC conversation state/save keys from `sceneId::npcId` to `npcId` only so the same NPC shares one persistent conversation history across scene changes.
- 2026-04-27: Removed code-side title text overrides from the investigation chat UI and inventory UI so prefab-authored `TitleText` contents are preserved instead of being replaced at runtime.
- 2026-04-27: Changed NPC investigation open flow to decide intro vs. restore based on saved conversation data presence. If an NPC already has saved exchanges/state, the shared UI now reloads that conversation; otherwise it prints the initial fixed line.
- 2026-04-27: Added persistent NPC conversation save/load support. Investigation conversation states are now serialized to `Application.persistentDataPath/npc_investigation_conversations.json`, restored by `sceneId::npcId`, and saved after intro creation, player messages, NPC replies, tell updates, and UI close.
- 2026-04-27: Added inspector-configurable retention limits to `NpcInvestigationController` for saved exchange count and saved statement count, and wired those limits into runtime trimming during save/load.
- 2026-04-27: Added inspector-configurable minimum reply delay on `NpcInvestigationController`. Fast server replies are now buffered client-side so NPCs remain in the thinking state until the minimum delay passes, then delayed streaming/final reply display begins.
- 2026-04-27: Moved first-contact investigation intro flow into `NpcInvestigationController`: first interaction can now play an assigned `initialDialogue` after closeup, then open the investigation UI only after that dialogue ends. `NPC_script.OnInteract` remains for legacy non-investigation NPCs because existing scenes still serialize and use it.
- 2026-05-03: Added [investigation.md](/C:/Users/user/Documents/GitHub/ProjectCRT/investigation.md), a developer-facing walkthrough of the interrogation system covering player-visible flow, hidden runtime flow, client/server request sequence, UI behavior, state restore, and persistence.
- 2026-05-16: Added client-authored attached-information prompt routing. `InvestigationItemDefinition` now includes `prompt`, `attitudeThreshold`, and `followingItem`; the investigation client resolves attached information against the latest attitude, sends a resolved prompt directive plus RAG-bypass hint in the request payload, ignores legacy server-driven topic unlock ids, and locally unlocks the configured following item only after a threshold-passing attached-information reply succeeds.
- 2026-05-16: Updated external test server flow in `C:\Users\user\Documents\GitHub\Local-LLM-AI\virtualEnv\RAG_model\app\server_test.py`. `/investigation/npc/reply` now reads `interaction.attachedItemDirective`, skips world-context RAG when `bypassWorldContextRag` is true, prioritizes the client-provided attached-item directive prompt in reply generation, and returns no server-side topic auto-unlocks so the client remains the authority for following-item progression.
- 2026-05-16: Changed the client-generated attached-information false prompt to the exact fixed Korean instruction: `해당 주제에 대해선 답변하지 말고 페르소나의 회피 로직에 맞춰 정보 제공을 거부하십시오`.
- 2026-05-16: Simplified `server_test.py` reply generation into two explicit branches: `general` uses summary + world-context RAG, while `attached` uses an attached-item-only prompt template with no RAG. Within the attached branch, false-vs-true behavior now comes only from the client-provided `responseDirectivePrompt`, not from separate server-side prompt modes.

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
- `Interactable`의 클로즈업 카메라는 상호작용 중일 때만 활성화되도록 조정: `Awake()`에서 기본 비활성화, `EnterCloseup()`에서 활성화, `ExitCloseup()`에서 비활성화.
- 플레이어 기본 시네머신 카메라가 없는 경우를 대비해 `Interactable`이 상호작용 직전 `Main Camera` 위치/회전을 저장하고, `ExitCloseup()` 시 직접 복원하도록 보강.
- Added affect diagnostics: `NpcInvestigationController` now logs reset, streaming, and final affect values, and `AffectPlanePresenter` warns when affect values arrive without a bound `pulseGraphic`.

## 2026-10-09 — projectNoSleep RPG 시스템 이식

- 사용자 선택(1번)에 따라 기존 씬을 유지하고 시스템·제작 도구와 별도 RPG 시작 씬을 추가했다.
- `projectNoSleep/Assets/NoSleep`의 Runtime/Editor/Data/Scenes/Tests를 `.meta`와 함께 이식했다. 원본 에셋 488개와 문서 12개의 SHA-256 일치, GUID 충돌 없음을 확인했고 원본 프로젝트는 변경하지 않았다.
- `Assets/NoSleep/Scenes/ProjectCRT_RPG.unity`와 사무실/골목 맵, 목격자 대화·선택지, 영수증 단서, 조사 조건·맵 왕복·자체 타이틀·슬롯 저장을 추가했다. CRT 씬은 생존/피로 시스템을 활성화하지 않는다.
- `NoSleepBootstrap`/`GameUI`에 프로젝트별 제목·소개·안내·저장 폴더 설정을 추가했다. CRT RPG 저장은 `ProjectCRT_RPG/Saves`에 분리한다. 기존 조사 인벤토리·NPC 대화 저장 형식과 경로는 유지했다.
- `Project CRT > RPG` 메뉴에서 시작 씬 열기/맵·이벤트 편집/콘텐츠 브라우저/연결/디버거/검사를 사용할 수 있다. 기존 빌드 씬 순서를 유지하고 새 씬만 마지막에 추가했다.
- Unity 6000.0.32f1 실제 임포트·컴파일·전체 콘텐츠 검사 통과. EditMode 273/273, PlayMode 96/96, 총 369개 통과(실패·건너뜀 0). 신규 CRT 통합 검사 6개 포함. 원본 497개 파일 보존, 문서화된 코드 변경 3개, GUID 중복·미해결 직렬화 참조·메타 누락 없음.
- 가이드: `Docs/RPG_MIGRATION.md`. 검증 및 XML 보고서: `Docs/RPG_VERIFICATION.md`, `Docs/Validation/RpgEditMode.xml`, `RpgPlayMode.xml`. 원본 문서는 `Docs/NoSleep`에 과거 기록으로 보관했다.
- 후속: 기존 LLM 조사 UI/API·조사 인벤토리·스토리/오디오/타이틀 연결, 기존 플레이 씬의 RPG 전환. 이번에는 사람의 수동 플레이·화면 평가·독립 실행 빌드를 수행하지 않았다.

## 2026-10-23 — 샹그리아 첫 방문 사건 진행

- `Shangrila_CaseTest` 씬에 거리 시작 → 실제 입구 진입 → 첫 입장 컷씬 1회 → NPC 대화 및 물건 조사 해금 흐름을 연결했다. 기존 Outdoor/Cutscene 제작 씬은 유지한다.
- `ShangrilaCaseManager`가 완료 컷씬 ID와 두 진행 단계를 관리한다. 완료/스킵은 저장하며 취소/실패는 다시 입장할 때 재시도한다. 기존 대화 종료 쿨다운이 사건 잠금을 해제하지 않도록 별도 상호작용 권한을 추가했다.
- 영업 안내문·메뉴판 두 임시 조사물을 기존 설명 대화와 정보 인벤토리에 연결했다. 상호작용 판정은 플레이어 몸과 비상호작용 트리거를 제외하며 기존 상호작용 볼륨과 실제 벽의 가림을 유지한다.
- 단일 사건 JSON 슬롯에 컷씬 완료, 인벤토리, 위치·수평/수직 시점을 원자적으로 저장한다. F5 저장/F9 불러오기, 씬 시작 자동 로드, 완료/정보 획득 시 자동 저장을 지원한다. 과거 저장 복원 시 이후의 조사 캐시도 제거한다. 기존 NPC 대화 기록은 기존 저장 시스템을 사용한다.
- 실제 Unity Play 두 세션에서 사건 흐름과 재시작 후 복원을 모두 검증했다. 정상 완료·스킵·취소·재입장·NPC 대화·물건 조사·저장 되돌리기·파일 손상/백업·상호작용 판정 호환성 통과. 자동 검증은 프로젝트 안의 임시 슬롯만 사용한다.
- 사용 안내: `Docs/SHANGRILA_CASE_FLOW.md`. 보고서: `Logs/Cases/verification-first-session.txt`, `Logs/Cases/verification.txt`.

## 2026-10-23 — 레베카 대사·TII 공동 생성 시제품

- Python 게이트웨이를 유지하고 레베카만 `/investigation/npc/turn` 통합 요청을 사용한다. 한 번의 OpenAI Responses 구조화 출력에서 대사·감정 변화·이번 턴 tell을 함께 받고, 서버와 Unity에서 검증한다. 다른 NPC의 reply/tell과 터미널 경로는 유지한다.
- 기존 UI와 최소 답변 표시 지연 1초를 유지한다. 검증된 결과도 1초가 지나기 전에는 대화 이력·TII·후속 정보에 반영하지 않는다. 성공 시 함께 반영하며 실패·취소·오래된 응답은 이전 상태를 보존한다.
- 레베카 맥락은 기존 페르소나·짧은 현재 세계관·최근 확정 발언·이전 감정·첨부 공개 지시를 사용한다. 새 경로는 로컬 모델·구형 RAG·서버 요약/개인 대화 로그를 사용하지 않는다. NPC 전체 기억과 사건 슬롯 되감기 통합은 아직 후속 범위다.
- 외부 `Local-LLM-AI/virtualEnv/RAG_model/app`에 `npc_joint_turn.py`와 세 서버 진입점의 경로 등록을 적용했다. 원본·설치 도구는 `tools/npc_gateway`, `tools/install_npc_joint_gateway.py`, 백업은 `Logs/NpcJointTurn/original`이다. 실행 중 서버는 강제 재시작하지 않았다.
- 검증: Unity 실제 Play 37개, 새 Python/Flask/SDK 모의 전송 16개, 기존 서버 회귀 7개 통과. 최종 Unity 정상 종료 코드 0, 컴파일 오류·NullReference/MissingReference 0. 실제 API 3회 검증은 자동 승인 검토에서 외부 전송·과금의 구체적 승인 부족으로 거부되어 실행하지 않았고 사용자 승인 요청을 남겼다.
- 사용·롤백·제한: `Docs/REBECCA_JOINT_TURN_PROTOTYPE.md`. 실제 모델의 자연스러움과 사건 사실 준수율은 아직 평가하지 않았다.

### 레베카 실제 API 검증 후속

- 사용자가 페르소나·합성 대화 전송과 과금을 포함한 실제 API 3회 검증을 명시적으로 승인했다. 승인 후 2회 시도했으며 성공한 생성 결과는 없다.
- 첫 시도는 게이트웨이 502, 두 번째는 공급자 `429 / credit_balance_exhausted`를 확인했다. 동일 오류가 예상되는 세 번째 호출은 실행하지 않았다. 크레딧 문제가 해결되면 남은 승인 범위 1회에서 재개할 수 있다.
- 검증 도구에 호출 전 시도 횟수 저장과 총 3회 제한, 키를 노출하지 않는 오류 진단을 추가했다. 기록은 `Logs/NpcJointTurn/live-smoke.json`이며 개인 플레이 기록·게임 저장은 사용하지 않았다. 실제 모델 대사/TII 품질은 아직 미검증이다.

## 2026-10-24 — 레베카 Responses 로그 저장과 실제 생성 성공

- 사용자 요청에 따라 `store=True`를 모듈 원본과 외부 서버에 적용했다. 기존 UI와 최소 표시 지연 1초는 유지한다. 실행 중 서버에는 재시작 후 반영된다.
- 키가 서버의 `apiKey.env`와 일치하고 공식 `https://api.openai.com/v1/`을 사용하는지 확인했다. 조직/프로젝트 별도 지정은 없으며 실제 응답 헤더의 프로젝트는 `proj_dcRkxu1SwimRpmUuUclGG1WH`다. 키 원문은 출력·기록하지 않았다.
- 남은 승인 1회로 17:27 KST 합성 기본 대화 검증에 성공했다. OpenAI HTTP 200 / completed, Flask 경로 HTTP 200, 대사·TII 검증 통과. 입력 1,412토큰·출력 123토큰, SDK 호출 5.015초. 총 3회 중 성공 1회·실패 2회, 남은 승인 0회.
- 요청/응답 ID와 프로젝트 헤더를 `Logs/NpcJointTurn/live-smoke.json`에 기록했다. 오프라인 기존 16개·새 호출 예산/추적 2개 통과. 실제 대시보드 표시는 직접 확인하지 않았고 첨부/장기 대화 품질은 후속 검증 범위다.

### Unity 실제 연결 확인 후속 (2026-10-24)

- 기존 프로세스가 없어 최신 실제 서버를 localhost:5000에 기동하고 Unity의 실제 사건 씬/조사 UI/기본 UnityWebRequest로 합성 대화 1회를 전송했다. HTTP 200 및 대사·Tell 0.05·interest 0.2·attitude 0.1·patience 95 반환을 확인했다. 기존 플레이 기록 전송과 저장은 피했다.
- 전송 대기 화면은 캡처했으나 검증 도구가 그래프 전용 Tell UI에 없는 숫자 텍스트를 읽으려다 실패했다. 검증 코드만 수정했으며 최종 완료 화면 검사는 Unity 창 활성화/스크립트 갱신 대기 상태다. 추가 API 호출은 하지 않았다. 상세 진행 상태와 산출물은 `Docs/REBECCA_JOINT_TURN_PROTOTYPE.md`에 기록했다.


## 2026-10-25 — SC-02 / SC-03 컷씬 제작

- SC-02: 고정 3인칭 산 조작, 이삿짐 5개 상호작용, 소파 착석·속마음 4개·기립, 맵 튜토리얼과 샹그리아 이동을 연결했다.
- SC-03: 출입문 상호작용, 제레미 인사와 레베카 재회, 시간 경과, 충격음, 복도 추적, 2번 룸 발견과 최종 암전까지 원문 대사 76개를 연결했다. 입구·서비스 문·2번 룸 문과 병·술잔도 연출 큐로 제어한다.
- 원본 캐릭터 3종과 샹그리아 FBX 4개, 기존 SC-01 씬·시퀀스를 보존했다. 제레미에는 이 테스트 전용 파생 메시·간이 뼈대를 추가했다.
- 검증: Unity 전체 103개 턴 재구성과 상태 복원, 실제 Play 짐 정리→지도→바 입장→전체 컷씬→암전, 취소·재시작·건너뛰기를 통과했다. 원문 80개 대사 일치와 원본 해시·반영본 일치도 확인했다. 기록은 Logs/SC23, 사용법과 1차 연기·임시 음원의 범위는 Docs/SC02_SC03_CUTSCENES.md.

## 2026-10-25 — SC-03 레베카 무료 애니메이션 적용

- Quaternius UAL 1·2 Standard(CC0), Anderson Rohr Crowd Reactions·Cleaning 무료 팩을 공식 배포처에서 다운로드했다. Gumroad 두 팩은 사용자가 지정한 이메일로 각각 0원 수령했다. 전체 ZIP은 로컬 Logs에 보관하고, 프로젝트에는 원본·선택한 FBX와 이용 조건을 남겼다.
- 기존 레베카 Avatar에 Humanoid 동작을 리타기팅하고 기존 컷씬 뼈대 경로를 사용하는 별도 클립 15개로 베이크했다. 서 있기·대화·기대기·걷기·복도 걷기·긍정·부정·짧은 웃음·목을 만지는 반응을 SC-03 대사에 연결했다. Cleaning은 후속 업무 연기 후보로 확보했다.
- 기존 이동 위치·방향을 유지하고 경로 길이에 보행 반복 횟수를 맞췄다. 카운터 손 위치를 보정했다. 등장과 병을 내려놓기 전 말하기·듣기에 각각 대응하는 병 추적 트랙 3개를 추가했다. 문·발견·병 내려놓기 등 장면 전용 연기는 유지했다.
- 검증: 96개 턴의 미리보기와 원래 포즈 복원, 베이크한 뼈 움직임·이동 경로·반응 큐·병 추적, 실제 Play SC-02→SC-03 입장→모든 76개 대사→암전·재생 반복·취소·건너뛰기를 통과했다. 검증한 97개 출력 파일을 반영했고 새 리소스 참조 18개가 정상 연결된다. SC-01·02, 원본 캐릭터·샹그리아 FBX 4개, 원래 SC23 애니메이션과 대사·카메라·다른 NPC 큐를 보존했다.
- 사용법과 에셋별 이용 조건: Docs/REBECCA_SC03_ANIMATIONS.md. 원본 보존·미리보기·실제 재생·반영 보고서: Logs/RebeccaAnimationAssets.

### SC-03 하이어라키 정리

- 캐릭터·배경·소품·조명·카메라·컷씬 마커/경로·효과·UI·렌더링·기존 참조 오브젝트를 종류별 빈 부모 아래로 정리했다. 컷씬 실행기는 최상위 배치가 필요하므로 `00_SC03_Cutscene_System`으로 이름을 정리하고 루트에 유지했다.
- 원래 오브젝트 695개의 월드 변환과 활성 상태, 모든 캐릭터의 상대 애니메이션 경로 및 컷씬 바인딩 보존을 확인했다. 기존 컴포넌트 데이터·시퀀스·동작 클립·SC-01/02는 유지했다. 씬만 반영했으며 원본 백업과 보고서는 Logs/SceneHierarchy에 보관했다.
