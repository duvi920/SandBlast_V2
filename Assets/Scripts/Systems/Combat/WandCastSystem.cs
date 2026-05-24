using SandBlast.Wand;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 서버 전용 — 완드 슬롯 주문을 발동한다.
// WandComponent / SpellData 버퍼가 없는 플레이어는 건너뛴다 (기존 ShootSystem 과 공존).
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
public partial struct WandCastSystem : ISystem
{
    private const float MuzzleOffset = 0.6f;
    private const float MultiShotAngle = 0.2618f; // 라디안 단위 15°

    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.TryGetSingleton<GamePrefabs>(out var prefabs)) return;
        if (prefabs.BulletPrefab == Entity.Null) return;
        if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime)) return;

        // 예측 중복 실행 방지: 해당 틱에 처음 실행될 때만 발사
        if (!networkTime.IsFirstTimeFullyPredictingTick) return;

        var dt  = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (input, ghostData, wand, mana, spells, owner) in SystemAPI
            .Query<RefRO<PlayerInput>, RefRO<PlayerGhostData>,
                   RefRW<WandComponent>, RefRW<ManaComponent>,
                   DynamicBuffer<SpellData>, RefRO<GhostOwner>>()
            .WithAll<Simulate>())
        {
            wand.ValueRW.CastCooldown -= dt;

            if (ghostData.ValueRO.IsDead) continue;
            if (!input.ValueRO.Shoot.IsSet) continue;
            if (wand.ValueRO.CastCooldown > 0f) continue;
            if (spells.Length == 0) continue;

            int slot  = math.clamp(wand.ValueRO.ActiveSlot, 0, spells.Length - 1);
            var spell = spells[slot];

            if (mana.ValueRO.Current < spell.ManaCost) continue;

            mana.ValueRW.Current    = math.max(0f, mana.ValueRO.Current - spell.ManaCost);
            wand.ValueRW.CastCooldown = wand.ValueRO.CastInterval;

            var dir     = new float2(math.cos(input.ValueRO.AimAngle), math.sin(input.ValueRO.AimAngle));
            int netId   = owner.ValueRO.NetworkId;

            SpawnProjectile(ref ecb, prefabs.BulletPrefab, in ghostData.ValueRO, in spell, dir, netId, 0f);

            if (spell.Modifier == SpellModifier.MultiShot)
            {
                SpawnProjectile(ref ecb, prefabs.BulletPrefab, in ghostData.ValueRO, in spell, dir, netId,  MultiShotAngle);
                SpawnProjectile(ref ecb, prefabs.BulletPrefab, in ghostData.ValueRO, in spell, dir, netId, -MultiShotAngle);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }

    static void SpawnProjectile(
        ref EntityCommandBuffer ecb,
        Entity prefab,
        in PlayerGhostData ghostData,
        in SpellData spell,
        float2 dir,
        int ownerNetworkId,
        float angleOffset)
    {
        if (math.abs(angleOffset) > 0.001f)
        {
            float c = math.cos(angleOffset), s = math.sin(angleOffset);
            dir = new float2(dir.x * c - dir.y * s, dir.x * s + dir.y * c);
        }

        var spawnPos = ghostData.Position + dir * MuzzleOffset;
        var bullet   = ecb.Instantiate(prefab);

        ecb.SetComponent(bullet, new BulletGhostData
        {
            Position       = spawnPos,
            Velocity       = dir * spell.ProjectileSpeed,
            OwnerNetworkId = ownerNetworkId,
            Damage         = spell.Damage,
            BulletType     = (int)spell.ProjectileType,
        });
        ecb.SetComponent(bullet, LocalTransform.FromPosition(
            new Unity.Mathematics.float3(spawnPos.x, spawnPos.y, 0f)));
        ecb.SetComponent(bullet, new BulletLifetime { Value = 4f });

        // 완드 전용 데이터 (서버 로컬)
        ecb.AddComponent(bullet, new ProjectileComponent
        {
            Type           = spell.ProjectileType,
            Payload        = spell.LiquidPayload,
            BlastRadius    = spell.BlastRadius,
            PenetrateCount = spell.Modifier == SpellModifier.Penetrate ? spell.PenetrateCount : 0,
        });
    }
}
