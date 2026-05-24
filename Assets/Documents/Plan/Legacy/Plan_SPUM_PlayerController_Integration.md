# SPUM 캐릭터와 PlayerController 연동 계획

## 개요
SPUM으로 만든 2D 스프라이트 캐릭터를 기존 `PlayerController`와 연동하여 이동, 점프, 대시 등의 동작에 맞는 애니메이션을 재생하도록 한다.

---

## 1. 현재 상태 분석

### PlayerController (기존)
- **위치**: `Assets/Scripts/Player/PlayerController.cs`
- **기능**: 키보드 입력 기반 이동, 점프, 대시 구현
- **물리**: Rigidbody2D + CapsuleCollider2D 사용
- **상태 플래그**: `_grounded`, `_dashing`

### SPUM 캐릭터 구조
- **컴포넌트**: `SPUM_Prefabs` (Animtor, 애니메이션 클립 관리)
- **상태 열거형**: `PlayerState` (IDLE, MOVE, ATTACK, DAMAGED, DEBUFF, DEATH, OTHER)
- **애니메이션 클립**: IDLE_List, MOVE_List, ATTACK_List, DAMAGED_List, DEBUFF_List, DEATH_List, OTHER_List

---

## 2. 구현 단계

### Step 1: SPUM 캐릭터 프리팹 생성
- [ ] SPUM 에디터에서 캐릭터 생성
- [ ] 생성된 캐릭터 GameObject에 `SPUM_Prefabs` 컴포넌트 확인
- [ ] Animator Controller 설정 확인
- [ ] 프로젝트의 Prefabs 폴더에 저장

### Step 2: PlayerController에 애니메이션 연동 추가
- [x] `SPUM_Prefabs` 참조 변수 추가
- [x] Animator 참조 확보 (`GetComponentInChildren<Animator>()`)
- [x] 이동 방향에 따른 스프라이트 플리핑 로직 추가
- [x] 상태 변경 메서드 구현 (`SetAnimationState(PlayerState)`)

### Step 3: 상태 매핑 구현
- [x] 상태 매핑 로직 구현 (DetermineAnimationState)
| PlayerController 상태 | SPUM PlayerState |
|----------------------|------------------|
| 정지 (속도 ≈ 0)      | IDLE             |
| 이동 중              | MOVE             |
| 대시 중              | MOVE             |
| 점프 (空中)          | MOVE             |
| 피격 시              | DAMAGED (TBD)    |
| 사망 시              | DEATH (TBD)      |

### Step 4: 애니메이션 클립 설정
- [ ] SPUM에서 애니메이션 클립 Export
- [ ] Animator Controller에 상태 추가
- [ ] Transition 설정 (Idle ↔ Move 등)

---

## 3. 코드 변경 사항

### PlayerController.cs 수정
```csharp
// 추가할 필드
[Header("Animation")]
public SPUM_Prefabs _spumPrefabs;
private Animator _animator;
private PlayerState _currentAnimState = PlayerState.IDLE;

// 추가할 메서드
private void InitAnimation()
{
    if (_spumPrefabs != null)
        _animator = _spumPrefabs.GetComponent<Animator>();
    else
        _animator = GetComponentInChildren<Animator>();
}

private void UpdateAnimation()
{
    PlayerState newState = DetermineAnimationState();
    if (newState != _currentAnimState)
    {
        _currentAnimState = newState;
        _spumPrefabs?.PlayStateAnimation(newState);
    }
}

private PlayerState DetermineAnimationState()
{
    if (!_grounded) return PlayerState.MOVE;
    if (Mathf.Abs(_velocity.x) > 0.1f) return PlayerState.MOVE;
    return PlayerState.IDLE;
}

private void UpdateSpriteDirection()
{
    if (_spumPrefabs != null && _velocity.x != 0)
    {
        float dir = Mathf.Sign(_velocity.x);
        _spumPrefabs.transform.localScale = new Vector3(-dir, 1, 1);
    }
}
```

---

## 4. 테스트 항목

- [ ] 캐릭터가 좌우 이동 시 MOVE 애니메이션 재생
- [ ] 정지 시 IDLE 애니메이션으로 전환
- [ ] 점프/대시 시 애니메이션 정상 동작
- [ ] 방향 전환 시 스프라이트 플리핑
- [ ] 다른 상태 (ATTACK, DAMAGED 등) 수동 트리거 테스트

---

## 5. 참고 파일

| 파일 | 용도 |
|------|------|
| `Assets/SPUM/Core/Script/Data/SPUM_Prefabs.cs` | 애니메이션 상태 관리 |
| `Assets/SPUM/Sample/Script/PlayerObj.cs` | 기존 SPUM 플레이어 예시 |
| `Assets/Scripts/Player/PlayerController.cs` | 현재 플레이어 컨트롤러 |
| `Assets/SPUM/Sample/Prefabs/SamplePlayer.prefab` | SPUM 샘플 프리팹 |

---

## 6. 예상 소요 시간

- Step 1 (프리팹 생성): 10분
- Step 2-3 (코드 연동): 20분
- Step 4 (애니메이션 설정): 15분
- 테스트 및 디버깅: 15분

**총 예상 시간: 약 60분**