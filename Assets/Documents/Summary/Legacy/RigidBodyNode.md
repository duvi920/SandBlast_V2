# RigidBodyNode

> `Assets/Scripts/RigidBody/RigidBodyNode.cs`

## 역할

강체 레이어의 개별 지형 셀 컴포넌트.  
`Rigidbody2D` + `PolygonCollider2D` 와 함께 부착해 사용하며, 모든 엣지가 끊기면 픽셀 레이어로 전환한다 (설계 문서 §2, §3.2).

## 컴포넌트 의존성

```
RequireComponent: Rigidbody2D, PolygonCollider2D
```

## 생명주기

| 이벤트 | 동작 |
|---|---|
| `Awake` | rb, col 캐싱 / `engine.Graph.AddNode(this)` 등록 |
| `LateUpdate` | 위치·회전 변경 감지 시 `engine.SolidMaskSync.EnqueueDirty(this)` |
| `OnDestroy` | `engine.Graph.RemoveNode(NodeId)` |

## ConvertToPixel (고립 시 호출)

`RigidBodyGraph.CheckIsolation` 이 모든 엣지가 BROKEN임을 감지하면 호출.

```
1. rb.linearVelocity, rb.angularVelocity 캡처
2. PolygonCollider2D 경로 → 월드 좌표 폴리곤 변환
3. engine.ConvertRigidToPixel(worldPoly, ...) 호출
4. Destroy(gameObject)
```

## Dirty Flag (LateUpdate)

강체가 이동·회전한 경우에만 `SolidMaskSyncManager` 에 큐잉해 불필요한 래스터화를 방지.

```csharp
if (rb.position != prevPos || rb.rotation != prevRot)
    engine.SolidMaskSync.EnqueueDirty(this);
```

## 인스펙터 필드

| 필드 | 설명 |
|---|---|
| `NodeId` | RigidBodyGraph 내 고유 ID (수동 할당 또는 툴로 자동 할당) |

## 관련 파일

- [RigidBodyGraph.md](RigidBodyGraph.md) — 노드 등록·관리
- [SolidMaskSyncManager.md](SolidMaskSyncManager.md) — 마스크 동기화
- [SandBlastEngine.md](SandBlastEngine.md) — ConvertRigidToPixel 수신
- 설계 문서 §2.2, §3.2, §4.2
