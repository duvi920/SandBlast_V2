# ChunkManager

> `Assets/Scripts/Chunk/ChunkManager.cs`

## 역할

픽셀 그리드를 32×32 청크로 나눠 슬리핑 상태를 관리한다.  
정지된 청크는 CA 순회에서 완전히 스킵해 퍼포먼스를 확보한다 (설계 문서 §7).

## 청크 레이아웃

```
chunksX = ceil(GridWidth  / CHUNK_SIZE)
chunksY = ceil(GridHeight / CHUNK_SIZE)
청크 인덱스 = cy * chunksX + cx
```

## 주요 메서드

```csharp
// 셀 이동·상태 변경 시 호출 — 소속 청크 활성화, 경계 셀이면 인접 청크도 깨움
void MarkMoved(int x, int y)

// 강체 마스크 동기화 시 호출 — DirtyFlag 설정으로 슬리핑 방지
void MarkRigidDirty(int x, int y)

// 청크 틱 종료 시 호출 — 슬리핑 카운터 업데이트
void EndTick(int cx, int cy, bool anyMoved)

// 셀 좌표로 청크가 활성(비-SLEEPING) 상태인지 확인
bool IsActive(int x, int y)

// 청크 메타데이터 참조 반환 (ref struct)
ref ChunkMeta GetChunk(int cx, int cy)
```

## 슬리핑 로직 (EndTick)

```
anyMoved || DirtyFlag || BorderDirty?
  YES → SleepTimer = 0, State = ACTIVE
  NO  → SleepTimer++
        SleepTimer >= CHUNK_SLEEP_THRESHOLD → State = SLEEPING
```

## 경계 전파 (WakeNeighbor)

`MarkMoved` 호출 시 셀이 청크 경계(로컬 x/y == 0 또는 CHUNK_SIZE-1)에 있으면  
인접 청크의 `BorderDirty = true` 로 설정해 다음 틱에 강제 활성화.

## 강제 웨이크업 트리거

- `MarkMoved` — 셀이 이동할 때
- `MarkRigidDirty` — 강체 마스크 변경 시
- `WakeNeighbor` — 경계 전파 시

## 관련 파일

- [ChunkMeta.md](ChunkMeta.md) — 청크 상태 구조체
- [SimulationConstants.md](SimulationConstants.md) — CHUNK_SIZE, CHUNK_SLEEP_THRESHOLD
- [PixelSimulator.md](PixelSimulator.md) — MarkMoved 호출자
- 설계 문서 §7
