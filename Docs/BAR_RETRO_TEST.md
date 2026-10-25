# Bar 그래픽 테스트

- 씬: `Assets/Scenes/Bar_RetroTest.unity`
- 메뉴: **Tools > ProjectCRT > Open Bar Retro Test**
- Game 뷰 또는 Play 모드에서 확인합니다. Scene 뷰에는 후처리를 적용하지 않습니다.
- Hierarchy의 **Bar Retro Look - toggle component to compare**에서 컴포넌트 또는 Effect Enabled를 끄면 화면 후처리를 비교할 수 있습니다. 비교 시 테스트 씬의 안개와 Forward 조명은 유지됩니다.
- Vertical Resolution: 기본 480줄. 화면 비율을 유지하므로 4:3에서는 640×480, 16:9에서는 약 853×480입니다.
- Color Bits: 기본 채널당 5비트. Dithering / Horizontal Blur / Saturation / Contrast / Tint / Vignette를 조정할 수 있습니다.

PC와 Mobile 파이프라인에 테스트 전용 Forward 렌더러를 인덱스 1로 추가했습니다. 기존 기본 렌더러 인덱스 0과 원본 Bar 씬, 공유 재질·텍스처는 유지합니다. 테스트 카메라는 인덱스 1을 명시적으로 사용합니다. 다른 씬에서는 후처리가 작동하지 않습니다.

현재 구현은 기존 카메라의 렌더 결과를 낮은 해상도로 샘플링한 뒤 색 보정·가로 번짐·4×4 디더링·색상 축소를 적용하고 Point 확대합니다. 3D 지오메트리 자체를 낮은 해상도로 렌더링하는 최적화는 포함하지 않습니다. Screen Space Overlay UI는 선명하게 유지됩니다.

이번 테스트에는 텍스처 교체, 모델 수정, 조명 재베이크가 포함되지 않습니다. 원작 스타일에 더 접근하려면 공간용 텍스처와 베이크 조명을 별도로 작업해야 합니다.

검증 이미지: `Logs/BarRetro/baseline.png`, `Logs/BarRetro/retro.png`. Unity 배치 실행으로 컴파일하고, 저장한 씬을 다시 열어 카메라의 렌더러 설정과 실제 출력 이미지를 확인했습니다.
