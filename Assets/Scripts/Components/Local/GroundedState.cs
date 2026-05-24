using Unity.Entities;
using Unity.NetCode;

[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct GroundedState : IComponentData
{
    public bool IsGrounded;
}
