# SandBlast 멀티플랫폼 로드맵
## 싱글플레이어 → 호스트 멀티 → MMORPG 전환 계획

> 작성일: 2026-05-27  
> 기준: SandBlast_V2 PvP 아레나 완료 시점  
> 전제: 횡스크롤 액션 RPG 전환(별도 문서 참조)을 완료한 상태를 기점으로 한다

---

## 0. 핵심 설계 원칙

**"코어는 한 번, 네트워크 레이어만 교체한다"**

픽셀 CA 물리, 전투, 이동, AI, 진행 시스템은 세 모드 모두에서 동일하다.  
모드 전환은 **네트워크 어댑터**와 **저장 레이어**만 갈아끼우는 방식으로 설계한다.  
이 원칙을 지키지 않으면 모드 추가마다 전체 게임 로직을 재작성하게 된다.

```
┌─────────────────────────────────────────────────────────────┐
│                    Game Core  (공통, 변경 없음)               │
│  CA 물리 엔진 | 전투 | 이동 | 완드 | 적 AI | 진행 | 인벤토리  │
└────────────────────────────┬────────────────────────────────┘
                             │
           ┌─────────────────┼──────────────────┐
           ▼                 ▼                  ▼
   ┌───────────────┐ ┌──────────────┐ ┌──────────────────┐
   │ OfflineAdapter│ │HostNfEAdapter│ │  MMOZoneAdapter  │
   │  (로컬만)     │ │(NfE Listen)  │ │ (전용 서버 존)    │
   └───────────────┘ └──────────────┘ └──────────────────┘
           │                 │                  │
   ┌───────────────┐ ┌──────────────┐ ┌──────────────────┐
   │ LocalJsonSave │ │HostFileSave  │ │  RemoteDBSave    │
   └───────────────┘ └──────────────┘ └──────────────────┘
```

---

## 1. 세 가지 모드 핵심 비교

| 항목 | 싱글플레이어 | 호스트 멀티 | MMORPG |
|------|-------------|------------|--------|
| **동시 접속 인원** | 1 | 2~8 | 수백~수천 |
| **서버 형태** | 없음 (로컬) | 플레이어 1인이 호스트 | 전용 서버 클러스터 |
| **네트워크 기술** | 없음 | NetCode for Entities (NfE) | NfE 전용서버 모드 + 존 샤딩 |
| **픽셀 그리드** | 클라이언트 전체 시뮬레이션 | 호스트 권위, 클라이언트 예측 | 존 서버별 분리 시뮬레이션 |
| **저장 방식** | 로컬 JSON | 호스트 파일 | 원격 DB (PostgreSQL) |
| **레벨 구조** | 선형 레벨 | 공유 인스턴스 레벨 | 오픈 월드 존 + 인스턴스 던전 |
| **캐릭터 소유** | 로컬 | 호스트 관리 | 서버 관리 (위변조 불가) |
| **콘텐츠** | 솔로 캠페인 | Co-op 캠페인 / PvP | 퀘스트·거래·길드·PvP 필드 |
| **인프라 비용** | 없음 | 없음 (호스트 부담) | 서버 임대·운영비 필요 |
| **개발 난이도** | ★★☆☆☆ | ★★★☆☆ | ★★★★★ |
| **출시 난이도** | ★★☆☆☆ | ★★★☆☆ | ★★★★★ |

---

## 2. 공통 코어 아키텍처 (세 모드 공유)

싱글플레이어를 구현하는 동시에 아래 인터페이스를 확립해야 나중에 모드 전환이 가능하다.

### 2-A. INetworkAdapter 인터페이스

```csharp
// 모든 네트워크 모드가 구현할 인터페이스
public interface INetworkAdapter {
    void Initialize();
    void Shutdown();
    bool IsServer { get; }
    bool IsClient { get; }
    void SendPlayerInput(PlayerInputData input);
    void BroadcastGameState(GameStateDelta delta);
    event Action<PlayerJoinEvent> OnPlayerJoined;
    event Action<PlayerLeaveEvent> OnPlayerLeft;
}

// 싱글: 자기 자신이 서버이자 클라이언트
public class OfflineAdapter : INetworkAdapter { ... }

// 호스트: NfE Listen Server 래핑
public class HostNfEAdapter : INetworkAdapter { ... }

// MMO: NfE Dedicated + Zone 라우팅
public class MMOZoneAdapter : INetworkAdapter { ... }
```

