# DeathEvent.cs

## 역할
플레이어가 사망했을 때 추가되는 이벤트 컴포넌트. 사망 원인(HP 소진·매몰·용암)과 킬러 정보를 담아 다른 시스템이 감지할 수 있게 한다.

## DeathCause enum

| 값 | 의미 |
|----|------|
| `Damage` | HP가 0 이하로 감소 (`DamageSystem`) |
| `Buried` | 픽셀 지형에 80% 이상 매몰 (`BurialCheckSystem`) |
| `Lava` | 용암 접촉 (예정) |

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `KillerNetworkId` | `int` | 사망을 유발한 플레이어 NetworkId. 미특정이면 `-1` |
| `Cause` | `DeathCause` | 사망 원인 enum |

## 사용 흐름
```
DamageSystem:      Health <= 0  → AddComponent<DeathEvent>(Cause=Damage)
BurialCheckSystem: 80% 매몰     → AddComponent<DeathEvent>(Cause=Buried)
    ↓
SurvivalCheckSystem: IsDead 기반 생존자 수 집계 → 승패 판정
```

## 관련 시스템
- [`DamageSystem`](../../Systems/Combat/DamageSystem.md) — Damage 원인으로 추가
- [`BurialCheckSystem`](../../Systems/BattleRoyale/BurialCheckSystem.md) — Buried 원인으로 추가
- [`SurvivalCheckSystem`](../../Systems/BattleRoyale/SurvivalCheckSystem.md) — 생존자 집계
