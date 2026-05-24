# 동기화 성능 최적화 계획

**작성일:** 2026-04-27  
**대상:** Unity DOTS + NetCode 기반 픽셀 그리드 동기화 시스템  
**프레임워크:** Unity 6 (6000.3.10f1) / Entities 1.3.2 / NetCode 1.3.2

---

## 현황 진단

### 동기화 파이프라인 구조

```
[Server] PixelSimulationSystem (30 ticks/sec)
    ↓
[Server] PixelGridSyncSystem (매 5 tick = ~167ms)
    → 변경 청크 감지 (전체 청크 바이트 비교)
    ↓
[Server] PixelGridRpcSendSystem
    → 청크 수 × 접속자 수 만큼 RPC 엔티티 생성
    ↓
[Client] PixelGridRpcApplySystem
    → 수신 데이터를 로컬 픽셀 그리드에 적용
```

### 패킷 크기

| 작업 | 페이로드 | 주기 |
|------|---------|------|
| PixelGridDeltaRpc (청크 1개) | **1,041 B** | 167ms마다 |
| 초기 전체 맵 동기화 | **~262 KB** | 클라이언트 접속 시 |
| ArenaMatchRpc | ~32 B | 2초마다 |

---

## 성능 병목 목록

### 🔴 Critical

#### 1. N+1 쿼리 — SolidMaskSyncSystem
**파일:** `Assets/Scripts/ECS/Systems/Sync/SolidMaskSyncSystem.cs:196-204`

```csharp
// 문제: 리지드 바디 수(N) 만큼 반복 호출되는 함수 내부에서 전체 엔티티 재쿼리
float2 GetRigidVelocity(int nodeId)
{
    foreach (var (node, vel) in
             SystemAPI.Query<RefRO<RigidBodyNodeComponent>, RefRO<RigidVelocityComponent>>())
    {
        if (node.ValueRO.NodeId == nodeId) return vel.ValueRO.Velocity;
    }
    return float2.zero;
}
```

- **문제:** 리지드 바디 N개 처리 시 전체 쿼리를 N번 수행 → **O(N²)**
- **영향:** 리지드 바디 10개 = 매 프레임 ECS 전체 쿼리 10회

#### 2. Managed 컬렉션 (GC 압박) — SolidMaskSyncSystem
**파일:** `Assets/Scripts/ECS/Systems/Sync/SolidMaskSyncSystem.cs:24-25, 54, 169`

```csharp
readonly Dictionary<int, float4x4>    _prevLtw  = new();
readonly Dictionary<int, HashSet<int>> _prevMask = new();
// 매 프레임 리지드 바디마다 new HashSet<int>() 생성
```

- **문제:** 매 프레임 폴리곤 래스터화 시 `HashSet<int>` 반복 할당 → GC 유발
- **영향:** 래스터화 셀 50~500개 × 리지드 바디 수 × 프레임 수

---

### 🟠 High

#### 3. 전체 청크 바이트 비교 (더티 플래그 없음) — PixelGridSyncSystem
**파일:** `Assets/Scripts/Systems/Simulation/PixelGridSyncSystem.cs:56-89`

```csharp
// 문제: 매 5 tick마다 모든 활성 청크의 1024바이트 배열을 전수 비교
for (int i = 0; i < cellCount; i++)   // cellCount = 1024
    if (snapshot[i] != prev[i]) { changed = true; break; }
```

- **문제:** 256 청크 맵 기준 매 167ms마다 **256 KB 스캔**
- **영향:** 변경이 없는 청크도 매번 전체 비교 수행

#### 4. 중첩 루프 RPC 브로드캐스트 — PixelGridRpcSendSystem
**파일:** `Assets/Scripts/Systems/Simulation/PixelGridRpcSendSystem.cs:47-77`

```csharp
// 문제: 변경 청크 수 × 접속자 수 만큼 엔티티 생성
for (int d = 0; d < diffBuf.Length; d++)        // 변경된 청크
    for (int c = 0; c < connections.Length; c++) // 접속자
        ecb.CreateEntity(); // 엔티티 × 청크 × 접속자
```

- **문제:** 청크 32개 변경, 접속자 4명 → **128 엔티티 생성/167ms**
- **영향:** 멀티플레이 인원이 늘수록 엔티티 생성 비용 선형 증가

#### 5. 초기 동기화 SLEEPING 청크 포함 — PixelGridInitialSyncSystem
**파일:** `Assets/Scripts/Systems/Simulation/PixelGridInitialSyncSystem.cs:56-98`

```csharp
// 문제: SLEEPING 청크까지 전부 순회하여 신규 클라이언트에게 전송
for (int cy = 0; cy < chunks.ChunksY; cy++)
for (int cx = 0; cx < chunks.ChunksX; cx++)
{
    // SLEEPING 청크 여부 확인 없이 전부 처리
}
```