### 2-B. IPlayerDataRepository 인터페이스

```csharp
// 저장 레이어 추상화
public interface IPlayerDataRepository {
    Task<PlayerSaveData> LoadAsync(string playerId);
    Task SaveAsync(string playerId, PlayerSaveData data);
    Task<bool> ExistsAsync(string playerId);
}

public class LocalJsonRepository  : IPlayerDataRepository { ... }  // 싱글
public class HostFileRepository   : IPlayerDataRepository { ... }  // 호스트
public class RemoteApiRepository  : IPlayerDataRepository { ... }  // MMO
```

### 2-C. IWorldStateRepository 인터페이스

```csharp
// 픽셀 그리드 영속성 추상화
public interface IWorldStateRepository {
    // 레벨/존 진입 시 지형 로드
    Task<PixelGridSnapshot> LoadZoneAsync(ZoneId zoneId);
    // 변경사항 저장 (싱글: 즉시, MMO: 주기적 flush)
    Task SaveChunkDeltaAsync(ZoneId zoneId, ChunkDelta delta);
}
```

### 2-D. 공통 게임 시스템 목록

아래 시스템들은 **모드 관계없이 동일 코드**를 사용한다.

| 시스템 | 비고 |
|--------|------|
| CA 픽셀 물리 5개 Job | 그리드 크기만 가변 |
| PlayerMoveSystem | 클라이언트 예측 그대로 |
| WandCastSystem | 서버 권위 여부만 플래그 |
| EnemyAISystem | 서버에서만 실행 (플래그로 분기) |
| XPSystem / StatScalingSystem | 서버 권위 필수 |
| InventorySystem | 서버 권위 필수 |
| LevelObjectiveSystem | 서버 권위 필수 |

---

## 3. 모드 1 — 싱글플레이어

### 3-A. 목표 게임플레이

- 1인 솔로 캠페인 (5 월드 × 3 레벨 + 보스)
- 오프라인 완전 지원
- 스팀·에픽·모바일 패키지 판매 모델

### 3-B. 아키텍처

```
Unity ECS World (단일 World)
  ├── 모든 시뮬레이션 (CA, AI, 전투, 진행)
  ├── OfflineAdapter (네트워크 없음)
  └── LocalJsonRepository (PlayerData + WorldState)
```

- `ClientServerBootstrap` 제거
- `DefaultWorldInitialization`으로 단순화
- 모든 시스템이 하나의 World에서 실행

### 3-C. 저장 데이터 구조

```json
{
  "playerId": "local",
  "playerLevel": 5,
  "xp": 1240,
  "skills": ["explosive_radius", "double_jump"],
  "inventory": [
    { "itemId": "wand_rare_003", "slot": 0 },
    { "itemId": "spell_lava_spray", "slot": 1 }
  ],
  "currentWorld": 2,
  "currentLevel": 1,
  "checkpointPos": { "x": 340, "y": 128 }
}
```

픽셀 그리드 상태는 **레벨 재시작** 방식으로 처리한다. 저장 시점의 지형 파괴 상태는 유지하지 않는다 (레벨 리셋 후 체크포인트 위치에서 재시작).

> 지형 영속성이 필요한 경우: `NativeArray<byte>` 전체를 바이너리로 직렬화하면 가능하지만, 레벨당 ~512KB 이상 → 추후 옵션으로 고려.

### 3-D. 개발 우선순위

Phase A~E (별도 문서 `횡스크롤_액션RPG_전환계획.md` 참조)  
**추가 요구사항:** 시작부터 `INetworkAdapter`, `IPlayerDataRepository` 인터페이스를 통해 시스템을 작성할 것 — 직접 static 호출 금지.

---

## 4. 모드 2 — 호스트 방식 멀티플레이어

### 4-A. 목표 게임플레이

- Co-op 캠페인 (2~4인, 같은 레벨 공유)
- 선택적 PvP 모드 (아레나 재활용)
- 한 명이 방 열면 나머지가 참가하는 P2P/Relay 구조
- 스팀 멀티플레이, LAN 파티 지원

### 4-B. 핵심 결정 — NfE 재활용

**기존 NetCode for Entities 코드의 약 60%를 재사용할 수 있다.**  
현재 PvP 아레나가 이미 NfE Listen Server 구조이므로, Co-op으로 전환하는 비용이 새로 작성하는 것보다 훨씬 낮다.

