using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ItemAuthoring : MonoBehaviour
{
    public int ItemType = 0; // 0=무기, 1=회복
    public int ItemId = 0;

    class Baker : Baker<ItemAuthoring>
    {
        public override void Bake(ItemAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ItemGhostData
            {
                ItemType = authoring.ItemType,
                ItemId = authoring.ItemId,
                IsPickedUp = false
            });
        }
    }
}
