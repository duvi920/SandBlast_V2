# GroundCheckSystem.md

## 역할
플레이어 발 아래로 레이캐스트를 발사해 지면 접촉 여부를 판정하고 `GroundedState`와 `PlayerGhostData.IsGrounded`를 갱신한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ClientSimulation + ServerSimulation` |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 쿼리 필터 | `.WithAll<Simulate>()` |

## 레이캐스트 설정

| 설정 | 값 | 설명 |
|------|----|------|
| 시작점 | `LocalTransform.Position + (0, -0.5, 0)` | 플레이어 바닥 중심 |
| 방향/길이 | 아래로 `0.55f` | 발 아래 0.05 유닛 여유 |
| Filter | `CollidesWith = ~0u` | 모든 레이어와 충돌 검사 |

## 레이캐스트 흐름
```
PhysicsWorldSingleton 싱글턴 획득
    ↓
RaycastInput { Start, End, Filter } 설정
    ↓
physicsWorld.CastRay(input, out hit)
    ↓
hit 여부 → GroundedState.IsGrounded
           PlayerGhostData.IsGrounded
```

## 주의사항
레이캐스트 Filter의 `GroupIndex = 0`은 플레이어 자신의 콜라이더와도 충돌할 수 있다. 실제 구현 시 플레이어 레이어를 제외하도록 Filter를 조정해야 한다.

## 관련 파일
- [`GroundedState`](../../Components/Local/GroundedState.md) — 결과 저장
- [`GravitySystem`](GravitySystem.md) — IsGrounded 읽기
- [`PlayerMoveSystem`](PlayerMoveSystem.md) — 점프 조건 판단
