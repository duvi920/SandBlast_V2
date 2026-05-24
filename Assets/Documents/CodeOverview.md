# SandBlast 코드 구조 개요

> 최종 갱신: 2026-04-30 (방 관리 시스템 추가 — RoomWatch/Reset/Countdown)  
> 기술 스택: Unity 6 (6000.3.10f1) · Unity ECS (DOTS) · Unity NetCode for Entities (NfE)  
> 구현 상태: **Phase 1~5 완료** + Room 관리 시스템(자동 리셋·카운트다운)

---

## 0. 프로젝트 상태

| 레이어 | 상태 |
|---|---|
| 픽셀 CA 엔진 (ECS) | ✅ 완료 — Fire/Smoke/Liquid/Powder/Debris 5단계 Job |
| 픽셀 그리드 네트워크 동기화 | ✅ 완료 — 청크 diff → RPC 전송/적용 |
| 플레이어 이동·충돌 | ✅ 완료 — 지형 3방향 픽셀 충돌 포함 |
| 완드(마법 무기) 시스템 | ✅ 완료 — WandCast / SpellProjectile / LiquidSpray / Mana |
| 픽셀 물질 확장 (Phase 3) | ✅ 완료 — Dirt, Wood, Gunpowder, Poison 추가 |
| 아레나 맵 (Phase 4) | ✅ 완료 — MapTemplate, MapLoadSystem, 에디터 도구 |
| 게임 모드 로직 (Phase 5) | ✅ 완료 — 타이머, 용암 상승, 승패 판정, 팀 부활, HUD |
| 레거시 MonoBehaviour | ⚠️ 잔존 — PlayerController.cs (Obsolete 표시됨, 삭제 대상) |
| 렌더링 | ⚠️ MonoBehaviour 유지 — PixelLayerRenderer (Texture2D는 메인스레드 전용) |

---

## 1. 전체 아키텍처

```
┌────────────────────────────────────────────────────────────────┐
│  GameBootstrap (ClientServerBootstrap)  — 포트 7979 자동 연결   │
└───────────────┬────────────────────────────────────────────────┘
                │
    ┌───────────┴─────────────┐
    │  Server World            │  Client World
    │  (권위적 시뮬레이션)      │  (예측 + 렌더링)
    └───────────┬─────────────┘
                │
   ┌────────────┴──────────────────────────────────────────┐
   │          ECS 싱글턴 레이어 (NativeArray)                │
   │  PixelGridSingleton  ChunkManagerSingleton             │
   │  SimConfigSingleton  ArenaMatchSingleton               │
   └────────────┬──────────────────────────────────────────┘
                │
   ┌────────────┴──────────────────────────────────────────┐
   │        CA 시뮬레이션 파이프라인 (Burst IJob)             │
   │  Fire → Smoke → Liquid → Powder → Debris               │
   └────────────┬──────────────────────────────────────────┘
                │ PixelGridSyncSystem (서버 5틱마다)
                │ PixelGridRpcSendSystem / PixelGridRpcApplySystem
                ▼
   ┌────────────────────────────────────────────────────────┐
   │       게임플레이 시스템 레이어 (Netcode 예측)              │
   │  PlayerMove / GravitySystem / GroundCheck               │
   │  WandCast / SpellProjectile / PoisonDamage              │
   │  ArenaMatch / TeamRespawn / MapLoad                     │
   └────────────────────────────────────────────────────────┘
```

---

## 2. 디렉터리 맵 (`Assets/Scripts/`)

