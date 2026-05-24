# PlayerAuthoring.cs

## 역할
Player.prefab의 MonoBehaviour → ECS 변환을 담당하는 Baker. Inspector에서 설정한 초기값을 엔티티 컴포넌트로 변환한다.

## Inspector 설정값

| 필드 | 기본값 | 설명 |
|------|--------|------|
| `StartHealth` | `100f` | 초기 체력 |
| `StartArmor` | `0f` | 초기 방어력 |
| `StartWeaponId` | `0` | 초기 무기 |
| `StartAmmoCount` | `30` | 초기 탄약 수 |

## Baker가 추가하는 컴포넌트

| 컴포넌트 | 역할 |
|----------|------|
| `PlayerGhostData` | 네트워크 동기화 상태 |
| `PlayerInput` | 입력 버퍼링 (IInputComponentData) |
| `GroundedState` | 지면 상태 |
| `WeaponState` | 발사 쿨다운 |
| `HitEvent` 버퍼 | 피격 이벤트 수신 |

## Prefab 설정 체크리스트
- [ ] `PlayerAuthoring` 컴포넌트 추가
- [ ] `GhostAuthoringComponent` 추가 → Ghost Mode: `OwnerPredicted`, Importance: `100`
- [ ] `PhysicsCollider` 추가 (GroundCheckSystem의 레이캐스트용)
- [ ] `SpriteRenderer` 추가 (SpriteFlipSystem용)
- [ ] `Animator` 추가 (AnimationSystem용)

## 관련 파일
- [`PlayerGhostData`](../Components/Ghost/PlayerGhostData.md)
- [`PlayerInput`](../Components/Local/PlayerInput.md)
- [`GroundedState`](../Components/Local/GroundedState.md)
- [`WeaponState`](../Components/Local/WeaponState.md)
