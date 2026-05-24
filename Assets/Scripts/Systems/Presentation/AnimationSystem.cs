using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

[UpdateInGroup(typeof(PresentationSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class AnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithoutBurst()
            .WithAll<PlayerGhostData, GroundedState>()
            .ForEach((Animator animator, in PlayerGhostData ghostData, in GroundedState groundedState) =>
            {
                if (animator == null) return;

                animator.SetBool("IsWalking", math.abs(ghostData.Velocity.x) > 0.1f);
                animator.SetBool("IsJumping", !groundedState.IsGrounded && ghostData.Velocity.y > 0f);
                animator.SetBool("IsFalling", !groundedState.IsGrounded && ghostData.Velocity.y < 0f);
                animator.SetBool("IsDead", ghostData.IsDead);
            }).Run();
    }
}
