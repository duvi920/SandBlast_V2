using Unity.Entities;

public struct WeaponState : IComponentData
{
    public float Cooldown;
    public float ReloadTimer;
    public bool IsReloading;
}
