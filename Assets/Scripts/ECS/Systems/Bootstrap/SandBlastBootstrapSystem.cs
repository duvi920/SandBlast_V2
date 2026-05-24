using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SandBlast
{
    // SandBlastConfigComponent를 읽어 PixelGridSingleton·ChunkManagerSingleton·SimConfigSingleton을
    // 한 번 생성하고 이후 비활성화된다. NativeArray 해제는 OnDestroy에서 수행한다.
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial struct SandBlastBootstrapSystem : ISystem
    {
        // 생명주기 관리를 위해 할당된 NativeArray 원본을 필드에 보관
        NativeArray<byte>      _type, _temperature, _lifetime, _rigidId, _flammability;
        NativeArray<ChunkMeta> _chunkMetas;
        NativeArray<int>       _xShuffle, _cellUpdateTick;
        bool                   _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SandBlastConfigComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized) return;
            _initialized = true;

            var cfg = SystemAPI.GetSingleton<SandBlastConfigComponent>();
            int n   = cfg.GridWidth * cfg.GridHeight;

            // ── 픽셀 그리드 NativeArray 할당 ──────────────────────
            _type         = new NativeArray<byte>(n, Allocator.Persistent);
            _temperature  = new NativeArray<byte>(n, Allocator.Persistent);
            _lifetime     = new NativeArray<byte>(n, Allocator.Persistent);
            _rigidId      = new NativeArray<byte>(n, Allocator.Persistent);
            _flammability = new NativeArray<byte>(n, Allocator.Persistent);

            state.EntityManager.CreateSingleton(new PixelGridSingleton
            {
                Width        = cfg.GridWidth,
                Height       = cfg.GridHeight,
                PixelsPerUnit = cfg.PixelsPerUnit,
                GridOrigin   = cfg.GridOrigin,
                Type         = _type,
                Temperature  = _temperature,
                Lifetime     = _lifetime,
                RigidId      = _rigidId,
                Flammability = _flammability,
            });

            // ── 청크 싱글턴 ───────────────────────────────────────
            int chunksX = (cfg.GridWidth  + SimulationConstants.CHUNK_SIZE - 1) / SimulationConstants.CHUNK_SIZE;
            int chunksY = (cfg.GridHeight + SimulationConstants.CHUNK_SIZE - 1) / SimulationConstants.CHUNK_SIZE;
            int chunkN  = chunksX * chunksY;

            _chunkMetas = new NativeArray<ChunkMeta>(chunkN, Allocator.Persistent);

            // 모든 청크를 ACTIVE로 초기화
            for (int i = 0; i < chunkN; i++)
                _chunkMetas[i] = new ChunkMeta { State = ChunkState.ACTIVE };

            state.EntityManager.CreateSingleton(new ChunkManagerSingleton
            {
                ChunksX = chunksX,
                ChunksY = chunksY,
                Metas   = _chunkMetas,
            });

            // ── 시뮬레이션 설정 싱글턴 ────────────────────────────
            _xShuffle      = new NativeArray<int>(cfg.GridWidth, Allocator.Persistent);
            _cellUpdateTick = new NativeArray<int>(n, Allocator.Persistent);

            for (int i = 0; i < cfg.GridWidth; i++) _xShuffle[i] = i;

            state.EntityManager.CreateSingleton(new SimConfigSingleton
            {
                TicksPerSecond = cfg.TicksPerSecond,
                TickTimer      = 0f,
                CurrentTick    = 0,
                XShuffle       = _xShuffle,
                CellUpdateTick = _cellUpdateTick,
            });

            // 설정 컴포넌트 제거 — RequireForUpdate 조건을 해제해 시스템 자동 중지
            state.EntityManager.RemoveComponent<SandBlastConfigComponent>(
                SystemAPI.GetSingletonEntity<SandBlastConfigComponent>());
        }

        public void OnDestroy(ref SystemState state)
        {
            if (!_initialized) return;

            if (_type.IsCreated)         _type.Dispose();
            if (_temperature.IsCreated)  _temperature.Dispose();
            if (_lifetime.IsCreated)     _lifetime.Dispose();
            if (_rigidId.IsCreated)      _rigidId.Dispose();
            if (_flammability.IsCreated) _flammability.Dispose();
            if (_chunkMetas.IsCreated)   _chunkMetas.Dispose();
            if (_xShuffle.IsCreated)     _xShuffle.Dispose();
            if (_cellUpdateTick.IsCreated) _cellUpdateTick.Dispose();
        }
    }
}
