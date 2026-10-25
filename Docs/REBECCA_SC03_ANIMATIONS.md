# SC-03 레베카 무료 애니메이션 적용

2026-10-25. 기존 레베카 모델과 재질, 샹그리아 배경을 유지하면서 SC-03의 대화와 이동 동작을 교체했다.

## 실행과 편집

`Assets/Scenes/ProjectCRT_SC03_ShangrilaReunion.unity`를 열어 Play, 문 근처에서 E로 입장한다. 대사는 E/Enter/Space로 진행하고, F6으로 다시 재생한다. `Tools > ProjectCRT > Cutscene Studio`에서 아래 시퀀스의 선택 턴 또는 전체 대본을 미리 볼 수 있다.

- 시퀀스: `Assets/Settings/Prologue/SC23/SC03_ShangrilaReunion.asset`
- 레베카 파생 동작: `Assets/Settings/Prologue/RebeccaAnimationTest/Animation`
- 다운로드한 원본과 이용 조건: `Assets/ThirdParty/CharacterAnimations`

## 받은 에셋

| 제작자 / 팩 | 공식 출처 | 이용 조건 | 이번 적용 |
|---|---|---|---|
| Quaternius / Universal Animation Library Standard | https://quaternius.itch.io/universal-animation-library | CC0 | 서 있기, 대화, 걷기, 복도 걷기 |
| Quaternius / Universal Animation Library 2 Standard | https://quaternius.itch.io/universal-animation-library-2 | CC0 | 기대기, 짧은 긍정·부정 반응 |
| Anderson Rohr / Crowd Reactions | https://andersonrohr.gumroad.com/l/mocap_pack4 | 개인·상업 프로젝트 무료, 단독 재배포·재판매·재포장 금지 | 웃음, 목을 만지는 반응, 고개 반응 |
| Anderson Rohr / Cleaning | https://andersonrohr.gumroad.com/l/mocap_pack2 | 위와 동일 | 유리잔 닦기 2종 확보. 후속 바 업무 연기에 사용할 후보 |

Gumroad 두 팩은 사용자가 지정한 수령 이메일로 각각 0원 수령했다. 전체 ZIP은 로컬 `Logs/RebeccaAnimationAssets/Downloads`에 보관한다. 프로젝트에는 Quaternius 원본과 선택한 Anderson Rohr iClone FBX 5개를 포함했다. 각 폴더의 `ATTRIBUTION.md`도 유지한다. Gumroad 원본 파일을 공개 에셋 팩으로 재배포하면 안 된다.

## 연결한 동작

| 장면 / 대사 | 동작 |
|---|---|
| 병을 들고 등장 | `Walk_Loop`. 기존 이동 경로를 유지하고 오른손 위치와 병 궤적을 함께 보정 |
| 일반 대화 / 상대 말 듣기 | `Idle_Talking_Loop` / `Idle_Loop`. 병을 내려놓기 전에는 오른손 접촉을 유지하는 별도 동작 사용 |
| 카운터에서 편하게 대화 | `Idle_Rail_Loop` 기반 기대기. 카운터 높이에 오른손 접촉 위치 보정 |
| “참 너답네.”, “그럴 줄 알았어.” | `Pointing And Laughing` 중 짧은 구간을 약하게 적용 |
| “응. 꽤 오래된 친구야.”, “맞아. 성실한 애야. 가끔은 너무 성실해서 탈이지만.” | `Yes`의 짧은 끄덕임 |
| “말린다고 들었을 것 같아?” | `Idle_No_Loop`의 짧은 부정 반응 |
| “왜, 안 어울려?” | `Scratch Neck` 중 짧은 구간 |
| “너라면 잘할 거야.” | `Holding Cup Nodding`의 머리·목 반응을 추출. 컵을 든 팔 동작은 제외 |
| 카운터를 나와 프라이빗 홀로 이동 | `Walk_Loop` → `Walk_Formal_Loop`. 기존 경로 길이에 보행 반복 수를 맞춤 |
| 방문 앞 대기 | 움직임을 줄인 `Idle_Loop` |

웃음·목 반응은 시작과 끝에서 기본 자세로 완화하며, 진행키를 빨리 눌러도 해당 짧은 반응이 끝나도록 연결했다. 문 밀기, 소리 듣기, 발견 직후의 놀람, 병 내려놓기는 기존 장면 전용 연기를 유지한다. 손·소품 접촉과 대본 연출을 보존하기 위한 선택이다.

## 적용 방식과 범위

다운로드 FBX의 Humanoid 동작을 원래 레베카 Avatar에 리타기팅한 뒤, 기존 뼈대 경로를 사용하는 별도 AnimationClip으로 베이크했다. 런타임은 기존 컷씬 재생·복원 구조를 그대로 사용한다. Source의 Humanoid Animator는 베이크 때만 활성화하며 재생 시에는 기존 컷씬 Animator를 사용한다.

이동 위치와 방향은 기존 SC-03 동작에서 가져오며, 외부 모션의 루트 이동을 누적하지 않는다. 발 높이를 보정하고 이동 거리에 맞춰 보행 횟수를 조절했다. 병은 보정한 오른손을 따라가는 별도 트랙으로 다시 생성했다. SC-03 원문 76개 대사, 카메라 프리셋, 산·제레미 연기와 다른 씬은 유지한다. 표정·립싱크·음성 생성은 이번 변경 범위에 포함하지 않는다.

동작을 다시 만들 때는 격리된 제작 프로젝트에서 `ProjectCRT.Prologue.Editor.RebeccaAnimationImport.BatchBuild`를 실행한다. 원본 SC23/Animation을 교체하지 않으므로 이전 동작으로 되돌릴 수 있다.

## 확인 자료

`Logs/RebeccaAnimationAssets`에 임포트·빌드·미리보기·실제 재생 검사와 반영 결과를 기록한다. 미리보기 검사는 모든 96개 턴과 원래 포즈 복원, 베이크한 뼈 움직임, 기존 이동 위치·방향, 반응 큐, 병과 손의 거리까지 검사한다. 실제 Play 검사는 SC-02에서 SC-03으로의 이동, 입장, 모든 대사, 종료, 재생 반복·취소·건너뛰기를 검사한다.
