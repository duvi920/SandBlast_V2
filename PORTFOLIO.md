# SandBlast — 포트폴리오

> Unity 6 · DOTS/ECS · Netcode for Entities 기반 픽셀 물리 PvP 게임  
> 개발 기간: 2026년 4월 ~ (진행 중)

---

## 프로젝트 개요

**"지형이 살아있는 배틀 게임"**

모든 픽셀이 물리 반응을 갖는 파괴 가능한 아레나에서 완드(마법 무기)로 싸우는 멀티플레이어 PvP 게임입니다. 단순 조준·사격이 아닌 **지형 파괴·액체 흘리기·연쇄 폭발**이 전술이 되며, Noita 스타일의 픽셀 물리 시뮬레이션과 실시간 멀티플레이어를 결합한 것이 핵심 차별점입니다.

| 지원 모드 | 인원 | 설명 |
|---|---|---|
| 아레나 듀얼 | 1v1 | 소규모 밀폐 맵, 제한 시간 초과 시 용암 상승으로 강제 결판 |
| 팀 아레나 | 2v2 | 중간 규모 맵, 팀원 부활 1회, 지형 통제 전술이 핵심 |

---

## 기술 스택

| 항목 | 기술 |
|---|---|
| 엔진 | Unity 6 (6000.3.10f1) |
| 물리 시뮬레이션 | Unity DOTS/ECS + Burst IJob (Cellular Automata) |
| 멀티플레이어 | Unity Netcode for Entities (NfE) |
| 렌더링 | Texture2D 직접 업로드 (MonoBehaviour 유지) + 픽셀 레이어 합성 |
| 입력 예측 | PredictedSimulationSystemGroup (클라이언트 예측) |
| 애니메이션 | SPUM (Sprite Parts Unity Module) |

---

## 아키텍처

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
                │ PixelGridSyncSystem (서버 5틱마다 diff 수집)
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

## 핵심 구현: 픽셀 CA 엔진

### 설계 원칙

| 레이어 | 기술 | 역할 |
|---|---|---|
| 강체 레이어 | Unity Physics 2D | 지형 구조물·이동 오브젝트 — 콜라이더 기반 물리 |
| 픽셀 레이어 | Cellular Automata (ECS Job) | 파괴 잔해·유체·모래·화재·연기 |

강체가 파괴될 때 `RigidToPixelConvertSystem`이 폴리곤을 래스터화하여 `SOLID_DEBRIS`로 전환하며, 전환 순간 Rigidbody2D의 속도를 파티클에 분배해 자연스러운 파쇄 연출을 구현합니다.

### 픽셀 물질 (CellType)

| 분류 | 타입 |
|---|---|
| 고체 (파괴 가능) | `SOLID_DIRT`, `SOLID_WOOD`, `SOLID_GUNPOWDER` |
| 고체 (파괴 불가) | `SOLID_STATIC`, `SOLID_INDESTRUCTIBLE`, `SOLID_BASALT`, `SOLID_RIGID` |
| 액체 | `LIQUID_WATER`, `LIQUID_LAVA`, `LIQUID_POISON` |
| 기체·파티클 | `FIRE`, `GAS_SMOKE`, `POWDER_SAND`, `POWDER_ASH` |

### CA 처리 파이프라인 (매 틱 순서)

```
1. SOLID_RIGID 마스크 동기화   ← 강체 위치 확정 먼저
2. FIRE 전파 + 수명 감소        (PixelFireJob)
3. GAS_SMOKE 상승 + 소멸        (PixelSmokeJob)
4. LIQUID 흐름                  (PixelLiquidJob)
5. POWDER 낙하 + 대각 흐름      (PixelPowderJob)
6. SOLID_DEBRIS 중력 + 슬리핑   (PixelDebrisJob)
```

처리 순서를 고정하는 이유: 같은 틱에 POWDER가 낙하하여 생긴 빈 공간에 FIRE가 전파될 때, 순서가 바뀌면 프레임마다 결과가 달라지는 비결정적 동작이 발생하기 때문입니다.

### 반응 매트릭스

```
나무(WOOD) + 불(FIRE)       → 연소 → 지형 붕괴
화약(GUNPOWDER) + 불/충격   → 폭발 → 주변 셀 파괴 + 넉백
용암(LAVA) + 흙(DIRT)       → 현무암(BASALT) 생성 → 통로 차단
독(POISON) + 플레이어 접촉  → 2 HP/s 지속 피해
```

### 청크 슬리핑 최적화

32×32 단위 청크로 분할하여 이동 셀이 없는 청크를 `SLEEPING` 상태로 전환, 시뮬레이션 순회에서 완전히 제외합니다. 경계 셀에 활성 타입이 유입될 때만 인접 청크를 깨우는 전파 방식으로 성능을 유지합니다.

```
ChunkState: ACTIVE → SLEEP_PENDING → SLEEPING
웨이크업 조건: 셀 유입 / 강체 Displacement / 폭발 반경 진입
```

---

## 핵심 구현: 픽셀 그리드 네트워크 동기화

