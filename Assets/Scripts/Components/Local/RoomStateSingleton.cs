using Unity.Entities;

public enum RoomPhase { Solo, Countdown, Battle }

public struct RoomStateSingleton : IComponentData
{
    public RoomPhase Phase;
    public float     CountdownTimer;  // Countdown 단계: 3 → 0
    public int       LastPlayerCount;
}
