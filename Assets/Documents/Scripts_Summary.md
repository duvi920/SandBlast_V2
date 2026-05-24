# SandBlast 스크립트 요약

> 최종 갱신: 2026-04-30 (매몰 승리 규칙 — 기절·근접공격·삽·BurialCheck 추가)
> 구현 상태: noitaPvpPlan.md Phase 1~5 완료 + Room 관리 시스템 + 매몰 승리 규칙

---

## 1. ECS 코어 — 픽셀 CA 엔진 (`Assets/Scripts/ECS/`)

### 싱글턴 데이터 (`ECS/Singletons/`)

| 파일 | 역할 |
|---|---|
| `PixelGridSingleton.cs` | `NativeArray<byte>` 5채널(Type·Temp·Life·RigidId·Flam) 보관. WorldToGrid/GridToWorld 변환 내장. |
| `ChunkManagerSingleton.cs` | 32×32 청크 슬리핑 상태(`ChunkMeta[]`) 관리. MarkMoved()로 인접 청크 웨이크업. |
| `SimConfigSingleton.cs` | 틱 타이머, XShuffle 버퍼, CellUpdateTick(이중 처리 방지 플래그) 보관. |

### CA Job (`ECS/Jobs/`) — 모두 `[BurstCompile] struct : IJob`

| 파일 | 역할 | 설계 문서 |
|---|---|---|
| `PixelFireJob.cs` | 불 수명·열 방출·연료 전파·연기·물 소화·화약 폭발 | §6.5 |
| `PixelSmokeJob.cs` | 연기 상승·수평 확산·수명 소진 | §6.6 |
| `PixelLiquidJob.cs` | 물/용암/독 흐름·용암 냉각·용암+흙→현무암 | §6.3, §6.4 |
| `PixelPowderJob.cs` | 모래/재 낙하·안식각 퇴적 | §6.1, §6.2 |
| `PixelDebrisJob.cs` | 강체 파편 낙하·지형화 | §6.7 |

### 시스템 (`ECS/Systems/`)

| 파일 | 역할 |
|---|---|
| `PixelSimulationSystem.cs` | 마스터 시스템 — 틱 타이머 관리, 5개 Job을 .Run() 순차 실행 |
| `PixelRenderSystem.cs` | Texture2D GPU 업로드 (메인스레드 전용, MonoBehaviour 연동) |
| `SolidMaskSyncSystem.cs` | 강체 LocalToWorld 변화 감지 → SOLID_RIGID 마스크 갱신 |
| `RigidBodyGraphSystem.cs` | 강체 그래프 연결 상태 유지; 엣지 파괴 요청(EdgeBreakRequest) 처리 |
| `RigidToPixelConvertSystem.cs` | 강체 파괴 시 폴리곤 래스터화 → SOLID_DEBRIS 변환 |
| `SandBlastBootstrapSystem.cs` | SandBlastConfigComponent 읽어 그리드·청크·시뮬 싱글턴 1회 생성 |

### 컴포넌트 (`ECS/Components/`)

| 파일 | 역할 |
|---|---|
| `RigidBodyComponents.cs` | `RigidBodyNodeComponent`, `EdgeElement`, `PolygonVertex`, `RigidVelocityComponent`, `PendingPixelConversionTag`, `EdgeBreakRequest`, `EdgeBreakQueueSingleton` |

### 유틸

| 파일 | 역할 |
|---|---|
| `PixelImpactUtility.cs` | `BulletImpact()`, `Explode()` — 원형 반경 내 셀 제거 + 불 생성 |
| `PixelGridAPI.cs` | MonoBehaviour에서 ECS 그리드에 접근하는 정적 브리지 API |
| `RigidBodyECSBridge.cs` | 레거시 강체 시스템 → ECS 강체 명령 제출 |

---

## 2. 게임플레이 시스템 (`Assets/Scripts/Systems/`)

### 이동 (`Movement/`)

| 파일 | 역할 |
|---|---|
| `PlayerMoveSystem.cs` | 수평 이동·점프(코요테·버퍼)·대시·픽셀 지형 3방향 충돌 처리. 클라이언트+로컬. |
| `GravitySystem.cs` | 중력 가속도 적용. |
| `GroundCheckSystem.cs` | 발 아래 샘플 포인트로 지면 접촉 판정. |
| `DebugMoveSystem.cs` | 개발용 자유 이동. |

