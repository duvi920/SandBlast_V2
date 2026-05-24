# GameBootstrap.cs

## 역할
Unity Netcode for Entities(NfE)의 `ClientServerBootstrap`을 상속해 Client World와 Server World를 자동으로 생성하는 진입점.

## 상속 구조
```
ClientServerBootstrap (NfE 제공)
    └── GameBootstrap
```

## 주요 설정

| 속성 | 값 | 설명 |
|------|----|------|
| `AutoConnectPort` | `7979` | 에디터 실행 시 클라이언트가 자동으로 접속할 로컬 포트 |

## 동작 방식
1. Unity가 씬을 로드하면 `GameBootstrap.Initialize()`가 한 번 호출된다.
2. `base.Initialize()`가 내부적으로 **ServerWorld**, **ClientWorld**, **DefaultWorld**를 분리 생성한다.
3. `AutoConnectPort`가 설정되어 있으면 에디터/로컬 빌드에서 클라이언트가 `127.0.0.1:7979`로 자동 연결을 시도한다.
4. 배포 빌드에서는 커맨드라인 인수(`-server`, `-connect IP`)로 동작을 제어한다.

## 관련 파일
- [`ServerConnectionSystem.cs`](../Systems/Connection/ServerConnectionSystem.md) — 서버 포트 Listen
- [`ClientConnectionSystem.cs`](../Systems/Connection/ClientConnectionSystem.md) — 서버 Connect

## 씬 설정
별도 GameObject 불필요. 프로젝트에 이 클래스가 존재하는 것만으로 NfE Bootstrap이 교체된다.
