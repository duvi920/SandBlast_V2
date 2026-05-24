# ItemPickupSystem.cs

## 역할
F키(`Interact`) 입력이 있는 플레이어 주변의 아이템을 줍는 처리를 담당한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |

## 줍기 조건
1. `PlayerInput.Interact.IsSet` — 이 틱에 F키가 눌림
2. `PlayerGhostData.IsDead == false`
3. `ItemGhostData.IsPickedUp == false`
4. `distance(Player.Position, Item.Position) < 1.5f`

## 줍기 조건
1. `PlayerInput.Interact.IsSet` — 이 틱에 G키가 눌림
2. `PlayerGhostData.IsDead == false`
3. `ItemGhostData.IsPickedUp == false`
4. `distance(Player.Position, Item.Position) < 1.5f`

## 아이템 타입 처리

| ItemType | 처리 내용 |
|----------|-----------|
| `0` (무기) | `WeaponId = ItemId`, `AmmoCount = 30` |
| `1` (회복) | `Health = min(Health + 30, 100)` |
| `2` (삽) | `HasShovel = true`, `ShovelUseCount = 3` |

## 아이템 처리 후 상태
`IsPickedUp = true`로 설정만 하고 엔티티를 삭제하지 않는다. Ghost가 살아있어야 모든 클라이언트에 "이 아이템은 이미 주워졌음"이 동기화된다. 클라이언트 측 Presentation 레이어에서 `IsPickedUp == true`인 아이템을 비활성화한다.

## 한 번에 하나만 줍기
내부 루프에서 첫 번째로 조건을 만족한 아이템을 줍고 `break`한다. 같은 틱에 두 아이템이 범위 안에 있어도 하나만 처리된다.

## 관련 파일
- [`PlayerInput`](../../Components/Local/PlayerInput.md) — Interact 입력
- [`ItemGhostData`](../../Components/Ghost/ItemGhostData.md) — IsPickedUp 갱신
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — WeaponId, AmmoCount, Health 갱신
