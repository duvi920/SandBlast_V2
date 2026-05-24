# ClientConnectionSystem.cs

## 역할
클라이언트 World에서 서버 `127.0.0.1:7979`로 연결을 요청하는 시스템. 에디터 환경에서는 `GameBootstrap.AutoConnectPort`가 자동 처리하지만, 배포 빌드나 커스텀 IP 연결이 필요한 경우 이 시스템을 확장해 사용한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ClientSimulation` |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 횟수 | 1회 (OnUpdate 종료 시 `state.Enabled = false`) |

## 동작 방식
```csharp
var endpoint = NetworkEndpoint.LoopbackIpv4.WithPort(7979);
NetworkStreamDriver.Connect(EntityManager, endpoint);
```
- `LoopbackIpv4` — `127.0.0.1`. 같은 머신의 서버로 연결.
- 실제 배포 시 UI에서 입력받은 IP를 `NetworkEndpoint.Parse(ip, port)`로 변환해 전달하도록 확장한다.

## 확장 포인트 — 커스텀 IP 연결
```csharp
// 로비 UI에서 받은 IP로 연결하는 확장 예시
if (NetworkEndpoint.TryParse(lobbyIp, 7979, out var ep))
    NetworkStreamDriver.Connect(EntityManager, ep);
```

## 관련 파일
- [`GameBootstrap`](../../Bootstrap/GameBootstrap.md) — 에디터 자동 연결 설정
- [`ServerConnectionSystem`](ServerConnectionSystem.md) — 서버 Listen
