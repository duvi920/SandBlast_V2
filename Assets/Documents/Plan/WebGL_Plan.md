# WebGL 웹 게임 전환 계획

> 최종 갱신: 2026-04-27

---

## 목표

네이티브 서버를 상시 실행해두고, 브라우저(WebGL)에서 클라이언트가 접속하는 구조로 전환한다.

```
[Browser — WebGL 클라이언트]
        │  WebSocket (WS / WSS)
        ▼
[Dedicated Server — Native Linux 빌드]
  ├─ DOTS CA 시뮬레이션 (멀티스레드)
  ├─ NfE Ghost 스냅샷 전송
  └─ 룸(방)별로 1개 프로세스
        │
        ▼
[매치메이킹 / 로비 서버 (선택)]
  ├─ 빈 방 조회
  ├─ 새 서버 프로세스 spin-up
  └─ 접속 정보(IP:Port) 클라이언트에 전달
```

---

## 서버 스케일링 모델 — "사용자당 서버 1개?" 에 대한 답

| 질문 | 답 |
|---|---|
| 사용자 1명 → 서버 1개? | **아니오.** 같은 방 플레이어들이 서버 1개를 공유함 |
| 새 방 생성 → 서버 1개? | **예.** 방(룸)별로 Dedicated Server 프로세스 1개 |
| 서버가 없어도 플레이 가능? | **아니오.** NfE는 서버 없이 클라이언트만 실행 불가 |

### 방식 비교

| 방식 | 장점 | 단점 | 권장 상황 |
|---|---|---|---|
| **단일 서버, 다중 룸** | 관리 단순 | NfE가 멀티-월드 비지원, 구현 복잡 | 비권장 |
| **룸별 프로세스 (현재 구조)** | NfE 설계와 정합, 구현 단순 | 프로세스 수 = 활성 룸 수 | **권장** |
| **컨테이너 오케스트레이션** | 자동 스케일, 클라우드 친화적 | 운영 복잡도 높음 | 상용화 단계 |

현재 `GameBootstrap`은 `ClientServerBootstrap`을 상속해 서버/클라이언트 월드를 자동 분리한다. 이 구조 그대로 Dedicated Server 빌드를 하면 된다.

---

## 패키지 호환성

| 패키지 | WebGL 클라이언트 | 네이티브 서버 |
|---|---|---|
| `com.unity.transport 2.3.0` | WebSocket 모드로 사용 가능 | UDP / WebSocket 모두 가능 |
| `com.unity.netcode 1.3.2` | 호환 | 호환 |
| `com.unity.entities 1.3.2` | 싱글스레드 폴백, 동작은 함 | 멀티스레드 그대로 |
| `com.unity.physics 1.3.2` | 동작하나 성능 저하 가능 | 정상 |
| URP, Input System, UI | 정상 | — |

> **WebGL 주의**: DOTS Job System이 싱글스레드로 폴백됨. CA 시뮬레이션 연산은 클라이언트에서 돌리지 말고 서버에서만 처리해야 함.

---

## 구현 단계

### 1단계 — Transport WebSocket 설정 (완료 예정)

- `UnityTransport` 컴포넌트에서 `UseWebSockets = true` 설정
- 서버 빌드와 WebGL 클라이언트 빌드 양쪽에 동일 설정 적용
- WebGL 빌드 시 Transport가 자동으로 WebSocket 사용 (추가 코드 불필요)

```csharp
// 서버 시작 전 (GameBootstrap 또는 서버 초기화 코드)
var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
transport.SetConnectionData("0.0.0.0", 7777);
transport.UseWebSockets = true;
```

현재 `GameBootstrap.AutoConnectPort = 7979` — WebSocket 전환 후 포트 통일 필요.

### 2단계 — 서버 빌드 분리

- Unity Build Settings → `Dedicated Server` 플랫폼으로 서버 빌드
- 헤드리스 모드로 실행 (`-batchmode -nographics`)
- DOTS / Job System 멀티스레드 그대로 활용
- 서버 프로세스는 룸당 1개. 룸 종료 시 프로세스 종료.

```bash
# 서버 실행 예시 (Linux)
./SandBlast.x86_64 -batchmode -nographics -port 7777
```

