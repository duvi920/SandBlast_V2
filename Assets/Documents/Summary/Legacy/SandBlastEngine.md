# SandBlastEngine

> `Assets/Scripts/SandBlastEngine.cs`

## 역할

엔진 전체를 조율하는 루트 MonoBehaviour. 씬의 모든 서브시스템을 초기화하고 매 프레임 업데이트 루프를 관리한다.

## 주요 책임

| 역할 | 설명 |
|---|---|
| 서브시스템 초기화 | `Awake`에서 PixelGrid, ChunkManager, PixelSimulator, ForceAccumulator, SolidMaskSyncManager, RigidBodyGraph 를 생성 |
| 틱 루프 | `Update`에서 고정 간격(TicksPerSecond)으로 CA 틱을 실행. 프레임 드롭 시 최대 3틱까지 따라잡기 |
| Public API | 외부(게임플레이 코드)에서 셀을 생성하는 진입점 제공 |
| 강체→픽셀 전환 | 고립된 RigidBodyNode 의 폴리곤을 래스터화해 SOLID_DEBRIS 셀로 채움 |

## 퍼블릭 인스펙터 필드

| 필드 | 기본값 | 설명 |
|---|---|---|
| `GridWidth` | 320 | 그리드 가로 픽셀 수 |
| `GridHeight` | 180 | 그리드 세로 픽셀 수 |
| `PixelsPerUnit` | 16 | 픽셀 1셀 = Unity 1유닛 / 16 |
| `GridOrigin` | (0,0) | 그리드 좌하단 모서리의 월드 좌표 |
| `TicksPerSecond` | 30 | 초당 CA 시뮬레이션 틱 수 |

## Public API

```csharp
// 월드 좌표 한 지점에 셀 하나 배치
void SpawnCell(Vector2 worldPos, CellType type, byte flammability = 0)

// 월드 좌표 중심으로 원형 영역에 셀 채우기
void SpawnCircle(Vector2 worldPos, float worldRadius, CellType type, byte flammability = 0)

// 강체 → 픽셀 전환 (RigidBodyNode 가 호출)
void ConvertRigidToPixel(Vector2[] worldPoly, Vector2 center, Vector2 velocity, float angularVel, int nodeId)

// 좌표 변환 유틸리티
Vector2Int WorldToGrid(Vector2 worldPos)
Vector2    GridToWorld(int gx, int gy)
```

## 틱 루프 처리 순서

```
1. SolidMaskSync.SyncAll()   — 강체 마스크 픽셀 레이어 반영
2. simulator.Tick()          — CA 시뮬레이션 (FIRE→SMOKE→LIQUID→POWDER→DEBRIS)
3. forces.Flush()            — 누적 힘 → Rigidbody2D.AddForce 일괄 전달
4. pixelRenderer.UpdateTexture() — 그리드 → Texture2D 베이크
```

## 씬 설정 방법

1. 빈 GameObject에 `SandBlastEngine` 부착
2. 자식 Quad (그리드 크기 = GridWidth/PPU × GridHeight/PPU) 에 `PixelLayerRenderer` 부착
3. 지형 타일 GameObject마다 `RigidBodyNode` 부착 후 `NodeId` 할당

> 에디터 창 `SandBlast > Setup Scene (Ctrl+Shift+S)` 으로 자동 구성 가능

## 관련 파일

- [PixelGrid.md](PixelGrid.md) — 그리드 데이터 구조
- [SandBlastSetupWindow.md](SandBlastSetupWindow.md) — 에디터 자동 설정 창
- 설계 문서 §1, §3.2
