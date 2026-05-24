# SandBlast 픽셀 물리 + NfE 넷코드 통합 계획 (재검토)

> 최초 작성: 2026-04-24 / 재검토: 2026-04-24  
> **ECS_Migration_Plan.md 반영 완료**

---

## 재검토 요약 — 기존 계획의 수정 사항

`ECS_Migration_Plan.md`는 `SandBlastEngine.cs`를 포함한 MonoBehaviour 레이어 전체를 ECS로 이전한다.  
이로 인해 기존 `IntegrationPlan.md`의 전제 4곳이 무효화된다.

| 기존 결정 | 무효화 이유 | 변경 내용 |
|-----------|-------------|-----------|
| `SandBlastEngine.Instance` 정적 참조 | ECS Migration이 `SandBlastEngine.cs` 삭제 | `PixelGridSingleton` 직접 읽기로 대체 |
| `GroundCheckSystem`에서 Burst 포기 | `PixelGridSingleton`은 `NativeArray` → Burst 호환 | `[BurstCompile]` 복원 |
| RigidBodyNode Hybrid 유지 | ECS Migration Phase 3이 ECS Entity로 전환 | `RigidBodyNodeComponent` 직접 활용 |
| `PixelRenderSyncSystem` 별도 추가 | ECS Migration Phase 5의 `PixelRenderSystem`과 동일 | 중복 제거, 하나로 병합 |

---

## 두 계획의 의존 관계

ECS Migration이 완료되어야 NfE 통합이 제대로 동작한다.  
두 작업을 **동시에 진행할 수 없는 단계**와 **병렬로 진행할 수 있는 단계**를 구분한다.

```
[독립]  NfE-A: PlayerController/Camera 제거
           │
           ▼
[선행]  ECS-1: PixelGridSingleton 데이터 이전 ◄─ 모든 것의 전제
           │
     ┌─────┼──────────────┐
     ▼     ▼              ▼
[병렬] ECS-2  NfE-B        ECS-3
   CA 시스템  GroundCheck  RigidBody ECS
   Burst Job  (Burst 복원)
     │                    │
     └──────┬─────────────┘
            ▼
       NfE-C: 투사체↔픽셀
            │
     ┌──────┴──────┐
     ▼             ▼
  ECS-4         NfE-D
SolidMaskSync  강체 파괴 동기화
     │             │
     └──────┬───────┘
            ▼
       NfE-E: PixelGrid 서버 동기화
            │
            ▼
      ECS-5+NfE-F: 렌더링 (병합)
            │
            ▼
       ECS-6: 정리 및 삭제
```

---

## 전체 통합 Phase 목록

---

### Phase NfE-A — 이동 시스템 충돌 해소 (독립, 선행 가능)

**ECS Migration과 무관하게 먼저 처리 가능.**

**제거 대상:** `PlayerController.cs`, `PlayerCamera.cs`

#### 1. PlayerController → NfE 이식

`PlayerController`에만 있고 NfE에 없는 기능 3가지를 이식한 뒤 제거한다.

| 기능 | PlayerController | 이식 위치 |
|------|-----------------|-----------|
| 코요테 타임 (0.12s) | `coyoteTimer` | `PlayerLocalState` 컴포넌트 (비동기화) |
| 점프 버퍼 (0.1s) | `jumpBufferTimer` | `PlayerLocalState` 컴포넌트 (비동기화) |
| 대시 | `dashTimer`, `isDashing` | `PlayerInput`에 `Dash: InputEvent` 추가 |

`PlayerLocalState`를 `[GhostField]` 없이 별도 컴포넌트로 분리하는 이유: 예측 롤백 시 타이머도 복원되어야 하는데, Ghost 동기화 대상이 되면 서버가 덮어써 버린다.

#### 2. PlayerCamera → CameraFollowSystem 업그레이드