### 3단계 — WebGL 클라이언트 빌드

- Build Settings → `WebGL` 플랫폼 선택
- `webGLThreadsSupport: 0` 유지 (현재 설정 그대로)
- Compression: Gzip 권장 (서버에서 `Content-Encoding: gzip` 헤더 필요)
- 빌드 결과물(`index.html`, `.js`, `.wasm`, `.data`)을 CDN 또는 정적 파일 서버에 배포

### 4단계 — SSL/TLS 처리 (HTTPS 서빙 시)

HTTPS 페이지에서는 브라우저가 WS(`ws://`)를 차단한다. WSS(`wss://`) 필요.

```
[Browser] ──WSS──▶ [Nginx / Caddy 리버스 프록시] ──WS──▶ [게임 서버 :7777]
```

```nginx
# Nginx 예시
location /ws {
    proxy_pass http://127.0.0.1:7777;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
}
```

### 5단계 — 매치메이킹 / 룸 관리 (선택)

규모가 작으면 생략 가능. 플레이어가 늘어나면 필요.

**최소 구현 (직접):**
1. REST API 서버 (Node.js / Go 등)
2. 빈 방 목록 DB (Redis 또는 인메모리)
3. 새 플레이어 → 빈 방 조회 → 없으면 새 서버 프로세스 `fork()`/`spawn()`
4. 접속 정보(IP:Port 또는 token) 반환 → 클라이언트가 해당 주소로 NfE 접속

**관리형 서비스 (UGS 경로):**
- Unity Gaming Services → Multiplay 호스팅 + Matchmaker
- Unity Transport가 UGS Relay를 지원하므로 NAT 없이 연결 가능
- 단, UGS는 유료 (트래픽 초과 시 과금)

### 6단계 — 네트워크 환경

- 서버 방화벽에서 WebSocket 포트 개방 (7777 또는 설정값)
- 공인 IP 또는 도메인 필요
- HTTPS라면 SSL 인증서 필요 (Let's Encrypt 무료 발급 가능)

---

## 현재 코드 상태 vs 필요 작업

| 항목 | 현재 상태 | 남은 작업 |
|---|---|---|
| `GameBootstrap` (서버/클라이언트 분리) | 완료 | — |
| `GoInGameSystem` (접속 흐름) | 완료 | — |
| 클라이언트 예측 | 완료 | — |
| `UnityTransport UseWebSockets` | 미적용 | **1단계 필수** |
| WebGL 빌드 설정 | 미적용 | **3단계** |
| 서버 Dedicated Server 빌드 | 미적용 | **2단계** |
| 매치메이킹 / 룸 관리 | 없음 | 5단계 (선택) |
| SSL / 리버스 프록시 | 없음 | 4단계 (HTTPS 시 필수) |

---

## CA 시뮬레이션 분배 전략

픽셀 CA 연산은 WebGL(싱글스레드)에서 돌리면 프레임이 깨진다.

| 연산 | 실행 위치 | 동기화 방법 |
|---|---|---|
| CA 시뮬레이션 전체 | **서버만** | 변경된 청크(Dirty Chunk)만 스냅샷으로 전송 |
| 렌더링 | **클라이언트만** | 수신한 청크 데이터를 텍스처로 베이크 |
| 플레이어 이동 (예측) | 클라이언트 + 서버 | NfE 클라이언트 예측 그대로 활용 |

청크 동기화는 NfE Ghost로 보내거나, 별도 커스텀 RPC로 Dirty Chunk만 압축 전송하는 두 가지 방법이 있다. 데이터량이 많으면 커스텀 RPC + LZ4 압축 권장.

---

## 메모리 / 빌드 크기 주의사항

- **WebGL 초기 메모리**: 현재 `32MB`. CA 그리드 크기에 따라 최소 `128MB`로 늘려야 할 수 있음.
- **빌드 크기**: DOTS 포함 시 WebGL 빌드 크기가 큼. `webGLAnalyzeBuildSize`로 확인 후 Code Stripping 수준 조정.
- **로딩 시간**: `.wasm` 파일이 크므로 Gzip 압축 + CDN 배포 필수.
