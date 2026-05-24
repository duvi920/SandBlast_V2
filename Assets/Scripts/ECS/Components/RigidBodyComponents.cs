using Unity.Entities;
using Unity.Mathematics;

namespace SandBlast
{
    // 강체 노드 식별자 — RigidBodyNode MonoBehaviour의 NodeId를 ECS로 이전
    public struct RigidBodyNodeComponent : IComponentData
    {
        public int NodeId;
    }

    // 강체 간 엣지 (양방향으로 각 노드에 저장)
    public struct EdgeElement : IBufferElementData
    {
        public int       PeerNodeId;
        public EdgeState State;    // 온전함 / 파괴됨
        public float     GapWidth; // 끊긴 틈새 폭 — CanLiquidPass 판정에 사용
    }

    // 폴리곤 정점 버퍼 (로컬 좌표) — SolidMaskSyncSystem 래스터화에 사용
    public struct PolygonVertex : IBufferElementData
    {
        public float2 LocalPos;
    }

    // 픽셀 전환 시 초기 속도로 사용 — MonoBehaviour에서 파괴 직전에 기록
    public struct RigidVelocityComponent : IComponentData
    {
        public float2 Velocity;
        public float  AngularVelocity; // 라디안/초
    }

    // 모든 엣지가 BROKEN → RigidToPixelConvertSystem이 픽셀 전환 처리
    public struct PendingPixelConversionTag : IComponentData { }

    // 엣지 파괴 요청 — RigidBodyECSBridge를 통해 외부에서 제출, GraphSystem이 처리
    public struct EdgeBreakRequest : IBufferElementData
    {
        public int   NodeA;
        public int   NodeB;
        public float GapWidth;
    }

    // EdgeBreakRequest 버퍼를 담는 싱글턴 엔티티의 마커
    public struct EdgeBreakQueueSingleton : IComponentData { }
}
