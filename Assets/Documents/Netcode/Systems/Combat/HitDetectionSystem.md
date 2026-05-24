# HitDetectionSystem.cs

## 역할
투사체와 플레이어 사이의 충돌을 AABB(원형 근사) 방식으로 감지하고 `HitEvent`를 발행한다. 충돌한 투사체는 즉시 삭제된다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 순서 | `BulletMoveSystem` 이후 |

## 충돌 감지 방식

| 항목 | 값 |
|------|-----|
| 알고리즘 | 원형 오버랩 (거리 비교) |
| `BulletRadius` | `0.15f` |
| `PlayerRadius` | `0.45f` |
| 충돌 임계값 | `BulletRadius + PlayerRadius = 0.6f` |

```
distance(bullet.Position, player.Position) < 0.6f → 충돌
```

## 충돌 처리 흐름
```
1. 모든 플레이어 엔티티/데이터를 NativeArray로 수집
2. 모든 투사체에 대해 플레이어 목록과 거리 비교
3. 충돌 시:
   - HitEvent { Damage, HitPosition, StunPower=BulletStunPower(15f) } 버퍼에 추가
   - ECB.DestroyEntity(bulletEntity)
   - break (한 투사체는 한 플레이어에만 적중)
```

## 현재 구현의 한계
- 자기 자신 충돌 방지가 완전하지 않다. `OwnerNetworkId`와 플레이어의 `GhostOwner.NetworkId`를 비교하는 로직 추가가 필요하다.
- Unity Physics의 `PhysicsCollider`를 사용하지 않아 실제 콜라이더 형태와 오차가 있다.

## 관련 파일
- [`HitEvent`](../../Components/Local/HitEvent.md) — 발행 대상 버퍼
- [`BulletGhostData`](../../Components/Ghost/BulletGhostData.md) — 위치 및 소유자 정보
- [`DamageSystem`](DamageSystem.md) — HitEvent 소비
