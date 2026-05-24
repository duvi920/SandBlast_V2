# PixelGrid

> `Assets/Scripts/Core/PixelGrid.cs`

## 역할

CA 시뮬레이션의 전체 데이터를 보관하는 1차원 배열 집합.  
메인 그리드와 메타 버퍼를 분리해 캐시 효율을 높인다 (설계 문서 §5.2).

## 데이터 구조

```
인덱스 공식: idx = y * Width + x
```

| 배열 | 타입 | 접근 빈도 | 설명 |
|---|---|---|---|
| `Type[]` | byte | 매 틱 전체 순회 | CellType을 byte 로 저장 |
| `Temperature[]` | byte | FIRE·LAVA 처리 시 | 열 수치 (자동 발화, 용암 냉각에 사용) |
| `Lifetime[]` | byte | FIRE·SMOKE·DEBRIS·POWDER 처리 시 | 두 가지 의미로 재활용: ① FIRE/SMOKE 소멸 카운터 ② DEBRIS/POWDER 슬리핑 카운터 |
| `RigidId[]` | byte | 강체 마스크 동기화 시 | 어느 강체가 점유 중인지 기록. 강체 이탈 시 정확한 EMPTY 복원에 사용 |
| `Flammability[]` | byte | FIRE 전파 시 | 인화성 0(낮음)~255(폭발). 재질별 기본값은 SimulationConstants 참고 |

## 주요 메서드

```csharp
int  Index(int x, int y)           // 1차원 인덱스 계산
bool InBounds(int x, int y)        // 범위 검사 (음수·초과를 uint 캐스트로 한 번에 처리)
CellType Get(int x, int y)         // 안전 접근 (범위 밖 → SOLID_STATIC 반환해 테두리 = 벽)
void Set(int x, int y, CellType t) // 안전 쓰기
```

## 설계 포인트

- **Lifetime 재활용**: 배열을 두 용도로 재활용해 추가 배열 할당 없이 슬리핑과 수명을 동시에 관리.
- **RigidId의 필요성**: 강체가 이동하면 이전에 점유했던 셀을 정확히 EMPTY 로 되돌려야 한다. 다른 강체가 같은 셀을 점유한 경우 실수로 지우지 않도록 ID 를 검사한다.
- **테두리 = 고체 벽**: `Get()` 이 범위 밖에서 SOLID_STATIC 을 반환하므로 모든 CA 규칙이 별도의 경계 검사 없이 자연스럽게 벽에 막힌다.

## 관련 파일

- [CellType.md](CellType.md) — 셀 타입 정의
- [SimulationConstants.md](SimulationConstants.md) — 상수값
- [PixelSimulator.md](PixelSimulator.md) — 데이터 소비자
- 설계 문서 §5.2
