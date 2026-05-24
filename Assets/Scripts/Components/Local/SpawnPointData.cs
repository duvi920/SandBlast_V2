using Unity.Entities;
using Unity.Mathematics;

public struct SpawnPointData : IComponentData
{
    public float2 Position;
    public int SpawnPointId;
    public bool IsOccupied;
}
