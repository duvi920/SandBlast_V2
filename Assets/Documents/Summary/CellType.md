# CellType (셀 타입)

> `Assets/Scripts/Core/CellType.cs`

## 역할

픽셀 그리드의 셀 타입을 정의하는 `byte` 열거체와 분류 확장 메서드입니다.  
`PixelGridSingleton`의 `Type` NativeArray에 직접 저장되며, ECS Burst Job에서 고속으로 분류 및 처리됩니다.

## 타입 목록

| 값 | 이름 | 분류 | 설명 |
|---|---|---|---|
| 0 | `EMPTY` | — | 빈 공간 |
| 1 | `SOLID_STATIC` | 고체 | 지형 셀 또는 정착해 굳은 잔해. 시뮬레이션 순회에서 제외 |
| 2 | `SOLID_RIGID` | 고체 | 이동 강체가 점유 중인 영역. 렌더링 시 투명 처리 |
| 3 | `SOLID_DEBRIS` | 고체 | 강체→픽셀 전환 잔해. 중력 적용, 슬리핑 후 SOLID_STATIC 전환 |
| 4 | `LIQUID_WATER` | 유체 | 물. 수평 확산(dispersion=5), 압력 전파 |
| 5 | `LIQUID_LAVA` | 유체 | 용암. 점성(dispersion=1), 냉각 시 SOLID_STATIC 고체화 |
| 6 | `POWDER_SAND` | 분말 | 모래. 안식각(대각 흐름), 정착 후 SOLID_STATIC 전환 |
| 7 | `POWDER_ASH` | 분말 | 재. 모래와 동일하나 20% 확률로 위로 떠오름 |
| 8 | `FIRE` | 에너지 | 불. 인접 연료로 전파, 수명 소진 시 ASH 또는 EMPTY |
| 9 | `GAS_SMOKE` | 에너지 | 연기. 위로 상승, 수명 소진 시 EMPTY |

## 상태 전환 규칙 (ECS 시스템 담당)

| 전환 | 조건 | 담당 시스템 |
|---|---|---|
| `SOLID_STATIC` → `SOLID_DEBRIS` | 강체 그래프 노드 고립 (모든 엣지 BROKEN) | `RigidToPixelConvertSystem` |
| `SOLID_DEBRIS` → `SOLID_STATIC` | 슬리핑: SLEEP_THRESHOLD 틱 동안 이동 없음 | `PixelDebrisSystem` |
| `POWDER_*` → `SOLID_STATIC` | 슬리핑: SLEEP_THRESHOLD 틱 동안 이동 없음 | `PixelPowderSystem` |
| `LIQUID_LAVA` → `SOLID_STATIC` | 온도가 LAVA_SOLIDIFY_THRESHOLD 이하로 냉각 | `PixelLiquidSystem` |
| `FIRE` → `POWDER_ASH` / `EMPTY` | 수명(lifetime) 소진 | `PixelFireSystem` |
| `FIRE` → `POWDER_ASH` | 인접 LIQUID_WATER 에 의해 즉시 소화 | `PixelFireSystem` |

## 확장 메서드 (CellTypeExt)

```csharp
bool IsSolid()   // SOLID_STATIC, SOLID_RIGID, SOLID_DEBRIS
bool IsLiquid()  // LIQUID_WATER, LIQUID_LAVA
bool IsFuel()    // POWDER_SAND, POWDER_ASH, SOLID_DEBRIS, SOLID_STATIC
```

## 관련 문서

- [CodeOverview.md](../CodeOverview.md) — 전체 ECS 아키텍처 및 시스템 파이프라인
- [SimulationConstants.md](SimulationConstants.md) — 전환 임계값 상수
