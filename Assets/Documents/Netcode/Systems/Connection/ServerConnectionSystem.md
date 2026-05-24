# ServerConnectionSystem.cs

## 역할
게임 서버를 7979 포트에서 Listen 상태로 만드는 시스템. 서버 World에서 한 번만 실행된 후 자신을 비활성화한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 횟수 | 1회 (OnUpdate 종료 시 `state.Enabled = false`) |

## 동작 방식
```csharp
var endpoint = NetworkEndpoint.AnyIpv4.WithPort(7979);
NetworkStreamDriver.Listen(endpoint);
```
- `AnyIpv4` — 머신의 모든 NIC(네트워크 인터페이스)에서 연결을 수락한다.
- `WithPort(7979)` — Listen 포트. `GameBootstrap.AutoConnectPort`와 일치해야 한다.

## GameBootstrap과의 관계
`GameBootstrap.AutoConnectPort = 7979`가 설정되어 있으면 에디터에서 Play 시 ClientWorld가 자동으로 `127.0.0.1:7979`로 연결을 시도한다. 즉, 에디터 환경에서는 이 시스템 없이도 동작하지만, Dedicated Server 빌드에서는 이 시스템이 반드시 필요하다.

## 관련 파일
- [`GameBootstrap`](../../Bootstrap/GameBootstrap.md) — AutoConnectPort 설정
- [`ClientConnectionSystem`](ClientConnectionSystem.md) — 클라이언트 측 Connect