```
Assets/Scripts/
├── ECS/                          # DOTS 핵심 레이어 (픽셀 CA 엔진)
│   ├── Authoring/                # 픽셀 엔진 설정용 Baker
│   ├── Singletons/               # NativeArray 보관 IComponentData
│   │   ├── PixelGridSingleton.cs
│   │   ├── ChunkManagerSingleton.cs
│   │   └── SimConfigSingleton.cs
│   ├── Components/               # 강체 관련 IComponentData & Buffer
│   ├── Jobs/                     # Burst IJob (CA 단계별)
│   │   ├── PixelFireJob.cs
│   │   ├── PixelSmokeJob.cs
│   │   ├── PixelLiquidJob.cs
│   │   ├── PixelPowderJob.cs
│   │   └── PixelDebrisJob.cs
│   ├── Systems/
│   │   ├── Simulation/           # CA 마스터 시스템 (PixelSimulationSystem)
│   │   ├── Rendering/            # PixelRenderSystem (텍스처 업로드)
│   │   ├── RigidBody/            # 강체 ECS 시스템
│   │   └── Sync/                 # SolidMaskSyncSystem
│   ├── Utils/
│   │   └── PixelImpactUtility.cs # 폭발·충격 픽셀 조작
│   ├── PixelGridAPI.cs           # MonoBehaviour → ECS 브리지 API
│   └── RigidBodyECSBridge.cs
│
├── Arena/                        # 아레나 맵 데이터 & 빌더
│   ├── MapTemplate.cs            # ScriptableObject 맵 데이터
│   ├── MapTemplateReference.cs   # managed IComponentData
│   └── ArenaMapBuilder.cs        # FillRect/Circle + 1v1/2v2 프리셋
│
├── Systems/                      # 게임플레이 ECS 시스템 (Netcode)
│   ├── Bootstrap/
│   │   ├── EnsureLocalPlayerSystem.cs
│   │   ├── GoInGameSystem.cs     # MapReadyTag 대기 후 InGame 전환·플레이어 스폰
│   │   ├── MapLoadSystem.cs      # MapTemplate → PixelGrid 1회 적용 + MapReadyTag 생성
│   │   ├── RoomWatchSystem.cs    # 접속자 수 감시 → 2인 접속 시 리셋 트리거
│   │   ├── RoomResetSystem.cs    # 맵·플레이어·매치 상태 초기화 + RoomResetRpc 브로드캐스트
│   │   └── MatchCountdownSystem.cs # Countdown 3→0 카운트 RPC 브로드캐스트
│   ├── Input/                    # GatherInputSystem
│   ├── Movement/                 # Gravity, GroundCheck, PlayerMove
│   ├── Combat/
│   │   ├── WandCastSystem.cs     # 완드 발사·마나 소모
│   │   ├── SpellProjectileSystem.cs # 투사체 충돌·피해·폭발
│   │   ├── LiquidSpraySystem.cs  # 액체 분사형 투사체
│   │   ├── ManaRegenSystem.cs
│   │   ├── PoisonDamageSystem.cs # 독 웅덩이 지속 피해
│   │   ├── BulletMoveSystem.cs   # 일반 투사체 이동
│   │   ├── BulletTerrainHitSystem.cs
│   │   ├── TerrainDamageSystem.cs # 강체 파괴 플래그 설정 (PendingPixelConversionTag)
│   │   ├── TerrainSyncSystem.cs   # Ghost IsDestroyed 동기화 → PendingPixelConversionTag
│   │   ├── DamageSystem.cs
│   │   └── HitDetectionSystem.cs
│   ├── Items/
│   │   ├── ItemSpawnSystem.cs    # 무기·회복 아이템 배치
│   │   ├── ItemPickupSystem.cs
│   │   └── WandPickupSystem.cs   # 완드 아이템 획득·프리셋 적용
│   ├── BattleRoyale/
│   │   ├── ArenaMatchSystem.cs   # 타이머·용암 상승·승패 판정
│   │   ├── TeamRespawnSystem.cs  # 2v2 팀원 부활
│   │   └── SurvivalCheckSystem.cs # 배틀로얄 생존자 판정 (레거시)
│   ├── Simulation/               # 픽셀 그리드 네트워크 동기화
│   │   ├── PixelGridSyncSystem.cs
│   │   ├── PixelGridRpcSendSystem.cs
│   │   ├── PixelGridRpcApplySystem.cs
│   │   ├── PixelGridInitialSyncSystem.cs # 신규 클라이언트 전체 맵 1회 전송
│   │   └── TerrainPaintRpcSystem.cs      # 클라이언트 페인트 RPC 수신·적용
│   ├── Connection/               # 서버/클라이언트 연결 관리
│   └── Presentation/
│       ├── PlayerVisualSystem.cs # SPUM Animator 연동
│       ├── AnimationSystem.cs
│       ├── CameraFollowSystem.cs
│       ├── SpriteFlipSystem.cs
│       ├── ArenaHudSystem.cs     # RPC 수신 → HUD 갱신
│       └── SpumAppearanceRandomizer.cs # SPUM 랜덤 외형 적용 유틸
│
├── Presentation/                 # MonoBehaviour HUD
│   └── ArenaHudBehaviour.cs     # IMGUI HP·마나·타이머·결과 화면
│
├── Components/
│   ├── Ghost/                    # NetCode GhostComponent (동기화 대상)
│   │   ├── PlayerGhostData.cs    # 위치·속도·HP·팀·부활 횟수
│   │   ├── BulletGhostData.cs
│   │   ├── ItemGhostData.cs
│   │   └── RigidBodyGhostData.cs
│   ├── Local/                    # 서버/클라이언트 로컬 컴포넌트
│   │   ├── ArenaMatchSingleton.cs # 매치 상태 (타이머·점수·용암)
│   │   ├── GamePrefabs.cs
│   │   ├── GameResultSingleton.cs
│   │   ├── PixelChunkDiff.cs
│   │   ├── RoomStateSingleton.cs  # 방 페이즈 (Solo/Countdown/Battle)
│   │   ├── RoomResetRequestTag.cs # 리셋 트리거 태그
│   │   └── (기타 PlayerInput, BulletLifetime 등)
│   ├── Rpc/
│   │   ├── PixelGridDeltaRpc.cs  # 청크 델타 동기화 RPC
│   │   ├── ArenaMatchRpc.cs      # 매치 틱·결과 RPC
│   │   ├── RoomResetRpc.cs       # 방 리셋 완료 알림
│   │   ├── MatchCountdownRpc.cs  # 카운트다운 숫자 브로드캐스트
│   │   └── TerrainPaintRpc.cs    # 클라이언트 페인트 요청
│   └── Wand/
│       └── WandComponents.cs     # WandComponent, SpellData, ManaComponent, ProjectileComponent
│
├── Core/                         # 공유 상수·유틸
│   ├── CellType.cs               # byte enum + IsSolid/IsLiquid/IsIndestructible 등
│   └── SimulationConstants.cs    # 엔진 전체 튜닝 상수 중앙 관리
│
├── Authoring/                    # Baker (MonoBehaviour → ECS Entity)
│   ├── PlayerAuthoring.cs
│   ├── GamePrefabsAuthoring.cs   # BulletPrefab, WandItemPrefab 등
│   ├── ArenaMatchAuthoring.cs    # ArenaMatchSingleton 초기값 설정
│   ├── MapTemplateAuthoring.cs   # MapTemplateReference Baker
│   └── (기타 BulletAuthoring, ItemAuthoring 등)
│
├── Player/                       # 과도기 레거시
│   ├── PlayerController.cs       # [Obsolete] ECS 완전 이전 후 삭제 예정
│   └── PlayerCamera.cs
│
├── Editor/
│   ├── ArenaMapEditorWindow.cs   # SandBlast > Arena Map Editor 메뉴
│   └── SandBlastSetupWindow.cs
│
├── Runtime/                      # 런타임 유틸
│   ├── RuntimePlayerSpawner.cs   # 로컬 플레이어 ECS 엔티티 생성
│   ├── EntityTransformFollower.cs # ECS LocalTransform → GameObject Transform 동기화
│   └── SandBlastAutoSetup.cs     # AfterSceneLoad 시 필수 오브젝트 자동 생성
│
└── Bootstrap/
    └── GameBootstrap.cs          # ClientServerBootstrap 진입점
```