### 전투 (`Combat/`)

| 파일 | 역할 |
|---|---|
| `WandCastSystem.cs` | 완드 발사 — 마나 소모, 쿨다운, MultiShot(±15°) 처리. 서버 전용. |
| `SpellProjectileSystem.cs` | 투사체 지형 충돌·폭발(`PixelImpactUtility.Explode`)·플레이어 피해·관통. 서버. |
| `LiquidSpraySystem.cs` | 액체 분사 — 용암/독 투사체가 매 틱 진행 경로에 액체 셀 생성. 서버. |
| `ManaRegenSystem.cs` | 마나 회복 (RegenRate × dt). 서버. |
| `PoisonDamageSystem.cs` | 독 웅덩이 접촉 시 2 HP/s 지속 피해. 서버. |
| `BulletMoveSystem.cs` | 일반(비-완드) 투사체 이동. |
| `BulletTerrainHitSystem.cs` | 일반 투사체 지형 충돌. `.WithNone<ProjectileComponent>()`로 완드 투사체 제외. |
| `MeleeAttackSystem.cs` | Q키 근접 공격 — 범위 0.9u, 데미지 8, StunPower 40, 쿨다운 0.6s. 서버. |
| `StunSystem.cs` | HitEvent.StunPower 누적 → 100 도달 시 3초 기절. 자동 회복(10/s). 서버. |
| `ShovelUseSystem.cs` | E키로 자기 주변 반경 6px POWDER_SAND 스탬프. 3회 후 소멸. 서버. |
| `DamageSystem.cs` | HP 0 → IsDead, DeathEvent(Cause=Damage) 처리. 서버. |
| `HitDetectionSystem.cs` | 투사체-플레이어 충돌 판정 → HitEvent(StunPower=15) 발행. 서버. |
| `ShootSystem.cs` | 일반 무기 발사 — 탄약 확인·쿨다운·BulletPrefab 스폰. 서버 전용. |
| `TerrainDamageSystem.cs` | `PendingPixelConversionTag` 강체의 `IsDestroyed` 플래그 설정. `RigidBodyGraphSystem` 이후 실행. 서버. |
| `TerrainSyncSystem.cs` | Ghost로 전달된 `IsDestroyed=true`를 감지해 `PendingPixelConversionTag` 추가. 클라이언트+서버. |

### 아이템 (`Items/`)

| 파일 | 역할 |
|---|---|
| `ItemSpawnSystem.cs` | 게임 시작 시 SpawnPoint마다 무기/회복 아이템 배치. 1회 실행 후 비활성화. |
| `ItemPickupSystem.cs` | 아이템 획득 감지. |
| `WandPickupSystem.cs` | 완드 아이템 획득 → 4종 프리셋 적용(기본·폭발·용암·관통). 서버. |

### 게임 모드 (`BattleRoyale/`)

| 파일 | 역할 |
|---|---|
| `ArenaMatchSystem.cs` | 타이머·용암 상승·1v1/2v2 승패 판정·시간 초과 결산·RPC 브로드캐스트. 서버. |
| `TeamRespawnSystem.cs` | 팀원 생존 조건 충족 시 사망 플레이어 부활 (RespawnCount 소비). 서버. |
| `SurvivalCheckSystem.cs` | 배틀로얄 생존자 1인 판정 (ArenaMatchSystem과 별도 동작). 서버. |
| `BurialCheckSystem.cs` | 3×5 픽셀 샘플 80% Solid → 즉사, DeathEvent(Cause=Buried). 기절 무관. 서버. |

### 네트워크 동기화 (`Simulation/`)

