# ChunkMeta / ChunkState (청크 메타데이터)

> `Assets/Scripts/Chunk/ChunkMeta.cs`

## 역할

청크 한 개의 슬리핑 상태를 추적하는 경량 구조체(Blittable Struct)와 상태 열거체입니다.  
`ChunkManagerSingleton`의 `Metas` NativeArray에 저장되어 Burst Job에서 참조 및 수정됩니다.

## ChunkState (청크 상태)

```csharp
enum ChunkState : byte { ACTIVE, SLEEP_PENDING, SLEEPING }
```

| 상태 | 설명 |
|---|---|
| `ACTIVE` | 이번 틱에 시뮬레이션 대상임 (활성) |
| `SLEEP_PENDING` | 이동 셀 없음 — SleepTimer 증가 중 |
| `SLEEPING` | 완전 정지 — CA 시뮬레이션 시스템에서 이 청크의 순회를 스킵함 |

## ChunkMeta 필드

| 필드 | 타입 | 설명 |
|---|---|---|
| `State` | ChunkState | 현재 청크의 상태 |
| `SleepTimer` | byte | 이동 셀이 없는 연속 틱 수. `CHUNK_SLEEP_THRESHOLD` 초과 시 `SLEEPING`으로 전환 |
| `DirtyFlag` | bool | 이번 틱에 강체 마스크가 변경됨 → 슬리핑 방지 |
| `BorderDirty` | bool | 인접 청크에서 셀이 유입됨 → 슬리핑 방지 |

## 상태 전이도

```
ACTIVE ──(이동 없음)──> SLEEP_PENDING ──(SleepTimer >= 임계값)──> SLEEPING
  ↑                          ↑                                        |
  └── 이동 발생 / DirtyFlag / BorderDirty ──────────────────────────┘
```

## 최적화 원리
- 모든 CA 시뮬레이션 Job(Fire, Liquid 등)은 시작 시 청크의 `State`를 확인합니다.
- `SLEEPING` 상태인 청크는 `Execute` 루프를 즉시 빠져나가 CPU 자원을 절약합니다.
- 셀이 이동하거나 외부 충격(`PixelImpactUtility`)이 발생하면 관련 청크의 `State`를 `ACTIVE`로 즉시 전환합니다.

## 관련 문서

- [CodeOverview.md](../CodeOverview.md) — 전체 ECS 아키텍처
- [SimulationConstants.md](SimulationConstants.md) — `CHUNK_SIZE`, `CHUNK_SLEEP_THRESHOLD` 정의
