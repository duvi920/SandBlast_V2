using Unity.Entities;
using Unity.NetCode;

// [비활성화] 호스트 멀티 전환으로 LobbySystem이 이 시스템을 대체한다.
// LobbySystem은 MinPlayers~MaxPlayers 범위의 인원을 지원하며,
// RoomStateSingleton.MinPlayers / MaxPlayers 필드를 통해 Inspector에서 조절 가능하다.
[DisableAutoCreation]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class RoomWatchSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<RoomStateSingleton>();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonRW<RoomStateSingleton>(out var room))
            return;

        int count = 0;
        foreach (var _ in SystemAPI.Query<RefRO<NetworkStreamInGame>>())
            count++;

        int prev = room.ValueRO.LastPlayerCount;
        room.ValueRW.LastPlayerCount = count;

        // 1 → 2: 리셋 트리거
        if (prev < 2 && count >= 2 && room.ValueRO.Phase == RoomPhase.Solo)
        {
            room.ValueRW.Phase = RoomPhase.Countdown;
            room.ValueRW.CountdownTimer = 3f;

            var resetEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<RoomResetRequestTag>(resetEntity);

            UnityEngine.Debug.Log("[RoomWatch] 2번째 플레이어 접속 — 방 리셋 요청");
            return;
        }

        // Countdown 중 한 명이 나감 → Solo로 복귀
        if (count < 2 && room.ValueRO.Phase == RoomPhase.Countdown)
        {
            room.ValueRW.Phase = RoomPhase.Solo;
            room.ValueRW.CountdownTimer = 3f;
            UnityEngine.Debug.Log("[RoomWatch] Countdown 중 접속 끊김 — Solo 복귀");
        }
    }
}
