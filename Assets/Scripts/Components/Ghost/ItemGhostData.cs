using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct ItemGhostData : IComponentData
{
    [GhostField] public float2 Position;
    [GhostField] public int ItemType;
    [GhostField] public int ItemId;
    [GhostField] public bool IsPickedUp;
}
