using SandBlast;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

// 클라이언트+서버 — Ghost 동기화로 전달된 IsDestroyed=true를 감지해
// PendingPixelConversionTag를 추가한다. 실제 픽셀 전환은 RigidToPixelConvertSystem이 처리한다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
public partial struct TerrainSyncSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (ghostData, entity) in SystemAPI
            .Query<RefRO<RigidBodyGhostData>>()
            .WithNone<PendingPixelConversionTag>()
            .WithEntityAccess())
        {
            if (ghostData.ValueRO.IsDestroyed)
                ecb.AddComponent<PendingPixelConversionTag>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
