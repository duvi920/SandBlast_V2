# 솔로 연습 → 상대 입장 시 방 리셋 & 즉시 대전 시작

**작성일:** 2026-04-28  
**상태:** 구현 완료 (버그픽스 포함)

---

## 개요

혼자 방에서 자유롭게 탐색·연습하다가 상대방이 접속하는 순간 맵을 리셋하고
카운트다운 후 정식 대전을 시작한다.

```
[플레이어 1명]  ──  상대 접속  ──▶  [리셋]  ──▶  [3·2·1·GO]  ──▶  [대전]
   Solo 모드                                                       Battle 모드
```

---

## 방 상태 (RoomPhase)

| Phase | 조건 | HUD |
|-------|------|-----|
| `Solo` | 접속자 1명 | "상대를 기다리는 중..." |
| `Countdown` | 2번째 플레이어 접속 직후 | 중앙 숫자 3·2·1·GO |
| `Battle` | 카운트다운 종료 | 타이머·체력·점수 |

---

## 구현 파일

### 신규

| 파일 | 역할 |
|------|------|
| `Components/Local/RoomStateSingleton.cs` | Phase·CountdownTimer·LastPlayerCount 보관 |
| `Components/Local/RoomResetRequestTag.cs` | 리셋 실행 트리거 마커 |
| `Components/Rpc/RoomResetRpc.cs` | 서버→클라이언트 리셋 알림 |
| `Components/Rpc/MatchCountdownRpc.cs` | 서버→클라이언트 카운트다운 숫자(3·2·1·0) |
| `Authoring/RoomStateAuthoring.cs` | GameManager 씬 오브젝트에 부착해 베이킹 |
| `Systems/Bootstrap/RoomWatchSystem.cs` | 접속자 수 1→2 감지 → Countdown 전환 + 리셋 요청 생성 |
| `Systems/Bootstrap/RoomResetSystem.cs` | 맵·플레이어·매치 상태 완전 초기화 후 RoomResetRpc 전송 |
| `Systems/BattleRoyale/MatchCountdownSystem.cs` | 매초 RPC 전송 → 0이 되면 Battle 전환 |

### 수정

| 파일 | 변경 내용 |
|------|-----------|
| `Systems/BattleRoyale/ArenaMatchSystem.cs` | `Phase == Battle`일 때만 실행 |
| `Systems/BattleRoyale/SurvivalCheckSystem.cs` | `Phase == Battle`일 때만 실행 |
| `Presentation/ArenaHudBehaviour.cs` | Phase별 HUD 분기 (대기·카운트다운·배틀) |
| `Systems/Presentation/ArenaHudSystem.cs` | `RoomResetRpc` / `MatchCountdownRpc` 수신 처리 |

---

## 버그픽스

| 커밋 | 내용 |
|------|------|
| `af11138` | `RoomResetSystem.cs`에 `using SandBlast;` 누락으로 `PixelGridSingleton` / `ChunkManagerSingleton` 타입을 못 찾던 컴파일 오류 수정 |

---

## 씬 설정

GameManager 오브젝트에 `RoomStateAuthoring` 컴포넌트를 추가한다.  
`CountdownDuration` (기본값 3초) Inspector에서 조절 가능.

---

## 엣지 케이스

| 상황 | 처리 |
|------|------|
| Countdown 중 한 명이 나감 | `RoomWatchSystem`이 2→1 감지 → Phase=Solo, 카운트다운 취소 |
| Battle 중 한 명이 나감 | 기존 `SurvivalCheckSystem`이 처리 |
| 3명 이상 접속 | `RoomWatchSystem`은 2명 기준으로만 동작 (추후 확장) |
