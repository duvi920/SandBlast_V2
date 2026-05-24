using Unity.Entities;

public enum DeathCause : byte
{
    Damage,  // HP 소진
    Buried,  // 매몰
    Lava,    // 용암
}

public struct DeathEvent : IComponentData
{
    public int KillerNetworkId;
    public DeathCause Cause;
}
