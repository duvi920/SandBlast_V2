using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace SandBlast
{
    // 서버 및 클라이언트 — PlayerInput의 페인팅 데이터를 읽어 픽셀 그리드에 적용한다.
    // 결정론적 동기화를 위해 PredictedSimulationSystemGroup에서 실행된다.
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(PixelSimulationSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial struct TerrainPaintSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PixelGridSingleton>();
            state.RequireForUpdate<ChunkManagerSingleton>();
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime)) return;
            if (!networkTime.IsFirstTimeFullyPredictingTick) return;

            foreach (var input in SystemAPI.Query<RefRO<PlayerInput>>().WithAll<Simulate>())
            {
                if (!input.ValueRO.IsPainting) continue;

                PixelGridAPI.SpawnCircle(
                    new Vector2(input.ValueRO.PaintPos.x, input.ValueRO.PaintPos.y),
                    input.ValueRO.PaintRadius,
                    (CellType)input.ValueRO.PaintCellType,
                    input.ValueRO.PaintFlammability);
            }
        }
    }
}
