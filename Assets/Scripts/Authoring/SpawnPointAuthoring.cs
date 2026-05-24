using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class SpawnPointAuthoring : MonoBehaviour
{
    public int SpawnPointId;

    class Baker : Baker<SpawnPointAuthoring>
    {
        public override void Bake(SpawnPointAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var pos = authoring.transform.position;

            AddComponent(entity, new SpawnPointData
            {
                Position = new float2(pos.x, pos.y),
                SpawnPointId = authoring.SpawnPointId,
                IsOccupied = false
            });
        }
    }
}
