using SandBlast;
using SandBlast.Arena;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 서버 전용 — RoomResetRequestTag가 존재할 때 맵·플레이어·매치 상태를 초기화한다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(ArenaMatchSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class RoomResetSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<RoomResetRequestTag>();
        RequireForUpdate<ArenaMatchSingleton>();
        RequireForUpdate<PixelGridSingleton>();
        RequireForUpdate<ChunkManagerSingleton>();
    }

    protected override void OnUpdate()
    {
        // ── 1. 맵 재생성 ─────────────────────────────────────────────
        var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
        var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();

        ClearGrid(ref grid, ref chunks);

        var templateQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MapTemplateReference>());
        if (!templateQuery.IsEmpty)
        {
            var templateRef = SystemAPI.ManagedAPI.GetSingleton<MapTemplateReference>();
            if (templateRef?.Value != null)
                ArenaMapBuilder.Apply(templateRef.Value, ref grid, ref chunks);
            else
                BuildDefaultFloor(ref grid, ref chunks);
        }
        else
        {
            BuildDefaultFloor(ref grid, ref chunks);
        }
        templateQuery.Dispose();

        SystemAPI.SetSingleton(grid);
        SystemAPI.SetSingleton(chunks);

        // ── 2. ArenaMatchSingleton 리셋 ──────────────────────────────
        if (SystemAPI.TryGetSingletonRW<ArenaMatchSingleton>(out var match))
        {
            float duration = match.ValueRO.MatchDuration;
            float lavaSpeed = match.ValueRO.LavaRiseSpeed;
            match.ValueRW = new ArenaMatchSingleton
            {
                TimeRemaining  = duration,
                MatchDuration  = duration,
                ScoreTeam0     = 0,
                ScoreTeam1     = 0,
                GameEnded      = false,
                WinnerTeamId   = -1,
                LavaRiseActive = false,
                LavaRiseY      = 0f,
                LavaRiseSpeed  = lavaSpeed,
            };
        }

        // ── 3. GameResultSingleton 리셋 ──────────────────────────────
        if (SystemAPI.TryGetSingletonRW<GameResultSingleton>(out var result))
        {
            result.ValueRW = new GameResultSingleton
            {
                WinnerNetworkId = -1,
                GameEnded       = false,
                IsDraw          = false,
            };
        }

        // ── 4. 플레이어 상태 리셋 ────────────────────────────────────
        // 스폰 포인트 수집
        var spawnPositions = new NativeList<float2>(8, Allocator.Temp);
        foreach (var sp in SystemAPI.Query<RefRO<SpawnPointData>>())
            spawnPositions.Add(sp.ValueRO.Position);

        int spawnIdx = 0;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (ghost, transform, entity) in SystemAPI
            .Query<RefRW<PlayerGhostData>, RefRW<LocalTransform>>()
            .WithEntityAccess())
        {
            float2 pos = spawnIdx < spawnPositions.Length
                ? spawnPositions[spawnIdx++]
                : float2.zero;

            ghost.ValueRW.Health    = 100f;
            ghost.ValueRW.IsDead    = false;
            ghost.ValueRW.Velocity  = float2.zero;
            ghost.ValueRW.Position  = pos;
            transform.ValueRW       = LocalTransform.FromPosition(new float3(pos.x, pos.y, 0f));

            // 부활 타이머 제거
            if (EntityManager.HasComponent<PlayerRespawnTimer>(entity))
                ecb.RemoveComponent<PlayerRespawnTimer>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
        spawnPositions.Dispose();

        // ── 5. RoomResetRpc 브로드캐스트 ────────────────────────────
        BroadcastResetRpc();

        // ── 6. RoomResetRequestTag 제거 ──────────────────────────────
        var tagQuery = EntityManager.CreateEntityQuery(ComponentType.ReadWrite<RoomResetRequestTag>());
        EntityManager.DestroyEntity(tagQuery);
        tagQuery.Dispose();

        UnityEngine.Debug.Log("[RoomReset] 방 리셋 완료");
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    static void ClearGrid(ref PixelGridSingleton grid, ref ChunkManagerSingleton chunks)
    {
        int total = grid.Width * grid.Height;
        for (int i = 0; i < total; i++)
        {
            grid.Type[i]         = (byte)CellType.EMPTY;
            grid.Temperature[i]  = 0;
            grid.Flammability[i] = 0;
            grid.Lifetime[i]     = 0;
        }
        // 변경된 청크를 전부 더티로 표시
        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width;  x++)
            chunks.MarkMoved(x, y);
    }

    static void BuildDefaultFloor(ref PixelGridSingleton grid, ref ChunkManagerSingleton chunks)
    {
        int w = grid.Width, h = grid.Height;
        for (int x = 0; x < w; x++)
        for (int y = 0; y < 4; y++)
        {
            int idx = grid.Index(x, y);
            grid.Type[idx]         = (byte)CellType.SOLID_STATIC;
            grid.Flammability[idx] = 0;
            chunks.MarkMoved(x, y);
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < 2; x++)
            { grid.Type[grid.Index(x, y)] = (byte)CellType.SOLID_STATIC; chunks.MarkMoved(x, y); }
            for (int x = w - 2; x < w; x++)
            { grid.Type[grid.Index(x, y)] = (byte)CellType.SOLID_STATIC; chunks.MarkMoved(x, y); }
        }
    }

    void BroadcastResetRpc()
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkStreamInGame>>().WithEntityAccess())
        {
            var rpcEnt = ecb.CreateEntity();
            ecb.AddComponent<RoomResetRpc>(rpcEnt);
            ecb.AddComponent(rpcEnt, new SendRpcCommandRequest { TargetConnection = entity });
        }
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
