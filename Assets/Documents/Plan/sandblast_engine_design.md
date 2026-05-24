# 샌드 블라스트 엔진 (Sand Blast Engine)
> 지형 파괴 및 픽셀 물리 시뮬레이션 엔진 설계 문서

---

## 1. 개요

샌드 블라스트 엔진은 게임의 지형 파괴, 유체, 분말, 화재 등을 담당하는 물리 시뮬레이션 엔진이다.
두 개의 레이어를 병렬로 운용하며 상호작용을 처리한다.

| 레이어 | 기술 | 역할 |
|---|---|---|
| 강체 레이어 | Unity Physics 2D (Rigidbody2D, Collider2D) | 지형 구조물, 보스, 이동 오브젝트 |
| 픽셀 레이어 | Cellular Automata | 파괴 잔해, 유체, 모래, 화재, 연기 |

---

## 2. 강체 레이어

### 2.1 책임 경계

강체 레이어와 픽셀 레이어의 책임은 **전환 시점을 기준으로 명확히 분리**된다.

| 구간 | 담당 시스템 | 책임 |
|---|---|---|
| 강체로 존재하는 동안 | 강체 그래프 | 파괴 실루엣, 물리 충돌 형태, 엣지 절단 판정 |
| 픽셀로 전환된 이후 | Cellular Automata | 잔해 낙하, 유체 흐름, 화재 전파 등 모든 CA 규칙 |

전환 이후 강체의 모양 정보는 CA에 전달되지 않는다. 픽셀 레이어는 전환된 셀을 모양과 무관하게 `SOLID_DEBRIS`로 취급하고 CA 규칙만 적용한다.

### 2.2 강체 그래프

지형 구조물은 사전 생성된 그래프로 관리된다. 레벨 로드 시 빌드하며 런타임 생성은 없다.

- 노드: 개별 지형 셀 (Rigidbody2D + PolygonCollider2D)
- 엣지: 인접 셀 간 조인트/체인 연결
- 데미지를 받으면 해당 엣지가 끊어진다.
- 연결이 모두 끊어진 고립 노드는 픽셀 레이어로 전환된다.

```
[셀 A] ── 조인트 ── [셀 B] ── 조인트 ── [셀 C]
                        ↓ 데미지
[셀 A] ── 조인트 ── [셀 B]              [셀 C] → SOLID_DEBRIS 전환
```

셀 모양(PolygonCollider2D)은 강체로 존재하는 동안에만 의미를 갖는다. 파괴 실루엣의 불규칙한 느낌은 이 모양에서 나온다.

---

## 3. 픽셀↔강체 경계 처리

### 3.1 경계 케이스 분류

| # | 케이스 | 복잡도 | 구현 우선순위 |
|---|---|---|---|
| 1 | 픽셀이 강체 위에 쌓임 | 낮음 | 2순위 |
| 2 | 유체가 강체 틈으로 침투 | 매우 높음 | 4순위 |
| 3 | 강체 → 픽셀 전환 순간 | 높음 | 1순위 |
| 4 | 픽셀이 강체에 힘 인가 | 중간 | 3순위 |

### 3.2 케이스 3 — 강체 → 픽셀 전환

전환 순간 `Rigidbody2D`의 속도와 회전량을 캡처해 파티클에 분배한다.

```
파티클 초기속도 = 강체 velocity + (angularVelocity × 파티클의 무게중심 오프셋)
```

셀 경계면 파티클에는 바깥 방향 폭발 벡터를 추가해 자연스러운 파쇄감을 연출한다.

### 3.3 케이스 1 — 픽셀이 강체 위에 쌓임

강체를 픽셀 레이어의 **불투과 영역**으로 취급한다.

- Collider 외곽선을 픽셀 레이어 고체 마스크로 매 프레임 동기화
- 강체가 이동하면 그 위의 픽셀은 displacement로 밀려남
- 완전한 마찰/접촉력 시뮬은 생략 (퍼포먼스 대비 게임플레이 기여 낮음)

### 3.4 케이스 4 — 픽셀이 강체에 힘 인가

- 매 픽셀마다 `AddForce` 호출 금지 (퍼포먼스 킬러)
- 한 프레임 내 특정 반경 파티클의 운동량을 **합산 후 일괄 전달** (배치 처리)

