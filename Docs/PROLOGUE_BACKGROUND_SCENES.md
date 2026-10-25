# SC-01~SC-03 배경 배치

제작 기준: 2026-10-25. 원문: `C:/Users/user/Downloads/Project_CRT_SC01_SC03_컷씬_대본_원문.md`.

후속 업데이트: SC-01에는 산·기사 연기와 대사·카메라워크가 연결되었다. 현재 SC-01의 재생/편집 방법은 [SC01_CUTSCENE.md](SC01_CUTSCENE.md)를 따른다. 아래 내용은 최초 배경 배치 기록이며 SC-02/SC-03은 배경 단계다.

이번 결과는 **배경·소품과 카메라 구도 초안**이다. 대사 재생, 캐릭터 연기, 실제 플레이어 동선, 장면 간 자동 전환은 후속 단계다. 기존 사건 진행용 씬을 대체하지 않는다.

## 만든 씬

| 씬 파일: Assets/Scenes/ | 배치 내용 | 카메라 |
| --- | --- | --- |
| ProjectCRT_SC01_TaxiArrival.unity | 비 오는 고가도로·도시, 실내가 있는 택시, 대시보드 CD 플레이어·케이스·디스크, 낡은 주거 건물 앞·짐가방 | 8 |
| ProjectCRT_SC02_SanRoom.unity | 작은 방, 소파·벽시계·이삿짐 상자 5개·침대·옷장·CRT·테이블·램프, 창의 차가운 빛과 실내의 따뜻한 빛 | 3 |
| ProjectCRT_SC03_ShangrilaReunion.unity | 기존 샹그리아 구조·거리, 카운터·스툴 3개·병 선반·병 16개·잔·접시·손님 테이블, 기존 복도·2번 룸의 독립 문·소파·테이블 | 12 |

SC-01은 고가도로(원점), 택시 실내 촬영 구역(X=90), 도착지(X=180)를 한 씬 안에서 분리했다. 주행과 하차는 아직 정적 배치다. 택시의 문·운전대는 원본 분리 부품을 유지한다. CD 소품과 도로·교각·가로등은 간단한 형상으로 보충했다. 비는 기본 파티클이며 유리의 빗방울·와이퍼·물보라·실시간 반사는 후속 작업이다.

SC-02의 시계 바늘은 19:35를 목표로 조정했다. 방의 천장 덮개는 고정 카메라 검수용으로 생략했다. 실제 조작 단계에서는 충돌과 고정 카메라 영역을 별도로 다듬어야 한다.

SC-03은 `Outdoor_BarStreetTest.unity`의 복사본을 바탕으로 제작했다. `shangrila.fbx`를 참조하는 기존 구조를 유지하고, 새 가구·바닥 마감·상부 벽·천장만 독립 오브젝트로 덧붙였다. 2번 룸의 실제 개구부는 대략 X=4.2~4.6, Z=11.75~12.65이며, 문과 카메라를 여기에 맞췄다. 문은 현재 검수하기 편하게 열려 있다. 발견 장면의 제레미 위치는 `MARK - jeremy_discovery_floor`로 표시했다. 신체 표현·수사 장비·경찰 봉쇄는 이번 범위에 넣지 않았다.

## 확인 방법

1. 원하는 새 씬을 열고 Play를 누른다.
2. `[` / `]` 또는 PageUp / PageDown으로 해당 씬의 카메라 프리셋을 전환한다.
3. WASD 이동, Q/E 높이, 마우스 오른쪽 버튼 시선 조정, Shift 빠른 이동을 사용할 수 있다.
4. 편집 중에는 `Background Stage` 오브젝트의 `Prologue Background Preview` 컴포넌트 메뉴에서 `Preview/Next Camera` 또는 `Previous Camera`를 사용한다.

각 Stage에는 기존 CutsceneStage, CutsceneCameraPreset, 배우 참조와 동선 마커가 연결되어 있다. 배경 검수 중에는 `Original cast - staging references (performance pending)` 그룹을 꺼 두었다. 산·레베카·제레미를 새 에셋으로 교체하지 않았으며, 원본 외형과 재질을 유지했다. 배경을 승인한 다음 이 그룹의 배우 위치·앉는 자세·시선·표정을 각 턴에 맞춰 연출한다. 택시 기사 외형과 연기는 아직 추가하지 않았다.

