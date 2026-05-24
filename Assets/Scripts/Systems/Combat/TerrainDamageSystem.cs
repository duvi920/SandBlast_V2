using SandBlast;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

// 서버 및 클라이언트 — PendingPixelConversionTag가 붙은 강체 노드의 IsDestroyed 플래그를 설정한다.
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(RigidBodyGraphSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
public partial struct TerrainDamageSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime)) return;
        if (!networkTime.IsFirstTimeFullyPredictingTick) return;

        foreach (var ghostData in SystemAPI
            .Query<RefRW<RigidBodyGhostData>>()
            .WithAll<PendingPixelConversionTag>()
            .WithAll<Simulate>())
        {
            if (!ghostData.ValueRO.IsDestroyed)
                ghostData.ValueRW.IsDestroyed = true;
        }
    }
}