### 3.5 케이스 2 — 유체 틈새 침투

- 완전한 시뮬레이션은 보류
- 강체 그래프 엣지를 **열림/닫힘** 상태로 관리
- 연결이 끊긴 엣지 경계만 유체 통과 허용

---

## 4. 고체 마스크 동기화

### 4.1 파이프라인

```
[Rigidbody2D 위치/회전]
        ↓
[Dirty Flag 체크] → 변경 없으면 스킵
        ↓
[PolygonCollider2D 월드 좌표 폴리곤 추출]
        ↓
[Scanline Fill 래스터라이즈] (AABB 클리핑 후 pointInPolygon)
        ↓
[XOR diff: solidPrev vs solidCurr]
        ↓
[변경 셀만 픽셀 레이어에 반영]
 ├─ 새로 덮인 셀 → SOLID_RIGID 기록 / 기존 픽셀 Displacement
 └─ 벗어난 셀   → SOLID_RIGID 해제 → EMPTY 복원
```

### 4.2 Dirty Flag

```csharp
Vector2 prevPos;
float prevRot;

void LateUpdate() {
    if (rb.position != prevPos || rb.rotation != prevRot) {
        MaskSyncManager.EnqueueDirty(this);
        prevPos = rb.position;
        prevRot = rb.rotation;
    }
}
```

### 4.3 Displacement

강체가 픽셀 셀을 새로 덮을 때 기존 픽셀 처리:

- 해당 셀이 `EMPTY` → `SOLID_RIGID` 기록
- 해당 셀에 픽셀(유체/모래 등)이 있으면 → 강체 `velocity` 방향으로 밀어내기
- 밀린 픽셀은 픽셀 시뮬레이션 큐에 재삽입 (자연 낙하 처리)

---

## 5. 픽셀 타입 시스템

### 5.1 CellType 열거체 (byte)

```csharp
public enum CellType : byte {
    EMPTY          = 0,

    // 고체
    SOLID_STATIC   = 1,  // 지형 셀, 파괴 전
    SOLID_RIGID    = 2,  // 이동 강체 점유 영역
    SOLID_DEBRIS   = 3,  // 픽셀화된 잔해, 중력 적용

    // 유체
    LIQUID_WATER   = 4,  // 수평 확산, 압력
    LIQUID_LAVA    = 5,  // 점성, 냉각 시 고체화

    // 분말
    POWDER_SAND    = 6,  // 안식각, 대각 흐름
    POWDER_ASH     = 7,  // 가벼운 분말, 부유

    // 에너지
    FIRE           = 8,  // 연소 전파, 수명
    GAS_SMOKE      = 9,  // 상승 확산, 소멸 틱
}
```

### 5.2 셀 데이터 구조

메인 그리드와 메타 버퍼를 분리해 캐시 효율을 높인다.

```csharp
// 메인 그리드 — 매 틱 전체 순회
byte[] cellType;

// 메타 버퍼 — 해당 타입만 접근
byte[] temperature;   // FIRE, LAVA, LIQUID
byte[] lifetime;      // FIRE, GAS_SMOKE 소멸 카운터 / DEBRIS 슬리핑 카운터
byte[] rigidId;       // SOLID_RIGID → 소속 강체 ID
```

`rigidId`는 강체가 벗어날 때 정확한 `SOLID_RIGID → EMPTY` 복원에 사용된다.

### 5.3 상태 전환 규칙

| 전환 | 조건 |
|---|---|
| `SOLID_STATIC` → `SOLID_DEBRIS` | 강체 그래프 노드 고립 (연결 전부 끊김) |
| `SOLID_DEBRIS` → `SOLID_STATIC` | 슬리핑: 일정 틱 동안 이동 없음 |
| `LIQUID_LAVA` → `SOLID_STATIC` | 온도 임계값 이하로 냉각 |
| `FIRE` → `GAS_SMOKE` | 수명 소진 |

### 5.4 SOLID_DEBRIS 슬리핑

잔해가 쌓여 정지하면 매 틱 순회 비용을 줄이기 위해 `SOLID_STATIC`으로 전환한다.

