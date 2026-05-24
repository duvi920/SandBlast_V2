# 매몰 승리 규칙 계획 (v3)

> 작성일: 2026-04-30  
> 수정일: 2026-04-30 (v3 — 삽 동작 방식·매몰 조건 변경)  
> 대상 브랜치: `claude/add-burial-victory-rule-jjNSn`

---

## 1. 목표

삽으로 자기 주변에 모래를 뿌려 상대를 파묻으면 즉시 승리하는 규칙을 추가한다.
HP 소진이 아닌 **별도의 즉사 승리 루트**로 동작하며, 기존 `SurvivalCheckSystem` / `ArenaMatchSystem`의 승패 흐름은 변경 없이 재사용한다.
매몰 사망은 **기절 여부와 무관**하게 발동된다.

---

## 2. 전체 전투 흐름

```
[공격자]
  ├─ 근접 공격 (MeleeAttack 입력)          ─┐
  └─ 원거리 공격 (기존 완드/투사체 명중)    ─┤→ 피격자 스턴 게이지 누적
                                              │
                                              ▼
                                    스턴 게이지 MAX 도달
                                              │
                                              ▼
                                    피격자 IsStunned = true
                                    (StunDuration 동안 행동 불가)
                                              │
                              (기절은 선택적 — 매몰 판정에 필수 아님)
                              │
                    [공격자] 삽(Shovel) 아이템 소지 상태에서
                    상대 근처에서 UseShovel 입력
                              │
                              ▼
                    ShovelUseSystem → StampCircle(POWDER_SAND, 공격자 자신 주변)
                              │
                              ▼
                    BurialCheckSystem → 모든 플레이어 대상 주변 Solid 80% 이상 확인
                    (기절 여부 무관)
                              │
                              ▼
                    ghostData.IsDead = true → 기존 SurvivalCheckSystem이 승리 감지
```

---

## 3. 기절(Stun) 시스템

### 3-1. 스턴 게이지

플레이어마다 `StunAccumulator` 값을 보유한다.

| 공격 종류 | 스턴 게이지 증가량 |
|-----------|------------------|
| 근접 공격 (MeleeAttack) | 40 |
| 완드 투사체 명중 | 15 |
| 폭발 범위 명중 | 20 |

- 게이지 최댓값: **100**
- 게이지가 100에 도달하면 **IsStunned = true**, 게이지 0 초기화
- 피격이 없으면 초당 **10씩** 자동 감소 (전투 이탈 시 회복)

### 3-2. 기절 상태

| 항목 | 값 |
|------|----|
| 지속 시간 | 3.0초 |
| 행동 제한 | 이동·점프·대시·공격·삽 사용 불가 |
| 시각 피드백 | `PlayerGhostData.IsStunned`를 Ghost로 동기화 → 클라이언트 애니메이션/이펙트 처리 |
| 해제 조건 | StunTimer 만료. 추가 피격은 타이머를 갱신하지 않음 (중첩 없음) |

### 3-3. 신규 컴포넌트 — `StunStateComponent`

```csharp
// Assets/Scripts/Components/Local/StunStateComponent.cs
// 서버 전용 로컬 — Ghost 불필요
public struct StunStateComponent : IComponentData
{
    public float StunAccumulator;  // 현재 스턴 게이지 (0~100)
    public float StunTimer;        // 남은 기절 시간 (초)
}
```

`PlayerGhostData`에 추가:
```csharp
[GhostField] public bool IsStunned;  // 클라이언트 렌더링용 동기화
```

---

## 4. 근접 공격 시스템

### 4-1. 입력

`PlayerInput`에 새 입력 추가:
```csharp
public InputEvent MeleeAttack;   // 근접 공격 (기본키: F 또는 좌클릭 장시간)
public InputEvent UseShovel;     // 삽 사용
```

### 4-2. MeleeAttackSystem

```
[UpdateInGroup(SimulationSystemGroup)]
[UpdateBefore(HitDetectionSystem)]
[WorldSystemFilter(ServerSimulation)]
```

**로직:**
- 공격자 주변 **0.9 유닛** 이내의 살아있는 적 플레이어 탐색
- 명중 시: `HitEvent { Damage = 8f, StunPower = 40f }` 버퍼에 추가
- 쿨다운: **0.6초**
- 기절 상태인 플레이어는 MeleeAttack 불가

### 4-3. HitEvent 확장

```csharp
public struct HitEvent : IBufferElementData
{
    public Entity  TargetEntity;
    public float   Damage;
    public float2  HitPosition;
    public float   StunPower;   // ← 신규 (0이면 스턴 없음)
}
```

---

