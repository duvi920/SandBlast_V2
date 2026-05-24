# DamageSystem.cs

## 역할
`HitEvent` 버퍼를 소비해 플레이어 체력을 감소시키고 사망 여부를 판정하는 시스템.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 순서 | `HitDetectionSystem` 이후 |

## 처리 로직
```
HitEvent 버퍼 순회:
    Health -= HitEvent.Damage
    if Health <= 0:
        Health = 0
        IsDead = true
        ECB.AddComponent<DeathEvent>(Cause=DeathCause.Damage)
        break (이미 사망 → 나머지 이벤트 무시)

버퍼 클리어: ECB.SetBuffer<HitEvent>(entity)
```

## ECB.SetBuffer vs ClearBuffer
`DynamicBuffer.Clear()`는 `Entities.ForEach` 내 RefRW에서 직접 호출 가능하지만, ECB를 통한 `SetBuffer`는 Playback 시점에 새 빈 버퍼로 교체한다. 구조적 변경(DeathEvent 추가)이 필요하므로 ECB를 사용한다.

## 체력/사망 값이 Ghost에 반영되는 방식
`PlayerGhostData.Health`와 `IsDead`는 `[GhostField]`이므로 서버가 값을 변경하면 다음 스냅샷에 포함되어 모든 클라이언트에 자동 전파된다.

## 관련 파일
- [`HitEvent`](../../Components/Local/HitEvent.md) — 소비 대상 버퍼
- [`DeathEvent`](../../Components/Local/DeathEvent.md) — 사망 이벤트 추가
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — Health, IsDead 갱신
- [`SurvivalCheckSystem`](../BattleRoyale/SurvivalCheckSystem.md) — IsDead 기반 생존자 집계
