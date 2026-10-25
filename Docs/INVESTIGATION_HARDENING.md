# 조사·서버 안정화와 RPG 비활성화

작업일: 2026-10-21 (Asia/Seoul).

`ProjectCRT_Demo`는 폐기된 작업이며 이번 변경에 포함하지 않는다. 현재 3D 조사 플레이와 포트 5000 NPC 서버를 대상으로 한다.

## 적용 사항

- 최소 답변 표시 지연은 1초로 유지한다. 통신이 먼저 끝나도 표시 지연이 지난 뒤 최종 답변을 표시한다. Bar 씬의 기존 1초 설정도 유지했다.
- NPC 닫기·전환·비활성화·씬 이동 시 요청을 취소하고 코루틴·공유 UI 잠금을 정리한다. 응답은 현재 대화 세대와 turnId가 일치할 때만 반영한다.
- reply는 성공 여부·turnId·오류 필드·내용을 확인한 뒤 저장하거나 후속 정보를 해금한다. 빈 답변, 생략 기호, 서버 오류 문자열은 성공으로 취급하지 않는다.
- 질문은 이전 대화 컨텍스트를 구성한 뒤 로컬 이력에 등록한다. 실제 이전 질문은 보존하므로 반복 질문 페널티 자체는 유지한다.
- 클라이언트 요청 제한은 기본 90초다. reply 표시 후 tell은 최대 5초 더 기다리고, 미완료 시 요청을 정리해 다음 질문을 허용한다. 각 값은 Inspector에서 설정할 수 있다.
- 조사·터미널 스트리밍은 수신 조각 사이에 UTF-8 디코더 상태를 유지한다.
- 터미널은 명령 인자를 검사하고 완료·실패 시 입력 복구 콜백을 실행한다. `/set_persona`의 인자 누락과 `/selfDestroy`의 완료 누락을 보완했다. 기존 CP949 터미널 소스는 UTF-8로 정규화했다.
- 조사 정보 DB에 기존 `old_tv`의 후속 항목 `test`를 등록했다.

## RPG 비활성화

RPG 소스·씬·데이터·저장은 삭제하지 않았다. 다음 네 어셈블리에 `PROJECTCRT_ENABLE_RPG` define constraint를 추가했으며 해당 define은 현재 설정하지 않는다.

- NoSleep.Runtime
- NoSleep.Editor
- NoSleep.Tests
- NoSleep.PlayMode.Tests

따라서 RPG 런타임, 상태창, NPC/이벤트/전투/생존 기능, 제작 메뉴, RPG 테스트 어셈블리가 현재 편집기와 플레이에서 로드되지 않는다. `ProjectCRT_RPG` 씬도 빌드 목록에서 비활성화했다. 기존 3D 조사 인벤토리와 Tab 조작은 유지한다. 이전 RPG 씬을 직접 열면 비활성 어셈블리의 컴포넌트가 실행되지 않으므로 현재 플레이 시작점으로 사용하지 않는다.

`verify_rpg_import.py`의 문서화된 변경 목록에도 네 asmdef를 기록했다. 나머지 RPG 원본 데이터의 보존 검사는 유지한다. 재활성화는 별도 의사결정이며 자동으로 define이나 빌드 씬을 복구하지 않는다.

## 서버 적용

대상: `C:/Users/user/Documents/GitHub/Local-LLM-AI/virtualEnv/RAG_model/app`.

- `llmClient.py`: 연결·읽기 제한, 스트림 완료 확인, 오류·중단 스트림을 예외로 전달.
- `server.py`, `server_test.py`, `server_for_experiment.py`: 오류·빈 답변의 성공 완료 차단. OpenAI 경로에도 요청 제한 적용.
- 새 `conversation_memory.py`: 대화 쓰기·초기화 잠금, 요약 생성 시점의 revision 검증, 파일의 원자적 교체. 오래된 요약의 덮어쓰기와 초기화 후 기억 부활을 차단한다.
- `summarizer_llm.py`: 추론 실패 시 설정된 규칙 기반 요약으로 복구.

실행 중인 서버 프로세스를 강제로 재시작하지 않았다. 서버 소스 변경은 해당 서버를 다시 시작한 뒤 적용된다. 키·모델·개인 대화 기록은 수정하지 않았다. 검토 가능한 전체 서버 변경은 `tools/npc_server_hardening.patch`에 보관한다. 적용 전 소스 백업은 Git 제외 경로인 `Logs/NpcServerHardening/original`에 있다.

## 검증

- 최종 결과: Unity 검사 22개 및 배포된 서버 소스의 오프라인 회귀 검사 7개 통과. 최종 Unity 로그의 C# 컴파일 오류·NullReferenceException·MissingReferenceException은 0이다.
- RPG 보존 검사: Git 기준으로 RPG 하위의 변경은 네 asmdef뿐이며, 각 파일도 define constraint 추가 외의 JSON 설정은 원본과 동일함을 확인했다. 기존 소스·씬·콘텐츠 데이터는 변경하지 않았다.
- Unity 6000.0.32f1 배치 컴파일 및 실제 PlayMode의 모의 클라이언트 검사: `Logs/InvestigationHardeningVerification.json`.
- 검증 진입점: `-executeMethod InvestigationHardeningVerification.Run`. 개인 대화 저장과 정보 해금 저장을 비활성화한 임시 씬을 사용한다. 실제 모델은 호출하지 않는다.
- 검사 범위: 1초 지연 유지와 빠른 답변 표시, 실패 시 저장·해금 차단, 늦은 응답 격리, 실제 씬 언로드 후 복구, tell/reply 무응답 복구, 취소된 네이티브 요청 해제, 모든 바이트 경계의 한글·이모지, 터미널 인자 누락 복구, 후속 항목 DB 조회.
- 서버 오프라인 회귀 검사: `python -B tools/tests/test_npc_hardening_server.py --app C:/Users/user/Documents/GitHub/Local-LLM-AI/virtualEnv/RAG_model/app`.
- 서버 검사는 연결 실패, 오류·중단·빈 스트림, 정상 응답, 제한 시간 설정, 요약 순서·초기화 격리, 반복 질문 규칙을 검사한다. 실제 서버 시작·키 로드·모델 추론은 하지 않는다.

첫 Unity 실행에서는 삭제된 데모의 옛 빌드 캐시가 재평가되며 누락 소스 오류를 출력한 뒤 재컴파일에 성공했다. 최종 검증은 갱신된 캐시로 다시 실행했다. 실제 모델 품질과 사람이 직접 전체 게임을 플레이하는 검사는 이번 회귀 검증 범위 밖이다.
