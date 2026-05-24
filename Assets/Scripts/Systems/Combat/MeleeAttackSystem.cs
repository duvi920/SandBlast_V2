using SandBlast;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — MeleeAttack 입력 시 근거리 적에게 HitEvent를 추가한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(HitDetectionSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct MeleeAttackSystem : ISystem
{
    private const float MeleeRange = 0.9f;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        var playerQuery = SystemAPI.QueryBuilder()
            .WithAll<PlayerGhostData>()
            .Build();

        var playerEntities  = playerQuery.ToEntityArray(Allocator.Temp);
        var playerDataArray = playerQuery.ToComponentDataArray<PlayerGhostData>(Allocator.Temp);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (input, ghostData, stun, attackerEntity) in SystemAPI
            .Query<RefRO<PlayerInput>, RefRO<PlayerGhostData>, RefRW<StunStateComponent>>()
            .WithAll<Simulate>()
            .WithEntityAccess())
        {
            if (ghostData.ValueRO.IsDead || ghostData.ValueRO.IsStunned) continue;
            if (!input.ValueRO.MeleeAttack.IsSet) continue;

            var s = stun.ValueRO;
            s.MeleeAttackCooldown -= dt;
            if (s.MeleeAttackCooldown > 0f)
            {
                stun.ValueRW = s;
                continue;
            }

            s.MeleeAttackCooldown = SimulationConstants.MeleeAttackCooldown;
            stun.ValueRW = s;

            float2 attackerPos = ghostData.ValueRO.Position;

            for (int i = 0; i < playerEntities.Length; i++)
            {
                if (playerEntities[i] == attackerEntity) continue;
                var target = playerDataArray[i];
                if (target.IsDead) continue;

                if (math.distance(attackerPos, target.Position) > MeleeRange) continue;

                if (!state.EntityManager.HasBuffer<HitEvent>(playerEntities[i]))
                    ecb.AddBuffer<HitEvent>(playerEntities[i]);

                ecb.AppendToBuffer(playerEntities[i], new HitEvent
                {
                    TargetEntity = playerEntities[i],
                    Damage       = SimulationConstants.MeleeDamage,
                    HitPosition  = target.Position,
                    StunPower    = SimulationConstants.MeleeStunPower,
                });

                break; // 한 번에 한 명
            }
        }

        playerEntities.Dispose();
        playerDataArray.Dispose();

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
