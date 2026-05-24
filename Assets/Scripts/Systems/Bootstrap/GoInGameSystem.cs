using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using SandBlast;
using SandBlast.Components;
using SandBlast.Wand;

// 클라이언트 및 서버 — 새 클라이언트 접속 시:
//   1. NetworkStreamInGame 추가 (고스트·RPC 활성화)
//   2. 서버의 경우: 플레이어 스폰 및 권한 할당
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
public partial struct GoInGameSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 맵이 준비되기 전에는 게임에 진입하지 않는다
        if (!SystemAPI.HasSingleton<MapReadyTag>()) return;

        var em  = state.EntityManager;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // NetworkId가 생성되었지만 아직 InGame 상태가 아닌 연결 엔티티를 찾음
        foreach (var (netId, entity) in SystemAPI
            .Query<RefRO<NetworkId>>()
            .WithNone<NetworkStreamInGame>()
            .WithEntityAccess())
        {
            ecb.AddComponent<NetworkStreamInGame>(entity);
            UnityEngine.Debug.Log($"[GoInGame] Added NetworkStreamInGame to connection {entity} (World: {state.WorldUnmanaged.Name})");

            // 서버일 경우에만 플레이어 스폰 로직 실행
            if (state.WorldUnmanaged.IsServer())
            {
                int clientNetId = netId.ValueRO.Value;
                AssignOrSpawnPlayer(ref state, ecb, clientNetId, entity);
            }
        }

        ecb.Playback(em);
        ecb.Dispose();
    }

    void AssignOrSpawnPlayer(ref SystemState state, EntityCommandBuffer ecb, int clientNetId, Entity connectionEntity)
    {
        var em = state.EntityManager;

        // 이미 이 클라이언트에 할당된 플레이어가 있으면 스킵
        foreach (var (owner, playerEntity) in SystemAPI
            .Query<RefRO<GhostOwner>>()
            .WithAll<PlayerTag>()
            .WithEntityAccess())
        {
            if (owner.ValueRO.NetworkId == clientNetId)
            {
                // CommandTarget 재설정 (재접속 대비)
                ecb.SetComponent(connectionEntity, new CommandTarget { targetEntity = playerEntity });
                return;
            }
        }

        // GhostOwner.NetworkId == 0 인 미소유 플레이어(씬 베이킹)에 먼저 할당
        foreach (var (owner, playerEntity) in SystemAPI
            .Query<RefRW<GhostOwner>>()
            .WithAll<PlayerTag>()
            .WithEntityAccess())
        {
            if (owner.ValueRO.NetworkId == 0)
            {
                owner.ValueRW.NetworkId = clientNetId;
                ecb.SetComponent(connectionEntity, new CommandTarget { targetEntity = playerEntity });
                UnityEngine.Debug.Log($"[GoInGame] Assigned baked player to client {clientNetId}");
                return;
            }
        }

        // 미소유 플레이어가 없으면 PlayerPrefab으로 새 플레이어 스폰
        if (!SystemAPI.TryGetSingleton<GamePrefabs>(out var prefabs))
        {
            UnityEngine.Debug.LogError("[GoInGame] GamePrefabs singleton not found!");
            return;
        }

        if (prefabs.PlayerPrefab == Entity.Null)
        {
            UnityEngine.Debug.LogError("[GoInGame] GamePrefabs.PlayerPrefab이 null!");
            return;
        }

        var player = ecb.Instantiate(prefabs.PlayerPrefab);
        ecb.SetComponent(player, new GhostOwner { NetworkId = clientNetId });
        ecb.SetComponent(player, LocalTransform.FromPosition(new float3(0f, 4f, 0f)));
        
        // 플레이어 이동 및 상태에 필요한 컴포넌트 추가
        ecb.AddComponent(player, default(PlayerInput));
        ecb.AddComponent(player, default(PlayerLocalState));
        ecb.AddComponent(player, default(GroundedState));
        ecb.AddComponent(player, new PlayerMovementData { Value = PlayerMovementState.Idle, IsGrounded = false });
        
        // IInputComponentData가 올바른 엔티티에 전달되도록 CommandTarget 설정
        ecb.SetComponent(connectionEntity, new CommandTarget { targetEntity = player });
        UnityEngine.Debug.Log($"[GoInGame] Spawned player for client {clientNetId}");
    }
}
