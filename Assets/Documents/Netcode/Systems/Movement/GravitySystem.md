# GravitySystem.cs

## 역할
플레이어 속도의 Y축에 중력 가속도를 적용하고 최대 낙하 속도를 제한하는 시스템.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ClientSimulation + ServerSimulation` |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 쿼리 필터 | `.WithAll<Simulate>()` |

## 상수

| 상수 | 값 | 설명 |
|------|----|------|
| `Gravity` | `30f` | 중력 가속도 (units/s²) |
| `MaxFallSpeed` | `-20f` | 낙하 속도 상한 |

## 동작 방식
```
Velocity.y -= Gravity * deltaTime
Velocity.y = max(Velocity.y, -20f)
```
단, `IsGrounded == true && Velocity.y < 0` 조건이면 중력을 적용하지 않는다. 플레이어가 경사면에 서 있을 때 계속 가속되는 것을 방지한다.

## PredictedSimulationSystemGroup에서 실행하는 이유
중력은 클라이언트가 로컬 예측으로 미리 계산해야 입력 지연 없이 자연스러운 이동감을 느낄 수 있다. 서버도 동일한 로직으로 실행해 결과를 검증한다.

## .WithAll<Simulate>() 필터의 중요성
`PredictedSimulationSystemGroup` 내부에서 예측 대상 엔티티는 `Simulate` 태그를 갖는다. 이 필터 없이 실행하면 Ghost 보간 데이터까지 수정하게 되어 예측이 깨진다.

## 관련 파일
- [`GroundedState`](../../Components/Local/GroundedState.md) — 지면 상태 읽기
- [`PlayerMoveSystem`](PlayerMoveSystem.md) — Velocity 기반 이동
