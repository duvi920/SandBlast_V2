using Unity.Entities;
using Unity.NetCode;

// 서버 전용 — 매 프레임 InGame 접속자 수를 감시한다.
// 1 → 2 전환: RoomResetRequestTag 생성 + Phase = Countdown
// 2 → 1 전환 (Countdown 중 나감): 카운트다운 취소 → Phase = Solo
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