- **문제:** 맵의 절반이 SLEEPING 상태여도 전체 ~262 KB 전송
- **영향:** 접속 시 불필요한 초기 지연 발생

---

### 🟡 Medium

#### 6. 폴리곤 래스터화 (매 프레임 재계산) — SolidMaskSyncSystem
**파일:** `Assets/Scripts/ECS/Systems/Sync/SolidMaskSyncSystem.cs:140-179`

```csharp
static HashSet<int> RasterizePoly(...)
{
    var result = new HashSet<int>();
    for (int gy = gMinY; gy <= gMaxY; gy++)
    for (int gx = gMinX; gx <= gMaxX; gx++)
        if (PointInPolygon(new float2(wx, wy), worldPts))  // O(폴리곤 엣지)
            result.Add(gy * grid.Width + gx);
    return result;
}
```

- **문제:** LocalToWorld 변경 시 매 프레임 바운딩 박스 전체 포인트 판정
- **영향:** 4-vertex 폴리곤, 500 셀 바운딩박스 기준 **프레임당 2,000회 판정**

#### 7. 동기화 간격 하드코딩 — PixelGridSyncSystem
**파일:** `Assets/Scripts/Systems/Simulation/PixelGridSyncSystem.cs:14`

```csharp
const int SyncIntervalTicks = 5;  // 외부 설정 불가
```

- **문제:** 씬/네트워크 상태에 따라 동적 조정 불가

---

## 최적화 방안

### 최적화 1: N+1 쿼리 제거 — `SolidMaskSyncSystem`
**우선순위: 🔴 Critical | 예상 효과: O(N²) → O(N)**

`GetRigidVelocity`를 `OnUpdate` 진입 시 `NativeHashMap`으로 미리 구성한 뒤 O(1) 조회로 대체한다.

```csharp
// Before (N+1)
float2 vel = GetRigidVelocity(nodeId);  // 내부에서 전체 쿼리

// After (O(1))
// OnUpdate 초반에 한 번만 빌드
var velocityMap = new NativeHashMap<int, float2>(64, Allocator.Temp);
foreach (var (node, vel) in SystemAPI.Query<...>())
    velocityMap[node.ValueRO.NodeId] = vel.ValueRO.Velocity;

// 이후 O(1) 조회
float2 vel = velocityMap.TryGetValue(nodeId, out var v) ? v : float2.zero;
velocityMap.Dispose();
```

---

### 최적화 2: Managed 컬렉션 → Native 컬렉션 교체 — `SolidMaskSyncSystem`
**우선순위: 🔴 Critical | 예상 효과: GC 할당 제거**

| 기존 | 교체 대상 |
|------|---------|
| `Dictionary<int, float4x4>` | `NativeHashMap<int, float4x4>` |
| `Dictionary<int, HashSet<int>>` | `NativeHashMap<int, NativeList<int>>` |
| `new HashSet<int>()` (매 프레임) | `NativeList<int>` 재사용 (`Clear()`) |

```csharp
// Before: 매 프레임 할당
var result = new HashSet<int>();

// After: 재사용 (Persistent 또는 Rewindable allocator)
_rasterBuffer.Clear();  // NativeList<int>, OnCreate에서 미리 할당
```

---

### 최적화 3: 더티 플래그 기반 청크 동기화 — `PixelGridSyncSystem`
**우선순위: 🟠 High | 예상 효과: 동기화 스캔 80%+ 감소**

CA 시뮬레이션 단계에서 픽셀이 변경될 때 청크를 "dirty"로 표시하고, 동기화 시 dirty 청크만 처리한다.

```csharp
// ChunkManagerSingleton에 더티 플래그 추가
struct ChunkMeta
{
    public ChunkState State;
    public bool IsDirty;  // 추가
}

// CA 시스템에서 픽셀 변경 시 마킹
chunkMeta[chunkIdx].IsDirty = true;

// PixelGridSyncSystem: dirty 청크만 비교
for (int i = 0; i < totalChunks; i++)
{
    if (!chunkMeta[i].IsDirty) continue;  // 스킵
    // ... 기존 비교 로직
    chunkMeta[i].IsDirty = false;
}
```

---

### 최적화 4: 초기 동기화 SLEEPING 청크 스킵 — `PixelGridInitialSyncSystem`
**우선순위: 🟠 High | 예상 효과: 초기 패킷 최대 50% 감소**

SLEEPING 상태인 청크는 공기(Type == 0) 픽셀만 존재하거나 변화가 없는 상태이므로 전송을 생략하고, 클라이언트 초기화 시 기본값(0)으로 처리한다.