```
lifetime 버퍼를 슬리핑 카운터로 재활용

매 틱:
  DEBRIS 셀이 이동하지 않으면 → lifetime++
  lifetime >= SLEEP_THRESHOLD → CellType = SOLID_STATIC (순회에서 제외)

깨어나는 조건:
  인접 셀에 LIQUID / POWDER / FIRE 접촉
  강체 Displacement로 충격 발생
```

**SLEEP_THRESHOLD 권장값**: 8~12틱 (프레임레이트에 따라 조정)

### 5.5 시뮬레이션 업데이트 순서

매 틱 처리 순서는 결과의 일관성에 직결된다.

```
1. SOLID_RIGID 마스크 동기화   ← 강체 위치 확정 먼저
2. FIRE 전파 + 수명 감소
3. GAS_SMOKE 상승 + 소멸
4. LIQUID 흐름
5. POWDER 낙하 + 대각 흐름
6. SOLID_DEBRIS 중력 + 슬리핑 카운터
```

FIRE를 앞에 두는 이유: 같은 틱에 POWDER가 떨어진 자리에 불이 옮겨붙을 때,
순서가 바뀌면 프레임마다 결과가 달라지는 비결정적 동작이 발생한다.

---

## 6. 타입별 시뮬레이션 규칙

> CA 규칙은 우선순위 순서대로 처리한다. 위 조건이 충족되면 아래 조건은 평가하지 않는다.

### 6.1 POWDER_SAND 이동 규칙

X 순회 순서를 매 프레임 뒤섞어 한쪽으로 쏠리는 artifact를 방지한다.

```
1. 아래 EMPTY → 직하강
2. 아래 막힘 → 좌하/우하 중 EMPTY 있으면 랜덤 선택 낙하
3. 양쪽 대각 모두 막힘 → 정지, sleepCounter++
4. sleepCounter >= SLEEP_THRESHOLD → SOLID_STATIC 전환
```

### 6.2 POWDER_ASH 이동 규칙

SAND와 동일하나 부유 체크를 앞에 추가한다.

```
0. 20% 확률로 위 EMPTY → 상승 (부유 효과)
1~4. POWDER_SAND와 동일
```

FIRE 인접 시 부유 확률을 높이면 열기류 연출이 가능하다.

### 6.3 LIQUID_WATER 이동 규칙

`dispersion` 값이 수평 확산 속도를 결정한다. 권장값: 4~6.

```
1. 아래 EMPTY → 직하강
2. 아래 막힘 → 좌하/우하 중 EMPTY 있으면 랜덤 선택 낙하
3. 대각도 막힘 → 수평 방향으로 dispersion 거리만큼 탐색,
                 첫 번째 빈 공간으로 이동
4. 위 셀이 LIQUID → 압력 전파, 수평 방향으로 역류 허용
```

### 6.4 LIQUID_LAVA 이동 규칙

WATER와 동일하나 아래 두 값이 다르다.

```csharp
const int LAVA_DISPERSION = 1;   // WATER: 4~6 → 점성 표현
const int WATER_DISPERSION = 5;

// 냉각 고체화
if (cellType == LIQUID_LAVA) {
    temperature[i] -= COOLING_RATE;
    if (temperature[i] <= SOLIDIFY_THRESHOLD)
        cellType[i] = SOLID_STATIC;
}
```

### 6.5 FIRE 연소 전파 규칙

```
매 틱, FIRE 셀마다:

1. lifetime--
   lifetime == 0 → cellType = ASH, 이하 스킵

2. 인접 4방향 탐색
   인접 셀이 FUEL이면:
     rand() < flammability[neighbor] / 255
     → cellType[neighbor] = FIRE
        lifetime[neighbor] = FIRE_LIFE_MAX ± 랜덤 편차

3. 위 셀이 EMPTY이면:
   rand() < SMOKE_CHANCE
   → cellType[above] = GAS_SMOKE
      smoke_lifetime[above] = SMOKE_LIFE_MAX

4. 인접 셀이 LIQUID_WATER이면:
   → FIRE 즉시 소멸 → ASH
```

#### FIRE 메타 데이터

```csharp
byte lifetime[i];      // 남은 연소 틱. 0 되면 ASH
byte temperature[i];   // 주변 온도 누적 (LAVA 연동에도 사용)

// FUEL 셀
byte flammability[i];  // 재질별 인화성
                       // 나무=80, 금속=5, 폭발물=255
```

