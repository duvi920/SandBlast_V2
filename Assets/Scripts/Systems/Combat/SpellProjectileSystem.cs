using SandBlast;
using SandBlast.Wand;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 서버 및 클라이언트 — ProjectileComponent 가 있는 완드 투사체의 지형 충돌·피해를 처리한다.
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateBefore(typeof(PixelSimulationSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
public partial class SpellProjectileSystem : SystemBase
{
    private const float DefaultImpactRadius = 0.5f;

    protected override void OnCreate()
    {
        RequireForUpdate<PixelGridSingleton>();
        RequireForUpdate<ChunkManagerSingleton>();
        RequireForUpdate<NetworkTime>();
    }

    protected override void OnUpdate()
    {
        var networkTime = SystemAPI.GetSingleton<NetworkTime>();
        if (!networkTime.IsFirstTimeFullyPredictingTick) return;

        var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
        var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();
        var ecb    = new EntityCommandBuffer(Allocator.Temp);

        foreach (var (bulletData, proj, entity) in SystemAPI
            .Query<RefRO<BulletGhostData>, RefRW<ProjectileComponent>>()
            .WithAll<Simulate>()
            .WithEntityAccess())
        {
            int2 gp = grid.WorldToGrid(bulletData.ValueRO.Position);
            if (!grid.InBounds(gp.x, gp.y)) continue;
            if (!((CellType)grid.Type[grid.Index(gp.x, gp.y)]).IsSolid()) continue;
            // ... (rest of the switch remains the same)

            float radius = proj.ValueRO.Type == ProjectileType.Explosion
                ? math.max(proj.ValueRO.BlastRadius, DefaultImpactRadius)
                : DefaultImpactRadius;

            switch (proj.ValueRO.Type)
            {
                case ProjectileType.Explosion:
                    PixelImpactUtility.Explode(ref grid, ref chunks,
                        bulletData.ValueRO.Position, radius, 10f);
                    break;

                case ProjectileType.Ice:
                    PixelImpactUtility.FreezeCircle(ref grid, ref chunks,
                        bulletData.ValueRO.Position, radius * 1.2f);
                    break;

                case ProjectileType.Sand:
                    PixelImpactUtility.StampCircle(ref grid, ref chunks,
                        bulletData.ValueRO.Position, radius * 1.5f, CellType.POWDER_SAND);
                    break;

                case ProjectileType.Lightning:
                    // Phase 3: 즉발 라인 관통
                    float2 beamEnd = bulletData.ValueRO.Position + math.normalize(bulletData.ValueRO.Velocity) * 20f;
                    PixelImpactUtility.ScorchLine(ref grid, ref chunks,
                        bulletData.ValueRO.Position, beamEnd, radius);
                    
                    // 플레이어 즉발 피해 처리
                    ApplyLightningDamage(ref ecb, bulletData.ValueRO.Position, beamEnd, radius, 
                        bulletData.ValueRO.Damage, bulletData.ValueRO.OwnerNetworkId);
                    
                    ecb.DestroyEntity(entity);
                    continue;

                default:
                    PixelImpactUtility.BulletImpact(ref grid, ref chunks,
                        bulletData.ValueRO.Position, radius);
                    break;
            }

            // 액체 페이로드 — 충격 지점 주변에 액체 셀 생성
            switch (proj.ValueRO.Payload)
            {
                case LiquidPayload.Lava:
                    PixelImpactUtility.StampCircle(ref grid, ref chunks,
                        bulletData.ValueRO.Position, radius * 0.8f, CellType.LIQUID_LAVA,
                        SimulationConstants.LAVA_INITIAL_TEMP);
                    break;

                case LiquidPayload.Water:
                    PixelImpactUtility.ExtinguishCircle(ref grid, ref chunks,
                        bulletData.ValueRO.Position, radius * 1.5f);
                    PixelImpactUtility.StampCircle(ref grid, ref chunks,
                        bulletData.ValueRO.Position, radius * 0.8f, CellType.LIQUID_WATER);
                    break;

                case LiquidPayload.Poison:
                    PixelImpactUtility.StampCircle(ref grid, ref chunks,
                        bulletData.ValueRO.Position, radius * 0.8f, CellType.LIQUID_POISON);
                    break;
            }

            // 관통 처리
            if (proj.ValueRO.PenetrateCount > 0)
            {
                proj.ValueRW.PenetrateCount--;
                // 셀을 파괴했으므로 투사체는 계속 이동
                continue;
            }

            ecb.DestroyEntity(entity);
        }

        // 플레이어 피해 판정 (BulletGhostData.Damage 활용, ProjectileComponent 유무 공통)
        ApplyPlayerDamage(ref ecb);

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    void ApplyLightningDamage(
        ref EntityCommandBuffer ecb, 
        float2 start, 
        float2 end, 
        float radius, 
        float damage, 
        int ownerNetId)
    {
        foreach (var (playerGhost, entity) in SystemAPI
            .Query<RefRW<PlayerGhostData>>()
            .WithEntityAccess()
            .WithAll<Simulate>())
        {
            if (playerGhost.ValueRO.IsDead) continue;
            if (ownerNetId == SystemAPI.GetComponent<GhostOwner>(entity).NetworkId) continue;

            float dist = PointToLineDistance(playerGhost.ValueRO.Position, start, end);
            if (dist < radius + 0.5f)
            {
                playerGhost.ValueRW.Health = math.max(0f, playerGhost.ValueRO.Health - damage);
                if (playerGhost.ValueRO.Health <= 0f)
                    playerGhost.ValueRW.IsDead = true;
            }
        }
    }

    static float PointToLineDistance(float2 p, float2 a, float2 b)
    {
        float l2 = math.distancesq(a, b);
        if (l2 == 0.0f) return math.distance(p, a);
        float t = math.max(0, math.min(1, math.dot(p - a, b - a) / l2));
        float2 projection = a + t * (b - a);
        return math.distance(p, projection);
    }

    void ApplyPlayerDamage(ref EntityCommandBuffer ecb)
    {
        const float HitRadius = 0.5f;

        foreach (var (bulletData, proj, bulletEntity) in SystemAPI
            .Query<RefRO<BulletGhostData>, RefRO<ProjectileComponent>>()
            .WithEntityAccess())
        {
            if (bulletData.ValueRO.Damage <= 0f) continue;

            foreach (var (playerGhost, entity) in SystemAPI
                .Query<RefRW<PlayerGhostData>>()
                .WithEntityAccess()
                .WithAll<Simulate>())
            {
                if (playerGhost.ValueRO.IsDead) continue;
                if (bulletData.ValueRO.OwnerNetworkId ==
                    SystemAPI.GetComponent<GhostOwner>(entity).NetworkId) continue;

                float dist = math.distance(bulletData.ValueRO.Position, playerGhost.ValueRO.Position);
                float hitR = proj.ValueRO.Type == ProjectileType.Explosion
                    ? math.max(proj.ValueRO.BlastRadius, HitRadius)
                    : HitRadius;
                if (dist > hitR) continue;

                playerGhost.ValueRW.Health =
                    math.max(0f, playerGhost.ValueRO.Health - bulletData.ValueRO.Damage);
                if (playerGhost.ValueRO.Health <= 0f)
                    playerGhost.ValueRW.IsDead = true;

                if (proj.ValueRO.PenetrateCount <= 0)
                    ecb.DestroyEntity(bulletEntity);
                break;
            }
        }
    }
}
