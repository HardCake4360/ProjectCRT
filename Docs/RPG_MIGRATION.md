# ProjectCRT RPG 시스템 이식

작업일: 2026-10-09 (Asia/Seoul)

사용자가 선택한 범위는 **시스템과 제작 도구를 가져오고 별도 RPG 시작 씬을 만드는 것**이다. 기존 `Bar`, `Park`, `HomeScene`, `TitleScene`의 플레이 방식과 타이틀 버튼 연결은 유지한다.

## 실행

Unity 6000.0.32f1에서 **Project CRT > RPG > Create or Open Prototype**을 선택하고 Play를 누른다. 이미 생성된 씬은 `Assets/NoSleep/Scenes/ProjectCRT_RPG.unity`에 있다. 자체 타이틀의 **새 게임**을 선택하면 조사 사무실에서 시작한다.

1. 사무실의 문에서 E를 눌러 골목으로 이동한다.
2. 목격자와 대화하고 `본 것을 물어본다`를 선택한다.
3. 바닥의 영수증을 조사한다. 중복 조사는 아이템을 추가 지급하지 않는다.
4. 사무실로 돌아와 단말기를 조사한다. 진술과 영수증을 모두 확보했을 때 목표가 완료 상태로 바뀐다.
5. F5로 슬롯 1에 저장한다. F9로 저장 슬롯을 열어 맵·위치·진술·단서·NPC 상태를 복원한다. 자체 타이틀의 이어하기와 슬롯 선택도 사용할 수 있다.

조작: WASD/방향키 이동, Shift 달리기, E/Enter/Space 상호작용·대사 진행, Tab/I 상태·소지품, J 일지, Esc 메뉴 닫기, F5 저장, F9 불러오기. 선택지는 화면 버튼 또는 방향키와 확인 키를 사용한다.

## 가져온 기능

| 영역 | 기능 |
| --- | --- |
| 상태·저장 | 스위치, 정수 변수, 셀프 스위치, 아이템·소지금, 슬롯/백업, 저장 v4 및 v1~v3 읽기 |
| 맵 | XZ 격자, 지면/장식 레이어, 이동 제한, 프리팹 팔레트, 회전·높이·지역 ID, 이벤트 배치, 맵 이동 |
| 대사·이벤트 | 대사, 선택지, 조건별 페이지, 자동/접촉/병렬 이벤트, 공통 이벤트·인자, 조건 분기, 안정 ID, 원자적 일회 지급, 체크포인트 |
| NPC | 배치, 경로, 일과, 충돌·점유·예약, 이동/방향 명령, 저장 가능한 NPC 상태 |
| 조작·UI | 쿼터뷰 카메라, 이동·달리기·상호작용, 자체 타이틀, 상태·소지품·일지·슬롯 UI |
| 제작·진단 | Map Editor, Event Editor, Content Browser, Map Connections, 이벤트 템플릿·키 선언·참조 조회, Event Debugger, 콘텐츠 검사 |
| 선택적 시스템 | 접촉 전투, 생존/피로/각성/시간, 동료·여정·추억. CRT 시작 씬에는 SurvivalSettings를 지정하지 않았다. |
| 검증 예제 | 원본 Demo, Horror Prototype, Systems Lab, NPC Lab과 EditMode/PlayMode 테스트 |

패키지·URP·입력 프로젝트 설정은 동일한 버전이어서 교체하지 않았다. 기존 빌드 씬 순서를 유지하고 CRT RPG 씬만 마지막에 추가했다. 원본 예제 4개는 제작·시스템 참고용으로 가져왔으며 자동으로 빌드 목록에 넣지 않았다.

## 파일 구조와 제작

- 재사용 기반: `Assets/NoSleep/Runtime`, `Editor`, `Tests`. 원본 namespace와 asmdef를 유지해 기존 전역 `EventObject`, `PlayerControler`, 대사 시스템과 이름이 충돌하지 않는다.
- CRT 제작 데이터: `Assets/NoSleep/Data/ProjectCRT`. `Maps/Office.asset`, `Street.asset`, `Events`, `NPCs`, `Tiles`, `Materials`, `StateKeys.asset`을 제공한다.
- CRT 생성·메뉴: `Assets/NoSleep/Editor/ProjectCRT/RpgPrototypeBuilder.cs`.
- 원본 제작 가이드: `Docs/NoSleep/CREATOR_GUIDE.md`, `EVENT_SYSTEM_GUIDE.md`, `M3_AUTHORING_GUIDE.md`, `M4_NPC_GUIDE.md`.

