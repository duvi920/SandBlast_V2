using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

[GhostComponent(PrefabType = GhostPrefabType.All)]
public struct PlayerInput : IInputComponentData
{
    public float2 MoveDirection;
    public InputEvent Jump;
    public InputEvent Shoot;
    public float AimAngle;
    public InputEvent Reload;
    public InputEvent Interact;
    public InputEvent Dash;
    public InputEvent MeleeAttack; // 근접 공격 (기본키: Q)
    public InputEvent UseShovel;   // 삽 사용 (기본키: E)

    // 디버그용 페인팅 액션
    public float2 PaintPos;
    public float  PaintRadius;
    public byte   PaintCellType;
    public byte   PaintFlammability;
    public bool   IsPainting;
}
