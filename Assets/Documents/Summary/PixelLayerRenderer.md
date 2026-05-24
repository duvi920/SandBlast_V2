# PixelLayerRenderer

> `Assets/Scripts/Rendering/PixelLayerRenderer.cs`

## 역할

매 틱 CA 그리드를 `Texture2D` 로 베이크해 쿼드(Quad) 메시에 출력한다.  
강체 레이어와 픽셀 레이어를 합성하는 렌더링 파이프라인의 픽셀 레이어 담당 (설계 문서 §10).

## 씬 설정

- 그리드 전체를 덮는 크기 `(GridWidth/PPU × GridHeight/PPU)` 의 Quad GameObject에 부착
- `Renderer.material.mainTexture` 에 자동 연결
- 머티리얼은 투명도가 지원되는 Unlit 셰이더 필요 (URP Unlit Transparent 권장)

## SOLID_RIGID 투명 처리

`SOLID_RIGID` 셀은 알파 0(투명)으로 렌더링해 강체 스프라이트/메시가 아래에서 보이도록 한다.  
불투명으로 두면 강체 이동 시 픽셀 텍스처가 강체 위를 덮어버린다.

## 색상 팔레트

| 셀 타입 | 색상 (RGBA) |
|---|---|
| EMPTY | 투명 (0,0,0,0) |
| SOLID_STATIC | 회갈색 (100,90,80,255) |
| SOLID_RIGID | 투명 (0,0,0,0) |
| SOLID_DEBRIS | 갈색 (130,100,70,255) |
| LIQUID_WATER | 반투명 파랑 (30,100,200,200) |
| LIQUID_LAVA | 주황 (220,80,20,255) |
| POWDER_SAND | 황토색 (200,180,100,255) |
| POWDER_ASH | 반투명 회색 (160,160,155,200) |
| GAS_SMOKE | 반투명 짙은회 (80,80,80,150) |
| FIRE | lifetime 기반 동적 색상 |

## 불 색상 (FireColor)

수명(lifetime) 비율에 따라 세 단계로 전환:

| 비율 | 색상 |
|---|---|
| > 60% | 빨강 (255,50,10) |
| 30~60% | 주황 (255,140,20) |
| < 30% | 노랑 (255,220,50) |

## 주요 메서드

```csharp
void Initialize(PixelGrid g)  // 텍스처·버퍼 초기화, 머티리얼 연결
void UpdateTexture()          // 그리드 전체를 Color32[] 로 변환 후 GPU 업로드
```

## 관련 파일

- [SandBlastEngine.md](SandBlastEngine.md) — UpdateTexture() 호출 시점
- [PixelGrid.md](PixelGrid.md) — 데이터 소스
- 설계 문서 §10
