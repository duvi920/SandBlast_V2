# SandBlast → 횡스크롤 액션 RPG 전환 계획

> 작성일: 2026-05-27  
> 기준 버전: SandBlast_V2 (Phase 5 완료 시점)  
> 목표: 멀티플레이 PvP 아레나 → 싱글플레이어 중심 횡스크롤 액션 RPG

---

## 1. 전환 방향 개요

### 핵심 방침

SandBlast의 **픽셀 CA 물리 엔진**은 그대로 유지한다. 이것이 이 프로젝트의 핵심 차별점이며, 횡스크롤 RPG 장르에서도 강력한 무기가 된다. 멀티플레이 아레나 구조만 제거하고, 싱글플레이어 레벨 기반 RPG 골격을 그 위에 얹는다.

### 장르 레퍼런스

| 레퍼런스 | 채용 요소 |
|----------|-----------|
| **Noita** | 픽셀 물리와 세계 탐험의 결합, 스펠 시스템 깊이 |
| **Hollow Knight** | 타이트한 전투, 보스 패턴, 지역별 세계관 |
| **Dead Cells** | 빠른 전투 리듬, 아이템 빌드 다양성 |
| **Terraria** | 광활한 2D 탐험, 환경 파괴와 건설 |

---

## 2. 재사용 / 수정 / 제거 분류

### 2-A. 그대로 재사용 (Keep)

| 시스템 | 위치 | 이유 |
|--------|------|------|
| CA 픽셀 물리 엔진 (5개 Job) | `ECS/Jobs/` | 핵심 차별점, 변경 불필요 |
| 셀 타입·반응 매트릭스 | `Core/CellType.cs` | 그대로 활용 |
| 플레이어 이동 (이동, 점프, 대시) | `Systems/Movement/` | 횡스크롤에 최적화된 구조 |
| 완드·스펠 시스템 | `Components/Wand/` | PvE 난이도 조절만 필요 |
| 근접 전투·스턴 | `Systems/Combat/` | 적 AI에 그대로 적용 |
| SPUM 캐릭터 애니메이션 | `SPUM/` | 적 스프라이트도 동일 파이프라인 활용 |
| PixelLayerRenderer | `Rendering/` | 렌더링 변경 없음 |
| 맵 템플릿 (ScriptableObject) | `Arena/MapTemplate.cs` | 구조 재활용, 내용만 교체 |
| ECS 아키텍처 전반 | `Scripts/` | 그대로 유지 |

### 2-B. 수정·확장 (Modify)

| 시스템 | 현재 상태 | 변경 방향 |
|--------|-----------|-----------|
| **카메라** | 고정 아레나 뷰 | 횡스크롤 추적 + 시야 잠금 영역 |
| **맵 포맷** | 소형 아레나 (고정 크기) | 다층 수평 레벨 (수배 넓이) |
| **ArenaMatchSystem** | PvP 타이머·승리조건 | 레벨 목표 달성 시스템으로 대체 |
| **ItemSpawnSystem** | 아레나 아이템 드롭 | 적 처치 드롭 + 보물상자 시스템 |
| **MapLoadSystem** | 서버 1회 실행 | 레벨 전환마다 실행, 체크포인트 반영 |
| **HUD** | HP·마나·타이머 | HP·마나·XP바·인벤토리 슬롯 추가 |
| **GoInGameSystem** | 2인 접속 대기 | 즉시 솔로 시작 |

### 2-C. 제거 (Remove)

| 시스템 | 위치 | 이유 |
|--------|------|------|
| NetCode for Entities (Ghost, RPC) | `Components/Ghost/`, `Systems/Connection/` | 싱글플레이어 전환 |
| RoomWatchSystem / RoomResetSystem | `Systems/Bootstrap/` | 매치메이킹 불필요 |
| TeamRespawnSystem | `Systems/BattleRoyale/` | 팀 개념 없음 |
| ArenaMatchSystem (PvP 로직) | `Systems/BattleRoyale/` | 레벨 시스템으로 완전 대체 |
| PixelGridRpcSendSystem | `Systems/Simulation/` | 네트워크 동기화 불필요 |
| ClientConnectionSystem | `Systems/Connection/` | 서버-클라이언트 구조 해체 |
| BurialCheckSystem (PvP 특화) | `Systems/BattleRoyale/` | PvE 용도 없음 (또는 재활용 검토) |

