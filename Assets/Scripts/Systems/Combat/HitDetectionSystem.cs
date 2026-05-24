using SandBlast;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — 매 프레임 투사체와 플레이어 간 거리를 검사해 충돌 시 HitEvent 버퍼에 데미지를 기록하고 투사체를 제거한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(BulletMoveSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct HitDetectionSystem : ISystem
{
    private const float BulletRadius = 0.15f;
    private const float PlayerRadius = 0.45f;
    private const float HitThreshold = BulletRadius + PlayerRadius;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // 플레이어 위치/엔티티를 NativeArray로 수집
        var playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerGhostData>()
            .Build();

        var playerEntities = playerQuery.ToEntityArray(Allocator.Temp);
        var playerDataArray = playerQuery.ToComponentDataArray<PlayerGhostData>(Allocator.Temp);

        foreach (var (bulletData, entity) in SystemAPI
            .Query<RefRO<BulletGhostData>>()
            .WithEntityAccess())
        {
            for (int i = 0; i < playerEntities.Length; i++)
            {
                var pd = playerDataArray[i];
                if (pd.IsDead) continue;
                if (pd.IsGrounded && bulletData.ValueRO.OwnerNetworkId == -1) continue; // 자기 자신 방어는 GhostOwner로 처리

                var dist = math.distance(bulletData.ValueRO.Position, pd.Position);
                if (dist > HitThreshold) continue;

                // HitEvent 버퍼에 추가
                if (!state.EntityManager.HasBuffer<HitEvent>(playerEntities[i]))
                    ecb.AddBuffer<HitEvent>(playerEntities[i]);

                ecb.AppendToBuffer(playerEntities[i], new HitEvent
                {
                    TargetEntity = playerEntities[i],
                    Damage = bulletData.ValueRO.Damage,
                    HitPosition = bulletData.ValueRO.Position,
                    StunPower = SimulationConstants.BulletStunPower,
                });

                ecb.DestroyEntity(entity);
                break;
            }
        }

        playerEntities.Dispose();
        playerDataArray.Dispose();

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
