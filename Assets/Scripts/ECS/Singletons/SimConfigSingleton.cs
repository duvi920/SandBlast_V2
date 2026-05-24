using Unity.Collections;
using Unity.Entities;

namespace SandBlast
{
    // 시뮬레이션 설정 및 틱 상태를 보관하는 ECS 싱글턴.
    // SandBlastEngine의 tickTimer·currentTick과 PixelSimulator의 cellUpdateTick·xShuffle을 통합한다.
    public struct SimConfigSingleton : IComponentData
    {
        public float TicksPerSecond;
        public float TickTimer;
        public int   CurrentTick;

        // Fisher-Yates 셔플용 버퍼 (가루/잔해 좌우 편향 방지)
        public NativeArray<int> XShuffle;

        // 셀 이중 처리 방지 카운터 — Array.Clear() 없이 currentTick 비교로 초기화
        public NativeArray<int> CellUpdateTick;
    }
}
