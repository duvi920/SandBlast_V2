using System;
using UnityEngine;

namespace SandBlast.Network
{
    // NfE Listen Server를 래핑하는 어댑터.
    // Host: 서버 월드 + 클라이언트 월드 동시 실행 (현재 플레이어가 방을 여는 경우)
    // Join: 클라이언트 월드만 실행하고 원격 호스트에 연결
    public class HostNfEAdapter : INetworkAdapter
    {
        public NetworkMode Mode     { get; }
        public bool        IsServer => Mode == NetworkMode.Host;
        public bool        IsClient => true; // 호스트도 자신의 클라이언트 월드를 통해 플레이

        public event Action<PlayerJoinEvent>  OnPlayerJoined;
        public event Action<PlayerLeaveEvent> OnPlayerLeft;

        readonly string _joinAddress; // null = 호스트 모드

        public HostNfEAdapter(NetworkMode mode, string joinAddress = null)
        {
            Mode         = mode;
            _joinAddress = joinAddress;
        }

        public void Initialize(ushort port)
        {
            // NfE 초기화는 GameBootstrap.Initialize()에서 처리됨.
            // 추후 Unity Relay 연동 시 이 메서드에서 Relay 할당을 수행한다.
            Debug.Log($"[HostNfEAdapter] Initialize — mode={Mode}, port={port}" +
                      (_joinAddress != null ? $", joinAddr={_joinAddress}" : ""));
        }

        public void Shutdown()
        {
            Debug.Log("[HostNfEAdapter] Shutdown");
        }

        // 연결 대상 주소를 반환한다. 호스트 모드에서는 null.
        public string JoinAddress => _joinAddress;
    }
}