> **주의:** 네트워크 코드 제거는 Phase 1의 가장 큰 작업이다. ECS Ghost 컴포넌트가 여러 시스템에 산재해 있으므로 순서를 지켜 단계적으로 제거한다.

---

## 3. 새로 추가할 시스템

### 3-A. 적 AI 시스템 (최우선)

```
Systems/Enemy/
  EnemySpawnSystem.cs        // 레벨 내 적 배치 및 스폰
  EnemyAISystem.cs           // AI 상태머신 (순찰 → 추격 → 공격)
  EnemyAttackSystem.cs       // 적 공격 처리 (투사체·근접)
  EnemyDeathSystem.cs        // 사망 이벤트 → 드롭 트리거

Components/Enemy/
  EnemyComponent.cs          // EnemyType, PatrolRange, AggroRange
  EnemyStateComponent.cs     // Patrol / Chase / Attack / Stunned / Dead
  EnemyStatsComponent.cs     // HP, Attack, Speed, XPReward
```

**AI 상태머신:**

```
[Patrol] ──(플레이어 AggroRange 진입)──► [Chase]
   ▲                                        │
   │ (플레이어 이탈)                         ▼
   └──────────────────────────────── [Attack]
                                     │  (AttackRange 내)
                                     ▼
                                  [Stunned]
```

**기본 적 유형 (Phase 2 목표):**

| 타입 | 이동 | 공격 | 특수 |
|------|------|------|------|
| **슬라임** | 점프 이동 | 접촉 피해 | 분열 (사망 시 2마리) |
| **고블린 궁수** | 지상 순찰 | 원거리 화살 | 불 화살 변종 |
| **석상 골렘** | 느린 지상 | 근접 강타 | 폭발 시 잠시 마비 |
| **불꽃 박쥐** | 공중 부유 | 돌진 | 주변에 FIRE 셀 생성 |
| **포이즌 슬라임** | 이동 없음 | LIQUID_POISON 투사 | 처치 시 독 웅덩이 |

### 3-B. 캐릭터 진행 시스템 (RPG Core)

```
Systems/Progression/
  XPSystem.cs                // 경험치 획득·레벨업 처리
  StatScalingSystem.cs       // 레벨별 HP·마나·공격력 갱신
  SkillUnlockSystem.cs       // 레벨업 시 스킬 포인트 지급

Components/Progression/
  PlayerProgressComponent.cs // Level, XP, XPToNext, SkillPoints
  PlayerStatsComponent.cs    // MaxHP, MaxMana, AttackMult, DefenseMult
```

**레벨 업 커브 (초안):**

| 레벨 | 필요 XP | 보상 |
|------|---------|------|
| 1 | - | 시작 |
| 2 | 100 | HP +10, SkillPoint +1 |
| 3 | 250 | 마나 +15, SkillPoint +1 |
| 5 | 700 | 완드 슬롯 +1, SkillPoint +2 |
| 10 | 2500 | 새 스킬 해금, 특수 능력 |

**스킬 트리 (3개 계열):**

```
[파괴자 계열]          [연금술사 계열]        [유격대원 계열]
폭발 반경 +20%         액체 스프레이 +1       대시 쿨타임 -30%
지형 파괴 보너스 XP    독·용암 지속시간 +50%  이중 점프
폭발 셀프 면역         물질 합성 (새 셀 생성)  구른 후 무적 0.3초
```

### 3-C. 인벤토리·장비 시스템

```
Systems/Inventory/
  LootDropSystem.cs          // 적 사망 → 아이템 엔티티 스폰
  InventorySystem.cs         // 플레이어 아이템 보관 (최대 N 슬롯)
  EquipSystem.cs             // 완드·장비 장착/해제

Components/Inventory/
  InventoryComponent.cs      // IComponentData, Fixed-size 버퍼
  ItemDefinition.cs          // ScriptableObject: 이름, 스탯, 아이콘
  EquippedWandComponent.cs   // 현재 장착 완드 참조
```

**아이템 등급:**

| 등급 | 색상 | 드롭률 |
|------|------|--------|
| Common | 흰색 | 60% |
| Uncommon | 초록 | 25% |
| Rare | 파란색 | 12% |
| Legendary | 황금색 | 3% |

### 3-D. 레벨·씬 관리 시스템

