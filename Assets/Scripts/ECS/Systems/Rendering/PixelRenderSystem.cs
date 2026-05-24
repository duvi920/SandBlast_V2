using Unity.Entities;
using UnityEngine;

namespace SandBlast
{
    // 매 프레임 PixelGridSingleton의 NativeArray를 Texture2D로 베이크해 화면에 출력한다.
    // PixelLayerRenderer MonoBehaviour(Texture2D / GPU 업로드)는 메인스레드 전용이므로 유지한다.
    // 렌더링은 클라이언트(혹은 단독 실행) 전용 — 서버 월드에서 실행되면 텍스처를 덮어써서 브러시가 안 보임
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation)]
    public partial class PixelRenderSystem : SystemBase
    {
        PixelLayerRenderer _renderer;

        protected override void OnCreate()
        {
            RequireForUpdate<PixelGridSingleton>();
        }

        protected override void OnStartRunning()
        {
            // 씬에서 PixelLayerRenderer를 찾는다 (씬 로드 후 최초 1회)
            _renderer = Object.FindFirstObjectByType<PixelLayerRenderer>();
        }

        protected override void OnUpdate()
        {
            if (_renderer == null)
            {
                _renderer = Object.FindFirstObjectByType<PixelLayerRenderer>();
                if (_renderer == null) return;
            }

            var grid = SystemAPI.GetSingleton<PixelGridSingleton>();

            // ECS NativeArray 데이터를 Texture2D로 업로드
            _renderer.UpdateTexture(grid.Type, grid.Lifetime, grid.Width, grid.Height);
        }
    }
}
