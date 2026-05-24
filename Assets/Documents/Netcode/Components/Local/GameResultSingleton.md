# GameResultSingleton.cs

## 역할
게임 종료 상태와 승자 정보를 저장하는 서버 싱글턴 컴포넌트. `SurvivalCheckSystem`이 매 틱 생존자를 확인해 이 값을 갱신한다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `WinnerNetworkId` | `int` | 승자의 NetworkId. 무승부이거나 게임 진행 중이면 `-1` |
| `GameEnded` | `bool` | 게임 종료 여부. `true`가 되면 `SurvivalCheckSystem`이 더 이상 집계하지 않음 |
| `IsDraw` | `bool` | `true`이면 동시 사망에 의한 무승부 |

## 싱글턴 접근 방법
```csharp
// 읽기
var result = SystemAPI.GetSingleton<GameResultSingleton>();

// 읽기/쓰기
var result = SystemAPI.GetSingletonRW<GameResultSingleton>();
result.ValueRW.GameEnded = true;
```

## 씬 설정
SubScene 내 `GameResultAuthoring` 컴포넌트가 붙은 GameObject가 Baker에서 이 컴포넌트를 엔티티에 추가한다.

## 관련 파일
- [`GameResultAuthoring`](../../Authoring/GameResultAuthoring.md) — Baker에서 초기값 설정
- [`SurvivalCheckSystem`](../../Systems/BattleRoyale/SurvivalCheckSystem.md) — 값 갱신
