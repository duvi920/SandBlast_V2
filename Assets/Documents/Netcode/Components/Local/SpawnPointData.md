# SpawnPointData.cs

## 역할
맵에 배치된 아이템 스폰 위치를 나타내는 컴포넌트. `ItemSpawnSystem`이 이 데이터를 읽어 게임 시작 시 아이템을 생성한다.

## 필드 목록

| 필드 | 타입 | 설명 |
|------|------|------|
| `Position` | `float2` | 스폰 지점의 월드 좌표 |
| `SpawnPointId` | `int` | 스폰 포인트 고유 ID |
| `IsOccupied` | `bool` | 현재 아이템이 이미 놓여 있는지 여부 (런타임 보충 로직에 활용) |

## 씬 설정
SubScene 내 각 스폰 위치에 GameObject를 배치하고 `SpawnPointAuthoring`을 추가한다. Baker가 GameObject의 월드 위치를 `Position`으로 자동 기록한다.

## 관련 파일
- [`SpawnPointAuthoring`](../../Authoring/SpawnPointAuthoring.md) — Baker
- [`ItemSpawnSystem`](../../Systems/Items/ItemSpawnSystem.md) — Position을 읽어 아이템 생성
