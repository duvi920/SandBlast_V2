using System;

namespace SandBlast.Network
{
    public enum NetworkMode { Offline, Host, Join }

    public struct PlayerJoinEvent
    {
        public int    NetworkId;
        public string PlayerId;
    }

    public struct PlayerLeaveEvent
    {
        public int NetworkId;
    }

    // 네트워크 모드를 추상화하는 인터페이스.
    // OfflineAdapter(싱글) / HostNfEAdapter(호스트 멀티) / MMOZoneAdapter(MMO)를
    // 동일한 인터페이스로 교체할 수 있도록 설계한다.
    public interface INetworkAdapter
    {
        NetworkMode Mode    { get; }
        bool        IsServer { get; }
        bool        IsClient { get; }

        void Initialize(ushort port);
        void Shutdown();

        event Action<PlayerJoinEvent>  OnPlayerJoined;
        event Action<PlayerLeaveEvent> OnPlayerLeft;
    }
}