#### 온도 연동 — LAVA/FIRE 시너지

`temperature` 버퍼를 공유하면 LAVA 근처에서 FUEL이 자동 점화된다.

```csharp
foreach (neighbor of [x, y]) {
    temperature[neighbor] += HEAT_EMISSION;
    if (temperature[neighbor] > AUTO_IGNITE_TEMP
        && cellType[neighbor] == FUEL)
        cellType[neighbor] = FIRE;
}
// 자연 냉각
temperature[i] = max(0, temperature[i] - COOLING_RATE);
```

#### 불 색상 — lifetime 기반

```csharp
Color FireColor(byte lifetime) {
    float r = lifetime / (float)FIRE_LIFE_MAX;
    if (r > 0.6f) return Color.red;     // 붉은 불꽃
    if (r > 0.3f) return Color.amber;   // 주황 불꽃
    return Color.yellow;                // 꺼져가는 노란 불꽃
}
```

### 6.6 GAS_SMOKE 이동 규칙

```
1. smoke_lifetime--
   smoke_lifetime == 0 → EMPTY

2. 위 EMPTY → 직상승, smoke_lifetime 인계

3. 위 막힘 → 좌/우 중 EMPTY 있으면 랜덤 수평 이동

4. 모두 막힘 → 제자리 유지, smoke_lifetime 계속 감소
```

---

## 7. 청크 슬리핑

픽셀 레이어 전체를 매 틱 순회하면 월드가 넓어질수록 퍼포먼스가 선형으로 증가한다. 청크 단위로 슬리핑을 적용해 활성 영역만 순회한다.

### 7.1 청크 메타데이터

```csharp
struct ChunkMeta {
    ChunkState state;       // ACTIVE | SLEEP_PENDING | SLEEPING
    byte       sleepTimer;  // 이동 셀 없는 틱 카운트
    bool       dirtyFlag;   // 강체 마스크 변경 여부
    bool       borderDirty; // 인접 청크에서 셀 유입 여부
}

enum ChunkState : byte { ACTIVE, SLEEP_PENDING, SLEEPING }
```

**CHUNK_SIZE 권장값: 32×32**
16이면 경계 전파 횟수가 많고, 64이면 보스 전투 중 거의 전체 맵이 활성화될 수 있다.

### 7.2 매 틱 청크 매니저 루프

```csharp
void TickAll() {
    foreach (chunk in chunks) {
        if (chunk.state == SLEEPING) continue; // 완전 스킵

        bool anyMoved = TickChunk(chunk);

        if (anyMoved || chunk.dirtyFlag || chunk.borderDirty) {
            chunk.sleepTimer = 0;
            chunk.state = ACTIVE;
        } else {
            chunk.sleepTimer++;
            chunk.state = SLEEP_PENDING;
            if (chunk.sleepTimer >= CHUNK_SLEEP_THRESHOLD)
                chunk.state = SLEEPING;
        }

        chunk.dirtyFlag = false;
        chunk.borderDirty = false;
    }
}
```

### 7.3 경계 전파 — 인접 청크 웨이크업

LIQUID나 FIRE가 청크 경계를 넘어갈 때 인접 슬리핑 청크를 깨운다.

```csharp
// 경계 셀에 활성 타입이 있으면 인접 청크에 마킹
if (IsBorderCell(x, y) && cellType != EMPTY) {
    ChunkMeta neighbor = GetNeighborChunk(x, y);
    neighbor.borderDirty = true; // 슬리핑 상태여도 강제 마킹
}
```

### 7.4 강제 웨이크업 트리거

슬리핑 청크도 즉시 ACTIVE로 전환하는 조건:

- 인접 활성 청크에서 셀 유입 (경계 전파)
- 강체 Displacement 범위 내 진입
- 폭발 / 충격파 반경 내 포함

---

## 8. 케이스 2 — 유체 틈새 침투

### 8.1 처리 흐름

LIQUID 셀이 `SOLID_STATIC`으로 이동을 시도할 때, 강체 그래프 엣지 상태를 조회해 통과 여부를 결정한다.