`PlayerCamera`의 `ClampToBounds`를 이식한다.  
ECS Migration 완료 전: `SimConfigSingleton`의 `GridWidth/Height`, `PixelsPerUnit`을 읽는다.  
ECS Migration 완료 후: `PixelGridSingleton`에서 직접 읽는다.

---

### Phase ECS-1 — 그리드 데이터 ECS 싱글턴화 (모든 Phase의 전제)

**`ECS_Migration_Plan.md` Phase 1 그대로 실행.**

`PixelGrid`, `ChunkManager`(plain C# class) → `NativeArray<byte>` Singleton Entity 이전.

핵심 결과물:
- `PixelGridSingleton` — 5채널 NativeArray, `SystemAPI.GetSingleton<>()`으로 모든 시스템에서 접근
- `ChunkManagerSingleton` — `NativeArray<ChunkMeta>`
- `SimConfigSingleton` — `XShuffle`, `CellUpdateTick`, `CurrentTick`
- `SandBlastBootstrapSystem` — `OnCreate`에서 Persistent 할당, `OnDestroy`에서 Dispose

**이 Phase 완료 후 `SandBlastEngine.Instance` 정적 참조 전략은 폐기된다.**  
`SandBlastEngine.cs`는 이 시점에 껍데기가 되므로 Phase ECS-6에서 삭제.

---

### Phase NfE-B — 지면 감지 통합 (ECS-1 완료 후)

**기존 IntegrationPlan의 Phase 2에서 핵심 전략 변경.**

#### 변경 전 (기존)
```csharp
// Burst 불가, SandBlastEngine.Instance 의존
SandBlastEngine.Instance.Grid.Get(gx, gy).IsSolid()
```

#### 변경 후 (ECS_Migration_Plan 반영)
```csharp
// Burst 가능, NativeArray 직접 접근
[BurstCompile]
public partial struct GroundCheckSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var grid = SystemAPI.GetSingleton<PixelGridSingleton>();  // NativeArray
        foreach (var (transform, groundedState, ghostData) in ...)
        {
            // 발 아래 5점 샘플 — NativeArray 직접 인덱싱
            bool grounded = false;
            for (int i = -2; i <= 2; i++)
            {
                int gx = WorldToGridX(transform.Position.x + i * 0.1f, grid);
                int gy = WorldToGridY(transform.Position.y - halfHeight, grid);
                if (grid.InBounds(gx, gy) &&
                    ((CellType)grid.Type[grid.Index(gx, gy)]).IsSolid())
                { grounded = true; break; }
            }
            groundedState.ValueRW.IsGrounded = grounded;
            ghostData.ValueRW.IsGrounded = grounded;
        }
    }
}
```

`using Unity.Physics` 의존성 제거. `[BurstCompile]` 복원.

---

### Phase ECS-2 — CA 시뮬레이션 Systems (ECS-1 완료 후, NfE-C와 병렬)

**`ECS_Migration_Plan.md` Phase 2 그대로 실행.**

`PixelSimulator.cs` → 5개 Burst `IJob`으로 분리.

```
SimulationSystemGroup 실행 순서:
  SolidMaskSyncSystem          (ECS-4 이후 배치)
  PixelTickSchedulerSystem
  PixelFireSystem    → FireTickJob (IJob, BurstCompile)
  PixelSmokeSystem   → SmokeTickJob
  PixelLiquidSystem  → LiquidTickJob
  PixelPowderSystem  → PowderTickJob
  PixelDebrisSystem  → DebrisTickJob
```

`System.Random` → `Unity.Mathematics.Random` 전환 필수 (Burst 호환).

> **NfE 통합과의 관계:** CA 시스템이 ECS로 오면 `BulletTerrainHitSystem`(Phase NfE-C)이 동일한 `PixelGridSingleton`에 직접 쓸 수 있게 된다. 단, CA Job과 NfE 시스템이 같은 NativeArray에 동시 접근하지 않도록 `state.Dependency` 체인 관리가 필요하다.

---

### Phase ECS-3 — RigidBodyNode → ECS Entity (ECS-1 완료 후)

**`ECS_Migration_Plan.md` Phase 3 그대로 실행.**

`RigidBodyNode.cs`(MonoBehaviour) → `RigidBodyNodeComponent` + `DynamicBuffer<EdgeElement>`.

핵심 결과물:
- `RigidBodyNodeAuthoring` — Baker
- `RigidBodyNodeComponent` — `NodeId`
- `EdgeElement` (IBufferElementData) — `PeerNodeId`, `EdgeState`, `GapWidth`
- `PolygonVertex` (IBufferElementData) — 래스터화용 로컬 정점
- `RigidBodyGraphSystem` — 엣지 파괴 감지, `IsIsolated` 태그 추가
- `RigidToPixelConvertSystem` — `IsIsolated` 엔티티를 픽셀로 전환, Entity 삭제

**이 Phase 완료 후 Phase NfE-D의 Hybrid 전략이 불필요해진다.**

---

### Phase NfE-C — 투사체↔픽셀 상호작용 (ECS-1 완료 후)

**기존 IntegrationPlan의 Phase 3에서 호출 방식 변경.**

#### 변경 전 (기존)
```csharp
SandBlastEngine.Instance.BulletImpact(worldPos, radius, force);
```

#### 변경 후 (ECS_Migration_Plan 반영)
`SandBlastEngine` 없이 `PixelGridSingleton`에 직접 쓴다.

```
추가할 시스템: BulletTerrainHitSystem (ServerSimulation)
    UpdateBefore(PixelTickSchedulerSystem)  ← CA 틱 전에 지형 변경 완료

    매 틱:
      투사체 위치 → PixelGridSingleton.Type[index]
      IsSolid() == true →
          반경 내 POWDER/LIQUID 셀: EMPTY로 제거
          SOLID_STATIC 인접 셀: FIRE 생성 (발화)
          ChunkManagerSingleton.MarkMoved() 호출
          ECB.DestroyEntity(bulletEntity)
```

`BulletImpact` / `Explode` API는 `SandBlastEngine` 대신 `PixelImpactUtility` 정적 Burst 함수로 구현.

```csharp
// ECS/Utils/PixelImpactUtility.cs
[BurstCompile]
public static class PixelImpactUtility
{
    public static void BulletImpact(
        ref PixelGridSingleton grid, ref ChunkManagerSingleton chunks,
        float2 worldPos, float radius)
    { ... }

    public static void Explode(
        ref PixelGridSingleton grid, ref ChunkManagerSingleton chunks,
        float2 worldPos, float radius, float force)
    { ... }
}
```

---

### Phase ECS-4 — SolidMaskSyncSystem (ECS-3 완료 후)

**`ECS_Migration_Plan.md` Phase 4 그대로 실행.**

`SolidMaskSyncManager.cs` → `SolidMaskSyncSystem` (ECS Transform 읽어 XOR diff).

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PixelTickSchedulerSystem))]
public partial struct SolidMaskSyncSystem : ISystem
{
    // RigidBodyNodeComponent + LocalToWorld + PolygonVertex 버퍼 읽기
    // → 월드 좌표 폴리곤 계산 → RasterizePoly
    // → prev 마스크와 XOR diff → CoverCell / UncoverCell
    // prevMasks: NativeParallelHashMap<int, NativeList<int>> (Persistent)
}
```

---

### Phase NfE-D — 강체 파괴 서버 권위 (ECS-3 완료 후)

**기존 IntegrationPlan Phase 4의 Hybrid 전략 → 순수 ECS 전략으로 변경.**

ECS-3 완료 시 `RigidBodyNode`는 이미 ECS Entity다.  
`TerrainDamageSystem`이 `EdgeElement` 버퍼를 직접 수정한다.

```
추가할 컴포넌트: RigidBodyGhostData (IComponentData)
    [GhostField] public int  NodeId
    [GhostField] public bool IsDestroyed
    (PlayerAuthoring의 Baker에서 RigidBodyNode에 추가)

