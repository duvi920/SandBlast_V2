# SurvivalCheckSystem.cs

## 역할
매 틱 살아있는 플레이어 수를 집계해 배틀로얄 종료 조건을 판정한다. 최후 1인 생존 시 승리, 동시 사망 시 무승부를 기록하고 게임을 종료한다.

## 시스템 속성

| 항목 | 값 |
|------|----|
| World | `ServerSimulation` 전용 |
| UpdateGroup | `SimulationSystemGroup` |
| 실행 순서 | `DamageSystem` 이후 |

## 판정 로직
```
aliveCount = IsDead == false인 플레이어 수 집계

aliveCount == 1  → GameEnded = true, WinnerNetworkId = 생존자 NetworkId
aliveCount == 0  → GameEnded = true, IsDraw = true
GameEnded == true → 이후 틱에서 집계 스킵
```

## GameEnded 이후 동작
현재 구현은 `GameResultSingleton`에 결과를 기록하는 것까지만 처리한다. 게임 종료 이후의 추가 처리(결과 화면 전환, 로비 복귀, 플레이어 입력 잠금 등)는 `GameResultSingleton.GameEnded`를 감시하는 별도 시스템에서 구현한다.

## 관련 파일
- [`PlayerGhostData`](../../Components/Ghost/PlayerGhostData.md) — IsDead 읽기
- [`GameResultSingleton`](../../Components/Local/GameResultSingleton.md) — 결과 기록
- [`DamageSystem`](../Combat/DamageSystem.md) — IsDead 설정 주체
