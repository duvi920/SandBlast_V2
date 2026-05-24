using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SandBlast
{
    // 씬에 배치해 강체 노드를 ECS Entity로 변환하는 Authoring.
    // 기존 RigidBodyNode MonoBehaviour를 대체한다 (Phase 6에서 삭제).
    // Physics 동작은 GameObject의 Rigidbody2D + PolygonCollider2D가 담당한다.
    [RequireComponent(typeof(Rigidbody2D), typeof(PolygonCollider2D))]
    public class RigidBodyNodeAuthoring : MonoBehaviour
    {
        [Tooltip("RigidBodyGraph 내 고유 ID")]
        public int   NodeId;

        [Tooltip("연결된 인접 노드 ID 목록 (엣지 초기화에 사용)")]
        public int[] ConnectedNodeIds = System.Array.Empty<int>();
    }

    public class RigidBodyNodeBaker : Baker<RigidBodyNodeAuthoring>
    {
        public override void Bake(RigidBodyNodeAuthoring src)
        {
            var e = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(e, new RigidBodyNodeComponent { NodeId = src.NodeId });
            AddComponent(e, new RigidVelocityComponent());
            AddComponent(e, new RigidBodyGhostData { NodeId = src.NodeId, IsDestroyed = false });
            AddBuffer<HitEvent>(e);

            // 엣지 버퍼 — 초기 상태 전부 INTACT
            var edgeBuf = AddBuffer<EdgeElement>(e);
            foreach (int peerId in src.ConnectedNodeIds)
                edgeBuf.Add(new EdgeElement
                {
                    PeerNodeId = peerId,
                    State      = EdgeState.INTACT,
                    GapWidth   = 0f,
                });

            // 폴리곤 정점 버퍼 — PolygonCollider2D Path[0]의 로컬 좌표
            var polyBuf = AddBuffer<PolygonVertex>(e);
            var col     = GetComponent<PolygonCollider2D>();
            if (col != null)
            {
                foreach (var p in col.GetPath(0))
                    polyBuf.Add(new PolygonVertex { LocalPos = new float2(p.x, p.y) });
            }
        }
    }
}