```
LIQUID 이동 시도
  → 인접 셀이 SOLID_STATIC?
      NO  → 기본 CA 규칙 적용 (통과)
      YES → 엣지 상태 조회
              INTACT  → 이동 차단
              BROKEN  → 틈새 너비 계산
                          gapWidth >= FLOW_THRESHOLD → 통과 허용
                          gapWidth <  FLOW_THRESHOLD → 차단
```

### 8.2 구현

엣지 상태는 강체 그래프에 이미 있는 정보라 추가 데이터 구조 없이 조회만 추가하면 된다.

```csharp
bool CanLiquidPass(int fromNode, int toNode) {
    Edge edge = graph.GetEdge(fromNode, toNode);
    if (edge == null || edge.state == EdgeState.INTACT) return false;
    return edge.gapWidth >= FLOW_THRESHOLD;
}
```

`gapWidth`는 엣지가 끊길 때 해당 폴리곤 경계 길이를 저장해두면 된다.

**FLOW_THRESHOLD 권장값**: 1~2픽셀
너무 낮으면 실금에도 유체가 새고, 너무 높으면 큰 파괴에도 유체가 통과하지 않는 어색함이 생긴다.

유체 압력 누적은 별도 구현이 필요 없다. CA 규칙 4(압력 전파)에서 막힌 쪽 수위가 자연스럽게 올라가는 동작이 이미 처리된다.

---

## 9. AddForce 배치 처리

### 9.1 문제

CA 시뮬 중 매 픽셀마다 `AddForce`를 호출하면 프레임당 수천 번 호출이 발생한다.

### 9.2 구현 — 틱 끝 일괄 처리

```csharp
// rigidId → 누적 힘 버퍼
Dictionary<int, Vector2> pendingForces = new();

// CA 시뮬 중 충격 발생 시 — 실제 AddForce 호출 없음
void AccumulateForce(int rigidId, Vector2 force) {
    if (!pendingForces.ContainsKey(rigidId))
        pendingForces[rigidId] = Vector2.zero;
    pendingForces[rigidId] += force;
}

// 틱 마지막에 일괄 처리
void FlushForces() {
    foreach (var (id, force) in pendingForces) {
        Rigidbody2D rb = rigidBodies[id];
        rb.AddForce(force, ForceMode2D.Impulse);
    }
    pendingForces.Clear();
}
```

### 9.3 타입별 힘 스케일

```csharp
// 폭발 파티클
AccumulateForce(rigidId, velocity * EXPLOSION_FORCE_SCALE);

// 유체 압력 (훨씬 약하게)
AccumulateForce(rigidId, Vector2.down * LIQUID_PRESSURE_SCALE);
```

---

## 10. 렌더링 합성

### 10.1 렌더 순서

```
1. 픽셀 레이어 → RenderTexture 베이크
   CA 그리드 전체를 텍스처로 변환

2. 강체 레이어 렌더
   Unity 스프라이트 / 메시 그대로 출력

3. 픽셀 레이어를 강체 위에 합성
   SOLID_RIGID 셀 → 알파 0 (구멍)
   나머지 셀 → 알파 1 (불투명)
```

`SOLID_RIGID` 셀을 투명하게 빼는 이유: 강체가 이동할 때 픽셀 잔해 위에 올라오는 느낌을 내기 위해서다. 빼지 않으면 강체 이동 시 픽셀 텍스처가 강체 위를 덮어버린다.

### 10.2 셰이더 처리

```hlsl
// 픽셀 레이어 합성 셰이더
float4 frag(v2f i) : SV_Target {
    float4 pixel = tex2D(_PixelTex, i.uv);
    float4 rigid = tex2D(_RigidTex, i.uv);

    // SOLID_RIGID 마스크 위치는 투명하게
    float mask = tex2D(_RigidMaskTex, i.uv).r;
    pixel.a *= (1.0 - mask);

    // 강체를 픽셀 위에 합성
    return lerp(pixel, rigid, rigid.a);
}
```

---

## 11. 미결 사항

- [x] 픽셀 레이어 청크 분할 및 비활성 청크 슬리핑
- [x] 케이스 2 — 유체 틈새 침투 상세 설계
- [x] 폭발 파티클 → 강체 AddForce 배치 처리 상세 설계
- [x] 픽셀 레이어 렌더링 (강체 레이어와 합성 방식)
