using Unity.Entities;
using UnityEngine;

public class GamePrefabsAuthoring : MonoBehaviour
{
    public GameObject PlayerPrefab;
    public GameObject BulletPrefab;
    public GameObject WeaponItemPrefab;
    public GameObject HealItemPrefab;
    public GameObject WandItemPrefab;
    public GameObject ShovelItemPrefab;

    class Baker : Baker<GamePrefabsAuthoring>
    {
        public override void Bake(GamePrefabsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new GamePrefabs
            {
                PlayerPrefab     = authoring.PlayerPrefab != null
                    ? GetEntity(authoring.PlayerPrefab, TransformUsageFlags.Dynamic)
                    : Unity.Entities.Entity.Null,
                BulletPrefab     = GetEntity(authoring.BulletPrefab,     TransformUsageFlags.Dynamic),
                WeaponItemPrefab = GetEntity(authoring.WeaponItemPrefab, TransformUsageFlags.Dynamic),
                HealItemPrefab   = GetEntity(authoring.HealItemPrefab,   TransformUsageFlags.Dynamic),
                WandItemPrefab   = authoring.WandItemPrefab != null
                    ? GetEntity(authoring.WandItemPrefab, TransformUsageFlags.Dynamic)
                    : Unity.Entities.Entity.Null,
                ShovelItemPrefab = authoring.ShovelItemPrefab != null
                    ? GetEntity(authoring.ShovelItemPrefab, TransformUsageFlags.Dynamic)
                    : Unity.Entities.Entity.Null,
            });
        }
    }
}
