# BulletMoveSystem.cs

## 역할
투사체를 매 틱 Velocity 방향으로 이동시키고 수명을 감소시킨다. 수명이 0 이하가 되면 엔티티를 삭제한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |

## 처리 로직
```
BulletLifetime.Value -= deltaTime
if (Value <= 0):
    ECB.DestroyEntity → 종료

Position += Velocity * deltaTime
LocalTransform.Position 동기화
```

## SimulationSystemGroup vs PredictedSimulationSystemGroup
투사체 이동은 서버에서만 실행되므로 `PredictedSimulationSystemGroup`(클라이언트 예측 포함)이 아닌 일반 `SimulationSystemGroup`에 배치했다. 클라이언트는 서버로부터 받은 Ghost 스냅샷을 보간하여 투사체 위치를 표현한다.

## 관련 파일
- [`BulletGhostData`](../../Components/Ghost/BulletGhostData.md) — Position, Velocity
- [`BulletLifetime`](../../Components/Local/BulletLifetime.md) — 수명
- [`HitDetectionSystem`](HitDetectionSystem.md) — 충돌 시 삭제 (수명보다 먼저 처리)
