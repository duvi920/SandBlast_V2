# BurialCheckSystem.cs

## 역할
살아있는 모든 플레이어의 몸체 주변 픽셀을 샘플링해 80% 이상이 Solid이면 매몰로 즉사 처리하는 시스템. 기절 여부와 무관하게 동작한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 순서 | `ShovelUseSystem` 이후, `DamageSystem` 이전 |
| 타입 | `SystemBase` (PixelGridSingleton 접근 필요) |
| 전제 조건 | `RoomPhase == Battle` |

## 픽셀 샘플링

플레이어 캡슐 내부 3×5 그리드(15개 지점)를 월드 좌표로 샘플링:

```
x 오프셋: {-0.20, 0, +0.20}
y 오프셋: {-0.60, -0.30, 0, +0.30, +0.60}
```

- 15개 중 **12개 이상(80%)** 이 `IsSolid()` → 즉사

## 즉사 처리

```
ghostData.IsDead = true
ECB.AddComponent<DeathEvent>(KillerNetworkId = -1, Cause = DeathCause.Buried)
```

`SurvivalCheckSystem`이 다음 틱에 `IsDead == true`를 감지해 승패를 판정한다.

## 상수

| 상수 | 값 |
|------|----|
| 샘플 수 | 15 (3×5) |
| 즉사 임계 | 12개 (80%) |

## 관련 파일
- [`ShovelUseSystem`](../Combat/ShovelUseSystem.md) — POWDER_SAND 생성
- [`DeathEvent`](../../Components/Local/DeathEvent.md) — Buried 원인으로 추가
- [`SurvivalCheckSystem`](SurvivalCheckSystem.md) — 승패 판정
- [`PixelGridSingleton`](../../ECS/Singletons/PixelGridSingleton.md) — 픽셀 샘플링
