using SandBlast;
using SandBlast.Wand;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — LiquidSpray 타입 투사체가 비행 중 매 틱 현재 위치에 액체 셀을 1개 생성한다.
// BulletMoveSystem 이동 후 실행해 최신 위치에 셀을 쌓는다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BulletMoveSystem))]
[UpdateBefore(typeof(PixelSimulationSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct LiquidSpraySystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PixelGridSingleton>();
        state.RequireForUpdate<ChunkManagerSingleton>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
        var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();

        foreach (var (bulletData, proj) in SystemAPI
            .Query<RefRO<BulletGhostData>, RefRO<ProjectileComponent>>())
        {
            int2 gp = grid.WorldToGrid(bulletData.ValueRO.Position);
            if (!grid.InBounds(gp.x, gp.y)) continue;

            int idx = grid.Index(gp.x, gp.y);
            if ((CellType)grid.Type[idx] != CellType.EMPTY) continue;

            CellType cellType = CellType.EMPTY;

            if (proj.ValueRO.Type == ProjectileType.Sand)
            {
                cellType = CellType.POWDER_SAND;
            }
            else if (proj.ValueRO.Type == ProjectileType.LiquidSpray
                     && proj.ValueRO.Payload != LiquidPayload.None)
            {
                cellType = proj.ValueRO.Payload == LiquidPayload.Lava
                    ? CellType.LIQUID_LAVA
                    : CellType.EMPTY; // Poison — Phase 3 에서 LIQUID_POISON 추가 후 활성화
            }

            if (cellType == CellType.EMPTY) continue;

            grid.Type[idx] = (byte)cellType;
            if (cellType == CellType.LIQUID_LAVA)
                grid.Temperature[idx] = SimulationConstants.LAVA_INITIAL_TEMP;
            chunks.MarkMoved(gp.x, gp.y);
        }
    }
}
