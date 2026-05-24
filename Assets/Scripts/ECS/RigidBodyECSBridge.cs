using Unity.Entities;
using UnityEngine;

namespace SandBlast
{
    // MonoBehaviour 코드에서 ECS RigidBodyGraphSystem에 명령을 제출하기 위한 정적 브리지.
    // Phase 6 정리 전까지 기존 RigidBodyNode MonoBehaviour가 이 API를 통해 ECS와 통신한다.
    public static class RigidBodyECSBridge
    {
        static World ECSWorld => World.DefaultGameObjectInjectionWorld;

        // ─── 엣지 파괴 ─────────────────────────────────────────────

        // 두 노드 사이 엣지를 파괴 요청으로 제출 — 다음 프레임 RigidBodyGraphSystem이 처리
        public static void BreakEdge(int nodeA, int nodeB, float gapWidth = 2f)
        {
            var sys = GetGraphSystem();
            if (sys == null) return;
            if (!ECSWorld.EntityManager.Exists(sys.QueueEntity)) return;

            ECSWorld.EntityManager
                .GetBuffer<EdgeBreakRequest>(sys.QueueEntity)
                .Add(new EdgeBreakRequest { NodeA = nodeA, NodeB = nodeB, GapWidth = gapWidth });
        }

        // ─── 속도 기록 ─────────────────────────────────────────────

        // 픽셀 전환 직전에 강체 속도를 ECS 엔티티에 기록 (RigidToPixelConvertSystem이 읽음)
        public static void SetNodeVelocity(int nodeId, Vector2 velocity, float angularVelocity)
        {
            var em     = ECSWorld?.EntityManager;
            if (em == null) return;

            Entity e = FindEntityByNodeId(em.Value, nodeId);
            if (e == Entity.Null) return;

            em.Value.SetComponentData(e, new RigidVelocityComponent
            {
                Velocity        = new Unity.Mathematics.float2(velocity.x, velocity.y),
                AngularVelocity = angularVelocity,
            });
        }

        // ─── 즉시 픽셀 전환 요청 ───────────────────────────────────

        // 엣지 파괴 없이 직접 픽셀 전환을 요청 (예: 폭발로 인한 즉시 파괴)
        public static void TriggerPixelConversion(int nodeId, Vector2 velocity, float angularVelocity)
        {
            SetNodeVelocity(nodeId, velocity, angularVelocity);

            var em = ECSWorld?.EntityManager;
            if (em == null) return;

            Entity e = FindEntityByNodeId(em.Value, nodeId);
            if (e == Entity.Null) return;

            if (!em.Value.HasComponent<PendingPixelConversionTag>(e))
                em.Value.AddComponent<PendingPixelConversionTag>(e);
        }

        // ─── 내부 유틸리티 ─────────────────────────────────────────

        static RigidBodyGraphSystem GetGraphSystem() =>
            ECSWorld?.GetExistingSystemManaged<RigidBodyGraphSystem>();

        // NodeId로 엔티티를 찾는다 (노드 수가 적으므로 선형 탐색으로 충분)
        public static Entity FindEntityByNodeId(int nodeId)
        {
            var world = ECSWorld;
            if (world == null) return Entity.Null;
            return FindEntityByNodeId(world.EntityManager, nodeId);
        }

        static Entity FindEntityByNodeId(EntityManager em, int nodeId)
        {
            using var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<RigidBodyNodeComponent>());
            using var entities  = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var components = q.ToComponentDataArray<RigidBodyNodeComponent>(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < components.Length; i++)
                if (components[i].NodeId == nodeId) return entities[i];

            return Entity.Null;
        }
    }
}