```
현재 NfE 구조 (PvP)        →    호스트 멀티 구조 (Co-op)
───────────────────────────────────────────────────────
PlayerGhostData             →    PlayerGhostData (그대로)
PixelGridDeltaRpc           →    PixelGridDeltaRpc (그대로)
WandCastSystem (서버 권위)  →    그대로
ArenaMatchSystem (PvP 조건) →    LevelObjectiveSystem (Co-op 목표)
RoomWatchSystem (2인 대기)  →    LobbySystem (N인 + 준비 확인)
TeamRespawn (PvP 팀)        →    CoopRespawnSystem (팀원 부활)
```

### 4-C. 아키텍처

```
호스트 머신
  ├── Server World (NfE ServerWorld)
  │   ├── CA 물리, AI, 전투, 진행 (권위)
  │   ├── HostNfEAdapter
  │   └── HostFileRepository
  └── Client World (NfE ClientWorld)
      ├── 플레이어 입력·예측
      └── 렌더링

클라이언트 머신 (게스트)
  └── Client World
      ├── 서버 state 수신·적용
      └── 렌더링
```

### 4-D. Co-op 특화 추가 시스템

```csharp
// 팀원 부활: 죽은 플레이어가 살아있는 팀원 옆에 부활
public class CoopRespawnSystem : SystemBase {
    // 사망 후 10초 대기 → 팀원 중 1명이 살아있으면 옆에 재생성
    // 전원 사망 = 마지막 체크포인트부터 재시작
}

// 공유 인벤토리 없음, 개인 인벤토리 유지
// 아이템 드롭은 먼저 줍는 사람이 가져감

// 로비 시스템 (방 코드 입력 / Steam Lobby API)
public class LobbySystem : SystemBase {
    // Unity Relay + Lobby 패키지 활용
    // 방 생성: 8자리 코드 발급
    // 참가: 코드 입력 → 서버 연결
}
```

### 4-E. 픽셀 그리드 동기화 재활용

현재 `PixelGridDeltaRpc`가 이미 청크 델타 동기화를 구현하고 있다.  
Co-op에서 추가 필요한 것:
- **대역폭 최적화**: 2→4인으로 늘면 동시 활성 청크 증가 → RLE 압축 고려
- **초기 동기화 최적화**: 신규 참가자에게 전체 맵 스냅샷 전송 (`PixelGridInitialSyncSystem` 기존 코드 재사용)

### 4-F. 싱글 → 호스트 전환 비용

싱글플레이어를 `INetworkAdapter` 기반으로 작성했다면:

| 작업 | 예상 기간 |
|------|-----------|
| `OfflineAdapter` → `HostNfEAdapter` 교체 | 1주 |
| NfE Ghost 컴포넌트 복원 | 1주 |
| LobbySystem 신규 작성 | 2주 |
| Co-op 부활·목표 시스템 조정 | 1~2주 |
| 밸런싱 (적 HP 스케일, 아이템 드롭율) | 1주 |
| **합계** | **6~8주** |

---

## 5. 모드 3 — MMORPG

### 5-A. 목표 게임플레이

- 수백 명이 같은 오픈 월드를 공유
- 지속적 세계 (서버가 꺼지지 않으면 지형 변화 영구 반영)
- 퀘스트, 거래, 길드, PvP 필드, 레이드 던전
- F2P 또는 월정액 BM

### 5-B. MMO에서 픽셀 CA 물리의 근본 문제

SandBlast의 핵심 기술을 MMO에 그대로 적용하면 다음 문제가 발생한다.

```
문제: 1000명이 같은 픽셀 그리드에서 동시에 지형을 파괴하면?
  - 현재 256×256 = 65,536 픽셀 → 서버 1코어로 처리 가능
  - MMO 오픈 월드 4096×4096 = 16,777,216 픽셀
  - 1000명이 각자 폭발, 용암 생성 시 → 동시 수십만 셀 업데이트
  - 단일 서버 처리 불가
```

**해결책: 존 기반 분산 시뮬레이션**

```
오픈 월드를 N개의 존(Zone)으로 분할
  각 존 = 독립 픽셀 그리드 (예: 512×256 픽셀)
  각 존 = 전용 서버 프로세스 1개
  플레이어가 존 경계를 넘으면 → 다음 존 서버로 핸드오프

Zone Server 0   Zone Server 1   Zone Server 2
  [플레이어 A,B]  [플레이어 C,D]  [플레이어 E]
  [픽셀 그리드]   [픽셀 그리드]   [픽셀 그리드]
       │               │               │
  [Zone DB]       [Zone DB]       [Zone DB]  (PostgreSQL)
```