추가할 시스템: TerrainDamageSystem (ServerSimulation)
    투사체 충돌 시 HitEvent → EdgeElement.TakeDamage()
    AllEdgesBroken → RigidBodyGhostData.IsDestroyed = true

TerrainSyncSystem (ClientAndServer)
    IsDestroyed 감지 → RigidToPixelConvertSystem에 위임
    (RigidToPixelConvertSystem은 ECS-3에서 이미 구현됨)
```

기존 Hybrid 브릿지(`SandBlastEngine.Instance.ConvertRigidToPixel()`)는 사용하지 않는다.

---

### Phase NfE-E — PixelGrid 서버 동기화 (ECS-2, ECS-4 완료 후)

ECS 완료 후 이 Phase가 크게 단순해진다.

`PixelGridSingleton.Type`은 `NativeArray<byte>`이므로 diff 계산과 직렬화가 간단하다.

```
추가할 컴포넌트: PixelChunkDiff (IBufferElementData)
    public int2  ChunkCoord
    public NativeArray<byte> CellTypes  ← 청크 스냅샷 (32×32 = 1024 bytes)

추가할 시스템: PixelGridSyncSystem (ServerSimulation)
    ChunkManagerSingleton.Metas 순회
    State != SLEEPING인 청크만 → PixelChunkDiff 버퍼에 추가
    NfE RPC로 클라이언트에 전송 (5틱마다 1회)

