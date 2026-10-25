# 실외 거리·바 입구 테스트

씬: `Assets/Scenes/Outdoor_BarStreetTest.unity`

Unity 메뉴 **Tools > ProjectCRT > Open Outdoor Street Test**로 열고 Play 합니다.

## 조작

- WASD: 기존 플레이어로 걷기
- 마우스: 시점 회전
- Escape: 이동 일시정지·마우스 잠금 해제
- 일시정지 중 클릭 또는 Escape: 계속

기존 `Assets/prefab/Player.prefab`의 PlayerControler와 CharacterController, MainLoop 및 InputManager를 사용합니다. 자유 카메라는 `Free Camera (disabled reference)`로 비활성화해 유지합니다. 샹그리아 모델의 넓은 출입구가 거리를 향하도록 180도 돌렸으며, 원래 플레이어 규격으로 열린 입구를 걸어서 드나듭니다. E키, 순간이동, 씬 로딩 없이 같은 씬 안에서 이어집니다. 기존 `Bar.unity`는 변경하지 않았습니다.

## 구성

- McPato의 **Buildings & Facades | PS1 Asset Packs**를 다운로드해 원본 `DoriHome2.fbx`와 내부 텍스처 5개를 보존했습니다. 30개 메시로 구성된 배치와 건물·도로·가로등을 활용합니다.
- 재질은 별도 URP Lit 복사본으로 만들어 원본 아틀라스를 연결했습니다. 거친 재질과 낮은 반사를 사용합니다.
- 월드 조명: 청회색 Directional Light와 남색 계열의 Trilight 주변광.
- 안개: 청회색 Exponential Squared 거리 안개. 볼류메트릭 산란이나 높이 안개는 포함하지 않습니다.
- 가로등: 기존 등기구 5개 위치에 따뜻한 Spot Light를 연결하고, 광원 사이에는 어두운 구간을 남깁니다.
- 바 입구: 사용자 샹그리아 블록아웃의 실제 출입구, 실내 조명과 인도로 새는 Spot Light. 기존 임시 전실은 비활성 상태로 유지합니다.
- 화면: 기존 레트로 렌더러를 재사용하며 480줄, 5비트 색상과 디더링을 유지합니다.

이번 실외 조명은 실시간 방식입니다. 원본 모델의 Unity 임포트 설정에 라이트맵 UV 생성을 켰지만 베이크된 실외 GI·Light Probes는 포함하지 않았습니다. 거리 길이를 확장할 때 주요 조명만 실시간 그림자를 유지하고 나머지는 베이크하는 방향으로 조정할 수 있습니다.

## 조정 위치

- **Moonlight - cold world light**: 월드 광원의 색·밝기·각도
- **Outdoor Lighting and Bar Entrance**: 가로등과 바 입구
- **Outdoor Retro Look**: 디더링·픽셀 크기·화면 색감
- **Street Walking - Player and Spawn Points**: 기존 플레이어 연결, 거리 시작점과 출입구 양쪽 기준점
- **Shangrila - placement lighting**: 샹그리아의 실내·입구 조명
- **Window > Rendering > Lighting > Environment**: 주변광과 안개 설정

**Tools > ProjectCRT > Verify Outdoor Street Test**는 저장된 씬을 다시 열고 재질·텍스처·카메라·조명·안개 설정을 검사합니다. 저장하지 않은 씬 변경은 먼저 저장하세요.

검증 이미지: `Logs/OutdoorStreet/street.png`, `bar_entrance.png`, `fog_distance.png`.

초기 자유 카메라 버전의 검증 이미지는 `Logs/OutdoorStreet/`에 남아 있습니다. 현재 보행·출입 검증은 **Tools > ProjectCRT > Verify Street Walking and Entry**로 실행합니다. 기존 플레이어 캡슐을 실제 충돌 환경에서 이동시켜 거리→출입구→실내→거리 왕복, 실내 벽 충돌과 바닥 접지를 확인합니다. 검증 결과와 1인칭 이미지는 `Logs/StreetWalk/verification.txt`, `inside.png`, `outside.png`에 기록합니다. 검증 중 벽 충돌 시험의 시작 위치만 재설정하며, 실제 플레이어의 출입에는 순간이동을 쓰지 않습니다.

## 크레딧

3D Model: McPato — Buildings & Facades | PS1 Asset Packs

Source: https://mcpato.itch.io/buildings-facades-ps1-asset-packs

License: CC BY 4.0 — https://creativecommons.org/licenses/by/4.0/

변경 내역과 배포 시 유지할 크레딧은 `Assets/ThirdParty/McPatoBuildings/ATTRIBUTION.md`에 기록했습니다. 기존 Bar 씬과 실내 테스트 씬은 수정하지 않습니다.


## 샹그리아 블록아웃 배치

사용자 모델 `Assets/resources/3Dmodels/maps/shangrila.fbx`를 `Shangrila - user blockout`으로 배치했습니다. 모델의 비율과 원본 재질을 유지하고 두 메시에는 씬 전용 MeshCollider를 추가했습니다. 기존 임시 바 전실과 겹치는 McPato 건물 5개는 삭제하지 않고 비활성화했습니다. 새 바 조명은 `Shangrila - placement lighting`에서 조정합니다. 기존 Bar 및 실내 테스트 씬은 그대로 유지합니다. 현재 블록아웃에는 텍스처 추가나 씬 전환 기능을 만들지 않았습니다. 배치 확인 이미지는 `Logs/ShangrilaPlacement.png`입니다.