```
Systems/Level/
  LevelManagerSystem.cs      // 레벨 로드·전환·상태 관리
  CheckpointSystem.cs        // 체크포인트 도달 시 저장
  LevelObjectiveSystem.cs    // 목표 추적 (적 처치, 보스 처치, 출구 도달)
  LevelTransitionSystem.cs   // 씬 전환 페이드·로딩

ScriptableObjects/
  LevelDefinition.cs         // MapTemplate + EnemyLayout + Objectives
  EnemyLayout.cs             // 적 배치 데이터 (위치, 타입, 순찰 경로)
```

**레벨 구조 예시:**

```
[시작 지점] ──> [탐험 구역 A] ──> [미니 보스] ──> [탐험 구역 B] ──> [보스 방] ──> [다음 레벨]
                     │                                    │
                [비밀 공간]                          [보물 상자]
               (숨겨진 아이템)                       (Rare 완드)
```

### 3-E. 보스 시스템

```
Systems/Boss/
  BossSpawnSystem.cs         // 보스 방 진입 시 스폰
  BossPhaseSystem.cs         // HP 임계값별 페이즈 전환
  BossAttackPatternSystem.cs // 어택 패턴 순환·랜덤 선택
  BossDeathSystem.cs         // 사망 연출·대규모 드롭·레벨 완료 트리거
```

**Phase 1 목표 보스 (2종):**

| 보스 | 특징 | 필살기 |
|------|------|--------|
| **용암 골렘** | 레벨 1 보스, 지면 LAVA 소환 | 용암 분출 → 맵 절반을 용암으로 덮음 |
| **독 마녀** | 레벨 2 보스, 공중 부유 | LIQUID_POISON 파동 → 독 안개 생성 |

### 3-F. 저장·불러오기 시스템

```
Systems/Save/
  SaveSystem.cs              // JSON 직렬화 → 로컬 파일
  SaveDataStructure.cs       // 저장 데이터 구조 (레벨, 인벤토리, 스탯)

저장 항목:
  - 현재 레벨 번호
  - 플레이어 레벨·XP
  - 장착 완드·스펠 구성
  - 인벤토리 내용
  - 체크포인트 위치
  - 획득한 스킬
```

---

## 4. 카메라 시스템 재설계

### 현재 → 목표

| 항목 | 현재 | 목표 |
|------|------|------|
| 카메라 모드 | 고정 아레나 전체 뷰 | 플레이어 추적 횡스크롤 |
| 시야 범위 | 전체 맵 표시 | 플레이어 중심 + 여유 오프셋 |
| 수직 이동 | 없음 | 점프 시 상하 여유 |
| 레벨 경계 | 없음 | 맵 경계에서 카메라 고정 |

### 구현 포인트

```csharp
// CameraFollowSystem 수정 방향
// 현재: 플레이어 위치에 단순 스냅
// 목표: 부드러운 추적 + 시야 잠금 + 수평 look-ahead

Vector3 target = playerPos + new Vector3(
    lookAheadX * inputDir,   // 이동 방향으로 미리 보기
    verticalOffset,          // 점프 시 위로 오프셋
    cameraZ
);
transform.position = Vector3.Lerp(current, target, smoothSpeed * dt);

// 맵 경계 클램프
target.x = Mathf.Clamp(target.x, minX + halfWidth, maxX - halfWidth);
target.y = Mathf.Clamp(target.y, minY + halfHeight, maxY - halfHeight);
```

---

## 5. 레벨 포맷 재설계

### 맵 크기 변화

| 구분 | 현재 (아레나) | 목표 (레벨) |
|------|--------------|-------------|
| 픽셀 그리드 크기 | ~256×256 | 512×256 ~ 1024×256 (수평 확장) |
| 청크 수 | ~64개 | 128~256개 |
| 레이어 구성 | 단일 전투 구역 | 탐험 + 전투 + 보스 구역 혼합 |

### LevelDefinition ScriptableObject

```csharp
[CreateAssetMenu]
public class LevelDefinition : ScriptableObject {
    public MapTemplate terrain;           // 지형 픽셀 데이터
    public EnemyLayout[] enemyLayouts;    // 적 배치 목록
    public Vector2Int[] checkpoints;      // 체크포인트 픽셀 좌표
    public Vector2Int exitPortalPos;      // 다음 레벨 입구
    public LevelObjective[] objectives;  // 클리어 조건
    public string bgmTrack;              // 배경음악
    public Color ambientColor;           // 조명 색조
}
```

### 레벨 테마 로드맵 (5개 월드)

