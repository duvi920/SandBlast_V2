using SandBlast.Wand;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 전용 — ItemType == 2 인 완드 아이템을 주울 때 WandComponent·SpellData 를 설정한다.
// ItemId 별 완드 프리셋:
//   0 = 기본 완드 (Bullet)
//   1 = 폭발 완드 (Explosion)
//   2 = 용암 완드 (LiquidSpray + Lava)
//   3 = 관통 완드 (Bullet + Penetrate)
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial struct WandPickupSystem : ISystem
{
    private const float PickupRange = 1.5f;
    private const int   WandItemType = 2;

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (input, ghostData, wand, spells) in SystemAPI
            .Query<RefRO<PlayerInput>, RefRO<PlayerGhostData>,
                   RefRW<WandComponent>, DynamicBuffer<SpellData>>()
            .WithAll<Simulate>())
        {
            if (!input.ValueRO.Interact.IsSet) continue;
            if (ghostData.ValueRO.IsDead) continue;

            foreach (var itemData in SystemAPI.Query<RefRW<ItemGhostData>>())
            {
                if (itemData.ValueRO.IsPickedUp) continue;
                if (itemData.ValueRO.ItemType != WandItemType) continue;

                float dist = math.distance(ghostData.ValueRO.Position, itemData.ValueRO.Position);
                if (dist > PickupRange) continue;

                ApplyWandPreset(itemData.ValueRO.ItemId, ref wand.ValueRW, spells);
                itemData.ValueRW.IsPickedUp = true;
                break;
            }
        }
    }

    static void ApplyWandPreset(int itemId, ref WandComponent wand, DynamicBuffer<SpellData> spells)
    {
        spells.Clear();

        switch (itemId)
        {
            case 1: // 폭발 완드
                wand.CastInterval = 0.6f;
                wand.IsAutoCast   = false;
                spells.Add(new SpellData
                {
                    ProjectileType = ProjectileType.Explosion,
                    LiquidPayload  = LiquidPayload.None,
                    Modifier       = SpellModifier.None,
                    ManaCost       = 15f,
                    Damage         = 50f,
                    ProjectileSpeed = 18f,
                    BlastRadius    = 1.5f,
                });
                break;

            case 2: // 용암 완드
                wand.CastInterval = 0.25f;
                wand.IsAutoCast   = true;
                spells.Add(new SpellData
                {
                    ProjectileType = ProjectileType.LiquidSpray,
                    LiquidPayload  = LiquidPayload.Lava,
                    Modifier       = SpellModifier.None,
                    ManaCost       = 8f,
                    Damage         = 15f,
                    ProjectileSpeed = 12f,
                    BlastRadius    = 0.5f,
                });
                break;

            case 3: // 관통 완드
                wand.CastInterval = 0.4f;
                wand.IsAutoCast   = false;
                spells.Add(new SpellData
                {
                    ProjectileType  = ProjectileType.Bullet,
                    LiquidPayload   = LiquidPayload.None,
                    Modifier        = SpellModifier.Penetrate,
                    ManaCost        = 10f,
                    Damage          = 40f,
                    ProjectileSpeed = 30f,
                    BlastRadius     = 0f,
                    PenetrateCount  = 2,
                });
                break;

            default: // 0 = 기본 완드
                wand.CastInterval = 0.3f;
                wand.IsAutoCast   = false;
                spells.Add(new SpellData
                {
                    ProjectileType = ProjectileType.Bullet,
                    LiquidPayload  = LiquidPayload.None,
                    Modifier       = SpellModifier.None,
                    ManaCost       = 5f,
                    Damage         = 25f,
                    ProjectileSpeed = 25f,
                    BlastRadius    = 0f,
                });
                break;
        }
    }
}
