using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using SandBlast;

// 서버 전용 — HitEvent의 StunPower를 소비해 스턴 게이지를 누적하고, 기절 타이머를 갱신한다.
// DamageSystem이 HitEvent 버퍼를 지우기 전에 실행해야 한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(HitDetectionSystem))]
[UpdateBefore(typeof(DamageSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct StunSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (ghostData, stun, hitBuffer) in SystemAPI
            .Query<RefRW<PlayerGhostData>, RefRW<StunStateComponent>, DynamicBuffer<HitEvent>>()
            .WithAll<Simulate>())
        {
            if (ghostData.ValueRO.IsDead) continue;

            var s = stun.ValueRO;

            // 기절 타이머 감소
            if (s.StunTimer > 0f)
            {
                s.StunTimer -= dt;
                if (s.StunTimer <= 0f)
                {
                    s.StunTimer = 0f;
                    ghostData.ValueRW.IsStunned = false;
                }
            }

            // 기절 중이 아닐 때만 게이지 자동 감소
            if (!ghostData.ValueRO.IsStunned)
                s.StunAccumulator = Unity.Mathematics.math.max(0f, s.StunAccumulator - SimulationConstants.StunDecayRate * dt);

            // HitEvent에서 StunPower 누적
            for (int i = 0; i < hitBuffer.Length; i++)
            {
                float power = hitBuffer[i].StunPower;
                if (power <= 0f) continue;

                s.StunAccumulator += power;

                if (s.StunAccumulator >= SimulationConstants.StunThreshold && !ghostData.ValueRO.IsStunned)
                {
                    s.StunAccumulator = 0f;
                    s.StunTimer = SimulationConstants.StunDuration;
                    ghostData.ValueRW.IsStunned = true;
                }
            }

            stun.ValueRW = s;
        }
    }
}
