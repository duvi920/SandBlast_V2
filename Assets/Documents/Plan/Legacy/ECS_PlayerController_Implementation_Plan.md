# ECS 기반 PlayerController 구현 계획 (완료)

## 1. 개요
기존의 MonoBehaviour 기반 `PlayerController`를 Unity ECS(Entities) 아키텍처로 전환하여 성능을 최적화하고 SPUM 애니메이션 시스템과 성공적으로 통합함.

---

## 2. 설계 및 구현 결과

### A. Components (Data) - 완료
- `PlayerTag`: 플레이어 식별용 태그.
- `MovementConfig`: 이동 속도, 점프 힘 등 설정 데이터.
- `PlayerState`: 현재 이동 상태(`Idle`, `Moving`, `Jumping`, `Dashing`) 및 지면 접촉 여부 보관.
- `PlayerVisualReference` (Managed): SPUM `SPUM_Prefabs` 컴포넌트 참조 보관.

### B. Systems (Logic) - 완료
1. `GatherInputSystem`: (기존 활용) Input System에서 입력을 수집하여 `PlayerInput`에 기록.
2. `PlayerMoveSystem`: (업데이트) 이동 물리 연산과 함께 `PlayerState`를 실시간으로 갱신.
3. `PlayerVisualSystem`: (신규) ECS 상태를 SPUM Animator 및 Transform(Flip)에 전달.

### C. Authoring (Baking) - 완료
- `PlayerAuthoring.cs`: 설정값과 SPUM 프리팹 참조를 받아 ECS 엔티티로 변환.

---

## 3. 구현 단계별 결과

### Step 1: 기본 컴포넌트 생성 [V]
- `PlayerComponents.cs`에 핵심 데이터 구조 정의 완료.

### Step 2: Authoring 스크립트 작성 [V]
- `PlayerAuthoring.cs`를 통해 하이브리드 베이킹 환경 구축 완료.

### Step 3: 입력 시스템 구축 [V]
- 기존 `GatherInputSystem` 및 `PlayerInput` 컴포넌트(Netcode 표준)와 연동 완료.

### Step 4: 이동 로직 구현 [V]
- `PlayerMoveSystem`에서 예측(Prediction) 기반 이동 및 애니메이션 상태 전이 로직 통합 완료.

### Step 5: SPUM 애니메이션 연동 (Hybrid) [V]
- `PlayerVisualSystem`을 통해 ECS 엔티티와 GameObject 기반 SPUM 애니메이션의 실시간 동기화 완료.

---

## 4. 향후 작업 추천
- **레거시 제거:** 테스트 완료 후 기존 `PlayerController.cs` (MonoBehaviour) 및 관련 컴포넌트 삭제.
- **물리 충돌 고도화:** 현재 이동 시스템에서 픽셀 지형과의 충돌 판정(Collision) 로직 추가 보완.
- **전투 시스템 연동:** `PlayerInput`의 `Shoot` 이벤트를 사용하여 ECS 기반 투사체 발사 시스템과 연동.

---
> **작성일:** 2026-04-25  
> **상태:** 구현 완료 및 검증 대기
