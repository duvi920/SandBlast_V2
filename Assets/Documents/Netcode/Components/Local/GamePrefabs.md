# GamePrefabs.cs

## 역할
런타임에 동적으로 생성해야 하는 Ghost Prefab의 Entity 참조를 보관하는 싱글턴 컴포넌트.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `PlayerPrefab` | `Entity` | 플레이어 Ghost Prefab 엔티티 |
| `BulletPrefab` | `Entity` | 총알 Ghost Prefab 엔티티 |
| `WeaponItemPrefab` | `Entity` | 무기 아이템 Ghost Prefab 엔티티 (ItemType=0) |
| `HealItemPrefab` | `Entity` | 회복 아이템 Ghost Prefab 엔티티 (ItemType=1) |
| `WandItemPrefab` | `Entity` | 완드 아이템 Ghost Prefab 엔티티 |
| `ShovelItemPrefab` | `Entity` | 삽 아이템 Ghost Prefab 엔티티 (ItemType=2). Inspector 미연결 시 스폰 비활성화 |

## 왜 Entity로 저장하는가?
DOTS에서 런타임 Prefab 인스턴스화는 `EntityManager.Instantiate(prefabEntity)` 또는 `EntityCommandBuffer.Instantiate(prefabEntity)` 방식을 사용한다. `GamePrefabsAuthoring`의 Baker가 `GetEntity(go)`를 호출해 MonoBehaviour Prefab을 Entity로 변환하고 이 컴포넌트에 저장한다.

## 사용 예시
```csharp
var prefabs = SystemAPI.GetSingleton<GamePrefabs>();
var bullet = ecb.Instantiate(prefabs.BulletPrefab);
```

## 씬 설정
SubScene 내 `GamePrefabsAuthoring` GameObject에서 세 Prefab을 Inspector로 연결한다.

## 관련 파일
- [`GamePrefabsAuthoring`](../../Authoring/GamePrefabsAuthoring.md) — Baker
- [`ShootSystem`](../../Systems/Combat/ShootSystem.md) — BulletPrefab 사용
- [`ItemSpawnSystem`](../../Systems/Items/ItemSpawnSystem.md) — Item Prefab 사용
