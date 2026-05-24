using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 서버 전용 예측 시스템 — 사격 입력이 들어오면 탄약 확인 후 BulletPrefab을 생성하고 쿨다운을 설정한다.
[BurstCompile]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ShootSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GamePrefabs>(out var prefabs))
            return;

        var dt = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (input, ghostData, weaponState, owner, entity) in SystemAPI
            .Query<RefRO<PlayerInput>, RefRO<PlayerGhostData>, RefRW<WeaponState>, RefRO<GhostOwner>>()
            .WithEntityAccess()
            .WithAll<Simulate>())
        {
            // 쿨다운 감소
            if (weaponState.ValueRO.Cooldown > 0f)
            {
                weaponState.ValueRW.Cooldown -= dt;
                continue;
            }

            if (!input.ValueRO.Shoot.IsSet) continue;
            if (ghostData.ValueRO.AmmoCount <= 0) continue;
            if (ghostData.ValueRO.IsDead) continue;

            // 투사체 생성
            var bullet = ecb.Instantiate(prefabs.BulletPrefab);

            const float BulletSpeed = 25f;
            const float MuzzleOffset = 0.6f;
            var dir = new float2(math.cos(input.ValueRO.AimAngle), math.sin(input.ValueRO.AimAngle));
            var spawnPos = ghostData.ValueRO.Position + dir * MuzzleOffset;

            ecb.SetComponent(bullet, new BulletGhostData
            {
                Position = spawnPos,
                Velocity = dir * BulletSpeed,
                OwnerNetworkId = owner.ValueRO.NetworkId,
                Damage = 25f,
                BulletType = 0
            });
            ecb.SetComponent(bullet, LocalTransform.FromPosition(new float3(spawnPos.x, spawnPos.y, 0f)));
            ecb.SetComponent(bullet, new BulletLifetime { Value = 3f });

            // 발사 후 처리
            weaponState.ValueRW.Cooldown = 0.2f; // 기본 연사 간격
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
