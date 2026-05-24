# PlayerMoveSystem.cs

## 역할
`PlayerInput`을 읽어 좌우 이동, 점프를 처리하고 위치를 업데이트하는 핵심 이동 시스템.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ClientSimulation + ServerSimulation` |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 실행 순서 | `GravitySystem`, `GroundCheckSystem` 이후 |
| 쿼리 필터 | `.WithAll<Simulate>()` |

## 상수

| 상수 | 값 |
|------|----|
| `MoveSpeed` | `8f` (units/s) |
| `JumpForce` | `15f` (units/s, Y 초기 속도) |

## 처리 순서
```
1. IsDead → 스킵
2. Velocity.x = MoveDirection.x * MoveSpeed
3. Jump.IsSet && IsGrounded → Velocity.y = 15f
4. MoveDirection.x != 0 → IsFacingRight 갱신
5. Position += Velocity * deltaTime
6. LocalTransform.Position 동기화
```

## LocalTransform과 PlayerGhostData.Position의 이중 갱신
DOTS의 물리/렌더링은 `LocalTransform`을 기준으로 동작하고, Ghost 동기화는 `PlayerGhostData.Position`을 기준으로 동작한다. 두 값을 항상 동기화해야 시각적 불일치가 발생하지 않는다.

## 클라이언트 예측 동작
```
[틱 N] 클라이언트: 입력 수집 → 로컬 예측 실행 → 화면 즉시 반영
                  서버: 같은 입력 수신 → 권위 시뮬레이션
[틱 N+5] 클라이언트: 서버 스냅샷 수신 → 예측값과 비교
                     차이가 크면 → 롤백 후 틱 N부터 재시뮬레이션
```

## 관련 파일
- [`PlayerInput`](../../Components/Local/PlayerInput.md) — 입력 소스
- [`GravitySystem`](GravitySystem.md) — Velocity.y 사전 처리
- [`GroundCheckSystem`](GroundCheckSystem.md) — IsGrounded 사전 처리
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — 상태 저장
