# SandBlast → ECS 전면 이전 계획

> 작성일: 2026-04-24  
> 대상 엔진: Unity 6 (6000.3.10f1) · DOTS (Entities, Jobs, Burst, Mathematics)

---

## 설계 원칙

| 항목 | 결정 |
|------|------|
| CA 병렬화 | Burst `IJob` 단일스레드 (Phase 2) → 청크 체커보드 `IJobParallelFor` (2차 최적화) |
| 그리드 데이터 | `NativeArray<byte>` 5채널, Singleton Entity에 보관 |
| 렌더링 | `PixelLayerRenderer` MonoBehaviour 유지 (Texture2D GPU 업로드는 메인스레드 전용) |
| RigidBody | `RigidBodyNode` MonoBehaviour → ECS Entity + `DynamicBuffer<EdgeElement>` |
| 진입점 | `SandBlastEngine.cs` 제거 → `SandBlastBootstrapAuthoring` (Baker가 Singleton 생성) |

---

## 새 파일 구조

```
Assets/Scripts/
├── ECS/
│   ├── Singletons/
│   │   ├── PixelGridSingleton.cs        ← PixelGrid 대체
│   │   ├── ChunkManagerSingleton.cs     ← ChunkManager 대체
│   │   └── SimConfigSingleton.cs        ← TicksPerSecond, xShuffle, currentTick 등
│   ├── Components/
│   │   ├── RigidBodyNodeComponent.cs    ← NodeId
│   │   ├── EdgeBuffer.cs                ← DynamicBuffer<EdgeElement>
│   │   └── PolygonVertexBuffer.cs       ← DynamicBuffer<PolygonVertex> (SolidMask 래스터화용)
│   ├── Systems/
│   │   ├── Bootstrap/
│   │   │   └── SandBlastBootstrapSystem.cs   ← NativeArray 할당 · 해제
│   │   ├── Sync/
│   │   │   └── SolidMaskSyncSystem.cs        ← SolidMaskSyncManager 대체
│   │   ├── Simulation/
│   │   │   ├── PixelTickSchedulerSystem.cs   ← 틱 타이머, 최대 3틱/프레임
│   │   │   ├── PixelFireSystem.cs
│   │   │   ├── PixelSmokeSystem.cs
│   │   │   ├── PixelLiquidSystem.cs
│   │   │   ├── PixelPowderSystem.cs
│   │   │   └── PixelDebrisSystem.cs
│   │   ├── RigidBody/
│   │   │   ├── RigidBodyGraphSystem.cs       ← RigidBodyGraph 대체
│   │   │   └── RigidToPixelConvertSystem.cs  ← ConvertRigidToPixel 로직
│   │   └── Rendering/
│   │       └── PixelRenderSystem.cs          ← PixelLayerRenderer.UpdateTexture() 드라이버
│   └── Authoring/
│       ├── SandBlastBootstrapAuthoring.cs    ← SandBlastEngine 대체
│       └── RigidBodyNodeAuthoring.cs         ← RigidBodyNode MonoBehaviour 대체
├── Core/          (변경 없음 — CellType, SimulationConstants, GridUtils)
├── Rendering/     (PixelLayerRenderer MonoBehaviour 유지, 시그니처 일부 수정)
│
│   ── 이전 완료 후 삭제 예정 ──
├── SandBlastEngine.cs
├── Simulation/PixelSimulator.cs
├── Chunk/ChunkManager.cs
├── Core/PixelGrid.cs
├── Physics/ForceAccumulator.cs
├── RigidBody/RigidBodyNode.cs
├── RigidBody/RigidBodyGraph.cs
└── Sync/SolidMaskSyncManager.cs
```

---

## Phase 1 — 그리드 데이터 ECS 싱글턴화

