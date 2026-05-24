# AnimationSystem.cs

## 역할
플레이어의 이동 속도, 점프/낙하 상태를 Animator에 전달해 애니메이션 전환을 구동한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ClientSimulation` 전용 |
| UpdateGroup | `PresentationSystemGroup` |
| 기반 클래스 | `SystemBase` (Hybrid Animator 접근 필요) |

## Animator 파라미터 매핑

| 파라미터 | 조건 | 설명 |
|----------|------|------|
| `IsWalking` | `abs(Velocity.x) > 0.1f` | 걷기 애니메이션 |
| `IsJumping` | `!IsGrounded && Velocity.y > 0` | 상승 중 |
| `IsFalling` | `!IsGrounded && Velocity.y < 0` | 하강 중 |
| `IsDead` | `ghostData.IsDead` | 사망 애니메이션 |

## 발사 애니메이션
설계 문서에서는 `IsShooting` 파라미터가 언급되지만 현재 `PlayerInput`의 `Shoot.IsSet`은 서버에서만 처리된다. 클라이언트에서 `PlayerInput` 값을 직접 읽어 `IsShooting`을 구현할 수 있다.

## 관련 파일
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — Velocity, IsDead 읽기
- [`GroundedState`](../../Components/Local/GroundedState.md) — IsGrounded 읽기