멀티플레이어에서 픽셀 그리드 상태를 모든 클라이언트와 일치시키는 것이 이 프로젝트의 가장 큰 기술 도전이었습니다.

```
서버                                      클라이언트
PixelSimulationSystem (CA 계산)
  └─▶ PixelGridSyncSystem                  PixelGridRpcApplySystem
        (5틱마다 활성 청크 diff 수집)    ◀───── PixelGridDeltaRpc 수신
          └─▶ PixelGridRpcSendSystem          (청크별 Type 채널 패치)
                (RPC 브로드캐스트)

신규 접속:
  └─▶ PixelGridInitialSyncSystem → 전체 맵 스냅샷 1회 전송
```

- `PixelChunkDiff`: `NativeArray<byte>(1024, Persistent)` — 청크 스냅샷
- `PixelGridDeltaRpc`: `FixedBytes4096` 직렬화 — blittable, NetCode 호환
- 청크 슬리핑 적용으로 비활성 영역은 전송하지 않아 대역폭 절감
- 대역폭 추정 (2v2): 약 150~450 KB/s (활성 청크 최대 20~30개 기준)

---

## 핵심 구현: 완드(마법 무기) 시스템

```
PlayerInput.Attack
  └─▶ WandCastSystem
        마나 확인·소모, 쿨다운 관리
        └─▶ ECB.Instantiate(BulletPrefab) + ProjectileComponent
              ├─▶ SpellProjectileSystem  (지형 충돌·폭발·플레이어 피해·관통)
              └─▶ LiquidSpraySystem      (용암/독 분사 — 비행 경로에 셀 생성)
```

| 컴포넌트 | 역할 |
|---|---|
| `WandComponent` | 슬롯 배열, 쿨다운, 자동 발사 여부 |
| `SpellData` (Buffer) | 투사체 유형·액체 페이로드·수정자·마나 비용·피해량 |
| `ManaComponent` | 현재/최대 마나, 회복 속도 |
| `ProjectileComponent` | 서버 로컬 — 투사체 종류·폭발 반경·관통 횟수 |

PvP 빌드 예시: 폭발 탄환 멀티샷(지형 파괴), 용암 방사(진입로 차단), 독 탄환(구역 거부), 관통 저격

---

## 핵심 구현: 매몰 승리 규칙

가장 최근에 구현한 독창적인 게임 메카닉입니다.

**흐름:** 근접공격 → 기절 → 삽으로 모래 매몰 → 매몰 판정으로 승리

| 시스템 | 역할 |
|---|---|
| `MeleeAttackSystem` | 근접 공격 판정 — 일정 범위 내 적에게 HitEvent 발생 |
| `StunSystem` | 피격 시 기절 상태(`StunStateComponent`) 부여 및 지속시간 관리 |
| `ShovelUseSystem` | 기절한 적 주변에 픽셀(모래 등) 생성 — 삽 사용 입력 처리 |
| `BurialCheckSystem` | 매 틱 기절 플레이어의 사방 픽셀 밀도 측정 → 임계값 초과 시 매몰 판정 |

---

## 멀티플레이어 시스템 구조

### 클라이언트 예측

이동 관련 시스템 전체가 `PredictedSimulationSystemGroup` 내에서 동작합니다.

| 시스템 | World | 설명 |
|---|---|---|
| `GroundCheckSystem` | Client + Server | 픽셀 그리드 기반 지면 판정 |
| `GravitySystem` | Client + Server | 중력 가속도 적용 |
| `PlayerMoveSystem` | Client + Server | 입력 기반 이동 + 픽셀 3방향 충돌 해소 |

이동 시스템이 예측 그룹에 속하므로 클라이언트는 서버 응답 전에 로컬에서 먼저 움직임을 계산하여 체감 반응 속도를 확보합니다. 지형 변경은 서버 권위로만 처리하여 예측 오차를 최소화합니다.

### 방(Room) 관리 시스템

```
1인 접속 중
  └─▶ RoomWatchSystem: Phase = Solo

2번째 플레이어 접속
  └─▶ RoomResetRequestTag 생성 → Phase = Countdown
        └─▶ RoomResetSystem: 맵 초기화 + 플레이어 리셋 + RoomResetRpc 브로드캐스트
              └─▶ MatchCountdownSystem: 3·2·1·0(GO) RPC 브로드캐스트
                    └─▶ Phase = Battle → ArenaMatchSystem 활성화
```

| RPC | 방향 | 설명 |
|---|---|---|
| `PixelGridDeltaRpc` | Server → Client | 청크 diff 동기화 |
| `ArenaMatchTickRpc` | Server → Client | 매치 타이머·점수 주기 갱신 |
| `ArenaMatchEndRpc` | Server → Client | 매치 결과 전송 |
| `RoomResetRpc` | Server → All | 방 리셋 완료 알림 |
| `MatchCountdownRpc` | Server → All | 카운트다운 숫자 (3·2·1·0=GO) |
| `TerrainPaintRpc` | Client → Server | 마우스 지형 페인트 요청 |

---