초기 접속:
    전체 PixelGridSingleton 스냅샷 1회 전송 (압축 적용)
```

---

### Phase ECS-5 + NfE-F — 렌더링 (병합)

기존 IntegrationPlan의 `PixelRenderSyncSystem`과 ECS Migration의 `PixelRenderSystem`은 **동일한 시스템**이다.  
`ECS_Migration_Plan.md`의 `PixelRenderSystem`을 채택하고 기존 안을 폐기한다.

```csharp
// ECS/Systems/Rendering/PixelRenderSystem.cs
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(CameraFollowSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class PixelRenderSystem : SystemBase
{
    PixelLayerRenderer _renderer;

    protected override void OnCreate()
        => _renderer = Object.FindFirstObjectByType<PixelLayerRenderer>();

    protected override void OnUpdate()
    {
        var grid = SystemAPI.GetSingleton<PixelGridSingleton>();
        _renderer?.UpdateTexture(grid.Type, grid.Lifetime, grid.Width, grid.Height);
    }
}
```

`PixelLayerRenderer.UpdateTexture()` 시그니처를 `(NativeArray<byte> type, NativeArray<byte> lifetime, int w, int h)`로 수정.  
`SandBlastEngine.Update()`에서의 `UpdateTexture()` 호출은 ECS-6에서 파일 삭제 시 함께 사라진다.

---

### Phase ECS-6 — 정리 및 삭제

**`ECS_Migration_Plan.md` Phase 6 그대로 실행.**

| 삭제 파일 | 대체 |
|-----------|------|
| `SandBlastEngine.cs` | `SandBlastBootstrapAuthoring` + `SandBlastBootstrapSystem` |
| `Simulation/PixelSimulator.cs` | `PixelFire/Smoke/Liquid/Powder/DebrisSystem` |
| `Core/PixelGrid.cs` | `PixelGridSingleton` |
| `Chunk/ChunkManager.cs` | `ChunkManagerSingleton` |
| `Physics/ForceAccumulator.cs` | ECS Physics `PhysicsVelocity` 직접 쓰기 |
| `RigidBody/RigidBodyNode.cs` | `RigidBodyNodeAuthoring` + `RigidBodyNodeComponent` |
| `RigidBody/RigidBodyGraph.cs` | `RigidBodyGraphSystem` |
| `Sync/SolidMaskSyncManager.cs` | `SolidMaskSyncSystem` |
| `Player/PlayerController.cs` | NfE 이동 시스템 (Phase NfE-A) |
| `Player/PlayerCamera.cs` | `CameraFollowSystem` (Phase NfE-A) |

---

## 전체 통합 로드맵

```
┌─ NfE-A ─────────────────────────────── 독립, 가장 먼저 처리
│   PlayerController / PlayerCamera 제거
│   PlayerLocalState, Dash 이식
│
└─ ECS-1 ─────────────────────────────── 모든 것의 전제
    PixelGridSingleton · ChunkManagerSingleton · SimConfigSingleton
    SandBlastBootstrapSystem
         │
    ┌────┴──────────────────────┐
    │                           │
    ECS-2 (CA Burst Jobs)       NfE-B (GroundCheck, Burst 복원)
    PixelFire/Smoke/...         PixelGridSingleton 직접 읽기
    │                           │
    │           ECS-3 ──────────┘
    │      RigidBodyNode → ECS Entity
    │           │
    │      ┌────┴────────────┐
    │    ECS-4           NfE-D
    │  SolidMaskSync     TerrainDamage (순수 ECS)
    │    │                   │
    │    └────────┬───────────┘
    │             │
    NfE-C ────────┤
   투사체↔픽셀    │
   PixelImpact    │
         │        │
         └────────┘
              │
           NfE-E
         PixelGrid 서버 동기화
         ChunkDiff RPC
              │
         ECS-5+NfE-F
          PixelRenderSystem
         (두 계획 병합)
              │
           ECS-6
           파일 삭제 · 씬 마이그레이션
```

---

## 재검토 후 핵심 설계 결정

### 결정 1: ECS Migration이 선행되어야 한다

~~SandBlastEngine은 MonoBehaviour로 유지한다~~ (기존 결정 폐기)

`ECS_Migration_Plan.md`의 Phase 1(데이터 이전)이 완료되면 NfE 통합의 모든 문제가 훨씬 단순해진다.  
`SandBlastEngine.Instance` 정적 참조, Hybrid 브릿지, Burst 포기 등 임시방편이 모두 사라진다.  
따라서 **ECS-1을 가장 먼저** 실행하고 이후 NfE 통합을 진행한다.

### 결정 2: PixelGridSingleton이 물리 권위 소스다

Unity.Physics 콜라이더 대신 `PixelGridSingleton.Type` NativeArray가 지면/충돌의 진실된 소스다.  
`NativeArray`이므로 `[BurstCompile]` Job 내에서 직접 읽을 수 있다. Burst 포기 없이 일관성 확보.

### 결정 3: 투사체→픽셀은 `PixelImpactUtility` Burst 함수로 구현

`SandBlastEngine.BulletImpact()` MonoBehaviour 메서드 대신,  
`PixelImpactUtility` 정적 Burst 함수로 구현해 `BulletTerrainHitSystem`에서 직접 호출한다.  
서버 전용으로 실행해 클라이언트에는 결과(Ghost 스냅샷 + 청크 diff)만 전달한다.

### 결정 4: 강체 파괴는 순수 ECS로

~~RigidBodyNode Hybrid(MonoBehaviour 유지) 브릿지~~ (기존 결정 폐기)

ECS-3 완료 시 `RigidBodyNodeComponent` + `EdgeElement` 버퍼가 존재하므로,  
`TerrainDamageSystem`이 서버에서 엣지 피해를 계산하고 `RigidBodyGhostData.IsDestroyed`를 설정한다.  
클라이언트는 Ghost 동기화로 파괴 시점을 수신하고 `RigidToPixelConvertSystem`이 픽셀 전환을 처리한다.

---

## 참조 문서

| 문서 | 역할 |
|------|------|
| [`ECS_Migration_Plan.md`](ECS_Migration_Plan.md) | 픽셀 시뮬레이터 ECS 전면 이전 상세 계획 |
| [`Netcode/README.md`](Netcode/README.md) | NfE 넷코드 시스템 인덱스 |
| [`BattleRoyale_NfE_SystemDesign.md`](BattleRoyale_NfE_SystemDesign.md) | 배틀로얄 넷코드 원본 설계 문서 |
