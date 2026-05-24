# 2D 횡스크롤 배틀로얄 — Netcode for Entities 시스템 설계

> **장르:** 2D 횡스크롤 PvP 배틀로얄  
> **네트워크:** Dedicated Server (서버 권위적)  
> **플레이어:** 8인+  
> **투사체:** 물리 기반 투사체 (Projectile)  
> **엔진:** Unity DOTS + Netcode for Entities

---

## 목차

1. [프로젝트 구조](#1-프로젝트-구조)
2. [Ghost 컴포넌트 목록](#2-ghost-컴포넌트-목록)
3. [일반 컴포넌트 목록](#3-일반-컴포넌트-목록)
4. [시스템 목록 및 책임](#4-시스템-목록-및-책임)
5. [시스템 실행 순서](#5-시스템-실행-순서)
6. [World 필터 정리](#6-world-필터-정리)
7. [Ghost Prefab 목록](#7-ghost-prefab-목록)
8. [구현 우선순위](#8-구현-우선순위)

---

## 1. 프로젝트 구조

```
Assets/
├── Scenes/
│   ├── MainMenu.unity
│   ├── GameScene.unity
│   └── GameSubScene/               ← ECS SubScene
│       ├── GhostCollection
│       ├── MapGeometry
│       └── ItemSpawnPoints
│
├── Scripts/
│   ├── Bootstrap/
│   │   └── GameBootstrap.cs
│   │
│   ├── Components/
│   │   ├── Ghost/                  ← 네트워크 동기화 컴포넌트
│   │   │   ├── PlayerGhostData.cs
│   │   │   ├── BulletGhostData.cs
│   │   │   └── ItemGhostData.cs
│   │   │
│   │   └── Local/                  ← 로컬 전용 컴포넌트
│   │       ├── GroundedState.cs
│   │       ├── WeaponState.cs
│   │       ├── BulletLifetime.cs
│   │       └── HitEvent.cs
│   │
│   ├── Systems/
│   │   ├── Input/
│   │   │   └── GatherInputSystem.cs
│   │   │
│   │   ├── Movement/
│   │   │   ├── PlayerMoveSystem.cs
│   │   │   ├── GravitySystem.cs
│   │   │   └── GroundCheckSystem.cs
│   │   │
│   │   ├── Combat/
│   │   │   ├── ShootSystem.cs
│   │   │   ├── BulletMoveSystem.cs
│   │   │   ├── HitDetectionSystem.cs
│   │   │   └── DamageSystem.cs
│   │   │
│   │   ├── BattleRoyale/
│   │   │   └── SurvivalCheckSystem.cs
│   │   │
│   │   ├── Items/
│   │   │   ├── ItemSpawnSystem.cs
│   │   │   └── ItemPickupSystem.cs
│   │   │
│   │   ├── Connection/
│   │   │   ├── ServerConnectionSystem.cs
│   │   │   └── ClientConnectionSystem.cs
│   │   │
│   │   └── Presentation/           ← 클라이언트 전용 (비-게임로직)
│   │       ├── SpriteFlipSystem.cs
│   │       ├── AnimationSystem.cs
│   │       └── CameraFollowSystem.cs
│   │
│   └── Authoring/                  ← Baker (MonoBehaviour → ECS 변환)
│       ├── PlayerAuthoring.cs
│       ├── BulletAuthoring.cs
│       └── ItemAuthoring.cs
│
└── Prefabs/
    ├── Player.prefab               ← GhostAuthoringComponent 포함
    ├── Bullet.prefab               ← GhostAuthoringComponent 포함
    └── Items/
        ├── WeaponItem.prefab
        └── HealItem.prefab
```

---

## 2. Ghost 컴포넌트 목록

> `[GhostField]` 어트리뷰트가 붙은 필드만 네트워크로 동기화됩니다.

### PlayerGhostData.cs
```csharp
// Ghost 모드: OwnerPredicted
// Importance: 100 (높은 동기화 우선순위)
public struct PlayerGhostData : IComponentData
{
    [GhostField] public float2 Position;
    [GhostField] public float2 Velocity;
    [GhostField(Quantization = 10)] public float Health;       // 소수점 1자리
    [GhostField(Quantization = 10)] public float Armor;
    [GhostField] public bool IsDead;
    [GhostField] public bool IsFacingRight;
    [GhostField] public int WeaponId;                          // 현재 장착 무기
    [GhostField] public int AmmoCount;
    [GhostField] public bool IsGrounded;
    [GhostField] public int KillCount;
}
```

### BulletGhostData.cs
```csharp
// Ghost 모드: Interpolated (서버 권위)
// Importance: 50
public struct BulletGhostData : IComponentData
{
    [GhostField] public float2 Position;
    [GhostField] public float2 Velocity;
    [GhostField] public int OwnerNetworkId;
    [GhostField(Quantization = 10)] public float Damage;
    [GhostField] public int BulletType;                        // 관통, 폭발 등
}
```

### ItemGhostData.cs
```csharp
// Ghost 모드: Interpolated
// Importance: 30
public struct ItemGhostData : IComponentData
{
    [GhostField] public float2 Position;
    [GhostField] public int ItemType;                          // 무기, 회복 등
    [GhostField] public int ItemId;                            // 세부 아이템 ID
    [GhostField] public bool IsPickedUp;
}
```

---

## 3. 일반 컴포넌트 목록

> 네트워크 동기화 없이 로컬에서만 사용하는 컴포넌트입니다.

| 컴포넌트 | 용도 |
|----------|------|
| `PlayerInput` (IInputComponentData) | 입력 버퍼링 및 서버 전송 |
| `GroundedState` | 현재 지면 접촉 여부 |
| `WeaponState` | 재장전 타이머, 연사 쿨다운 |
| `BulletLifetime` | 투사체 수명 (서버 전용) |
| `HitEvent` (IBufferElementData) | 충돌 이벤트 큐 |
| `DeathEvent` | 사망 처리 이벤트 |
| `SpawnRequest` | 리스폰 요청 (배틀로얄은 없음) |

### PlayerInput.cs
```csharp
// IInputComponentData → NfE가 자동으로 버퍼링하여 서버에 전송
public struct PlayerInput : IInputComponentData
{
    public float2 MoveDirection;     // 좌우: -1 ~ 1
    public InputEvent Jump;
    public InputEvent Shoot;
    public float AimAngle;           // 마우스 방향 (라디안)
    public InputEvent Reload;
    public InputEvent Interact;      // 아이템 줍기
}
```

---

## 4. 시스템 목록 및 책임

---

### 📁 Connection/

#### ServerConnectionSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ServerSimulation` |
| UpdateGroup | `SimulationSystemGroup` |
| 읽는 컴포넌트 | — |
| 쓰는 컴포넌트 | `NetworkStreamDriver` |
| **책임** | 서버 포트 리슨, 클라이언트 연결 수락 |

#### ClientConnectionSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ClientSimulation` |
| UpdateGroup | `SimulationSystemGroup` |
| 읽는 컴포넌트 | — |
| 쓰는 컴포넌트 | `NetworkStreamDriver` |
| **책임** | 서버 IP:Port로 연결 요청 |

---

### 📁 Input/

#### GatherInputSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ClientSimulation` |
| UpdateGroup | `GhostInputSystemGroup` |
| 읽는 컴포넌트 | `GhostOwnerIsLocal` (필터) |
| 쓰는 컴포넌트 | `PlayerInput` |
| **책임** | 키보드/마우스 입력을 PlayerInput에 기록 |

```
Input.GetAxisRaw("Horizontal") → MoveDirection.x
Input.GetKeyDown(Space)        → Jump.Set()
Camera.ScreenToWorldPoint()    → AimAngle
Input.GetMouseButton(0)        → Shoot.Set()
Input.GetKeyDown(R)            → Reload.Set()
Input.GetKeyDown(F)            → Interact.Set()
```

---

### 📁 Movement/

#### GravitySystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ClientAndServer` |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 읽는 컴포넌트 | `GroundedState` |
| 쓰는 컴포넌트 | `PlayerGhostData.Velocity.y` |
| **책임** | 중력 가속도 적용, 최대 낙하속도 제한 |

```
Velocity.y -= GRAVITY * dt          (중력: 30f)
Velocity.y = max(Velocity.y, -20f)  (최대 낙하속도)
```

#### GroundCheckSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ClientAndServer` |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 읽는 컴포넌트 | `LocalTransform`, `PhysicsCollider` |
| 쓰는 컴포넌트 | `GroundedState`, `PlayerGhostData.IsGrounded` |
| **책임** | 하단 레이캐스트로 지면 접촉 여부 판정 |

#### PlayerMoveSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ClientAndServer` |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 읽는 컴포넌트 | `PlayerInput`, `GroundedState` |
| 쓰는 컴포넌트 | `PlayerGhostData`, `LocalTransform` |
| **책임** | 좌우 이동, 점프, 속도 기반 위치 업데이트 |

```
// 실행 조건: .WithAll<Simulate>() 필수
Position += Velocity * dt
IsFacingRight = MoveDirection.x > 0 (입력 있을 때)
Jump: IsGrounded일 때만 Velocity.y = JUMP_FORCE (15f)
```

---

### 📁 Combat/

#### ShootSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ServerSimulation` |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 읽는 컴포넌트 | `PlayerInput`, `PlayerGhostData`, `WeaponState` |
| 쓰는 컴포넌트 | `BulletGhostData` (생성), `WeaponState` |
| **책임** | 발사 입력 시 투사체 엔티티 생성, 쿨다운 관리 |

```
조건: Shoot.IsSet && WeaponState.Cooldown <= 0 && AmmoCount > 0
투사체 초기 속도: AimAngle 방향 * WeaponSpeed
생성 위치: Player.Position + 총구 오프셋
쿨다운 리셋: WeaponState.Cooldown = WeaponData[WeaponId].FireRate
```

#### BulletMoveSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ServerSimulation` |
| UpdateGroup | `PredictedSimulationSystemGroup` |
| 읽는 컴포넌트 | `BulletGhostData`, `BulletLifetime` |
| 쓰는 컴포넌트 | `BulletGhostData.Position`, `BulletLifetime` |
| **책임** | 투사체 이동, 수명 감소, 수명 만료 시 삭제 |

```
Position += Velocity * dt
Lifetime.Value -= dt
Lifetime <= 0 → ECB.DestroyEntity()
```

#### HitDetectionSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ServerSimulation` |
| UpdateGroup | `SimulationSystemGroup` (물리 후) |
| 읽는 컴포넌트 | `BulletGhostData`, `PlayerGhostData` |
| 쓰는 컴포넌트 | `HitEvent` (버퍼에 추가), 투사체 삭제 |
| **책임** | 투사체 ↔ 플레이어 충돌 감지, HitEvent 발행 |

```
AABB 또는 Physics.Overlap 방식
자기 자신(OwnerNetworkId) 충돌 제외
충돌 시: HitEvent { TargetEntity, Damage, HitPosition } 버퍼에 추가
충돌한 투사체: ECB.DestroyEntity()
```

#### DamageSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ServerSimulation` |
| UpdateGroup | `SimulationSystemGroup` |
| 읽는 컴포넌트 | `HitEvent` 버퍼 |
| 쓰는 컴포넌트 | `PlayerGhostData.Health`, `PlayerGhostData.IsDead` |
| **책임** | HitEvent를 소비해 HP 감소, 사망 판정 |

```
Health -= HitEvent.Damage
Health <= 0 → IsDead = true, DeathEvent 발행
HitEvent 버퍼 클리어
```

---

### 📁 BattleRoyale/

#### SurvivalCheckSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ServerSimulation` |
| UpdateGroup | `SimulationSystemGroup` |
| 읽는 컴포넌트 | `PlayerGhostData.IsDead` |
| 쓰는 컴포넌트 | `GameResultSingleton` |
| **책임** | 생존자 수 카운트, 최후 1인 시 게임 종료 |

```
aliveCount = 살아있는 플레이어 수
aliveCount == 1 → 승자 결정, 게임 종료 RPC 전송
aliveCount == 0 → 무승부 처리
```

---

### 📁 Items/

#### ItemSpawnSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ServerSimulation` |
| UpdateGroup | `SimulationSystemGroup` |
| 읽는 컴포넌트 | `SpawnPointData` |
| 쓰는 컴포넌트 | `ItemGhostData` (생성) |
| **책임** | 게임 시작 시 맵에 아이템 배치, 런타임 보충 |

#### ItemPickupSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ServerSimulation` |
| UpdateGroup | `SimulationSystemGroup` |
| 읽는 컴포넌트 | `PlayerInput.Interact`, `PlayerGhostData.Position`, `ItemGhostData` |
| 쓰는 컴포넌트 | `PlayerGhostData.WeaponId/AmmoCount`, `ItemGhostData.IsPickedUp` |
| **책임** | 플레이어 근처 아이템 줍기 처리 |

```
거리 체크: length(PlayerPos - ItemPos) < PICKUP_RANGE (1.5f)
Interact.IsSet → 아이템 타입에 따라 처리
  무기: WeaponId 변경, AmmoCount 설정
  회복: Health += HealAmount (최대 100 초과 불가)
ItemGhostData.IsPickedUp = true → 클라이언트에서 비활성화
```

---

### 📁 Presentation/ (클라이언트 전용)

#### SpriteFlipSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ClientSimulation` |
| UpdateGroup | `PresentationSystemGroup` |
| 읽는 컴포넌트 | `PlayerGhostData.IsFacingRight` |
| 쓰는 컴포넌트 | `SpriteRenderer` (Hybrid) |
| **책임** | 이동 방향에 따라 스프라이트 좌우 반전 |

#### AnimationSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ClientSimulation` |
| UpdateGroup | `PresentationSystemGroup` |
| 읽는 컴포넌트 | `PlayerGhostData.Velocity`, `GroundedState` |
| 쓰는 컴포넌트 | `AnimatorComponent` (Hybrid) |
| **책임** | 속도/상태 기반 애니메이션 파라미터 설정 |

```
IsWalking  = abs(Velocity.x) > 0.1f
IsJumping  = !IsGrounded && Velocity.y > 0
IsFalling  = !IsGrounded && Velocity.y < 0
IsShooting = ShootInput (이벤트 기반)
```

#### CameraFollowSystem.cs
| 항목 | 내용 |
|------|------|
| WorldFilter | `ClientSimulation` |
| UpdateGroup | `PresentationSystemGroup` |
| 읽는 컴포넌트 | `LocalTransform`, `GhostOwnerIsLocal` |
| 쓰는 컴포넌트 | `Camera.main.transform` |
| **책임** | 로컬 플레이어를 부드럽게 추적 |

---

## 5. 시스템 실행 순서

```
─── 매 틱 시작 ───────────────────────────────────────────

[GhostInputSystemGroup]
  └── GatherInputSystem              (Client)
        입력 수집 → PlayerInput 기록

[PredictedSimulationSystemGroup]     (Client + Server, 예측 롤백 포함)
  ├── GroundCheckSystem
  ├── GravitySystem
  ├── PlayerMoveSystem
  └── ShootSystem                    (Server only — 투사체 생성)

[SimulationSystemGroup]              (Server only)
  ├── BulletMoveSystem
  ├── HitDetectionSystem
  ├── DamageSystem
  ├── ItemPickupSystem
  └── SurvivalCheckSystem

[GhostSendSystem]                    (Server → Client 스냅샷 전송)
[GhostReceiveSystem]                 (Client 수신)
  └── GhostUpdateSystem              (보간 적용)

[PresentationSystemGroup]            (Client only)
  ├── SpriteFlipSystem
  ├── AnimationSystem
  └── CameraFollowSystem

─── 매 틱 종료 ───────────────────────────────────────────
```

---

## 6. World 필터 정리

| 시스템 | ClientSimulation | ServerSimulation | 이유 |
|--------|:---:|:---:|------|
| GatherInputSystem | ✅ | ❌ | 키보드/마우스는 클라이언트에만 존재 |
| PlayerMoveSystem | ✅ | ✅ | 클라이언트 예측 + 서버 권위 모두 필요 |
| GravitySystem | ✅ | ✅ | 예측 대상 |
| GroundCheckSystem | ✅ | ✅ | 예측 대상 |
| ShootSystem | ❌ | ✅ | 투사체 생성은 서버 권위 |
| BulletMoveSystem | ❌ | ✅ | 서버 권위 |
| HitDetectionSystem | ❌ | ✅ | 서버 권위 |
| DamageSystem | ❌ | ✅ | 서버 권위 |
| ItemSpawnSystem | ❌ | ✅ | 서버 권위 |
| ItemPickupSystem | ❌ | ✅ | 서버 권위 |
| SpriteFlipSystem | ✅ | ❌ | 렌더링은 클라이언트만 |
| AnimationSystem | ✅ | ❌ | 렌더링은 클라이언트만 |
| CameraFollowSystem | ✅ | ❌ | 렌더링은 클라이언트만 |

---

## 7. Ghost Prefab 목록

| Prefab | Ghost Mode | Importance | 비고 |
|--------|-----------|-----------|------|
| `Player.prefab` | OwnerPredicted | 100 | 로컬 플레이어는 예측, 타인은 보간 |
| `Bullet.prefab` | Interpolated | 50 | 서버 권위, 클라이언트 보간만 |
| `WeaponItem.prefab` | Interpolated | 30 | 맵 위 무기 아이템 |
| `HealItem.prefab` | Interpolated | 30 | 맵 위 회복 아이템 |

---

## 8. 구현 우선순위

### Phase 1 — 기반 (로컬 테스트 가능)
- [ ] 프로젝트 셋업 + Bootstrap
- [ ] PlayerGhostData, PlayerInput 컴포넌트
- [ ] GatherInputSystem
- [ ] GravitySystem + GroundCheckSystem
- [ ] PlayerMoveSystem
- [ ] SpriteFlipSystem

### Phase 2 — 전투
- [ ] BulletGhostData 컴포넌트
- [ ] ShootSystem
- [ ] BulletMoveSystem
- [ ] HitDetectionSystem
- [ ] DamageSystem

### Phase 3 — 배틀로얄 규칙
- [ ] SurvivalCheckSystem

### Phase 4 — 아이템
- [ ] ItemGhostData 컴포넌트
- [ ] ItemSpawnSystem
- [ ] ItemPickupSystem

### Phase 5 — 폴리시
- [ ] AnimationSystem
- [ ] CameraFollowSystem
- [ ] UI (체력바, 생존자 수)
- [ ] 매치메이킹 / 로비

---

*마지막 수정: 2025*
