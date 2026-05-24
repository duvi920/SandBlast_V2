# ParallaxBackground

> `Assets/Scripts/Rendering/ParallaxBackground.cs`

## 역할

카메라 이동 델타에 `parallaxFactor`를 곱해 배경 레이어를 지형보다 느리게 스크롤시킨다.  
`SandBlastAutoSetup`이 Play Mode 진입 시 자동 생성하며, 내부에서 두 배경 GameObject를 직접 만든다.

## 레이어 구성

| 레이어 | 속도 비율 | sortingOrder | Z 위치 | 내용 |
|---|---|---|---|---|
| ParallaxSky | × 0.15 | -10 | 5f | 하늘 그라디언트 (하단 지평선 파랑 → 상단 짙은 파랑) |
| ParallaxHills | × 0.40 | -5 | 3f | 원경 언덕 실루엣 (하단 33% 불투명, 상단 투명) |
| PixelLayer (지형) | × 1.00 | -1 | 0.1f | 기존 CA 지형 렌더러 (변경 없음) |

## 스크롤 공식

```
LateUpdate():
    첫 프레임: 카메라 기준 X 캡처 (CameraFollowSystem frame-1 snap 이후)
    이후: layer.position.x = originX + (camera.x - cameraOriginX) * parallaxFactor
```

Y축 이동은 적용하지 않는다 (현재 맵 기준 수직 카메라 이동량 ≈ 0).

## 스프라이트 크기

카메라 이동 범위 전체를 덮도록 충분히 크게 생성한다.

```
texW = worldW × 2.5 × pixelsPerUnit  (최대 16384 clamped)
texH = worldH × 2.0 × pixelsPerUnit  (최대 16384 clamped)
```

1v1 맵(512×256): 1280×512 px  
2v2 맵(1024×256): 2560×512 px

## 텍스처 생성

- **하늘(Sky):** `FilterMode.Bilinear`, 수직 그라디언트 — 하단 `(135,180,220)` → 상단 `(30,50,120)`
- **언덕(Hills):** `FilterMode.Point`, 하단 33% `(60,90,60)` 불투명, 상단 투명

## 주요 메서드

```csharp
void Initialize(Camera cam, float worldW, float worldH, Vector2 gridOrigin, float pixelsPerUnit)
    // 두 레이어 생성 + 카메라 기준 초기화
```

## 관련 파일

- [PixelLayerRenderer.md](PixelLayerRenderer.md) — CA 지형 렌더러 (sortingOrder=-1)
- [SandBlastAutoSetup.md](../SandBlastAutoSetup.md) — 자동 생성 진입점
- [CameraFollowSystem.md](../Netcode/Systems/Presentation/CameraFollowSystem.md) — 카메라 추적 시스템
