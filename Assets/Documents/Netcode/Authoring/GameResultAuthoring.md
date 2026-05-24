# GameResultAuthoring.cs

## 역할
`GameResultSingleton` 컴포넌트를 씬 로드 시 자동으로 생성하는 Baker.

## Baker 동작
Inspector 설정값 없이 항상 초기값으로 컴포넌트를 생성한다.

```csharp
AddComponent(entity, new GameResultSingleton
{
    WinnerNetworkId = -1,  // 아직 승자 없음
    GameEnded = false,
    IsDraw = false
});
```

## 씬 설정
SubScene 내 빈 GameObject 하나에 추가한다. 게임 당 반드시 하나만 존재해야 한다. `SurvivalCheckSystem`이 `RequireForUpdate`로 이 컴포넌트의 존재를 전제하지 않으므로 누락 시 `TryGetSingleton`이 false를 반환해 시스템이 조용히 스킵된다.

## 관련 파일
- [`GameResultSingleton`](../Components/Local/GameResultSingleton.md) — 생성 대상 컴포넌트
- [`SurvivalCheckSystem`](../Systems/BattleRoyale/SurvivalCheckSystem.md) — 값 갱신
