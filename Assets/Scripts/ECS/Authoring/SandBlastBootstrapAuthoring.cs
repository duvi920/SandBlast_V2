using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SandBlast
{
    // Baker가 읽어 SandBlastBootstrapSystem에 전달하는 설정 컴포넌트
    public struct SandBlastConfigComponent : IComponentData
    {
        public int    GridWidth;
        public int    GridHeight;
        public float  PixelsPerUnit;
        public float  TicksPerSecond;
        public float2 GridOrigin;
    }

    // 씬에 배치해 그리드 설정을 지정하는 Authoring MonoBehaviour.
    // 기존 SandBlastEngine MonoBehaviour를 대체한다 (Phase 6에서 삭제).
    public class SandBlastBootstrapAuthoring : MonoBehaviour
    {
        [Header("Grid")]
        public int     GridWidth     = 320;
        public int     GridHeight    = 180;
        public float   PixelsPerUnit = 16f;
        public Vector2 GridOrigin    = Vector2.zero;

        [Header("Simulation")]
        public float TicksPerSecond = 30f;

        bool _injected;

        // NetCode(ClientServerBootstrap)는 플레이 시작 후 월드를 생성/재생성할 수 있다.
        // Start 한 번만으로는 월드가 아직 없어 설정 컴포넌트를 주입하지 못하는 경우가 있어,
        // 월드가 준비될 때까지 몇 프레임 재시도한다.
        void Update()
        {
            if (_injected) return;

            var cfg = new SandBlastConfigComponent
            {
                GridWidth      = GridWidth,
                GridHeight     = GridHeight,
                PixelsPerUnit  = PixelsPerUnit,
                TicksPerSecond = TicksPerSecond,
                GridOrigin     = new float2(GridOrigin.x, GridOrigin.y),
            };

            bool injectedAny = false;
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;
                var em = world.EntityManager;

                // 씬 로드 Job이 EntityManager를 쓰는 중일 수 있으므로 안전하게 스킵
                try
                {
                    using var configQ = em.CreateEntityQuery(
                        ComponentType.ReadOnly<SandBlastConfigComponent>());
                    using var gridQ = em.CreateEntityQuery(
                        ComponentType.ReadOnly<PixelGridSingleton>());

                    if (!configQ.IsEmpty || !gridQ.IsEmpty) continue;

                    var e = em.CreateEntity();
                    em.AddComponentData(e, cfg);
                    injectedAny = true;
                }
                catch (System.InvalidOperationException)
                {
                    // EntityManager가 Job에 의해 잠긴 경우 다음 프레임에 재시도
                }
            }

            // 월드가 아직 없는 프레임이면 다음 프레임에 재시도
            if (injectedAny)
                _injected = true;
        }
    }

    public class SandBlastBootstrapBaker : Baker<SandBlastBootstrapAuthoring>
    {
        public override void Bake(SandBlastBootstrapAuthoring src)
        {
            var e = GetEntity(TransformUsageFlags.None);
            AddComponent(e, new SandBlastConfigComponent
            {
                GridWidth     = src.GridWidth,
                GridHeight    = src.GridHeight,
                PixelsPerUnit = src.PixelsPerUnit,
                TicksPerSecond = src.TicksPerSecond,
                GridOrigin    = new float2(src.GridOrigin.x, src.GridOrigin.y),
            });
        }
    }
}
