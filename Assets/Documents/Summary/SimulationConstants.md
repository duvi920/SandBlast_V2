# SimulationConstants

> `Assets/Scripts/Core/SimulationConstants.cs`

## 역할

시뮬레이션 전반에서 사용하는 튜닝 상수 모음. 게임플레이 느낌을 조정할 때 이 파일 하나만 수정하면 된다.

## 상수 목록

### 청크 슬리핑 (§7)

| 상수 | 값 | 설명 |
|---|---|---|
| `CHUNK_SIZE` | 32 | 청크 한 변의 셀 수 (32×32). 16이면 경계 전파 빈도 과다, 64이면 보스 전투 시 거의 전체 맵 활성 |
| `CHUNK_SLEEP_THRESHOLD` | 5 | 이 틱 수 동안 이동 셀 없으면 SLEEPING 전환 |

### 분말·잔해 슬리핑 (§5.4, §6.1)

| 상수 | 값 | 설명 |
|---|---|---|
| `SLEEP_THRESHOLD` | 10 | 이 틱 수 동안 이동 없으면 SOLID_STATIC 전환 (권장: 8~12) |

### 액체 확산 (§6.3, §6.4)

| 상수 | 값 | 설명 |
|---|---|---|
| `WATER_DISPERSION` | 5 | 물 수평 확산 거리 (셀/틱) |
| `LAVA_DISPERSION` | 1 | 용암 수평 확산 거리 — 점성 표현 |

### 불·연기 (§6.5, §6.6)

| 상수 | 값 | 설명 |
|---|---|---|
| `FIRE_LIFE_MAX` | 80 | 불의 최대 수명(틱) |
| `SMOKE_LIFE_MAX` | 40 | 연기의 최대 수명(틱) |
| `SMOKE_CHANCE` | 0.15 | 불 위에 연기 생성 확률 (15%) |
| `ASH_CHANCE` | 0.30 | 불 소멸 시 재(ASH) 남을 확률 (30%) |

### 온도 (§6.4, §6.5)

| 상수 | 값 | 설명 |
|---|---|---|
| `AUTO_IGNITE_TEMP` | 200 | 이 온도 이상이면 연료 자동 발화 |
| `HEAT_EMISSION` | 10 | 불·용암이 인접 셀에 매 틱 전달하는 열량 |
| `COOLING_RATE` | 2 | 매 틱 자연 냉각량 |
| `LAVA_SOLIDIFY_THRESHOLD` | 30 | 이 온도 이하면 용암이 SOLID_STATIC으로 굳음 |
| `LAVA_INITIAL_TEMP` | 240 | 용암 생성 시 초기 온도 |

### 인화성 프리셋 (§6.5)

| 상수 | 값 | 재질 |
|---|---|---|
| `FLAMMABILITY_WOOD` | 80 | 나무 |
| `FLAMMABILITY_METAL` | 5 | 금속 |
| `FLAMMABILITY_EXPLOSIVE` | 255 | 폭발물 |
| `FLAMMABILITY_DEBRIS` | 40 | 강체→픽셀 전환 잔해 기본값 |

### 기타

| 상수 | 값 | 설명 |
|---|---|---|
| `ASH_FLOAT_CHANCE` | 0.20 | 재가 위로 떠오를 확률 (열기류 연출) |
| `EXPLOSION_FORCE_SCALE` | 0.5 | 폭발 파티클 → 강체 힘 배율 |
| `LIQUID_PRESSURE_SCALE` | 0.01 | 유체 압력 → 강체 힘 배율 |
| `FLOW_THRESHOLD` | 1.5 | 유체 틈새 침투 허용 최소 틈 너비(픽셀) |

## 관련 파일

- [PixelSimulator.md](PixelSimulator.md) — 상수 소비자
- [ChunkManager.md](ChunkManager.md) — CHUNK_SIZE, CHUNK_SLEEP_THRESHOLD 사용
- 설계 문서 §5.4, §6, §7, §8, §9
