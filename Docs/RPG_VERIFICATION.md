# ProjectCRT RPG 이식 검증

검증 작업일: 2026-10-09 (Asia/Seoul)

## 실제 실행 결과

Unity **6000.0.32f1**에서 실제 ProjectCRT 프로젝트를 대상으로 실행했다. 기존 패키지·렌더 파이프라인·입력 설정을 유지했다.

| 검사 | 결과 |
| --- | --- |
| Unity Runtime/Editor/Test 어셈블리 임포트·컴파일 | 통과 |
| `ProjectCRT.RPG.Editor.RpgPrototypeBuilder.BuildAndValidate` | CRT 씬/데이터 생성 및 전체 콘텐츠 검사 통과 |
| Unity EditMode | **273/273 통과**, 실패/건너뜀 0 |
| Unity PlayMode (그래픽 사용) | **96/96 통과**, 실패/건너뜀 0 |
| 합계 | **369/369 통과** |
| 원본 기준선 SHA-256 | 원본 에셋 488개 + 문서 12개 일치, 원본 변경 없음 |
| 이식 후 기준선 | 497개 파일 그대로 유지, 화면·저장 설정을 위한 3개 코드 파일 변경 |
| GUID·참조·메타 | Assets GUID 912개 중복 없음, 이식 에셋의 직렬화 참조 311개 해석, `.meta` 누락 없음 |
| 기존 제작 데이터 보존 | CRT 생성 메뉴 2회 재실행 시 데이터·씬·기존 씬 파일 바이트 동일, 빌드 목록 중복/재정렬 없음 |

원본 테스트 363개(EditMode 270 + PlayMode 93)와 새 CRT 통합 테스트 6개(EditMode 3 + PlayMode 3)를 실행했다. 결과 원본은 [EditMode XML](Validation/RpgEditMode.xml)과 [PlayMode XML](Validation/RpgPlayMode.xml)이다. 로컬 실행 로그는 Git에서 제외된 `Logs/rpg_import_compile.log`, `rpg_editmode.log`, `rpg_playmode.log`에 있다.

## CRT 통합 테스트 범위

- CRT 맵·배치·NPC·연결의 유효성, 원본 제작 브라우저 검색 범위 포함 여부.
- 생성된 씬의 CRT 제목/시작 맵/두 맵 카탈로그/독립 저장 폴더/생존 비활성 설정.
- 기존 씬과 제작 파일·안정 ID·빌드 순서를 생성 메뉴 재실행 시 보존.
- 실제 Bootstrap/UI의 타이틀 표시와 입력 잠금, 새 게임·시작 자동 이벤트·조사 목표 설정.
- 사무실↔골목 이동, 목격자 진술, 영수증 획득과 재조사 시 중복 지급 방지.
- RPG 슬롯 저장 후 맵·플레이어 위치·단서·NPC 상태 복원 및 새 게임 초기화. 테스트 저장은 고유 임시 디렉터리만 사용한다.
- 대화 거절 선택지와 진술 없는 상태에서는 조사 완료로 넘어가지 않음. 두 조건 확보 후 정상 완료.

기존 전체 회귀 테스트에는 상태/저장 v1~v4, 공통·병렬 이벤트·안전 저장 경계, 맵 제작·참조 검사, NPC 이동/점유/일과/예약 저장과 전투·생존/여정 샘플이 포함된다.

## 실행 중 수정 사항

신규 EditMode 테스트의 NUnit 컬렉션 제약 표현을 수정했다. 생성 메뉴 보존 테스트에서 Unity가 이름 없는 배치 테스트 씬을 `RestoreSceneManagerSetup`으로 복원하지 못해, 해당 경우 빈 테스트 씬을 새로 만드는 처리를 추가했다. 수정 후 전체 EditMode와 PlayMode를 다시 실행했으며 위 표는 최종 실행 결과다.

Unity 실행에는 로컬 라이선스 서비스 접근이 필요해 권한 확장된 숨김 배치 프로세스를 사용했다. 사람의 수동 플레이·에디터 조작·화면 레이아웃 평가와 독립 실행 빌드는 이번에 실행하지 않았다. 기존 LLM 조사/대화 API 및 조사 인벤토리 통합은 이번 범위에 포함하지 않았다.

## 다시 검증하기

먼저 Unity에서 **Project CRT > RPG > Validate Content**를 실행한다. Test Runner에서 EditMode와 PlayMode 전체를 실행할 수 있다. 게임 세션이 없는 빈 씬에서 테스트를 시작한다.

배치 테스트는 Unity 실행 파일에 `-batchmode -projectPath <ProjectCRT 경로> -runTests -testPlatform EditMode -testResults <결과.xml> -logFile <로그.log>` 인자를 사용한다. PlayMode에서는 `-testPlatform PlayMode`로 바꾸고 `-nographics`를 사용하지 않는다. 생성/검사는 `-executeMethod ProjectCRT.RPG.Editor.RpgPrototypeBuilder.BuildAndValidate -quit`로 실행한다.

정적 참조·기준선 검사는 프로젝트 루트에서 `python tools/verify_rpg_import.py`로 실행한다. 향후 원본 이식 파일을 변경하면 `CUSTOMIZED` 목록과 가이드의 변경 목록을 함께 갱신한다. 이식 기준선 해시는 변경 전 원본 추적용으로 유지한다.
