using Unity.Collections;
using Unity.Entities;

namespace SandBlast
{
    // 엣지 파괴 요청을 처리하고 완전히 고립된 노드를 PendingPixelConversionTag로 마킹한다.
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(SolidMaskSyncSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class RigidBodyGraphSystem : SystemBase
    {
        // 외부에서 접근할 싱글턴 엔티티 (EdgeBreakRequest 버퍼 보유)
        public Entity QueueEntity { get; private set; }

        EntityQuery _nodeQuery;

        protected override void OnCreate()
        {
            // EdgeBreakRequest 버퍼 싱글턴 생성
            QueueEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<EdgeBreakQueueSingleton>(QueueEntity);
            EntityManager.AddBuffer<EdgeBreakRequest>(QueueEntity);

            _nodeQuery = GetEntityQuery(
                ComponentType.ReadOnly<RigidBodyNodeComponent>(),
                ComponentType.ReadWrite<EdgeElement>());
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime)) return;
            if (!networkTime.IsFirstTimeFullyPredictingTick) return;

            var requests = EntityManager.GetBuffer<EdgeBreakRequest>(QueueEntity);
            if (requests.Length == 0) return;

            // NodeId → Entity 맵 구축 (노드 수가 적으므로 매 프레임 재구축해도 무방)
            var nodeMap = BuildNodeMap();

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var req in requests)
                ProcessBreak(req.NodeA, req.NodeB, req.GapWidth, nodeMap, ecb);

            requests.Clear();
            nodeMap.Dispose();

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }

        void ProcessBreak(int a, int b, float gapWidth,
                          NativeHashMap<int, Entity> nodeMap,
                          EntityCommandBuffer ecb)
        {
            UpdateEdge(a, b, gapWidth, nodeMap, ecb);
            UpdateEdge(b, a, gapWidth, nodeMap, ecb);
        }

        void UpdateEdge(int ownerNodeId, int peerNodeId, float gapWidth,
                        NativeHashMap<int, Entity> nodeMap,
                        EntityCommandBuffer ecb)
        {
            if (!nodeMap.TryGetValue(ownerNodeId, out Entity ownerEntity)) return;

            var edges = EntityManager.GetBuffer<EdgeElement>(ownerEntity);
            for (int i = 0; i < edges.Length; i++)
            {
                if (edges[i].PeerNodeId != peerNodeId) continue;
                edges[i] = new EdgeElement
                {
                    PeerNodeId = peerNodeId,
                    State      = EdgeState.BROKEN,
                    GapWidth   = gapWidth,
                };
                break;
            }

            // 모든 엣지가 끊긴 경우 픽셀 전환 예약
            if (AllEdgesBroken(edges) &&
                !EntityManager.HasComponent<PendingPixelConversionTag>(ownerEntity))
                ecb.AddComponent<PendingPixelConversionTag>(ownerEntity);
        }

        static bool AllEdgesBroken(DynamicBuffer<EdgeElement> edges)
        {
            if (edges.Length == 0) return true; // 엣지 없는 노드는 즉시 고립
            foreach (var e in edges)
                if (e.State == EdgeState.INTACT) return false;
            return true;
        }

        NativeHashMap<int, Entity> BuildNodeMap()
        {
            var map = new NativeHashMap<int, Entity>(_nodeQuery.CalculateEntityCount(), Allocator.Temp);
            var entities = _nodeQuery.ToEntityArray(Allocator.Temp);
            var nodes    = _nodeQuery.ToComponentDataArray<RigidBodyNodeComponent>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
                map[nodes[i].NodeId] = entities[i];
            entities.Dispose();
            nodes.Dispose();
            return map;
        }
    }
}
