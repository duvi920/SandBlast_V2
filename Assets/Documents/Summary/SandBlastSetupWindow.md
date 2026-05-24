# SandBlastSetupWindow

> `Assets/Scripts/Editor/SandBlastSetupWindow.cs`

## 역할

씬에 SandBlast 오브젝트를 자동으로 구성하는 유니티 에디터 창.  
메뉴: **SandBlast > Setup Scene** (`Ctrl+Shift+S`)

## 기능

### Setup Scene 버튼
다음 오브젝트를 씬에 자동 생성한다 (Undo 지원):

| 오브젝트 | 컴포넌트 | 설명 |
|---|---|---|
| `SandBlastEngine` | SandBlastEngine, SandBlastDebugPainter | 루트 엔진 |
| `PixelLayer` (자식 Quad) | PixelLayerRenderer, Renderer | 픽셀 레이어 렌더링 |
| Main Camera 조정 | — | 직교 투영으로 그리드 전체를 화면에 맞춤 |

- 이미 `SandBlastEngine` 이 씬에 있으면 버튼 비활성화
- 픽셀 레이어 Quad 의 Z = 0.1 (강체가 z=0 에 있어 픽셀 레이어 뒤에 렌더링됨)
- 머티리얼: URP Unlit Transparent 우선, 없으면 Sprites/Default 폴백

### Remove SandBlast Objects 버튼
`SandBlastEngine` GameObject를 씬에서 제거한다.

### Quick Spawn (플레이 모드 전용)
| 버튼 | 동작 |
|---|---|
| Water / Lava / Sand / Fire / Smoke / Debris | 그리드 중앙에 반지름 2 의 원형으로 셀 생성 |
| Clear Grid | `PixelGrid.Type[]` 전체 초기화 |

## 인스펙터 필드

| 필드 | 기본값 | 설명 |
|---|---|---|
| Width (px) | 320 | 그리드 가로 픽셀 수 |
| Height (px) | 180 | 그리드 세로 픽셀 수 |
| Pixels Per Unit | 16 | PPU |
| Origin (world) | (0,0) | 그리드 좌하단 월드 좌표 |
| Ticks / Second | 30 | CA 시뮬 틱 레이트 |

## 관련 파일

- [SandBlastEngine.md](SandBlastEngine.md) — 생성 대상
- [PixelLayerRenderer.md](PixelLayerRenderer.md) — 생성 대상
- [SandBlastDebugPainter.md](SandBlastDebugPainter.md) — 자동 부착
