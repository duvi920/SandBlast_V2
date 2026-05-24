# BulletLifetime.cs

## 역할
투사체 엔티티의 남은 수명을 초 단위로 추적한다. 서버 전용 컴포넌트로 수명이 0 이하가 되면 `BulletMoveSystem`이 해당 엔티티를 삭제한다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `Value` | `float` | 남은 수명 (초). 기본값 `3f` |

## 수명 처리 흐름
```
ShootSystem: BulletLifetime { Value = 3f } 설정
    ↓ 매 서버 틱
BulletMoveSystem: Value -= deltaTime
    ↓ Value <= 0
ECB.DestroyEntity(bulletEntity)
```

## 왜 필요한가?
투사체가 벽에 맞지 않고 맵 밖으로 나갔을 때 메모리 누수 방지. `HitDetectionSystem`이 충돌 시 삭제하지만, 충돌 없이 사라지는 경우도 처리해야 한다.

## 관련 시스템
- [`ShootSystem`](../../Systems/Combat/ShootSystem.md) — 초기값 설정
- [`BulletMoveSystem`](../../Systems/Combat/BulletMoveSystem.md) — Value 감소 및 삭제
