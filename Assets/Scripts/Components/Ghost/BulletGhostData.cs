using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 모든 클라이언트에 동기화되는 투사체 상태.
[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct BulletGhostData : IComponentData
{
    [GhostField] public float2 Position;
    [GhostField] public float2 Velocity;
    [GhostField] public int OwnerNetworkId;
    [GhostField(Quantization = 10)] public float Damage;
    [GhostField] public int BulletType;
}
