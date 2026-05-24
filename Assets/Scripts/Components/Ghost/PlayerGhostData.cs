using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 모든 클라이언트에 동기화되는 플레이어 상태. [GhostField]로 표시된 필드는 NetCode가 스냅샷에 포함해 전송한다.
[GhostComponent(PrefabType = GhostPrefabType.All, OwnerSendType = SendToOwnerType.All)]
public struct PlayerGhostData : IComponentData
{
    [GhostField] public float2 Position;
    [GhostField] public float2 Velocity;
    [GhostField(Quantization = 10)] public float Health;    // 10배 정밀도로 양자화 (소수점 1자리)
    [GhostField(Quantization = 10)] public float Armor;     // 10배 정밀도로 양자화
    [GhostField] public bool IsDead;
    [GhostField] public bool IsFacingRight;
    [GhostField] public int WeaponId;
    [GhostField] public int AmmoCount;
    [GhostField] public bool IsGrounded;
    [GhostField] public int KillCount;
    [GhostField] public int TeamId;       // 0=A팀, 1=B팀 (1v1에서는 플레이어 인덱스)
    [GhostField] public int RespawnCount; // 남은 부활 횟수
    [GhostField] public bool IsStunned;   // 기절 중 — 클라이언트 렌더링용 동기화
    [GhostField] public bool HasShovel;   // 삽 소지 여부
    [GhostField] public int ShovelUseCount; // 남은 삽 사용 횟수
}