## Ghost 컴포넌트 동기화

| 컴포넌트 | 전송 방식 | 설명 |
|---|---|---|
| `PlayerGhostData` | All (Snapshot) | 위치, 속도, 체력, 팀, 부활 횟수 |
| `PlayerInput` | Owner → Server | 클라이언트 입력 (GhostInput) |
| `PlayerMovementData` | All | 현재 이동 상태 + IsGrounded |
| `ManaComponent` | All | 현재/최대 마나 — HUD 표시용 |
| `BulletGhostData` | All | 투사체 위치·속도·소유자 |
| `RigidBodyGhostData` | All | 강체 위치·파괴 상태 |

---

## 구현 이력 (타임라인)

| 날짜 | 마일스톤 |
|---|---|
| Phase 1 | CA 엔진 ECS 전환 — PixelGridSingleton, Burst Job 5종, ChunkManager 슬리핑 |
| Phase 1 | 픽셀 그리드 RPC 델타 동기화 구현 |
| Phase 1 | PlayerMoveSystem 픽셀 3방향 충돌 완성 |
| Phase 2 | 완드 시스템 — WandCast / SpellProjectile / LiquidSpray / ManaRegen |
| Phase 3 | 픽셀 물질 확장 — Dirt, Wood, Gunpowder, Poison + 반응 매트릭스 |
| Phase 4 | 아레나 맵 — MapTemplate ScriptableObject, ArenaMapBuilder, 에디터 도구 |
| Phase 5 | 게임 모드 — 타이머, 용암 상승, 승패 판정, 2v2 팀 부활, IMGUI HUD |
| 2026-04-25 | NfE 통합 (PlayerLocalState, Coyote Time, Dash, GroundCheck Unity.Physics 제거) |
| 2026-04-27 | 클라이언트 예측 활성화, MPPM 패키지, 이동 동기화 최적화 |
| 2026-04-27 | 투사체 이펙트 시스템, SPUM 랜덤 외형, 아레나 매치 시스템 |
| 2026-04-30 | 방 관리 시스템 (RoomWatch/Reset/Countdown), 시차 스크롤 배경 |
| 2026-05-05 | 매몰 승리 규칙 (기절·근접공격·삽·BurialCheck) |

---

## 코드 규모

| 분류 | 파일 수 |
|---|---|
| ECS 픽셀 엔진 (Singletons / Jobs / Systems) | 15 |
| 게임플레이 시스템 (Movement / Combat / BattleRoyale) | 20+ |
| 컴포넌트 (Ghost / Local / RPC / Wand) | 20+ |
| Arena / Map / Bootstrap | 10 |
| 렌더링 / 프레젠테이션 | 8 |
| 에디터 / 런타임 유틸 | 6 |
| **총계** | **~80+** |

---

## 기술적 도전 및 해결

### 1. CA 시뮬레이션 비결정성 제거
매 틱 X 순회 순서를 셔플하여 모래가 한쪽으로 쏠리는 artifact를 제거하고, 업데이트 순서를 `CellUpdateTick` 버퍼로 관리하여 동일 틱 이중 처리를 방지했습니다.

### 2. 픽셀 그리드 대역폭 관리
활성 청크만 diff 수집 → `FixedBytes4096` blittable RPC로 전송하는 구조로 NetCode 직렬화 호환성과 대역폭 절감을 동시에 달성했습니다.

### 3. Unity.Physics 의존성 제거
`GroundCheckSystem`에서 Unity.Physics를 제거하고 `PixelGridSingleton`을 직접 읽는 방식으로 전환하여, ECS Job 내에서 Burst 컴파일이 가능해졌습니다.

### 4. 강체→픽셀 전환 순간 처리
`RigidBodyGhostData.IsDestroyed` Ghost 필드를 서버→클라이언트로 동기화한 뒤, `TerrainSyncSystem`이 `PendingPixelConversionTag`를 붙이고 `RigidToPixelConvertSystem`이 래스터화를 수행하는 파이프라인으로 전환 순간의 일관성을 확보했습니다.

### 5. AddForce 배치 처리
CA 시뮬레이션 중 픽셀마다 `AddForce`를 호출하는 대신 `ForceAccumulator`에 누적 후 틱 끝에 일괄 전달하여 프레임당 수천 번의 물리 호출을 방지했습니다.

---

## 향후 계획

| 우선도 | 항목 |
|---|---|
| 높음 | 완드 Bounce·Homing 수정자 구현 |
| 높음 | 사운드 시스템 (셀 타입별 충돌음·완드 발사음) |
| 중간 | 새로운 셀 타입 (얼음, 산성 액체, 전기) |
| 중간 | HUD UI Toolkit 전환 (현재 IMGUI) |
| 중간 | Unity Lobby/Relay 매치메이킹 |
| 낮음 | WebGL 빌드 최적화 |
| 낮음 | 리플레이 시스템 |

---

> 관련 문서: `Assets/Documents/CodeOverview.md` · `Assets/Documents/Netcode/README.md` · `Assets/Documents/Plan/sandblast_engine_design.md`
