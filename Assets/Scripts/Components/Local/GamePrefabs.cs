using Unity.Entities;

public struct GamePrefabs : IComponentData
{
    public Entity PlayerPrefab;
    public Entity BulletPrefab;
    public Entity WeaponItemPrefab;
    public Entity HealItemPrefab;
    public Entity WandItemPrefab;
    public Entity ShovelItemPrefab;
}
