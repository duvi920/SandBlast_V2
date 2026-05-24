# BulletGhostData.cs

## 역할
투사체(총알)의 상태를 동기화하는 Ghost 컴포넌트. 서버가 권위를 가지며 클라이언트는 보간만 수행한다.

## Ghost 설정

| 항목 | 값 |
|------|----|
| Ghost 모드 | `Interpolated` (GhostAuthoringComponent에서 설정) |
| Importance | `50` |

## 필드 목록

| 필드 | 타입 | 동기화 | 설명 |
|------|------|--------|------|
| `Position` | `float2` | ✅ | 투사체 현재 위치 |
| `Velocity` | `float2` | ✅ | 이동 방향 및 속도 |
| `OwnerNetworkId` | `int` | ✅ | 발사한 플레이어의 NetworkId (자기 자신 충돌 방지용) |
| `Damage` | `float` | ✅ Quantization=10 | 피해량 |
| `BulletType` | `int` | ✅ | 투사체 종류 (0=일반, 1=관통, 2=폭발) |

## Interpolated 모드 특성
- 클라이언트는 서버 스냅샷 사이를 **보간**하여 부드러운 이동을 표현한다.
- 클라이언트 측 히트 판정은 없고, 서버의 `HitDetectionSystem`만이 피해를 결정한다.
- 투사체 수가 많을 때 Importance=50이 PlayerGhostData(100)보다 낮아 대역폭이 부족하면 후순위로 전송된다.

## 관련 시스템
- [`ShootSystem`](../../Systems/Combat/ShootSystem.md) — 생성 및 초기값 설정
- [`BulletMoveSystem`](../../Systems/Combat/BulletMoveSystem.md) — Position 갱신, 수명 관리
- [`HitDetectionSystem`](../../Systems/Combat/HitDetectionSystem.md) — 충돌 감지 후 삭제
