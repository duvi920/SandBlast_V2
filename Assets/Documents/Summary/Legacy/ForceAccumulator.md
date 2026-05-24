# ForceAccumulator

> `Assets/Scripts/Physics/ForceAccumulator.cs`

## 역할

CA 시뮬레이션 중 픽셀별 힘 기여를 누적하고, 틱 끝에 `AddForce` 로 일괄 적용한다.  
매 픽셀마다 `AddForce` 를 호출하면 프레임당 수천 번 호출이 발생하므로 배치 처리한다 (설계 문서 §9).

## API

```csharp
void Register(int id, Rigidbody2D rb)   // 강체 등록 (RigidBodyNode.Awake 에서 호출)
void Unregister(int id)                 // 강체 해제 (RigidBodyNode.OnDestroy 에서 호출)
void Accumulate(int rigidId, Vector2 force) // CA 시뮬 중 힘 누적 (실제 AddForce 없음)
void Flush()                            // 틱 종료 시 일괄 전달 후 버퍼 초기화
```

## 힘 배율 (SimulationConstants)

| 상황 | 계수 | 값 |
|---|---|---|
| 폭발 파티클 | `EXPLOSION_FORCE_SCALE` | 0.5 |
| 유체 압력 | `LIQUID_PRESSURE_SCALE` | 0.01 |

## 흐름

```
CA 시뮬 중 충격 발생
    → Accumulate(rigidId, force)   // pending 딕셔너리에 누적
틱 종료
    → Flush()                      // rigidBodies[id].AddForce(force, Impulse)
                                   // pending.Clear()
```

## 관련 파일

- [SandBlastEngine.md](SandBlastEngine.md) — Flush() 호출 시점
- [PixelSimulator.md](PixelSimulator.md) — Accumulate() 호출자
- 설계 문서 §9
