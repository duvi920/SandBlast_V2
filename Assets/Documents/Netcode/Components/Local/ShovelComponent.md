# ShovelComponent.cs

## 역할
삽 아이템의 남은 사용 횟수를 보관하는 서버 전용 로컬 컴포넌트. 플레이어 엔티티에 붙지 않고 삽 자체를 아이템 엔티티로 관리하지도 않는다 — `PlayerGhostData.HasShovel` / `ShovelUseCount` Ghost 필드가 소지 상태를 클라이언트에 전달하므로 이 컴포넌트는 서버 내부 연산에만 쓰인다.

> **현재 구현**: `ShovelComponent`는 별도 엔티티에 붙지 않고 플레이어 엔티티의 `PlayerGhostData` 필드로 사용 횟수를 관리한다. 이 컴포넌트는 향후 독립 아이템 엔티티 방식으로 전환 시 활용 예정이다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `UseCount` | `int` | 남은 사용 횟수 (기본값: 3) |

## 관련 파일
- [`ShovelUseSystem`](../../Systems/Combat/ShovelUseSystem.md) — UseCount 차감
- [`PlayerGhostData`](../Ghost/PlayerGhostData.md) — HasShovel, ShovelUseCount Ghost 필드
- [`ItemPickupSystem`](../../Systems/Items/ItemPickupSystem.md) — 획득 시 초기화
