# CameraFollowSystem.cs

## 역할
`GhostOwnerIsLocal` 태그가 붙은 로컬 플레이어 Ghost를 부드럽게 카메라로 추적한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ClientSimulation` 전용 |
| UpdateGroup | `PresentationSystemGroup` |
| 기반 구조체 | `ISystem` (Camera.main은 `OnUpdate`에서 직접 접근) |

## 추적 로직
```
Lerp(
    현재 카메라 위치,
    new Vector3(player.x, player.y, cam.z),  ← Z는 카메라 고정
    SmoothSpeed * deltaTime
)
```
`SmoothSpeed = 5f`로 완전히 즉각 추적하지 않고 약간의 지연(부드러운 추적)을 준다.

## 왜 첫 번째 쿼리 결과만 처리하는가
`GhostOwnerIsLocal`이 붙은 플레이어 Ghost는 클라이언트 당 항상 하나다. `break`로 첫 번째 결과만 처리하고 루프를 종료한다.

## Camera.main 의존성
`Camera.main`은 `MainCamera` 태그가 붙은 첫 번째 카메라를 반환한다. 씬에 `MainCamera` 태그 카메라가 없으면 `null`을 반환하므로 null 체크가 포함되어 있다.

## 관련 파일
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — 필터로 사용
- [`GatherInputSystem`](../Input/GatherInputSystem.md) — 같은 GhostOwnerIsLocal 패턴
