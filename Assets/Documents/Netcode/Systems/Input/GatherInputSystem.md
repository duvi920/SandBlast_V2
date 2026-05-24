# GatherInputSystem.cs

## 역할
키보드/마우스 입력을 읽어 `PlayerInput` 컴포넌트에 기록하는 시스템. 로컬 플레이어의 Ghost에만 적용된다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ClientSimulation` |
| UpdateGroup | `GhostInputSystemGroup` |
| 쿼리 필터 | `.WithAll<GhostOwnerIsLocal>()` |

## GhostInputSystemGroup에서 실행하는 이유
NfE는 이 그룹에서 수집된 `IInputComponentData` 값을 자동으로 서버로 전송하고 버퍼에 기록한다. 다른 그룹에서 입력을 수집하면 예측 롤백 시 입력 히스토리가 깨질 수 있다.

## 입력 매핑

| 입력 소스 | 대상 필드 |
|-----------|-----------|
| `A/D` 또는 방향키 | `MoveDirection.x` (-1 ~ 1) |
| `Space` KeyDown | `Jump.Set()` |
| `F` isPressed | `Shoot.Set()` |
| `R` KeyDown | `Reload.Set()` |
| `G` KeyDown | `Interact.Set()` |
| `Shift` / `C` KeyDown | `Dash.Set()` |
| `Q` KeyDown | `MeleeAttack.Set()` |
| `E` KeyDown | `UseShovel.Set()` |
| 마우스 월드 좌표 → 플레이어 방향 | `AimAngle` (라디안) |

## AimAngle 계산
```
마우스 스크린 좌표
    → Camera.ScreenToWorldPoint()
    → 플레이어 Position과의 차 벡터
    → math.atan2(diff.y, diff.x)
    → AimAngle (라디안)
```

## GhostOwnerIsLocal 필터
여러 플레이어 Ghost가 존재하는 경우, 이 태그가 붙은 Ghost만 입력을 수집한다. 다른 플레이어의 Ghost에는 입력이 덮어씌워지지 않는다.

## 관련 파일
- [`PlayerInput`](../../Components/Local/PlayerInput.md) — 기록 대상 컴포넌트
- [`PlayerMoveSystem`](../Movement/PlayerMoveSystem.md) — 입력 소비
- [`ShootSystem`](../Combat/ShootSystem.md) — 발사 입력 소비
