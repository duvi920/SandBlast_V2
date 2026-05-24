using SandBlast;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

// 서버 전용 — UseShovel 입력 시 공격자 자신의 주변에 POWDER_SAND를 스탬프한다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(StunSystem))]
[UpdateBefore(typeof(BurialCheckSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class ShovelUseSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<PixelGridSingleton>();
        RequireForUpdate<ChunkManagerSingleton>();
    }

    protected override void OnUpdate()
    {
        var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
        var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();

        // 픽셀 반경을 월드 단위로 변환
        float radius = SimulationConstants.ShovelRadiusPx / grid.PixelsPerUnit;

        foreach (var (input, ghostData) in SystemAPI
            .Query<RefRO<PlayerInput>, RefRW<PlayerGhostData>>()
            .WithAll<Simulate>())
        {
            if (ghostData.ValueRO.IsDead) continue;
            if (ghostData.ValueRO.IsStunned) continue;
            if (!ghostData.ValueRO.HasShovel) continue;
            if (!input.ValueRO.UseShovel.IsSet) continue;

            PixelImpactUtility.StampCircle(
                ref grid,
                ref chunks,
                ghostData.ValueRO.Position,
                radius,
                CellType.POWDER_SAND);

            int remaining = ghostData.ValueRO.ShovelUseCount - 1;
            ghostData.ValueRW.ShovelUseCount = remaining;

            if (remaining <= 0)
            {
                ghostData.ValueRW.HasShovel = false;
                ghostData.ValueRW.ShovelUseCount = 0;
            }
        }
    }
}