CRT 데이터를 `Assets/NoSleep` 아래 둔 이유는 원본 편집기와 검사기의 검색 범위가 이 폴더로 지정되어 있기 때문이다. 새 맵/이벤트/NPC도 이 범위에 만들고 **Project CRT > RPG > Validate Content**를 실행한다. 원본 **No Sleep** 메뉴도 사용할 수 있다. 안정적인 맵/배치/이벤트 ID는 저장 키이므로 기존 저장이 있으면 신중하게 변경한다.

`Create or Open Prototype`은 이미 있는 씬을 열고 에셋 내용을 덮어쓰지 않는다. 생성이 중단되어 데이터만 남아 있는 경우도 덮어쓰기 대신 중단한다. 해당 데이터에서 씬을 복구하거나 별도의 새 데이터를 제작한다.

## 저장과 원본 추적

CRT RPG 저장은 `Application.persistentDataPath/ProjectCRT_RPG/Saves`에 있다. 기존 `investigation_inventory_*.json`과 NPC 조사 대화 저장은 그대로 유지한다. 원본 샘플은 같은 프로젝트의 `NoSleep/Saves`를 사용한다. `NoSleepBootstrap`에 제목·소개 문구·시작 안내·저장 폴더 설정을 추가했으며 원본 샘플의 기본값을 유지한다. 슬롯 포맷과 이전 로직은 변경하지 않았다.

`Docs/NoSleep/IMPORT_MANIFEST.json`은 원본의 에셋 488개(.meta 포함)와 문서 12개의 이식 전 기준 SHA-256이다. 이식 시 파일 일치와 기존 GUID 충돌 없음을 확인했다. CRT 적용으로 원본에서 바뀐 파일은 `NoSleepBootstrap.cs`, `GameUI.cs`, `GameUI.Progress.cs`이며 신규 CRT 파일은 별도 추가했다. `tools/import_nosleep.py`는 변경된 대상 파일을 보존하고 덮어쓰기를 거부하므로, 현재 커스터마이즈된 폴더에 다시 실행해서 업데이트하는 용도로는 사용하지 않는다.

`Docs/NoSleep/PROJECT_HISTORY.md`와 `VERIFICATION.md`는 원본 프로젝트의 과거 기록이다. 그 문서의 테스트 수치는 이번 ProjectCRT 검증 결과가 아니다. 원본에서 언급된 `Docs/Validation` 보고서와 임시 검증 폴더는 이식 대상에서 제외했다. 이번 결과는 `Docs/RPG_VERIFICATION.md`에 기록한다.

## 다음 연결 작업

이번 씬의 NPC는 데이터 기반 고정 대사·선택지를 사용한다. 기존 LLM 조사 UI/API, InvestigationInventoryManager, StoryManager, 오디오/연출·기존 타이틀과의 연결은 별도 작업이다. 단서 저장도 현재 RPG GameState가 관리하며 기존 조사 소지품과 자동 동기화하지 않는다. 별도 씬은 Unity에서 직접 열어 시작한다.

기존 씬을 전환할 때는 맵 데이터/기존 3D 공간의 역할, 기존 NPC UI 진입점, 입력 잠금과 카메라 전환, 단서·퀘스트 상태와 저장의 소유자를 먼저 정한다. 현재 CRT RPG 씬을 기존 싱글톤이 실행 중인 씬 위에 Additive로 로드하는 방식은 연결 전 사용하지 않는다. 원본의 후속 M5~M7(카메라·오디오 연출 확장, 정식 규칙, 설정·키 재지정·자동 저장 등)을 완성된 기능으로 간주하지 않는다.

현재 맵, 대사, 기본 큐브·캡슐 비주얼은 이식 검증용 샘플이며 ProjectCRT의 정식 사건/아트/게임 밸런스를 확정하지 않는다.
