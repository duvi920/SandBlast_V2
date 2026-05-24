using Unity.Entities;
using UnityEngine;

public class BulletAuthoring : MonoBehaviour
{
    public float DefaultLifetime = 3f;

    class Baker : Baker<BulletAuthoring>
    {
        public override void Bake(BulletAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new BulletGhostData());
            AddComponent(entity, new BulletLifetime { Value = authoring.DefaultLifetime });
        }
    }
}
