using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

// 서버 전용 — Countdown 단계에서 3·2·1·GO RPC를 브로드캐스트하고, 0이 되면 Battle로 전환한다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(RoomResetSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class MatchCountdownSystem : SystemBase
{
    int _lastBroadcastCount = -1;

    protected override void OnCreate()
    {
        RequireForUpdate<RoomStateSingleton>();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonRW<RoomStateSingleton>(out var room))
            return;
        if (room.ValueRO.Phase != RoomPhase.Countdown)
        {
            _lastBroadcastCount = -1;
            return;
        }

        // RoomResetRequestTag가 아직 처리 중이면 대기
        if (SystemAPI.HasSingleton<RoomResetRequestTag>())
            return;

        room.ValueRW.CountdownTimer -= SystemAPI.Time.DeltaTime;
        float timer = room.ValueRO.CountdownTimer;

        // 매초 내림 정수가 바뀔 때 RPC 전송
        int currentCount = (int)System.Math.Ceiling((double)System.Math.Max(timer, 0f));
        if (currentCount != _lastBroadcastCount)
        {
            _lastBroadcastCount = currentCount;
            BroadcastCountdown(currentCount);
            UnityEngine.Debug.Log($"[Countdown] {(currentCount == 0 ? "GO!" : currentCount.ToString())}");
        }

        if (timer <= 0f)
        {
            room.ValueRW.Phase = RoomPhase.Battle;
            _lastBroadcastCount = -1;
            UnityEngine.Debug.Log("[Countdown] 대전 시작 — Phase = Battle");
        }
    }

    void BroadcastCountdown(int count)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkStreamInGame>>().WithEntityAccess())
        {
            var rpcEnt = ecb.CreateEntity();
            ecb.AddComponent(rpcEnt, new MatchCountdownRpc { Count = count });
            ecb.AddComponent(rpcEnt, new SendRpcCommandRequest { TargetConnection = entity });
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