| 월드 | 테마 | 주요 셀 타입 | 보스 |
|------|------|-------------|------|
| **1. 광산** | 흙·돌 동굴 | DIRT, POWDER_SAND | 용암 골렘 |
| **2. 독 늪** | 액체 지대 | LIQUID_POISON, LIQUID_WATER | 독 마녀 |
| **3. 화산** | 용암 지형 | LIQUID_LAVA, SOLID_BASALT | 불꽃 거인 |
| **4. 고대 유적** | 폭발물 함정 | SOLID_GUNPOWDER, SOLID_WOOD | 유적 수호자 |
| **5. 하늘 요새** | 부유 지형 | SOLID_INDESTRUCTIBLE | 최종 보스 |

---

## 6. 구현 로드맵 (Phase별)

### Phase A: 기반 전환 (4~6주)

> **목표:** 싱글플레이어로 부팅, 횡스크롤 카메라, 레벨 로드 작동

**작업 목록:**

- [ ] NetCode for Entities 의존성 제거
  - Ghost 컴포넌트 → 일반 IComponentData로 교체
  - RPC 시스템 전부 제거
  - ClientServerBootstrap → 단순 World 초기화로 교체
  - `ServerConnectionSystem`, `ClientConnectionSystem` 삭제
- [ ] RoomWatch/Reset/Countdown 시스템 제거
- [ ] `TeamRespawnSystem`, `ArenaMatchSystem` 제거
- [ ] `GoInGameSystem` → 즉시 레벨 로드로 교체
- [ ] `LevelManagerSystem` 신규 작성 (레벨 로드·전환)
- [ ] `CameraFollowSystem` 수정 (횡스크롤 + 경계 클램프)
- [ ] `LevelDefinition` ScriptableObject 작성
- [ ] 테스트 레벨 1개 제작 (적 없이, 물리만 동작 확인)

**완료 기준:** 1인이 단독으로 게임 시작 → 레벨 탐험 → 레벨 전환 가능

---

### Phase B: 적 AI (4~6주)

> **목표:** 기본 적 3종이 플레이어를 추격·공격

**작업 목록:**

- [ ] `EnemyComponent`, `EnemyStateComponent`, `EnemyStatsComponent` 작성
- [ ] `EnemyAISystem` 상태머신 구현 (Patrol/Chase/Attack)
- [ ] `EnemyAttackSystem` 구현 (근접 타격, 원거리 투사체)
- [ ] `EnemyDeathSystem` + XP 지급 연결
- [ ] `EnemySpawnSystem` 구현 (LevelDefinition 기반 배치)
- [ ] 슬라임, 고블린 궁수, 석상 골렘 3종 구현
- [ ] 적이 픽셀 물리와 상호작용하는지 검증 (불 → 적 피해 등)

**완료 기준:** 적 3종이 출현, 플레이어와 전투 가능, 처치 시 XP 지급

---

### Phase C: RPG 진행 (4~5주)

> **목표:** 레벨업, 스킬 포인트, 아이템 드롭·장착 작동

**작업 목록:**

- [ ] `PlayerProgressComponent`, `PlayerStatsComponent` 작성
- [ ] `XPSystem` 구현 (경험치 적산·레벨업 이벤트)
- [ ] `StatScalingSystem` 구현 (레벨별 스탯 갱신)
- [ ] `SkillUnlockSystem` + 3계열 스킬 트리 데이터 정의
- [ ] `LootDropSystem` 구현 (적 사망 → 아이템 엔티티)
- [ ] `ItemDefinition` ScriptableObject + 아이템 DB 기초 구성
- [ ] `InventorySystem` + `EquipSystem` 구현
- [ ] HUD에 XP바, 레벨 표시, 인벤토리 단축키(I) 추가

**완료 기준:** 적 처치 → XP 획득 → 레벨업 → 스킬 선택 → 아이템 장착 루프 완성

---

### Phase D: 레벨 디자인 & 저장 (3~4주)

> **목표:** 3개 레벨 완성, 체크포인트·저장 작동

**작업 목록:**

- [ ] `CheckpointSystem` 구현 (도달 → 저장 트리거)
- [ ] `SaveSystem` 구현 (JSON 파일 저장·불러오기)
- [ ] `LevelObjectiveSystem` 구현 (목표 체크 → 클리어 연출)
- [ ] 월드 1 레벨 1~3 디자인 (LevelDefinition 3개)
- [ ] 레벨 에디터 도구 확장 (ArenaMapEditorWindow → LevelEditorWindow)
- [ ] 레벨 전환 UI (페이드 인/아웃, 로딩 화면)

