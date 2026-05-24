using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace SandBlast
{
    // MonoBehaviour 코드에서 ECS PixelGridSingleton에 접근하기 위한 정적 API.
    // 기존 SandBlastEngine의 SpawnCell / SpawnCircle / WorldToGrid 공개 API를 대체한다.
    public static class PixelGridAPI
    {
        static PixelGridSingleton   _gridCache;
        static ChunkManagerSingleton _chunkCache;
        static bool                  _cached;

        // 런타임 강제 부트스트랩(베이킹/NetCode 월드 타이밍 이슈 우회)
        static bool _runtimeBootstrapped;
        static NativeArray<byte>      _rtType, _rtTemperature, _rtLifetime, _rtRigidId, _rtFlammability;
        static NativeArray<ChunkMeta> _rtChunkMetas;
        static NativeArray<int>       _rtXShuffle, _rtCellUpdateTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _gridCache = default;
            _chunkCache = default;
            _cached = false;

            _runtimeBootstrapped = false;
            _rtType = default;
            _rtTemperature = default;
            _rtLifetime = default;
            _rtRigidId = default;
            _rtFlammability = default;
            _rtChunkMetas = default;
            _rtXShuffle = default;
            _rtCellUpdateTick = default;
        }

        // ─── 싱글턴 접근 ────────────────────────────────────────

        public static bool TryGetGrid(out PixelGridSingleton grid, out ChunkManagerSingleton chunks)
        {
            if (_cached && _gridCache.Type.IsCreated)
            {
                grid   = _gridCache;
                chunks = _chunkCache;
                return true;
            }

            // 먼저, 런타임에 그리드가 없다면 씬 설정을 기반으로 강제 생성 시도
            EnsureRuntimeBootstrap();

            foreach (var world in World.All)
            {
                if (world.IsCreated)
                {
                    var em = world.EntityManager;
                    // 컴포넌트가 존재하는지 확인
                    using var query = em.CreateEntityQuery(typeof(PixelGridSingleton));
                    if (!query.IsEmpty)
                    {
                        _gridCache = query.GetSingleton<PixelGridSingleton>();
                        
                        using var chunkQuery = em.CreateEntityQuery(typeof(ChunkManagerSingleton));
                        _chunkCache = !chunkQuery.IsEmpty
                            ? chunkQuery.GetSingleton<ChunkManagerSingleton>()
                            : default;

                        _cached = true;
                        grid = _gridCache;
                        chunks = _chunkCache;
                        return true;
                    }
                }
            }

            grid   = default;
            chunks = default;
            return false;
        }

        static void EnsureRuntimeBootstrap()
        {
            if (_runtimeBootstrapped) return;

            // 씬에 부트스트랩 오브젝트가 없으면 생성하지 않는다.
            var authoring = Object.FindFirstObjectByType<SandBlastBootstrapAuthoring>();
            if (authoring == null) return;

            // NetCode를 쓰면 DefaultGameObjectInjectionWorld가 "서버"로 잡히거나,
            // 클라이언트/로컬 월드가 별도로 생성될 수 있다.
            // 실제 렌더/입력/플레이어 시스템이 도는 월드(보통 Client/Local)를 우선 선택한다.
            var world = PickBestWorld();
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;

            // 이미 생성돼 있다면 끝
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<PixelGridSingleton>()))
            {
                if (!q.IsEmpty)
                {
                    _runtimeBootstrapped = true;
                    return;
                }
            }

            int w = authoring.GridWidth;
            int h = authoring.GridHeight;
            int n = w * h;
            if (w <= 0 || h <= 0 || n <= 0) return;

            // NativeArray 할당
            _rtType         = new NativeArray<byte>(n, Allocator.Persistent);
            _rtTemperature  = new NativeArray<byte>(n, Allocator.Persistent);
            _rtLifetime     = new NativeArray<byte>(n, Allocator.Persistent);
            _rtRigidId      = new NativeArray<byte>(n, Allocator.Persistent);
            _rtFlammability = new NativeArray<byte>(n, Allocator.Persistent);

            em.CreateSingleton(new PixelGridSingleton
            {
                Width         = w,
                Height        = h,
                PixelsPerUnit = authoring.PixelsPerUnit,
                GridOrigin    = new float2(authoring.GridOrigin.x, authoring.GridOrigin.y),
                Type          = _rtType,
                Temperature   = _rtTemperature,
                Lifetime      = _rtLifetime,
                RigidId       = _rtRigidId,
                Flammability  = _rtFlammability,
            });

            // 청크 싱글턴
            int chunksX = (w + SimulationConstants.CHUNK_SIZE - 1) / SimulationConstants.CHUNK_SIZE;
            int chunksY = (h + SimulationConstants.CHUNK_SIZE - 1) / SimulationConstants.CHUNK_SIZE;
            int chunkN  = chunksX * chunksY;
            _rtChunkMetas = new NativeArray<ChunkMeta>(chunkN, Allocator.Persistent);
            for (int i = 0; i < chunkN; i++)
                _rtChunkMetas[i] = new ChunkMeta { State = ChunkState.ACTIVE };

            em.CreateSingleton(new ChunkManagerSingleton
            {
                ChunksX = chunksX,
                ChunksY = chunksY,
                Metas   = _rtChunkMetas,
            });

            // 시뮬레이션 설정 싱글턴
            _rtXShuffle      = new NativeArray<int>(w, Allocator.Persistent);
            _rtCellUpdateTick = new NativeArray<int>(n, Allocator.Persistent);
            for (int i = 0; i < w; i++) _rtXShuffle[i] = i;

            em.CreateSingleton(new SimConfigSingleton
            {
                TicksPerSecond = authoring.TicksPerSecond,
                TickTimer      = 0f,
                CurrentTick    = 0,
                XShuffle       = _rtXShuffle,
                CellUpdateTick = _rtCellUpdateTick,
            });

            _runtimeBootstrapped = true;
        }

        static World PickBestWorld()
        {
            World fallback = World.DefaultGameObjectInjectionWorld;

            World server = null;
            World local  = null;
            World client = null;

            foreach (var w in World.All)
            {
                if (w == null || !w.IsCreated) continue;
                var name = w.Name ?? string.Empty;

                // 권위 있는 월드 우선: Server > Local > Client
                // PixelGridSyncSystem이 ServerWorld를 읽으므로 패인터도 ServerWorld에 써야 한다.
                if (server == null && name.Contains("Server")) server = w;
                if (local  == null && name.Contains("Local"))  local  = w;
                if (client == null && name.Contains("Client")) client = w;
            }

            return server ?? local ?? client ?? fallback;
        }

        // 월드 좌표 → 그리드 좌표 (Vector2 → Vector2Int 래퍼)
        public static bool WorldToGrid(Vector2 worldPos, out Vector2Int gp)
        {
            if (!TryGetGrid(out var grid, out _))
            {
                gp = default;
                return false;
            }
            int2 p = grid.WorldToGrid(new float2(worldPos.x, worldPos.y));
            gp = new Vector2Int(p.x, p.y);
            return true;
        }

        // 그리드 (x, y) 셀 타입 반환 (InBounds 밖이면 SOLID_STATIC)
        public static CellType GetCell(Vector2Int gp)
        {
            if (!TryGetGrid(out var grid, out _)) return CellType.SOLID_STATIC;
            return grid.Get(gp.x, gp.y);
        }

        // ─── 스폰 API ────────────────────────────────────────────

        public static void SpawnCell(Vector2 worldPos, CellType type, byte flammability = 0)
        {
            if (!TryGetGrid(out var grid, out var chunks)) return;

            int2 gp = grid.WorldToGrid(new float2(worldPos.x, worldPos.y));
            if (!grid.InBounds(gp.x, gp.y)) return;

            WriteCell(grid, chunks, gp.x, gp.y, type, flammability);
        }

        public static void SpawnCircle(Vector2 worldPos, float worldRadius, CellType type, byte flammability = 0)
        {
            if (!TryGetGrid(out var grid, out var chunks)) return;

            int  pixelRadius = Mathf.RoundToInt(worldRadius * grid.PixelsPerUnit);
            int2 center      = grid.WorldToGrid(new float2(worldPos.x, worldPos.y));

            for (int dy = -pixelRadius; dy <= pixelRadius; dy++)
            for (int dx = -pixelRadius; dx <= pixelRadius; dx++)
            {
                if (dx * dx + dy * dy > pixelRadius * pixelRadius) continue;
                int gx = center.x + dx, gy = center.y + dy;
                if (!grid.InBounds(gx, gy)) continue;

                int idx = grid.Index(gx, gy);
                var existing = (CellType)grid.Type[idx];
                // 지우기(EMPTY) 모드는 모든 셀 덮어쓰기 허용; 그 외에는 빈 셀·연기만 페인팅
                if (type != CellType.EMPTY &&
                    existing != CellType.EMPTY &&
                    existing != CellType.GAS_SMOKE) continue;

                WriteCell(grid, chunks, gx, gy, type, flammability);
            }
        }

        public static void ClearGrid()
        {
            if (!TryGetGrid(out var grid, out _)) return;
            for (int i = 0; i < grid.Type.Length; i++)
            {
                grid.Type[i]         = 0;
                grid.Temperature[i]  = 0;
                grid.Lifetime[i]     = 0;
                grid.Flammability[i] = 0;
            }
        }

        // ─── 내부 ─────────────────────────────────────────────

        static void WriteCell(PixelGridSingleton grid, ChunkManagerSingleton chunks,
                              int gx, int gy, CellType type, byte flammability)
        {
            int idx = grid.Index(gx, gy);
            grid.Type[idx]         = (byte)type;
            grid.Flammability[idx] = flammability;

            if (type == CellType.FIRE)
                grid.Lifetime[idx] = (byte)SimulationConstants.FIRE_LIFE_MAX;
            else if (type == CellType.GAS_SMOKE)
                grid.Lifetime[idx] = (byte)SimulationConstants.SMOKE_LIFE_MAX;
            else if (type == CellType.LIQUID_LAVA)
                grid.Temperature[idx] = (byte)SimulationConstants.LAVA_INITIAL_TEMP;

            if (chunks.Metas.IsCreated)
                chunks.MarkMoved(gx, gy);
        }
    }
}