---

## 3. 픽셀 CA 엔진 (ECS Core)

### 3.1 ECS 싱글턴 (`ECS/Singletons/`)

| 싱글턴 | 역할 |
|---|---|
| `PixelGridSingleton` | `NativeArray<byte>` 5채널(Type, Temp, Life, RigidId, Flam) 보관 |
| `ChunkManagerSingleton` | 32×32 청크별 슬리핑 상태(`ChunkMeta[]`) 관리 |
| `SimConfigSingleton` | 틱 타이머, XShuffle 버퍼, CellUpdateTick(이중 처리 방지) 보관 |

### 3.2 CA Job 파이프라인 (`ECS/Jobs/`)

`PixelSimulationSystem`이 매 틱마다 아래 순서로 `.Run()`(동기 Burst) 실행합니다.

| 순서 | Job | 역할 |
|---|---|---|
| 1 | `PixelFireJob` | 불 수명·열 방출·연료 전파·연기 생성·화약 폭발 |
| 2 | `PixelSmokeJob` | 연기 상승·수평 확산·수명 소진 |
| 3 | `PixelLiquidJob` | 물/용암/독 흐름·용암 냉각·용암+흙→현무암 반응 |
| 4 | `PixelPowderJob` | 모래/재 낙하·안식각 퇴적 |
| 5 | `PixelDebrisJob` | 강체 파편 낙하·지형화 |

