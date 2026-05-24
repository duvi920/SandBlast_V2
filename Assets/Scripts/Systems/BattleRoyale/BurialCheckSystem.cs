using SandBlast;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — 모든 살아있는 플레이어의 주변 픽셀을 샘플링해 80% 이상이 Solid이면 즉사 처리한다.
// 기절 여부와 무관하게 동작한다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ShovelUseSystem))]
[UpdateBefore(typeof(DamageSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class BurialCheckSystem : SystemBase
{
    // 3×5 샘플 오프셋 (월드 단위)
    private static readonly float2[] SampleOffsets = new float2[]
    {
        new float2(-0.20f, -0.60f), new float2(0f, -0.60f), new float2(0.20f, -0.60f),
        new float2(-0.20f, -0.30f), new float2(0f, -0.30f), new float2(0.20f, -0.30f),
        new float2(-0.20f,  0.00f), new float2(0f,  0.00f), new float2(0.20f,  0.00f),
        new float2(-0.20f,  0.30f), new float2(0f,  0.30f), new float2(0.20f,  0.30f),
        new float2(-0.20f,  0.60f), new float2(0f,  0.60f), new float2(0.20f,  0.60f),
    };

    private const int SampleCount   = 15;
    private const int SolidThreshold = 12; // 15개 중 12개(80%)

    protected override void OnCreate()
    {
        RequireForUpdate<PixelGridSingleton>();
        RequireForUpdate<RoomStateSingleton>();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<RoomStateSingleton>(out var room))
            return;
        if (room.Phase != RoomPhase.Battle)
            return;

        var grid = SystemAPI.GetSingleton<PixelGridSingleton>();
        var ecb  = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (ghostData, entity) in SystemAPI
            .Query<RefRW<PlayerGhostData>>()
            .WithEntityAccess()
            .WithAll<Simulate>())
        {
            if (ghostData.ValueRO.IsDead) continue;

            int solidCount = 0;
            float2 pos = ghostData.ValueRO.Position;

            for (int i = 0; i < SampleCount; i++)
            {
                float2 samplePos = pos + SampleOffsets[i];
                int2 gp = grid.WorldToGrid(samplePos);
                if (!grid.InBounds(gp.x, gp.y)) continue;

                CellType ct = (CellType)grid.Type[grid.Index(gp.x, gp.y)];
                if (ct.IsSolid())
                    solidCount++;
            }

            if (solidCount < SolidThreshold) continue;

            ghostData.ValueRW.IsDead = true;
            ecb.AddComponent(entity, new DeathEvent
            {
                KillerNetworkId = -1,
                Cause           = DeathCause.Buried,
            });
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