| 파일 | 역할 |
|---|---|
| `PixelGridSyncSystem.cs` | 5틱마다 활성 청크 스냅샷 diff 계산 → `PixelChunkDiff` 버퍼 갱신. 서버. |
| `PixelGridRpcSendSystem.cs` | `PixelChunkDiff` → `PixelGridDeltaRpc` 전체 클라이언트 브로드캐스트. 서버. |
| `PixelGridRpcApplySystem.cs` | `PixelGridDeltaRpc` 수신 → 로컬 PixelGrid Type 채널 패치. 클라이언트. |
| `PixelGridInitialSyncSystem.cs` | 새 클라이언트 InGame 전환 시 전체 맵 스냅샷을 1회 전송. SLEEPING 청크도 포함. 서버. |
| `TerrainPaintRpcSystem.cs` | 클라이언트 페인트 요청 RPC 수신 → 서버 그리드에 직접 적용. 서버. |

### 연결·부트스트랩 (`Connection/`, `Bootstrap/`)

| 파일 | 역할 |
|---|---|
| `ServerConnectionSystem.cs` | 서버 연결 수락 처리. |
| `ClientConnectionSystem.cs` | 클라이언트 연결 요청. |
| `EnsureLocalPlayerSystem.cs` | 로컬 플레이어 엔티티 초기화 보장. |
| `GoInGameSystem.cs` | `MapReadyTag` 생성 후 새 연결에 `NetworkStreamInGame` 추가 및 플레이어 스폰. 클라이언트+서버. |
| `MapLoadSystem.cs` | MapTemplate → PixelGrid 1회 적용 + WandItem 스폰 + `MapReadyTag` 생성. 서버. |
| `RoomWatchSystem.cs` | 매 프레임 InGame 접속자 수 감시. 1→2 전환 시 `RoomResetRequestTag` 생성 + Phase=Countdown. 2→1 전환(Countdown 중)이면 Phase=Solo 복귀. 서버. |
| `RoomResetSystem.cs` | `RoomResetRequestTag` 존재 시 맵·플레이어·매치·결과 싱글턴을 전부 초기화하고 `RoomResetRpc`를 전체 브로드캐스트. 서버. |
| `MatchCountdownSystem.cs` | RoomPhase=Countdown 구간에서 3→2→1→GO RPC를 매초 브로드캐스트. 0 도달 시 Phase=Battle 전환. 서버. |

### 프레젠테이션 (`Presentation/`)

| 파일 | 역할 |
|---|---|
| `PlayerVisualSystem.cs` | ECS `PlayerMovementData` → SPUM Animator 상태 전달. 클라이언트. |
| `AnimationSystem.cs` | 애니메이션 프레임 갱신. |
| `CameraFollowSystem.cs` | 카메라 추적. |
| `SpriteFlipSystem.cs` | 스프라이트 방향 전환. |
| `ArenaHudSystem.cs` | ArenaMatchTickRpc / ArenaMatchEndRpc 수신 → ArenaHudBehaviour 갱신. 클라이언트. |
| `SpumAppearanceRandomizer.cs` | SPUM_Prefabs 인스턴스에 랜덤 외형을 적용하는 정적 유틸. Body·Eye 필수 파츠 + Hair/Cloth 등 확률 파츠를 Resources에서 로드해 적용. PlayerVisualSystem에서 첫 프레임에 호출. |

---

## 3. 컴포넌트 (`Assets/Scripts/Components/`)

### Ghost 컴포넌트 (NetCode 동기화)

| 파일 | 주요 필드 |
|---|---|
| `PlayerGhostData.cs` | Position, Velocity, Health, Armor, IsDead, IsFacingRight, WeaponId, AmmoCount, IsGrounded, KillCount, TeamId, RespawnCount, **IsStunned, HasShovel, ShovelUseCount** |
| `BulletGhostData.cs` | Position, Direction, OwnerId |
| `ItemGhostData.cs` | Position, ItemType(0=무기, 1=회복, 2=완드), ItemId, IsPickedUp |
| `RigidBodyGhostData.cs` | 강체 위치·파괴 상태 동기화 (`IsDestroyed` 필드 포함) |

### 플레이어 컴포넌트 (`Components/PlayerComponents.cs`)

| 타입 | 역할 |
|---|---|
| `PlayerTag` | 플레이어 엔티티 식별 태그 (GhostComponent) |
| `MovementConfig` | 이동 속도·점프·대시 설정값 (Baking 시점 설정) |
| `PlayerMovementState` | enum — Idle / Moving / Jumping / Dashing |
| `PlayerMovementData` | 현재 이동 상태(`Value`) + `IsGrounded` 플래그 (GhostComponent) |
| `PlayerVisualReference` | SPUM 프리팹 참조 보관 Managed Component |

