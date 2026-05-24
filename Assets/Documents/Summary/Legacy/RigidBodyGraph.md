# RigidBodyGraph

> `Assets/Scripts/RigidBody/RigidBodyGraph.cs`

## 역할

지형 셀 간 구조적 연결을 관리하는 그래프.  
노드가 고립(모든 엣지 BROKEN)되면 자동으로 픽셀 레이어로 전환한다 (설계 문서 §2.2).

## 구조

| 요소 | 타입 | 설명 |
|---|---|---|
| 노드 | `RigidBodyNode` | 개별 지형 셀 (Rigidbody2D + PolygonCollider2D) |
| 엣지 | `Edge` | 인접 셀 간 구조적 연결 |
| 노드 딕셔너리 | `Dictionary<int, RigidBodyNode>` | nodeId → 노드 |
| 엣지 딕셔너리 | `Dictionary<long, Edge>` | 두 nodeId 를 조합한 키 → 엣지 |

## 엣지 키 생성

```csharp
// 방향에 무관하게 동일한 키 생성 (a,b) == (b,a)
static long Key(int a, int b) =>
    a <= b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
```

## 주요 메서드

```csharp
void AddNode(RigidBodyNode node)
void RemoveNode(int nodeId)
void AddEdge(int a, int b)

// 엣지를 BROKEN 으로 전환하고 양 노드의 고립 여부를 검사
void BreakEdge(int a, int b, float gapWidth = 2f)

// 유체 틈새 침투 허용 여부 (설계 문서 §8)
bool CanLiquidPass(int fromNode, int toNode)

Edge GetEdge(int a, int b)
```

## 고립 감지 (CheckIsolation)

`BreakEdge` 호출 시 양 노드에 대해 실행:
- 노드에 연결된 `INTACT` 엣지가 하나라도 있으면 → 고립 아님
- 모든 엣지 BROKEN → `node.ConvertToPixel()` 호출 후 노드 제거

## 유체 침투 흐름 (§8)

```
LIQUID 이동 시도
  → 인접 셀이 SOLID_STATIC?
      NO  → 기본 CA 규칙
      YES → CanLiquidPass 조회
              INTACT  → 차단
              BROKEN + GapWidth >= FLOW_THRESHOLD → 통과
```

## 관련 파일

- [Edge.md](Edge.md) — 엣지 구조체
- [RigidBodyNode.md](RigidBodyNode.md) — 노드 컴포넌트
- [SandBlastEngine.md](SandBlastEngine.md) — Graph 프로퍼티로 접근
- 설계 문서 §2, §8
