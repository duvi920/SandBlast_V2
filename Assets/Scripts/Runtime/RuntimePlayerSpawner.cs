using SandBlast.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace SandBlast
{
    public static class RuntimePlayerSpawner
    {
        public static (World world, Entity entity) SpawnLocalPlayer(Vector3 spawnPos)
        {
            var world = PickBestWorld();
            if (world == null || !world.IsCreated)
                return (null, Entity.Null);

            var em = world.EntityManager;

            var e = em.CreateEntity();

            em.AddComponentData(e, new PlayerGhostData
            {
                Position      = new float2(spawnPos.x, spawnPos.y),
                Velocity      = float2.zero,
                Health        = 100f,
                Armor         = 0f,
                IsDead        = false,
                IsFacingRight = true,
                WeaponId      = 0,
                AmmoCount     = 0,
                IsGrounded    = false,
                KillCount     = 0,
            });

            em.AddComponentData(e, LocalTransform.FromPosition(new float3(spawnPos.x, spawnPos.y, spawnPos.z)));
            em.AddComponentData(e, default(PlayerInput));
            em.AddComponentData(e, default(PlayerLocalState));
            em.AddComponentData(e, default(GroundedState));
            em.AddComponentData(e, new PlayerMovementData { Value = PlayerMovementState.Idle, IsGrounded = false });

            em.AddComponent<PlayerTag>(e);
            em.AddComponent<GhostOwnerIsLocal>(e);
            em.AddComponent<Simulate>(e);

            return (world, e);
        }

        static World PickBestWorld()
        {
            World fallback = World.DefaultGameObjectInjectionWorld;
            World client = null;
            World local  = null;

            foreach (var w in World.All)
            {
                if (w == null || !w.IsCreated) continue;
                var name = w.Name ?? string.Empty;
                if (client == null && name.Contains("Client")) client = w;
                if (local == null && name.Contains("Local"))   local  = w;
            }

            return client ?? local ?? fallback;
        }
    }
}