**존 용량 한계 설계:**
- 존당 최대 플레이어: 50~100명 (서버 부하 기준)
- 초과 시: 동일 지형의 **인스턴스** 생성 (다른 서버 프로세스)
- 인스턴스 내 지형 변화는 해당 인스턴스에만 적용

### 5-C. MMO 서버 아키텍처

```
                    ┌─────────────────┐
                    │   Load Balancer  │
                    │  (HAProxy/Nginx)  │
                    └────────┬─────────┘
                             │
         ┌───────────────────┼───────────────────┐
         ▼                   ▼                   ▼
  ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
  │  Auth Server│   │  Zone Server│   │  Chat Server│
  │  (로그인)   │   │  (게임 로직)│   │  (채팅)     │
  │             │   │  NfE+CA물리 │   │  WebSocket  │
  └──────┬──────┘   └──────┬──────┘   └─────────────┘
         │                 │
  ┌──────▼──────┐   ┌──────▼──────┐
  │  Auth DB    │   │  Zone DB    │
  │ (계정·캐릭) │   │ (지형·아이템)│
  │ PostgreSQL  │   │ PostgreSQL  │
  └─────────────┘   └─────────────┘
                         │
                   ┌─────▼─────┐
                   │  Redis    │
                   │ (세션·캐시)│
                   └───────────┘
```

**Zone Server (핵심):**
- Unity Dedicated Server 빌드 (Linux 헤드리스)
- NfE ServerWorld + CA 물리 시뮬레이션
- 담당 존의 픽셀 그리드만 시뮬레이션
- 플레이어 입력 수신, 물리 처리, state 브로드캐스트
- 주기적으로 청크 델타를 Zone DB에 flush

### 5-D. MMO 전용 신규 시스템

#### (1) 존 관리 시스템

```csharp
// 서버 사이드 (Zone Orchestrator)
public class ZoneManagerService {
    Dictionary<ZoneId, ZoneServerProcess> activeZones;

    // 존 활성화: 플레이어 진입 시 서버 프로세스 시작
    async Task<ZoneServerInfo> ActivateZoneAsync(ZoneId id);
    
    // 존 비활성화: 15분간 플레이어 없으면 프로세스 종료 후 DB 저장
    async Task DeactivateZoneAsync(ZoneId id);
    
    // 인스턴스 생성: 존 용량 초과 시
    async Task<ZoneServerInfo> CreateInstanceAsync(ZoneId id);
}
```

#### (2) 플레이어 핸드오프 시스템

```csharp
// 존 경계 통과 처리
public class ZoneTransferSystem : SystemBase {
    // 1. 플레이어가 존 경계 픽셀에 도달
    // 2. Auth Server에 전송 요청
    // 3. 현재 존에서 플레이어 데이터 스냅샷
    // 4. 다음 존 서버에 플레이어 스폰 요청
    // 5. 클라이언트에 새 서버 주소 + 포트 전달
    // 6. 클라이언트가 새 존 서버에 재접속
    // 총 지연: 목표 500ms 이하
}
```

#### (3) 지형 영속성 시스템

```csharp
// 픽셀 그리드 변화를 DB에 저장
public class TerrainPersistenceSystem : SystemBase {
    // 매 30틱마다: 변경된 청크 식별
    // 압축: LZ4로 청크 바이너리 압축
    // 비동기 flush: DB에 upsert
    // 로드: 서버 시작 시 DB에서 전체 복원

    // DB 스키마
    // zone_chunks(zone_id, chunk_x, chunk_y, data BYTEA, updated_at)
}
```

#### (4) MMO 경제·사회 시스템

```csharp
// 플레이어 간 거래소
public class MarketplaceSystem { ... }

// 길드 (최대 50명)
public class GuildSystem { ... }

// 퀘스트 (NPC → 목표 → 보상)
public class QuestSystem { ... }

// 글로벌 이벤트 (용암 범람 월드 이벤트 등)
public class WorldEventSystem { ... }
```

### 5-E. 호스트 → MMO 전환 비용

