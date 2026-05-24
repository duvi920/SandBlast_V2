using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;

// 서버 전용 — 매 프레임 생존 플레이어 수를 집계해 1명이면 우승자를 기록하고, 0명이면 무승부로 게임을 종료한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct SurvivalCheckSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<RoomStateSingleton>(out var room))
            return;
        if (room.Phase != RoomPhase.Battle)
            return;

        if (!SystemAPI.TryGetSingletonRW<GameResultSingleton>(out var result))
            return;

        if (result.ValueRO.GameEnded)
            return;

        int aliveCount = 0;
        int lastAliveNetworkId = -1;

        foreach (var (ghostData, owner) in SystemAPI
            .Query<RefRO<PlayerGhostData>, RefRO<GhostOwner>>())
        {
            if (!ghostData.ValueRO.IsDead)
            {
                aliveCount++;
                lastAliveNetworkId = owner.ValueRO.NetworkId;
            }
        }

        if (aliveCount == 1)
        {
            result.ValueRW.GameEnded = true;
            result.ValueRW.WinnerNetworkId = lastAliveNetworkId;
            result.ValueRW.IsDraw = false;
            UnityEngine.Debug.Log($"[Server] Game Over — Winner: NetworkId {lastAliveNetworkId}");
        }
        else if (aliveCount == 0)
        {
            result.ValueRW.GameEnded = true;
            result.ValueRW.IsDraw = true;
            UnityEngine.Debug.Log("[Server] Game Over — Draw");
        }
    }
}
