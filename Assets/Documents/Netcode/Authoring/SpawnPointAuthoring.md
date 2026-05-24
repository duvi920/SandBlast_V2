# SpawnPointAuthoring.cs

## 역할
맵에 배치된 아이템 스폰 위치 GameObject를 `SpawnPointData` 컴포넌트로 변환하는 Baker.

## Inspector 설정값

| 필드 | 설명 |
|------|------|
| `SpawnPointId` | 이 스폰 포인트의 고유 번호 |

## Baker 동작
GameObject의 `transform.position`을 자동으로 `SpawnPointData.Position`에 저장한다. 씬에서 직접 드래그해 위치를 지정하면 된다.

```csharp
var pos = authoring.transform.position;
Position = new float2(pos.x, pos.y)
```

## 씬 배치 방법
1. SubScene 내부에 빈 GameObject 생성
2. `SpawnPointAuthoring` 컴포넌트 추가
3. `SpawnPointId` 설정 (0, 1, 2, ...)
4. GameObject를 원하는 위치로 이동

에디터에서 Gizmo를 추가하면 스폰 위치를 시각적으로 확인할 수 있다.

## 관련 파일
- [`SpawnPointData`](../Components/Local/SpawnPointData.md) — 변환 대상 컴포넌트
- [`ItemSpawnSystem`](../Systems/Items/ItemSpawnSystem.md) — 스폰 위치 읽기
