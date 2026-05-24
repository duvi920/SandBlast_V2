# WeaponState.cs

## 역할
무기의 발사 쿨다운과 재장전 상태를 추적하는 로컬 컴포넌트. 서버 전용으로 사용되며 Ghost 동기화 대상이 아니다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `Cooldown` | `float` | 다음 발사까지 남은 시간 (초). 0 이하이면 발사 가능 |
| `ReloadTimer` | `float` | 재장전 완료까지 남은 시간 (초) |
| `IsReloading` | `bool` | 재장전 중 여부. true이면 발사 불가 |

## 발사 가능 조건
```
ShootSystem 내부:
Cooldown <= 0
AND !IsReloading
AND PlayerGhostData.AmmoCount > 0
AND PlayerInput.Shoot.IsSet
```

## 쿨다운 리셋
`ShootSystem`에서 발사 성공 시 `Cooldown = 0.2f` (기본값)로 초기화된다. 무기별 연사 속도는 `WeaponId`에 따라 다른 값으로 설정할 수 있도록 확장 가능하다.

## 관련 시스템
- [`ShootSystem`](../../Systems/Combat/ShootSystem.md) — Cooldown 감소 및 리셋
