# BulletAuthoring.cs

## 역할
Bullet.prefab을 ECS 엔티티로 변환하는 Baker. `ShootSystem`이 런타임에 이 Prefab을 인스턴스화하여 투사체 엔티티를 생성한다.

## Inspector 설정값

| 필드 | 기본값 | 설명 |
|------|--------|------|
| `DefaultLifetime` | `3f` | 투사체 기본 수명 (초). ShootSystem에서 덮어쓸 수 있음 |

## Baker가 추가하는 컴포넌트

| 컴포넌트 | 초기값 | 설명 |
|----------|--------|------|
| `BulletGhostData` | 기본값(0) | ShootSystem에서 설정됨 |
| `BulletLifetime` | `DefaultLifetime` | 수명 타이머 |

## Prefab 설정 체크리스트
- [ ] `BulletAuthoring` 컴포넌트 추가
- [ ] `GhostAuthoringComponent` 추가 → Ghost Mode: `Interpolated`, Importance: `50`
- [ ] 스프라이트 또는 파티클 시스템 추가 (시각 표현)
- [ ] `GamePrefabsAuthoring`의 `BulletPrefab` 필드에 연결

## 관련 파일
- [`BulletGhostData`](../Components/Ghost/BulletGhostData.md)
- [`BulletLifetime`](../Components/Local/BulletLifetime.md)
- [`GamePrefabsAuthoring`](GamePrefabsAuthoring.md)
- [`ShootSystem`](../Systems/Combat/ShootSystem.md)
