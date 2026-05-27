using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 서버 전용 — Co-op 부활 시스템 (호스트 멀티용).
// ArenaMatchSingleton이 없을 때만 활성화되어 PvP의 TeamRespawnSystem과 자동으로 배타적이다.
// 살아있는 플레이어가 1명 이상이면 사망 후 CoopRespawnDelay 초 뒤 해당 위치 옆에 부활.
// 전원 사망 시 Solo Phase로 복귀해 방을 리셋한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct CoopRespawnSystem : ISystem
{
    const float CoopRespawnDelay = 10f;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<RoomStateSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // PvP 아레나 모드(ArenaMatchSingleton 존재)에서는 TeamRespawnSystem이 담당
        if (SystemAPI.HasSingleton<ArenaMatchSingleton>()) return;

        if (!SystemAPI.TryGetSingleton<RoomStateSingleton>(out var room)) return;
        if (room.Phase != RoomPhase.Battle) return;

        float dt = SystemAPI.Time.DeltaTime;

        // 살아있는 플레이어 위치 수집
        bool   anyAlive  = false;
        bool   anyDead   = false;
        float2 alivePos  = float2.zero;

        foreach (var ghost in SystemAPI.Query<RefRO<PlayerGhostData>>())
        {
            if (ghost.ValueRO.IsDead) { anyDead  = true; continue; }
            anyAlive = true;
            alivePos = ghost.ValueRO.Position;
        }

        // 전원 사망 → 방 리셋
        if (anyDead && !anyAlive)
        {
            if (SystemAPI.TryGetSingletonRW<RoomStateSingleton>(out var roomRW))
                roomRW.ValueRW.Phase = RoomPhase.Solo;

            var resetTag = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<RoomResetRequestTag>(resetTag);

            UnityEngine.Debug.Log("[CoopRespawn] 전원 사망 — 방 리셋");
            return;
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 패스 1: 사망 플레이어 중 부활 가능한 경우 타이머 추가
        foreach (var (ghost, entity) in SystemAPI
            .Query<RefRO<PlayerGhostData>>()
            .WithNone<PlayerRespawnTimer>()
            .WithEntityAccess())
        {
            if (!ghost.ValueRO.IsDead || !anyAlive) continue;
            if (ghost.ValueRO.RespawnCount <= 0)    continue;

            ecb.AddComponent(entity, new PlayerRespawnTimer { TimeRemaining = CoopRespawnDelay });
            UnityEngine.Debug.Log($"[CoopRespawn] 부활 타이머 시작 — {CoopRespawnDelay}초 후 부활");
        }

        // 패스 2: 타이머 만료 시 살아있는 플레이어 옆에 부활
        foreach (var (ghost, timer, transform, entity) in SystemAPI
            .Query<RefRW<PlayerGhostData>, RefRW<PlayerRespawnTimer>, RefRW<LocalTransform>>()
            .WithEntityAccess())
        {
            timer.ValueRW.TimeRemaining -= dt;
            if (timer.ValueRO.TimeRemaining > 0f) continue;

            // 살아있는 플레이어 옆 1.5 유닛에 부활
            float2 spawnPos = alivePos + new float2(1.5f, 0f);

            ghost.ValueRW.IsDead        = false;
            ghost.ValueRW.Health        = 100f;
            ghost.ValueRW.RespawnCount -= 1;
            ghost.ValueRW.Position      = spawnPos;
            transform.ValueRW           = LocalTransform.FromPosition(new float3(spawnPos.x, spawnPos.y, 0f));

            ecb.RemoveComponent<PlayerRespawnTimer>(entity);
            UnityEngine.Debug.Log("[CoopRespawn] 플레이어 부활");
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
