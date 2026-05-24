# HitEvent.cs

## 역할
투사체가 플레이어에게 맞았을 때 발생하는 이벤트를 담는 버퍼 컴포넌트. `IBufferElementData`를 구현해 하나의 엔티티에 여러 HitEvent를 동시에 쌓을 수 있다.

## 버퍼 방식을 사용하는 이유
같은 틱에 두 발의 총알이 동시에 맞는 경우를 처리하기 위해 단일 컴포넌트 대신 동적 버퍼를 사용한다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `TargetEntity` | `Entity` | 피격된 플레이어 엔티티 |
| `Damage` | `float` | 이 이벤트에서 적용할 피해량 |
| `HitPosition` | `float2` | 충돌 위치 (히트 이펙트 스폰에 활용 가능) |
| `StunPower` | `float` | 스턴 게이지 증가량. 0이면 스턴 없음 |

## 이벤트 생명주기
```
HitDetectionSystem / MeleeAttackSystem: 충돌·근접 공격 감지 → 버퍼에 HitEvent 추가
    ↓
StunSystem: StunPower 소비 → 스턴 게이지 누적 (DamageSystem 이전에 실행)
    ↓
DamageSystem: 버퍼를 순회하며 HP 감소
    ↓
DamageSystem: 처리 완료 후 버퍼 클리어 (ECB.SetBuffer<HitEvent>)
```

## StunPower 기준값

| 발생 시스템 | StunPower |
|------------|-----------|
| `HitDetectionSystem` (투사체) | `15f` (`BulletStunPower`) |
| `MeleeAttackSystem` (근접) | `40f` (`MeleeStunPower`) |
| 폭발 (`SpellProjectileSystem`) | `20f` (`ExplosionStunPower`) |

## 관련 시스템
- [`HitDetectionSystem`](../../Systems/Combat/HitDetectionSystem.md) — 투사체 이벤트 생성
- [`MeleeAttackSystem`](../../Systems/Combat/MeleeAttackSystem.md) — 근접 이벤트 생성
- [`StunSystem`](../../Systems/Combat/StunSystem.md) — StunPower 소비
- [`DamageSystem`](../../Systems/Combat/DamageSystem.md) — Damage 소비 및 클리어
