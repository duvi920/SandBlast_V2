using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using SandBlast.Components;

// 클라이언트+서버 예측 — 공중에 있는 플레이어에 중력(30 m/s²)을 적용하고 최대 낙하 속도를 제한한다.
[BurstCompile]
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
public partial struct GravitySystem : ISystem
{
    private const float Gravity = 30f;
    private const float MaxFallSpeed = -20f;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var dt = SystemAPI.Time.DeltaTime;

        foreach (var (ghostData, groundedState) in SystemAPI
            .Query<RefRW<PlayerGhostData>, RefRO<GroundedState>>()
            .WithAll<PlayerTag>())
        {
            // vel.y <= 0f: 착지 직후 vel.y = 0일 때도 중력을 건너뛰어 불필요한 침투를 방지
            if (groundedState.ValueRO.IsGrounded && ghostData.ValueRO.Velocity.y <= 0f)
                continue;

            var vel = ghostData.ValueRO.Velocity;
            vel.y -= Gravity * dt;
            vel.y = math.max(vel.y, MaxFallSpeed);
            ghostData.ValueRW.Velocity = vel;
        }
    }
}