**완료 기준:** 레벨 1~3 순서대로 클리어, 중간 저장 후 이어하기 가능

---

### Phase E: 보스 & 콘텐츠 (4~6주)

> **목표:** 보스 2종, 월드 1 전체 완성

**작업 목록:**

- [ ] `BossPhaseSystem` 구현 (HP 임계값별 페이즈 전환)
- [ ] `BossAttackPatternSystem` 구현 (패턴 풀 + 랜덤 선택)
- [ ] 용암 골렘 보스 구현 (패턴 3개 이상)
- [ ] 독 마녀 보스 구현 (패턴 3개 이상)
- [ ] 보스 방 연출 (등장 컷씬, 배경음 전환, HP 바 표시)
- [ ] 월드 1 전체 밸런싱 (적 HP/XP, 스탯 스케일 조정)
- [ ] 사운드 기초 (공격음, 피격음, 폭발음, BGM 2트랙)

**완료 기준:** 월드 1 처음부터 보스 클리어까지 막힘 없이 플레이 가능

---

## 7. 기술 리스크 & 대응

| 리스크 | 심각도 | 대응 방안 |
|--------|--------|-----------|
| NetCode 제거 시 컴파일 오류 대량 발생 | 높음 | Phase A에서 Ghost 컴포넌트를 먼저 일반 IComponentData로 1:1 교체 후 네트워크 코드 제거 |
| 레벨 폭이 넓어지면 CA 시뮬레이션 성능 저하 | 중간 | 카메라 외 청크는 시뮬레이션 틱 감소 (화면 밖 = 저속 업데이트) |
| 적 AI가 픽셀 지형에서 길을 잃음 | 중간 | 픽셀 레벨 네비게이션 대신 유도 벡터(AI Steering) + 지형 감지 레이캐스트로 단순화 |
| ECS에서 복잡한 AI 상태머신 구현 부담 | 낮음 | 상태를 IComponentData enum으로 표현, 조건 분기를 각 시스템 OnUpdate()에서 처리 |
| 저장 시 픽셀 그리드(NativeArray) 직렬화 | 중간 | 레벨 재시작 방식으로 우선 처리, 플레이어 데이터만 저장 (지형 상태 저장은 추후) |

---

## 8. 월드 디자인 원칙

1. **환경 파괴가 전략**: 지형 파괴는 선택이 아닌 핵심 전술. 레벨은 파괴를 장려하는 구조로 설계.
2. **물질 상호작용 퍼즐**: 물 + 불 = 증기로 통로를 열거나, 폭발물로 벽을 뚫는 선택지를 제공.
3. **적도 물리에 반응**: 적이 용암에 빠지거나 폭발에 날려갈 수 있어야 한다.
4. **비밀 공간**: 파괴 가능한 벽 뒤 숨겨진 구역, 탐험 보상 제공.
5. **페이스 변화**: 전투 구역 → 탐험/퍼즐 구역 → 전투 구역 리듬 유지.

---

## 9. 우선순위 요약

| 순위 | Phase | 항목 | 예상 기간 |
|------|-------|------|-----------|
| 1 | A | NetCode 제거 + 싱글플레이어 전환 | 4~6주 |
| 2 | A | 횡스크롤 카메라 + 레벨 로드 | 포함 |
| 3 | B | 기본 적 AI 3종 | 4~6주 |
| 4 | C | XP/레벨업 + 인벤토리 | 4~5주 |
| 5 | D | 체크포인트·저장 + 레벨 3개 | 3~4주 |
| 6 | E | 보스 2종 + 사운드 기초 | 4~6주 |
| 7 | 추후 | 월드 2~5 콘텐츠 제작 | 미정 |
| 8 | 추후 | 선택적 Co-op 멀티플레이어 복원 | 미정 |

**총 예상 기간:** 약 5~6개월 (Phase A~E)

---

## 10. 다음 즉시 행동

Phase A를 시작하기 위한 첫 3가지 작업:

1. **의존성 파악**: NetCode for Entities를 직접 참조하는 파일 목록 추출  
   → `grep -r "Unity.NetCode" Assets/Scripts/ --include="*.cs" -l`
2. **Ghost 컴포넌트 매핑**: 각 Ghost 컴포넌트를 일반 IComponentData로 교체 목록 작성
3. **테스트 씬 준비**: 네트워크 없이 단독 실행 가능한 최소 씬 구성

