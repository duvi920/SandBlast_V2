# GamePrefabsAuthoring.cs

## 역할
런타임 동적 생성에 필요한 Ghost Prefab들의 참조를 `GamePrefabs` 싱글턴 컴포넌트로 변환하는 Baker.

## Inspector 설정값

| 필드 | 연결 대상 |
|------|-----------|
| `BulletPrefab` | Bullet.prefab |
| `WeaponItemPrefab` | WeaponItem.prefab |
| `HealItemPrefab` | HealItem.prefab |

## Baker 동작
```csharp
GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic)
```
`GetEntity()`는 Inspector에서 지정한 MonoBehaviour Prefab을 베이킹 과정에서 ECS Entity로 변환한다. 이 Entity 참조를 `GamePrefabs` 컴포넌트에 저장해 런타임 시스템에서 `Instantiate`할 수 있게 한다.

## 씬 설정
SubScene 내 빈 GameObject 하나에 이 컴포넌트를 추가하고 세 Prefab을 연결한다. 게임 당 하나만 존재해야 한다(싱글턴).

## 관련 파일
- [`GamePrefabs`](../Components/Local/GamePrefabs.md) — 변환 대상 컴포넌트
- [`ShootSystem`](../Systems/Combat/ShootSystem.md) — BulletPrefab 사용
- [`ItemSpawnSystem`](../Systems/Items/ItemSpawnSystem.md) — Item Prefab 사용
