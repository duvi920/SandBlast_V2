using SandBlast;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — 플레이어가 LIQUID_POISON 셀 위에 있으면 초당 POISON_DAMAGE_PER_SECOND HP 감소.
// 독 웅덩이에 발이 닿은 순간부터 적용한다 (발 아래 3개 샘플 포인트 확인).
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct PoisonDamageSystem : ISystem
{
    private const float SampleWidth      = 0.25f;
    private const float PlayerHalfHeight = 0.75f;
    private const float FootOffset       = 0.04f;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PixelGridSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt   = SystemAPI.Time.DeltaTime;
        var   grid = SystemAPI.GetSingleton<PixelGridSingleton>();
        float dmg  = SimulationConstants.POISON_DAMAGE_PER_SECOND * dt;

        foreach (var ghostData in SystemAPI
            .Query<RefRW<PlayerGhostData>>()
            .WithAll<Simulate>())
        {
            if (ghostData.ValueRO.IsDead) continue;

            float2 pos   = ghostData.ValueRO.Position;
            float  footY = pos.y - PlayerHalfHeight - FootOffset;
            bool   inPoison = false;

            for (int i = 0; i < 3 && !inPoison; i++)
            {
                float wx = pos.x + math.lerp(-SampleWidth, SampleWidth, i / 2f);
                int2  gp = grid.WorldToGrid(new float2(wx, footY));
                if (!grid.InBounds(gp.x, gp.y)) continue;
                if ((CellType)grid.Type[grid.Index(gp.x, gp.y)] == CellType.LIQUID_POISON)
                    inPoison = true;
            }

            if (!inPoison) continue;

            float newHp = ghostData.ValueRO.Health - dmg;
            ghostData.ValueRW.Health = math.max(0f, newHp);
            if (ghostData.ValueRO.Health <= 0f)
                ghostData.ValueRW.IsDead = true;
        }
    }
}
