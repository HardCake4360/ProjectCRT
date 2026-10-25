# 컷씬 제작과 테스트

## 바로 확인하기

1. `Assets/Scenes/Shangrila_CutsceneTest.unity`를 엽니다. 기존 거리 씬을 복제한 전용 테스트 씬입니다.
2. Play 후 **F6**으로 샹그리아 샘플을 시작합니다.
3. **E / Enter / Space**는 출력 중인 대사를 전체 표시하고, 다음 입력은 다음 턴으로 이동합니다. **ESC**는 전체 스킵입니다.
4. **F8**은 캐릭터 설정 → 반복음 → 음절형 비교용 소리 순으로 음성 방식을 바꿉니다. 비교용 소리는 직접 합성한 임시 샘플이며 실제 한국어를 읽는 TTS가 아닙니다.
5. 컷씬이 끝나면 카메라·캐릭터 상태와 보행 조작이 복구됩니다.

대본은 `Assets/Settings/Cutscenes/Shangrila_Introduction.asset`입니다. 8턴으로 전경, 경로 이동, 이름이 있는 대사, 이름 없는 속마음, 애니메이션, 포즈·응시와 줌을 확인할 수 있습니다. 샘플은 자동 시작하지 않습니다. 기존 Bar와 Outdoor 씬의 저장 내용은 변경하지 않습니다.

## Play 없이 확인하기

**Tools > ProjectCRT > Cutscene Studio**를 엽니다. Sequence에는 대본을, Stage에는 테스트 씬의 `Cutscene System`을 지정합니다.

- 전체 재생, 선택 턴만 재생, 선택 턴부터 재생
- 이전/다음 턴, 시간 이동, 정지, 배속, 자동 진행
- 실제 화면 비율의 카메라·대사창 미리보기
- 턴 목록과 인스펙터 편집, 턴 추가·복제
- Scene View 구도를 카메라 프리셋에 저장(현재 구도를 유지하도록 프리셋의 자동 응시 연결은 해제하며, 필요하면 다시 지정)
- 누락된 배우·카메라·경로·포즈·표정 바인딩 검사

앞선 턴의 상태를 재구성한 다음 선택 턴을 평가합니다. 미리보기는 씬 로딩, 게임 진행 이벤트, 대화 저장, 네트워크 요청을 실행하지 않습니다. 정지·창 닫기·다른 대본 선택·재컴파일·Play 전환 시 카메라와 캐릭터 상태를 복원합니다. 카메라 프리셋 저장과 턴 편집은 명시적인 편집 작업이며 Undo를 지원합니다. 화면 미리보기는 무음입니다. 선택한 일반 대사에서 **반복음 미리듣기 / 음절형 미리듣기** 버튼으로 Play 없이 보이스를 비교할 수 있습니다. 임시 파형만 만들며 에셋을 저장하지 않고 최대 30초까지 재생합니다. Unity 에디터의 미리듣기 API를 지원하지 않는 버전에서는 안내를 표시합니다. 실제 화면과 함께 확인하려면 F6/F8을 사용합니다.

## 기본 구성

| 항목 | 역할 |
| --- | --- |
| CutsceneSequence | 턴 목록, 준비할 씬/소리, 스킵 허용, 종료 정책 |
| CutsceneStage | 실제 카메라, 플레이어, 배우, 위치·카메라·경로 ID 연결 |
| CutsceneDirector | 준비, 입력 제한, 턴 실행, 로딩, 스킵/취소, 복원 |
| CutsceneActor | 캐릭터 표시 이름·음성, 포즈·표정과 애니메이션 연결 |
| CutsceneCameraPreset / CutscenePath | 씬에 배치하는 카메라 위치와 곡선 경로 |
| CutsceneVoiceProfile | 반복음과 음절/모음 샘플, 높이, 볼륨, 반복 간격 |
| CutsceneTrigger | 기존 E 상호작용 또는 트리거 진입으로 시작, 완료/스킵 이벤트 |

대본은 씬 오브젝트를 직접 저장하지 않고 ID를 사용합니다. 배우·카메라·경로·위치의 ID는 같은 무대 안에서 중복되지 않아야 합니다. 새 대본은 **Create > ProjectCRT > Cutscene > Sequence**로 만듭니다.

## 턴 설정

`Has Dialogue`를 끄면 대사 없는 연출 턴입니다. 대사가 있으면 화자 ID와 표시 방식(Spoken / Narration / Thought)을 지정합니다. 이름은 Profile / Hidden / Alias로 설정합니다. 기본적으로 나레이션과 속마음은 이름과 보이스를 숨깁니다. 원하면 Alias와 음성 모드로 개별 지정할 수 있습니다.

카메라 도착 후 창을 페이드 인하고, 창이 나타난 뒤 글자를 출력하는 것이 기본입니다. `Dialogue After Camera`를 끄면 카메라가 움직이는 중에 대사를 표시합니다. 대기 조건은 다음과 같습니다.

- `Advance`: 진행키(Manual) 또는 자동(Auto)
- `Minimum Duration`: 턴 최소 유지 시간
- `Wait For Camera / Actors / Timeline`: 각각 완료까지 대기
- `Auto Delay`: 대사 출력 완료 후 자동 진행까지의 여유

대사는 전체 문장을 TMP에 배치한 뒤 표시 글자 수를 늘립니다. 공백·구두점은 보이스를 내지 않고 구두점 뒤에 여유를 둡니다. 전체 표시 입력 시 남은 글자의 보이스를 한꺼번에 재생하지 않습니다. 일반 효과음과 대사 보이스는 별도 AudioSource입니다.