## 5. 삽(Shovel) 시스템

### 5-1. 아이템 정의

- 기존 `ItemSpawnSystem`이 관리하는 아이템 풀에 `SHOVEL` 타입 추가
- 맵의 지정 스폰 포인트에 배치 (완드 아이템과 동일 방식)
- 줍기: `Interact` 입력으로 획득. 완드와 달리 **단일 소지** 가능
- `PlayerGhostData.HasShovel: bool` (Ghost 동기화) 추가

### 5-2. ShovelUseSystem

```
[UpdateInGroup(SimulationSystemGroup)]
[UpdateAfter(StunSystem)]
[UpdateBefore(BurialCheckSystem)]
[WorldSystemFilter(ServerSimulation)]
```

**실행 조건:**
1. 공격자가 삽을 소지 중 (`HasShovel == true`)
2. `UseShovel` 입력 발생

**실행 내용:**
```
PixelImpactUtility.StampCircle(
    center : 공격자 자신의 픽셀 좌표,
    radius : 6px,
    cellType: CellType.POWDER_SAND
)
```
- 삽 1회 사용당 **자기 주변** 반경 6px 원형으로 모래 셀 채우기
- 상대를 향해 달려들어 모래를 뿌리는 방식 — 기절 여부와 무관하게 사용 가능
- 연속 사용 가능 (쿨다운 없음, 매 입력마다 1회)
- 삽은 사용 **3회** 후 소멸 (`ShovelUseCount` 차감)

### 5-3. 신규 컴포넌트 — `ShovelComponent`

```csharp
// Assets/Scripts/Components/Local/ShovelComponent.cs
public struct ShovelComponent : IComponentData
{
    public int UseCount;  // 남은 사용 횟수 (기본값: 3)
}
```

---

## 6. 매몰 판정 시스템 (업데이트)

### 6-1. 판정 조건 (v1→v3 변경 이력)

| 항목 | v1 | v2 | v3 (채택) |
|------|----|----|-----------|
| 대상 | 모든 플레이어 | IsStunned인 플레이어만 | **모든 살아있는 플레이어** |
| 트리거 | 자동(물리적 포위) | 삽 사용 후 확인 | **삽 사용 후 확인** |
| 즉사 조건 | 80% 포위 + 0.3초 | 80% 포위 즉시 | **80% 포위 즉시** |
| 기절 필요 | — | 필요 | **불필요** |

### 6-2. 픽셀 샘플링

플레이어 캡슐 내부 **3×5 그리드 (15개 지점)** 샘플링은 동일하게 유지:

```
x 샘플: pos.x + {-0.20, 0, +0.20}
y 샘플: pos.y + {-0.60, -0.30, 0, +0.30, +0.60}
```

- 15개 중 **12개 이상(80%)** Solid → 즉사 처리
- 기절 여부와 무관하게 모든 살아있는 플레이어에 적용

### 6-3. BurialCheckSystem 실행 흐름

```
BurialCheckSystem.OnUpdate()
  ├─ Phase != Battle → return
  ├─ foreach player:
  │   ├─ IsDead == true → skip
  │   ├─ SamplePixels(3×5) → solidCount   ← 기절 여부 무관
  │   └─ solidCount >= 12
  │       → ghostData.IsDead = true
  │       → ecb.AddComponent(DeathEvent { KillerNetworkId = -1, Cause = Buried })
  └─ ecb.Playback()
```

---

## 7. 시스템 실행 순서

```
PredictedSimulationSystemGroup
  └─ PlayerMoveSystem            (IsStunned이면 입력 무시)

SimulationSystemGroup
  ├─ MeleeAttackSystem           ← 신규
  ├─ BulletMoveSystem
  ├─ HitDetectionSystem
  ├─ StunSystem                  ← 신규 (HitEvent의 StunPower 처리)
  ├─ ShovelUseSystem             ← 신규
  ├─ BurialCheckSystem           ← 신규 (기절+매몰 판정)
  ├─ DamageSystem                (기존 유지)
  ├─ SurvivalCheckSystem         (기존 유지)
  └─ ArenaMatchSystem            (기존 유지)
```

---

## 8. 구현 순서 (태스크)

### Phase 1 — 기절 시스템

- [ ] `StunStateComponent.cs` 생성
- [ ] `PlayerGhostData`에 `[GhostField] bool IsStunned` 추가
- [ ] `HitEvent`에 `float StunPower` 추가
- [ ] `StunSystem.cs` 구현 (게이지 누적, 기절 적용, 타이머 감소, 자동 회복)
- [ ] `PlayerMoveSystem`에 `IsStunned` 체크 추가 (입력 무시)
- [ ] `PlayerAuthoring`에 `StunStateComponent` Baker 추가

