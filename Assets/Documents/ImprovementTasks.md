# 향후 개선 및 유지보수 과제

> 최종 갱신: 2026-04-30

---

## ✅ 완료된 항목

- [x] 멀티플레이어 이동 동기화 (Client Prediction 및 Server Reconciliation)
- [x] 클라이언트 측 지형(Pixel Grid) 로드 및 추락 방지
- [x] 고스트 엔티티 권한 자동 할당 시스템 (`EnsureLocalPlayerSystem`)
- [x] 서버/클라이언트 월드별 디버그 시각화 차별화 (`PlayerDebugBoxSystem`: 서버=주황, 클라이언트=파랑)
- [x] 클라이언트 In-Game 상태 전환 로직 (`GoInGameSystem` + `MapReadyTag`)
- [x] SPUM 프리팹 하이브리드 ECS 연동 (`PlayerVisualSystem` + `PlayerVisualReference`)
- [x] 픽셀 지형 실시간 동기화 — 청크 diff RPC (`PixelGridSyncSystem` → `PixelGridDeltaRpc` → `PixelGridRpcApplySystem`)
- [x] 완드 시스템 (`WandCastSystem`, `SpellProjectileSystem`, `LiquidSpraySystem`, `ManaRegenSystem`)
- [x] 투사체 이펙트 확장 — Explosion, LiquidSpray, Sand (`PixelImpactUtility`: BulletImpact/Explode/StampCircle/FreezeCircle/ScorchLine)
- [x] 아레나 매치 시스템 — 타이머·용암 상승·승패 판정·팀 부활 (`ArenaMatchSystem`, `TeamRespawnSystem`)
- [x] 청크 Dirty flag 동기화 최적화 (`ChunkMeta.SyncDirty` 기반 PixelGridSyncSystem)
- [x] 방(Room) 관리 시스템 — 2인 접속 시 자동 리셋·카운트다운 (`RoomWatchSystem`, `RoomResetSystem`, `MatchCountdownSystem`)
- [x] SPUM 랜덤 외형 자동 적용 (`SpumAppearanceRandomizer`)
- [x] 시차 스크롤 배경 (`ParallaxBackground`)

---

## 🚀 우선 순위 과제

### 높음

| 항목 | 설명 |
|---|---|
| `PlayerController.cs` 삭제 | `[Obsolete]` 표시됨. ECS `PlayerMoveSystem` + `PlayerVisualSystem`으로 완전 이전 완료. |
| `TeamRespawnSystem` 딜레이 구현 | 현재 즉시 부활 — `PlayerRespawnTimer` 컴포넌트의 `RespawnDelay`를 이용한 지연 부활로 교체 필요. |
| `RoomStateSingleton` Authoring 추가 | 씬에 배치하는 Authoring이 없어 초기값을 코드 외부에서 설정하기 불편함. |

### 중간

| 항목 | 설명 |
|---|---|
| `WandComponents.cs` Bounce 수정자 미구현 | `SpellModifier.Bounce = 3` 정의됨, 처리 로직 없음. |
| `ArenaMatchSystem` RPC 전송 주기 외부화 | `MATCH_SYNC_INTERVAL = 1f` 상수를 `SimulationConstants`로 이동. |
| `PixelImpactUtility.BulletImpact` 하드코드 제거 | 파괴 가능 셀 목록이 하드코드 — `CellTypeExt.IsDestructibleByImpact()` 활용으로 교체. |
| `MatchCountdownSystem` UI 연동 | 카운트다운 RPC를 수신해 HUD에 표시하는 클라이언트 시스템 부재. |

### 낮음

| 항목 | 설명 |
|---|---|
| `SurvivalCheckSystem` 역할 재정리 | `ArenaMatchSystem`과 중복 로직 — 레거시 배틀로얄 모드 전용임을 명확히 분리하거나 제거. |
| WebGL 빌드 대응 | `Plan/WebGL_Plan.md` 참조 — NetCode WebSocket 전환, 서버 분리 빌드 테스트 필요. |
| 매몰 승리 규칙 구현 | `Plan/BurialVictoryRule_Plan.md` 참조 — 기절 + 삽 매몰 흐름. |

---

## 🛠️ 장기 과제

- [ ] WebGL 환경 NetCode 안정성 테스트 (WebSocket 전환)
- [ ] 매치메이킹 서버 연동 (방 목록 조회·자동 매칭)
- [ ] 완드 Bounce 수정자 구현 (`SpellModifier.Bounce`)
- [ ] 강체(RigidBody) 시스템 레거시 정리 — `RigidBodyECSBridge`, `RigidBodyNode` MonoBehaviour 제거