배우 동작에는 위치 프리셋 이동, 회전, 고정 포즈, AnimationClip, 표정, 응시 대상을 지정합니다. `Timing`으로 턴 시작, 카메라 도착, 특정 글자(1부터 시작), 대사 완료, 다음 턴 진행 시점을 선택하고 `Delay`로 지연을 줍니다. `Start Time`은 애니메이션 자체의 시작 시각입니다. 다음 턴에서 지정하지 않은 포즈·표정은 유지됩니다. 진행 시점 동작에 지연이 있으면 해당 연출을 마친 후 다음 턴에 진입합니다.

표정 프리셋은 모델이 가진 BlendShape 값 또는 얼굴 재질 슬롯 교체를 연결합니다. 현재 모델에 없는 표정을 자동 생성하지 않습니다. 새로운 표정 리소스가 생기면 배우의 `Expressions` 목록에 연결합니다.

## 카메라와 경로

- Maintain: 이전 구도/움직임 유지
- Cut: 지정 구도로 즉시 전환
- Blend: 위치·각도·화각을 함께 보간
- Rough: 짧고 빠른 보간
- Cut Then Zoom: 즉시 전환 후 화각 변화
- Path: 곡선의 시작/종료 지점과 시간 지정, 진입 보간 후 이동

카메라 프리셋은 위치·회전·화각과 응시 대상/화면 배치를 가집니다. 경로는 씬의 Knot 오브젝트로 편집하는 Catmull–Rom 곡선입니다. 등속 이동 옵션과 경로의 일부 구간 재생, 역방향 이동을 지원합니다. 위치 이동과 화각 변화는 서로 다른 연출입니다. 이동 중 응시 대상은 별도로 지정할 수 있습니다.

## Timeline

턴에 TimelineAsset을 연결할 수 있습니다. Animation Track 이름을 배우 ID 또는 `actor:배우ID`로 지정하면 해당 Animator에 연결합니다. Audio Track은 컷씬 전용 소스로 출력합니다. 같은 배우를 그 턴의 애니메이션 명령과 Timeline이 동시에 제어하도록 설정하면 검증 오류를 표시합니다.

현재 연결기는 Animation / Audio Track을 지원합니다. Signal, Activation 등 게임 상태를 바꿀 수 있는 트랙은 검증에서 거부합니다. 카메라는 컷씬의 위치/경로 프리셋으로 제어합니다. 편집 미리보기에서는 애니메이션 이벤트와 오디오를 억제합니다. 실행 종료 시 Timeline 그래프와 원래 NPC 애니메이션 제어를 정리합니다.

## 로딩과 종료 정책

`Stage Scene`을 비워 두면 현재 무대를 사용합니다. 지정하면 암전 중 해당 씬을 Additive 로딩하고 원래 씬의 오브젝트를 일시 중지합니다. 필요한 음성 데이터를 준비한 다음 컷씬을 시작합니다. 임시 무대를 언로드하거나, `Unload Stage On Finish`를 꺼서 다음 재생을 위해 비활성 상태로 보관할 수 있습니다.

- Return: 기존 시점/위치와 조작 복원
- Return To Previous Scene: 별도 무대를 닫고 유지해 둔 이전 씬으로 복귀
- Load Scene: 목표 씬을 로딩하고 `Destination Spawn ID`에 플레이어 배치

Stage Scene과 목표 씬은 Build Settings에 있어야 합니다. 목표 스폰은 목표 씬의 Stage > Spawns 목록에서 연결합니다. 기존 SceneLoader가 로딩 후 조작을 자동으로 푸는 동작과 충돌하지 않도록 컷씬 재생기가 전환과 입력 복구를 소유합니다.

정상 종료와 전체 스킵은 `Committed` / Trigger의 `On Committed`를 한 번 호출합니다. 이 이벤트에 퀘스트·진행도 저장을 연결합니다. Cancel은 진행도를 확정하지 않고 복구합니다. 재생 중 오류도 입력·카메라·배우 제어를 정리합니다. 진행키는 컷씬 준비/카메라 이동/창 페이드 중에는 대사를 넘기지 않습니다.

## 검증

- **Tools > ProjectCRT > Cutscenes > Verify Runtime Lifecycle**
- **Tools > ProjectCRT > Cutscenes > Verify Preview Restoration**
- **Tools > ProjectCRT > Cutscenes > Verify Scene Loading and Timeline**

결과는 `Logs/Cutscenes/`에 기록합니다. 런타임 검증은 임시 대본을 사용하며 NPC 대화 저장과 AI 서버 호출을 실행하지 않습니다. 샘플 캡처는 `turn-*.png`입니다. 검증 실행 중에는 스크립트를 편집하지 마세요. Play 중 도메인이 재로드되면 검증 코루틴이 중단됩니다.


검증 결과(2026-10-23): 런타임 입력/타이핑/동작 시점/스킵/취소/복귀, 편집 미리보기 복원, 실제 Additive 및 목표 씬 로딩, 캐시 무대 재사용, Timeline 연결, 에디터의 두 음성 방식 미리듣기 API 호출, 기존 NPC 대화 검증을 통과했습니다. 음성 미리듣기 보고서는 `Logs/Cutscenes/audition-verification.txt`, 기존 대화 보고서는 `Logs/ShangrilaNpcVerification.txt`입니다.
