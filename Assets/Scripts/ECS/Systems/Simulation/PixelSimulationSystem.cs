using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SandBlast
{
    // 픽셀 CA 시뮬레이션 마스터 시스템.
    // 틱 타이머를 관리하고 매 틱마다 Fire→Smoke→Liquid→Powder→Debris 순으로 Job을 실행한다.
    // Job은 .Run() (동기, Burst 컴파일)으로 실행해 CA 순서 의존성을 보장한다.
    [BurstCompile]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(SolidMaskSyncSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PixelSimulationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PixelGridSingleton>();
            state.RequireForUpdate<SimConfigSingleton>();
            state.RequireForUpdate<ChunkManagerSingleton>();
            state.RequireForUpdate<NetworkTime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
            var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();
            var cfgRW  = SystemAPI.GetSingletonRW<SimConfigSingleton>();

            if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime)) return;
            if (networkTime.ServerTick.RawValue == 0) return;

            // 예측 중복 실행 방지 (결정론적 락스텝: 각 틱당 1회만 실행)
            if (!networkTime.IsFirstTimeFullyPredictingTick) return;

            // 초기화: CurrentTick이 0이면 현재 서버 틱으로 맞춤
            if (cfgRW.ValueRW.CurrentTick == 0)
            {
                cfgRW.ValueRW.CurrentTick = (int)networkTime.ServerTick.RawValue;
                return;
            }

            // 서버 틱에 맞춰 시뮬레이션 전진
            int targetTick = (int)networkTime.ServerTick.RawValue;
            
            // PredictedSimulationSystemGroup 내부이므로 networkTime.ServerTick은 
            // 클라이언트에서 현재 예측 중인 틱일 수도 있고, 서버 틱일 수도 있습니다.
            // 여기서는 단순히 CurrentTick을 1씩 증가시키며 따라갑니다.
            
            if (cfgRW.ValueRW.CurrentTick < targetTick)
            {
                cfgRW.ValueRW.CurrentTick++;
                int currentTick = cfgRW.ValueRW.CurrentTick;

                // int 오버플로우(~20억 틱 ≈ 연속 가동 약 750일) 후 CurrentTick이 음수로 반전되면
                // CellUpdateTick 비교가 영구 true가 되어 모든 셀이 매 틱 중복 처리된다.
                // 이를 방지하기 위해 CellUpdateTick 배열 전체를 0으로 초기화하고 CurrentTick을 1로 리셋한다.
                if (currentTick <= 0)
                {
                    var tick = cfgRW.ValueRW.CellUpdateTick;
                    for (int i = 0; i < tick.Length; i++) tick[i] = 0;
                    cfgRW.ValueRW.CurrentTick = 1;
                    currentTick = 1;
                }

                ShuffleX(cfgRW.ValueRW.XShuffle, (uint)currentTick);

                var cellUpdateTick = cfgRW.ValueRW.CellUpdateTick;
                var chunkMetas     = chunks.Metas;
                int chunksX        = chunks.ChunksX;
                int chunksY        = chunks.ChunksY;

                new PixelFireJob
                {
                    Type = grid.Type, Temperature = grid.Temperature,
                    Lifetime = grid.Lifetime, Flammability = grid.Flammability,
                    CellUpdateTick = cellUpdateTick, ChunkMetas = chunkMetas,
                    Width = grid.Width, Height = grid.Height,
                    CurrentTick = currentTick, ChunksX = chunksX, ChunksY = chunksY,
                    Rng = Unity.Mathematics.Random.CreateFromIndex((uint)currentTick * 1234567u + 1u),
                }.Run();

                new PixelSmokeJob
                {
                    Type = grid.Type, Temperature = grid.Temperature,
                    Lifetime = grid.Lifetime, Flammability = grid.Flammability,
                    CellUpdateTick = cellUpdateTick, ChunkMetas = chunkMetas,
                    Width = grid.Width, Height = grid.Height,
                    CurrentTick = currentTick, ChunksX = chunksX, ChunksY = chunksY,
                    Rng = Unity.Mathematics.Random.CreateFromIndex((uint)currentTick * 7654321u + 2u),
                }.Run();

                new PixelLiquidJob
                {
                    Type = grid.Type, Temperature = grid.Temperature,
                    Lifetime = grid.Lifetime, Flammability = grid.Flammability,
                    CellUpdateTick = cellUpdateTick, ChunkMetas = chunkMetas,
                    Width = grid.Width, Height = grid.Height,
                    CurrentTick = currentTick, ChunksX = chunksX, ChunksY = chunksY,
                    Rng = Unity.Mathematics.Random.CreateFromIndex((uint)currentTick * 2345678u + 3u),
                }.Run();

                new PixelPowderJob
                {
                    Type = grid.Type, Temperature = grid.Temperature,
                    Lifetime = grid.Lifetime, Flammability = grid.Flammability,
                    CellUpdateTick = cellUpdateTick, ChunkMetas = chunkMetas,
                    XShuffle = cfgRW.ValueRW.XShuffle,
                    Width = grid.Width, Height = grid.Height,
                    CurrentTick = currentTick, ChunksX = chunksX, ChunksY = chunksY,
                    Rng = Unity.Mathematics.Random.CreateFromIndex((uint)currentTick * 8765432u + 4u),
                }.Run();

                new PixelDebrisJob
                {
                    Type = grid.Type, Temperature = grid.Temperature,
                    Lifetime = grid.Lifetime, Flammability = grid.Flammability,
                    CellUpdateTick = cellUpdateTick, ChunkMetas = chunkMetas,
                    XShuffle = cfgRW.ValueRW.XShuffle,
                    Width = grid.Width, Height = grid.Height,
                    CurrentTick = currentTick, ChunksX = chunksX, ChunksY = chunksY,
                    Rng = Unity.Mathematics.Random.CreateFromIndex((uint)currentTick * 3456789u + 5u),
                }.Run();
            }
        }

        // Fisher-Yates 셔플 (Burst 호환 정적 메서드)
        static void ShuffleX(NativeArray<int> arr, uint seed)
        {
            var rng = Unity.Mathematics.Random.CreateFromIndex(seed);
            for (int i = arr.Length - 1; i > 0; i--)
            {
                int j      = rng.NextInt(0, i + 1);
                int tmp    = arr[i];
                arr[i]     = arr[j];
                arr[j]     = tmp;
            }
        }
    }
}
