# GroundedState.cs

## 역할
플레이어가 현재 지면에 닿아 있는지 여부를 나타내는 로컬 상태 컴포넌트. 네트워크로 동기화되지 않으며 매 틱 `GroundCheckSystem`이 갱신한다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `IsGrounded` | `bool` | 지면 접촉 여부 |

## PlayerGhostData.IsGrounded와의 차이

| | `GroundedState.IsGrounded` | `PlayerGhostData.IsGrounded` |
|-|---------------------------|------------------------------|
| 동기화 | ❌ 로컬 전용 | ✅ Ghost 동기화 |
| 용도 | 시스템 간 로컬 참조 | 클라이언트 UI, 애니메이션 |
| 갱신 주체 | `GroundCheckSystem` | `GroundCheckSystem` |

두 값은 `GroundCheckSystem`에서 동시에 갱신된다. 로컬 시스템들은 `GroundedState`를 참조하고, 다른 클라이언트에 보여줄 상태는 `PlayerGhostData.IsGrounded`를 참조한다.

## 관련 시스템
- [`GroundCheckSystem`](../../Systems/Movement/GroundCheckSystem.md) — 값 설정
- [`GravitySystem`](../../Systems/Movement/GravitySystem.md) — 지면 접촉 시 중력 무시
- [`PlayerMoveSystem`](../../Systems/Movement/PlayerMoveSystem.md) — 점프 가능 여부 판단
