using Unity.Entities;

// 서버 전용 로컬 — 스턴 게이지와 기절 타이머를 보관한다. Ghost 불필요.
public struct StunStateComponent : IComponentData
{
    public float StunAccumulator; // 현재 스턴 게이지 (0~100)
    public float StunTimer;       // 남은 기절 시간 (초)
    public float MeleeAttackCooldown; // 근접 공격 쿨다운 타이머
}
