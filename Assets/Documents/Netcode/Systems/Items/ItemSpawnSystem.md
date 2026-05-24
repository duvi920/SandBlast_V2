# ItemSpawnSystem.cs

## 역할
게임 시작 시 맵의 모든 `SpawnPointData` 위치에 아이템을 무작위로 배치하는 시스템. 한 번 실행 후 비활성화된다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 횟수 | 1회 (`state.Enabled = false`) |
| 전제 조건 | `GamePrefabs`, `SpawnPointData` 존재 시 실행 |

## 생성 로직
```
Random seed = 현재 시간 기반
hasShovel = prefabs.ShovelItemPrefab != Entity.Null

SpawnPointData 순회:
    typeRange = hasShovel ? 3 : 2
    itemType  = random.NextInt(0, typeRange)  // 0=무기, 1=회복, 2=삽
    prefab    = typeRange별 선택

    ECB.Instantiate(prefab)
    SetComponent ItemGhostData { Position, ItemType, ItemId, IsPickedUp = false }
    SetComponent LocalTransform
```

## 삽 스폰 조건
`GamePrefabs.ShovelItemPrefab`이 `Entity.Null`이 아닐 때만 삽이 스폰 풀에 포함된다. Inspector에서 `GamePrefabsAuthoring.ShovelItemPrefab`에 프리팹을 연결해야 활성화된다.

## 런타임 보충 확장
현재는 게임 시작 시 1회만 배치한다. 일정 시간마다 빈 스폰 포인트에 아이템을 보충하는 기능을 추가하려면 `state.Enabled = false`를 제거하고 타이머 기반 조건을 추가한다.

## 관련 파일
- [`GamePrefabs`](../../Components/Local/GamePrefabs.md) — Item Prefab 참조
- [`SpawnPointData`](../../Components/Local/SpawnPointData.md) — 배치 위치
- [`ItemGhostData`](../../Components/Ghost/ItemGhostData.md) — 초기값 설정
- [`SpawnPointAuthoring`](../../Authoring/SpawnPointAuthoring.md) — 스폰 포인트 씬 설정