### 완드 컴포넌트 (`Wand/WandComponents.cs`)

| 타입 | 설명 |
|---|---|
| `WandComponent` | 활성 슬롯, 쿨다운, 발사 간격, 자동 발사 여부 |
| `SpellData` (Buffer) | ProjectileType(Bullet/Explosion/LiquidSpray), LiquidPayload(None/Lava/Poison), SpellModifier(None/MultiShot/Penetrate) |
| `ManaComponent` | Current, Max, RegenRate |
| `ProjectileComponent` | 서버 로컬 — BlastRadius, PenetrateCount |

### RPC

| 파일 | 역할 |
|---|---|
| `PixelGridDeltaRpc.cs` | ChunkX, ChunkY, FixedBytes4096 CellData |
| `ArenaMatchRpc.cs` | `ArenaMatchTickRpc`(주기 동기화), `ArenaMatchEndRpc`(결과) |
| `RoomResetRpc.cs` | 서버→전체 클라이언트: 방 리셋 완료 알림. IRpcCommand 마커. |
| `MatchCountdownRpc.cs` | 서버→전체 클라이언트: 카운트다운 숫자(`Count` 3·2·1·0=GO). |
| `TerrainPaintRpc.cs` | 클라이언트→서버: 마우스 페인트 요청 (WorldPos, WorldRadius, CellType, Flammability). |

### 로컬 싱글턴 & 컴포넌트

| 파일 | 역할 |
|---|---|
| `ArenaMatchSingleton.cs` | 타이머, 팀 점수, 용암 높이, 게임 종료 상태 |
| `GameResultSingleton.cs` | 배틀로얄 결과 (WinnerNetworkId, IsDraw) |
| `GamePrefabs.cs` | BulletPrefab, WeaponItemPrefab, HealItemPrefab, WandItemPrefab, PlayerPrefab, **ShovelItemPrefab** |
| `PixelChunkDiff.cs` | 청크 diff 버퍼 요소 (ChunkCoord, CellTypes NativeArray) |
| `PlayerLocalState.cs` | 클라이언트 로컬 타이머 상태 — CoyoteTimer, JumpBufferTimer, DashTimer 등 (GhostField 없음, 롤백 시 서버가 덮어쓰지 않음) |
| `MapReadyTag.cs` | 서버 맵 로드 완료 시 1회 생성되는 태그 싱글턴 — GoInGameSystem이 이 태그를 기다림 |
| `RoomStateSingleton.cs` | 방 페이즈 상태 — `RoomPhase` enum(Solo/Countdown/Battle), CountdownTimer, LastPlayerCount |
| `RoomResetRequestTag.cs` | 서버 전용 트리거 태그 — 이 컴포넌트가 존재하면 RoomResetSystem이 즉시 리셋 실행 |
| `StunStateComponent.cs` | 서버 로컬 — 스턴 게이지(`StunAccumulator`), 기절 타이머(`StunTimer`), 근접 쿨다운(`MeleeAttackCooldown`) |
| `ShovelComponent.cs` | 서버 로컬 — 삽 사용 횟수(`UseCount`). 현재는 PlayerGhostData 필드로 관리 |

---

## 4. 아레나 맵 (`Assets/Scripts/Arena/`)

| 파일 | 역할 |
|---|---|
| `MapTemplate.cs` | ScriptableObject — CellFillRule[], SpawnPoints[], WandSpawnPoints[], IndestructibleRadius |
| `MapTemplateReference.cs` | managed IComponentData — MapTemplate ScriptableObject 참조 보관 |
| `ArenaMapBuilder.cs` | FillRect/FillCircle 그리드 쓰기, 1v1/2v2 프리셋 생성 정적 메서드 |

---

## 5. Authoring (Baker)

| 파일 | 역할 |
|---|---|
| `PlayerAuthoring.cs` | WandComponent, ManaComponent, SpellData 기본값 베이킹 |
| `GamePrefabsAuthoring.cs` | BulletPrefab, WandItemPrefab 등 프리팹 참조 베이킹 |
| `ArenaMatchAuthoring.cs` | ArenaMatchSingleton 초기값(MatchDuration, LavaRiseSpeed) 베이킹 |
| `MapTemplateAuthoring.cs` | MapTemplateReference 베이킹 |
| `GameResultAuthoring.cs` | GameResultSingleton 초기화 |
| `SpawnPointAuthoring.cs` | SpawnPointData 베이킹 |

