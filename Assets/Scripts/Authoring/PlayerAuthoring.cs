using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;
using SandBlast.Components;
using SandBlast.Wand;

namespace SandBlast.Authoring
{
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float MoveSpeed = 6.0f;
        public float JumpForce = 14.0f;
        public float DashSpeed = 20.0f;
        public float DashDuration = 0.14f;

        [Header("Animation")]
        public SPUM_Prefabs SpumPrefabs;

        public class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // 핵심 컴포넌트 추가
                AddComponent<PlayerTag>(entity);
                AddComponent<PlayerInput>(entity);
                AddComponent(entity, new PlayerGhostData
                {
                    Position = new float2(authoring.transform.position.x, authoring.transform.position.y),
                    Health = 100,
                    IsFacingRight = true
                });
                AddComponent<PlayerLocalState>(entity);
                AddComponent<GroundedState>(entity);
                
                // GhostOwner는 GhostAuthoringComponent Baker가 자동으로 추가한다. 중복 추가 금지.
                AddComponent<Simulate>(entity);
                
                // 이동 설정 데이터 추가
                AddComponent(entity, new MovementConfig
                {
                    MoveSpeed = authoring.MoveSpeed,
                    JumpForce = authoring.JumpForce,
                    DashSpeed = authoring.DashSpeed,
                    DashDuration = authoring.DashDuration
                });

                // 기절 시스템
                AddComponent<StunStateComponent>(entity);

                // 초기 상태 추가
                AddComponent(entity, new PlayerMovementData
                {
                    Value = PlayerMovementState.Idle,
                    IsGrounded = true
                });

                // 완드 시스템 초기화 — 기본 완드(슬롯 0)로 시작
                AddComponent(entity, new WandComponent
                {
                    ActiveSlot    = 0,
                    CastCooldown  = 0f,
                    CastInterval  = 0.08f,
                    IsAutoCast    = true,
                });
                AddComponent(entity, new ManaComponent
                {
                    Current   = 100f,
                    Max       = 100f,
                    RegenRate = 8f,
                });
                var spells = AddBuffer<SpellData>(entity);
                spells.Add(new SpellData
                {
                    ProjectileType  = ProjectileType.Sand,
                    LiquidPayload   = LiquidPayload.None,
                    Modifier        = SpellModifier.None,
                    ManaCost        = 2f,
                    Damage          = 5f,
                    ProjectileSpeed = 14f,
                    BlastRadius     = 0.8f,
                    PenetrateCount  = 0,
                });

                if (authoring.SpumPrefabs != null)
                {
                    AddComponentObject(entity, new PlayerVisualReference
                    {
                        Prefabs = authoring.SpumPrefabs
                    });
                }
            }
        }
    }
}
