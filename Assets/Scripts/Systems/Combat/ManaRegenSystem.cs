using SandBlast.Wand;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — 매 틱 플레이어의 마나를 ManaComponent.RegenRate 만큼 회복한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ManaRegenSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (mana, ghostData) in SystemAPI
            .Query<RefRW<ManaComponent>, RefRO<PlayerGhostData>>()
            .WithAll<Simulate>())
        {
            if (ghostData.ValueRO.IsDead) continue;
            if (mana.ValueRO.Current >= mana.ValueRO.Max) continue;

            mana.ValueRW.Current = math.min(
                mana.ValueRO.Current + mana.ValueRO.RegenRate * dt,
                mana.ValueRO.Max);
        }
    }
}
