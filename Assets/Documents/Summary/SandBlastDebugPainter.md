# SandBlastDebugPainter

> `Assets/Scripts/Debug/SandBlastDebugPainter.cs`

## 역할

런타임 디버그 페인터. 마우스로 셀을 직접 배치·지워 CA 시뮬레이션을 즉시 확인할 수 있다.  
`SandBlastEngine` 과 같은 GameObject에 부착한다.

## 조작법

| 입력 | 동작 |
|---|---|
| 좌클릭 드래그 | 선택된 셀 타입으로 원형 브러시 페인팅 |
| 우클릭 드래그 | 지우기 (EMPTY) |
| 스크롤 휠 | 브러시 크기 조절 (0.1 ~ 8.0 worldUnit) |
| Tab | 팔레트에서 다음 셀 타입 선택 |
| F1 | 디버그 패널 표시/숨김 |

## 팔레트 순서

1. POWDER_SAND
2. POWDER_ASH
3. LIQUID_WATER
4. LIQUID_LAVA
5. FIRE
6. GAS_SMOKE
7. SOLID_STATIC _(FLAMMABILITY_WOOD 자동 적용)_
8. SOLID_DEBRIS
9. EMPTY

## 페인팅 로직

```csharp
// 화면 좌표 → 월드 좌표 변환
Ray ray = Camera.main.ScreenPointToRay(mousePos);
engine.SpawnCircle(worldPos, brushRadius, paint, flamm);
```

`SOLID_STATIC` 선택 시 `FLAMMABILITY_WOOD(80)` 자동 적용.

## GUI 패널 (OnGUI)

- 팔레트별 컬러 스와치 + 이름 토글
- 브러시 반지름 슬라이더
- F1 숨김 시 힌트 레이블만 표시

## 관련 파일

- [SandBlastEngine.md](SandBlastEngine.md) — SpawnCircle 호출 대상
- [CellType.md](CellType.md) — 팔레트 타입 정의
