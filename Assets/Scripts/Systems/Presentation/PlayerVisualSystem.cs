using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using SandBlast.Components;

namespace SandBlast.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct PlayerVisualSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (visualRef, playerState, ghostData) in SystemAPI
                .Query<PlayerVisualReference, RefRO<PlayerMovementData>, RefRO<PlayerGhostData>>())
            {
                if (visualRef.Prefabs == null) continue;

                // 첫 프레임에 외형 랜덤화
                if (!visualRef.Initialized)
                {
                    visualRef.Initialized = true;
                    SpumAppearanceRandomizer.Randomize(visualRef.Prefabs, ghostData.ValueRO.NetworkId);

                    if (!visualRef.Prefabs.allListsHaveItemsExist())
                        visualRef.Prefabs.PopulateAnimationLists();
                    visualRef.Prefabs.OverrideControllerInit();
                }

                // 애니메이션 상태 업데이트
                global::PlayerState spumState = MapToSpumState(playerState.ValueRO.Value);
                visualRef.Prefabs.PlayStateAnimation(spumState);

                // 방향 전환
                float targetScaleX = ghostData.ValueRO.IsFacingRight ? -1f : 1f;
                if (visualRef.Prefabs.transform.localScale.x != targetScaleX)
                    visualRef.Prefabs.transform.localScale = new Vector3(targetScaleX, 1f, 1f);
            }
        }

        private global::PlayerState MapToSpumState(PlayerMovementState state)
        {
            return state switch
            {
                PlayerMovementState.Idle    => global::PlayerState.IDLE,
                PlayerMovementState.Moving  => global::PlayerState.MOVE,
                PlayerMovementState.Jumping => global::PlayerState.MOVE,
                PlayerMovementState.Dashing => global::PlayerState.MOVE,
                _                           => global::PlayerState.IDLE
            };
        }
    }
}
