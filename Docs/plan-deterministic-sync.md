# 결정론적 락스텝(입력 동기화) 기반 동기화 전환 계획

**작성일:** 2026-05-24
**상태:** 계획 (Plan)

---

## 1. 개요 및 목표

현재 `SandBlast`는 서버에서 셀룰러 오토마타(CA) 픽셀 시뮬레이션을 수행하고, 변경된 청크의 픽셀 데이터(Diff)를 클라이언트에게 RPC로 전송하여 동기화하는 방식을 사용하고 있습니다. 
이 방식은 픽셀의 변화량이 많아질수록 네트워크 대역폭이 폭증하고, 서버에 큰 부하를 주는 문제가 있습니다.

이를 해결하기 위해 **결정론적 시뮬레이션(Deterministic Simulation)**과 **입력 동기화(Input Sync / Lockstep)** 방식으로 아키텍처를 전면 개편합니다.
즉, 모든 클라이언트와 서버가 **완벽하게 동일한 픽셀 이동 규칙과 난수 시드**를 가지게 만들고, 네트워크로는 픽셀 데이터 대신 **플레이어의 입력(이동, 마법 사용 등)만 동기화**하여 각자 로컬에서 시뮬레이션을 진행하도록 변경합니다.

---

## 2. 기존 방식과 새로운 방식 비교

### 기존 방식 (State Sync - Chunk Diff)
- **과정:** 서버에서 CA 연산 -> 변경된 청크 계산 -> `PixelGridDeltaRpc` 생성 -> 클라이언트에 전송 -> 클라이언트 픽셀 덮어쓰기.
- **단점:** 대규모 폭발이나 물이 흐를 때 대역폭 소모 극심, 틱 지연 시 화면이 끊겨 보임.

### 새로운 방식 (Deterministic Input Sync)
- **과정:** 클라이언트가 '마법 사용' 입력을 서버로 전송 -> 서버가 모든 클라이언트의 입력을 취합하여 동일한 틱(Tick)에 분배 -> 각 클라이언트는 동일한 입력을 바탕으로 자체적으로 CA 시뮬레이션 수행.
- **장점:** 네트워크 대역폭 소모가 플레이어 입력 크기로 고정됨 (매우 적음). 부드러운 화면 갱신 가능.
- **단점:** 클라이언트 간 시뮬레이션 결과가 1픽셀이라도 달라지면(나비효과) 영구적인 Desync(비동기화) 발생. 롤백(Rollback) 처리가 까다로움.

---

## 3. 단계별 전환 계획

### Phase 1: 시뮬레이션 결정론 확보 (Determinism Guarantee)
결정론적 동기화의 핵심은 "동일한 입력 = 동일한 결과"를 100% 보장하는 것입니다.
- **Job System 실행 순서 제어:** `IJobParallelFor` 사용 시 스레드 실행 순서에 따라 픽셀이 이동/충돌하는 결과가 달라질 수 있습니다(Race Condition). 병렬 처리 시 공간 분할(Chunk) 단위로 배타적인 접근을 보장하거나, 픽셀 처리 순서를 고정해야 합니다.
- **난수(Random) 동기화:** `UnityEngine.Random` 대신 `Unity.Mathematics.Random`을 사용하고, 시뮬레이션 틱(Tick) 번호를 시드(Seed)로 사용하여 모든 클라이언트가 동일한 난수를 생성하도록 합니다.
- **부동소수점(Float) 오차 제거:** 픽셀 시스템 내의 물리 연산이나 이동에서 부동소수점 오차가 발생하지 않도록 정수(Integer) 기반이나 고정 소수점(Fixed-point) 연산으로 통일합니다.

### Phase 2: 상태 동기화 제거 (Remove Pixel State Sync)
기존 픽셀 데이터를 전송하던 무거운 시스템들을 제거합니다.
- `PixelGridSyncSystem.cs` 무효화 및 삭제
- `PixelGridRpcSendSystem.cs` 및 `PixelGridRpcApplySystem.cs` 삭제
- `PixelGridDeltaRpc.cs` 삭제
- 맵 최초 로딩 시 초기 상태를 맞추기 위한 `PixelGridInitialSyncSystem.cs`는 유지하되, 전체 맵 데이터 또는 **맵 시드(Seed)**를 동기화하는 방식으로 최적화합니다.

### Phase 3: 입력 및 커맨드 동기화 (Input/Command Sync)
Netcode for Entities(NfE)의 기능을 활용하여 입력을 동기화합니다.
- **ICommandData 도입:** 플레이어의 조작(좌우 이동, 점프, 완드 조준 각도, 발사 트리거 등)을 `ICommandData`로 캡슐화합니다.
- 모든 투사체 발사, 폭발 생성 등 픽셀에 영향을 주는 행위는 로컬에서의 직접 실행을 금지하고, 서버로부터 확정된 입력 틱(Tick)에만 실행되도록 변경합니다.

### Phase 4: 동기화 검증 및 Desync 감지 시스템 구현
결정론적 시뮬레이션은 개발 중 잦은 Desync를 유발합니다. 이를 빠르게 잡기 위한 도구가 필요합니다.
- **Hash Check System:** 매 N 틱마다 서버와 클라이언트가 `PixelGridSingleton`의 전체 배열 상태를 해싱(Hash)하여 값을 비교합니다.
- 해시값이 불일치할 경우 즉시 경고 로그를 띄우고, 필요하다면 방 리셋(Room Reset)을 트리거하는 강제 복구 절차를 마련합니다.

### Phase 5: 롤백(Rollback) 전략 (Delay Lockstep 타협)
Netcode for Entities는 기본적으로 클라이언트 예측(Prediction)과 롤백을 지원하지만, 수백만 개의 픽셀 상태를 매 틱 저장하고 롤백하는 것은 메모리와 CPU에 큰 부담입니다.
- **권장 전략 (Delay Lockstep):** 픽셀 시스템에 대한 롤백을 포기하고, 입력을 일정 시간 지연(Delay)시켜 모든 클라이언트가 서버로부터 확정된 입력을 받은 후에만 시뮬레이션을 진행하게 합니다. (전통적인 RTS 게임의 Lockstep 방식)
- 캐릭터 이동은 예측(Prediction)을 사용하되, 픽셀 지형 파괴나 생성이 일어나는 행위만 확정 틱에서 실행되도록 분리하는 하이브리드 방식을 검토합니다.

---

## 4. 작업 목록 (TODOs)

- [ ] `SimulationConstants.cs`에 결정론적 동기화용 시드 변수 추가
- [ ] `PixelFireJob`, `PixelLiquidJob` 등의 Burst Job 내부 난수 생성 로직을 틱 기반 `Unity.Mathematics.Random`으로 교체
- [ ] 병렬 Job 처리 시 데이터 경쟁(Data Race)으로 인한 비결정성 여부 조사
- [ ] 기존 `PixelGridSyncSystem` 비활성화
- [ ] 플레이어 액션(`WandCastSystem`, `TerrainPaintRpcSystem`)을 Netcode Command 기반으로 리팩토링
- [ ] 틱 단위 픽셀 배열 Hash 비교 디버깅 시스템 구축