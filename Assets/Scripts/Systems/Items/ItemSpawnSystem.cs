using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 서버 전용 — 게임 시작 시 한 번만 실행되어 SpawnPoint마다 무작위로 무기 또는 회복 아이템을 배치한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ItemSpawnSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<GamePrefabs>();
        state.RequireForUpdate<SpawnPointData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        // 게임 시작 시 한 번만 실행
        state.Enabled = false;

        if (!SystemAPI.TryGetSingleton<GamePrefabs>(out var prefabs))
            return;

        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        var random = new Random(0xACE); // 고정 시드 사용 (결정론적 배치)

        bool hasShovel = prefabs.ShovelItemPrefab != Unity.Entities.Entity.Null;

        foreach (var spawnPoint in SystemAPI.Query<RefRO<SpawnPointData>>())
        {
            // 0=무기, 1=회복, 2=삽 (완드는 MapLoadSystem에서 MapTemplate 기반으로 별도 배치)
            int typeRange = hasShovel ? 3 : 2;
            var itemType = random.NextInt(0, typeRange);
            var prefab = itemType == 0 ? prefabs.WeaponItemPrefab
                       : itemType == 1 ? prefabs.HealItemPrefab
                       : prefabs.ShovelItemPrefab;

            var item = ecb.Instantiate(prefab);
            var pos = spawnPoint.ValueRO.Position;
            ecb.SetComponent(item, new ItemGhostData
            {
                Position = pos,
                ItemType = itemType,
                ItemId = random.NextInt(0, 3),
                IsPickedUp = false
            });
            ecb.SetComponent(item, LocalTransform.FromPosition(new float3(pos.x, pos.y, 0f)));
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
