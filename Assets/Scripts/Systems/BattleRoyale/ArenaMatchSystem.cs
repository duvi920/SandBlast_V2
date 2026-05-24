using SandBlast;
using SandBlast.Arena;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — 아레나 매치 타이머, 용암 상승, 승패 판정을 담당한다.
// MapMode에 따라 1v1(최후 1인) / 2v2(팀 전멸) 규칙을 적용한다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(DamageSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class ArenaMatchSystem : SystemBase
{
    // 매치 상태 동기화 RPC 전송 주기 (초)
    const float TickSyncInterval = 2f;
    float _syncTimer;

    protected override void OnCreate()
    {
        RequireForUpdate<ArenaMatchSingleton>();
        RequireForUpdate<PixelGridSingleton>();
        RequireForUpdate<ChunkManagerSingleton>();
        RequireForUpdate<RoomStateSingleton>();
    }

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingleton<RoomStateSingleton>(out var room))
            return;
        if (room.Phase != RoomPhase.Battle)
            return;

        if (!SystemAPI.TryGetSingletonRW<ArenaMatchSingleton>(out var match))
            return;

        if (match.ValueRO.GameEnded)
            return;

        float dt = SystemAPI.Time.DeltaTime;

        // 1. 타이머 감소
        match.ValueRW.TimeRemaining -= dt;

        // 2. 용암 상승 활성화 — 남은 시간이 절반 이하일 때
        if (!match.ValueRO.LavaRiseActive &&
            match.ValueRO.TimeRemaining <= match.ValueRO.MatchDuration * 0.5f)
            match.ValueRW.LavaRiseActive = true;

        // 3. 용암 상승 처리
        if (match.ValueRO.LavaRiseActive)
        {
            float newLava = match.ValueRO.LavaRiseY + match.ValueRO.LavaRiseSpeed * dt;
            match.ValueRW.LavaRiseY = newLava;
            ApplyLavaRise((int)math.floor(newLava));
        }

        // 4. 승패 판정
        CheckWinCondition(ref match.ValueRW);

        // 5. 시간 초과
        if (!match.ValueRO.GameEnded && match.ValueRO.TimeRemaining <= 0f)
            ResolveByScore(ref match.ValueRW);

        // 6. RPC 주기 동기화
        _syncTimer -= dt;
        if (_syncTimer <= 0f)
        {
            _syncTimer = TickSyncInterval;
            BroadcastTickRpc(in match.ValueRO);
        }

        // 7. 종료 시 결과 RPC 전송
        if (match.ValueRO.GameEnded)
            BroadcastEndRpc(in match.ValueRO);
    }

    // ── 용암 상승 ────────────────────────────────────────────────────

    void ApplyLavaRise(int lavaPixelY)
    {
        if (lavaPixelY <= 0) return;

        var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
        var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();

        // 바닥에서 lavaPixelY 높이까지 한 줄씩 채운다 (바닥 고정 셀 아래는 건너뜀)
        int rowY = lavaPixelY;
        if (!grid.InBounds(0, rowY)) return;

        for (int x = 0; x < grid.Width; x++)
        {
            if (!grid.InBounds(x, rowY)) continue;
            int idx = grid.Index(x, rowY);
            var ct  = (CellType)grid.Type[idx];
            if (ct == CellType.EMPTY || ct == CellType.LIQUID_WATER || ct == CellType.LIQUID_POISON)
            {
                grid.Type[idx]         = (byte)CellType.LIQUID_LAVA;
                grid.Temperature[idx]  = SimulationConstants.LAVA_INITIAL_TEMP;
                grid.Flammability[idx] = 0;
                grid.Lifetime[idx]     = 0;
                chunks.MarkMoved(x, rowY);
            }
        }

        SystemAPI.SetSingleton(grid);
        SystemAPI.SetSingleton(chunks);
    }

    // ── 승패 판정 ────────────────────────────────────────────────────

    void CheckWinCondition(ref ArenaMatchSingleton match)
    {
        // MapTemplateReference 로 MapMode를 읽는다 (없으면 1v1 규칙 적용)
        var mapMode = MapMode.OneVsOne;
        if (SystemAPI.ManagedAPI.TryGetSingleton<MapTemplateReference>(out var tmRef) && tmRef?.Value != null)
            mapMode = tmRef.Value.Mode;

        if (mapMode == MapMode.OneVsOne)
            Check1v1(ref match);
        else
            Check2v2(ref match);
    }

    void Check1v1(ref ArenaMatchSingleton match)
    {
        int alive = 0, lastTeam = -1;
        foreach (var (ghost, owner) in SystemAPI.Query<RefRO<PlayerGhostData>, RefRO<GhostOwner>>())
        {
            if (!ghost.ValueRO.IsDead) { alive++; lastTeam = ghost.ValueRO.TeamId; }
        }

        if (alive == 1)
        {
            match.GameEnded = true;
            match.WinnerTeamId = lastTeam;
        }
        else if (alive == 0)
        {
            match.GameEnded = true;
            match.WinnerTeamId = 2; // 무승부
        }
    }

    void Check2v2(ref ArenaMatchSingleton match)
    {
        bool team0Alive = false, team1Alive = false;
        foreach (var ghost in SystemAPI.Query<RefRO<PlayerGhostData>>())
        {
            if (ghost.ValueRO.IsDead) continue;
            if (ghost.ValueRO.TeamId == 0) team0Alive = true;
            else team1Alive = true;
        }

        if (!team0Alive && !team1Alive)
        { match.GameEnded = true; match.WinnerTeamId = 2; }
        else if (!team0Alive)
        { match.GameEnded = true; match.WinnerTeamId = 1; match.ScoreTeam1++; }
        else if (!team1Alive)
        { match.GameEnded = true; match.WinnerTeamId = 0; match.ScoreTeam0++; }
    }

    void ResolveByScore(ref ArenaMatchSingleton match)
    {
        match.GameEnded = true;
        if (match.ScoreTeam0 > match.ScoreTeam1) match.WinnerTeamId = 0;
        else if (match.ScoreTeam1 > match.ScoreTeam0) match.WinnerTeamId = 1;
        else match.WinnerTeamId = 2; // 무승부
    }

    // ── RPC 전송 ─────────────────────────────────────────────────────

    void BroadcastTickRpc(in ArenaMatchSingleton match)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkStreamInGame>>().WithEntityAccess())
        {
            var rpcEnt = ecb.CreateEntity();
            ecb.AddComponent(rpcEnt, new ArenaMatchTickRpc
            {
                TimeRemaining = match.TimeRemaining,
                ScoreTeam0    = match.ScoreTeam0,
                ScoreTeam1    = match.ScoreTeam1,
                LavaRiseY     = match.LavaRiseY,
            });
            ecb.AddComponent(rpcEnt, new SendRpcCommandRequest { TargetConnection = entity });
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    void BroadcastEndRpc(in ArenaMatchSingleton match)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkStreamInGame>>().WithEntityAccess())
        {
            var rpcEnt = ecb.CreateEntity();
            ecb.AddComponent(rpcEnt, new ArenaMatchEndRpc
            {
                WinnerTeamId = match.WinnerTeamId,
                ScoreTeam0   = match.ScoreTeam0,
                ScoreTeam1   = match.ScoreTeam1,
                IsDraw       = match.WinnerTeamId == 2,
            });
            ecb.AddComponent(rpcEnt, new SendRpcCommandRequest { TargetConnection = entity });
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
