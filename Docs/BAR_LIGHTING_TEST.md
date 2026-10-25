# Bar 베이크 조명 비교

씬은 `Assets/Scenes/Bar_RetroLightingTest.unity`이며 **Tools > ProjectCRT > Open Bar Lighting Test**로 엽니다. 기존 `Bar.unity`와 `Bar_RetroTest.unity`는 비교 기준으로 유지합니다.

## 구성

- 공간과 캐릭터의 Unlit/고정 방향 툰 재질을 테스트 전용 URP Lit 재질로 복제했습니다. 기존 색과 텍스처를 옮기고 반사를 낮췄습니다. 기존 툰 셰이더는 베이크 조명을 샘플링하지 않아 그대로는 조명 비교가 되지 않습니다. 투명 디테일 재질은 유지합니다.
- 활성 공간 메시를 복제해 라이트맵 UV를 생성했습니다. 원본 FBX와 공유 재질, 텍스처는 수정하지 않습니다.
- 따뜻한 펜던트와 카운터 조명 3개는 Mixed / Shadowmask입니다. 정적 공간의 직접·간접광은 베이크하고, 움직이는 대상에는 실시간 직접광과 그림자를 제공합니다.
- 넓은 카운터 광원과 차가운 측면 광원은 Baked Rectangle입니다. 창문 광원은 분위기를 비교하기 위한 가상 광원입니다.
- 캐릭터의 명암이 완전히 뭉치지 않도록 약한 푸른 실시간 방향광을 추가했습니다. 이 보조광에는 그림자가 없습니다.
- 얼굴 주변에는 약한 따뜻한 실시간 보조광을 더했습니다. 그림자를 추가하지 않으며, Mixed 포인트 광원의 실시간 그림자는 256px 등급으로 맞춰 그림자 아틀라스의 자동 축소를 방지했습니다.
- Light Probes는 바닥 위부터 머리 높이까지 배치했습니다. 캐릭터와 움직이는 대상은 Blend Probes로 주변의 베이크 조명을 받습니다.
- 낮은 세기의 푸른 주변광, 약한 안개와 기존 레트로 화면 후처리를 사용합니다. 색을 두 번 따뜻하게 만들지 않도록 후처리 Tint는 흰색으로 조정했습니다.

## 조정 방법

Hierarchy의 **Bar Lighting - Warm Counter and Cool Ambient** 아래에서 광원을 조정합니다. Mixed와 Baked 광원의 위치·색·세기, 정적 메시와 재질을 바꾼 뒤에는 **Window > Rendering > Lighting > Generate Lighting**으로 다시 베이크하세요. Realtime 보조광과 `Bar Retro Look`의 화면 효과는 즉시 비교할 수 있습니다.

조명 설정 에셋: `Assets/Settings/BarRetro/Lighting/BarRetro_BakedLighting.lighting`. 초안은 Progressive CPU, 16 texels/unit, 간접광 128 samples, 3 bounces로 구성했습니다. 라이트맵 UV와 광원 방향, 빛샘을 확인한 뒤 최종 샘플 수를 올리는 용도입니다.

## 비교와 한계

같은 카메라의 캐릭터 근접 화면과 방 전체 화면을 `Logs/BarLighting/before_*.png`, `after_*.png`에 기록합니다. 완료 상태와 라이트맵·프로브 수는 `Logs/BarLighting/status.txt`에 기록합니다.

실제 베이크 결과는 라이트맵 아틀라스 1개, 정적 렌더러 40개, 베이크된 프로브 90개입니다. 씬을 다시 열어 UV·GI 재질·라이트맵·프로브가 유지되는지 검증했습니다. **Tools > ProjectCRT > Capture Bar Lighting Comparison**으로 고정된 두 시점의 전후 이미지를 다시 만들 수 있습니다. **Verify Bar Lighting Test**는 저장된 조명 씬을 다시 열고 검증합니다. 플레이 중 이동·상호작용 검증과 최종 품질 베이크는 이번 검증 범위에 포함하지 않습니다.

이번 비교에는 조명뿐 아니라 실제 조명을 받기 위한 재질 변경도 포함됩니다. 캐릭터의 고정 명암이 실제 광원에 따른 명암으로 바뀌므로 이전 툰 표현과 차이가 납니다. 기존 툰 표현과 베이크 조명을 함께 쓰려면 별도의 GI 지원 툰 셰이더 작업이 필요합니다. 투명 눈썹·속눈썹 같은 작은 디테일은 여전히 Unlit일 수 있습니다.