**제거 대상:** `PixelGrid`, `ChunkManager` (plain C# class)  
**목표:** `NativeArray<byte>` 5채널을 Singleton IComponentData로 이전

### PixelGridSingleton

```csharp
// ECS/Singletons/PixelGridSingleton.cs
public struct PixelGridSingleton : IComponentData, IDisposable
{
    public int    Width, Height;
    public NativeArray<byte> Type;
    public NativeArray<byte> Temperature;
    public NativeArray<byte> Lifetime;
    public NativeArray<byte> RigidId;
    public NativeArray<byte> Flammability;
    public float  PixelsPerUnit;
    public float2 GridOrigin;

    public int  Index(int x, int y) => y * Width + x;
    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public void Dispose()
    {
        Type.Dispose(); Temperature.Dispose();
        Lifetime.Dispose(); RigidId.Dispose(); Flammability.Dispose();
    }
}
```

### ChunkManagerSingleton

```csharp
// ECS/Singletons/ChunkManagerSingleton.cs
// ChunkMeta는 이미 blittable struct이므로 그대로 사용
public struct ChunkManagerSingleton : IComponentData, IDisposable
{
    public int ChunksX, ChunksY;
    public NativeArray<ChunkMeta> Metas;
    public void Dispose() => Metas.Dispose();
}
```

### SimConfigSingleton

```csharp
// ECS/Singletons/SimConfigSingleton.cs
public struct SimConfigSingleton : IComponentData, IDisposable
{
    public float            TicksPerSecond;
    public float            TickTimer;
    public int              CurrentTick;
    public NativeArray<int> XShuffle;        // Fisher-Yates용 셔플 버퍼
    public NativeArray<int> CellUpdateTick;  // 이중 처리 방지 카운터
    public void Dispose() { XShuffle.Dispose(); CellUpdateTick.Dispose(); }
}
```

### SandBlastBootstrapAuthoring

```csharp
// ECS/Authoring/SandBlastBootstrapAuthoring.cs
public class SandBlastBootstrapAuthoring : MonoBehaviour
{
    public int     GridWidth  = 320;
    public int     GridHeight = 180;
    public float   PixelsPerUnit  = 16f;
    public float   TicksPerSecond = 30f;
    public Vector2 GridOrigin;
}
// Baker는 config 값만 컴포넌트로 기록.
// NativeArray 실제 할당은 SandBlastBootstrapSystem.OnCreate()에서 수행.
```

### SandBlastBootstrapSystem

```csharp
// ECS/Systems/Bootstrap/SandBlastBootstrapSystem.cs
[CreateAfter(typeof(BeginSimulationEntityCommandBufferSystem))]
public partial struct SandBlastBootstrapSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // config entity에서 width/height 읽은 뒤 NativeArray 할당
        int n = width * height;
        state.EntityManager.CreateSingleton(new PixelGridSingleton {
            Width = width, Height = height,
            Type         = new NativeArray<byte>(n, Allocator.Persistent),
            Temperature  = new NativeArray<byte>(n, Allocator.Persistent),
            Lifetime     = new NativeArray<byte>(n, Allocator.Persistent),
            RigidId      = new NativeArray<byte>(n, Allocator.Persistent),
            Flammability = new NativeArray<byte>(n, Allocator.Persistent),
        });
        state.EntityManager.CreateSingleton(new ChunkManagerSingleton { ... });
        state.EntityManager.CreateSingleton(new SimConfigSingleton { ... });
    }

    public void OnDestroy(ref SystemState state)
    {
        SystemAPI.GetSingleton<PixelGridSingleton>().Dispose();
        SystemAPI.GetSingleton<ChunkManagerSingleton>().Dispose();
        SystemAPI.GetSingleton<SimConfigSingleton>().Dispose();
    }
}
```

---

## Phase 2 — CA 시뮬레이션 Systems + Burst Jobs

**제거 대상:** `PixelSimulator.cs`  
**목표:** 5개 시뮬레이션 단계 → 각각 `ISystem` + Burst `IJob`

### 실행 순서 보장

```
SimulationSystemGroup
  └─ SolidMaskSyncSystem          (강체 마스크 먼저)
  └─ PixelTickSchedulerSystem     (틱 카운트 계산)
  └─ PixelFireSystem
  └─ PixelSmokeSystem    [UpdateAfter(PixelFireSystem)]
  └─ PixelLiquidSystem   [UpdateAfter(PixelSmokeSystem)]
  └─ PixelPowderSystem   [UpdateAfter(PixelLiquidSystem)]
  └─ PixelDebrisSystem   [UpdateAfter(PixelPowderSystem)]
```

### PixelTickSchedulerSystem

```csharp
// 기존 SandBlastEngine.Update() 타이머 로직 이전
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct PixelTickSchedulerSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        ref var cfg = ref SystemAPI.GetSingletonRW<SimConfigSingleton>().ValueRW;
        float interval = 1f / cfg.TicksPerSecond;
        cfg.TickTimer += SystemAPI.Time.DeltaTime;

        int ticks = math.min(3, (int)(cfg.TickTimer / interval));
        cfg.TickTimer -= ticks * interval;
        cfg.CurrentTick += ticks;
        if (cfg.CurrentTick <= 0) { /* 오버플로우 방지 */ cfg.CurrentTick = 1; }

        ShuffleX(ref cfg);  // Fisher-Yates
    }
}
```

### Burst Job 패턴 (Fire 예시)

```csharp
// ECS/Systems/Simulation/PixelFireSystem.cs
[BurstCompile]
public struct FireTickJob : IJob
{
    public NativeArray<byte> Type, Temperature, Lifetime, Flammability;
    public NativeArray<int>  CellUpdateTick;
    public NativeArray<ChunkMeta> ChunkMetas;
    public int   Width, Height, CurrentTick, ChunksX;
    public Unity.Mathematics.Random Rng;

    public void Execute()
    {
        // PixelSimulator.SimulateFire() 로직 그대로 이식
        // System.Random → Unity.Mathematics.Random
        // Span<int> stackalloc → int4 (Burst 호환)
    }
}

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(PixelTickSchedulerSystem))]
public partial struct PixelFireSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var grid  = SystemAPI.GetSingleton<PixelGridSingleton>();
        var chunk = SystemAPI.GetSingleton<ChunkManagerSingleton>();
        var cfg   = SystemAPI.GetSingleton<SimConfigSingleton>();

        state.Dependency = new FireTickJob {
            Type = grid.Type, Temperature = grid.Temperature,
            Lifetime = grid.Lifetime, Flammability = grid.Flammability,
            CellUpdateTick = cfg.CellUpdateTick,
            ChunkMetas = chunk.Metas,
            Width = grid.Width, Height = grid.Height,
            CurrentTick = cfg.CurrentTick,
            ChunksX = chunk.ChunksX,
            Rng = new Unity.Mathematics.Random((uint)cfg.CurrentTick ^ 0xDEADBEEF),
        }.Schedule(state.Dependency);
    }
}
```

> **주의:** 각 단계 Job은 이전 Job이 완료된 후 순차 실행되어야 합니다.  
> `[UpdateAfter]` 체인 + `state.Dependency` 전달로 JobHandle 의존성을 연결합니다.

### Random 대체

| 기존 | 대체 |
|------|------|
| `System.Random.NextDouble()` | `rng.NextFloat()` |
| `System.Random.Next(2)` | `rng.NextBool()` |
| `System.Random.Next(i+1)` | `rng.NextInt(0, i+1)` |

---

## Phase 3 — RigidBodyNode → ECS Entity

**제거 대상:** `RigidBodyNode.cs` (MonoBehaviour), `RigidBodyGraph.cs`  
**목표:** Entity + DynamicBuffer 기반 그래프

### 새 컴포넌트

```csharp
// ECS/Components/RigidBodyNodeComponent.cs
public struct RigidBodyNodeComponent : IComponentData
{
    public int NodeId;
}

// ECS/Components/EdgeBuffer.cs
public struct EdgeElement : IBufferElementData
{
    public int       PeerNodeId;
    public EdgeState State;    // INTACT / BROKEN (기존 EdgeState enum 재사용)
    public float     GapWidth;
}

// ECS/Components/PolygonVertexBuffer.cs
public struct PolygonVertex : IBufferElementData
{
    public float2 LocalPos;  // 로컬 공간 폴리곤 정점
}
```

### RigidBodyNodeAuthoring

```csharp
// ECS/Authoring/RigidBodyNodeAuthoring.cs
public class RigidBodyNodeAuthoring : MonoBehaviour
{
    public int   NodeId;
    public int[] ConnectedNodeIds;
}
// Baker → RigidBodyNodeComponent + EdgeElement 버퍼 + PolygonVertex 버퍼 추가
// PhysicsBodyAuthoring은 Unity Physics 패키지의 컴포넌트 그대로 활용
```

### RigidBodyGraphSystem

```csharp
// ECS/Systems/RigidBody/RigidBodyGraphSystem.cs
// BreakEdge: ECB로 EdgeElement.State = BROKEN 갱신
// CheckIsolation: 모든 엣지가 BROKEN이면 IsIsolated 태그 컴포넌트 추가
// → RigidToPixelConvertSystem이 IsIsolated 엔티티를 처리
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct RigidBodyGraphSystem : ISystem { ... }
```

### RigidToPixelConvertSystem

```csharp
// ECS/Systems/RigidBody/RigidToPixelConvertSystem.cs
// 기존 SandBlastEngine.ConvertRigidToPixel() 로직 이전
// LocalToWorld + PolygonVertex 버퍼 읽어 래스터화 → SOLID_DEBRIS 셀 기록
// 처리 후 Entity 삭제 (ECB.DestroyEntity)
```

---

## Phase 4 — SolidMaskSyncSystem

**제거 대상:** `SolidMaskSyncManager.cs`  
**목표:** ECS Transform 읽어 폴리곤 래스터화 → XOR diff → CoverCell/UncoverCell

```csharp
// ECS/Systems/Sync/SolidMaskSyncSystem.cs
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PixelTickSchedulerSystem))]
public partial struct SolidMaskSyncSystem : ISystem
{
    // prevMasks: NativeParallelHashMap<int, NativeList<int>> (nodeId → 이전 프레임 인덱스 목록)
    // OnCreate에서 Allocator.Persistent로 할당, OnDestroy에서 해제

    public void OnUpdate(ref SystemState state)
    {
        ref var grid = ref SystemAPI.GetSingletonRW<PixelGridSingleton>().ValueRW;
        var chunk    = SystemAPI.GetSingleton<ChunkManagerSingleton>();

        foreach (var (node, ltw, polyBuf) in
                 SystemAPI.Query<RefRO<RigidBodyNodeComponent>,
                                 RefRO<LocalToWorld>,
                                 DynamicBuffer<PolygonVertex>>())
        {
            // LocalToWorld + LocalPos → 월드 좌표 폴리곤 계산
            // RasterizePoly → curr 인덱스 집합
            // prev와 XOR diff → CoverCell / UncoverCell
        }
    }
}
```

---

## Phase 5 — 렌더링 드라이버

**목표:** ECS System이 `PixelLayerRenderer.UpdateTexture()`를 매 프레임 호출

`PixelLayerRenderer`는 **MonoBehaviour 유지** (Texture2D.SetPixels32 + Apply는 메인스레드 전용).  
`UpdateTexture(PixelGrid grid)` 시그니처를 `UpdateTexture(NativeArray<byte> type, NativeArray<byte> lifetime)`으로 변경.

```csharp
// ECS/Systems/Rendering/PixelRenderSystem.cs
// ISystem이 아닌 SystemBase 사용 (Managed 객체 참조 필요)
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class PixelRenderSystem : SystemBase
{
    PixelLayerRenderer _renderer;

    protected override void OnCreate()
    {
        _renderer = Object.FindFirstObjectByType<PixelLayerRenderer>();
    }

    protected override void OnUpdate()
    {
        var grid = SystemAPI.GetSingleton<PixelGridSingleton>();
        _renderer?.UpdateTexture(grid.Type, grid.Lifetime, grid.Width, grid.Height);
    }
}
```

---

## Phase 6 — 정리 및 삭제

Phase 1~5 완료 후 아래 파일 삭제:

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

**유지 파일:**

| 파일 | 이유 |
|------|------|
| `Core/CellType.cs` | enum, 변경 없음 |
| `Core/SimulationConstants.cs` | 상수 집합, 변경 없음 |
| `Core/GridUtils.cs` | 폴리곤 래스터화 유틸, 변경 없음 |
| `Chunk/ChunkMeta.cs` | blittable struct, 변경 없음 |
| `RigidBody/Edge.cs`, `EdgeState.cs` | blittable, 변경 없음 |
| `Rendering/PixelLayerRenderer.cs` | MonoBehaviour 유지 (시그니처만 수정) |

---

## 단계별 작업 순서 요약

```
Phase 1  (기반)    PixelGridSingleton · ChunkManagerSingleton · SimConfigSingleton · BootstrapSystem
Phase 2  (시뮬)    PixelTickSchedulerSystem + Fire/Smoke/Liquid/Powder/Debris IJob
Phase 3  (강체)    RigidBodyNodeAuthoring · RigidBodyGraphSystem · RigidToPixelConvertSystem
Phase 4  (동기화)  SolidMaskSyncSystem (NativeArray 기반 XOR diff)
Phase 5  (렌더)    PixelRenderSystem + PixelLayerRenderer 시그니처 수정
Phase 6  (정리)    기존 파일 삭제, SubScene 씬 설정 마이그레이션
```

---

## 주요 주의사항

| 항목 | 내용 |
|------|------|
| **NativeArray 생명주기** | `OnCreate`에서 `Allocator.Persistent` 할당, `OnDestroy`에서 반드시 `Dispose()` |
| **CA 이중 쓰기 방지** | `cellUpdateTick` 배열도 `NativeArray<int>` Singleton으로 이전 |
| **Random** | `System.Random` → `Unity.Mathematics.Random` (Burst 호환, seed 전달 필요) |
| **CanLiquidPass 미구현** | `RigidId` 채널로 pixel→nodeId 매핑 가능 — Phase 3에서 함께 해결 |
| **ForceAccumulator** | ECS Physics 환경에서 `PhysicsVelocity` 컴포넌트 직접 수정으로 대체 |
| **씬 설정** | 기존 MonoBehaviour 오브젝트 → SubScene + Authoring 컴포넌트로 전환 |
| **체커보드 최적화** | Phase 2 완료 후 선택 사항 — 청크 단위로 홀짝 분리해 `IJobParallelFor` 전환 |
