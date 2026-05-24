using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 클라이언트 로컬 플레이어 타이머 상태 — [GhostField] 없음, 예측 롤백 시 서버 덮어쓰기 방지
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct PlayerLocalState : IComponentData
{
    public float  CoyoteTimer;
    public float  JumpBufferTimer;
    public bool   IsDashing;
    public float  DashTimer;
    public float  DashCooldownTimer;
    public float2 DashDir;
}