| 작업 | 예상 기간 | 난이도 |
|------|-----------|--------|
| 전용 서버 빌드 (Linux 헤드리스) | 1~2주 | 낮음 |
| 존 분할 + ZoneManagerService | 4~6주 | 높음 |
| 플레이어 핸드오프 시스템 | 3~4주 | 높음 |
| Auth Server (로그인, JWT) | 2~3주 | 중간 |
| 지형 영속성 (PostgreSQL 연동) | 3~4주 | 높음 |
| 클라이언트 존 전환 UX | 2주 | 중간 |
| 퀘스트 시스템 | 4~6주 | 중간 |
| 길드·채팅 시스템 | 3~4주 | 중간 |
| 거래소 시스템 | 4~6주 | 높음 |
| 인프라 구축 (Docker, k8s) | 2~3주 | 높음 |
| 부하 테스트 + 최적화 | 4~6주 | 높음 |
| **합계** | **32~48주** | |

---

## 6. 전환 로드맵 전체

```
[현재: PvP 아레나]
         │
         ▼  (5~6개월, 별도 문서 참조)
[Phase 1~5: 싱글플레이어 횡스크롤 RPG]
  - INetworkAdapter / IPlayerDataRepository 인터페이스 확립
  - 캐릭터 진행, 적 AI, 보스, 5 월드
  - 출시 가능 상태 (스팀 얼리 액세스 등)
         │
         ▼  (+6~8주)
[Phase 6: 호스트 방식 Co-op 멀티 추가]
  - HostNfEAdapter 구현 (기존 NfE 재활용)
  - LobbySystem, CoopRespawnSystem
  - 동일 콘텐츠를 2~4인 Co-op으로 플레이
  - 선택적 PvP 아레나 모드 복원
         │
         ▼  (+8~12개월)
[Phase 7: MMORPG 전환]
  - 존 기반 서버 아키텍처
  - 지형 영속성
  - MMO 경제·사회 시스템
  - 오픈 월드 설계
```

---

## 7. 모드별 기술 스택 비교

| 구성 요소 | 싱글 | 호스트 멀티 | MMO |
|-----------|------|------------|-----|
| **Unity** | 6 (client only) | 6 (client + listen server) | 6 (client) |
| **서버 런타임** | 없음 | Unity (호스트 머신) | Unity Dedicated Server (Linux) |
| **네트워킹** | 없음 | NfE + Unity Transport | NfE + Unity Transport + Relay |
| **인증** | 없음 | 없음 | JWT (ASP.NET Core / Node.js) |
| **데이터베이스** | JSON 파일 | JSON 파일 | PostgreSQL |
| **캐시** | 없음 | 없음 | Redis |
| **인프라** | 없음 | 없음 | Docker + Kubernetes (GKE/AWS EKS) |
| **릴레이** | 없음 | Unity Relay | 자체 또는 Unity Relay |
| **모니터링** | 없음 | 없음 | Grafana + Prometheus |

---

## 8. 아키텍처 결정 기록 (ADR)

### ADR-001: 싱글플레이어에서 NetCode for Entities 제거 여부

**결정:** 싱글플레이어에서는 NfE를 완전 제거한다.  
**이유:**
- NfE는 World를 Server/Client 두 개로 분리하는 오버헤드가 크다
- 싱글에서 Ghost 동기화는 불필요한 CPU/메모리 낭비
- `INetworkAdapter` 인터페이스로 추후 복원이 가능하므로 손실 없음  
**트레이드오프:** Co-op 추가 시 NfE를 다시 통합해야 하지만, Phase 6에서 6~8주 비용이면 감수할 수 있다.

---

### ADR-002: MMO 픽셀 물리 — 단일 그리드 vs 존 분할

**결정:** 존 기반 분산 시뮬레이션을 채택한다.  
**이유:**
- 단일 거대 그리드(4096×4096)는 서버 1대로 시뮬레이션 불가 (16M 픽셀/틱)
- 존 분할은 서버를 수평 확장할 수 있는 유일한 방법
- 존 경계에서 물리 연속성 끊김은 경계를 "수직 벽"으로 디자인하면 자연스럽게 숨길 수 있음  
**트레이드오프:** 존 핸드오프 구현 복잡도 증가, 존 경계 근처에서 물리 상호작용 불가.

---

### ADR-003: MMO 지형 영속성 — 전체 저장 vs 델타 저장

