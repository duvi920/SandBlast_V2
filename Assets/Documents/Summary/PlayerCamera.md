# PlayerCamera

`PlayerCamera`는 플레이어를 부드럽게 추적하는 직교(Orthographic) 카메라 컨트롤러입니다.

## 개요
- **네임스페이스**: `SandBlast`
- **설명**: 플레이어의 위치를 부드럽게 따라가며, 설정에 따라 카메라 뷰포트가 픽셀 그리드 경계 밖으로 나가지 않도록 제한합니다.
- **주요 기능**: `Vector3.SmoothDamp`를 사용한 부드러운 카메라 이동 및 그리드 경계 클램핑.

## 주요 속성
| 속성 | 타입 | 설명 |
| :--- | :--- | :--- |
| `Target` | `Transform` | 카메라가 추적할 대상(주로 플레이어)의 Transform입니다. |
| `SmoothTime` | `float` | `SmoothDamp` 추적 시간(초)입니다. 값이 낮을수록 카메라가 대상을 더 즉각적으로 따라갑니다. |
| `ClampToBounds` | `bool` | 활성화 시, 카메라 뷰포트가 픽셀 그리드의 경계 내에 머물도록 제한합니다. |

## 주요 로직
- **Awake**: 카메라 컴포넌트와 `SandBlastEngine` 인스턴스를 캐싱합니다.
- **LateUpdate**: 
  - 플레이어의 위치를 기반으로 목표 위치(`desired`)를 계산합니다.
  - `ClampToBounds`가 활성화된 경우, `SandBlastEngine`의 그리드 크기와 카메라의 가로/세로 절반 크기를 계산하여 카메라 위치가 그리드 밖으로 벗어나지 않도록 클램핑합니다.
  - `Vector3.SmoothDamp`를 호출하여 최종적으로 카메라의 위치를 업데이트합니다.