---

## 6. HUD 및 프레젠테이션

| 파일 | 역할 |
|---|---|
| `ArenaHudBehaviour.cs` | IMGUI — HP/마나 바, 타이머, 팀 점수, 용암 경고, 결과 화면. 싱글톤 MonoBehaviour. |
| `PlayerCamera.cs` | 시네머신/카메라 제어. |
| `PixelLayerRenderer.cs` | PixelGrid → Texture2D → SpriteRenderer 렌더링. |
| `ParallaxBackground.cs` | 하늘(×0.15)·언덕(×0.40) 두 배경 레이어를 런타임 자동 생성; 카메라 델타 × factor로 지형보다 느리게 스크롤. |

---

## 6.1 런타임 유틸 (`Assets/Scripts/Runtime/`)

| 파일 | 역할 |
|---|---|
| `RuntimePlayerSpawner.cs` | 로컬 플레이어 ECS 엔티티 생성. |
| `EntityTransformFollower.cs` | ECS `LocalTransform` → GameObject `Transform` 동기화 (MonoBehaviour). |
| `SandBlastAutoSetup.cs` | `AfterSceneLoad` 시 씬에 필수 오브젝트가 없으면 자동 생성 (Bootstrap·PixelLayer·카메라·**ParallaxBackground**). |

---

## 7. 핵심 유틸 (`Core/`, `Chunk/`, `RigidBody/`)

| 파일 | 역할 |
|---|---|
| `CellType.cs` | byte enum + IsSolid/IsLiquid/IsFuel/IsIndestructible/DefaultFlammability 확장 메서드 |
| `SimulationConstants.cs` | 엔진·전투 전체 튜닝 상수 중앙 관리 |
| `GridUtils.cs` | 래스터화, 좌표 변환 수학 유틸 |
| `ChunkMeta.cs` | 청크 슬리핑 상태 struct (SLEEPING/ACTIVE/WAKING) |
| `Edge.cs` | 강체 폴리곤 엣지 blittable struct |

---

## 8. 에디터 도구 (`Editor/`) 및 디버그 (`Debug/`)

| 파일 | 역할 |
|---|---|
| `ArenaMapEditorWindow.cs` | `SandBlast > Arena Map Editor` — 1v1/2v2 프리셋 에셋 생성 |
| `SandBlastSetupWindow.cs` | 씬 초기 설정 자동화 |
| `SandBlastDebugPainter.cs` | 런타임 마우스 브러시로 셀 그리기 (`Debug/`) |
| `PlayerDebugRenderer.cs` (`PlayerDebugBoxSystem`) | 서버(주황)·클라이언트(파랑) 구분하여 플레이어 엔티티 위치를 `Debug.DrawLine` 사각형으로 시각화. `PresentationSystemGroup`. |

---

## 9. 레거시 (삭제 대상)

| 파일 | 상태 |
|---|---|
| `Player/PlayerController.cs` | `[Obsolete]` — ECS PlayerMoveSystem + PlayerVisualSystem으로 대체 완료 |
| `Core/PixelSimulator.cs` | Managed CA 루프 — ECS Jobs로 이전 완료 |
| `Core/ChunkManager.cs` | Managed 청크 관리 — ChunkManagerSingleton으로 이전 완료 |
| `Core/PixelGrid.cs` | Managed 그리드 클래스 — PixelGridSingleton(NativeArray)으로 이전 완료 |
| `Core/ForceAccumulator.cs` | 레거시 강체 힘 집산 시스템 — ECS 강체 컴포넌트로 이전 완료 |

---

> **관련 문서:** `CodeOverview.md` (아키텍처 개요), `ImprovementTasks.md` (개선 과제)  
> **설계 기준서:** `Plan/sandblast_engine_design.md`, `Plan/ECS_Migration_Plan.md`  
> **레거시 계획서:** `Plan/Legacy/` 폴더 참조
