# PlayerGhostData.cs

## 역할
플레이어 상태를 네트워크로 동기화하는 Ghost 컴포넌트. `[GhostField]`가 붙은 필드만 서버→클라이언트로 전송된다.

## Ghost 설정

| 항목 | 값 |
|------|----|
| Ghost 모드 | `OwnerPredicted` (GhostAuthoringComponent에서 설정) |
| Importance | `100` (스냅샷 전송 우선순위 최상위) |
| OwnerSendType | `All` (오너 포함 모든 클라이언트에 전송) |

## 필드 목록

| 필드 | 타입 | 동기화 | 설명 |
|------|------|--------|------|
| `Position` | `float2` | ✅ | 월드 좌표 (XY) |
| `Velocity` | `float2` | ✅ | 현재 속도 벡터 |
| `Health` | `float` | ✅ Quantization=10 | 체력 (소수점 1자리 정밀도) |
| `Armor` | `float` | ✅ Quantization=10 | 방어력 |
| `IsDead` | `bool` | ✅ | 사망 여부 |
| `IsFacingRight` | `bool` | ✅ | 스프라이트 방향 |
| `WeaponId` | `int` | ✅ | 현재 장착 무기 ID |
| `AmmoCount` | `int` | ✅ | 현재 탄약 수 |
| `IsGrounded` | `bool` | ✅ | 지면 접촉 여부 |
| `KillCount` | `int` | ✅ | 킬 수 |
| `TeamId` | `int` | ✅ | 팀 ID (0=A, 1=B) |
| `RespawnCount` | `int` | ✅ | 남은 부활 횟수 |
| `IsStunned` | `bool` | ✅ | 기절 중 여부 — 클라이언트 애니메이션·이펙트용 |
| `HasShovel` | `bool` | ✅ | 삽 소지 여부 |
| `ShovelUseCount` | `int` | ✅ | 남은 삽 사용 횟수 |

## Quantization
`Quantization = 10`은 float 값을 `int`로 인코딩할 때 10배 스케일을 사용한다는 의미. `Health = 73.5` → 전송값 `735`. 대역폭 절약과 정밀도 사이의 트레이드오프.

## OwnerPredicted 동작 흐름
```
[Client - 오너]        [Server]           [Client - 타인]
입력 수집               입력 수신           스냅샷 수신
로컬 예측 실행   ←→   권위 시뮬레이션     보간 적용
롤백/보정
```

## 관련 시스템
- [`PlayerMoveSystem`](../../Systems/Movement/PlayerMoveSystem.md) — Position, Velocity 갱신; IsStunned이면 입력 무시
- [`DamageSystem`](../../Systems/Combat/DamageSystem.md) — Health, IsDead 갱신
- [`StunSystem`](../../Systems/Combat/StunSystem.md) — IsStunned 갱신
- [`ShovelUseSystem`](../../Systems/Combat/ShovelUseSystem.md) — HasShovel, ShovelUseCount 갱신
- [`ItemPickupSystem`](../../Systems/Items/ItemPickupSystem.md) — HasShovel, ShovelUseCount 획득 처리
- [`SpriteFlipSystem`](../../Systems/Presentation/SpriteFlipSystem.md) — IsFacingRight 읽기
