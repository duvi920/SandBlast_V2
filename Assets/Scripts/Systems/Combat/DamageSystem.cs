using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

// 서버 전용 — HitEvent 버퍼를 소비해 플레이어 HP를 감소시키고, HP가 0 이하가 되면 DeathEvent를 추가한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HitDetectionSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct DamageSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (ghostData, hitBuffer, entity) in SystemAPI
            .Query<RefRW<PlayerGhostData>, DynamicBuffer<HitEvent>>()
            .WithEntityAccess())
        {
            if (hitBuffer.IsEmpty) continue;
            if (ghostData.ValueRO.IsDead) continue;

            for (int i = 0; i < hitBuffer.Length; i++)
            {
                var hp = ghostData.ValueRO.Health - hitBuffer[i].Damage;
                ghostData.ValueRW.Health = hp;

                if (hp <= 0f)
                {
                    ghostData.ValueRW.Health = 0f;
                    ghostData.ValueRW.IsDead = true;
                    ecb.AddComponent(entity, new DeathEvent { KillerNetworkId = -1, Cause = DeathCause.Damage });
                    break;
                }
            }

            ecb.SetBuffer<HitEvent>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
