# PlayerInput.cs

## 역할
플레이어의 키보드/마우스 입력을 담는 컴포넌트. `IInputComponentData`를 구현하면 NfE가 매 틱마다 자동으로 버퍼링하여 서버로 전송한다.

## IInputComponentData 특성
- 일반 `IComponentData`와 달리 NfE 내부에서 **입력 히스토리 버퍼**를 관리한다.
- 클라이언트 예측 롤백 시 과거 틱의 입력을 재사용해 시뮬레이션을 재실행한다.
- `[GhostComponent]`가 붙어 있어 서버에도 전달된다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `MoveDirection` | `float2` | 좌우 이동 입력 (-1 ~ 1). Y는 사용하지 않음 |
| `Jump` | `InputEvent` | 점프 버튼. `IsSet`이 true인 틱에만 처리 |
| `Shoot` | `InputEvent` | 발사 버튼. `IsSet`이 true인 틱에만 처리 |
| `AimAngle` | `float` | 마우스 방향각 (라디안). 발사 방향 계산에 사용 |
| `Reload` | `InputEvent` | 재장전 버튼 (R키) |
| `Interact` | `InputEvent` | 아이템 줍기 버튼 (G키) |
| `Dash` | `InputEvent` | 대시 버튼 (Shift / C키) |
| `MeleeAttack` | `InputEvent` | 근접 공격 (Q키) |
| `UseShovel` | `InputEvent` | 삽 사용 — 자기 주변에 모래 뿌리기 (E키) |

## InputEvent란?
NfE가 제공하는 단발성 입력 구조체. `Set()`을 호출한 틱에만 `IsSet == true`가 되고 다음 틱에는 자동으로 초기화된다. 키 누름 이벤트를 예측 시스템에서 안전하게 사용할 수 있게 한다.

```csharp
// 잘못된 방법 — bool은 여러 틱에 걸쳐 true로 남을 수 있음
public bool JumpPressed;

// 올바른 방법 — InputEvent는 한 틱만 유효
public InputEvent Jump;
// ...
if (Input.GetKeyDown(KeyCode.Space)) input.Jump.Set();
```

## 관련 시스템
- [`GatherInputSystem`](../../Systems/Input/GatherInputSystem.md) — 이 컴포넌트에 입력 기록
- [`PlayerMoveSystem`](../../Systems/Movement/PlayerMoveSystem.md) — MoveDirection, Jump, Dash 읽기
- [`ShootSystem`](../../Systems/Combat/ShootSystem.md) — Shoot, AimAngle 읽기
- [`ItemPickupSystem`](../../Systems/Items/ItemPickupSystem.md) — Interact 읽기
- [`MeleeAttackSystem`](../../Systems/Combat/MeleeAttackSystem.md) — MeleeAttack 읽기
- [`ShovelUseSystem`](../../Systems/Combat/ShovelUseSystem.md) — UseShovel 읽기
