# ShootSystem.cs

## 역할
발사 입력이 있을 때 투사체 엔티티를 생성하고 발사 쿨다운을 관리하는 시스템. 투사체 생성은 서버만이 수행하는 권위 연산이다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 쿼리 필터 | `.WithAll<Simulate>()` |

## 발사 조건 (AND)
1. `PlayerInput.Shoot.IsSet` — 이 틱에 발사 버튼이 눌림
2. `WeaponState.Cooldown <= 0` — 쿨다운 해소됨
3. `PlayerGhostData.AmmoCount > 0` — 탄약 있음
4. `PlayerGhostData.IsDead == false` — 살아있음

## 투사체 생성 로직
```
AimAngle → 방향 벡터 dir = (cos, sin)
spawnPos = PlayerPosition + dir * 0.6f  (총구 오프셋)

ECB.Instantiate(GamePrefabs.BulletPrefab)
    SetComponent BulletGhostData { Position, Velocity = dir * 25f, OwnerNetworkId, Damage = 25f }
    SetComponent LocalTransform
    SetComponent BulletLifetime { Value = 3f }

WeaponState.Cooldown = 0.2f  (연사 간격)
```

## 서버 전용인 이유
클라이언트가 임의로 투사체를 생성하면 치팅이 가능하다. 서버가 단독으로 엔티티를 생성하고 Ghost로 클라이언트에 전파하는 방식을 사용한다.

## EntityCommandBuffer 사용 이유
ForEach 쿼리 내부에서는 구조적 변경(엔티티 생성/삭제)이 불가능하다. ECB에 명령을 기록한 뒤 `Playback()`으로 일괄 처리한다.

## 관련 파일
- [`GamePrefabs`](../../Components/Local/GamePrefabs.md) — BulletPrefab 참조
- [`BulletGhostData`](../../Components/Ghost/BulletGhostData.md) — 초기값 설정
- [`WeaponState`](../../Components/Local/WeaponState.md) — 쿨다운 관리
- [`BulletMoveSystem`](BulletMoveSystem.md) — 이후 이동 처리
