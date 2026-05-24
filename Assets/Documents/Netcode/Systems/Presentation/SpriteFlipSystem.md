# SpriteFlipSystem.cs

## 역할
`PlayerGhostData.IsFacingRight` 값에 따라 플레이어 스프라이트를 좌우 반전시킨다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ClientSimulation` 전용 |
| UpdateGroup | `PresentationSystemGroup` |
| 기반 클래스 | `SystemBase` (Hybrid 컴포넌트 접근 필요) |

## SystemBase를 사용하는 이유
`SpriteRenderer`는 Unity의 `UnityEngine.Component`로 ECS 외부의 Hybrid 객체다. `ISystem` 구조체에서는 `EntityManager.GetComponentObject<T>()`를 호출할 수 없으므로 `SystemBase`(클래스 기반)를 사용한다.

## Hybrid 컴포넌트 접근
```csharp
var sr = EntityManager.GetComponentObject<SpriteRenderer>(entity);
sr.flipX = !ghostData.IsFacingRight;
```
이 방식은 ECS 엔티티에 `SpriteRenderer`가 Managed 컴포넌트로 추가되어 있어야 한다.

## PresentationSystemGroup
게임 로직이 모두 완료된 이후 마지막으로 실행되는 그룹. 렌더링/UI에 관련된 것만 여기에 배치한다.

## 관련 파일
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — IsFacingRight 읽기
- [`PlayerMoveSystem`](../Movement/PlayerMoveSystem.md) — IsFacingRight 설정
