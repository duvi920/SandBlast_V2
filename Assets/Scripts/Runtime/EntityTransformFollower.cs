using Unity.Entities;
using UnityEngine;

namespace SandBlast
{
    /// <summary>
    /// ECS Entity의 LocalTransform을 읽어 GameObject Transform에 반영한다.
    /// 디버그/테스트에서 "엔티티는 움직이는데 화면에서 안 보이는" 문제를 피하기 위한 브리지.
    /// </summary>
    public sealed class EntityTransformFollower : MonoBehaviour
    {
        public string WorldNameHint;
        public int EntityIndex;
        public int EntityVersion;

        Entity Entity => new Entity { Index = EntityIndex, Version = EntityVersion };

        void LateUpdate()
        {
            World world = null;

            if (!string.IsNullOrEmpty(WorldNameHint))
            {
                foreach (var w in World.All)
                    if (w != null && w.IsCreated && w.Name == WorldNameHint) { world = w; break; }
            }

            world ??= World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            var em = world.EntityManager;
            var e  = Entity;
            if (!em.Exists(e)) return;
            if (!em.HasComponent<Unity.Transforms.LocalTransform>(e)) return;

            var lt = em.GetComponentData<Unity.Transforms.LocalTransform>(e);
            transform.position = lt.Position;
        }
    }
}

