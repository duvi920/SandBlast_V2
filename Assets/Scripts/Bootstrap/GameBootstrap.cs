using System;
using SandBlast.Network;
using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;

// 호스트 멀티 부트스트랩.
// --host  : 서버 + 클라이언트 월드 (Listen Server, 포트 7979 리슨)
// --join <ip> : 클라이언트 월드만 생성 후 원격 호스트에 연결
// (인수 없음)   : 에디터 기본값 — 호스트 모드와 동일 (포트 자동 연결)
public class GameBootstrap : ClientServerBootstrap
{
    // 현재 세션의 네트워크 어댑터 — 다른 시스템에서 참조 가능
    public static INetworkAdapter      NetworkAdapter { get; private set; }
    // 플레이어 데이터 저장소
    public static IPlayerDataRepository PlayerData    { get; private set; }

    static bool IsMppmClone =>
        Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--virtual-project-clone");

    static bool IsHeadlessServer =>
        Array.Exists(Environment.GetCommandLineArgs(), a =>
            a is "--server" or "-server" or "-batchmode");

    static string GetJoinAddress()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] is "--join" or "-join")
                return args[i + 1];
        return null;
    }

    public override bool Initialize(string defaultWorldName)
    {
        Application.runInBackground = true;
        AutoConnectPort = 7979;

        string joinAddr = GetJoinAddress();

        // ── MPPM 가상 클라이언트 (에디터 멀티플레이어 테스트) ────────
        if (IsMppmClone)
        {
            NetworkAdapter = new HostNfEAdapter(NetworkMode.Join);
            PlayerData     = new HostFileRepository();
            var clientWorld = CreateClientWorld("ClientWorld");
            World.DefaultGameObjectInjectionWorld = clientWorld;
            return true;
        }

        // ── 원격 호스트에 참가 (--join <ip>) ─────────────────────────
        if (joinAddr != null)
        {
            NetworkAdapter  = new HostNfEAdapter(NetworkMode.Join, joinAddr);
            PlayerData      = new HostFileRepository();
            AutoConnectPort = 0; // 자동 접속 비활성화 — 수동으로 엔드포인트 지정
            var clientWorld = CreateClientWorld("ClientWorld");
            World.DefaultGameObjectInjectionWorld = clientWorld;
            ScheduleRemoteConnect(clientWorld, joinAddr, 7979);
            Debug.Log($"[Bootstrap] Join 모드 — {joinAddr}:7979 연결 시도");
            return true;
        }

        // ── 호스트 (서버 + 클라이언트 Listen Server) ─────────────────
        NetworkAdapter = new HostNfEAdapter(NetworkMode.Host);
        PlayerData     = new HostFileRepository();
        NetworkAdapter.Initialize(7979);

        if (!IsHeadlessServer)
            DisableAudioListener();

        Debug.Log("[Bootstrap] Host 모드 — 포트 7979 리슨");
        return base.Initialize(defaultWorldName);
    }

    // 클라이언트 월드에 원격 서버 연결 요청을 예약한다.
    // NfE의 NetworkStreamSystem이 이 엔티티를 감지해 연결을 수행한다.
    static void ScheduleRemoteConnect(World clientWorld, string ip, ushort port)
    {
        var ep = NetworkEndpoint.Parse(ip, port, NetworkFamily.Ipv4);
        var e  = clientWorld.EntityManager.CreateEntity(typeof(NetworkStreamRequestConnect));
        clientWorld.EntityManager.SetComponentData(e, new NetworkStreamRequestConnect { Endpoint = ep });
    }

    static void DisableAudioListener()
    {
        var listener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }
}
