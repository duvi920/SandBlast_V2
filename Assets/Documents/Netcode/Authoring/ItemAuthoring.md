# ItemAuthoring.cs

## 역할
WeaponItem.prefab 및 HealItem.prefab을 ECS 엔티티로 변환하는 Baker.

## Inspector 설정값

| 필드 | 값 | 설명 |
|------|----|------|
| `ItemType` | `0` (무기) / `1` (회복) | 아이템 분류 |
| `ItemId` | `0` ~ N | 세부 아이템 ID (무기 종류, 회복템 종류 구분) |

## Baker가 추가하는 컴포넌트

| 컴포넌트 | 초기값 | 설명 |
|----------|--------|------|
| `ItemGhostData` | ItemType, ItemId, IsPickedUp=false | 네트워크 동기화 |

## Prefab 설정 체크리스트

### WeaponItem.prefab
- [ ] `ItemAuthoring` 추가, `ItemType = 0`
- [ ] `GhostAuthoringComponent` → Ghost Mode: `Interpolated`, Importance: `30`
- [ ] `GamePrefabsAuthoring.WeaponItemPrefab`에 연결

### HealItem.prefab
- [ ] `ItemAuthoring` 추가, `ItemType = 1`
- [ ] `GhostAuthoringComponent` → Ghost Mode: `Interpolated`, Importance: `30`
- [ ] `GamePrefabsAuthoring.HealItemPrefab`에 연결

## 관련 파일
- [`ItemGhostData`](../Components/Ghost/ItemGhostData.md)
- [`GamePrefabsAuthoring`](GamePrefabsAuthoring.md)
- [`ItemSpawnSystem`](../Systems/Items/ItemSpawnSystem.md)
- [`ItemPickupSystem`](../Systems/Items/ItemPickupSystem.md)