## 샹그리아 NPC 대화

기존 Bar 씬의 레베카와 제레미를 `Shangrila - Bar NPCs` 아래 복제해 실내 양쪽에 배치했습니다. 기존 대사·조사 컨트롤러·성격 키·애니메이션과 대화 기록 설정을 유지합니다. 원본 Bar 씬과 NPC 모델/애니메이션 에셋은 이번 작업에서 수정하지 않았습니다.

2m 이내에서 NPC를 바라보면 이름과 [E] 안내가 표시됩니다. E로 기존 대화/조사 기능을 열고, 닫기 버튼 또는 ESC로 종료하면 보행으로 돌아갑니다. 첫 대화 여부는 기존 NPC별 저장 기록을 따릅니다. 레베카의 기존 첫 대사 이후 조사 UI로 이어지고, 제레미는 기존 인사와 조사 UI를 사용합니다. 대화 중에는 NPC별로 배치한 전용 클로즈업 구도로 전환하며 NPC가 대화 카메라를 바라봅니다. 종료 시 보행 시점과 원래 화각으로 돌아옵니다.

기존 DialogueUI, NpcInvestigationUI와 UI 입력용 EventSystem을 씬에 연결했습니다. 대화 중 거리 HUD를 숨겨 UI 중첩을 줄였습니다. AI 질문 응답은 기존 로컬 대화 서버(localhost:5000)가 실행 중이어야 합니다.

검증 메뉴: **Tools > ProjectCRT > Verify Shangrila NPC Conversations**. 플레이어의 실제 상호작용 광선으로 두 NPC 대화 열기, 레베카 첫 대사 연결, 기존 조사창 열기/닫기, 보행 복귀와 거리 제한을 확인합니다. 검증은 대화 기록 저장을 일시적으로 끄고 네트워크 요청을 보내지 않습니다. 결과: `Logs/ShangrilaNpcVerification.txt`. NPC 배치 이후 거리 왕복 및 출입 충돌 검증도 다시 통과했습니다. 서버의 AI 응답 생성은 이번 검증 범위에 포함하지 않습니다.


### NPC 클로즈업 조정

각 NPC 아래의 `Conversation Shot` 오브젝트가 대화 구도를 결정합니다. 얼굴 높이에 맞춘 위치와 대화 애니메이션의 얼굴 방향을 고려한 정면 각도, 34도 화각을 사용합니다. 얼굴은 오른쪽 조사창과 아래쪽 대화창을 피해 화면 왼쪽 위에 배치합니다. NPC별 위치와 회전은 씬에서 직접 조정할 수 있습니다. 클로즈업 동안에만 약한 따뜻한 얼굴 보조광을 켜고, 보행용 카메라의 레트로 렌더링을 그대로 사용하며, 별도 카메라 렌더러를 추가하지 않습니다. 기존 Interactable의 클로즈업 시작·종료 호출에 연결되어 첫 대사부터 조사 대화가 끝날 때까지 구도를 유지합니다.


레베카는 머리 뼈를 시선 목표로 사용해 대화 애니메이션 중에도 얼굴이 같은 화면 영역에 남도록 합니다. 제레미는 모델에 맞춘 고정 구도를 사용합니다. 전용 보조광은 Conversation Shot의 비활성화와 함께 꺼집니다. 대화 종료 시 위치·회전·원래 화각 복귀, 두 NPC의 얼굴 투영 위치와 벽 가림 검사를 통과했습니다. Full HD 플레이 캡처는 `Logs/ShangrilaCloseup/rebecca-intro.png`, `rebecca.png`, `rebecca-idle.png`, `jeremy.png`에 있습니다.


### 부드러운 대화 카메라 전환

`ShangrilaConversationShot`의 Enter Duration(기본 0.7초), Return Duration(기본 0.6초)으로 전환 시간을 조정합니다. 위치·회전·화각을 함께 보간하며 시작과 끝에서 부드럽게 감속합니다. 진입 중 대화를 닫아도 현재 시점에서 복귀를 시작합니다. 복귀가 끝날 때까지 보행·마우스 시점·새 상호작용 입력을 잠시 막아 카메라 제어가 충돌하지 않게 합니다. 일시정지 중에도 전환이 완료되도록 실제 시간 기준으로 움직입니다. 기존 클로즈업 카메라는 선택적 인터페이스를 통해 연결하므로 원본 Bar의 다른 카메라 동작은 기존 경로를 유지합니다.


### 카메라 도착 후 대화창 표시

대화 컨트롤러는 카메라 진입 완료를 확인한 다음 첫 대사 또는 조사창을 엽니다. 실외 테스트 씬의 두 대화 캔버스에만 `ConversationCanvasFade`를 추가해 약 0.28초에 걸쳐 투명도 0→1로 표시합니다. 페이드가 끝나면 버튼 입력을 허용합니다. 이동 도중 종료하면 대기 중인 표시 작업을 취소하므로 창이 뒤늦게 열리지 않습니다. 카메라 전환을 사용하지 않는 원본 씬의 대화 시작 시점은 유지합니다. 재설정 메뉴는 **Tools > ProjectCRT > Configure Conversation Window Fade**입니다.
