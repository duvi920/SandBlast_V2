using Unity.Entities;
using Unity.NetCode;

// 서버 전용 — 호스트 멀티 로비 관리 시스템.
// RoomWatchSystem을 대체하며 2~MaxPlayers 인원을 지원한다.
// MinPlayers 이상 접속 시 카운트다운을 시작하고 Battle Phase로 진입한다.
// MaxPlayers 초과 접속은 서버에서 거부하거나 스펙테이터로 처리할 수 있다(추후 구현).
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class LobbySystem : SystemBase
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

        int minPlayers = room.ValueRO.MinPlayers > 0 ? room.ValueRO.MinPlayers : 2;

        // Solo 상태에서 최소 인원 충족 → Countdown 전환 + 맵 리셋
        if (prev < minPlayers && count >= minPlayers && room.ValueRO.Phase == RoomPhase.Solo)
        {
            room.ValueRW.Phase         = RoomPhase.Countdown;
            room.ValueRW.CountdownTimer = 3f;

            var resetEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<RoomResetRequestTag>(resetEntity);

            UnityEngine.Debug.Log($"[Lobby] {count}/{(room.ValueRO.MaxPlayers > 0 ? room.ValueRO.MaxPlayers : 4)}명 접속 — 카운트다운 시작");
            return;
        }

        // Countdown 중 최소 인원 미달 → Solo 복귀
        if (count < minPlayers && room.ValueRO.Phase == RoomPhase.Countdown)
        {
            room.ValueRW.Phase         = RoomPhase.Solo;
            room.ValueRW.CountdownTimer = 3f;
            UnityEngine.Debug.Log("[Lobby] 인원 부족 — 대기 상태로 복귀");
        }

        // Battle 중 전원 퇴장 → Solo 복귀
        if (count == 0 && room.ValueRO.Phase == RoomPhase.Battle)
        {
            room.ValueRW.Phase = RoomPhase.Solo;
            UnityEngine.Debug.Log("[Lobby] 모든 플레이어 퇴장 — 대기 상태로 복귀");
        }
    }
}