**결정:** 청크 단위 델타 저장을 채택한다.  
**이유:**
- 전체 존 그리드 저장 시 ~512KB~2MB/존 → 변경이 없어도 주기적 전체 저장은 낭비
- 기존 `PixelGridDeltaRpc`가 이미 청크 델타를 추출하는 로직을 갖고 있음
- DB 스키마: `zone_chunks(zone_id, chunk_x, chunk_y, data)` — 변경된 청크만 upsert  
**트레이드오프:** 최초 존 생성 시 기준 스냅샷 필요, 복원 로직이 약간 복잡.

---

### ADR-004: MMO 인증 서버 기술 선택

**결정:** ASP.NET Core (C#)로 Auth Server 구현.  
**이유:**
- Unity 클라이언트 코드와 동일 언어 → 데이터 구조 공유 가능 (shared library)
- JWT + PostgreSQL 연동이 성숙하고 문서가 풍부  
**트레이드오프:** Node.js에 비해 초기 설정 복잡, 컨테이너 이미지 크기 큼.

---

## 9. 기술 리스크 요약

| 리스크 | 모드 | 심각도 | 대응 |
|--------|------|--------|------|
| INetworkAdapter를 나중에 추가하면 대규모 리팩토링 | 싱글 | 높음 | 처음부터 인터페이스 기반으로 작성 |
| NfE 재통합 시 Ghost 컴포넌트 충돌 | 호스트 | 중간 | 인터페이스로 감싸서 교체 비용 최소화 |
| 존 경계에서 물리 연속성 깨짐 | MMO | 중간 | 경계를 SOLID_INDESTRUCTIBLE 벽으로 설계 |
| 지형 영속성 DB 병목 | MMO | 중간 | 비동기 flush + Redis 버퍼 레이어 |
| 존 전환 시 500ms 이상 지연 | MMO | 중간 | 미리 다음 존 서버 예열 (pre-warm) |
| 악성 사용자 클라이언트 조작 | MMO | 높음 | 서버 권위 필수, 클라이언트 입력만 신뢰 |
| Unity Dedicated Server 운영 비용 | MMO | 중간 | 스팟 인스턴스 + 오토스케일링 |

---

## 10. 우선순위 및 의사결정 포인트

### 10-A. 반드시 싱글플레이어 단계에서 해야 할 것

1. `INetworkAdapter` 인터페이스 정의 및 `OfflineAdapter` 구현 — **나중에 추가하면 대규모 리팩토링**
2. `IPlayerDataRepository` 인터페이스 정의 — **MMO에서 DB 교체 비용 최소화**
3. 서버 권위 시스템 표시 — 어떤 시스템이 서버에서만 실행되어야 하는지 주석/플래그 추가

### 10-B. 호스트 전환 결정 시점

싱글플레이어 월드 2~3 완성 후, 플레이어 피드백을 보고 Co-op 수요 확인 후 결정.  
조건: 싱글 콘텐츠가 5~8시간 이상의 플레이 가치를 갖춰야 Co-op 투자가 의미 있다.

### 10-C. MMO 전환 결정 시점

호스트 멀티 출시 후 동시 접속자 수, 커뮤니티 규모를 보고 결정.  
MMO는 운영 비용이 0→∞로 뛰는 단계 — 수익 모델 확정 없이 진입하면 위험하다.  
**최소 조건:** 월 서버 비용을 커버하는 수익 모델 확정, 팀 규모 3인 이상.

---

## 11. 각 모드별 출시 체크리스트

### 싱글플레이어 출시 조건
- [ ] 5 월드 × 3 레벨 + 보스 5종 완성
- [ ] 저장·불러오기 안정적 작동
- [ ] 15fps 이상 유지 (저사양 PC 기준)
- [ ] 치명적 버그 없음 (1시간 플레이 기준)
- [ ] 스팀 페이지 / 상점 등록

### 호스트 멀티 출시 조건
- [ ] 2~4인 Co-op 1 월드 안정 동작
- [ ] LAN 및 인터넷 (Unity Relay) 연결 지원
- [ ] 호스트 연결 끊김 처리 (호스트 마이그레이션 or 세션 종료)
- [ ] 핑 200ms 이하에서 조작감 유지

### MMO 오픈 베타 조건
- [ ] 존 3개 이상, 동시 접속 100명 테스트 통과
- [ ] 지형 영속성 72시간 연속 운영 테스트
- [ ] Auth Server 보안 감사 (취약점 점검)
- [ ] 수익 모델 구현 (배틀패스 or 아이템샵)
- [ ] 운영팀 최소 2인 (CS, 서버 관리)
