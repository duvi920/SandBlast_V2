using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 서버 전용 — 투사체를 매 프레임 속도(Velocity) × dt 만큼 이동시키고 수명이 다한 투사체를 제거한다.
[BurstCompile]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
public partial struct BulletMoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime)) return;
        if (!networkTime.IsFirstTimeFullyPredictingTick) return;

        var dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (bulletData, lifetime, transform, entity) in SystemAPI
            .Query<RefRW<BulletGhostData>, RefRW<BulletLifetime>, RefRW<LocalTransform>>()
            .WithEntityAccess()
            .WithAll<Simulate>())
        {
            lifetime.ValueRW.Value -= dt;
            if (lifetime.ValueRO.Value <= 0f)
            {
                ecb.DestroyEntity(entity);
                continue;
            }

            var newPos = bulletData.ValueRO.Position + bulletData.ValueRO.Velocity * dt;
            bulletData.ValueRW.Position = newPos;
            transform.ValueRW.Position = new float3(newPos.x, newPos.y, 0f);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