### 3.3 픽셀 물질 (`Core/CellType.cs`)

| 분류 | 타입 |
|---|---|
| 고체 (파괴 가능) | `SOLID_DIRT`, `SOLID_WOOD`, `SOLID_GUNPOWDER` |
| 고체 (파괴 불가) | `SOLID_STATIC`, `SOLID_INDESTRUCTIBLE`, `SOLID_BASALT`, `SOLID_RIGID` |
| 액체 | `LIQUID_WATER`, `LIQUID_LAVA`, `LIQUID_POISON` |
| 기체·파티클 | `FIRE`, `GAS_SMOKE`, `POWDER_SAND`, `POWDER_ASH` |

---

## 4. 픽셀 그리드 네트워크 동기화

```
서버                                      클라이언트
PixelSimulationSystem (CA 계산)
  └─▶ PixelGridSyncSystem                  PixelGridRpcApplySystem
        (5틱마다 활성 청크 diff 수집)    ◀───── PixelGridDeltaRpc 수신
          └─▶ PixelGridRpcSendSystem          (청크별 Type 채널 패치)
                (RPC 브로드캐스트)

신규 접속 클라이언트
  └─▶ GoInGameSystem (MapReadyTag 확인 후 InGame 전환)
        └─▶ PixelGridInitialSyncSystem      (전체 맵 스냅샷 1회 전송, SLEEPING 청크 포함)
```

- `PixelChunkDiff`: `NativeArray<byte>(1024, Persistent)` — 32×32 청크 스냅샷
- `PixelGridDeltaRpc`: `FixedBytes4096` 앞 1024 bytes 사용 (blittable, NetCode 직렬화 호환)

---

## 5. 완드(마법 무기) 시스템

```
PlayerInput.Attack
  └─▶ WandCastSystem
        마나 확인·소모, 쿨다운 관리
        └─▶ ECB.Instantiate(BulletPrefab)
              + AddComponent<ProjectileComponent>
                  └─▶ SpellProjectileSystem  (지형 충돌·폭발·피해)
                  └─▶ LiquidSpraySystem      (용암/독 분사형)
```

| 컴포넌트 | 역할 |
|---|---|
| `WandComponent` | 활성 슬롯, 쿨다운, 자동 발사 여부 |
| `SpellData` (Buffer) | 투사체 유형·액체·수정자·마나비용·피해량 |
| `ManaComponent` | 현재/최대 마나, 회복 속도 |
| `ProjectileComponent` | 서버 로컬 전용 — 투사체 종류·반경·관통 수 |

---

## 6. 아레나 게임 모드

### 매치 흐름

```
씬 로드
  └─▶ MapLoadSystem (서버, 1회)
        MapTemplate → ArenaMapBuilder.Apply() → PixelGrid
        WandItem 스폰 (MapTemplate.WandSpawnPoints)
  └─▶ ItemSpawnSystem (서버, 1회)
        SpawnPoint마다 무기/회복 아이템 배치

게임 중
  └─▶ ArenaMatchSystem (서버, 매 틱)
        타이머 감산 → 남은 시간 50%에서 용암 상승 활성화
        용암 상승: 바닥에서 올라오며 EMPTY/WATER/POISON → LIQUID_LAVA 치환
        승패 판정 (1v1: 생존자 1명 / 2v2: 팀 전멸)
        시간 초과: 팀 점수 비교로 결산
        주기적 ArenaMatchTickRpc 브로드캐스트

종료
  └─▶ ArenaMatchEndRpc → 클라이언트
        └─▶ ArenaHudSystem → ArenaHudBehaviour.ShowResult()
```

### 2v2 팀 부활

- `TeamRespawnSystem`: 팀원 생존 + `RespawnCount > 0` 조건 충족 시 즉시 부활
- `MapSpawnPoint.TeamId`: 팀0=짝수 SpawnId, 팀1=홀수 SpawnId 규칙

---

## 7. 방(Room) 관리 시스템

