# 투사체 이펙트 시스템 계획 (v2 - 완드 시스템 통합)

> 작성일: 2026-04-27
> 대상 브랜치: feat/projectile-effects

---

## 1. 목표

기존 완드(Wand) 시스템을 확장하여 플레이어가 다양한 투사체 이펙트를 발사하고, 픽셀 CA 그리드와 고도화된 상호작용을 할 수 있도록 구현한다.

---

## 2. 투사체 및 페이로드 확장 설계

### 2-1. ProjectileType 확장
| 타입 | 설명 | 지형 충돌 효과 |
|------|------|---------------|
| **Bullet** | 기본 탄환 | 점 충격 (PixelImpactUtility.BulletImpact) |
| **Explosion** | 폭발탄 | 반경 내 파괴 + FIRE 생성 |
| **LiquidSpray** | 액체 살포 | 비행 중 및 충돌 시 액체 셀 생성 |
| **Ice** (신규) | 냉동탄 | 충돌 지점 온도 급감 + LIQUID 고정 |
| **Sand** (신규) | 모래탄 | 충돌 지점에 POWDER_SAND 생성 |
| **Lightning** (신규) | 번개 | 즉발 라인 관통 + 고온 발생 (Phase 3) |

### 2-2. LiquidPayload 확장
| 페이로드 | 생성 셀 타입 | 효과 |
|----------|--------------|------|
| **None** | - | - |
| **Lava** | LIQUID_LAVA | 고온, 인접 가연성 셀 점화 |
| **Water** (신규) | LIQUID_WATER | FIRE 소화, 인접 가연성 셀 습도 증가 |
| **Poison** (신규) | LIQUID_POISON | 플레이어 중독 데미지 영역 형성 |

---

## 3. 아키텍처 및 데이터 구조

### 3-1. Enum 수정 (`WandComponents.cs`)
- `ProjectileType` 및 `LiquidPayload`에 신규 항목 추가.

### 3-2. 시스템 흐름
- **WandCastSystem**: (기존 유지) `SpellData`를 읽어 `ProjectileComponent`를 포함한 Bullet 생성.
- **SpellProjectileSystem**: (확장) 새로운 `ProjectileType` 및 `Payload`에 따른 충돌 로직 분기 처리.
- **PixelImpactUtility**: (기능 추가)
    - `ExtinguishCircle`: 물탄용 소화 로직.
    - `FreezeCircle`: 냉동탄용 온도 저하 로직.
    - `StampCircle`: 모래/액체 스탬핑용 범용 로직.

---

## 4. 구현 순서 (태스크)

### Phase 1 – 데이터 정의 및 기초 (Branch: `feat/projectile-effects`)
- [ ] `ProjectileType` 및 `LiquidPayload` enum 업데이트
- [ ] `PixelImpactUtility`에 `ExtinguishCircle`, `StampCircle` 메서드 추가
- [ ] `SpellProjectileSystem`에서 `ProjectileType.Bullet`과 `Payload.Water` 연동 (물탄 기본 구현)

### Phase 2 – 특수 탄환 구현
- [ ] **화염/폭발 강화**: `Explosion` 시 반경 내 온도를 상승시켜 가연성 물질 즉시 점화
- [ ] **모래탄**: `ProjectileType.Sand` 추가 및 `CellType.POWDER_SAND` 생성 로직 연결
- [ ] **냉동탄**: `ProjectileType.Ice` 추가 및 주변 온도 급감 로직 구현

### Phase 3 – 고도화 및 UI
- [ ] **번개탄**: `ProjectileType.Lightning` 즉발 레이캐스트 로직 구현
- [ ] **독탄**: `LiquidPayload.Poison` 및 `PoisonDamageSystem` 연동
- [ ] **HUD**: 현재 선택된 완드 슬롯 및 주문 정보를 보여주는 UI(ArenaHud) 업데이트

---

## 5. 파일 변경 목록

| 파일 | 변경 내용 |
|------|----------|
| `Assets/Scripts/Components/Wand/WandComponents.cs` | Enum 확장 |
| `Assets/Scripts/Systems/Combat/SpellProjectileSystem.cs` | 신규 타입 처리 로직 추가 |
| `Assets/Scripts/ECS/Utils/PixelImpactUtility.cs` | 신규 충격 유틸리티 메서드 추가 |
| `Assets/Scripts/Presentation/ArenaHudBehaviour.cs` | 완드 정보 표시 기능 추가 |

---

## 6. 미결 사항
- 번개탄의 시각적 선 표현 (LineRenderer vs Particle)
- 냉동탄으로 얼린 LIQUID가 다시 녹는 메커니즘 (Temperature 기반 상태 변화 필요)
