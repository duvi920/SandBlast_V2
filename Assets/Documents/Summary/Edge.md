# Edge / EdgeState

> `Assets/Scripts/RigidBody/Edge.cs`

## 역할

강체 그래프에서 두 인접 노드 사이의 구조적 연결(조인트/체인)을 나타낸다.  
데미지를 받으면 `BROKEN` 상태로 전환되고, 끊어진 틈의 너비(`GapWidth`)가 `FLOW_THRESHOLD` 이상이면 유체가 통과할 수 있다 (설계 문서 §2.2, §8).

## EdgeState

```csharp
enum EdgeState : byte { INTACT, BROKEN }
```

| 상태 | 설명 |
|---|---|
| `INTACT` | 연결 유지 중 — 이동 차단, 유체 통과 불가 |
| `BROKEN` | 끊김 — GapWidth >= FLOW_THRESHOLD 면 유체 통과 허용 |

## Edge 필드

| 필드 | 설명 |
|---|---|
| `NodeA`, `NodeB` | 연결된 두 노드의 ID |
| `State` | 현재 상태 (기본값: INTACT) |
| `GapWidth` | 엣지가 끊길 때 해당 폴리곤 경계 길이를 저장. 유체 침투 판정에 사용 |

## 유체 침투 판정

```csharp
// RigidBodyGraph.CanLiquidPass
bool CanLiquidPass(int fromNode, int toNode)
{
    Edge edge = GetEdge(fromNode, toNode);
    if (edge == null || edge.State == EdgeState.INTACT) return false;
    return edge.GapWidth >= SimulationConstants.FLOW_THRESHOLD; // 권장: 1~2픽셀
}
```

## 관련 파일

- [RigidBodyGraph.md](RigidBodyGraph.md) — Edge 를 소유·관리
- [SimulationConstants.md](SimulationConstants.md) — FLOW_THRESHOLD
- 설계 문서 §8
