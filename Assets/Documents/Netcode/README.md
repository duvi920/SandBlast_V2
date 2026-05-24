# SandBlast Netcode for Entities — 구현 현황

> Unity DOTS + Netcode for Entities 기반 멀티플레이어 동기화 구조  
> 최종 갱신: 2026-04-30

---

## 주요 구현 이력

| 날짜 | 내용 |
|---|---|
| 2026-04-27 | 클라이언트 예측 활성화, MapLoadSystem 클라이언트 적용, EnsureLocalPlayerSystem, GoInGameSystem |
| 2026-04-27 | 완드 시스템, 투사체 이펙트, 아레나 매치, 청크 SyncDirty 최적화 |
| 2026-04-30 | 방 관리 시스템 (RoomWatch/Reset/Countdown), SPUM 랜덤 외형, 시차 스크롤 배경 |

---

## 시스템 구조 및 실행 순서

### 1. Connection & Bootstrap
| 시스템 | World | 설명 |
|------|-------|------|
| `GoInGameSystem` | Client + Server | `MapReadyTag` 확인 후 `NetworkStreamInGame` 추가 및 플레이어 스폰 |
| `EnsureLocalPlayerSystem` | Client | 로컬 플레이어 엔티티 식별 및 `GhostOwnerIsLocal` 권한 부여 |
| `MapLoadSystem` | Server + Client | MapTemplate → PixelGrid 1회 적용 + `MapReadyTag` 생성 |

### 2. 방(Room) 관리
| 시스템 | World | 설명 |
|------|-------|------|
| `RoomWatchSystem` | Server | InGame 접속자 수 감시 — 1→2 시 `RoomResetRequestTag` 생성·Phase=Countdown |
| `RoomResetSystem` | Server | 맵·플레이어·매치 상태 초기화 → `RoomResetRpc` 브로드캐스트 |
| `MatchCountdownSystem` | Server | Countdown 3→0 RPC 브로드캐스트 → Phase=Battle |

### 3. Input
| 시스템 | World | 설명 |
|------|-------|------|
| `GatherInputSystem` | Client | 로컬 입력 수집 → `PlayerInput` (GhostInput) |

### 4. Movement (Predicted)
*모든 이동 시스템은 `PredictedSimulationSystemGroup`에서 실행되며 클라이언트 예측을 지원함.*

| 시스템 | World | 설명 |
|------|-------|------|
| `GroundCheckSystem` | Client + Server | 픽셀 그리드 기반 지면 판정 |
| `GravitySystem` | Client + Server | 중력 가속도 적용 |
| `PlayerMoveSystem` | Client + Server | 입력 기반 이동, 픽셀 충돌 해소 및 `LocalTransform` 갱신 |

### 5. Combat (Server 전용)
| 시스템 | World | 설명 |
|------|-------|------|
| `WandCastSystem` | Server | 완드 발사 — 마나 소모, 쿨다운, MultiShot |
| `SpellProjectileSystem` | Server | 투사체 지형 충돌·폭발·플레이어 피해·관통 |
| `LiquidSpraySystem` | Server | 액체 분사형 투사체 — 비행 경로에 셀 생성 |
| `ManaRegenSystem` | Server | 마나 회복 |
| `PoisonDamageSystem` | Server | 독 웅덩이 접촉 2 HP/s 피해 |
| `HitDetectionSystem` | Server | 투사체-플레이어 충돌 판정 |
| `DamageSystem` | Server | HP 0 → `IsDead` 설정 |

### 6. 픽셀 그리드 동기화
| 시스템 | World | 설명 |
|------|-------|------|
| `PixelGridSyncSystem` | Server | 5틱마다 활성 청크 SyncDirty 확인 → `PixelChunkDiff` 갱신 |
| `PixelGridRpcSendSystem` | Server | `PixelGridDeltaRpc` 전체 클라이언트 브로드캐스트 |
| `PixelGridRpcApplySystem` | Client | `PixelGridDeltaRpc` 수신 → 로컬 PixelGrid Type 채널 패치 |
| `PixelGridInitialSyncSystem` | Server | 신규 접속 시 전체 맵 스냅샷 1회 전송 (SLEEPING 청크 포함) |
| `TerrainPaintRpcSystem` | Server | `TerrainPaintRpc` 수신 → 서버 그리드 직접 적용 |

### 7. 게임 모드 (Server 전용)
| 시스템 | World | 설명 |
|------|-------|------|
| `ArenaMatchSystem` | Server | 타이머·용암 상승·1v1/2v2 승패 판정·RPC 브로드캐스트 |
| `TeamRespawnSystem` | Server | 2v2 팀원 부활 (RespawnCount 소비) |

### 8. Presentation
| 시스템 | World | 설명 |
|------|-------|------|
| `PlayerVisualSystem` | Client | `PlayerMovementData` → SPUM Animator 상태 갱신, 초기 외형 랜덤화 |
| `CameraFollowSystem` | Client | 로컬 플레이어 추적 및 맵 경계 제한 |
| `ArenaHudSystem` | Client | `ArenaMatchTickRpc` / `ArenaMatchEndRpc` 수신 → `ArenaHudBehaviour` 갱신 |
| `PlayerDebugBoxSystem` | Client + Server | 서버(주황)/클라이언트(파랑) 플레이어 위치 박스 시각화 |

---

## 컴포넌트 동기화 (Ghost)

| 컴포넌트 | 전송 방식 | 설명 |
|---------|---------|------|
| `PlayerGhostData` | All (Snapshot) | 위치, 속도, 체력, 팀, 부활 횟수 등 핵심 상태 |
| `PlayerInput` | Owner → Server | 클라이언트 입력 데이터 (GhostInput) |
| `PlayerTag` | All | 플레이어 엔티티 식별 태그 |
| `PlayerMovementData` | All | 현재 이동 상태 (Idle/Moving/Jumping/Dashing) + IsGrounded |
| `ManaComponent` | All | 현재/최대 마나 — HUD 표시용 |
| `BulletGhostData` | All | 투사체 위치·속도·소유자 |
| `ItemGhostData` | All | 아이템 위치·종류·획득 여부 |
| `RigidBodyGhostData` | All | 강체 위치·파괴 상태 |

## RPC 목록

| RPC | 방향 | 설명 |
|---|---|---|
| `PixelGridDeltaRpc` | Server → Client | 청크 diff 동기화 (FixedBytes4096) |
| `ArenaMatchTickRpc` | Server → Client | 매치 타이머·점수 주기 동기화 |
| `ArenaMatchEndRpc` | Server → Client | 매치 결과 전송 |
| `RoomResetRpc` | Server → All | 방 리셋 완료 알림 |
| `MatchCountdownRpc` | Server → All | 카운트다운 숫자 (3·2·1·0=GO) |
| `TerrainPaintRpc` | Client → Server | 마우스 지형 페인트 요청 |
