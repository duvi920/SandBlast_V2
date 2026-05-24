# PlayerController

> `Assets/Scripts/Player/PlayerController.cs`

## 역할

픽셀 그리드 위에서 동작하는 플레이어 캐릭터 컨트롤러.  
`Rigidbody2D(Kinematic)` + `PixelGrid` 직접 샘플링으로 충돌을 처리해  
Unity Physics 콜라이더가 없는 픽셀 셀(SOLID_STATIC, SOLID_DEBRIS 등)에서도 착지한다.

## 씬 설정

**에디터 창에서 자동 생성:**  
`SandBlast > Setup Scene` → **Add Player** 버튼

**수동 설정:**
1. 플레이어 GameObject에 `PlayerController` 부착 (Rigidbody2D 자동 추가됨)
2. 자식에 `SpriteRenderer` 추가 → 이동 방향에 따라 자동 flipX
3. `SandBlastEngine` 이 씬에 있으면 자동 참조됨

## 조작법

| 키 | 동작 |
|---|---|
| ←→ / AD | 이동 |
| Space / W / ↑ | 점프 |
| Shift / C | 대시 |

## 인스펙터 필드

### 이동
| 필드 | 기본값 | 설명 |
|---|---|---|
| MoveSpeed | 6 | 최대 이동 속도 (units/s) |
| Acceleration | 50 | 지면 가속도 (units/s²) |
| Deceleration | 40 | 지면 감속도 |
| AirAcceleration | 25 | 공중 가속도 |

### 점프
| 필드 | 기본값 | 설명 |
|---|---|---|
| JumpSpeed | 14 | 점프 초기 수직 속도 |
| Gravity | 36 | 기본 중력 |
| FallGravityMultiplier | 1.8 | 하강 시 중력 배율 (빠른 낙하감) |
| MaxFallSpeed | 40 | 최대 낙하 속도 |
| CoyoteTime | 0.12s | 낭떠러지 직후 점프 허용 시간 |
| JumpBufferTime | 0.10s | 착지 직전 점프 입력 유예 시간 |

### 대시
| 필드 | 기본값 | 설명 |
|---|---|---|
| DashSpeed | 20 | 대시 속도 |
| DashDuration | 0.14s | 대시 지속 시간 |
| DashCooldown | 0.55s | 재사용 대기 시간 |

### 충돌 박스
| 필드 | 기본값 | 설명 |
|---|---|---|
| ColliderSize | (0.75, 1.5) | AABB 크기 (worldUnit). Unity Collider 가 아닌 그리드 샘플링용 |
| ColliderOffset | (0, 0) | rb.position 기준 중심 오프셋 |

## 충돌 감지 방식

Unity Physics 콜라이더 없이 **PixelGrid 직접 샘플링**으로 충돌을 처리한다.

```
CheckGrounded: 발 아래 5개 지점 샘플 → IsSolid() 여부
OverlapsSolid: AABB 꼭짓점 4 + 엣지 중점 4 = 8지점 샘플
ResolveCollision: X축 → Y축 순서로 분리 처리 (모서리 끼임 방지)
```

`IsSolid()` = `SOLID_STATIC | SOLID_RIGID | SOLID_DEBRIS`이므로  
모래·재(분말)는 슬리핑 전까지 통과 가능, 용암·물에는 빠짐.

## 처리 순서 (FixedUpdate)

```
1. 대시 시작 여부 판정
2. 대시 진행 중이면 → 이동 + return (중력/점프 무시)
3. 지면 감지 + 코요테 타이머 갱신
4. 점프 버퍼 처리 → 점프 실행
5. 수평 가속/감속
6. 중력 적용 (하강 시 배율 적용)
7. ResolveCollision(velocity * dt) → rb.MovePosition
```

## 입력 분리 구조

- `Update`: `wasPressedThisFrame` 수집 (FixedUpdate 타이밍에서 유실 방지)
- `FixedUpdate`: 실제 물리 처리 및 `wantsJump`, `wantsDash` 플래그 소비

## 관련 파일

- [SandBlastEngine.md](SandBlastEngine.md)
- [PixelGrid.md](PixelGrid.md)
- [CellType.md](CellType.md) — IsSolid() 정의
- [SandBlastSetupWindow.md](SandBlastSetupWindow.md) — Add Player 버튼