### Phase 2 — 근접 공격

- [ ] `PlayerInput`에 `InputEvent MeleeAttack` 추가
- [ ] `MeleeAttackSystem.cs` 구현 (범위 판정, HitEvent StunPower=40 추가, 쿨다운)
- [ ] 클라이언트 입력 수집: `GatherInputSystem`에서 F키 → `MeleeAttack` 매핑

### Phase 3 — 삽 아이템

- [ ] `ShovelComponent.cs` 생성
- [ ] `PlayerGhostData`에 `[GhostField] bool HasShovel`, `int ShovelUseCount` 추가
- [ ] `PlayerInput`에 `InputEvent UseShovel` 추가
- [ ] 아이템 타입 enum에 `Shovel` 추가 및 `ItemSpawnSystem`에 삽 스폰 포인트 추가
- [ ] `ItemPickupSystem`에서 삽 획득 처리
- [ ] `ShovelUseSystem.cs` 구현 (근거리 기절 플레이어 탐색 → StampCircle → UseCount 차감)

### Phase 4 — 매몰 판정 및 승리

- [ ] `BurialCheckSystem.cs` 구현 (기절 조건 추가, 타이머 제거, KillerNetworkId 기록)
- [ ] `DeathEvent`에 `DeathCause` enum 추가 (`Damage` / `Buried` / `Lava`)
- [ ] HUD 결과 화면에 매몰 사망 메시지 표시

### Phase 5 — 밸런스 및 튜닝

- [ ] `SimulationConstants.cs`에 스턴 관련 상수 외부화
  - `StunThreshold = 100f`, `StunDecayRate = 10f`, `StunDuration = 3.0f`
  - `MeleeStunPower = 40f`, `BulletStunPower = 15f`, `ExplosionStunPower = 20f`
  - `BurialSolidRatio = 0.8f`, `ShovelRadius = 6`, `ShovelUseCount = 3`
- [ ] 아레나 테스트 맵에서 전체 흐름 검증

---

## 9. 파일 변경 목록

| 파일 | 작업 |
|------|------|
| `Components/Local/StunStateComponent.cs` | **신규** — 스턴 게이지·타이머 |
| `Components/Local/ShovelComponent.cs` | **신규** — 삽 사용 횟수 |
| `Systems/Combat/MeleeAttackSystem.cs` | **신규** — 근접 공격 판정 |
| `Systems/Combat/StunSystem.cs` | **신규** — 스턴 누적·적용·해제 |
| `Systems/Combat/ShovelUseSystem.cs` | **신규** — 삽 사용 → 자기 주변 StampCircle(POWDER_SAND) |
| `Systems/BattleRoyale/BurialCheckSystem.cs` | **신규** — 매몰 즉사 판정 (기절 무관) |
| `Components/Ghost/PlayerGhostData.cs` | **수정** — `IsStunned`, `HasShovel`, `ShovelUseCount` Ghost 필드 추가 |
| `Components/Local/HitEvent.cs` | **수정** — `StunPower` 필드 추가 |
| `Components/Local/PlayerInput.cs` | **수정** — `MeleeAttack`, `UseShovel` InputEvent 추가 |
| `Components/Local/DeathEvent.cs` | **수정 (Phase 4)** — `DeathCause` enum 추가 |
| `Systems/Movement/PlayerMoveSystem.cs` | **수정** — `IsStunned` 시 입력 처리 건너뜀 |
| `Systems/Combat/HitDetectionSystem.cs` | **수정** — HitEvent에 StunPower 전달 |
| `Core/SimulationConstants.cs` | **수정 (Phase 5)** — 스턴·매몰 상수 추가 |
| `Authoring/PlayerAuthoring.cs` | **수정** — StunStateComponent Baker 추가 |

---

## 10. 미결 사항

- **근접 공격 키**: F키 단독 vs. 조준 없이 좌클릭 장시간 vs. 별도 근접 전용 슬롯
- **기절 경고 UI**: 피격자 머리 위 스턴 게이지 바 표시 여부
- **삽 스폰 위치**: 완드 아이템과 동일 스폰 풀 공유 vs. 고정 위치 별도 배치
- **팀전(2v2)에서 팀원 구출**: 아군 옆에서 지형을 폭발로 파괴하면 매몰 해제 → 의도된 협력 메커니즘으로 허용할지 결정
- **자기 매몰 방지**: 공격자가 자기 주변에 뿌린 모래로 자신도 매몰될 수 있음 — 허용(실력 요소) vs. 공격자 본인 면역 처리 여부
