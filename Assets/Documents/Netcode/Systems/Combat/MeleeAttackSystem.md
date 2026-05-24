# MeleeAttackSystem.cs

## 역할
`MeleeAttack` 입력이 들어오면 공격자 주변 0.9유닛 이내의 살아있는 적 플레이어에게 `HitEvent`를 추가하는 근접 공격 시스템.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 순서 | `HitDetectionSystem` 이전 |

## 공격 조건
1. `PlayerInput.MeleeAttack.IsSet` (Q키)
2. 공격자 `IsDead == false`
3. 공격자 `IsStunned == false` — 기절 중이면 근접 공격 불가
4. `MeleeAttackCooldown <= 0`

## 공격 처리

| 항목 | 값 |
|------|----|
| 탐색 범위 | 0.9유닛 |
| 발행 HitEvent.Damage | `8f` (`MeleeDamage`) |
| 발행 HitEvent.StunPower | `40f` (`MeleeStunPower`) |
| 쿨다운 | `0.6s` (`MeleeAttackCooldown`) |
| 한 틱에 명중 가능 수 | 1명 (첫 번째로 범위 내 적) |

## 관련 파일
- [`PlayerInput`](../../Components/Local/PlayerInput.md) — MeleeAttack 입력
- [`HitEvent`](../../Components/Local/HitEvent.md) — 발행 대상
- [`StunStateComponent`](../../Components/Local/StunStateComponent.md) — 쿨다운 관리
- [`StunSystem`](StunSystem.md) — HitEvent.StunPower 소비
