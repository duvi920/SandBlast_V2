using Unity.Entities;
using Unity.NetCode;

namespace SandBlast
{
    // [Phase 2] 결정론적 동기화 전환을 위해 기존 RPC 기반 초기 동기화 비활성화.
    // 추후 맵 시드(Seed) 동기화 방식으로 대체 예정.
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class PixelGridInitialSyncSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            // TODO: 신규 접속 클라이언트에게 현재 맵 시드와 틱 번호를 동기화
        }
    }
}
