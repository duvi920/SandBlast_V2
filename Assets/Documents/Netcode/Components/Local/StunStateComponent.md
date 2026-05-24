# StunStateComponent.cs

## 역할
플레이어의 스턴 게이지와 기절 타이머를 보관하는 서버 전용 로컬 컴포넌트. Ghost 동기화 없이 서버 내부에서만 사용된다. 클라이언트에는 `PlayerGhostData.IsStunned` (Ghost 필드)로만 결과가 전달된다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `StunAccumulator` | `float` | 현재 스턴 게이지 (0 ~ 100). 100 도달 시 기절 발동 |
| `StunTimer` | `float` | 남은 기절 시간(초). 0이 되면 기절 해제 |
| `MeleeAttackCooldown` | `float` | 근접 공격 쿨다운 잔여 시간(초) |

## 스턴 게이지 동작

```
피격 시: StunAccumulator += StunPower
    → StunAccumulator >= 100
        → IsStunned = true, StunTimer = 3.0s, StunAccumulator = 0

기절 중이 아닐 때:
    → StunAccumulator -= 10 × dt (자동 회복)

StunTimer > 0:
    → StunTimer -= dt
    → StunTimer <= 0 → IsStunned = false
```

## 관련 파일
- [`StunSystem`](../../Systems/Combat/StunSystem.md) — 이 컴포넌트를 읽고 씀
- [`MeleeAttackSystem`](../../Systems/Combat/MeleeAttackSystem.md) — MeleeAttackCooldown 차감
- [`PlayerGhostData`](../Ghost/PlayerGhostData.md) — IsStunned Ghost 필드 (결과 동기화)
- [`PlayerAuthoring`](../../Authoring/PlayerAuthoring.md) — Baker에서 기본값으로 추가
