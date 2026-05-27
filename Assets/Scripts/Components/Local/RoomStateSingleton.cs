using Unity.Entities;

public enum RoomPhase { Solo, Countdown, Battle }

public struct RoomStateSingleton : IComponentData
{
    public RoomPhase Phase;
    public float     CountdownTimer;  // Countdown 단계: 3 → 0
    public int       LastPlayerCount;
    // 호스트 멀티 확장 — LobbySystem이 사용
    public int       MaxPlayers;      // 최대 참가 인원 (0이면 기본 4)
    public int       MinPlayers;      // 카운트다운 시작 최소 인원 (0이면 기본 2)
    public int       HostNetworkId;   // 방을 연 호스트 플레이어의 NetworkId
}
