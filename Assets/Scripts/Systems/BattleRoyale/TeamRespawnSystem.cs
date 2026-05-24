using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 서버 전용 — 플레이어 부활 대기 시간 추적. Ghost에 포함하지 않아 서버 로컬로만 존재.
public struct PlayerRespawnTimer : IComponentData
{
    public float TimeRemaining;
}

// 서버 전용 — 2v2 팀원 부활 처리.
// 조건: 팀에 살아있는 팀원이 1명 이상 존재하고, 사망 플레이어의 RespawnCount > 0.
// 사망 후 RESPAWN_DELAY 초 뒤에 팀 스폰 포인트 중 비어 있는 위치에 부활.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(ArenaMatchSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct TeamRespawnSystem : ISystem
{
    const float RespawnDelay = 5f;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ArenaMatchSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<ArenaMatchSingleton>(out var match)) return;
        if (match.GameEnded) return;

        float dt = SystemAPI.Time.DeltaTime;

        // 팀별 생존 여부 집계 (IsDead=false 인 플레이어만 카운트)
        bool team0Alive = false, team1Alive = false;
        foreach (var ghost in SystemAPI.Query<RefRO<PlayerGhostData>>())
        {
            if (ghost.ValueRO.IsDead) continue;
            if (ghost.ValueRO.TeamId == 0) team0Alive = true;
            else team1Alive = true;
        }

        // 스폰 포인트를 팀별로 미리 수집 (nested Query 방지)
        var team0Spawns = new NativeList<float2>(4, Allocator.Temp);
        var team1Spawns = new NativeList<float2>(4, Allocator.Temp);
        foreach (var sp in SystemAPI.Query<RefRO<SpawnPointData>>())
        {
            if (sp.ValueRO.SpawnPointId % 2 == 0)
                team0Spawns.Add(sp.ValueRO.Position);
            else
                team1Spawns.Add(sp.ValueRO.Position);
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 패스 1: 새로 사망 + 부활 조건 충족 → 타이머 컴포넌트 추가
        foreach (var (ghost, entity) in SystemAPI
            .Query<RefRO<PlayerGhostData>>()
            .WithNone<PlayerRespawnTimer>()
            .WithEntityAccess())
        {
            if (!ghost.ValueRO.IsDead) continue;
            if (ghost.ValueRO.RespawnCount <= 0) continue;

            bool teammateAlive = ghost.ValueRO.TeamId == 0 ? team0Alive : team1Alive;
            if (!teammateAlive) continue;

            ecb.AddComponent(entity, new PlayerRespawnTimer { TimeRemaining = RespawnDelay });
        }

        // 패스 2: 타이머 감소 → 만료 시 부활 처리
        foreach (var (ghost, timer, transform, entity) in SystemAPI
            .Query<RefRW<PlayerGhostData>, RefRW<PlayerRespawnTimer>, RefRW<LocalTransform>>()
            .WithEntityAccess())
        {
            timer.ValueRW.TimeRemaining -= dt;
            if (timer.ValueRO.TimeRemaining > 0f) continue;

            var spawns = ghost.ValueRO.TeamId == 0 ? team0Spawns : team1Spawns;
            float2 spawnPos = spawns.Length > 0 ? spawns[0] : float2.zero;

            ghost.ValueRW.IsDead        = false;
            ghost.ValueRW.Health        = 100f;
            ghost.ValueRW.RespawnCount -= 1;
            ghost.ValueRW.Position      = spawnPos;
            transform.ValueRW           = LocalTransform.FromPosition(new float3(spawnPos.x, spawnPos.y, 0f));

            ecb.RemoveComponent<PlayerRespawnTimer>(entity);
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();

        team0Spawns.Dispose();
        team1Spawns.Dispose();
    }
}
