# SolidMaskSyncManager

> `Assets/Scripts/Sync/SolidMaskSyncManager.cs`

## 역할

이동하는 `RigidBodyNode` 의 콜라이더를 픽셀 레이어에 `SOLID_RIGID` 셀로 동기화한다.  
매 프레임 폴리곤 래스터화 → 이전 마스크와 XOR 비교 → 변경 셀만 픽셀 레이어에 반영 (설계 문서 §4).

## 파이프라인 (SyncNode)

```
[RigidBodyNode 위치·회전 변경 감지 (LateUpdate)]
        ↓ EnqueueDirty
[dirty 큐]
        ↓ SyncAll → SyncNode
[PolygonCollider2D → 월드 좌표 폴리곤]
        ↓
[Scanline AABB + PointInPolygon 래스터화] → curr HashSet<int>
        ↓
[XOR diff: prevMask vs curr]
  ├─ curr에 새로 추가된 셀 → CoverCell  (SOLID_RIGID 기록, 기존 픽셀 Displacement)
  └─ prev에서 벗어난 셀   → UncoverCell (SOLID_RIGID 해제 → EMPTY)
        ↓
[prevMasks[nodeId] = curr]
```

## Displacement (CoverCell)

강체가 새로 덮는 셀에 기존 픽셀이 있으면 강체 이동 방향으로 밀어낸다.

```
탐색 순서: (velocity 방향), (위), (velocity 반대), (우), (좌)
빈 셀을 찾으면 해당 위치로 픽셀 이동
모두 막히면 픽셀 소멸
```

## 주요 메서드

```csharp
void EnqueueDirty(RigidBodyNode node) // RigidBodyNode.LateUpdate 에서 호출
void SyncAll()                        // SandBlastEngine.Update 에서 매 틱 호출
void RemoveNode(int nodeId)           // 강체 → 픽셀 전환 시 이전 마스크 전부 EMPTY 복원
```

## 설계 포인트

- **XOR diff**: 전체 셀을 매번 초기화하지 않고 변경된 셀만 처리해 퍼포먼스 확보
- **RigidId 검사**: `UncoverCell` 시 `RigidId` 가 일치하는 경우에만 EMPTY 복원. 다른 강체가 같은 셀을 점유 중이면 건드리지 않음
- **Dirty Flag**: 위치·회전 변경 시에만 큐잉 → 정지한 강체는 래스터화 비용 없음

## 관련 파일

- [RigidBodyNode.md](RigidBodyNode.md) — EnqueueDirty 호출자
- [PixelGrid.md](PixelGrid.md) — SOLID_RIGID·RigidId 쓰기 대상
- [ChunkManager.md](ChunkManager.md) — MarkRigidDirty 호출
- [GridUtils.md](GridUtils.md) — PointInPolygon 사용
- 설계 문서 §4
