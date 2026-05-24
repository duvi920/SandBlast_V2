using Unity.Entities;
using Unity.Mathematics;

public struct HitEvent : IBufferElementData
{
    public Entity TargetEntity;
    public float Damage;
    public float2 HitPosition;
    public float StunPower; // 0이면 스턴 없음
}
