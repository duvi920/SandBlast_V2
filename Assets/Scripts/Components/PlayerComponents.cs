using Unity.Entities;
using Unity.Mathematics;

namespace SandBlast.Components
{
    /// <summary>
    /// 플레이어 엔티티를 식별하기 위한 태그 컴포넌트
    /// </summary>
    [Unity.NetCode.GhostComponent(PrefabType = Unity.NetCode.GhostPrefabType.All)]
    public struct PlayerTag : IComponentData { }

    /// <summary>
    /// 플레이어별 이동 속성 설정 (Baking 시점에 설정)
    /// </summary>
    [Unity.NetCode.GhostComponent(PrefabType = Unity.NetCode.GhostPrefabType.All)]
    public struct MovementConfig : IComponentData
    {
        public float MoveSpeed;
        public float JumpForce;
        public float DashSpeed;
        public float DashDuration;
    }

    /// <summary>
    /// 플레이어의 현재 이동 상태
    /// </summary>
    public enum PlayerMovementState : byte
    {
        Idle,
        Moving,
        Jumping,
        Dashing
    }

    [Unity.NetCode.GhostComponent(PrefabType = Unity.NetCode.GhostPrefabType.All)]
    public struct PlayerMovementData : IComponentData
    {
        public PlayerMovementState Value;
        public bool IsGrounded;
    }

    /// <summary>
    /// 하이브리드 ECS를 위해 SPUM 프리팹 참조를 보관하는 Managed Component
    /// </summary>
    public class PlayerVisualReference : IComponentData
    {
        public SPUM_Prefabs Prefabs;
        public bool Initialized;
    }
}