```csharp
for (int cy = 0; cy < chunks.ChunksY; cy++)
for (int cx = 0; cx < chunks.ChunksX; cx++)
{
    int chunkIdx = cy * chunks.ChunksX + cx;

    // SLEEPING 청크 스킵 (클라이언트는 0으로 초기화됨)
    if (chunkMeta[chunkIdx].State == ChunkState.Sleeping)
        continue;

    // ... 기존 RPC 전송 로직
}
```

> **주의:** 클라이언트 PixelGrid 초기화 시 전체를 0으로 설정하는 코드가 있어야 함. 확인 필요: `PixelGridRpcApplySystem.cs`.

---

### 최적화 5: 폴리곤 래스터화 캐싱 — `SolidMaskSyncSystem`
**우선순위: 🟡 Medium | 예상 효과: LocalToWorld 미변경 시 재계산 제거**

이미 `_prevLtw`로 이전 변환을 캐싱하고 있으므로, 변경 없을 때 래스터화 완전 스킵.

```csharp
// 기존 코드도 변경 감지는 함. 래스터화 결과를 캐싱하여 재사용
if (_prevLtw.TryGetValue(id, out var prev) && prev.Equals(current))
{
    // LocalToWorld 동일 → 캐싱된 마스크 재사용, 래스터화 스킵
    continue;
}
// 변경된 경우에만 RasterizePoly 호출
```

현재 `_prevMask`에 결과를 저장하는 구조가 이미 있으나, 불필요한 HashSet 재생성이 발생하고 있음. Native 컬렉션으로 전환(최적화 2)과 함께 처리.

---

### 최적화 6: 동기화 간격 외부 설정화 — `PixelGridSyncSystem`
**우선순위: 🟡 Medium | 예상 효과: 씬별/네트워크별 튜닝 가능**

```csharp
// SimulationConstants.cs에 추가
public const int SYNC_INTERVAL_TICKS = 5;

// PixelGridSyncSystem.cs
const int SyncIntervalTicks = SimulationConstants.SYNC_INTERVAL_TICKS;
```

추후 네트워크 지연이 높은 환경에서는 3 tick, 저지연 환경에서는 7 tick 등 동적 조정 가능하도록 확장할 수 있음.

---

## 작업 우선순위 요약

| 순위 | 항목 | 파일 | 효과 | 난이도 |
|------|------|------|------|--------|
| 1 | N+1 쿼리 제거 | `SolidMaskSyncSystem.cs:196` | O(N²) → O(N) | 낮음 |
| 2 | Native 컬렉션 전환 | `SolidMaskSyncSystem.cs:24` | GC 제거 | 중간 |
| 3 | 더티 플래그 동기화 | `PixelGridSyncSystem.cs:56` | 스캔 80% 감소 | 중간 |
| 4 | 초기 동기화 SLEEPING 스킵 | `PixelGridInitialSyncSystem.cs:56` | 초기 패킷 50% 감소 | 낮음 |
| 5 | 래스터화 캐시 개선 | `SolidMaskSyncSystem.cs:140` | 재계산 제거 | 낮음 |
| 6 | 동기화 간격 외부화 | `SimulationConstants.cs` | 튜닝 유연성 | 매우 낮음 |

---

## 기존 최적화 현황 (변경 불필요)

- ✅ **청크 슬리핑** — 비활성 청크 CA 시뮬레이션 제외 (`CHUNK_SLEEP_THRESHOLD = 5`)
- ✅ **uint 패킹** — 4셀 → 1 uint, 4104B → 1041B로 압축
- ✅ **델타 전송** — 변경된 청크만 브로드캐스트
- ✅ **이중 처리 방지** — `CellUpdateTick` 배열로 CA 단계 간 중복 방지
- ✅ **Burst 컴파일** — CA 잡에 `[BurstCompile]` 적용

---

## 참고 파일 목록

| 파일 | 역할 |
|------|------|
| `Assets/Scripts/Systems/Simulation/PixelGridSyncSystem.cs` | 델타 청크 감지 |
| `Assets/Scripts/Systems/Simulation/PixelGridRpcSendSystem.cs` | RPC 브로드캐스트 |
| `Assets/Scripts/Systems/Simulation/PixelGridRpcApplySystem.cs` | 클라이언트 수신 적용 |
| `Assets/Scripts/Systems/Simulation/PixelGridInitialSyncSystem.cs` | 신규 클라이언트 초기 동기화 |
| `Assets/Scripts/ECS/Systems/Sync/SolidMaskSyncSystem.cs` | 리지드 바디 마스크 동기화 |
| `Assets/Scripts/Core/SimulationConstants.cs` | 튜닝 상수 |
| `Assets/Documents/ImprovementTasks.md` | 기존 개선 태스크 목록 |
