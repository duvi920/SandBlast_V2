using SandBlast;
using SandBlast.Wand;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 투사체가 픽셀 지형에 충돌했을 때 지형을 파괴하고 투사체를 제거한다.
// PixelTickSchedulerSystem(=PixelSimulationSystem) 전에 실행해 CA 틱에 변경이 반영되게 한다.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(PixelSimulationSystem))]
[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class BulletTerrainHitSystem : SystemBase
{
    private const float ImpactRadius = 0.5f;

    RigidBodyGraphSystem _graphSystem;

    protected override void OnCreate()
    {
        RequireForUpdate<PixelGridSingleton>();
        RequireForUpdate<ChunkManagerSingleton>();
    }

    protected override void OnUpdate()
    {
        if (_graphSystem == null)
            _graphSystem = World.GetExistingSystemManaged<RigidBodyGraphSystem>();

        var grid   = SystemAPI.GetSingleton<PixelGridSingleton>();
        var chunks = SystemAPI.GetSingleton<ChunkManagerSingleton>();
        var ecb    = new EntityCommandBuffer(Allocator.Temp);

        // ProjectileComponent 가 있는 완드 투사체는 SpellProjectileSystem 이 처리
        foreach (var (bulletData, entity) in
            SystemAPI.Query<RefRO<BulletGhostData>>()
                .WithNone<ProjectileComponent>()
                .WithEntityAccess())
        {
            int2 gp = grid.WorldToGrid(bulletData.ValueRO.Position);
            if (!grid.InBounds(gp.x, gp.y)) continue;

            int      idx = grid.Index(gp.x, gp.y);
            CellType ct  = (CellType)grid.Type[idx];
            if (!ct.IsSolid()) continue;

            // SOLID_RIGID 충돌 시 해당 노드의 엣지 파괴 요청
            if (ct == CellType.SOLID_RIGID && _graphSystem != null &&
                EntityManager.Exists(_graphSystem.QueueEntity))
            {
                int nodeId = grid.RigidId[idx];
                BreakAllEdgesOfNode(nodeId);
            }

            // 반경 내 지형 픽셀 제거 + 발화
            PixelImpactUtility.BulletImpact(ref grid, ref chunks,
                bulletData.ValueRO.Position, ImpactRadius);

            ecb.DestroyEntity(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }

    // 지정 노드의 모든 엣지를 파괴 요청으로 제출
    void BreakAllEdgesOfNode(int nodeId)
    {
        if (_graphSystem == null) return;
        Entity queueEntity = _graphSystem.QueueEntity;
        if (!EntityManager.Exists(queueEntity)) return;

        var edgeReqBuf = EntityManager.GetBuffer<EdgeBreakRequest>(queueEntity);

        foreach (var (node, edges) in
            SystemAPI.Query<RefRO<RigidBodyNodeComponent>, DynamicBuffer<EdgeElement>>())
        {
            if (node.ValueRO.NodeId != nodeId) continue;
            foreach (var edge in edges)
            {
                if (edge.State == EdgeState.INTACT)
                    edgeReqBuf.Add(new EdgeBreakRequest
                    {
                        NodeA    = nodeId,
                        NodeB    = edge.PeerNodeId,
                        GapWidth = 2f,
                    });
            }
            break;
        }
    }
}
