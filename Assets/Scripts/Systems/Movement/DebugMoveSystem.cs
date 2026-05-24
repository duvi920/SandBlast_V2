using Unity.Entities;
using UnityEngine;

namespace SandBlast.Systems
{
    // 개발용 자유 이동 시스템 — 현재 비활성 상태 (스텁)
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial struct DebugMoveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // 의도적으로 비워 둠 (콘솔 스팸 방지).
        }

        public void OnUpdate(ref SystemState state)
        {
            // 의도적으로 비워 둠.
        }
    }
}
