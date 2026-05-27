using System;

namespace SandBlast.Network
{
    // 싱글플레이어용 더미 어댑터.
    // NetCode 없이 INetworkAdapter 인터페이스를 구현한다.
    // 횡스크롤 RPG 전환(Phase A) 이후 활성화할 예정.
    public class OfflineAdapter : INetworkAdapter
    {
        public NetworkMode Mode     => NetworkMode.Offline;
        public bool        IsServer => true;
        public bool        IsClient => true;

        public event Action<PlayerJoinEvent>  OnPlayerJoined;
        public event Action<PlayerLeaveEvent> OnPlayerLeft;

        public void Initialize(ushort port) { }
        public void Shutdown()              { }
    }
}