```
접속자 수 변화 (RoomWatchSystem, 서버 매 프레임)
  1인 → 2인: RoomResetRequestTag 생성, Phase = Countdown
  2인 → 1인 (Countdown 중): Phase = Solo 복귀

RoomResetRequestTag 존재 시 (RoomResetSystem, 서버)
  ① PixelGrid 전체 초기화 → MapTemplate 재적용
  ② ArenaMatchSingleton 리셋 (타이머·점수·용암 초기화)
  ③ GameResultSingleton 리셋
  ④ 모든 플레이어 HP 100, 위치 = SpawnPoint 순서대로 배치
  ⑤ RoomResetRpc 전체 클라이언트 브로드캐스트
  ⑥ RoomResetRequestTag 제거

Phase = Countdown 구간 (MatchCountdownSystem, 서버)
  RoomResetRequestTag 처리 완료 대기
  CountdownTimer 3→0 감산
  매초 MatchCountdownRpc(Count) 브로드캐스트
  CountdownTimer <= 0: Phase = Battle
```

| 컴포넌트 | 역할 |
|---|---|
| `RoomStateSingleton` | Phase(Solo/Countdown/Battle), CountdownTimer, LastPlayerCount |
| `RoomResetRequestTag` | 리셋 원인 신호 — 생성되는 순간 RoomResetSystem이 1회 처리 후 삭제 |
| `RoomResetRpc` | 방 리셋 완료 알림 — 클라이언트 씬 재초기화에 활용 가능 |
| `MatchCountdownRpc` | Count 필드: 3·2·1·0(=GO) |

---

## 9. 강체(RigidBody) 및 동기화

`SolidMaskSyncSystem` (ECS)이 매 프레임 `LocalToWorld` 변화를 감지하여 픽셀 그리드의 `SOLID_RIGID` 마스크를 갱신합니다. 강체가 파괴되면 `RigidToPixelConvertSystem`이 폴리곤을 래스터화하여 `SOLID_DEBRIS` 셀로 변환합니다.

---

## 10. 주요 상수 (`Core/SimulationConstants.cs`)

| 카테고리 | 상수 | 값 |
|---|---|---|
| 픽셀 엔진 | `CHUNK_SIZE` | 32 px |
| 픽셀 엔진 | `TICKS_PER_SECOND` | 30 |
| 이동 | `MoveSpeed` (PlayerMoveSystem) | 6 m/s |
| 이동 | `JumpForce` | 14 m/s |
| 이동 | `DashSpeed` | 20 m/s |
| 불 | `FIRE_LIFE_MAX` | 80 |
| 용암 | `LAVA_INITIAL_TEMP` | 240 |
| 화약 | `GUNPOWDER_BLAST_RADIUS` | 16 px |
| 독 | `POISON_DAMAGE_PER_SECOND` | 2 HP/s |

---

## 11. 씬 설정 체크리스트

Unity Editor에서 아레나 씬을 구성할 때 필요한 오브젝트/컴포넌트:

1. **GameManager 오브젝트**
   - `GameResultAuthoring`
   - `ArenaMatchAuthoring` (MatchDuration, LavaRiseSpeed 설정)
   - `MapTemplateAuthoring` (1v1 또는 2v2 에셋 할당)
   - `GamePrefabsAuthoring` (BulletPrefab, WandItemPrefab 등 할당)

2. **ArenaMapEditorWindow** (`SandBlast > Arena Map Editor`)
   - 1v1 / 2v2 프리셋 에셋 생성 → `Assets/MapTemplates/` 저장

3. **ArenaHudBehaviour** MonoBehaviour — 씬의 Canvas 또는 빈 오브젝트에 부착

---

## 12. 알려진 이슈 및 개선 대상

> 상세 내용: `ImprovementTasks.md` 참조

| 우선도 | 항목 |
|---|---|
| 높음 | `PlayerController.cs` 삭제 (ECS 완전 이전 완료됨) |
| 높음 | `PixelImpactUtility` 파괴 가능 셀 목록 하드코드 → `CellTypeExt` 활용으로 교체 |
| 높음 | `TeamRespawnSystem` 즉시 부활 → float `RespawnTimer` 필드로 딜레이 구현 |
| 중간 | `PixelGridSyncSystem` O(n) 변경 감지 → Dirty flag 기반으로 교체 |
| 중간 | `WandComponents.cs` Bounce 수정자 미구현 |
| 낮음 | `ArenaMatchSystem` 매치 동기화 RPC 전송 주기 설정값으로 외부화 |
