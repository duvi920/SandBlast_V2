# PixelSimulator

> `Assets/Scripts/Simulation/PixelSimulator.cs`

## 역할

셀룰러 오토마타(CA) 시뮬레이션 엔진. 매 틱 각 타입의 이동·전파 규칙을 적용한다 (설계 문서 §5.5, §6).

## 업데이트 순서 (매 틱)

```
1. FIRE (불)        — 수명 감소, 열 방출, 이웃 연료 발화, 연기 생성, 물에 의한 소화
2. GAS_SMOKE (연기) — 수명 감소, 상승, 수평 표류
3. LIQUID (액체)    — 냉각(LAVA), 수직 하강, 대각 하강, 수평 확산, 압력 역류
4. POWDER (분말)    — 부유(ASH만), 수직 하강, 대각 하강, 슬리핑
5. SOLID_DEBRIS (잔해) — 깨어남 검사, 수직/대각 하강, 슬리핑
```

> FIRE 를 앞에 두는 이유: 같은 틱에 POWDER 가 낙하한 자리에 불이 옮겨붙을 때, 순서가 바뀌면 프레임마다 결과가 달라지는 비결정적 동작이 발생한다.

## 핵심 설계 포인트

### 중복 처리 방지 (`cellUpdateTick`)
`int[] cellUpdateTick` — 셀이 이번 틱에 이미 처리됐는지 기록.  
`Array.Clear()` 없이 현재 틱 번호와 비교해 초기화 비용 제거.

### X축 순서 셔플 (`xShuffle`)
`POWDER`·`DEBRIS` 시뮬 시 고정 X 순서로 순회하면 항상 한쪽으로 쏠리는 artifact 발생.  
매 틱 Fisher-Yates 셔플로 순서를 무작위화해 방지.

### TryMove
```csharp
bool TryMove(int fx, int fy, int tx, int ty)
```
대상 셀이 `EMPTY` 일 때만 이동. 모든 메타 버퍼(Type, Temperature, Lifetime, Flammability)를 swap.

## 타입별 규칙 요약

### FIRE (§6.5)
```
1. lifetime-- → 0 되면 ASH(30%) 또는 EMPTY
2. 인접 4방향 열 방출 → AUTO_IGNITE_TEMP 초과 연료 자동 발화
3. 인접 연료 → flammability/255 확률로 발화
4. 위 EMPTY → SMOKE_CHANCE 로 연기 생성
5. 인접 WATER → 즉시 소화(ASH)
```

### GAS_SMOKE (§6.6)
```
1. lifetime-- → 0 되면 EMPTY
2. 위 EMPTY → 상승
3. 위 막힘 → 좌/우 수평 이동(랜덤)
```

### LIQUID (§6.3, §6.4)
```
LAVA: 매 틱 온도 감소 → 임계 이하 → SOLID_STATIC
1. 아래 EMPTY → 수직 하강
2. 아래 막힘 → 좌하/우하 대각 하강
3. 대각도 막힘 → 수평 확산(dispersion 거리, WATER=5 LAVA=1)
4. 위가 LIQUID → 압력: 좌/우 역류 허용
```

### POWDER_SAND (§6.1)
```
1. 아래 EMPTY → 직하강
2. 아래 막힘 → 좌하/우하 대각 (랜덤)
3. 양쪽 막힘 → sleepCounter++ → SLEEP_THRESHOLD → SOLID_STATIC
```

### POWDER_ASH (§6.2)
```
0. 20% 확률 위 EMPTY → 상승(부유)
1~3. POWDER_SAND 와 동일
```

### SOLID_DEBRIS (§5.4)
```
인접에 LIQUID/POWDER/FIRE 있으면 슬리핑 카운터 리셋
카운터 >= SLEEP_THRESHOLD → SOLID_STATIC 전환
아니면: 수직/대각 하강 시도, 못 움직이면 카운터++
```

## 관련 파일

- [PixelGrid.md](PixelGrid.md) — 데이터 소스
- [ChunkManager.md](ChunkManager.md) — 슬리핑 관리
- [ForceAccumulator.md](ForceAccumulator.md) — 힘 배치 전달
- [SimulationConstants.md](SimulationConstants.md) — 튜닝 상수
- 설계 문서 §5.5, §6
