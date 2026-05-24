# StunSystem.cs

## 역할
`HitEvent` 버퍼의 `StunPower`를 소비해 스턴 게이지를 누적하고, 게이지가 임계값(100)에 도달하면 기절 상태를 발동한다. 기절 타이머 감소와 자동 회복도 처리한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 순서 | `HitDetectionSystem` 이후, `DamageSystem` 이전 |

## 처리 로직

```
foreach player:
    StunTimer -= dt
    if StunTimer <= 0: IsStunned = false

    if !IsStunned:
        StunAccumulator -= StunDecayRate × dt  // 초당 10 자동 감소

    foreach HitEvent:
        StunAccumulator += HitEvent.StunPower
        if StunAccumulator >= 100 && !IsStunned:
            IsStunned = true
            StunTimer = 3.0s
            StunAccumulator = 0
```

## 기절 중 제한
`PlayerMoveSystem`에서 `IsStunned == true`이면 입력 처리를 건너뛴다. `MeleeAttackSystem`과 `ShovelUseSystem`도 기절 중인 플레이어의 행동을 차단한다.

## 상수 (SimulationConstants)

| 상수 | 값 |
|------|----|
| `StunThreshold` | 100f |
| `StunDecayRate` | 10f/s |
| `StunDuration` | 3.0s |
| `BulletStunPower` | 15f |
| `MeleeStunPower` | 40f |
| `ExplosionStunPower` | 20f |

## 관련 파일
- [`StunStateComponent`](../../Components/Local/StunStateComponent.md) — 상태 저장
- [`HitEvent`](../../Components/Local/HitEvent.md) — StunPower 소비
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — IsStunned 갱신
- [`PlayerMoveSystem`](../Movement/PlayerMoveSystem.md) — IsStunned 체크
