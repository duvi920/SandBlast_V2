using Unity.Entities;
using Unity.NetCode;

// 강체 노드의 네트워크 동기화 상태.
// 서버가 IsDestroyed=true 로 설정 → 클라이언트 TerrainSyncSystem이 픽셀 전환을 트리거한다.
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct RigidBodyGhostData : IComponentData
{
    [GhostField] public int  NodeId;
    [GhostField] public bool IsDestroyed;
}
