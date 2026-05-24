# ItemGhostData.cs

## 역할
맵 위에 배치된 아이템(무기, 회복템)의 상태를 동기화하는 Ghost 컴포넌트.

## Ghost 설정

| 항목 | 값 |
|------|----|
| Ghost 모드 | `Interpolated` |
| Importance | `30` (우선순위 낮음 — 아이템은 자주 움직이지 않음) |

## 필드 목록

| 필드 | 타입 | 동기화 | 설명 |
|------|------|--------|------|
| `Position` | `float2` | ✅ | 아이템 위치 |
| `ItemType` | `int` | ✅ | 0=무기, 1=회복 |
| `ItemId` | `int` | ✅ | 세부 아이템 ID (무기 종류, 회복템 종류) |
| `IsPickedUp` | `bool` | ✅ | 수거 여부 — 클라이언트에서 GameObject 비활성화 트리거로 사용 |

## IsPickedUp 처리 흐름
```
서버: ItemPickupSystem → IsPickedUp = true
  ↓ Ghost 스냅샷 전송
클라이언트: IsPickedUp 수신 → Presentation 레이어에서 아이템 GameObject 숨김
```
아이템 엔티티는 삭제하지 않고 `IsPickedUp` 플래그만 변경한다. 이렇게 하면 Ghost가 계속 존재하면서 모든 클라이언트가 동기화 상태를 유지한다.

## 관련 시스템
- [`ItemSpawnSystem`](../../Systems/Items/ItemSpawnSystem.md) — 게임 시작 시 생성
- [`ItemPickupSystem`](../../Systems/Items/ItemPickupSystem.md) — IsPickedUp 갱신
