using System;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

public class GameBootstrap : ClientServerBootstrap
{
    static bool IsMppmClone =>
        Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--virtual-project-clone");

    public override bool Initialize(string defaultWorldName)
    {
        AutoConnectPort = 7979;
        Application.runInBackground = true;

        // MPPM 가상 플레이어는 Client만 생성 — 서버 포트 충돌 방지
        // true 반환 = "부트스트랩이 다 처리했으니 DefaultWorld 만들지 마" 신호
        // false 반환하면 DefaultWorld가 추가 생성되어 DefaultGameObjectInjectionWorld를 덮어씀
        if (IsMppmClone)
        {
            var clientWorld = CreateClientWorld("ClientWorld");
            World.DefaultGameObjectInjectionWorld = clientWorld;
            return true;
        }

        // 메인 에디터(서버)에서는 오디오 불필요 — AudioListener 경고 제거
        DisableAudioListener();

        return base.Initialize(defaultWorldName);
    }

    static void DisableAudioListener()
    {
        var listener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
        if (listener != null)
            listener.enabled = false;
    }
}
