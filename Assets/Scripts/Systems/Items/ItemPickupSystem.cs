using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — Interact 입력이 들어오면 PickupRange(1.5m) 내 아이템을 획득한다.
// 무기는 WeaponId·탄약을 갱신하고, 회복 아이템은 HP를 최대 100까지 회복한다.
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct ItemPickupSystem : ISystem
{
    private const float PickupRange = 1.5f;
    private const float MaxHealth = 100f;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (playerInput, playerGhost, playerEntity) in SystemAPI
            .Query<RefRO<PlayerInput>, RefRW<PlayerGhostData>>()
            .WithEntityAccess()
            .WithAll<Simulate>())
        {
            if (!playerInput.ValueRO.Interact.IsSet) continue;
            if (playerGhost.ValueRO.IsDead) continue;

            foreach (var (itemData, itemEntity) in SystemAPI
                .Query<RefRW<ItemGhostData>>()
                .WithEntityAccess())
            {
                if (itemData.ValueRO.IsPickedUp) continue;

                var dist = math.distance(playerGhost.ValueRO.Position, itemData.ValueRO.Position);
                if (dist > PickupRange) continue;

                // 아이템 타입: 0=무기, 1=회복, 2=삽
                if (itemData.ValueRO.ItemType == 0)
                {
                    playerGhost.ValueRW.WeaponId = itemData.ValueRO.ItemId;
                    playerGhost.ValueRW.AmmoCount = 30;
                }
                else if (itemData.ValueRO.ItemType == 1)
                {
                    var newHp = playerGhost.ValueRO.Health + 30f;
                    playerGhost.ValueRW.Health = math.min(newHp, MaxHealth);
                }
                else if (itemData.ValueRO.ItemType == 2)
                {
                    playerGhost.ValueRW.HasShovel = true;
                    playerGhost.ValueRW.ShovelUseCount = SandBlast.SimulationConstants.ShovelUseCount;
                }

                itemData.ValueRW.IsPickedUp = true;
                break; // 한 번에 하나만 줍기
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