복사된 SC-03의 기존 플레이어·대화·사건 관리 루트는 꺼 두었다. 이번 배경 확인을 위해 과거 컷씬이나 저장 상태가 자동 실행되지 않는다. 원래 거리/사건 테스트 씬의 활성 상태는 바꾸지 않았다. 새 씬은 Build Settings의 끝에 추가하며 시작 씬 순서는 유지한다.

## 다운로드한 무료 팩과 크레딧

다운로드는 각 제작자의 무료 파일을 이용했다. 유료 확장판은 구매하지 않았다. 소스는 `Assets/ThirdParty/Prologue/` 아래에 보존하고, 새 URP 재질은 `Assets/Settings/Prologue/`에 분리했다. FBX 내장 텍스처를 추출하고 Point 필터를 적용했다. 원본 배포 스크립트와 .blend는 Unity 자동 실행/자동 변환 대상에 넣지 않았다.

| 폴더 | 출처 / 제작자 | 조건·사용 |
| --- | --- | --- |
| BricchiCar | [Retro Car With Interior — Bricchi Games](https://bricchi-games.itch.io/retro-car-with-interior) | 제작자 페이지의 Creative Commons 표기, 상업 사용 허용·크레딧 필요. 정확한 CC 세부 종류를 임의로 확정하지 않음. 택시로 색 조정·간판 추가 |
| ElegantCrowSkyscrapers | [Retro Skyscraper Pack — Elegant Crow](https://elegantcrow.itch.io/retro-skyscraper-pack) | CC0. SC-01 도시 배경 |
| McPatoInterior | [House Interior — McPato](https://mcpato.itch.io/house-interior-psx-assets) | CC BY 4.0. 가방·실내 텍스처 |
| McPatoBar | [Barranco Bar — McPato](https://mcpato.itch.io/barranco-bar-ps1-environment) | CC BY 4.0. 병·잔 |
| FurnitureFree | [PSX Furniture Free — heyheythere](https://heyheythere.itch.io/psx-furniture-free) | CC BY 4.0. 소파·시계·상자·가구. 제작자 AI 보조 사용 표기 있음 |
| GGBotCars | [PSX Style Cars — GGBotNet](https://ggbot.itch.io/psx-style-cars) | CC0. 배경 차량·차량 소리 후보로 확보, 현재 씬에는 미배치 |

추가로 이미 프로젝트에 있던 McPato 건물 외벽 팩을 SC-01 도착지와 SC-03 거리에서 재사용했다. 기존 출처와 CC BY 4.0 기록을 유지한다. 수정 사항은 재질 변환·색·크기·방향·배치 및 일부 메시 부품의 재사용이다.

배포 크레딧에는 다음 출처를 포함한다.

- Retro Car With Interior: Bricchi Games — https://bricchi-games.itch.io/retro-car-with-interior — Creative Commons, adapted for ProjectCRT.
- 3D Model: McPato — House Interior / Barranco Bar / Buildings Facades — CC BY 4.0 — https://mcpato.itch.io — adapted for ProjectCRT.
- PSX Furniture Free by heyheythere - https://heyheythere.itch.io/psx-furniture-free - CC BY 4.0.
- Retro Skyscraper Pack: Elegant Crow — CC0; PSX Style Cars: GGBotNet — CC0.

각 팩의 `ATTRIBUTION.md`와 동봉 라이선스를 함께 보존한다. 다운로드 크기·SHA256은 `Logs/PrologueAssets/downloads.json`에 기록했다.

## 검증과 다음 작업

Unity 6000.0.32f1의 별도 프로젝트 복사본에서 실제 임포트·C# 컴파일·씬 생성과 카메라 렌더를 수행했다. 사용자의 실행 중 편집기는 창 활성화가 실패하여 전환하지 않았다. 복사본은 독립 Library를 사용하며, 완성한 새 씬·새 재질과 필요한 메타데이터를 원래 프로젝트로 가져왔다. 기존에 열려 있는 씬의 미저장 변경을 저장하거나 폐기하지 않았다.

검증 자료는 `Logs/PrologueAssets/layout-report.txt`, `validation.txt`, `Previews/SC01-01.png` 등의 이미지다. 세 씬의 누락 스크립트와 활성 렌더러의 재질/셰이더를 검사하고 전체 23개 구도를 렌더했다. 원본 샹그리아·산·레베카·제레미 FBX의 SHA256을 작업 전후 비교한다. 본 배치는 최종 아트가 아니며, 배경 승인 후 캐릭터 크기에 맞춘 카메라 조정·문과 소품 접촉·경로 이동·대사·음향을 순서대로 제작한다.
