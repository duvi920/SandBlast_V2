# ShovelUseSystem.cs

## 역할
삽(`HasShovel == true`)을 소지한 플레이어가 E키(`UseShovel`)를 누르면 자기 주변 반경 6픽셀에 `POWDER_SAND`를 채우는 시스템. 3회 사용 후 삽이 소멸된다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 순서 | `StunSystem` 이후, `BurialCheckSystem` 이전 |
| 타입 | `SystemBase` (PixelGridSingleton 접근 필요) |

## 사용 조건
1. `PlayerGhostData.HasShovel == true`
2. `PlayerInput.UseShovel.IsSet` (E키)
3. `IsDead == false`, `IsStunned == false`

## 처리 내용
```
float radius = ShovelRadiusPx(6f) / grid.PixelsPerUnit

PixelImpactUtility.StampCircle(
    center   : 공격자 자신의 Position,
    radius   : 반경,
    cellType : POWDER_SAND
)

ShovelUseCount--
if ShovelUseCount <= 0:
    HasShovel = false
    ShovelUseCount = 0
```

## 매몰 메커니즘
삽으로 자기 위치 주변에 모래를 뿌려 근처의 상대를 덮는다. 상대가 움직이지 못하는 상태(기절 등)이면 효과적이지만, 기절이 필수 조건은 아니다. `BurialCheckSystem`이 매 프레임 모든 플레이어의 매몰 여부를 판정한다.

## 관련 파일
- [`PlayerInput`](../../Components/Local/PlayerInput.md) — UseShovel 입력
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — HasShovel, ShovelUseCount
- [`PixelImpactUtility`](../../ECS/PixelImpactUtility.md) — StampCircle
- [`BurialCheckSystem`](../BattleRoyale/BurialCheckSystem.md) — 매몰 판정
