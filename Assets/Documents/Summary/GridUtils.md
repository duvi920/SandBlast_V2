# GridUtils

> `Assets/Scripts/Core/GridUtils.cs`

## 역할

그리드 관련 정적 유틸리티 메서드 모음. 현재는 점-폴리곤 포함 판정 하나만 포함한다.

## PointInPolygon

```csharp
bool PointInPolygon(Vector2 point, Vector2[] poly)
```

**알고리즘**: 레이 캐스팅 (조르단 곡선 정리)  
점에서 오른쪽으로 무한 광선을 쏴 폴리곤 변과 홀수 번 교차하면 내부.

**사용처**

| 호출자 | 목적 |
|---|---|
| `SandBlastEngine.RasterizeWorldPolygon` | 강체 → 픽셀 전환 시 폴리곤 내부 셀 열거 |
| `SolidMaskSyncManager.RasterizePoly` | 강체 이동 시 점유 셀 계산 |

**시간 복잡도**: O(n) — n은 폴리곤 꼭짓점 수

## 관련 파일

- [SandBlastEngine.md](SandBlastEngine.md)
- [SolidMaskSyncManager.md](SolidMaskSyncManager.md)
